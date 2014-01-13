using OSX.WmiLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SDImager
{
    public partial class frmMain : Form
    {
        bool m_DrivesFound;
        CancellationTokenSource cts;
        string Operation;

        public frmMain()
        {
            InitializeComponent();
        }

        private void DriveChanged(object sender, EventArrivedEventArgs e)
        {
            //DumpChangedProperties((ManagementBaseObject)e.NewEvent["PreviousInstance"], (ManagementBaseObject)e.NewEvent["TargetInstance"]);
            FillDriveList();
        }

        //private void DumpChangedProperties(ManagementBaseObject oldObj, ManagementBaseObject newObj)
        //{
        //    foreach (var p in oldObj.Properties)
        //    {
        //        var v1 = p.Value == null ? "(null)" : p.Value.ToString();
        //        var v2 = newObj[p.Name] == null ? "(null)" : newObj[p.Name].ToString();
        //        if (v1 != v2)
        //        {
        //            Debug.WriteLine("{0}: {1} => {2}", p.Name, v1, v2);
        //        }
        //    }
        //}

        private void frmMain_Load(object sender, EventArgs e)
        {
#if DEBUG
            btnWipe.Visible = true;
#endif
            DriveTools.StartDriveChangeNotification(DriveChanged);
            FillDriveList();
            ResetProgress();
        }

        private void FillDriveList()
        {
            Task.Run(new Action(LoadDiskDrives));
        }

        private void LoadDiskDrives()
        {
            m_DrivesFound = false;
            string m_SelectedID = null;
            Invoke(new Action(() =>
            {
                if (lstDiskDrive.SelectedItem != null)
                    m_SelectedID = lstDiskDrive.SelectedItem.ToString();
                lstDiskDrive.DataSource = null;
                lstDiskDrive.Items.Clear();
                lstDiskDrive.Items.Add("(loading...)");
                lstDiskDrive.SelectedIndex = 0;
                lstDiskDrive.Refresh();
                WmiInfo.LoadDiskInfo();
                var l = DriveTools.GetRemovableDiskDrives().OrderBy(z => z.ID).ToList();
                lstDiskDrive.Items.Clear();
                if (l.Count == 0)
                {
                    lstDiskDrive.Items.Add("(No removable disk drives found)");
                    lstDiskDrive.SelectedIndex = 0;
                }
                else
                {
                    lstDiskDrive.DataSource = l;
                    m_DrivesFound = true;
                    var o = l.FirstOrDefault(z => z.ID == m_SelectedID);
                    if (o != null)
                        lstDiskDrive.SelectedItem = o;
                    else
                        lstDiskDrive_SelectedIndexChanged(this, EventArgs.Empty);
                }
                lstDiskDrive.Refresh();
            }));
        }

        private void btnChooseFile_Click(object sender, EventArgs e)
        {
            ofdFilename.FileName = txtFilename.Text;
            if (ofdFilename.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtFilename.Text = ofdFilename.FileName;
            }
        }

        private bool TryLockVolumes(DiskDrive disk)
        {
            try
            {
                disk.LockVolumes();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Could not lock volume(s) on disk drive {0}. Please ensure that no other program is accessing this drive.\n\nException: {1}",
                    disk.ID, ex.Message), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;

            }
        }

        private async void btnRead_Click(object sender, EventArgs e)
        {
            DiskDrive dd = (DiskDrive)lstDiskDrive.SelectedItem;
            if (!CheckFilename(txtFilename.Text, dd, true))
            {
                txtFilename.Focus();
                return;
            }
            var filename = Path.GetFullPath(txtFilename.Text);

            Operation = "Reading";
            if (!TryLockVolumes(dd)) return;
            SetButtons(false);
            using (Stream dest = new FileStream(txtFilename.Text, FileMode.Create, FileAccess.Write, FileShare.None),
                source = dd.CreateStream(FileAccess.Read))
            {
                progress.Maximum = (int)(dd.GetDriveSize() / (1024 * 1024)) + 1;
                progress.Value = 0;
                cts = new CancellationTokenSource();
                try
                {
                    await CopyStreamAsync(source, dest, (long)dd.GetDriveSize(), cts.Token, null);
                }
                catch (Exception ex)
                {
                    if (!(ex is OperationCanceledException))
                        MessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK);
                    cts.Cancel();
                }
            }
            dd.UnlockVolumes();

            if (!cts.IsCancellationRequested)
                MessageBox.Show("Reading complete.", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ResetProgress();
            SetButtons(true);
            cts = null;
        }

        private async void btnWrite_Click(object sender, EventArgs e)
        {
            DiskDrive dd = (DiskDrive)lstDiskDrive.SelectedItem;
            if (!CheckFilename(txtFilename.Text, dd, false))
            {
                txtFilename.Focus();
                return;
            }
            var filename = Path.GetFullPath(txtFilename.Text);
            if (MessageBox.Show("All data on SD card will be erased and overwritten. Are you sure?", "Warning",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.No) return;

            Operation = "Writing";
            if (!TryLockVolumes(dd)) return;
            SetButtons(false);
            using (Stream source = new FileStream(txtFilename.Text, FileMode.Open, FileAccess.Read, FileShare.Read),
                    dest = dd.CreateStream(FileAccess.Write))
            {
                progress.Maximum = (int)(source.Length / (1024 * 1024)) + 1;
                progress.Value = 0;
                cts = new CancellationTokenSource();
                try
                {
                    await CopyStreamAsync(source, dest, source.Length, cts.Token, null);
                }
                catch (Exception ex)
                {
                    if (!(ex is OperationCanceledException))
                        MessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK);
                    cts.Cancel();
                }
            }
            dd.DismountVolumes();
            dd.UnlockVolumes();
            dd.MountVolumes();

            FillDriveList();
            if (!cts.IsCancellationRequested)
                MessageBox.Show("Writing complete.", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ResetProgress();
            cts = null;
        }

        private bool CheckFilename(string filename, DiskDrive dd, bool write)
        {
            if (dd == null)
            {
                MessageBox.Show("Choose valid SD drive first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (filename.Length == 0)
            {
                MessageBox.Show("Image file name is missing.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            filename = Path.GetFullPath(filename);
            if (dd.Volumes.Any(z => z.Name == Path.GetPathRoot(filename)))
            {
                MessageBox.Show("Image file must not be located on SD drive.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (write && File.Exists(filename))
            {
                if (MessageBox.Show("Image file already exists. Do you want to overwrite?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.No)
                    return false;
            }
            return true;
        }

        private void ResetProgress()
        {
            progress.Value = 0;
            lblSpeed.Text = "(n/a)";
            lblByteRemaining.Text = lblSpeed.Text;
            lblTimeRemaining.Text = lblSpeed.Text;
        }

        private async Task CopyStreamAsync(Stream source, Stream dest, long count, CancellationToken token,
            Action<long> ProgressChanged)
        {
            byte[][] buf = new byte[2][];
            buf[0] = new byte[1024 * 1024];
            buf[1] = new byte[1024 * 1024];
            var oldCaption = this.Text;

            int len = 0, lasti = 0;
            Task writeTask;
            Task<int> readTask;
            long remaining = count, written = 0;
            Stopwatch sw = Stopwatch.StartNew();
            if (ProgressChanged != null)
                ProgressChanged(0);

            int bufIdx = 0;

            // initially start read task
            readTask = source.ReadAsync(buf[bufIdx], 0, (int)Math.Min(remaining, buf[0].Length));
            do
            {
                // check token for cancellation
                token.ThrowIfCancellationRequested();

                // wait for read task
                len = await readTask;
                remaining -= len;
                written += len;

                // start write task because reading is finished
                writeTask = dest.WriteAsync(buf[bufIdx], 0, len);

                // switch buffers
                bufIdx = (bufIdx + 1) % 2;

                // start next read task if necessary
                if (remaining > 0)
                    readTask = source.ReadAsync(buf[bufIdx], 0, (int)Math.Min(remaining, buf[0].Length));

                // wait for write task
                await writeTask;

                //update progress
                progress.Value += 1;
                if (sw.ElapsedMilliseconds >= 500)
                {
                    if (ProgressChanged != null)
                        ProgressChanged(written);
                    double speed = (progress.Value - lasti) / sw.Elapsed.TotalSeconds;
                    lblSpeed.Text = string.Format("{0:N1} MB/s", speed);
                    lblByteRemaining.Text = string.Format("{0:N0} MB", progress.Maximum - progress.Value);
                    var rem = TimeSpan.FromSeconds((progress.Maximum - progress.Value) / speed);
                    lblTimeRemaining.Text = rem.ToString(@"h\:mm\:ss");
                    this.Text = string.Format("{0} ({1}: {2:P0})", Application.ProductName, Operation, (double)progress.Value / progress.Maximum);
                    lasti = progress.Value;
                    sw.Restart();
                }
            } while (remaining > 0);

            if (ProgressChanged != null)
                ProgressChanged(count);

            progress.Value = progress.Maximum;
            lblByteRemaining.Text = string.Format("0 MB");
            progress.Refresh();
            sw.Stop();
            this.Text = oldCaption;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (cts != null)
                cts.Cancel();
            else
                this.Close();
        }

        private void SetButtons(bool p)
        {
            bool b = lstDiskDrive.SelectedIndex >= 0 && m_DrivesFound;
            btnRead.Enabled = p && b;
            btnWrite.Enabled = p && b;
            btnWipe.Enabled = p && b;
            btnFormat.Enabled = p && b;
            txtFilename.Enabled = p;
            btnChooseFile.Enabled = p;
            lstDiskDrive.Enabled = p;
            btnStop.Text = p ? "Exit" : "Cancel";
        }

        private void frmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            DriveTools.StopDriveChangeNotification();
        }

        private void lstDiskDrive_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetButtons(true);
            var dd = lstDiskDrive.SelectedItem as DiskDrive;
            if (dd == null)
            {
                lblVolume.Text = "(n/a)";
                lblPartition.Text = lblVolume.Text;
                lblInterfaceType.Text = lblVolume.Text;
                lblPhysicalDriveSize.Text = lblVolume.Text;
                lblFormat.Text = lblVolume.Text;
                lblModel.Text = lblVolume.Text;
            }
            else
            {
                if (dd.Volumes.Count() == 0)
                {
                    lblVolume.Text = "(none)";
                    lblFormat.Text = "(none)";
                }
                else
                {
                    lblVolume.Text = string.Join(", ", dd.Volumes.Select(z => z.Name));
                    lblFormat.Text = string.Join(", ", dd.Volumes.Select(z => z.FileSystem));
                }

                if (dd.DiskPartitions.Count() == 0)
                    lblPartition.Text = "(none)";
                else
                    lblPartition.Text = string.Join(", ", dd.DiskPartitions);

                lblInterfaceType.Text = dd.InterfaceType;
                lblPhysicalDriveSize.Text = string.Format("{0:N0} MB", dd.GetDriveSize() / (1024 * 1024));
                lblModel.Text = dd.Model;
                btnFormat.Enabled = dd.Volumes.Count() == 1;
            }

        }

        private async void btnWipe_Click(object sender, EventArgs e)
        {
            DiskDrive dd = (DiskDrive)lstDiskDrive.SelectedItem;
            if (dd == null)
            {
                MessageBox.Show("Choose valid SD drive first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (MessageBox.Show("All data on SD card will be erased and partitions deleted. Are you sure?", "Warning",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.No) return;

            Operation = "Wiping";
            if (!TryLockVolumes(dd)) return;
            SetButtons(false);
            using (Stream source = new ConstantStream(0xff), dest = dd.CreateStream(FileAccess.Write))
            {
                var mbr = Properties.Resources.MBR;
                dest.Write(mbr, 0, mbr.Length);

                progress.Value = 0;
                progress.Maximum = (int)(((long)dd.GetDriveSize() - mbr.Length) / (1024 * 1024)) + 1;

                cts = new CancellationTokenSource();
                try
                {
                    await CopyStreamAsync(source, dest, (long)dd.GetDriveSize() - mbr.Length, cts.Token, null);
                }
                catch (Exception ex)
                {
                    if (!(ex is OperationCanceledException))
                        MessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK);
                    cts.Cancel();
                }
            }
            dd.DismountVolumes();
            dd.UnlockVolumes();
            dd.MountVolumes();
            ResetProgress();

            FillDriveList();
            if (!cts.IsCancellationRequested)
                MessageBox.Show("Erasing complete.", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetButtons(true);
            cts = null;
        }

        private async void btnFormat_Click(object sender, EventArgs e)
        {
            DiskDrive dd = (DiskDrive)lstDiskDrive.SelectedItem;
            if (dd == null)
            {
                MessageBox.Show("Choose valid SD drive first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (MessageBox.Show("All data on SD card will be lost during format. Are you sure?", "Warning",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.No) return;

            SetButtons(false);

            this.Text += " (Formatting)";
            lblSpeed.Text = "Windows is formatting the drive...";
            cts = new CancellationTokenSource();

            using (Stream dest = dd.CreateStream(FileAccess.Write))
            {
                progress.Value = 0;
                progress.Maximum = 100;
                //await Task.Run(() => dd.Format(ProgressHandler, cts.Token));
                try
                {
                    await Task.Run(() => dd.Volumes.First().Format("FAT32", true), cts.Token);
                }
                catch (Exception ex)
                {
                    if (!(ex is OperationCanceledException))
                        MessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    cts.Cancel();
                }
            }
            ResetProgress();

            FillDriveList();
            if (!cts.IsCancellationRequested)
                MessageBox.Show("Formatting complete.", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            cts = null;
        }

        private void ProgressHandler(object sender, ProgressEventArgs e)
        {
            BeginInvoke(new Action(() => progress.Value = e.Current));
        }
    }
}

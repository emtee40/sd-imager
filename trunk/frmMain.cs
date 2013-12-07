using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SDImager
{
    public partial class frmMain : Form
    {
        CancellationTokenSource cts;
        string Operation;

        public frmMain()
        {
            InitializeComponent();
        }

        private void DriveChanged(object sender, EventArgs e)
        {
            Invoke(new Action(FillSDDrives));
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
#if DEBUG
            btnErase.Visible = true;
            chkLL.Visible = true;
#endif
            DriveTools.StartDriveChangeNotification(DriveChanged);
            FillSDDrives();
            ResetProgress();
            lstSDDrive_SelectedIndexChanged(sender, e);
        }

        private void FillSDDrives()
        {
            lstSDDrive.DataSource = DriveTools.GetRemovableVolumes(chkLL.Checked).ToList();
            lstSDDrive_SelectedIndexChanged(this, EventArgs.Empty);
            lstSDDrive.Refresh();
        }

        private void btnChooseFile_Click(object sender, EventArgs e)
        {
            ofdFilename.FileName = txtFilename.Text;
            if (ofdFilename.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtFilename.Text = ofdFilename.FileName;
            }
        }

        private async void btnRead_Click(object sender, EventArgs e)
        {
            VolumeInfo vi = (VolumeInfo)lstSDDrive.SelectedItem;
            if (!CheckFilename(txtFilename.Text, vi, true))
            {
                txtFilename.Focus();
                return;
            }
            var filename = Path.GetFullPath(txtFilename.Text);

            Operation = "Reading";
            SetButtons(false);

            var dest = new FileStream(txtFilename.Text, FileMode.Create, FileAccess.Write, FileShare.None);
            var source = vi.GetPhysicalDriveStream(FileAccess.Read);
            vi.LockVolume();

            progress.Maximum = (int)(vi.PhysicalDriveSize / (1024 * 1024)) + 1;
            progress.Value = 0;
            cts = new CancellationTokenSource();
            try
            {
                await CopyStreamAsync(source, dest, (long)vi.PhysicalDriveSize, cts.Token);
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                    MessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK);
                cts.Cancel();
            }
            ResetProgress();
            vi.UnlockVolume();
            vi.CloseHandles();
            dest.Close();
            if (!cts.IsCancellationRequested)
                MessageBox.Show("Reading complete.", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetButtons(true);
            cts = null;
            this.Text = Application.ProductName;
        }

        private async void btnWrite_Click(object sender, EventArgs e)
        {
            VolumeInfo vi = (VolumeInfo)lstSDDrive.SelectedItem;
            if (!CheckFilename(txtFilename.Text, vi, false))
            {
                txtFilename.Focus();
                return;
            }
            var filename = Path.GetFullPath(txtFilename.Text);
            if (MessageBox.Show("All data on SD card will be erased and overwritten. Are you sure?", "Warning",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.No) return;

            Operation = "Writing";
            SetButtons(false);

            var source = new FileStream(txtFilename.Text, FileMode.Open, FileAccess.Read, FileShare.Read);
            var dest = vi.GetPhysicalDriveStream(FileAccess.Write);
            vi.LockVolume();
            vi.DismountVolume();

            progress.Maximum = (int)(source.Length / (1024 * 1024)) + 1;
            progress.Value = 0;
            cts = new CancellationTokenSource();
            try
            {
                await CopyStreamAsync(source, dest, source.Length, cts.Token);
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                    MessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK);
                cts.Cancel();
            }
            ResetProgress();
            vi.UnlockVolume();
            vi.CloseHandles();
            source.Close();
            if (!cts.IsCancellationRequested)
                MessageBox.Show("Writing complete.", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetButtons(true);
            cts = null;
            this.Text = Application.ProductName;
        }

        private bool CheckFilename(string filename, VolumeInfo vi, bool write)
        {
            if (vi == null)
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
            if (Path.GetPathRoot(filename).StartsWith(vi.VolumeID))
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

        private async Task CopyStreamAsync(Stream source, Stream dest, long count, CancellationToken token)
        {
            byte[] buf = new byte[1024 * 1024];
            var oldCaption = this.Text;

            int len = 0, lasti = 0;
            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                token.ThrowIfCancellationRequested();
                len = await source.ReadAsync(buf, 0, (int)Math.Min(count, buf.Length));
                count -= len;
                await dest.WriteAsync(buf, 0, len);

                progress.Value += 1;
                if (sw.ElapsedMilliseconds >= 1000)
                {
                    double speed = (progress.Value - lasti) / sw.Elapsed.TotalSeconds;
                    lblSpeed.Text = string.Format("{0:N1} MB/s", speed);
                    lblByteRemaining.Text = string.Format("{0:N0} MB", progress.Maximum - progress.Value);
                    var rem = TimeSpan.FromSeconds((progress.Maximum - progress.Value) / speed);
                    lblTimeRemaining.Text = rem.ToString(@"h\:mm\:ss");
                    this.Text = string.Format("{0} ({1}: {2:P0})", Application.ProductName, Operation, (double)progress.Value / progress.Maximum);
                    lasti = progress.Value;
                    sw.Restart();
                }
            } while (count > 0);
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
            btnRead.Enabled = p;
            btnWrite.Enabled = p;
            btnErase.Enabled = p;
            btnFormat.Enabled = p && !chkLL.Checked;
            txtFilename.Enabled = p;
            btnChooseFile.Enabled = p;
            lstSDDrive.Enabled = p;
            chkLL.Enabled = p;
            btnStop.Text = p ? "Exit" : "Cancel";
        }

        private void frmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            DriveTools.StopDriveChangeNotification();
        }

        private void lstSDDrive_SelectedIndexChanged(object sender, EventArgs e)
        {
            var vi = (VolumeInfo)lstSDDrive.SelectedItem;
            if (vi == null)
            {
                lblVolume.Text = "(n/a)";
                lblPartition.Text = lblVolume.Text;
                lblPhysicalDrive.Text = lblVolume.Text;
                lblPhysicalDriveSize.Text = lblVolume.Text;
                lblFormat.Text = lblVolume.Text;
                lblModel.Text = lblVolume.Text;
            }
            else
            {
                lblVolume.Text = vi.VolumeID;
                lblPartition.Text = vi.PartitionID;
                lblPhysicalDrive.Text = vi.PhysicalDriveID;
                lblPhysicalDriveSize.Text = string.Format("{0:N0} MB", vi.PhysicalDriveSize / (1024 * 1024));
                lblModel.Text = vi.Model;
                try
                {
                    lblFormat.Text = vi.DriveInfo.DriveFormat;
                }
                catch
                {
                    lblFormat.Text = "(unknown)";
                }
            }

        }

        private void chkLL_CheckedChanged(object sender, EventArgs e)
        {
            SetButtons(true);
            FillSDDrives();
        }

        private async void btnErase_Click(object sender, EventArgs e)
        {
            VolumeInfo vi = (VolumeInfo)lstSDDrive.SelectedItem;
            if (MessageBox.Show("All data on SD card will be erased. Are you sure?", "Warning",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.No) return;

            Operation = "Erasing";
            SetButtons(false);

            var source = new ConstantStream(0xff);
            var dest = vi.GetPhysicalDriveStream(FileAccess.Write);
            vi.LockVolume();
            vi.DismountVolume();

            var mbr = Properties.Resources.MBR;
            dest.Write(mbr, 0, mbr.Length);

            progress.Value = 0;
            progress.Maximum = (int)((vi.PhysicalDriveSize - mbr.Length) / (1024 * 1024)) + 1;

            cts = new CancellationTokenSource();
            try
            {
                await CopyStreamAsync(source, dest, (long)vi.PhysicalDriveSize - mbr.Length, cts.Token);
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                    MessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK);
                cts.Cancel();
            }
            ResetProgress();
            vi.UnlockVolume();
            vi.CloseHandles();
            source.Close();
            if (!cts.IsCancellationRequested)
                MessageBox.Show("Erasing complete.", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetButtons(true);
            cts = null;
            this.Text = Application.ProductName;
        }

        private async void btnFormat_Click(object sender, EventArgs e)
        {
            VolumeInfo vi = (VolumeInfo)lstSDDrive.SelectedItem;
            if (MessageBox.Show("All data on SD card will be lost during format. Are you sure?", "Warning",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.No) return;

            SetButtons(false);

            this.Text += " (Formatting)";
            lblSpeed.Text = "Windows is formatting the drive...";
            cts = new CancellationTokenSource();
            var dest = vi.GetPhysicalDriveStream(FileAccess.Write);
            //vi.LockVolume();
            //vi.DismountVolume();
            progress.Value = 0;
            progress.Maximum = 100;
            await Task.Run(() => vi.Format(ProgressHandler, cts.Token));
            //vi.UnlockVolume();
            vi.CloseHandles();
            if (!cts.IsCancellationRequested)
                MessageBox.Show("Formatting complete.", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ResetProgress();
            SetButtons(true);
            cts = null;
            this.Text = Application.ProductName;
        }

        private void ProgressHandler(object sender, ProgressEventArgs e)
        {
            BeginInvoke(new Action(() => progress.Value = e.Current));
        }
    }
}

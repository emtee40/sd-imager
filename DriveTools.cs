using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace SDImager
{
    internal class VolumeInfo : IDisposable
    {
        public string VolumeID;
        public string PartitionID;
        public string PhysicalDriveID;
        public long PhysicalDriveSize;
        public DriveInfo DriveInfo;
        public string InterfaceType;
        public string Model;

        FileStream hVolume;
        FileStream hPhysicalDrive;
        ManualResetEvent hWait;

        public override string ToString()
        {
            if (DriveInfo == null)
                return string.Format("{0} [{1}: {2:N0} MB]", PhysicalDriveID, InterfaceType, PhysicalDriveSize / (1024 * 1024));
            else
                try
                {
                    return string.Format(@"{0}\ [{1}, {2}: {3:N0} MB]", VolumeID, DriveInfo.DriveFormat, InterfaceType, PhysicalDriveSize / (1024 * 1024));
                }
                catch
                {
                    return string.Format(@"{0}\ [not ready]", VolumeID);
                }
        }

        public void CloseHandles()
        {
            if (hVolume != null)
            {
                hVolume.Dispose();
                hVolume = null;
            }
            if (hPhysicalDrive != null)
            {
                hPhysicalDrive.Dispose();
                hPhysicalDrive = null;
            }
        }

        public bool LockVolume()
        {
            if (DriveInfo != null)
            {
                GetVolumeStream();
                IOWrapper.LockVolume(hVolume.SafeFileHandle);
            }
            return true;
        }

        public bool UnlockVolume()
        {
            if (DriveInfo != null)
            {
                GetVolumeStream();
                IOWrapper.UnlockVolume(hVolume.SafeFileHandle);
            }
            return true;
        }

        public bool DismountVolume()
        {
            if (DriveInfo != null)
            {
                GetVolumeStream();
                IOWrapper.DismountVolume(hVolume.SafeFileHandle);
            }
            return true;
        }

        public FileStream GetVolumeStream()
        {
            if (DriveInfo == null) return null;
            if (hVolume != null) return hVolume;
            hVolume = IOWrapper.GetFileStream(string.Format(@"\\.\{0}", VolumeID), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return hVolume;
        }

        public FileStream GetPhysicalDriveStream(FileAccess access)
        {
            if (hPhysicalDrive != null) return hPhysicalDrive;
            hPhysicalDrive = IOWrapper.GetFileStream(PhysicalDriveID, FileMode.Open, access, FileShare.ReadWrite);
            return hPhysicalDrive;
        }

        private ManagementObject GetWMIVolume()
        {
            //return new ManagementObject(string.Format(@"Win32_Volume.Name='{0}\\'", VolumeID));
            foreach (var o in new ManagementObjectSearcher("SELECT * FROM Win32_Volume WHERE DriveLetter='" + VolumeID + "'").Get())
                return (ManagementObject)o;
            return null;
        }

        public void Format(ProgressEventHandler progressHandler, CancellationToken token)
        {
            if (DriveInfo == null)
                throw new NotSupportedException();

            var mo = GetWMIVolume();
            object r;
            r = mo.InvokeMethod("Dismount", new object[] { true, false });
            var param = mo.GetMethodParameters("Format");
            param["FileSystem"] = "FAT32";
            param["QuickFormat"] = true;
            param["Label"] = "Empty";
            var ob = new ManagementOperationObserver();
            //var tcs = new TaskCompletionSource<void>();
            ob.Progress += progressHandler;
            ob.Completed += OnCompleted;
            hWait = new ManualResetEvent(false);
            mo.InvokeMethod(ob, "Format", param, null);
            try
            {
                Task.Run(() => hWait.WaitOne()).Wait(token);
            }
            catch (OperationCanceledException)
            {
                ob.Cancel();
            }
            hWait.Dispose();
            hWait = null;
            r = mo.InvokeMethod("Mount", null);
        }

        private void OnCompleted(object sender, CompletedEventArgs e)
        {
            hWait.Set();
        }

        public void Dispose()
        {
            CloseHandles();
        }
    }

    internal static class DriveTools
    {
        private static ManagementEventWatcher mew1, mew2, mew3;
        private static EventHandler NotifyHandler;

        private static IEnumerable<VolumeInfo> GetRemovableVolumesDefault()
        {
            List<VolumeInfo> r = new List<VolumeInfo>();
            var os = new ManagementObjectSearcher();
            //os.Query = new ObjectQuery("SELECT * FROM Win32_LogicalDisk WHERE DriveType=2");

            foreach (var di in DriveInfo.GetDrives().Where(z => z.DriveType == DriveType.Removable))
            {
                var vi = new VolumeInfo();
                vi.VolumeID = di.Name.Substring(0, 2);
                vi.DriveInfo = di;

                os.Query = new ObjectQuery(string.Format(@"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{0}'}} WHERE ResultClass=Win32_DiskPartition", vi.VolumeID));
                foreach (var o in os.Get())
                    vi.PartitionID = (string)o["DeviceID"];

                if (vi.PartitionID != null)
                {
                    os.Query = new ObjectQuery(string.Format(@"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{0}'}} WHERE ResultClass=Win32_DiskDrive", vi.PartitionID));
                    foreach (var o in os.Get())
                    {
                        vi.PhysicalDriveID = (string)o["DeviceID"];
                        vi.InterfaceType = (string)o["InterfaceType"];
                        vi.PhysicalDriveSize = IOWrapper.GetLength(vi.GetPhysicalDriveStream(FileAccess.Read).SafeFileHandle);
                        vi.Model = (string)o["Model"];
                        vi.CloseHandles();
                    }
                }

                if (vi.PhysicalDriveID != null)
                    r.Add(vi);
            }
            return r;
        }

        public static IEnumerable<VolumeInfo> GetRemovableVolumes(bool lowlevel)
        {
            return lowlevel ? GetRemovableVolumesLowLevel() : GetRemovableVolumesDefault();
        }

        private static IEnumerable<VolumeInfo> GetRemovableVolumesLowLevel()
        {
            List<VolumeInfo> r = new List<VolumeInfo>();
            var os = new ManagementObjectSearcher();
            os.Query = new ObjectQuery("SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB' AND MediaType LIKE 'Removable%'");
            foreach (var o in os.Get())
            {
                var vi = new VolumeInfo();
                vi.PhysicalDriveID = (string)o["DeviceID"];
                vi.InterfaceType = (string)o["InterfaceType"];
                vi.PhysicalDriveSize = IOWrapper.GetLength(vi.GetPhysicalDriveStream(FileAccess.Read).SafeFileHandle);
                vi.Model = (string)o["Model"];
                vi.VolumeID = "(none)";
                vi.PartitionID = vi.VolumeID;
                vi.CloseHandles();
                r.Add(vi);
            }
            return r;
        }

        public static void StartDriveChangeNotification(EventHandler handler)
        {
            if (mew1 != null || mew2 != null)
                throw new Exception("Already started");

            NotifyHandler = handler;
            mew1 = new ManagementEventWatcher("SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_DiskDrive'");
            mew1.EventArrived += EventArrived;
            mew1.Start();
            mew2 = new ManagementEventWatcher("SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_DiskDrive'");
            mew2.EventArrived += EventArrived;
            mew2.Start();
            mew3 = new ManagementEventWatcher("SELECT * FROM __InstanceModificationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_DiskDrive'");
            mew3.EventArrived += EventArrived;
            mew3.Start();
        }

        public static void StopDriveChangeNotification()
        {
            if (mew1 != null)
            {
                mew1.Stop();
                mew1.Dispose();
                mew1 = null;
            }
            if (mew2 != null)
            {
                mew2.Stop();
                mew2.Dispose();
                mew2 = null;
            }
            if (mew3 != null)
            {
                mew3.Stop();
                mew3.Dispose();
                mew3 = null;
            }
        }

        private static void EventArrived(object sender, EventArrivedEventArgs e)
        {
            if (NotifyHandler != null)
                NotifyHandler(NotifyHandler.Target, EventArgs.Empty);
        }
    }
}

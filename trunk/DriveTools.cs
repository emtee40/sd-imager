using System;
using System.Collections.Generic;
using System.Management;

namespace SDImager
{
    internal static class DriveTools
    {
        private static List<ManagementEventWatcher> mew;
        private static EventArrivedEventHandler NotifyHandler;

        public static void StartDriveChangeNotification(EventArrivedEventHandler handler)
        {
            if (mew != null)
                throw new Exception("Already started");

            NotifyHandler = handler;
            mew = new List<ManagementEventWatcher>();
            mew.Add(new ManagementEventWatcher("SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_DiskDrive'"));
            mew.Add(new ManagementEventWatcher("SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_DiskDrive'"));
            mew.Add(new ManagementEventWatcher("SELECT * FROM __InstanceModificationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_DiskDrive'"));
            mew.Add(new ManagementEventWatcher("SELECT * FROM Win32_VolumeChangeEvent WITHIN 1"));

            foreach (var m in mew)
            {
                m.EventArrived += EventArrived;
                m.Start();
            }
        }

        public static void StopDriveChangeNotification()
        {
            if (mew != null)
                foreach (var m in mew)
                {
                    m.Stop();
                    m.Dispose();
                }
        }

        private static void EventArrived(object sender, EventArrivedEventArgs e)
        {
            NotifyHandler?.Invoke(NotifyHandler.Target, e);
        }
    }
}

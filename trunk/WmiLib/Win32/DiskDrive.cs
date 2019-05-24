using OSX.WmiLib.Infrastructure;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OSX.WmiLib.Win32
{
    [WmiClass(@"Root\CIMV2:Win32_DiskDrive")]
    public class DiskDrive : WmiFileHandleObject
    {
        public string InterfaceType => _getter();
        public string Model => _getter();
        public string MediaType => _getter();
        public uint? Index => _getter();
        public string DeviceID => _getter();
        public uint? BytesPerSector => _getter();
        public string Caption => _getter();
        public string Description => _getter();
        public string Manufacturer => _getter();
        public ulong? Size => _getter();
        public uint? SectorsPerTrack => _getter();
        public ulong? TotalCylinders => _getter();
        public uint? TotalHeads => _getter();
        public ulong? TotalSectors => _getter();
        public ulong? TotalTracks => _getter();
        public uint? TracksPerCylinder => _getter();

        public IEnumerable<DiskPartition> DiskPartitions { get { return GetAssociators<DiskPartition>(); } }
        public IEnumerable<LogicalDisk> LogicalDisks { get { return DiskPartitions.SelectMany(p => p.LogicalDisks); } }
        public IEnumerable<Volume> Volumes { get { return LogicalDisks.Select(l => l.Volume); } }
        private long m_DriveSize = -1;

        public long GetDriveSize()
        {
            if (m_DriveSize < 0)
                using (var handle = CreateHandle(FileAccess.Read))
                {
                    m_DriveSize = IOWrapper.GetLength(handle);
                }
            return m_DriveSize;
        }

        public override string GetFilename()
        {
            return DeviceID;
        }

        public void LockVolumes()
        {
            foreach (var v in Volumes) v.Lock();
        }

        public void UnlockVolumes()
        {
            foreach (var v in Volumes) v.Unlock();
        }

        public void DismountVolumes()
        {
            foreach (var v in Volumes) v.Dismount(true);
        }

        public void MountVolumes()
        {
            foreach (var v in Volumes) v.Mount();
        }
    }
}

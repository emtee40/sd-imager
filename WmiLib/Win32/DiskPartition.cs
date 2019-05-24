using OSX.WmiLib.Infrastructure;
using System.Collections.Generic;

namespace OSX.WmiLib.Win32
{
    [WmiClass(@"Root\CIMV2:Win32_DiskPartition")]
    public class DiskPartition : WmiObject
    {
        public ulong? BlockSize => _getter();
        public ulong? NumberOfBlocks => _getter();
        public ulong? Size => _getter();
        public string Type => _getter();
        public uint? DiskIndex => _getter();
        public uint? Index => _getter();
        public string Description => _getter();
        public ulong? StartingOffset => _getter();
        public bool? PrimaryPartition => _getter();
        public bool? Bootable => _getter();
        public bool? BootPartition => _getter();

        public IEnumerable<LogicalDisk> LogicalDisks { get { return GetAssociators<LogicalDisk>(); } }

    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OSX.IOlib
{
    [WmiClass("Win32_LogicalDisk")]
    internal class LogicalDisk : BaseObject<LogicalDisk>
    {
        [WmiProperty]
        public string Description { get; private set; }
        [WmiProperty]
        public DriveType DriveType { get; private set; }
        [WmiProperty]
        public string FileSystem { get; private set; }
        [WmiProperty]
        public ulong FreeSpace { get; private set; }
        [WmiProperty]
        public uint MediaType { get; private set; }
        [WmiProperty]
        public ulong Size { get; private set; }
        [WmiProperty]
        public string VolumeName { get; private set; }

        public Volume Volume { get { return Volume.Find(ID + @"\"); } }
    }
}

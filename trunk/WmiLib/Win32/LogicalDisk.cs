using OSX.WmiLib.Infrastructure;
using System.IO;
using System.Linq;

namespace OSX.WmiLib.Win32
{
    [WmiClass(@"Root\CIMV2:Win32_LogicalDisk")]
    public class LogicalDisk : WmiObject
    {
        public string Description => _getter();
        public DriveType? DriveType => _getter();
        public string FileSystem => _getter();
        public ulong? FreeSpace => _getter();
        public uint? MediaType => _getter();
        public ulong? Size => _getter();
        public string VolumeName => _getter();

        private Volume m_Volume;
        public Volume Volume
        {
            get
            {
                if (m_Volume == null)
                {
                    var v = GetContext().Instances<Volume>().Where(z => z.Name == GetKey() + @"\").SingleOrDefault();
                    m_Volume = v;
                }
                return m_Volume;
            }
        }
    }
}

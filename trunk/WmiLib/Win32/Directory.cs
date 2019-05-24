using OSX.WmiLib.Infrastructure;

namespace OSX.WmiLib.Win32
{
    [WmiClass(@"Root\CIMV2:Win32_Directory")]
    public class Directory : WmiFileHandleObject
    {

        public string Name => _getter();
        public string Drive => _getter();
        public string FileType => _getter();
        public string FSName => _getter();
        public string Path => _getter();

        public override string GetFilename()
        {
            return Name;
        }
    }
}

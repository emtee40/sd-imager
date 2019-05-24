using Microsoft.Win32.SafeHandles;
using OSX.WmiLib.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;

namespace OSX.WmiLib.Win32
{
    [WmiClass(@"Root\CIMV2:Win32_Volume")]
    public class Volume : WmiFileHandleObject
    {
        public bool? Automount => _getter();
        public ulong? BlockSize => _getter();
        public bool? BootVolume => _getter();
        public ulong? Capacity => _getter();
        public string Name => _getter();
        public bool? Compressed => _getter();
        public string DeviceID => _getter();
        public string DriveLetter => _getter();
        public DriveType? DriveType => _getter();
        public string FileSystem => _getter();
        public ulong? FreeSpace => _getter();
        public string Label => _getter();
        public uint? MaximumFileNameLength => _getter();
        public bool? PageFilePresent => _getter();
        public uint? SerialNumber => _getter();

        public IEnumerable<Directory> MountPoints => GetAssociators<Directory>(); 

        private SafeFileHandle m_LockHandle;
        public SafeFileHandle GetLockHandle() => m_LockHandle;

        public void Lock()
        {
            if (m_LockHandle != null && !m_LockHandle.IsInvalid)
                throw new InvalidOperationException("Volume already locked");

            var handle = CreateHandle(FileAccess.ReadWrite, FileShare.ReadWrite);
            IOWrapper.LockVolume(handle);
            m_LockHandle = handle;
        }

        public void Unlock()
        {
            if (m_LockHandle == null)
                throw new InvalidOperationException("Volume was not locked");

            IOWrapper.UnlockVolume(m_LockHandle);
            m_LockHandle.Close();
            m_LockHandle = null;
        }

        public void Dismount(bool Force = false, bool Permanent = false)
        {
            SafeFileHandle handle = GetLockHandle();
            if (handle != null)
                IOWrapper.DismountVolume(handle);
            else
            {
                var r = (uint)Call("Dismount", Force, Permanent);
                switch (r)
                {
                    case 1: throw new SecurityException("Access Denied");
                    case 2: throw new IOException("Volume Has Mount Points");
                    case 3: throw new IOException("Volume Does Not Support The No-Autoremount State");
                    case 4: throw new IOException("Force Option Required");
                }
            }
        }

        public void Mount()
        {
            var r = (uint)Call("Mount", null);
            switch (r)
            {
                case 1: throw new SecurityException("Access Denied");
                case 2: throw new IOException("Unknown Error");
            }
        }

        public bool Reset()
        {
            return (uint)Call("Reset", null) == 0;
        }

        public void Format(string FileSystem = "NTFS", bool QuickFormat = false, uint ClusterSize = 0, string Label = "", bool EnableCompression = false)
        {
            var r = (uint)Call("Format", FileSystem, QuickFormat, ClusterSize, Label, EnableCompression);
            switch (r)
            {
                case 1: throw new InvalidOperationException("Unsupported file system");
                case 2: throw new IOException("Incompatible media in drive");
                case 3: throw new SecurityException("Access denied");
                case 4: throw new IOException("Call canceled");
                case 5: throw new IOException("Call cancellation request too late");
                case 6: throw new IOException("Volume write protected");
                case 7: throw new IOException("Volume lock failed");
                case 8: throw new IOException("Unable to quick format");
                case 9: throw new IOException("Input/Output (I/O) error");
                case 10: throw new IOException("Invalid volume label");
                case 11: throw new IOException("No media in drive");
                case 12: throw new IOException("Volume is too small");
                case 13: throw new IOException("Volume is too large");
                case 14: throw new IOException("Volume is not mounted");
                case 15: throw new IOException("Cluster size is too small");
                case 16: throw new IOException("Cluster size is too large");
                case 17: throw new IOException("Cluster size is beyond 32 bits");
                case 18: throw new IOException("Unknown error");
            }
        }

        public override string GetFilename()
        {
            if (string.IsNullOrEmpty(DriveLetter))
                throw new NotSupportedException("Volume does not have a DriveLetter");
            return @"\\.\" + DriveLetter;
        }
    }
}

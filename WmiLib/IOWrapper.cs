using Microsoft.Win32.SafeHandles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace OSX.WmiLib
{
    public class IOWrapper
    {
        //
        // CreateFile constants
        //

        const uint FILE_DEVICE_DISK = 0x00000007;
        const uint FILE_DEVICE_FILE_SYSTEM = 0x00000009;
        const uint METHOD_BUFFERED = 0;
        const uint FILE_ANY_ACCESS = 0;
        const uint FILE_READ_ACCESS = (0x0001);
        const uint FILE_WRITE_ACCESS = (0x0002);
        const uint IOCTL_DISK_BASE = FILE_DEVICE_DISK;

        public static uint FSCTL_LOCK_VOLUME = CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 6, METHOD_BUFFERED, FILE_ANY_ACCESS);
        public static uint FSCTL_UNLOCK_VOLUME = CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 7, METHOD_BUFFERED, FILE_ANY_ACCESS);
        public static uint FSCTL_DISMOUNT_VOLUME = CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 8, METHOD_BUFFERED, FILE_ANY_ACCESS);
        public static uint IOCTL_DISK_GET_PARTITION_INFO_EX = CTL_CODE(IOCTL_DISK_BASE, 0x0012, METHOD_BUFFERED, FILE_ANY_ACCESS);
        public static uint IOCTL_DISK_SET_PARTITION_INFO_EX = CTL_CODE(IOCTL_DISK_BASE, 0x0013, METHOD_BUFFERED, FILE_READ_ACCESS | FILE_WRITE_ACCESS);
        public static uint IOCTL_DISK_GET_DRIVE_LAYOUT_EX = CTL_CODE(IOCTL_DISK_BASE, 0x0014, METHOD_BUFFERED, FILE_ANY_ACCESS);
        public static uint IOCTL_DISK_SET_DRIVE_LAYOUT_EX = CTL_CODE(IOCTL_DISK_BASE, 0x0015, METHOD_BUFFERED, FILE_READ_ACCESS | FILE_WRITE_ACCESS);
        public static uint IOCTL_DISK_CREATE_DISK = CTL_CODE(IOCTL_DISK_BASE, 0x0016, METHOD_BUFFERED, FILE_READ_ACCESS | FILE_WRITE_ACCESS);
        public static uint IOCTL_DISK_GET_LENGTH_INFO = CTL_CODE(IOCTL_DISK_BASE, 0x0017, METHOD_BUFFERED, FILE_READ_ACCESS);
        public static uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = CTL_CODE(IOCTL_DISK_BASE, 0x0028, METHOD_BUFFERED, FILE_ANY_ACCESS);

        static uint CTL_CODE(uint DeviceType, uint Function, uint Method, uint Access)
        {
            return ((DeviceType) << 16) | ((Access) << 14) | ((Function) << 2) | (Method);
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern SafeFileHandle CreateFile(
           string lpFileName,
           [MarshalAs(UnmanagedType.U4)] FileAccess DesiredAccess,
           [MarshalAs(UnmanagedType.U4)] FileShare ShareMode,
           IntPtr lpSecurityAttributes,
           [MarshalAs(UnmanagedType.U4)] FileMode CreationDisposition,
           [MarshalAs(UnmanagedType.U4)] FileAttributes FlagsAndAttributes,
           IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            [Out] IntPtr lpOutBuffer,
            uint nOutBufferSize,
            ref uint lpBytesReturned,
            IntPtr lpOverlapped);

        public static void LockVolume(SafeHandle handle)
        {
            uint bytesReturned = 0;
            var b = DeviceIoControl(handle.DangerousGetHandle(), FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, ref  bytesReturned, IntPtr.Zero);
            if (!b)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }

        public static void UnlockVolume(SafeHandle handle)
        {
            uint bytesReturned = 0;
            var b = DeviceIoControl(handle.DangerousGetHandle(), FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, ref  bytesReturned, IntPtr.Zero);
            if (!b)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }

        public static void DismountVolume(SafeHandle handle)
        {
            uint bytesReturned = 0;
            var b = DeviceIoControl(handle.DangerousGetHandle(), FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, ref  bytesReturned, IntPtr.Zero);
            if (!b)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }

        public static long GetLength(SafeHandle handle)
        {
            var hResult = Marshal.AllocHGlobal(sizeof(ulong));
            uint bytesReturned = 0;
            long r = 0;
            try
            {
                var b = DeviceIoControl(handle.DangerousGetHandle(), IOCTL_DISK_GET_LENGTH_INFO, IntPtr.Zero, 0, hResult, sizeof(long), ref bytesReturned, IntPtr.Zero);
                if (!b)
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                r = Marshal.ReadInt64(hResult);
            }
            finally
            {
                Marshal.FreeHGlobal(hResult);
            }
            return r;
        }

        public static FileStream GetFileStream(string filename, FileMode mode, FileAccess access, FileShare share)
        {
            SafeFileHandle h = CreateFile(filename, access, share, IntPtr.Zero, mode, FileAttributes.Normal, IntPtr.Zero);
            if (h.IsInvalid)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            return new FileStream(h, access);
        }

    }

}

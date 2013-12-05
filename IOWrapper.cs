using Microsoft.Win32.SafeHandles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SDImager
{
    public class IOWrapper
    {
        //
        // CreateFile constants
        //

        const uint FILE_DEVICE_FILE_SYSTEM = 9;
        const uint METHOD_BUFFERED = 0;
        const uint FILE_ANY_ACCESS = 0;

        public static uint FSCTL_LOCK_VOLUME = CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 6, METHOD_BUFFERED, FILE_ANY_ACCESS);
        public static uint FSCTL_UNLOCK_VOLUME = CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 7, METHOD_BUFFERED, FILE_ANY_ACCESS);
        public static uint FSCTL_DISMOUNT_VOLUME = CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 8, METHOD_BUFFERED, FILE_ANY_ACCESS);

        static uint CTL_CODE(uint DeviceType, uint Function, uint Method, uint Access)
        {
            return ((DeviceType) << 16) | ((Access) << 14) | ((Function) << 2) | (Method);
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern SafeFileHandle CreateFile(
           string lpFileName,
           [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
           [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
           IntPtr lpSecurityAttributes,
           [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
           [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
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

        public static bool LockVolume(SafeHandle handle)
        {
            uint bytesReturned = 0;
            var b = DeviceIoControl(handle.DangerousGetHandle(), FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, ref  bytesReturned, IntPtr.Zero);
            if (!b)
                throw new IOException();
            return true;
        }

        public static bool UnlockVolume(SafeHandle handle)
        {
            uint bytesReturned = 0;
            var b = DeviceIoControl(handle.DangerousGetHandle(), FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, ref  bytesReturned, IntPtr.Zero);
            if (!b)
                throw new IOException();
            return true;
        }

        public static bool DismountVolume(SafeHandle handle)
        {
            uint bytesReturned = 0;
            var b = DeviceIoControl(handle.DangerousGetHandle(), FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, ref  bytesReturned, IntPtr.Zero);
            if (!b)
                throw new IOException();
            return true;
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

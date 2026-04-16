using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AegisTune.CleanupEngine;

[SupportedOSPlatform("windows")]
public sealed class WindowsRecycleBinShell : IRecycleBinShell
{
    private const string AllDrivesPath = "";
    private const uint NoConfirmationFlag = 0x00000001;
    private const uint NoProgressUiFlag = 0x00000002;
    private const uint NoSoundFlag = 0x00000004;

    public RecycleBinSnapshot Query()
    {
        NativeMethods.ShQueryRecycleBinInfo info = new()
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.ShQueryRecycleBinInfo>()
        };

        int hresult = NativeMethods.SHQueryRecycleBinW(AllDrivesPath, ref info);
        if (hresult < 0)
        {
            return new RecycleBinSnapshot(
                IsAvailable: false,
                ItemCount: 0,
                TotalBytes: 0,
                Note: $"Recycle Bin query failed: {FormatHresult(hresult)}");
        }

        return new RecycleBinSnapshot(
            IsAvailable: true,
            ItemCount: info.i64NumItems,
            TotalBytes: info.i64Size);
    }

    public void Empty()
    {
        uint flags = NoConfirmationFlag | NoProgressUiFlag | NoSoundFlag;
        int hresult = NativeMethods.SHEmptyRecycleBinW(IntPtr.Zero, AllDrivesPath, flags);
        if (hresult < 0)
        {
            Marshal.ThrowExceptionForHR(hresult);
        }
    }

    private static string FormatHresult(int hresult) =>
        Marshal.GetExceptionForHR(hresult)?.Message ?? $"HRESULT 0x{hresult:X8}";

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct ShQueryRecycleBinInfo
        {
            public uint cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern int SHQueryRecycleBinW(
            string pszRootPath,
            ref ShQueryRecycleBinInfo pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern int SHEmptyRecycleBinW(
            IntPtr hwnd,
            string pszRootPath,
            uint dwFlags);
    }
}

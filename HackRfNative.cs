using System.Runtime.InteropServices;

namespace RF_SafetyScanner;

internal static partial class HackRfNative
{
    public const int HACKRF_SUCCESS = 0;

    [LibraryImport("hackrf", EntryPoint = "hackrf_init")]
    public static partial int Init();

    [LibraryImport("hackrf", EntryPoint = "hackrf_exit")]
    public static partial int Exit();

    [LibraryImport("hackrf", EntryPoint = "hackrf_open")]
    public static partial int Open(out IntPtr device);

    [LibraryImport("hackrf", EntryPoint = "hackrf_close")]
    public static partial int Close(IntPtr device);

    [LibraryImport("hackrf", EntryPoint = "hackrf_set_freq")]
    public static partial int SetFreq(IntPtr device, ulong freqHz);

    [LibraryImport("hackrf", EntryPoint = "hackrf_set_sample_rate")]
    public static partial int SetSampleRate(IntPtr device, double sampleRateHz);

    [LibraryImport("hackrf", EntryPoint = "hackrf_set_lna_gain")]
    public static partial int SetLnaGain(IntPtr device, uint value);

    [LibraryImport("hackrf", EntryPoint = "hackrf_set_vga_gain")]
    public static partial int SetVgaGain(IntPtr device, uint value);

    [LibraryImport("hackrf", EntryPoint = "hackrf_set_amp_enable")]
    public static partial int SetAmpEnable(IntPtr device, byte value);

    [LibraryImport("hackrf", EntryPoint = "hackrf_start_rx")]
    public static partial int StartRx(IntPtr device, HackRfRxCallback callback, IntPtr ctx);

    [LibraryImport("hackrf", EntryPoint = "hackrf_stop_rx")]
    public static partial int StopRx(IntPtr device);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int HackRfRxCallback(ref HackRfTransfer transfer);

    [StructLayout(LayoutKind.Sequential)]
    public struct HackRfTransfer
    {
        public IntPtr Device;
        public IntPtr Buffer;
        public int BufferLength;
        public int ValidLength;
        public IntPtr RxCtx;
        public IntPtr TxCtx;
    }
}

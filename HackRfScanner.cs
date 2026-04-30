using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace RF_SafetyScanner;

public sealed class HackRfScanner : IRadioScanner
{
    private readonly ScannerSettings _settings;
    private readonly IntPtr _device;
    private readonly ConcurrentQueue<double[]> _blocks = new();
    private readonly HackRfNative.HackRfRxCallback _callback;
    private readonly Dictionary<long, DetectionHistory> _history = new();
    private bool _disposed;

    public HackRfScanner(ScannerSettings settings)
    {
        _settings = settings;

        Check(HackRfNative.Init(), "hackrf_init");
        Check(HackRfNative.Open(out _device), "hackrf_open");

        Check(HackRfNative.SetSampleRate(_device, 2_000_000), "hackrf_set_sample_rate");
        Check(HackRfNative.SetBasebandFilterBandwidth(_device, 1_750_000), "hackrf_set_baseband_filter_bandwidth");
        Check(HackRfNative.SetLnaGain(_device, 6), "hackrf_set_lna_gain");
        Check(HackRfNative.SetVgaGain(_device, 6), "hackrf_set_vga_gain");
        Check(HackRfNative.SetAmpEnable(_device, 0), "hackrf_set_amp_enable");

        _callback = RxCallback;
        Check(HackRfNative.StartRx(_device, _callback, IntPtr.Zero), "hackrf_start_rx");
    }

    public async IAsyncEnumerable<SignalReading> ScanAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        const double spanHz = 100_000;

        while (!token.IsCancellationRequested)
        {
            for (double f = _settings.StartHz; f <= _settings.StopHz; f += _settings.StepHz)
            {
                token.ThrowIfCancellationRequested();

                Check(HackRfNative.SetFreq(_device, (ulong)f), "hackrf_set_freq");

                while (_blocks.TryDequeue(out _)) { }

                await Task.Delay(_settings.DwellMs, token);

                var collected = new List<double[]>();
                while (_blocks.TryDequeue(out var block))
                    collected.Add(block);

                if (collected.Count == 0)
                    continue;

                var powers = collected.SelectMany(x => x).ToArray();
                double avgPower = Math.Max(powers.Average(), 1e-12);
                double level = 10.0 * Math.Log10(avgPower);

                long binKey = (long)Math.Round(f / _settings.StepHz);
                if (!_history.TryGetValue(binKey, out var h))
                    _history[binKey] = h = new DetectionHistory();

                double pulseScore = h.UpdateAndGetPulseScore(level);

                // MVP bandwidth estimate from level. Production FFT can replace this without changing UI/classifier.
                double estimatedBw = level >= _settings.CriticalDbfs ? 30_000 : level >= _settings.AlertDbfs ? 6_000 : 2_000;
                var spectrum = FftTools.BuildSyntheticSpectrum(level, estimatedBw, spanHz, 128);

                yield return new SignalReading
                {
                    FrequencyHz = f,
                    LevelDbfs = level,
                    BandwidthHz = estimatedBw,
                    PulseScore = pulseScore,
                    IsPulsed = pulseScore >= _settings.PulseScoreThreshold,
                    SpectrumDbfs = spectrum
                };
            }
        }
    }

    private int RxCallback(ref HackRfNative.HackRfTransfer transfer)
    {
        try
        {
            if (transfer.ValidLength <= 0 || transfer.Buffer == IntPtr.Zero)
                return 0;

            var bytes = new byte[transfer.ValidLength];
            Marshal.Copy(transfer.Buffer, bytes, 0, bytes.Length);

            var powers = new double[bytes.Length / 2];
            int idx = 0;

            for (int i = 0; i + 1 < bytes.Length; i += 2)
            {
                double iVal = (bytes[i] - 127.5) / 127.5;
                double qVal = (bytes[i + 1] - 127.5) / 127.5;
                powers[idx++] = (iVal * iVal + qVal * qVal) * 0.5;
            }

            _blocks.Enqueue(powers);
        }
        catch
        {
        }

        return 0;
    }

    private static void Check(int result, string call)
    {
        if (result != HackRfNative.HACKRF_SUCCESS)
            throw new InvalidOperationException($"{call} failed with code {result}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { HackRfNative.StopRx(_device); } catch { }
        try { HackRfNative.Close(_device); } catch { }
        try { HackRfNative.Exit(); } catch { }
    }
}

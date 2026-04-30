namespace RF_SafetyScanner;

public sealed class MockScanner : IRadioScanner
{
    private readonly ScannerSettings _settings;
    private readonly Dictionary<long, DetectionHistory> _history = new();

    public MockScanner(ScannerSettings settings)
    {
        _settings = settings;
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

                double level = -90 + Random.Shared.NextDouble() * 6;
                double bw = 2_000;

                if (Math.Abs(f - 1_800_000) < 7_500) { level = -61 + Random.Shared.NextDouble() * 3; bw = 2_000; }
                if (Math.Abs(f - 4_620_000) < 15_000) { level = -64 + Random.Shared.NextDouble() * 3; bw = 12_000; }

                // Simulated pulsed/wideband radar-like block near 5.8 MHz.
                if (Math.Abs(f - 5_800_000) < 90_000)
                {
                    bool pulseOn = DateTime.UtcNow.Millisecond % 220 < 95;
                    level = pulseOn ? -57 + Random.Shared.NextDouble() * 4 : -78 + Random.Shared.NextDouble() * 6;
                    bw = 85_000;
                }

                // Simulated band-edge overload.
                if (f < _settings.StartHz + 200_000 || f > _settings.StopHz - 200_000)
                {
                    level = -55 + Random.Shared.NextDouble() * 5;
                    bw = 140_000;
                }

                long binKey = (long)Math.Round(f / _settings.StepHz);
                if (!_history.TryGetValue(binKey, out var h))
                    _history[binKey] = h = new DetectionHistory();

                double pulseScore = h.UpdateAndGetPulseScore(level);
                var spectrum = FftTools.BuildSyntheticSpectrum(level, bw, spanHz, 128);

                yield return new SignalReading
                {
                    FrequencyHz = f,
                    LevelDbfs = level,
                    BandwidthHz = bw,
                    PulseScore = pulseScore,
                    IsPulsed = pulseScore >= _settings.PulseScoreThreshold,
                    SpectrumDbfs = spectrum
                };

                await Task.Delay(_settings.DwellMs, token);
            }
        }
    }

    public void Dispose()
    {
    }
}

namespace RF_SafetyScanner;

public sealed class ScannerSettings
{
    public double StartHz { get; set; }
    public double StopHz { get; set; }
    public double StepHz { get; set; }
    public int DwellMs { get; set; }
    public double AlertDbfs { get; set; } = -65;
    public double CriticalDbfs { get; set; } = -58;
    public double WidebandHz { get; set; } = 20_000;
    public double OverloadHz { get; set; } = 100_000;
    public double EdgeIgnoreHz { get; set; } = 500_000;
    public double PulseScoreThreshold { get; set; } = 0.35;
    public bool IgnoreEdges { get; set; } = true;
    public bool IgnoreCenterDcSpike { get; set; } = true;
    public bool SmartMuteCritical { get; set; } = true;
}

public sealed class SignalReading
{
    public double FrequencyHz { get; set; }
    public double LevelDbfs { get; set; }
    public double BandwidthHz { get; set; }
    public double PulseScore { get; set; }
    public bool IsPulsed { get; set; }
    public double[] SpectrumDbfs { get; set; } = [];
}

public sealed class SignalEvent
{
    public string Timestamp { get; set; } = "";
    public double FrequencyMhz { get; set; }
    public double LevelDbfs { get; set; }
    public double BandwidthKhz { get; set; }
    public double PulseScore { get; set; }
    public string SignalType { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Action { get; set; } = "";
}

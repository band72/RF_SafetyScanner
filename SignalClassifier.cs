namespace RF_SafetyScanner;

public static class SignalClassifier
{
    private static int _consecutiveClippingCount = 0;

    public static (string type, string severity, string action, bool log, bool mute) Classify(
        SignalReading reading,
        ScannerSettings settings)
    {
        if (reading.LevelDbfs > -3)
            _consecutiveClippingCount++;
        else
            _consecutiveClippingCount = 0;

        if (reading.LevelDbfs > -3 && _consecutiveClippingCount > 20)
        {
            return ("ADC_CLIPPING_OR_FRONT_END_OVERLOAD", "IGNORE_RF / MUTE_AUDIO", "Hardware saturation - MUTE AUDIO", true, settings.SmartMuteCritical);
        }

        bool isLeftEdge = reading.FrequencyHz < settings.StartHz + settings.EdgeIgnoreHz;
        bool isRightEdge = reading.FrequencyHz > settings.StopHz - settings.EdgeIgnoreHz;
        bool isEdge = isLeftEdge || isRightEdge;

        double centerHz = (settings.StartHz + settings.StopHz) * 0.5;
        bool isCenterDc = Math.Abs(reading.FrequencyHz - centerHz) <= 2_000;

        if (settings.IgnoreCenterDcSpike && isCenterDc && reading.LevelDbfs > settings.AlertDbfs)
            return ("CENTER_DC_SPIKE", "IGNORE", "Ignore center DC artifact", false, false);

        if (settings.IgnoreEdges && isEdge && reading.BandwidthHz >= settings.OverloadHz)
            return ("EDGE_OVERLOAD", "IGNORE", "Ignore band-edge front-end overload", false, false);

        bool isWide = reading.BandwidthHz >= settings.WidebandHz;
        bool isPulsed = reading.PulseScore >= settings.PulseScoreThreshold || reading.IsPulsed;

        if (isWide && isPulsed && reading.LevelDbfs >= settings.AlertDbfs)
            return ("PULSED_WIDEBAND", "CRITICAL", "MUTE AUDIO / radar-like pulsed wideband event", true, settings.SmartMuteCritical);

        if (isWide && reading.LevelDbfs >= settings.CriticalDbfs)
            return ("WIDEBAND", "CRITICAL", "MUTE AUDIO / wideband high-energy event", true, settings.SmartMuteCritical);

        string type;
        if (reading.BandwidthHz < 5_000)
            type = "CARRIER";
        else if (reading.BandwidthHz <= 20_000)
            type = "VOICE_OR_AM";
        else
            type = "WIDEBAND";

        type += GetHfBandName(reading.FrequencyHz);

        if (reading.LevelDbfs >= settings.CriticalDbfs)
            return (type, "CRITICAL", "MUTE AUDIO / critical receiver level", true, settings.SmartMuteCritical);

        if (reading.LevelDbfs >= settings.AlertDbfs)
            return (type, "ALERT", "Strong receiver level logged", true, false);

        if (isWide && reading.LevelDbfs >= settings.AlertDbfs - 5)
            return (type, "WATCH", "Wideband activity below alert threshold", true, false);

        return (type, "NORMAL", "No action", false, false);
    }

    private static string GetHfBandName(double freqHz)
    {
        double mhz = freqHz / 1_000_000.0;
        if (mhz >= 1.8 && mhz <= 2.0) return " (160m Amateur)";
        if (mhz >= 3.5 && mhz <= 4.0) return " (80m Amateur)";
        if (mhz >= 5.33 && mhz <= 5.41) return " (60m Amateur)";
        if (mhz >= 7.0 && mhz <= 7.3) return " (40m Amateur)";
        if (mhz >= 10.1 && mhz <= 10.15) return " (30m Amateur)";
        if (mhz >= 14.0 && mhz <= 14.35) return " (20m Amateur)";
        if (mhz >= 18.068 && mhz <= 18.168) return " (17m Amateur)";
        if (mhz >= 21.0 && mhz <= 21.45) return " (15m Amateur)";
        if (mhz >= 24.89 && mhz <= 24.99) return " (12m Amateur)";
        if (mhz >= 28.0 && mhz <= 29.7) return " (10m Amateur)";
        if (mhz >= 26.965 && mhz <= 27.405) return " (CB Radio)";
        if (mhz >= 8.7 && mhz <= 8.9) return " (Marine/Aviation)";
        if (mhz >= 13.0 && mhz <= 13.4) return " (Marine/Aviation)";
        
        // General SW broadcast bands
        if (mhz >= 5.9 && mhz <= 6.2) return " (49m Broadcast)";
        if (mhz >= 9.4 && mhz <= 9.9) return " (31m Broadcast)";
        if (mhz >= 11.6 && mhz <= 12.1) return " (25m Broadcast)";
        if (mhz >= 13.5 && mhz <= 13.9) return " (22m Broadcast)";
        if (mhz >= 15.1 && mhz <= 15.8) return " (19m Broadcast)";

        return "";
    }
}

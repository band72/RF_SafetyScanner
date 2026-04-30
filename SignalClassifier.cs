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

        if (reading.LevelDbfs >= settings.CriticalDbfs)
            return (type, "CRITICAL", "MUTE AUDIO / critical receiver level", true, settings.SmartMuteCritical);

        if (reading.LevelDbfs >= settings.AlertDbfs)
            return (type, "ALERT", "Strong receiver level logged", true, false);

        if (isWide && reading.LevelDbfs >= settings.AlertDbfs - 5)
            return (type, "WATCH", "Wideband activity below alert threshold", true, false);

        return (type, "NORMAL", "No action", false, false);
    }
}

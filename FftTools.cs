namespace RF_SafetyScanner;

public static class FftTools
{
    public static double[] BuildSyntheticSpectrum(double peakDbfs, double bandwidthHz, double spanHz, int bins)
    {
        var rnd = Random.Shared;
        var arr = new double[bins];

        for (int i = 0; i < bins; i++)
            arr[i] = -92 + rnd.NextDouble() * 7;

        int center = bins / 2;
        int halfWidthBins = Math.Max(1, (int)((bandwidthHz / spanHz) * bins / 2));

        for (int i = Math.Max(0, center - halfWidthBins); i <= Math.Min(bins - 1, center + halfWidthBins); i++)
        {
            double taper = 1.0 - Math.Abs(i - center) / (double)Math.Max(1, halfWidthBins + 1);
            arr[i] = Math.Max(arr[i], peakDbfs - (1 - taper) * 8 + rnd.NextDouble() * 2);
        }

        return arr;
    }

    public static double EstimateBandwidthFromSynthetic(double[] spectrumDbfs, double spanHz, double noiseFloorDbfs, double thresholdAboveNoiseDb = 12)
    {
        if (spectrumDbfs.Length == 0)
            return 0;

        double threshold = noiseFloorDbfs + thresholdAboveNoiseDb;
        int bins = spectrumDbfs.Count(x => x >= threshold);
        double binWidthHz = spanHz / spectrumDbfs.Length;
        return bins * binWidthHz;
    }
}

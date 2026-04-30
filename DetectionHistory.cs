namespace RF_SafetyScanner;

public sealed class DetectionHistory
{
    private readonly int _maxCount;
    private readonly Queue<double> _levels = new();

    public DetectionHistory(int maxCount = 16)
    {
        _maxCount = maxCount;
    }

    public double UpdateAndGetPulseScore(double levelDbfs)
    {
        _levels.Enqueue(levelDbfs);
        while (_levels.Count > _maxCount)
            _levels.Dequeue();

        if (_levels.Count < 5)
            return 0;

        var arr = _levels.ToArray();
        double avg = arr.Average();
        double variance = arr.Select(x => (x - avg) * (x - avg)).Average();
        double stdev = Math.Sqrt(variance);

        int directionChanges = 0;
        for (int i = 2; i < arr.Length; i++)
        {
            double d1 = arr[i - 1] - arr[i - 2];
            double d2 = arr[i] - arr[i - 1];
            if (Math.Sign(d1) != Math.Sign(d2) && Math.Abs(d1) > 1.0 && Math.Abs(d2) > 1.0)
                directionChanges++;
        }

        double stdevScore = Math.Clamp(stdev / 12.0, 0, 1);
        double changeScore = Math.Clamp(directionChanges / 8.0, 0, 1);

        return Math.Clamp((stdevScore * 0.65) + (changeScore * 0.35), 0, 1);
    }
}

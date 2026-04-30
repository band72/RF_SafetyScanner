namespace RF_SafetyScanner;

public interface IRadioScanner : IDisposable
{
    IAsyncEnumerable<SignalReading> ScanAsync(CancellationToken token);
}

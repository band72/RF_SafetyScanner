using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Media;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RF_SafetyScanner;

public partial class MainWindow : Window
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    private const int WM_APPCOMMAND = 0x319;
    private const int APPCOMMAND_VOLUME_MUTE = 0x80000;

    private CancellationTokenSource? _cts;
    private readonly ObservableCollection<SignalEvent> _events = new();
    private readonly Queue<double> _pulseVisual = new();
    private string _csvPath = "";
    private string _jsonPath = "";
    private bool _muted;

    public MainWindow()
    {
        InitializeComponent();
        EventGrid.ItemsSource = _events;
        InitializeLogs();
    }

    private void InitializeLogs()
    {
        var logDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        _csvPath = System.IO.Path.Combine(logDir, $"rf_events_v3_{stamp}.csv");
        _jsonPath = System.IO.Path.Combine(logDir, $"rf_events_v3_{stamp}.jsonl");

        File.WriteAllText(_csvPath, "Timestamp,FrequencyMHz,LevelDbfs,BandwidthKhz,PulseScore,SignalType,Severity,Action\n");
        Log($"CSV log: {_csvPath}");
        Log($"JSONL log: {_jsonPath}");
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var settings))
            return;

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        _cts = new CancellationTokenSource();
        StatusText.Text = "Scanning...";
        Log("Scan started.");

        IRadioScanner scanner;
        try
        {
            scanner = new HackRfScanner(settings);
            Log("HackRF scanner loaded.");
        }
        catch (Exception ex)
        {
            if (UseMockBox.IsChecked == true)
            {
                scanner = new MockScanner(settings);
                Log($"HackRF unavailable, using mock scanner. Reason: {ex.Message}");
            }
            else
            {
                Log($"HackRF unavailable: {ex.Message}");
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                StatusText.Text = "Stopped";
                return;
            }
        }

        try
        {
            await Task.Run(async () =>
            {
                await foreach (var reading in scanner.ScanAsync(_cts.Token))
                {
                    Dispatcher.Invoke(() => ProcessReading(reading, settings));
                }
            });
        }
        catch (OperationCanceledException)
        {
            Log("Scan stopped.");
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
        }
        finally
        {
            scanner.Dispose();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            StatusText.Text = "Stopped";
            SetMute(false);
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseButton.IsEnabled = false;
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            await Task.Delay(250); // allow scanner.Dispose() to finish cleanly
        }
        Application.Current.Shutdown();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _cts?.Cancel();
        base.OnClosing(e);
    }

    private bool TryReadSettings(out ScannerSettings settings)
    {
        settings = new ScannerSettings();
        double startMhz = 0, stopMhz = 0, stepKhz = 0, alert = 0, critical = 0, wideKhz = 0, overKhz = 0, edgeKhz = 0, pulseScore = 0;
        int dwellMs = 0;

        bool ok =
            double.TryParse(StartMhzBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out startMhz) &&
            double.TryParse(StopMhzBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out stopMhz) &&
            double.TryParse(StepKhzBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out stepKhz) &&
            int.TryParse(DwellMsBox.Text, out dwellMs) &&
            double.TryParse(AlertDbfsBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out alert) &&
            double.TryParse(CriticalDbfsBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out critical) &&
            double.TryParse(WidebandKhzBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out wideKhz) &&
            double.TryParse(OverloadKhzBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out overKhz) &&
            double.TryParse(EdgeIgnoreKhzBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out edgeKhz) &&
            double.TryParse(PulseScoreBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out pulseScore);

        if (!ok || startMhz <= 0 || stopMhz <= startMhz || stepKhz <= 0 || dwellMs < 20)
        {
            MessageBox.Show("Invalid scan settings.", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        settings.StartHz = startMhz * 1_000_000;
        settings.StopHz = stopMhz * 1_000_000;
        settings.StepHz = stepKhz * 1_000;
        settings.DwellMs = dwellMs;
        settings.AlertDbfs = alert;
        settings.CriticalDbfs = critical;
        settings.WidebandHz = wideKhz * 1_000;
        settings.OverloadHz = overKhz * 1_000;
        settings.EdgeIgnoreHz = edgeKhz * 1_000;
        settings.PulseScoreThreshold = pulseScore;
        settings.IgnoreEdges = IgnoreEdgesBox.IsChecked == true;
        settings.IgnoreCenterDcSpike = IgnoreCenterBox.IsChecked == true;
        settings.SmartMuteCritical = SmartMuteBox.IsChecked == true;
        return true;
    }

    private void ProcessReading(SignalReading reading, ScannerSettings settings)
    {
        var c = SignalClassifier.Classify(reading, settings);

        CurrentFreqText.Text = $"Frequency: {reading.FrequencyHz / 1_000_000.0:F3} MHz";
        CurrentLevelText.Text = $"Level: {reading.LevelDbfs:F1} dBFS";
        CurrentBandwidthText.Text = $"Bandwidth: {reading.BandwidthHz / 1_000.0:F1} kHz";
        CurrentPulseText.Text = $"Pulse score: {reading.PulseScore:F2}";
        CurrentTypeText.Text = $"Type: {c.type} / {c.severity}";

        double normalizedLevel = Math.Clamp(reading.LevelDbfs + 100, 0, 100);
        LevelProgressBar.Value = normalizedLevel;

        if (c.severity == "CRITICAL")
            LevelProgressBar.Foreground = Brushes.Red;
        else if (c.severity == "ALERT")
            LevelProgressBar.Foreground = Brushes.Orange;
        else
            LevelProgressBar.Foreground = Brushes.LimeGreen;

        DrawSpectrum(reading.SpectrumDbfs, settings.AlertDbfs);
        DrawPulse(reading.PulseScore);

        if (c.mute)
            SetMute(true);
        else if (_muted && c.severity != "CRITICAL")
            SetMute(false);

        if (c.log)
        {
            AddEvent(reading, c.type, c.severity, c.action);

            if (c.severity == "CRITICAL")
            {
                SystemSounds.Hand.Play();
                if (AutoStopBox.IsChecked == true)
                {
                    Log("AUTO-STOP triggered by CRITICAL event.");
                    _cts?.Cancel();
                }
            }
            else if (c.severity == "ALERT")
            {
                SystemSounds.Beep.Play();
            }
        }
    }

    private void SetMute(bool muted)
    {
        if (muted && !_muted)
        {
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                SendMessageW(helper.Handle, WM_APPCOMMAND, helper.Handle, (IntPtr)APPCOMMAND_VOLUME_MUTE);
                Log("OS Master Volume MUTE signal sent.");
            }
            catch { }
        }

        _muted = muted;
        MuteText.Text = muted ? "Mute state: CRITICAL MUTE REQUESTED" : "Mute state: clear";
        MuteText.Foreground = muted ? Brushes.Red : Brushes.Green;
    }

    private void AddEvent(SignalReading reading, string type, string severity, string action)
    {
        var ev = new SignalEvent
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            FrequencyMhz = reading.FrequencyHz / 1_000_000.0,
            LevelDbfs = reading.LevelDbfs,
            BandwidthKhz = reading.BandwidthHz / 1_000.0,
            PulseScore = reading.PulseScore,
            SignalType = type,
            Severity = severity,
            Action = action
        };

        _events.Insert(0, ev);
        if (_events.Count > 1000)
            _events.RemoveAt(_events.Count - 1);

        File.AppendAllText(_csvPath, $"{ev.Timestamp},{ev.FrequencyMhz:F6},{ev.LevelDbfs:F2},{ev.BandwidthKhz:F2},{ev.PulseScore:F3},{ev.SignalType},{ev.Severity},\"{ev.Action}\"\n");
        File.AppendAllText(_jsonPath, JsonSerializer.Serialize(ev) + "\n");

        Log($"{ev.Severity}: {ev.SignalType} {ev.FrequencyMhz:F3} MHz {ev.LevelDbfs:F1} dBFS BW {ev.BandwidthKhz:F1} kHz Pulse {ev.PulseScore:F2}");
    }

    private void DrawSpectrum(double[] spectrum, double alertDbfs)
    {
        SpectrumCanvas.Children.Clear();
        if (spectrum.Length < 2)
            return;

        double w = Math.Max(1, SpectrumCanvas.ActualWidth);
        double h = Math.Max(1, SpectrumCanvas.ActualHeight);

        var poly = new Polyline { Stroke = Brushes.LightSkyBlue, StrokeThickness = 1.5 };

        for (int i = 0; i < spectrum.Length; i++)
        {
            double x = i * w / (spectrum.Length - 1);
            double clamped = Math.Clamp(spectrum[i], -100, -20);
            double y = h - ((clamped + 100) / 80.0 * h);
            poly.Points.Add(new Point(x, y));
        }

        SpectrumCanvas.Children.Add(poly);

        var alertLine = new Line
        {
            X1 = 0, X2 = w,
            Y1 = h - ((alertDbfs + 100) / 80.0 * h),
            Y2 = h - ((alertDbfs + 100) / 80.0 * h),
            Stroke = Brushes.Orange,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        SpectrumCanvas.Children.Add(alertLine);
    }

    private void DrawPulse(double pulseScore)
    {
        PulseCanvas.Children.Clear();
        while (_pulseVisual.Count > 80)
            _pulseVisual.Dequeue();

        _pulseVisual.Enqueue(pulseScore);

        double w = Math.Max(1, PulseCanvas.ActualWidth);
        double h = Math.Max(1, PulseCanvas.ActualHeight);
        var arr = _pulseVisual.ToArray();

        if (arr.Length < 2)
            return;

        var poly = new Polyline { Stroke = Brushes.OrangeRed, StrokeThickness = 2 };

        for (int i = 0; i < arr.Length; i++)
        {
            double x = i * w / Math.Max(1, arr.Length - 1);
            double y = h - (Math.Clamp(arr[i], 0, 1) * h);
            poly.Points.Add(new Point(x, y));
        }

        PulseCanvas.Children.Add(poly);
    }

    private void Log(string msg)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        LogBox.ScrollToEnd();
    }
}

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SumraX.Core.Interfaces;

namespace SumraX.App.Views;

public partial class BootstrapWindow : Window
{
    private readonly DispatcherTimer _dotsTimer;
    private int _dotFrame;
    private double _progress;
    private double _trackWidth = 360;

    public ObservableCollection<string> BootLines { get; } = [];

    public BootstrapWindow(ILocalizationService? loc = null)
    {
        InitializeComponent();
        BootLog.ItemsSource = BootLines;

        StatusText.Text = loc?.Get("BootstrapInitializing") ?? "Initializing...";
        AppendLog("> sumrax.core.boot()");
        AppendLog("> loading secure modules...");

        _dotsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
        _dotsTimer.Tick += (_, _) =>
        {
            _dotFrame = (_dotFrame + 1) % 4;
            DotsText.Text = new string('.', _dotFrame);
        };

        Loaded += OnLoaded;
        SizeChanged += (_, _) => UpdateProgressVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _trackWidth = Math.Max(ActualWidth - 40, 160);
        ((Storyboard)FindResource("BootStoryboard")).Begin(this, true);
        _dotsTimer.Start();
        SetProgress(0.04);
    }

    public void SetStatus(string message) => RunUi(() => StatusText.Text = message);

    public void SetPhase(string phase) => RunUi(() => PhaseText.Text = phase);

    public void SetProgress(double value) => RunUi(() =>
    {
        _progress = Math.Clamp(value, 0, 1);
        UpdateProgressVisual();
    });

    public void BumpProgress(double delta) => SetProgress(_progress + delta);

    public void AppendLog(string line) => RunUi(() =>
    {
        BootLines.Add(line);
        while (BootLines.Count > 4)
        {
            BootLines.RemoveAt(0);
        }

        LastLog.Text = line;
        LogScroll.ScrollToEnd();
    });

    public async Task CloseAnimatedAsync()
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(CloseAnimatedAsync);
            return;
        }

        if (!IsVisible)
        {
            return;
        }

        _dotsTimer.Stop();
        SetProgress(1);
        AppendLog("> system ready.");
        await Task.Delay(200);

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(350))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var tcs = new TaskCompletionSource();
        fade.Completed += (_, _) => tcs.TrySetResult();
        BeginAnimation(OpacityProperty, fade);
        await tcs.Task;
        Close();
    }

    private void RunUi(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.BeginInvoke(action);
        }
    }

    private void UpdateProgressVisual()
    {
        var width = _trackWidth * _progress;
        ProgressFill.Width = width;
        PercentText.Text = $"{(int)(_progress * 100)}%";
    }
}

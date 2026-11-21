using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace SumraX.App.Views.Controls;

public partial class SumrakPowerButton : UserControl
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(System.Windows.Input.ICommand), typeof(SumrakPowerButton));

    public static readonly DependencyProperty IsConnectedProperty =
        DependencyProperty.Register(nameof(IsConnected), typeof(bool), typeof(SumrakPowerButton),
            new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty IsConnectingProperty =
        DependencyProperty.Register(nameof(IsConnecting), typeof(bool), typeof(SumrakPowerButton),
            new PropertyMetadata(false, OnVisualChanged));

    private Storyboard? _pulse;
    private Storyboard? _spin;

    public System.Windows.Input.ICommand? Command
    {
        get => (System.Windows.Input.ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public bool IsConnected
    {
        get => (bool)GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    public bool IsConnecting
    {
        get => (bool)GetValue(IsConnectingProperty);
        set => SetValue(IsConnectingProperty, value);
    }

    public SumrakPowerButton()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _pulse = (Storyboard)FindResource("PulseRing");
            _spin = (Storyboard)FindResource("SpinRing");
            ApplyVisual();
        };
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SumrakPowerButton b && b.IsLoaded)
        {
            b.ApplyVisual();
        }
    }

    private void ApplyVisual()
    {
        _pulse?.Stop(RingOuter);
        _spin?.Stop(RingSpin);
        RingSpin.Opacity = 0;

        if (IsConnecting)
        {
            RingSpin.Opacity = 0.9;
            _spin?.Begin(RingSpin, true);
        }
        else if (IsConnected)
        {
            _pulse?.Begin(RingOuter, true);
        }
    }
}
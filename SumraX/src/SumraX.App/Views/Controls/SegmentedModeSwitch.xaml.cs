using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace SumraX.App.Views.Controls;

public partial class SegmentedModeSwitch : UserControl
{
    public static readonly DependencyProperty IsTunModeProperty =
        DependencyProperty.Register(nameof(IsTunMode), typeof(bool), typeof(SegmentedModeSwitch),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnModeChanged));

    public static readonly DependencyProperty LeftLabelProperty =
        DependencyProperty.Register(nameof(LeftLabel), typeof(string), typeof(SegmentedModeSwitch), new PropertyMetadata("Proxy"));

    public static readonly DependencyProperty RightLabelProperty =
        DependencyProperty.Register(nameof(RightLabel), typeof(string), typeof(SegmentedModeSwitch), new PropertyMetadata("TUN"));

    public SegmentedModeSwitch()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisual(IsTunMode, animate: false);
    }

    public bool IsTunMode
    {
        get => (bool)GetValue(IsTunModeProperty);
        set => SetValue(IsTunModeProperty, value);
    }

    public string LeftLabel
    {
        get => (string)GetValue(LeftLabelProperty);
        set => SetValue(LeftLabelProperty, value);
    }

    public string RightLabel
    {
        get => (string)GetValue(RightLabelProperty);
        set => SetValue(RightLabelProperty, value);
    }

    private void OnProxyClick(object sender, RoutedEventArgs e) => IsTunMode = false;

    private void OnTunClick(object sender, RoutedEventArgs e) => IsTunMode = true;

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SegmentedModeSwitch sw && e.NewValue is bool tun)
        {
            sw.UpdateVisual(tun, animate: sw.IsLoaded);
        }
    }

    private void UpdateVisual(bool tun, bool animate)
    {
        var target = tun ? 88.0 : 0.0;
        if (animate)
        {
            HighlightShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
                new DoubleAnimation(target, TimeSpan.FromMilliseconds(160))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
        }
        else
        {
            HighlightShift.X = target;
        }

        ProxyText.Foreground = tun
            ? (System.Windows.Media.Brush)FindResource("TextSecondaryBrush")
            : (System.Windows.Media.Brush)FindResource("NeonGreenBrush");
        TunText.Foreground = tun
            ? (System.Windows.Media.Brush)FindResource("NeonGreenBrush")
            : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
    }
}

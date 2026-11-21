using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace SumraX.App.Views.Controls;

public partial class SpeedGaugeControl : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty SpeedMbpsProperty =
        DependencyProperty.Register(nameof(SpeedMbps), typeof(double), typeof(SpeedGaugeControl),
            new PropertyMetadata(0.0, OnSpeedChanged));

    public static readonly DependencyProperty MaxMbpsProperty =
        DependencyProperty.Register(nameof(MaxMbps), typeof(double), typeof(SpeedGaugeControl),
            new PropertyMetadata(1000.0, OnSpeedChanged));

    public static readonly DependencyProperty UnitLabelProperty =
        DependencyProperty.Register(nameof(UnitLabel), typeof(string), typeof(SpeedGaugeControl),
            new PropertyMetadata("Mbps"));

    public double SpeedMbps
    {
        get => (double)GetValue(SpeedMbpsProperty);
        set => SetValue(SpeedMbpsProperty, value);
    }

    public double MaxMbps
    {
        get => (double)GetValue(MaxMbpsProperty);
        set => SetValue(MaxMbpsProperty, value);
    }

    public string UnitLabel
    {
        get => (string)GetValue(UnitLabelProperty);
        set => SetValue(UnitLabelProperty, value);
    }

    public SpeedGaugeControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateGauge();
    }

    private static void OnSpeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpeedGaugeControl gauge)
        {
            gauge.UpdateGauge();
        }
    }

    private void UpdateGauge()
    {
        if (!IsLoaded)
        {
            return;
        }

        var ratio = MaxMbps <= 0 ? 0 : Math.Clamp(SpeedMbps / MaxMbps, 0, 1);
        var angle = -135 + ratio * 270;
        NeedleRotate.Angle = angle;

        var arcRatio = ratio * 0.75;
        ValueArc.StrokeDashArray = new DoubleCollection { arcRatio * 100, 100 - arcRatio * 100 };
        SpeedText.Text = SpeedMbps < 0.05 ? "—" : SpeedMbps.ToString("0.00", CultureInfo.CurrentCulture);
    }
}

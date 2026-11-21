using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace SumraX.App.Views.Controls;

public partial class AnimatedShieldSword : UserControl
{
    private static readonly Color IceCyan = Color.FromRgb(0, 232, 255);
    private static readonly Color IceBright = Color.FromRgb(184, 244, 255);
    private static readonly Color NeonBlue = Color.FromRgb(0, 168, 255);
    private static readonly Color NeonGreen = Color.FromRgb(0, 255, 136);
    private static readonly Color MutedBorder = Color.FromRgb(58, 69, 85);

    public static readonly DependencyProperty IsProtectedProperty =
        DependencyProperty.Register(nameof(IsProtected), typeof(bool), typeof(AnimatedShieldSword),
            new PropertyMetadata(false, OnVisualStateChanged));

    public static readonly DependencyProperty IsConnectingProperty =
        DependencyProperty.Register(nameof(IsConnecting), typeof(bool), typeof(AnimatedShieldSword),
            new PropertyMetadata(false, OnVisualStateChanged));

    public static readonly DependencyProperty IsTunModeProperty =
        DependencyProperty.Register(nameof(IsTunMode), typeof(bool), typeof(AnimatedShieldSword),
            new PropertyMetadata(true, OnVisualStateChanged));

    private Storyboard? _idleStoryboard;
    private Storyboard? _connectingStoryboard;
    private Storyboard? _tunConnectingStoryboard;
    private Storyboard? _iceRevealStoryboard;
    private Storyboard? _iceShimmerStoryboard;
    private bool _wasProtected;

    public bool IsProtected
    {
        get => (bool)GetValue(IsProtectedProperty);
        set => SetValue(IsProtectedProperty, value);
    }

    public bool IsConnecting
    {
        get => (bool)GetValue(IsConnectingProperty);
        set => SetValue(IsConnectingProperty, value);
    }

    public bool IsTunMode
    {
        get => (bool)GetValue(IsTunModeProperty);
        set => SetValue(IsTunModeProperty, value);
    }

    public AnimatedShieldSword()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _idleStoryboard = (Storyboard)FindResource("IdleStoryboard");
        _connectingStoryboard = (Storyboard)FindResource("ConnectingStoryboard");
        _tunConnectingStoryboard = (Storyboard)FindResource("TunConnectingStoryboard");
        _iceRevealStoryboard = (Storyboard)FindResource("IceRevealStoryboard");
        _iceShimmerStoryboard = (Storyboard)FindResource("IceShimmerStoryboard");
        ApplyVisualState();
    }

    private static void OnVisualStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedShieldSword control && control.IsLoaded)
        {
            control.ApplyVisualState();
        }
    }

    private void ApplyVisualState()
    {
        StopAllStoryboards();
        ResetMonoblockTransform();
        ResetRingRotations();

        if (IsConnecting)
        {
            ApplyConnectingLook();
            return;
        }

        if (IsProtected)
        {
            ApplyProtectedLook();
            return;
        }

        ApplyIdleLook();
    }

    private void ResetMonoblockTransform()
    {
        MonoblockViewbox.RenderTransform = null;
    }

    private void ResetRingRotations()
    {
        RingOuterRotate.Angle = 0;
        RingInnerRotate.Angle = 0;
        TunArcRotate.Angle = 0;
        PulseRing1.Opacity = 0;
        PulseRing2.Opacity = 0;
        TunArc.Opacity = 0;
        ScanLine.Opacity = 0;
        ConnectLabel.Opacity = 0;
    }

    private void ApplyConnectingLook()
    {
        _wasProtected = false;
        HideBlockService();
        ShowIdleDecor(false);

        ConnectLabel.Text = IsTunMode ? "TUN" : "VPN";
        ConnectLabel.Foreground = new SolidColorBrush(IsTunMode ? NeonGreen : IceCyan);
        ConnectLabel.Opacity = 0.75;
        ScanLine.Opacity = IsTunMode ? 0.85 : 0.55;
        ScanLine.Fill = new SolidColorBrush(IsTunMode ? Color.FromArgb(0x88, 0, 255, 136) : Color.FromArgb(0x88, 0, 232, 255));

        ScreenBezel.Background = (Brush)FindResource("BackGradient");
        ScreenBezel.BorderBrush = new SolidColorBrush(IsTunMode ? NeonGreen : NeonBlue);
        RingOuter.Stroke = new SolidColorBrush(IsTunMode ? NeonGreen : NeonBlue);
        RingOuter.Opacity = 0.55;
        RingInner.Stroke = new SolidColorBrush(IsTunMode ? IceCyan : NeonBlue);
        RingInner.Opacity = 0.35;
        IceRing.Opacity = 0;
        StatusBar.Fill = new SolidColorBrush(IsTunMode ? NeonGreen : NeonBlue);
        StatusBar.Opacity = 0.55;
        SetScreenGlow(IsTunMode ? NeonGreen : NeonBlue, IsTunMode ? 0.55 : 0.35);

        PulseRing1.Stroke = new SolidColorBrush(IsTunMode ? NeonGreen : NeonBlue);
        PulseRing1.Opacity = 0.15;
        PulseRing2.Stroke = new SolidColorBrush(NeonGreen);
        PulseRing2.Opacity = IsTunMode ? 0.08 : 0;

        if (IsTunMode)
        {
            TunArc.Opacity = 0.85;
            BeginStoryboardSafe(_tunConnectingStoryboard);
        }
        else
        {
            BeginStoryboardSafe(_connectingStoryboard);
        }
    }

    private void ApplyProtectedLook()
    {
        ShowIdleDecor(false);
        ScreenBezel.Background = (Brush)FindResource("IceScreenGradient");
        ScreenBezel.BorderBrush = new SolidColorBrush(IceCyan);
        RingOuter.Stroke = new SolidColorBrush(IceCyan);
        RingOuter.Opacity = 0.55;
        RingInner.Stroke = new SolidColorBrush(IceBright);
        RingInner.Opacity = 0.5;
        StatusBar.Fill = new SolidColorBrush(IceCyan);
        StatusBar.Opacity = 0.85;
        SetScreenGlow(IceCyan, 0.7);

        if (!_wasProtected)
        {
            HideBlockService();
            BlockServiceScale.ScaleX = 0.72;
            BlockServiceScale.ScaleY = 0.72;
            BeginStoryboardSafe(_iceRevealStoryboard);
        }
        else
        {
            BlockServiceLabel.Opacity = 1;
            BlockServiceBloom.Opacity = 0.95;
            IceScreenGlow.Opacity = 0.55;
            IceRing.Opacity = 0.75;
        }

        BeginStoryboardSafe(_iceShimmerStoryboard);
        _wasProtected = true;
    }

    private void ApplyIdleLook()
    {
        _wasProtected = false;
        HideBlockService();
        ShowIdleDecor(true);

        ScreenBezel.Background = (Brush)FindResource("BackGradient");
        ScreenBezel.BorderBrush = new SolidColorBrush(MutedBorder);
        RingOuter.Stroke = new SolidColorBrush(NeonBlue);
        RingOuter.Opacity = 0.32;
        RingInner.Stroke = new SolidColorBrush(Color.FromRgb(68, 214, 44));
        RingInner.Opacity = 0.22;
        IceRing.Opacity = 0;
        StatusBar.Fill = new SolidColorBrush(Color.FromRgb(0, 212, 255));
        StatusBar.Opacity = 0.35;
        SetScreenGlow(NeonBlue, 0.22);
        BeginStoryboardSafe(_idleStoryboard);
    }

    private void BeginStoryboardSafe(Storyboard? storyboard)
    {
        if (storyboard == null)
        {
            return;
        }

        try
        {
            storyboard.Begin(this, true);
        }
        catch
        {
            // Storyboard target names may be outside the namescope; static visuals still apply.
        }
    }

    private void HideBlockService()
    {
        BlockServiceLabel.Opacity = 0;
        BlockServiceBloom.Opacity = 0;
        IceScreenGlow.Opacity = 0;
        IceFlash.Opacity = 0;
        IceRing.Opacity = 0;
    }

    private void ShowIdleDecor(bool visible)
    {
        IdleScreenDecor.Opacity = visible ? 1 : 0;
    }

    private void StopAllStoryboards()
    {
        _idleStoryboard?.Stop(this);
        _connectingStoryboard?.Stop(this);
        _tunConnectingStoryboard?.Stop(this);
        _iceRevealStoryboard?.Stop(this);
        _iceShimmerStoryboard?.Stop(this);
    }

    private void SetScreenGlow(Color color, double opacity)
    {
        if (ScreenBezel.Effect is DropShadowEffect glow)
        {
            glow.Color = color;
            glow.Opacity = opacity;
        }
    }
}

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using SumraX.App.ViewModels;

namespace SumraX.App.Views;

public partial class ServerPickerOverlay : UserControl
{
    private MainViewModel? _vm;
    private bool _animating;

    public ServerPickerOverlay()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => HookViewModel();
        Loaded += (_, _) => HookViewModel();
    }

    private void HookViewModel()
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = DataContext as MainViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsServerPickerOpen) || _vm == null || _animating)
        {
            return;
        }

        if (_vm.IsServerPickerOpen)
        {
            ShowAnimated();
        }
        else
        {
            HideAnimated();
        }
    }

    private void ShowAnimated()
    {
        _animating = true;
        Visibility = Visibility.Visible;
        IsHitTestVisible = true;
        UpdateLayout();

        var height = Sheet.ActualHeight > 0 ? Sheet.ActualHeight : 720;
        SheetTransform.Y = height;

        var anim = new DoubleAnimation(height, 0, TimeSpan.FromMilliseconds(360))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        anim.Completed += (_, _) => _animating = false;
        SheetTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, anim);
    }

    private void HideAnimated()
    {
        _animating = true;
        var height = Sheet.ActualHeight > 0 ? Sheet.ActualHeight : 720;

        var anim = new DoubleAnimation(0, height, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        anim.Completed += (_, _) =>
        {
            Visibility = Visibility.Collapsed;
            IsHitTestVisible = false;
            _animating = false;
        };
        SheetTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, anim);
    }

    private void Backdrop_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _vm?.CloseServerPickerCommand.Execute(null);
    }
}

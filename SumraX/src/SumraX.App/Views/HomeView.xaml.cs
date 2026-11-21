using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using SumraX.App.ViewModels;

namespace SumraX.App.Views;

public partial class HomeView : UserControl
{
    private HomeViewModel? _vm;
    private Storyboard? _pulse;

    public HomeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) =>
        {
            StopPulse();
            DetachVm();
        };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachVm();
        if (e.NewValue is HomeViewModel vm)
        {
            _vm = vm;
            vm.PropertyChanged += OnVmPropertyChanged;
            if (vm.ShowBlockServiceToast)
            {
                PlayIn();
            }
        }
    }

    private void DetachVm()
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeViewModel.ShowBlockServiceToast) || _vm == null)
        {
            return;
        }

        if (_vm.ShowBlockServiceToast)
        {
            PlayIn();
        }
        else
        {
            PlayOut();
        }
    }

    private void PlayIn()
    {
        BlockServiceBanner.Visibility = Visibility.Visible;
        ((Storyboard)FindResource("BlockServiceIn")).Begin(this, true);
        _pulse ??= (Storyboard)FindResource("BlockServicePulse");
        _pulse.Begin(this, true);
    }

    private void PlayOut()
    {
        StopPulse();
        var sb = (Storyboard)FindResource("BlockServiceOut");
        sb.Completed -= OnOutCompleted;
        sb.Completed += OnOutCompleted;
        sb.Begin(this, true);
    }

    private void StopPulse()
    {
        _pulse?.Stop(this);
    }

    private static void OpenButtonContextMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu == null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void HomeAddButton_Click(object sender, RoutedEventArgs e) => OpenButtonContextMenu(sender, e);

    private void HomeGroupMoreButton_Click(object sender, RoutedEventArgs e) => OpenButtonContextMenu(sender, e);

    private void OnOutCompleted(object? sender, EventArgs e)
    {
        if (sender is Storyboard sb)
        {
            sb.Completed -= OnOutCompleted;
        }

        if (_vm is { ShowBlockServiceToast: false })
        {
            BlockServiceBanner.Visibility = Visibility.Collapsed;
        }
    }
}

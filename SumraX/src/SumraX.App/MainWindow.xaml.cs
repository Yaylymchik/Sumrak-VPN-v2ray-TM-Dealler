using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using SumraX.App.ViewModels;
using SumraX.Core.Interfaces;

namespace SumraX.App;

public partial class MainWindow : Window
{
    private const double CollapsedWidth = 56;
    private const double ExpandedWidth = 196;

    private MainViewModel Vm => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.HostApp.Services.GetRequiredService<MainViewModel>();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncNavSelection(Vm.SelectedNavIndex);
        ApplySidebarWidth(Vm.IsSidebarExpanded ? ExpandedWidth : CollapsedWidth, animate: false);
        Vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedNavIndex))
        {
            SyncNavSelection(Vm.SelectedNavIndex);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsSidebarExpanded))
        {
            ApplySidebarWidth(Vm.IsSidebarExpanded ? ExpandedWidth : CollapsedWidth, animate: true);
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (App.RequestedShutdown)
        {
            return;
        }

        var settings = App.HostApp.Services.GetRequiredService<ISettingsRepository>();
        var s = await settings.GetAsync();
        if (!s.MinimizeToTray)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void Sidebar_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Vm.CollapseSidebarIfUnpinned();
    }

    private void ApplySidebarWidth(double width, bool animate)
    {
        SidebarBorder.BeginAnimation(WidthProperty, null);
        if (!animate)
        {
            SidebarBorder.Width = width;
            return;
        }

        var anim = new DoubleAnimation
        {
            To = width,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        SidebarBorder.BeginAnimation(WidthProperty, anim);
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string tag && int.TryParse(tag, out var index))
        {
            Vm.SelectedNavIndex = index;
        }
    }

    private void SyncNavSelection(int index)
    {
        foreach (var rb in FindNavButtons())
        {
            if (rb.Tag is string tag && int.TryParse(tag, out var i))
            {
                rb.IsChecked = i == index;
            }
        }
    }

    private IEnumerable<System.Windows.Controls.RadioButton> FindNavButtons()
    {
        if (Content is not DependencyObject root)
        {
            yield break;
        }

        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is System.Windows.Controls.RadioButton rb && rb.GroupName == "Nav")
            {
                yield return rb;
            }

            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < count; i++)
            {
                queue.Enqueue(System.Windows.Media.VisualTreeHelper.GetChild(current, i));
            }
        }
    }
}

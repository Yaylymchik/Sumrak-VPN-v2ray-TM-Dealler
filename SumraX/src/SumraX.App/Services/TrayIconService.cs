using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Hardcodet.Wpf.TaskbarNotification;
using SumraX.App.ViewModels;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;

namespace SumraX.App.Services;

public sealed class TrayIconService : IDisposable
{
    private static readonly SolidColorBrush MenuBg = Freeze(Color.FromRgb(0x0A, 0x0A, 0x0B));
    private static readonly SolidColorBrush MenuBorder = Freeze(Color.FromRgb(0xFF, 0xD4, 0x00));
    private static readonly SolidColorBrush ItemFg = Freeze(Color.FromRgb(0xF5, 0xF5, 0xF5));
    private static readonly SolidColorBrush MutedFg = Freeze(Color.FromRgb(0x9A, 0x9A, 0x9A));
    private static readonly SolidColorBrush AccentFg = Freeze(Color.FromRgb(0xFF, 0xD4, 0x00));
    private static readonly SolidColorBrush HoverBg = Freeze(Color.FromRgb(0x1A, 0x1A, 0x12));

    private static readonly Style MenuItemStyle = CreateMenuItemStyle();
    private static readonly ControlTemplate MenuTemplate = CreateContextMenuTemplate();

    private readonly IVpnEngine _vpn;
    private readonly MainViewModel _main;
    private readonly ILocalizationService _loc;
    private TaskbarIcon? _icon;
    private Window? _window;

    public TrayIconService(IVpnEngine vpn, MainViewModel main, ILocalizationService loc)
    {
        _vpn = vpn;
        _main = main;
        _loc = loc;
        _vpn.StatusChanged += (_, _) =>
        {
            UpdateTooltip();
            RebuildMenu();
        };
        _loc.LanguageChanged += (_, _) => RebuildMenu();
    }

    public void Attach(Window window)
    {
        _window = window;

        _icon = new TaskbarIcon
        {
            ToolTipText = _loc.Get("AppTitle"),
            ContextMenu = BuildMenu()
        };

        try
        {
            _icon.IconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/sumrax-icon-48.png", UriKind.Absolute));
        }
        catch
        {
            // tray works without custom icon
        }

        _icon.TrayMouseDoubleClick += (_, _) => ShowWindow();
        UpdateTooltip();
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu
        {
            Background = MenuBg,
            BorderBrush = MenuBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 5, 6, 5),
            Width = 196,
            FontFamily = new FontFamily("Segoe UI Semibold, Segoe UI"),
            FontSize = 12,
            Template = MenuTemplate,
            HasDropShadow = false
        };

        menu.Resources[SystemColors.MenuBrushKey] = MenuBg;
        menu.Resources[SystemColors.MenuBarBrushKey] = MenuBg;
        menu.Resources[SystemColors.MenuTextBrushKey] = ItemFg;
        menu.Resources[SystemColors.HighlightBrushKey] = HoverBg;
        menu.Resources[SystemColors.HighlightTextBrushKey] = ItemFg;
        menu.Resources[SystemColors.ControlTextBrushKey] = ItemFg;
        menu.Resources[SystemColors.GrayTextBrushKey] = MutedFg;

        var serverName = _main.Home.ServerDisplayName;
        var selectServerLabel = _loc.Get("SelectServer");
        if (!string.IsNullOrWhiteSpace(serverName) &&
            !string.Equals(serverName, selectServerLabel, StringComparison.Ordinal))
        {
            menu.Items.Add(CreateHeader(TruncateServerName(serverName)));
        }

        var connectLabel = _vpn.State == ConnectionState.Connected
            ? _loc.Get("Disconnect")
            : _loc.Get("Connect");
        menu.Items.Add(CreateItem(connectLabel, (_, _) => _ = ToggleConnectionAsync(), accent: true));
        menu.Items.Add(CreateItem(_loc.Get("TrayChangeServer"), (_, _) =>
        {
            ShowWindow();
            _ = _main.OpenServersCommand.ExecuteAsync(null);
        }));
        menu.Items.Add(CreateItem(_loc.Get("TrayImportClipboard"), (_, _) =>
        {
            ShowWindow();
            _ = _main.Servers.PasteAndImportCommand.ExecuteAsync(null);
        }));
        menu.Items.Add(CreateItem(_loc.Get("TrayRouting"), (_, _) =>
        {
            ShowWindow();
            _main.OpenSettingsRouting();
        }));
        // No Separator — WPF ItemContainerStyle/Separator mismatch crashed the app
        menu.Items.Add(CreateItem(_loc.Get("TrayShow"), (_, _) => ShowWindow()));
        menu.Items.Add(CreateItem(_loc.Get("TrayExit"), (_, _) =>
        {
            App.RequestShutdown();
            Application.Current.Shutdown();
        }));
        return menu;
    }

    private static ControlTemplate CreateContextMenuTemplate()
    {
        var template = new ControlTemplate(typeof(ContextMenu));
        var border = new FrameworkElementFactory(typeof(Border), "Bd");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

        var items = new FrameworkElementFactory(typeof(StackPanel));
        items.SetValue(Panel.IsItemsHostProperty, true);
        items.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
        border.AppendChild(items);
        template.VisualTree = border;
        return template;
    }

    private static Style CreateMenuItemStyle()
    {
        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.ForegroundProperty, ItemFg));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
        style.Setters.Add(new Setter(Control.TemplateProperty, CreateMenuItemTemplate()));
        return style;
    }

    private static ControlTemplate CreateMenuItemTemplate()
    {
        var template = new ControlTemplate(typeof(MenuItem));

        var border = new FrameworkElementFactory(typeof(Border), "Bd");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, MenuBorder);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        content.SetValue(TextElement.ForegroundProperty, new TemplateBindingExtension(Control.ForegroundProperty));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);

        template.VisualTree = border;

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, HoverBg, "Bd"));
        hover.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1), "Bd"));
        template.Triggers.Add(hover);

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(Control.ForegroundProperty, MutedFg));
        template.Triggers.Add(disabled);

        return template;
    }

    private static string TruncateServerName(string name)
    {
        const int max = 18;
        var clean = name.Replace("🎮", "").Trim();
        return clean.Length <= max ? clean : clean[..(max - 1)] + "…";
    }

    private static MenuItem CreateHeader(string header) => new()
    {
        Header = header,
        IsEnabled = false,
        Style = MenuItemStyle,
        Foreground = MutedFg,
        FontSize = 10,
        FontWeight = FontWeights.SemiBold,
        Padding = new Thickness(10, 4, 10, 6),
        Background = Brushes.Transparent
    };

    private static MenuItem CreateItem(string header, RoutedEventHandler handler, bool accent = false)
    {
        var item = new MenuItem
        {
            Header = header,
            Style = MenuItemStyle,
            Foreground = accent ? AccentFg : ItemFg,
            FontWeight = accent ? FontWeights.SemiBold : FontWeights.Normal,
            Padding = new Thickness(10, 6, 10, 6),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        item.Click += handler;
        return item;
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void RebuildMenu()
    {
        if (_icon == null)
        {
            return;
        }

        try
        {
            _icon.ContextMenu = BuildMenu();
            UpdateTooltip();
        }
        catch
        {
            // Never crash the app from tray menu rebuild
        }
    }

    private void UpdateTooltip()
    {
        if (_icon == null)
        {
            return;
        }

        var state = _vpn.State switch
        {
            ConnectionState.Connected => _loc.Get("Protected"),
            ConnectionState.Connecting or ConnectionState.Reconnecting => _loc.Get("Connecting"),
            _ => _loc.Get("NotConnected")
        };
        _icon.ToolTipText = $"{_loc.Get("AppTitle")} — {state}";
    }

    private void ShowWindow()
    {
        if (_window == null)
        {
            return;
        }

        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private async Task ToggleConnectionAsync()
    {
        await _main.Home.ToggleConnectionCommand.ExecuteAsync(null);
        UpdateTooltip();
        RebuildMenu();
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }
}

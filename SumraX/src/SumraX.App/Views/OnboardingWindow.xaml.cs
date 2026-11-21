using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Infrastructure.Regions;

namespace SumraX.App.Views;

public partial class OnboardingWindow : Window
{
    private readonly ILocalizationService _loc;
    private readonly ISettingsRepository _settings;
    private readonly IUserProfileRepository _userProfile;
    private readonly IRegionTimeService _regionTime;

    private int _step;
    private AppLanguage _language = AppLanguage.Russian;
    private string _regionCode = "RU";
    private string _userName = string.Empty;

    private TextBox? _nameBox;
    private ComboBox? _regionBox;

    public OnboardingWindow(
        ILocalizationService loc,
        ISettingsRepository settings,
        IUserProfileRepository userProfile,
        IRegionTimeService regionTime)
    {
        _loc = loc;
        _settings = settings;
        _userProfile = userProfile;
        _regionTime = regionTime;
        InitializeComponent();
        BuildDots();
        ShowStep(0);
        _loc.LanguageChanged += (_, _) => ShowStep(_step);
    }

    private void BuildDots()
    {
        DotsPanel.Children.Clear();
        for (var i = 0; i < 4; i++)
        {
            DotsPanel.Children.Add(new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(0x5A, 0x6A, 0x7A)),
                Margin = new Thickness(4, 0, 4, 0)
            });
        }
    }

    private void UpdateDots()
    {
        for (var i = 0; i < DotsPanel.Children.Count; i++)
        {
            if (DotsPanel.Children[i] is Border dot)
            {
                dot.Background = new SolidColorBrush(i <= _step
                    ? Color.FromRgb(0x00, 0xA8, 0xFF)
                    : Color.FromRgb(0x5A, 0x6A, 0x7A));
            }
        }
    }

    private void ShowStep(int step)
    {
        _step = step;
        StepContent.Children.Clear();
        BackButton.Visibility = step > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content = step == 3 ? _loc.Get("OnboardingFinish") : _loc.Get("OnboardingContinue");
        UpdateDots();
        AnimateContent();

        switch (step)
        {
            case 0:
                StepTitle.Text = _loc.Get("OnboardingWelcomeTitle");
                StepSubtitle.Text = _loc.Get("OnboardingWelcomeSubtitle");
                StepContent.Children.Add(MakeHeroText(_loc.Get("OnboardingWelcomeBody")));
                break;
            case 1:
                StepTitle.Text = _loc.Get("OnboardingLanguageTitle");
                StepSubtitle.Text = _loc.Get("OnboardingLanguageSubtitle");
                StepContent.Children.Add(MakeLanguageButtons());
                break;
            case 2:
                StepTitle.Text = _loc.Get("OnboardingRegionTitle");
                StepSubtitle.Text = _loc.Get("OnboardingRegionSubtitle");
                StepContent.Children.Add(MakeRegionPicker());
                break;
            case 3:
                StepTitle.Text = _loc.Get("OnboardingNameTitle");
                StepSubtitle.Text = _loc.Get("OnboardingNameSubtitle");
                StepContent.Children.Add(MakeNameInput());
                break;
        }
    }

    private UIElement MakeHeroText(string text) =>
        new TextBlock
        {
            Text = text,
            FontFamily = (FontFamily)FindResource("PrimaryFont"),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 40, 0, 0)
        };

    private UIElement MakeLanguageButtons()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        panel.Children.Add(MakeChoiceButton(_loc.Get("LangEnglish"), AppLanguage.English));
        panel.Children.Add(MakeChoiceButton(_loc.Get("LangRussian"), AppLanguage.Russian));
        panel.Children.Add(MakeChoiceButton(_loc.Get("LangTurkmen"), AppLanguage.Turkmen));
        return panel;
    }

    private Button MakeChoiceButton(string label, AppLanguage lang)
    {
        var btn = new Button
        {
            Content = label,
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(16, 14, 16, 14),
            Background = _language == lang
                ? new SolidColorBrush(Color.FromRgb(0x00, 0xA8, 0xFF))
                : new SolidColorBrush(Color.FromRgb(0x23, 0x22, 0x28)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xA8, 0xFF)),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        btn.Click += (_, _) =>
        {
            _language = lang;
            _loc.SetLanguage(lang);
            ShowStep(_step);
        };
        return btn;
    }

    private UIElement MakeRegionPicker()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        _regionBox = new ComboBox
        {
            Style = (Style)FindResource("ComboPremium"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        foreach (var region in RegionCatalog.All)
        {
            _regionBox.Items.Add($"{region.DisplayName} ({region.UtcOffsetLabel})");
        }

        var idx = RegionCatalog.All.ToList().FindIndex(r => r.Code == _regionCode);
        _regionBox.SelectedIndex = idx >= 0 ? idx : 0;
        panel.Children.Add(_regionBox);
        panel.Children.Add(new TextBlock
        {
            Text = _loc.Get("OnboardingRegionHint"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontFamily = (FontFamily)FindResource("PrimaryFont"),
            FontSize = 12
        });
        return panel;
    }

    private UIElement MakeNameInput()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 30, 0, 0) };
        _nameBox = new TextBox
        {
            Style = (Style)FindResource("SearchBox"),
            Text = _userName,
            FontSize = 16
        };
        panel.Children.Add(_nameBox);
        return panel;
    }

    private void AnimateContent()
    {
        if (Resources["SlideIn"] is Storyboard sb)
        {
            sb.Begin(this);
        }
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_step == 2 && _regionBox != null && _regionBox.SelectedIndex >= 0)
        {
            _regionCode = RegionCatalog.All[_regionBox.SelectedIndex].Code;
        }

        if (_step == 3)
        {
            _userName = _nameBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_userName))
            {
                MessageBox.Show(_loc.Get("OnboardingNameRequired"), _loc.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await FinishAsync();
            return;
        }

        ShowStep(_step + 1);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_step > 0)
        {
            ShowStep(_step - 1);
        }
    }

    private async Task FinishAsync()
    {
        var region = RegionCatalog.Find(_regionCode) ?? RegionCatalog.All[0];
        await _regionTime.ApplyRegionAsync(region.Code);

        var appSettings = await _settings.GetAsync();
        appSettings.OnboardingCompleted = true;
        appSettings.Language = _language;
        appSettings.RegionCode = region.Code;
        appSettings.TimeZoneId = region.WindowsTimeZoneId;
        appSettings.UserDisplayName = _userName;
        await _settings.SaveAsync(appSettings);
        _loc.SetLanguage(_language);

        var profile = await _userProfile.GetAsync();
        profile.DisplayName = _userName;
        await _userProfile.SaveAsync(profile);

        DialogResult = true;
        Close();
    }
}

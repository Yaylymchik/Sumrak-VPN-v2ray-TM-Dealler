using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;

namespace SumraX.App.ViewModels;

public partial class AiViewModel : LocalizableViewModelBase
{
    private readonly IAiAssistant _ai;
    private readonly IVpnEngine _vpn;

    public ObservableCollection<AiMessage> Messages { get; } = [];

    [ObservableProperty] private string _input = string.Empty;
    [ObservableProperty] private bool _isThinking;
    [ObservableProperty] private string _pageTitle = "";
    [ObservableProperty] private string _clearLabel = "";
    [ObservableProperty] private string _sendLabel = "";
    [ObservableProperty] private string _inputHint = "";
    [ObservableProperty] private string _pageSubtitle = "";
    [ObservableProperty] private string _thinkingLabel = "";

    public AiViewModel(IAiAssistant ai, IVpnEngine vpn, ILocalizationService loc) : base(loc)
    {
        _ai = ai;
        _vpn = vpn;
        CompleteInitialization();
    }

    protected override void OnLanguageChanged()
    {
        PageTitle = L("NavAiCore");
        PageSubtitle = L("AiCoreSubtitle");
        ClearLabel = L("Clear");
        SendLabel = L("Send");
        InputHint = L("AskAi");
        ThinkingLabel = L("AiThinking");
        if (Messages.Count <= 1)
        {
            Messages.Clear();
            Messages.Add(new AiMessage { IsUser = false, Content = L("AiWelcome") });
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(Input) || IsThinking)
        {
            return;
        }

        var question = Input.Trim();
        Input = string.Empty;
        IsThinking = true;
        try
        {
            var status = _vpn.CurrentStatus;
            var answer = await _ai.AskAsync(question, status);
            Messages.Clear();
            foreach (var msg in _ai.History)
            {
                Messages.Add(msg);
            }
            _ = answer;
        }
        finally
        {
            IsThinking = false;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        _ai.ClearHistory();
        Messages.Clear();
        Messages.Add(new AiMessage { IsUser = false, Content = L("ChatCleared") });
    }
}

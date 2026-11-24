using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SumraX.App.ViewModels;

public sealed class SettingsHubRowViewModel
{
    public string Title { get; set; } = "";
    public SettingsSection Section { get; set; } = SettingsSection.Root;
    public string? Action { get; set; }
    public bool IsDanger { get; set; }
}

public sealed class SettingsHubGroupViewModel
{
    public string Title { get; set; } = "";
    public ObservableCollection<SettingsHubRowViewModel> Rows { get; } = [];
}
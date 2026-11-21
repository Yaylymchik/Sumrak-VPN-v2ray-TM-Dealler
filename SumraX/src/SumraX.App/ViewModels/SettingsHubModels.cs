using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SumraX.App.ViewModels;

public sealed class SettingsHubRowViewModel
{
    public required string Title { get; init; }
    public SettingsSection Section { get; init; } = SettingsSection.Root;
    public string? Action { get; init; }
    public bool IsDanger { get; init; }
}

public sealed class SettingsHubGroupViewModel
{
    public required string Title { get; init; }
    public ObservableCollection<SettingsHubRowViewModel> Rows { get; } = [];
}
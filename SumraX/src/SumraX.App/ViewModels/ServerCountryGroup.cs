using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SumraX.Core.Models;

namespace SumraX.App.ViewModels;

public partial class ServerCountryGroup : ObservableObject
{
    public string CountryName { get; init; } = "";
    public string CountryCode { get; init; } = "";
    public string FlagEmoji { get; init; } = "🌍";
    public ObservableCollection<ServerProfile> Servers { get; init; } = [];

    public int ServerCount => Servers.Count;

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isActive;
}

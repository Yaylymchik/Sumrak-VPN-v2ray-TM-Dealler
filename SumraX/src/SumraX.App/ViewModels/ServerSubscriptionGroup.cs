using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SumraX.Core.Models;

namespace SumraX.App.ViewModels;

/// <summary>Home screen subscription group.</summary>
public partial class ServerSubscriptionGroup : ObservableObject
{
    public string GroupKey { get; init; } = "";
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public ObservableCollection<ServerProfile> Servers { get; init; } = [];

    [ObservableProperty] private bool _isExpanded = true;
}
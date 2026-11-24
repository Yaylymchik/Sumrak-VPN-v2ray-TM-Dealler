using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SumraX.Core.Models;
using SumraX.Infrastructure.Vpn;

namespace SumraX.App.ViewModels;

public partial class SettingsViewModel
{
  private string _editSubscriptionUrl = "";

    [ObservableProperty] private string _subEditRemarks = "";
    [ObservableProperty] private string _subEditUrlDisplay = "";
    [ObservableProperty] private string _subEditUserAgent = "";
    [ObservableProperty] private string _subEditFilter = "";
    [ObservableProperty] private bool _subEditEnabled = true;
    [ObservableProperty] private bool _subEditAutoUpdate = true;
    [ObservableProperty] private int _subEditUpdateIntervalHours = 1;
    [ObservableProperty] private bool _subEditUpdateNotification;
    [ObservableProperty] private int _subEditRequestTimeoutSeconds = 15;
    [ObservableProperty] private bool _subEditUpdateOnOpenUseGlobal = true;
    [ObservableProperty] private bool _subEditUpdateOnOpen;
    [ObservableProperty] private bool _subEditConnectOnOpenUseGlobal = true;
    [ObservableProperty] private bool _subEditConnectOnOpen;
    [ObservableProperty] private bool _subEditSkipFilter;
    [ObservableProperty] private bool _subEditCollapsedByDefault;
    [ObservableProperty] private bool _subEditSendHwidUseGlobal = true;
    [ObservableProperty] private bool _subEditSendHwid = true;
    [ObservableProperty] private bool _subEditUseAlternativeHwid;
    [ObservableProperty] private string _subEditAlternativeHwidValue = "";
    [ObservableProperty] private string _subEditSelectedServerSort = "";
    [ObservableProperty] private bool _subEditAllowInsecureUrl;
    [ObservableProperty] private string _subEditPrevProfile = "";
    [ObservableProperty] private string _subEditNextProfile = "";
    [ObservableProperty] private string _subEditSelectedPingType = "";
    [ObservableProperty] private bool _subEditPingOnOpenUseGlobal = true;
    [ObservableProperty] private bool _subEditPingOnOpen;
    [ObservableProperty] private string _subEditSelectedAutoConnect = "";
    [ObservableProperty] private bool _subEditFragmentEnabled;
    [ObservableProperty] private bool _subEditFragmentUseGlobal = true;
    [ObservableProperty] private string _subEditFragmentPackets = "";
    [ObservableProperty] private string _subEditFragmentLength = "";
    [ObservableProperty] private string _subEditFragmentInterval = "";
    [ObservableProperty] private bool _subEditMuxEnabled;
    [ObservableProperty] private bool _subEditMuxUseGlobal = true;
    [ObservableProperty] private string _subEditMuxConcurrency = "";
    [ObservableProperty] private bool _subEditNoiseEnabled;
    [ObservableProperty] private bool _subEditNoiseUseGlobal = true;
    [ObservableProperty] private string _subEditNoiseRand = "";
    [ObservableProperty] private string _subEditNoiseDelay = "";

    public ObservableCollection<string> SubEditPingTypeChoices { get; } = [];
    public ObservableCollection<string> SubEditAutoConnectChoices { get; } = [];
    public ObservableCollection<string> SubEditServerSortChoices { get; } = [];

    public string SectionSubscriptionEditLabel { get; private set; } = "";
    public string SubEditRemarksLabel { get; private set; } = "";
    public string SubEditUrlLabel { get; private set; } = "";
    public string SubEditUserAgentLabel { get; private set; } = "";
    public string SubEditFilterLabel { get; private set; } = "";
    public string SubEditEnabledLabel { get; private set; } = "";
    public string SubEditAutoUpdateLabel { get; private set; } = "";
    public string SubEditUpdateIntervalLabel { get; private set; } = "";
    public string SubEditSectionUpdateLabel { get; private set; } = "";
    public string SubEditSectionOnOpenLabel { get; private set; } = "";
    public string SubEditSectionOtherLabel { get; private set; } = "";
    public string SubEditSectionDataLabel { get; private set; } = "";
    public string SubEditUpdateNotificationLabel { get; private set; } = "";
    public string SubEditRequestTimeoutLabel { get; private set; } = "";
    public string SubEditUpdateOnOpenLabel { get; private set; } = "";
    public string SubEditConnectOnOpenLabel { get; private set; } = "";
    public string SubEditSkipFilterLabel { get; private set; } = "";
    public string SubEditCollapsedLabel { get; private set; } = "";
    public string SubEditSendHwidLabel { get; private set; } = "";
    public string SubEditAlternativeHwidLabel { get; private set; } = "";
    public string SubEditAlternativeHwidValueLabel { get; private set; } = "";
    public string SubEditServerSortLabel { get; private set; } = "";
    public string SubEditUserAgentHint { get; private set; } = "";
    public string SubEditHwidHint { get; private set; } = "";
    public string SubEditAllowInsecureLabel { get; private set; } = "";
    public string SubEditPingTypeLabel { get; private set; } = "";
    public string SubEditAutoConnectLabel { get; private set; } = "";
    public string SubEditPrevProfileLabel { get; private set; } = "";
    public string SubEditNextProfileLabel { get; private set; } = "";
    public string SubEditGroupTunnelLabel { get; private set; } = "";
    public string SubEditUseGlobalLabel { get; private set; } = "";
    public string SubEditSaveLabel { get; private set; } = "";
    public string SubEditUpdateNowLabel { get; private set; } = "";
    public string SectionSubscriptionPingEditLabel { get; private set; } = "";
    public string SubEditPingOnOpenLabel { get; private set; } = "";
    public string SubEditOpenGlobalPingLabel { get; private set; } = "";
    public string SubEditGlobalPingHint { get; private set; } = "";

    private static readonly string[] PingTypeValues = ["", "tcp", "icmp", "proxy", "proxy-head"];
    private static readonly string[] AutoConnectValues = ["", "lowestdelay", "lastused", "random"];
    private static readonly string[] ServerSortValues = ["", "none", "ping", "name"];

    private void ApplySubscriptionEditLabels()
    {
        SectionSubscriptionEditLabel = L("SubEditTitle");
        SubEditRemarksLabel = L("SubEditRemarks");
        SubEditUrlLabel = L("SubEditUrl");
        SubEditUserAgentLabel = L("SubEditUserAgent");
        SubEditFilterLabel = L("SubEditFilter");
        SubEditEnabledLabel = L("SubEditEnabled");
        SubEditAutoUpdateLabel = L("SubEditAutoUpdate");
        SubEditUpdateIntervalLabel = L("SubEditUpdateIntervalHours");
        SubEditSectionUpdateLabel = L("SubEditSectionUpdate");
        SubEditSectionOnOpenLabel = L("SubEditSectionOnOpen");
        SubEditSectionOtherLabel = L("SubEditSectionOther");
        SubEditSectionDataLabel = L("SubEditSectionData");
        SubEditUpdateNotificationLabel = L("SubEditUpdateNotification");
        SubEditRequestTimeoutLabel = L("SubEditRequestTimeout");
        SubEditUpdateOnOpenLabel = L("SubEditUpdateOnOpen");
        SubEditConnectOnOpenLabel = L("SubEditConnectOnOpen");
        SubEditSkipFilterLabel = L("SubEditSkipFilter");
        SubEditCollapsedLabel = L("SubEditCollapsed");
        SubEditSendHwidLabel = L("SendHwidHeader");
        SubEditAlternativeHwidLabel = L("SubEditAlternativeHwid");
        SubEditAlternativeHwidValueLabel = L("CustomHwidValue");
        SubEditServerSortLabel = L("SubEditServerSort");
        SubEditUserAgentHint = L("SubEditUserAgentHint");
        SubEditHwidHint = L("SubEditHwidHint");
        SubEditAllowInsecureLabel = L("SubEditAllowInsecure");
        SubEditPingTypeLabel = L("PingType");
        SubEditAutoConnectLabel = L("SubEditAutoConnect");
        SubEditPrevProfileLabel = L("SubEditPrevProfile");
        SubEditNextProfileLabel = L("SubEditNextProfile");
        SubEditGroupTunnelLabel = L("SubEditGroupTunnel");
        SubEditUseGlobalLabel = L("SubEditInheritGlobal");
        SubEditSaveLabel = L("SubEditSave");
        SubEditUpdateNowLabel = L("GroupMenuUpdate");
        SectionSubscriptionPingEditLabel = L("SubEditPingTitle");
        SubEditPingOnOpenLabel = L("SubEditPingOnOpen");
        SubEditOpenGlobalPingLabel = L("SubEditOpenGlobalPing");
        SubEditGlobalPingHint = L("SubEditGlobalPingHint");

        RebuildSubEditChoiceLists();
        NotifySubscriptionEditLabelProperties();
    }

    private void NotifySubscriptionEditLabelProperties()
    {
        foreach (var name in new[]
        {
            nameof(SectionSubscriptionEditLabel), nameof(SubEditRemarksLabel), nameof(SubEditUrlLabel),
            nameof(SubEditUserAgentLabel), nameof(SubEditFilterLabel), nameof(SubEditEnabledLabel),
            nameof(SubEditAutoUpdateLabel), nameof(SubEditUpdateIntervalLabel), nameof(SubEditAllowInsecureLabel),
            nameof(SubEditPingTypeLabel), nameof(SubEditAutoConnectLabel), nameof(SubEditPrevProfileLabel),
            nameof(SubEditNextProfileLabel), nameof(SubEditGroupTunnelLabel), nameof(SubEditUseGlobalLabel),
            nameof(SubEditSaveLabel), nameof(SubEditUpdateNowLabel),
            nameof(SectionSubscriptionPingEditLabel), nameof(SubEditPingOnOpenLabel),
            nameof(SubEditOpenGlobalPingLabel), nameof(SubEditGlobalPingHint),
            nameof(SubEditSectionUpdateLabel), nameof(SubEditSectionOnOpenLabel),
            nameof(SubEditSectionOtherLabel), nameof(SubEditSectionDataLabel),
            nameof(SubEditUpdateNotificationLabel), nameof(SubEditRequestTimeoutLabel),
            nameof(SubEditUpdateOnOpenLabel), nameof(SubEditConnectOnOpenLabel),
            nameof(SubEditSkipFilterLabel), nameof(SubEditCollapsedLabel), nameof(SubEditSendHwidLabel),
            nameof(SubEditAlternativeHwidLabel), nameof(SubEditAlternativeHwidValueLabel),
            nameof(SubEditServerSortLabel), nameof(SubEditUserAgentHint), nameof(SubEditHwidHint),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private void RebuildSubEditChoiceLists()
    {
        SubEditPingTypeChoices.Clear();
        SubEditPingTypeChoices.Add(L("SubEditInheritGlobal"));
        SubEditPingTypeChoices.Add(L("PingTypeTcp"));
        SubEditPingTypeChoices.Add(L("PingTypeIcmp"));
        SubEditPingTypeChoices.Add(L("PingTypeProxy"));
        SubEditPingTypeChoices.Add(L("PingTypeProxyHead"));

        SubEditAutoConnectChoices.Clear();
        SubEditAutoConnectChoices.Add(L("SubEditInheritGlobal"));
        SubEditAutoConnectChoices.Add(L("SubEditAutoConnectLowest"));
        SubEditAutoConnectChoices.Add(L("SubEditAutoConnectLast"));
        SubEditAutoConnectChoices.Add(L("SubEditAutoConnectRandom"));

        SubEditServerSortChoices.Clear();
        SubEditServerSortChoices.Add(L("SubEditInheritGlobal"));
        SubEditServerSortChoices.Add(L("SubEditSortNone"));
        SubEditServerSortChoices.Add(L("SubEditSortPing"));
        SubEditServerSortChoices.Add(L("SubEditSortName"));
    }

    public async void NavigateToSubscriptionEdit(string? subscriptionUrl)
    {
        subscriptionUrl = SubscriptionUrlNormalizer.Normalize(subscriptionUrl ?? "");
        if (string.IsNullOrWhiteSpace(subscriptionUrl))
        {
            return;
        }

        _editSubscriptionUrl = subscriptionUrl;
        var settings = await _settings.GetAsync();
        var item = SubscriptionItemSettingsHelper.GetOrCreate(settings, subscriptionUrl);
        await _settings.SaveAsync(settings);
        ApplySubscriptionItemFields(subscriptionUrl, item);

        CurrentSection = SettingsSection.SubscriptionEdit;
        var title = string.IsNullOrWhiteSpace(item.Remarks) ? SectionSubscriptionEditLabel : item.Remarks.Trim();
        SectionTitle = title;
        OnPropertyChanged(nameof(SectionTitle));
    }

    public async void NavigateToSubscriptionPingEdit(string? subscriptionUrl)
    {
        subscriptionUrl = SubscriptionUrlNormalizer.Normalize(subscriptionUrl ?? "");
        if (string.IsNullOrWhiteSpace(subscriptionUrl))
        {
            return;
        }

        _editSubscriptionUrl = subscriptionUrl;
        var settings = await _settings.GetAsync();
        var item = SubscriptionItemSettingsHelper.GetOrCreate(settings, subscriptionUrl);
        await _settings.SaveAsync(settings);
        SubEditRemarks = item.Remarks;
        SubEditSelectedPingType = PingTypeValueToDisplay(item.PingType);
        SubEditPingOnOpenUseGlobal = item.PingOnOpen == null;
        SubEditPingOnOpen = item.PingOnOpen == true;

        CurrentSection = SettingsSection.SubscriptionPingEdit;
        UpdateSectionTitle();
    }

    private void ApplySubscriptionItemFields(string subscriptionUrl, SubscriptionItemSettings item)
    {
        SubEditUrlDisplay = subscriptionUrl;
        SubEditRemarks = item.Remarks;
        SubEditUserAgent = item.UserAgent ?? "";
        SubEditFilter = item.Filter ?? "";
        SubEditEnabled = item.Enabled;
        SubEditAutoUpdate = item.AutoUpdate;
        SubEditUpdateIntervalHours = item.UpdateIntervalHours > 0
            ? item.UpdateIntervalHours
            : Math.Max(1, (item.UpdateIntervalMinutes + 59) / 60);
        SubEditUpdateNotification = item.UpdateNotification == true;
        SubEditRequestTimeoutSeconds = item.RequestTimeoutSeconds is > 0 and var t ? t : 15;
        SubEditUpdateOnOpenUseGlobal = item.UpdateOnOpen == null;
        SubEditUpdateOnOpen = item.UpdateOnOpen == true;
        SubEditConnectOnOpenUseGlobal = item.ConnectOnOpen == null;
        SubEditConnectOnOpen = item.ConnectOnOpen == true;
        SubEditSkipFilter = item.SkipFilter == true;
        SubEditCollapsedByDefault = item.CollapsedByDefault == true;
        SubEditSendHwidUseGlobal = item.SendHwidHeader == null;
        SubEditSendHwid = item.SendHwidHeader != false;
        SubEditUseAlternativeHwid = item.UseAlternativeHwid == true;
        SubEditAlternativeHwidValue = item.AlternativeHwidValue ?? "";
        SubEditSelectedServerSort = ServerSortValueToDisplay(item.ServerSortMode);
        SubEditAllowInsecureUrl = item.AllowInsecureUrl;
        SubEditPrevProfile = item.PrevProfile ?? "";
        SubEditNextProfile = item.NextProfile ?? "";
        SubEditSelectedPingType = PingTypeValueToDisplay(item.PingType);
        SubEditPingOnOpenUseGlobal = item.PingOnOpen == null;
        SubEditPingOnOpen = item.PingOnOpen == true;
        SubEditSelectedAutoConnect = AutoConnectValueToDisplay(item.AutoConnectType);

        SubEditFragmentUseGlobal = item.FragmentEnabled == null;
        SubEditFragmentEnabled = item.FragmentEnabled == true;
        SubEditFragmentPackets = item.FragmentPackets ?? "";
        SubEditFragmentLength = item.FragmentLength ?? "";
        SubEditFragmentInterval = item.FragmentInterval ?? "";

        SubEditMuxUseGlobal = item.MuxEnabled == null;
        SubEditMuxEnabled = item.MuxEnabled == true;
        SubEditMuxConcurrency = item.MuxConcurrency ?? "";

        SubEditNoiseUseGlobal = item.NoiseEnabled == null;
        SubEditNoiseEnabled = item.NoiseEnabled == true;
        SubEditNoiseRand = item.NoiseRand ?? "";
        SubEditNoiseDelay = item.NoiseDelay ?? "";
    }

    [RelayCommand]
    private void OpenGlobalPingSettings() => CurrentSection = SettingsSection.Ping;

    private string PingTypeValueToDisplay(string? value)
    {
        var idx = Array.IndexOf(PingTypeValues, value ?? "");
        if (idx < 0)
        {
            idx = 0;
        }

        return SubEditPingTypeChoices.Count > idx ? SubEditPingTypeChoices[idx] : SubEditPingTypeChoices[0];
    }

    private string? PingTypeDisplayToValue(string display)
    {
        var idx = SubEditPingTypeChoices.IndexOf(display);
        if (idx < 0)
        {
            idx = 0;
        }

        var raw = PingTypeValues[Math.Clamp(idx, 0, PingTypeValues.Length - 1)];
        return string.IsNullOrEmpty(raw) ? null : raw;
    }

    private string AutoConnectValueToDisplay(string? value)
    {
        var idx = Array.IndexOf(AutoConnectValues, value ?? "");
        if (idx < 0)
        {
            idx = 0;
        }

        return SubEditAutoConnectChoices.Count > idx ? SubEditAutoConnectChoices[idx] : SubEditAutoConnectChoices[0];
    }

    private string ServerSortValueToDisplay(string? value)
    {
        var idx = Array.IndexOf(ServerSortValues, value ?? "");
        if (idx < 0)
        {
            idx = 0;
        }

        return SubEditServerSortChoices.Count > idx ? SubEditServerSortChoices[idx] : SubEditServerSortChoices[0];
    }

    private string? ServerSortDisplayToValue(string display)
    {
        var idx = SubEditServerSortChoices.IndexOf(display);
        if (idx < 0)
        {
            idx = 0;
        }

        var raw = ServerSortValues[Math.Clamp(idx, 0, ServerSortValues.Length - 1)];
        return string.IsNullOrEmpty(raw) ? null : raw;
    }

    private string? AutoConnectDisplayToValue(string display)
    {
        var idx = SubEditAutoConnectChoices.IndexOf(display);
        if (idx < 0)
        {
            idx = 0;
        }

        var raw = AutoConnectValues[Math.Clamp(idx, 0, AutoConnectValues.Length - 1)];
        return string.IsNullOrEmpty(raw) ? null : raw;
    }

    [RelayCommand]
    private async Task SaveSubscriptionEditAsync()
    {
        if (string.IsNullOrWhiteSpace(_editSubscriptionUrl))
        {
            return;
        }

        try
        {
            var settings = await _settings.GetAsync();
            var item = SubscriptionItemSettingsHelper.GetOrCreate(settings, _editSubscriptionUrl);
            item.Remarks = SubEditRemarks.Trim();
            item.UserAgent = string.IsNullOrWhiteSpace(SubEditUserAgent) ? null : SubEditUserAgent.Trim();
            item.Filter = string.IsNullOrWhiteSpace(SubEditFilter) ? null : SubEditFilter.Trim();
            item.Enabled = SubEditEnabled;
            item.AutoUpdate = SubEditAutoUpdate;
            item.UpdateIntervalHours = Math.Clamp(SubEditUpdateIntervalHours, 1, 168);
            item.UpdateIntervalMinutes = item.UpdateIntervalHours * 60;
            item.UpdateNotification = SubEditUpdateNotification ? true : null;
            item.RequestTimeoutSeconds = Math.Clamp(SubEditRequestTimeoutSeconds, 5, 120);
            item.UpdateOnOpen = SubEditUpdateOnOpenUseGlobal ? null : SubEditUpdateOnOpen;
            item.ConnectOnOpen = SubEditConnectOnOpenUseGlobal ? null : SubEditConnectOnOpen;
            item.SkipFilter = SubEditSkipFilter ? true : null;
            item.CollapsedByDefault = SubEditCollapsedByDefault ? true : null;
            item.SendHwidHeader = SubEditSendHwidUseGlobal ? null : SubEditSendHwid;
            item.UseAlternativeHwid = SubEditUseAlternativeHwid ? true : null;
            item.AlternativeHwidValue = string.IsNullOrWhiteSpace(SubEditAlternativeHwidValue)
                ? null
                : SubEditAlternativeHwidValue.Trim();
            item.ServerSortMode = ServerSortDisplayToValue(SubEditSelectedServerSort);
            item.AllowInsecureUrl = SubEditAllowInsecureUrl;
            item.PrevProfile = string.IsNullOrWhiteSpace(SubEditPrevProfile) ? null : SubEditPrevProfile.Trim();
            item.NextProfile = string.IsNullOrWhiteSpace(SubEditNextProfile) ? null : SubEditNextProfile.Trim();
            item.PingType = PingTypeDisplayToValue(SubEditSelectedPingType);
            item.PingOnOpen = SubEditPingOnOpenUseGlobal ? null : SubEditPingOnOpen;
            item.AutoConnectType = AutoConnectDisplayToValue(SubEditSelectedAutoConnect);

            item.FragmentEnabled = SubEditFragmentUseGlobal ? null : SubEditFragmentEnabled;
            item.FragmentPackets = string.IsNullOrWhiteSpace(SubEditFragmentPackets) ? null : SubEditFragmentPackets.Trim();
            item.FragmentLength = string.IsNullOrWhiteSpace(SubEditFragmentLength) ? null : SubEditFragmentLength.Trim();
            item.FragmentInterval = string.IsNullOrWhiteSpace(SubEditFragmentInterval) ? null : SubEditFragmentInterval.Trim();

            item.MuxEnabled = SubEditMuxUseGlobal ? null : SubEditMuxEnabled;
            item.MuxConcurrency = string.IsNullOrWhiteSpace(SubEditMuxConcurrency) ? null : SubEditMuxConcurrency.Trim();

            item.NoiseEnabled = SubEditNoiseUseGlobal ? null : SubEditNoiseEnabled;
            item.NoiseRand = string.IsNullOrWhiteSpace(SubEditNoiseRand) ? null : SubEditNoiseRand.Trim();
            item.NoiseDelay = string.IsNullOrWhiteSpace(SubEditNoiseDelay) ? null : SubEditNoiseDelay.Trim();

            SubscriptionItemSettingsHelper.SyncRemark(settings, _editSubscriptionUrl, item.Remarks);
            await _settings.SaveAsync(settings);
            UpdateMessage = L("SettingsSaved");
            await App.HostApp.Services.GetRequiredService<ServersViewModel>().LoadCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            UpdateMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RefreshSubscriptionEditAsync()
    {
        if (string.IsNullOrWhiteSpace(_editSubscriptionUrl))
        {
            return;
        }

        await SaveSubscriptionEditAsync();
        UpdateMessage = L("LoadingSubscription");
        var refresh = await _subRefresh.RefreshUrlAsync(_editSubscriptionUrl);
        UpdateMessage = refresh.Success
            ? L("GroupMenuUpdate")
            : refresh.Errors.FirstOrDefault() ?? L("SubscriptionImportFailed");
        await App.HostApp.Services.GetRequiredService<ServersViewModel>().LoadCommand.ExecuteAsync(null);
    }

}
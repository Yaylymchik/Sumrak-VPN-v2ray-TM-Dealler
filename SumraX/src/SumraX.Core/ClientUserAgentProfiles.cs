using SumraX.Core.Enums;

namespace SumraX.Core;

public static class ClientUserAgentProfiles
{
    public static readonly ClientUserAgentPreset[] AllPresets =
    [
        ClientUserAgentPreset.ChromeWindows,
        ClientUserAgentPreset.ChromeAndroid,
        ClientUserAgentPreset.SafariMacOS,
        ClientUserAgentPreset.SafariIOS,
        ClientUserAgentPreset.FirefoxWindows,
        ClientUserAgentPreset.EdgeWindows
    ];

    public static string GetHttpUserAgent(ClientUserAgentPreset preset) => preset switch
    {
        ClientUserAgentPreset.ChromeAndroid =>
            "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36",
        ClientUserAgentPreset.SafariMacOS =>
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15",
        ClientUserAgentPreset.SafariIOS =>
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1",
        ClientUserAgentPreset.FirefoxWindows =>
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:131.0) Gecko/20100101 Firefox/131.0",
        ClientUserAgentPreset.EdgeWindows =>
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0",
        _ =>
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
    };

    public static string GetTlsFingerprint(ClientUserAgentPreset preset) => preset switch
    {
        ClientUserAgentPreset.FirefoxWindows => "firefox",
        ClientUserAgentPreset.SafariMacOS => "safari",
        ClientUserAgentPreset.SafariIOS => "ios",
        ClientUserAgentPreset.ChromeAndroid => "android",
        _ => "chrome"
    };
}

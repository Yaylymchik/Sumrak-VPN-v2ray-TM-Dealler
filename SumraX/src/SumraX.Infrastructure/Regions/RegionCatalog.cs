namespace SumraX.Infrastructure.Regions;

public sealed record RegionInfo(string Code, string DisplayName, string WindowsTimeZoneId, string UtcOffsetLabel);

public static class RegionCatalog
{
    public static IReadOnlyList<RegionInfo> All { get; } =
    [
        new("TM", "Turkmenistan", "West Asia Standard Time", "UTC+5"),
        new("RU", "Russia", "Russian Standard Time", "UTC+3"),
        new("KZ", "Kazakhstan", "Central Asia Standard Time", "UTC+6"),
        new("UZ", "Uzbekistan", "West Asia Standard Time", "UTC+5"),
        new("TR", "Turkey", "Turkey Standard Time", "UTC+3"),
        new("DE", "Germany", "W. Europe Standard Time", "UTC+1"),
        new("US", "United States", "Eastern Standard Time", "UTC-5"),
        new("AE", "UAE", "Arabian Standard Time", "UTC+4")
    ];

    public static RegionInfo? Find(string code) =>
        All.FirstOrDefault(r => r.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
}

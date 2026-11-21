using SumraX.Infrastructure.Vpn;
using SumraX.Core.Models;
using SumraX.Core.Enums;

var link = "vmess://eyJhZGQiOiJleGFtcGxlLmNvbSIsImFpZCI6IjAiLCJob3N0IjoiIiwiaWQiOiIxMjM0NTY3OC0xMjM0LTEyMzQtMTIzNC0xMjM0NTY3ODlhYmIiLCJuZXQiOiJ3cyIsInBhdGgiOiIvd3MiLCJwb3J0IjoiNDQzIiwicHMiOiJ0ZXN0Iiwic2N5IjoiYXV0byIsInNuaSI6IiIsInRscyI6InRscyIsInR5cGUiOiJub25lIiwidiI6IjIifQ==";
var svc = new ConfigImportService();
var p = await svc.ImportFromLinkAsync(link);
Console.WriteLine(p != null ? $"OK: {p.Name} {p.Address}:{p.Port} {p.Protocol}" : "FAIL");

var settings = new AppSettings();
var cfg = XrayConfigBuilder.Build(p!, settings);
Console.WriteLine(cfg.Contains("\"protocol\": \"vmess\"") ? "Config OK" : "Config FAIL");
Console.WriteLine(cfg.Contains("geosite:") ? "GEO BAD" : "GEO OK");

namespace SumraX.Core.Enums;

public enum VpnProtocol
{
    VMess,
    VLESS,
    Trojan,
    Shadowsocks,
    Hysteria2,
    Hysteria,
    Socks,
    WireGuard,
    Tuic,
    ShadowsocksR
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}

public enum AppTheme
{
    Cyber,
    Midnight,
    Emerald,
    Ember,
    Arctic
}

public enum AppLanguage
{
    English,
    Russian,
    Turkmen
}

public enum SubscriptionPlan
{
    Free,
    Premium,
    Enterprise
}

public enum ImportSource
{
    QrCode,
    Link,
    File,
    Manual
}

public enum TunStackMode
{
    System,
    GVisor,
    Mixed
}

public enum TunProvider
{
    SumraXTun,
    SingBox,
    Tun2Proxy
}

/// <summary>Matches v2rayNG pref_ping_type: tcp / icmp / proxy / proxy-head.</summary>
public enum PingType
{
    Tcp,
    Icmp,
    Proxy,
    ProxyHead
}

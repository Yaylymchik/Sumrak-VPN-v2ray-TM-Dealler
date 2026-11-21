using System.Security.Cryptography;
using System.Text;
using SumraX.Core.Interfaces;

namespace SumraX.Infrastructure.Security;

/// <summary>
/// Uses Windows DPAPI for local encryption of sensitive data.
/// </summary>
public sealed class DpapiSecureStorage : ISecureStorage
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SumraX.VPN.v1");

    public string Encrypt(string plainText)
    {
        var data = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Decrypt(string cipherText)
    {
        var protectedBytes = Convert.FromBase64String(cipherText);
        var data = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(data);
    }

    public async Task SaveSecureAsync(string key, string value)
    {
        AppPaths.EnsureDirectories();
        var path = Path.Combine(AppPaths.DataRoot, $"{key}.dat");
        await File.WriteAllTextAsync(path, Encrypt(value));
    }

    public async Task<string?> LoadSecureAsync(string key)
    {
        var path = Path.Combine(AppPaths.DataRoot, $"{key}.dat");
        if (!File.Exists(path))
        {
            return null;
        }
        var encrypted = await File.ReadAllTextAsync(path);
        return Decrypt(encrypted);
    }
}

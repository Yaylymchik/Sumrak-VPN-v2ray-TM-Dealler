using SumraX.Infrastructure.Vpn;
using Xunit;

namespace SumraX.Infrastructure.Tests;

public sealed class Crypt5LinkDecryptorTests
{
    [Fact]
    public void TryDecrypt_empty_returns_false()
    {
        Assert.False(Crypt5LinkDecryptor.TryDecryptPayload("", out _));
    }

    [Fact]
    public void Reorder_pipeline_does_not_throw_on_garbage()
    {
        Assert.False(Crypt5LinkDecryptor.TryDecryptPayload("not-valid-base64!!!", out _));
    }
}
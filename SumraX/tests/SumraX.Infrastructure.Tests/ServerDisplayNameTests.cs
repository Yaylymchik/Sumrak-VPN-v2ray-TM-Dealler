using SumraX.Core.Util;
using Xunit;

namespace SumraX.Infrastructure.Tests;

public sealed class ServerDisplayNameTests
{
    [Theory]
    [InlineData("NL Netherlands?serverDescription=RXVyb3BI", "NL Netherlands")]
    [InlineData("TR Google?serverDescription=VMO8cmtpeWU=", "TR Google")]
    public void UnpackRemarks_strips_server_description_query(string raw, string expected)
    {
        var unpacked = ServerDisplayName.UnpackRemarks(raw);
        Assert.Equal(expected, unpacked);
    }

    [Theory]
    [InlineData("NL Netherlands?serverDescription=RXVyb3BI", "NL Netherlands")]
    [InlineData("TR Google?serverDescription=VMO8cmtpeWU=", "TR Google")]
    public void FromProfile_hides_server_description_query_tail(string raw, string expected)
    {
        var display = ServerDisplayName.FromProfile(raw, "1.2.3.4");
        Assert.Equal(expected, display);
    }

    [Fact]
    public void FromProfile_pipe_remarks_never_show_raw_query()
    {
        var display = ServerDisplayName.FromProfile("CH [ CH | Alpine 📶 4G ]", "example.com");
        Assert.DoesNotContain("serverDescription", display, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("?", display);
    }
}
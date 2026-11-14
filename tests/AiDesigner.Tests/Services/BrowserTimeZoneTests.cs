using AiDesigner.Services.Results;

namespace AiDesigner.Tests.Services;

public class BrowserTimeZoneTests
{
    [Fact]
    public void Resolve_KnownId_ReturnsThatZone()
    {
        var zone = BrowserTimeZone.Resolve("UTC");

        Assert.Equal(TimeSpan.Zero, zone.BaseUtcOffset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not/AZone")]
    public void Resolve_MissingOrUnknownId_FallsBackToLocal(string? id)
    {
        var zone = BrowserTimeZone.Resolve(id);

        Assert.Equal(TimeZoneInfo.Local, zone);
    }
}

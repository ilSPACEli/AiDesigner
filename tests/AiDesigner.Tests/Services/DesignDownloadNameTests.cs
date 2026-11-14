using AiDesigner.Services.Results;

namespace AiDesigner.Tests.Services;

public class DesignDownloadNameTests
{
    [Fact]
    public void AC21_For_BuildsPngNameFromMoment()
    {
        var name = DesignDownloadName.For(new DateTime(2026, 11, 14, 18, 5, 9));

        Assert.Equal("design-20261114-180509.png", name);
    }
}

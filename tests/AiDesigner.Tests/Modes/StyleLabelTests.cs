using AiDesigner.Models;
using AiDesigner.Services.Modes;

namespace AiDesigner.Tests.Modes;

// Рядок про стиль — спільний для сторінки «Дизайн» і картки галереї.
public class StyleLabelTests
{
    [Fact]
    public void AiMode_KnownStyle_Determined()
    {
        var label = StyleLabel.For(AnalysisMode.LOCAL_AI, RoomStyle.SCANDINAVIAN);

        Assert.Equal(new StyleLabel("Стиль: Скандинавський", true), label);
    }

    [Theory]
    [InlineData(AnalysisMode.LOCAL_AI)]
    [InlineData(AnalysisMode.CLOUD_AI)]
    public void AiMode_NoStyle_NotDetermined(AnalysisMode mode)
    {
        var label = StyleLabel.For(mode, null);

        Assert.Equal(new StyleLabel(DisplayNames.StyleNotDetermined, false), label);
    }

    [Fact]
    public void BasicMode_NoLabel()
    {
        Assert.Null(StyleLabel.For(AnalysisMode.BASIC, null));
    }

    [Fact]
    public void FromApi_AiModeAndStyle_Determined()
    {
        Assert.Equal(new StyleLabel("Стиль: Бохо", true), StyleLabel.For("CLOUD_AI", "BOHO"));
    }

    [Fact]
    public void FromApi_AiModeWithoutStyle_NotDetermined()
    {
        Assert.Equal(new StyleLabel(DisplayNames.StyleNotDetermined, false), StyleLabel.For("LOCAL_AI", null));
    }

    [Theory]
    [InlineData("loft")]       // регістр має збігатися з контрактом API
    [InlineData("1")]          // число не приймаємо
    [InlineData("GOTHIC")]     // поза переліком
    public void FromApi_UnknownStyle_NotDetermined(string style)
    {
        var label = StyleLabel.For("CLOUD_AI", style);

        Assert.NotNull(label);
        Assert.False(label!.IsDetermined);
    }

    [Theory]
    [InlineData(null)]         // робота збережена до S2 — аналізу немає
    [InlineData("BASIC")]
    [InlineData("UNKNOWN")]
    [InlineData("2")]
    public void FromApi_NoAiMode_NoLabel(string? mode)
    {
        Assert.Null(StyleLabel.For(mode, "LOFT"));
    }
}

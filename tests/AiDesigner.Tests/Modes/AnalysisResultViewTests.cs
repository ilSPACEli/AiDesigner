using AiDesigner.Models;
using AiDesigner.Services;
using AiDesigner.Services.Modes;

namespace AiDesigner.Tests.Modes;

// Блок «Аналіз» на сторінці «Дизайн»: освітленість, режим, стиль, повідомлення про відкат (FR-19, AC-30).
public class AnalysisResultViewTests
{
    private static AnalysisResult Result(AnalysisMode mode, RoomStyle? style = null, LightingLevel lighting = LightingLevel.MODERATE) => new()
    {
        Colors = new[] { new DominantColor { Hex = "#AABBCC", Hue = 210, Share = 100 } },
        Lighting = lighting,
        AnalyzedAt = new DateTime(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc),
        Mode = mode,
        Style = style,
        Confidence = style is null ? null : 0.8m
    };

    [Fact]
    public void AC27_AiMode_WithStyle_ShowsStyleName()
    {
        var view = AnalysisResultView.From(Result(AnalysisMode.CLOUD_AI, RoomStyle.LOFT));

        Assert.NotNull(view.Style);
        Assert.Equal("Стиль: Лофт", view.Style!.Text);
        Assert.True(view.Style.IsDetermined);
        Assert.Equal("Режим: Хмарний ШІ", view.ModeText);
        Assert.Null(view.Notice);
    }

    [Fact]
    public void AC28_AiMode_WithoutStyle_ShowsNotDetermined_Neutral()
    {
        var view = AnalysisResultView.From(Result(AnalysisMode.LOCAL_AI, style: null));

        Assert.NotNull(view.Style);
        Assert.Equal(DisplayNames.StyleNotDetermined, view.Style!.Text);
        Assert.False(view.Style.IsDetermined);
    }

    [Fact]
    public void BasicMode_NoStyleLine()
    {
        var view = AnalysisResultView.From(Result(AnalysisMode.BASIC));

        Assert.Null(view.Style);
        Assert.Equal("Режим: Аналіз без ШІ", view.ModeText);
    }

    [Fact]
    public void AC30_AiFailed_NoticeShown_ResultIsBasic()
    {
        var notice = DesignService.StyleFailedNotice(AnalysisMode.CLOUD_AI);

        // Сервіс при відкаті повертає результат у режимі BASIC і Notice.
        var view = AnalysisResultView.From(Result(AnalysisMode.BASIC), notice);

        Assert.Equal(notice, view.Notice);
        Assert.Contains("без ШІ", view.Notice);
        Assert.Null(view.Style);
        Assert.Equal("помірне", view.LightingText);
    }

    [Fact]
    public void AC30_Completed_WithNotice_ReturnsNotice()
    {
        var notice = DesignService.StyleFailedNotice(AnalysisMode.LOCAL_AI);

        Assert.Equal(notice, AnalysisResultView.Completed(notice));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Completed_WithoutNotice_DefaultMessage(string? notice)
    {
        Assert.Equal(AnalysisResultView.CompletedMessage, AnalysisResultView.Completed(notice));
    }

    [Fact]
    public void BlankNotice_TreatedAsNone()
    {
        Assert.Null(AnalysisResultView.From(Result(AnalysisMode.BASIC), "  ").Notice);
    }

    [Theory]
    [InlineData(LightingLevel.DARK, "темне")]
    [InlineData(LightingLevel.MODERATE, "помірне")]
    [InlineData(LightingLevel.BRIGHT, "яскраве")]
    public void Lighting_HasUkrainianText(LightingLevel level, string expected)
    {
        Assert.Equal(expected, AnalysisResultView.Lighting(level));
        Assert.Equal(expected, AnalysisResultView.From(Result(AnalysisMode.BASIC, lighting: level)).LightingText);
    }

    [Fact]
    public void From_NullResult_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AnalysisResultView.From(null!));
    }
}

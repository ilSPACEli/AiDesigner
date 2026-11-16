using AiDesigner.Api.Dto;
using AiDesigner.Models;

namespace AiDesigner.Tests.Api;

public class ResultAnalysisDtoTests
{
    private static readonly ColorDto[] Colors =
    {
        new("#D8C3A5", 36, 61.2m), new("#322D28", 30, 30.6m), new("#9A8F80", 35, 8.2m)
    };

    private static ResultAnalysisDto Dto(string? mode = "CLOUD_AI", string? style = "LOFT", decimal? confidence = 0.82m,
        string? lighting = "MODERATE", IReadOnlyList<ColorDto>? colors = null) =>
        new(mode, lighting, colors ?? Colors, style, confidence);

    [Fact]
    public void Valid_CloudAiWithStyle_GivesModel()
    {
        var model = Dto().ToModel(out var error);

        Assert.Null(error);
        Assert.Equal(AnalysisMode.CLOUD_AI, model!.Mode);
        Assert.Equal(LightingLevel.MODERATE, model.Lighting);
        Assert.Equal(RoomStyle.LOFT, model.Style);
        Assert.Equal(0.82m, model.Confidence);
        Assert.Equal(3, model.Colors.Count);
    }

    [Theory]
    [InlineData("BASIC", null, null)]
    [InlineData("LOCAL_AI", null, 0.59)]
    [InlineData("CLOUD_AI", null, null)]
    [InlineData("LOCAL_AI", "BOHO", 0.6)]
    [InlineData("CLOUD_AI", "MODERN", 1.0)]
    public void Valid_Combinations(string mode, string? style, double? confidence)
    {
        var model = Dto(mode, style, (decimal?)confidence).ToModel(out var error);

        Assert.Null(error);
        Assert.NotNull(model);
    }

    [Theory]
    [InlineData(null, "LOFT", 0.8, "analysis.mode")]
    [InlineData("AI", "LOFT", 0.8, "analysis.mode")]
    [InlineData("CLOUD_AI", "GOTHIC", 0.8, "analysis.style")]
    [InlineData("CLOUD_AI", "loft", 0.8, "analysis.style")]
    [InlineData("BASIC", "LOFT", 0.8, "analysis:")]
    [InlineData("BASIC", null, 0.8, "analysis:")]
    [InlineData("CLOUD_AI", null, 1.01, "analysis.confidence")]
    [InlineData("CLOUD_AI", null, -0.1, "analysis.confidence")]
    [InlineData("CLOUD_AI", "LOFT", 0.59, "analysis.confidence")]
    [InlineData("CLOUD_AI", "LOFT", null, "analysis.confidence")]
    public void Invalid_IsRejectedWithFieldName(string? mode, string? style, double? confidence, string field)
    {
        var model = Dto(mode, style, (decimal?)confidence).ToModel(out var error);

        Assert.Null(model);
        Assert.StartsWith(field, error);
    }

    [Fact]
    public void InvalidPalette_IsRejectedWithAnalysisPrefix()
    {
        Assert.Null(Dto(lighting: "NIGHT").ToModel(out var lightingError));
        Assert.StartsWith("analysis.lighting", lightingError);

        Assert.Null(Dto(colors: Colors.Take(2).ToArray()).ToModel(out var colorsError));
        Assert.StartsWith("analysis.colors", colorsError);
    }

    [Fact]
    public void ResultDto_HasStyleAndMode_FromAnalysis()
    {
        var withAnalysis = new SavedResult
        {
            Id = 7,
            CatalogItemId = "sofa-01",
            Analysis = new ResultAnalysis { Mode = AnalysisMode.CLOUD_AI, Style = RoomStyle.LOFT }
        };
        var beforeS2 = new SavedResult { Id = 8, CatalogItemId = "sofa-01" };

        var dto = ResultDto.From(withAnalysis);
        var oldDto = ResultDto.From(beforeS2);

        Assert.Equal("LOFT", dto.Style);
        Assert.Equal("CLOUD_AI", dto.AnalysisMode);
        Assert.Null(oldDto.Style);
        Assert.Null(oldDto.AnalysisMode);
    }

    [Fact]
    public void CatalogItemDto_Style_RoundTrips()
    {
        var item = new CatalogItem { Id = "sofa-01", ImageFile = "images/catalog/sofa-01.png", Style = RoomStyle.SCANDINAVIAN };

        var dto = CatalogItemDto.From(item);

        Assert.Equal("SCANDINAVIAN", dto.Style);
        Assert.Equal(RoomStyle.SCANDINAVIAN, dto.ToModel().Style);
        Assert.Null(CatalogItemDto.From(new CatalogItem { Id = "x", ImageFile = "x.png" }).Style);
    }
}

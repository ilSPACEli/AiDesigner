using AiDesigner.Api.Dto;
using AiDesigner.Models;

namespace AiDesigner.Tests.Api;

public class AnalysisRequestDtoTests
{
    private static ColorDto Color(string? hex = "#D8C3A5", int hue = 36, decimal share = 30m) => new(hex, hue, share);

    private static AnalysisRequestDto Request(string? lighting = "MODERATE", params ColorDto[] colors) =>
        new(colors.Length > 0 ? colors : new[] { Color(), Color("#322D28", 30), Color("#F0F0F0", 0) }, lighting);

    [Fact]
    public void ToModel_ValidRequest_GivesAnalysis()
    {
        var analysis = Request().ToModel(out var error);

        Assert.Null(error);
        Assert.NotNull(analysis);
        Assert.Equal(LightingLevel.MODERATE, analysis.Lighting);
        Assert.Equal(new[] { "#D8C3A5", "#322D28", "#F0F0F0" }, analysis.Colors.Select(c => c.Hex));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("moderate")]
    [InlineData("1")]
    [InlineData("DARK, BRIGHT")]
    [InlineData("SUNNY")]
    public void ToModel_UnknownLighting_IsRejected(string? lighting)
    {
        Assert.Null(Request(lighting).ToModel(out var error));
        Assert.StartsWith("lighting", error);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void ToModel_WrongColorCount_IsRejected(int count)
    {
        var request = new AnalysisRequestDto(Enumerable.Repeat(Color(), count).ToList(), "DARK");

        Assert.Null(request.ToModel(out var error));
        Assert.StartsWith("colors:", error);
    }

    [Fact]
    public void ToModel_NoColors_IsRejected()
    {
        Assert.Null(new AnalysisRequestDto(null, "DARK").ToModel(out var error));
        Assert.StartsWith("colors:", error);
    }

    [Theory]
    [InlineData(null, 0, 10)]
    [InlineData("D8C3A5", 0, 10)]
    [InlineData("#D8C3A", 0, 10)]
    [InlineData("#GGGGGG", 0, 10)]
    [InlineData("#D8C3A5", -1, 10)]
    [InlineData("#D8C3A5", 360, 10)]
    [InlineData("#D8C3A5", 0, -1)]
    [InlineData("#D8C3A5", 0, 101)]
    public void ToModel_InvalidColor_IsRejectedWithItsIndex(string? hex, int hue, double share)
    {
        var request = Request("DARK", Color(), Color(), Color(hex, hue, (decimal)share));

        Assert.Null(request.ToModel(out var error));
        Assert.StartsWith("colors[2]", error);
    }

    [Fact]
    public void From_Analysis_RoundTripsThroughToModel()
    {
        var analysis = new AnalysisResult
        {
            Lighting = LightingLevel.BRIGHT,
            Colors = new[]
            {
                new DominantColor { Hex = "#3B5BA5", Hue = 222, Share = 45m },
                new DominantColor { Hex = "#A7B8D8", Hue = 219, Share = 35m },
                new DominantColor { Hex = "#F0F0F0", Hue = 0, Share = 20m }
            }
        };

        var back = AnalysisRequestDto.From(analysis).ToModel(out var error);

        Assert.Null(error);
        Assert.Equal(analysis.Lighting, back!.Lighting);
        Assert.Equal(analysis.Colors.Select(c => (c.Hex, c.Hue, c.Share)), back.Colors.Select(c => (c.Hex, c.Hue, c.Share)));
    }

    [Fact]
    public void CatalogItemDto_UsesContractFormat_AndRoundTrips()
    {
        var item = new CatalogItem
        {
            Id = "sofa-01",
            Name = "Сірий диван",
            Type = ItemType.SOFA,
            ImageFile = "images/catalog/sofa-01.png",
            MainColor = "#82817A",
            SuitableLightings = new[] { LightingLevel.DARK, LightingLevel.BRIGHT },
            LicenseSource = "test"
        };

        var dto = CatalogItemDto.From(item);
        var back = dto.ToModel();

        Assert.Equal("SOFA", dto.Type);
        Assert.Equal("/images/catalog/sofa-01.png", dto.ImageUrl);
        Assert.Equal(new[] { "DARK", "BRIGHT" }, dto.SuitableLightings);
        Assert.Equal(item.ImageFile, back.ImageFile);
        Assert.Equal(item.Type, back.Type);
        Assert.Equal(item.SuitableLightings, back.SuitableLightings);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(0, true)]
    public void SuggestionDto_PartialMatch_OnlyForZeroScore(int score, bool partial)
    {
        var dto = SuggestionDto.From(new Suggestion
        {
            Item = new CatalogItem { Id = "x", ImageFile = "images/catalog/x.png" },
            Rank = 1,
            Score = score,
            Reason = "r"
        });

        Assert.Equal(partial, dto.PartialMatch);
    }
}

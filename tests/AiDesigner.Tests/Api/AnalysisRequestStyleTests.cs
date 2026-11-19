using System.Net;
using System.Net.Http.Json;
using AiDesigner.Api.Dto;
using AiDesigner.Models;
using AiDesigner.Tests.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiDesigner.Tests.Api;

// Стиль кімнати в POST /api/suggestions (docs/api.md 1.3): необов'язковий, лише з переліку.
public class AnalysisRequestStyleTests
{
    private static readonly ColorDto[] Colors =
    {
        new("#3B5BA5", 222, 45m), new("#A7B8D8", 219, 35m), new("#F0F0F0", 0, 20m)
    };

    [Theory]
    [InlineData("LOFT", RoomStyle.LOFT)]
    [InlineData("SCANDINAVIAN", RoomStyle.SCANDINAVIAN)]
    [InlineData(null, null)]
    public void ValidStyle_GoesToModel(string? style, RoomStyle? expected)
    {
        var model = new AnalysisRequestDto(Colors, "MODERATE", style).ToModel(out var error);

        Assert.Null(error);
        Assert.Equal(expected, model!.Style);
    }

    [Theory]
    [InlineData("GOTHIC")]
    [InlineData("loft")]
    [InlineData("")]
    [InlineData("1")]
    public void UnknownStyle_IsRejected(string style)
    {
        var model = new AnalysisRequestDto(Colors, "MODERATE", style).ToModel(out var error);

        Assert.Null(model);
        Assert.Equal("style: невідомий стиль.", error);
    }

    [Fact]
    public void From_Analysis_CarriesStyle()
    {
        var analysis = Palettes.Of(LightingLevel.MODERATE, Palettes.Rooms["синя"]);
        var withStyle = new AnalysisResult { Colors = analysis.Colors, Lighting = analysis.Lighting, Style = RoomStyle.BOHO };

        Assert.Equal("BOHO", AnalysisRequestDto.From(withStyle).Style);
        Assert.Null(AnalysisRequestDto.From(analysis).Style);
    }

    [Fact]
    public async Task PostSuggestions_WithStyle_Returns200()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/suggestions",
            new AnalysisRequestDto(Colors, "MODERATE", "LOFT"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostSuggestions_UnknownStyle_Returns400()
    {
        using var factory = new ApiFactory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/suggestions",
            new AnalysisRequestDto(Colors, "MODERATE", "GOTHIC"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("style: невідомий стиль.", problem!.Detail);
    }
}

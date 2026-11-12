using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AiDesigner.Api.Dto;
using AiDesigner.Api.Endpoints;
using AiDesigner.Data;
using AiDesigner.Models;
using AiDesigner.Repositories;
using AiDesigner.Tests.Data;
using AiDesigner.Tests.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiDesigner.Tests.Api;

public class SuggestionEndpointsTests
{
    private static readonly IReadOnlyList<CatalogItem> CatalogJson =
        CatalogLoader.LoadFromFile(Path.Combine(RepoPaths.WebRoot, CatalogLoader.DefaultRelativePath));

    private static AnalysisRequestDto BlueRoom(LightingLevel lighting = LightingLevel.MODERATE) =>
        AnalysisRequestDto.From(Palettes.Of(lighting, Palettes.Rooms["синя"]));

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string title)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal((int)status, problem!.Status);
        Assert.Equal(title, problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }

    // ---------- GET /api/catalog ----------

    [Fact]
    public async Task GetCatalog_ReturnsWholeCatalog_InContractFormat()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/catalog");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(CatalogJson.Count, items.Count);

        var first = items[0];
        Assert.Equal(JsonValueKind.Array, first.GetProperty("suitableLightings").ValueKind);
        Assert.StartsWith("/images/", first.GetProperty("imageUrl").GetString());
        Assert.Equal(JsonValueKind.String, first.GetProperty("type").ValueKind);
        Assert.Equal(CatalogJson.Select(i => i.Id).Order(StringComparer.Ordinal),
                     items.Select(i => i.GetProperty("id").GetString()));
    }

    // ---------- POST /api/suggestions ----------

    [Fact]
    public async Task PostSuggestions_Valid_Returns200_WithRankedFullItems()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/suggestions", BlueRoom());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var suggestions = await response.Content.ReadFromJsonAsync<List<SuggestionDto>>();
        Assert.InRange(suggestions!.Count, 3, 4);
        Assert.Equal(Enumerable.Range(1, suggestions.Count), suggestions.Select(s => s.Rank));
        Assert.All(suggestions, s =>
        {
            Assert.StartsWith("/images/", s.Item.ImageUrl);
            Assert.Contains("MODERATE", s.Item.SuitableLightings);
            Assert.Equal(s.Score == 0, s.PartialMatch);
        });
    }

    [Theory]
    [InlineData("""{"colors":[{"hex":"#D8C3A5","hue":36,"share":60}],"lighting":"DARK"}""")]
    [InlineData("""{"colors":[{"hex":"#D8C3A5","hue":36,"share":60},{"hex":"#322D28","hue":30,"share":30},{"hex":"#F0F0F0","hue":0,"share":10}],"lighting":"NIGHT"}""")]
    [InlineData("""{"colors":[{"hex":"red","hue":36,"share":60},{"hex":"#322D28","hue":30,"share":30},{"hex":"#F0F0F0","hue":0,"share":10}],"lighting":"DARK"}""")]
    [InlineData("""{"colors":""")]
    [InlineData("null")]
    public async Task PostSuggestions_InvalidBody_Returns400Problem(string body)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/suggestions", new StringContent(body, Encoding.UTF8, "application/json"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, SuggestionEndpoints.BadRequestTitle);
    }

    [Fact]
    public async Task PostSuggestions_NotJson_Returns400Problem()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/suggestions", new StringContent("lighting=DARK", Encoding.UTF8, "text/plain"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, SuggestionEndpoints.BadRequestTitle);
    }

    [Fact]
    public async Task PostSuggestions_NoItemForLighting_Returns422Problem()
    {
        var onlyBright = new CatalogItem
        {
            Id = "lamp", Name = "Лампа", Type = ItemType.LAMP, ImageFile = "images/catalog/lamp.png",
            MainColor = "#F5F5F5", SuitableLightings = new[] { LightingLevel.BRIGHT }, LicenseSource = "test"
        };
        using var factory = new ApiFactory(catalog: new InMemoryCatalogRepository(new[] { onlyBright }));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/suggestions", BlueRoom(LightingLevel.DARK));

        await AssertProblemAsync(response, (HttpStatusCode)422, SuggestionEndpoints.NoSuitableItemsTitle);
    }

    [Fact]
    public async Task PostSuggestions_EmptyCatalog_Returns503Problem()
    {
        using var factory = new ApiFactory(catalog: new InMemoryCatalogRepository(Array.Empty<CatalogItem>()));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/suggestions", BlueRoom());

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, SuggestionEndpoints.CatalogUnavailableTitle);
    }
}

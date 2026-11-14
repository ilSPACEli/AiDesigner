using AiDesigner.Api.Dto;
using AiDesigner.Clients;
using AiDesigner.Data;
using AiDesigner.Models;
using AiDesigner.Repositories;
using AiDesigner.Tests.Api;
using AiDesigner.Tests.Data;
using AiDesigner.Tests.Services;

namespace AiDesigner.Tests.Clients;

// SuggestionClient і CatalogClient "по-справжньому": через ApiFactory (TestServer), без мережі й без PostgreSQL.
public class SuggestionClientTests
{
    private static AnalysisRequestDto BlueRoom(LightingLevel lighting = LightingLevel.MODERATE) =>
        AnalysisRequestDto.From(Palettes.Of(lighting, Palettes.Rooms["синя"]));

    [Fact]
    public async Task Suggest_Valid_ReturnsSuccess_WithItemsUsableOnCanvas()
    {
        using var factory = new ApiFactory();
        var client = new SuggestionClient(factory.CreateClient());

        var result = await client.SuggestAsync(BlueRoom());

        Assert.Equal(SuggestionClientStatus.Success, result.Status);
        Assert.InRange(result.Suggestions.Count, 3, 4);
        var item = result.Suggestions[0].ToModel().Item;
        Assert.StartsWith("images/catalog/", item.ImageFile);
        Assert.False(string.IsNullOrWhiteSpace(item.Name));
    }

    [Fact]
    public async Task Suggest_NoItemForLighting_ReturnsNoSuitableItems()
    {
        var onlyBright = new CatalogItem
        {
            Id = "lamp", Name = "Лампа", Type = ItemType.LAMP, ImageFile = "images/catalog/lamp.png",
            MainColor = "#F5F5F5", SuitableLightings = new[] { LightingLevel.BRIGHT }, LicenseSource = "test"
        };
        using var factory = new ApiFactory(catalog: new InMemoryCatalogRepository(new[] { onlyBright }));
        var client = new SuggestionClient(factory.CreateClient());

        var result = await client.SuggestAsync(BlueRoom(LightingLevel.DARK));

        Assert.Equal(SuggestionClientStatus.NoSuitableItems, result.Status);
        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public async Task Suggest_EmptyCatalog_ReturnsUnavailable()
    {
        using var factory = new ApiFactory(catalog: new InMemoryCatalogRepository(Array.Empty<CatalogItem>()));
        var client = new SuggestionClient(factory.CreateClient());

        var result = await client.SuggestAsync(BlueRoom());

        Assert.Equal(SuggestionClientStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Suggest_InvalidRequest_ReturnsUnavailable()
    {
        using var factory = new ApiFactory();
        var client = new SuggestionClient(factory.CreateClient());

        var result = await client.SuggestAsync(new AnalysisRequestDto(Array.Empty<ColorDto>(), "DARK"));

        Assert.Equal(SuggestionClientStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task Catalog_GetAll_ReturnsWholeCatalog()
    {
        using var factory = new ApiFactory();
        var client = new CatalogClient(factory.CreateClient());
        var expected = CatalogLoader.LoadFromFile(Path.Combine(RepoPaths.WebRoot, CatalogLoader.DefaultRelativePath));

        var items = await client.GetAllAsync();

        Assert.Equal(expected.Select(i => i.Id).Order(StringComparer.Ordinal), items.Select(i => i.Id));
        Assert.All(items, i => Assert.StartsWith("/images/", i.ImageUrl));
        Assert.All(items, i => Assert.NotEmpty(i.SuitableLightings));
    }
}

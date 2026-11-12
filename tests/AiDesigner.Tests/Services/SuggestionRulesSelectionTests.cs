using System.Collections.Generic;
using System.Linq;
using Xunit;
using AiDesigner.Models;
using AiDesigner.Services;

namespace AiDesigner.Tests.Services;

public class CatalogTests
{
    private IReadOnlyList<CatalogItem> GetTestItems()
    {
        return new List<CatalogItem>
        {
            new CatalogItem { Id = "item1", Name = "Сірий диван", Type = ItemType.SOFA, MainColor = "#808080", SuitableLightings = new[] { LightingLevel.BRIGHT, LightingLevel.MODERATE }, ImageFile = "1.png" },

            new CatalogItem { Id = "item2", Name = "Червоний стіл", Type = ItemType.TABLE, MainColor = "#FF0000", SuitableLightings = new[] { LightingLevel.BRIGHT }, ImageFile = "2.png" },
            new CatalogItem { Id = "item3", Name = "Бордовий стіл", Type = ItemType.TABLE, MainColor = "#AA0000", SuitableLightings = new[] { LightingLevel.BRIGHT }, ImageFile = "3.png" },
            new CatalogItem { Id = "item4", Name = "Малиновий стіл", Type = ItemType.TABLE, MainColor = "#FF0033", SuitableLightings = new[] { LightingLevel.BRIGHT }, ImageFile = "4.png" },
 
            new CatalogItem { Id = "item5", Name = "Блакитний диван", Type = ItemType.SOFA, MainColor = "#00FFFF", SuitableLightings = new[] { LightingLevel.BRIGHT }, ImageFile = "5.png" },

            new CatalogItem { Id = "item6", Name = "Темна тумба", Type = ItemType.SOFA, MainColor = "#808080", SuitableLightings = new[] { LightingLevel.DARK }, ImageFile = "6.png" }
        };
    }

    [Fact]
    public void AC06_SameInput_ReturnsSameSuggestions()
    {

        var catalog = new Catalog(GetTestItems());
        var analysis = new AnalysisResult
        {
            Lighting = LightingLevel.BRIGHT,
            Colors = new List<DominantColor> { new DominantColor { Hex = "#FF0000", Hue = 0, Share = 60 } }
        };

        var result1 = catalog.Select(analysis, 4);
        var result2 = catalog.Select(analysis, 4);

        Assert.Equal(result1.Count, result2.Count);
        for (int i = 0; i < result1.Count; i++)
        {
            Assert.Equal(result1[i].Item.Id, result2[i].Item.Id);
            Assert.Equal(result1[i].Rank, result2[i].Rank);
        }
    }

    [Fact]
    public void AC07_SuggestionHasRequiredFields()
    {
        var catalog = new Catalog(GetTestItems());
        var analysis = new AnalysisResult
        {
            Lighting = LightingLevel.BRIGHT,
            Colors = new List<DominantColor> { new DominantColor { Hex = "#FF0000", Hue = 0, Share = 100 } }
        };

        var result = catalog.Select(analysis, 4);

        Assert.NotEmpty(result);
        foreach (var suggestion in result)
        {
            Assert.NotNull(suggestion.Item);
            Assert.False(string.IsNullOrEmpty(suggestion.Item.Name));
            Assert.False(string.IsNullOrEmpty(suggestion.Item.ImageFile));
            Assert.False(string.IsNullOrEmpty(suggestion.Reason));
            Assert.True(suggestion.Rank > 0);
        }
    }

    [Fact]
    public void R2_Select_ExcludesItemsWithIncompatibleLighting()
    {
        var catalog = new Catalog(GetTestItems());
        var analysis = new AnalysisResult
        {
            Lighting = LightingLevel.BRIGHT, 
            Colors = new List<DominantColor> { new DominantColor { Hex = "#FF0000", Hue = 0, Share = 100 } }
        };

        var result = catalog.Select(analysis, 4);

        Assert.DoesNotContain(result, s => s.Item.Id == "item6");
    }

    [Fact]
    public void R3_Select_ReturnsMax2ItemsOfSameType()
    {
        // Arrange
        var catalog = new Catalog(GetTestItems());
        var analysis = new AnalysisResult
        {
            Lighting = LightingLevel.BRIGHT,
            Colors = new List<DominantColor> { new DominantColor { Hex = "#FF0000", Hue = 0, Share = 100 } }
        };

        var result = catalog.Select(analysis, 4);

        var tableCount = result.Count(s => s.Item.Type == ItemType.TABLE);
        Assert.True(tableCount <= 2, "Має бути не більше 2 предметів одного типу (R3)");
    }

    [Fact]
    public void R1_NeutralPalette_IgnoresHueAndSelectsNeutral()
    {
        var catalog = new Catalog(GetTestItems());
        var analysis = new AnalysisResult
        {
            Lighting = LightingLevel.BRIGHT,

            Colors = new List<DominantColor> { new DominantColor { Hex = "#808080", Hue = 0, Share = 100 } }
        };

        var result = catalog.Select(analysis, 4);

        var sofa = result.FirstOrDefault(r => r.Item.Id == "item1");
        Assert.NotNull(sofa);
        Assert.Equal("Універсальний нейтральний колір, що підходить для цієї кімнати.", sofa.Reason);
    }
}
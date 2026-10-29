using AiDesigner.Data;
using AiDesigner.Models;
using AiDesigner.Services;
using AiDesigner.Services.Analysis;
using AiDesigner.Tests.Data;

namespace AiDesigner.Tests.Services;

// Правила добору R1–R5 (docs/requirements.md, розділ 4) і AC-06 для UC-03
public class CatalogRulesTests
{
    private static readonly LightingLevel[] AllLevels = { LightingLevel.DARK, LightingLevel.MODERATE, LightingLevel.BRIGHT };

    // ---------- Допоміжне: тестові предмети й палітри ----------

    private static CatalogItem Item(string id, ItemType type, string hex, params LightingLevel[] lightings) => new()
    {
        Id = id,
        Name = id,
        Type = type,
        ImageFile = $"images/catalog/{id}.png",
        MainColor = hex,
        SuitableLightings = lightings.Length > 0 ? lightings : AllLevels,
        LicenseSource = "test"
    };

    // Палітра як від аналізатора: відтінок рахуємо з hex, частки — як задано
    private static AnalysisResult Palette(LightingLevel lighting, params (string Hex, decimal Share)[] colors) => new()
    {
        Lighting = lighting,
        Colors = colors
            .Select(c => new DominantColor { Hex = c.Hex, Hue = (int)Math.Round(ColorHelper.GetHue(c.Hex)) % 360, Share = c.Share })
            .ToList()
    };

    private static Suggestion Find(IReadOnlyList<Suggestion> result, string id) => Assert.Single(result, s => s.Item.Id == id);

    // ---------- R1: колір ----------

    [Fact]
    public void R1_Analogous_WithinThirtyDegrees()
    {
        // Палітра — червоний (0°). Помаранчевий 30° — ще аналогова гармонія, 32° — уже ні.
        var catalog = new Catalog(new[]
        {
            Item("in", ItemType.SOFA, "#FF7F00"),    // ≈ 30°
            Item("out", ItemType.LAMP, "#FF8800")    // ≈ 32°
        });

        var result = catalog.Select(Palette(LightingLevel.MODERATE, ("#FF0000", 100)), 4);

        Assert.Equal(2, Find(result, "in").Score);
        Assert.Contains("Аналогова", Find(result, "in").Reason);
        Assert.Equal(0, Find(result, "out").Score);
    }

    [Fact]
    public void R1_Complementary_OppositeHueWithinThirtyDegrees()
    {
        // Протилежний до червоного (0°) — 180°. Бірюзовий 180° і 200° — комплементарні, 220° — ні.
        var catalog = new Catalog(new[]
        {
            Item("cyan", ItemType.RUG, "#00FFFF"),     // 180°
            Item("sky", ItemType.LAMP, "#00AAFF"),     // 200°
            Item("blue", ItemType.SOFA, "#0055FF")     // 220°
        });

        var result = catalog.Select(Palette(LightingLevel.MODERATE, ("#FF0000", 100)), 4);

        Assert.Contains("Комплементарна", Find(result, "cyan").Reason);
        Assert.Contains("Комплементарна", Find(result, "sky").Reason);
        Assert.Equal(0, Find(result, "blue").Score);
    }

    [Theory]
    [InlineData("#FF002B", "#FF2B00")]   // 350° і 10° — різниця 20°, а не 340°
    [InlineData("#FF2B00", "#FF002B")]
    public void R1_HueDistance_WrapsAround360(string roomHex, string itemHex)
    {
        var catalog = new Catalog(new[] { Item("item", ItemType.SOFA, itemHex) });

        var result = catalog.Select(Palette(LightingLevel.MODERATE, (roomHex, 100)), 4);

        Assert.Equal(2, Find(result, "item").Score);
        Assert.Contains("Аналогова", Find(result, "item").Reason);
    }

    [Fact]
    public void R1_NeutralDominantColor_IsNotUsedForHarmony()
    {
        // Сірий (Hue = 0) — найбільший колір, але відтінку не має: червоний предмет НЕ отримує «гармонію з червоним».
        // Гармонія рахується від найбільшого ненейтрального кольору — синього.
        var catalog = new Catalog(new[]
        {
            Item("red", ItemType.SOFA, "#FF0000"),
            Item("blue", ItemType.LAMP, "#0000FF"),
            Item("gray", ItemType.SHELF, "#808080")
        });

        var result = catalog.Select(Palette(LightingLevel.MODERATE, ("#808080", 70), ("#0000FF", 30)), 4);

        Assert.Equal(0, Find(result, "red").Score);
        Assert.Equal(2, Find(result, "blue").Score);
        Assert.Equal(1, Find(result, "gray").Score);
    }

    [Fact]
    public void R1_AllNeutralPalette_OnlyNeutralItemsMatchByColor()
    {
        var catalog = new Catalog(new[]
        {
            Item("gray", ItemType.SOFA, "#808080"),
            Item("red", ItemType.TABLE, "#FF0000")
        });

        var result = catalog.Select(Palette(LightingLevel.MODERATE, ("#808080", 60), ("#D8D2C8", 40)), 4);

        Assert.Equal(1, Find(result, "gray").Score);
        Assert.Equal(Catalog.PartialMatchReason, Find(result, "red").Reason);
    }

    // ---------- R2: освітленість ----------

    [Fact]
    public void R2_ItemWithWrongLighting_IsNeverSuggested_EvenByR5()
    {
        // Червоний предмет ідеально пасує за кольором, але не за освітленістю.
        var catalog = new Catalog(new[]
        {
            Item("dark-only", ItemType.SOFA, "#FF0000", LightingLevel.DARK),
            Item("blue", ItemType.LAMP, "#0000FF"),
            Item("green", ItemType.PLANT, "#00FF00")
        });

        var result = catalog.Select(Palette(LightingLevel.BRIGHT, ("#FF0000", 100)), 4);

        Assert.DoesNotContain(result, s => s.Item.Id == "dark-only");
    }

    // ---------- R3: різноманітність ----------

    [Fact]
    public void R3_ThirdItemOfSameType_IsSkipped_NextTypeIsTaken()
    {
        var catalog = new Catalog(new[]
        {
            Item("t1", ItemType.TABLE, "#FF0000"),
            Item("t2", ItemType.TABLE, "#FF0011"),
            Item("t3", ItemType.TABLE, "#FF1100"),
            Item("x-lamp", ItemType.LAMP, "#FF2200")
        });

        var result = catalog.Select(Palette(LightingLevel.MODERATE, ("#FF0000", 100)), 4);

        Assert.Equal(2, result.Count(s => s.Item.Type == ItemType.TABLE));
        Assert.DoesNotContain(result, s => s.Item.Id == "t3");
        Assert.Contains(result, s => s.Item.Id == "x-lamp");
    }

    // ---------- R4: ранжування ----------

    [Fact]
    public void R4_HigherScoreFirst_TiesById_RanksFromOne()
    {
        var catalog = new Catalog(new[]
        {
            Item("b-gray", ItemType.SOFA, "#808080"),     // 1 — нейтральний
            Item("c-red", ItemType.TABLE, "#FF0000"),     // 2
            Item("a-red", ItemType.LAMP, "#FF0011"),      // 2
            Item("d-orange", ItemType.RUG, "#FF6A00")     // 2
        });

        var result = catalog.Select(Palette(LightingLevel.MODERATE, ("#FF0000", 100)), 4);

        Assert.Equal(new[] { "a-red", "c-red", "d-orange", "b-gray" }, result.Select(s => s.Item.Id));
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Select(s => s.Rank));
    }

    // ---------- R5: недостатньо придатних ----------

    [Fact]
    public void R5_FewerThanThreeColorMatches_AreFilledWithMark()
    {
        // За кольором пасує лише один предмет — перелік доповнюється за R2 з позначкою.
        var catalog = new Catalog(new[]
        {
            Item("red", ItemType.SOFA, "#FF0000"),
            Item("blue", ItemType.LAMP, "#0000FF"),
            Item("green", ItemType.PLANT, "#00FF00"),
            Item("violet", ItemType.RUG, "#8000FF")
        });

        var result = catalog.Select(Palette(LightingLevel.MODERATE, ("#FF0000", 100)), 4);

        Assert.InRange(result.Count, 3, 4);
        Assert.Equal("red", result[0].Item.Id);
        Assert.All(result.Skip(1), s =>
        {
            Assert.Equal(0, s.Score);
            Assert.Equal(Catalog.PartialMatchReason, s.Reason);
        });
    }

    [Fact]
    public void R5_NoItemForLighting_ReturnsEmpty()
    {
        // Жодного предмета для яскравого приміщення — порожній перелік;
        // повідомлення «придатних предметів немає» показує DesignService.
        var catalog = new Catalog(new[]
        {
            Item("a", ItemType.SOFA, "#FF0000", LightingLevel.DARK),
            Item("b", ItemType.LAMP, "#808080", LightingLevel.DARK)
        });

        var result = catalog.Select(Palette(LightingLevel.BRIGHT, ("#FF0000", 100)), 4);

        Assert.Empty(result);
    }

    // ---------- AC-06 на справжньому каталозі ----------

    // Типові палітри кімнат (зокрема справжні результати аналізу з ручних перевірок)
    public static IEnumerable<object[]> RealPalettes()
    {
        var palettes = new Dictionary<string, (string, decimal)[]>
        {
            ["теракотова"] = new[] { ("#D7D2C7", 49.9m), ("#954A28", 17.1m), ("#C7775E", 16.8m), ("#814A27", 16.2m) },
            ["сіра спальня"] = new[] { ("#A89E9D", 27.9m), ("#DBD4CF", 27.9m), ("#7C787E", 19.3m), ("#57515D", 13.5m), ("#2A272F", 11.4m) },
            ["синя"] = new[] { ("#3B5BA5", 45m), ("#A7B8D8", 35m), ("#F0F0F0", 20m) },
            ["зелена"] = new[] { ("#4F7942", 50m), ("#C9D6A3", 30m), ("#2E2E2E", 20m) },
            ["фіолетова"] = new[] { ("#6A3D9A", 60m), ("#CAB2D6", 40m) }
        };

        foreach (var (name, colors) in palettes)
            foreach (var level in AllLevels)
                yield return new object[] { name, level, colors };
    }

    private static readonly Catalog RealCatalog = new(
        CatalogLoader.LoadFromFile(Path.Combine(RepoPaths.WebRoot, CatalogLoader.DefaultRelativePath)));

    [Theory]
    [MemberData(nameof(RealPalettes))]
    public void AC06_RealCatalog_GivesThreeToFourItems_EachSatisfyingR1toR3_OrMarkedR5(
        string palette, LightingLevel lighting, (string Hex, decimal Share)[] colors)
    {
        var analysis = Palette(lighting, colors);

        var result = RealCatalog.Select(analysis, DesignService.SuggestionCount);

        // 3–4 предмети
        Assert.InRange(result.Count, 3, 4);
        Assert.Equal(Enumerable.Range(1, result.Count), result.Select(s => s.Rank));

        // R3: не більше 2 одного типу
        Assert.All(result.GroupBy(s => s.Item.Type), g => Assert.True(g.Count() <= 2, $"{palette}: {g.Key} × {g.Count()}"));

        var colorMatches = result.Count(s => s.Score > 0);
        foreach (var s in result)
        {
            // R2: кожен предмет придатний за освітленістю
            Assert.Contains(lighting, s.Item.SuitableLightings);

            // R1 або R5 з позначкою
            if (s.Score > 0)
                Assert.NotEqual(Catalog.PartialMatchReason, s.Reason);
            else
                Assert.Equal(Catalog.PartialMatchReason, s.Reason);
        }

        // R5 вмикається лише тоді, коли за кольором пасує менше трьох
        if (result.Any(s => s.Score == 0))
            Assert.True(colorMatches < 3, $"{palette}/{lighting}: R5 при {colorMatches} збігах за кольором");
    }

    [Theory]
    [MemberData(nameof(RealPalettes))]
    public void AC06_RealCatalog_SameInputGivesSameList(string palette, LightingLevel lighting, (string Hex, decimal Share)[] colors)
    {
        var analysis = Palette(lighting, colors);

        var first = RealCatalog.Select(analysis, DesignService.SuggestionCount).Select(s => s.Item.Id);
        var second = RealCatalog.Select(analysis, DesignService.SuggestionCount).Select(s => s.Item.Id);

        Assert.Equal(first, second);
        _ = palette;
    }
}

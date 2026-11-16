using AiDesigner.Models;
using Microsoft.EntityFrameworkCore;

namespace AiDesigner.Tests.Data;

// Відображення моделі на таблиці: що записали — те й прочитали; ключі й обмеження діють.
public class AppDbContextTests
{
    private static CatalogItem Item(string id, params LightingLevel[] lightings) => new()
    {
        Id = id,
        Name = "Тестове крісло",
        Type = ItemType.ARMCHAIR,
        ImageFile = $"images/catalog/{id}.png",
        MainColor = "#997B5A",
        SuitableLightings = lightings,
        LicenseSource = "test"
    };

    [Theory]
    [InlineData(new[] { LightingLevel.DARK })]
    [InlineData(new[] { LightingLevel.DARK, LightingLevel.BRIGHT })]
    [InlineData(new[] { LightingLevel.DARK, LightingLevel.MODERATE, LightingLevel.BRIGHT })]
    public void CatalogItem_RoundTrip_KeepsTypeAndLightings(LightingLevel[] lightings)
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
        {
            db.CatalogItems.Add(Item("chair-01", lightings));
            db.SaveChanges();
        }

        using var check = testDb.CreateContext();
        var item = check.CatalogItems.Single();
        Assert.Equal(ItemType.ARMCHAIR, item.Type);
        Assert.Equal(lightings, item.SuitableLightings);
    }

    [Fact]
    public void CatalogItem_StoresTypeAndLightingsAsText()
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
        {
            db.CatalogItems.Add(Item("chair-01", LightingLevel.DARK, LightingLevel.BRIGHT));
            db.SaveChanges();
        }

        using var check = testDb.CreateContext();
        var row = check.Database
            .SqlQuery<StoredText>($"SELECT Type, SuitableLightings FROM CatalogItems")
            .Single();
        Assert.Equal("ARMCHAIR", row.Type);
        Assert.Equal("DARK,BRIGHT", row.SuitableLightings);
    }

    [Fact]
    public void SavedResult_RoundTrip_KeepsImagesDateAndItem()
    {
        using var testDb = new TestDb();
        var createdAt = new DateTime(2026, 11, 14, 18, 20, 0, DateTimeKind.Utc);
        int id;

        using (var db = testDb.CreateContext())
        {
            db.CatalogItems.Add(Item("chair-01", LightingLevel.MODERATE));
            var result = new SavedResult
            {
                CreatedAt = createdAt,
                CatalogItemId = "chair-01",
                ImageData = new byte[] { 1, 2, 3 },
                ThumbnailData = new byte[] { 4, 5 }
            };
            db.SavedResults.Add(result);
            db.SaveChanges();
            id = result.Id;
        }

        Assert.True(id > 0);   // Id призначила БД

        using var check = testDb.CreateContext();
        var saved = check.SavedResults.Include(r => r.CatalogItem).Single(r => r.Id == id);
        Assert.Equal(createdAt, saved.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);   // DateTime.Equals не порівнює Kind
        Assert.Equal(new byte[] { 1, 2, 3 }, saved.ImageData);
        Assert.Equal(new byte[] { 4, 5 }, saved.ThumbnailData);
        Assert.Equal("Тестове крісло", saved.CatalogItem!.Name);
    }

    [Fact]
    public void SavedResult_UnknownCatalogItem_IsRejected()
    {
        using var testDb = new TestDb();
        using var db = testDb.CreateContext();

        db.SavedResults.Add(new SavedResult
        {
            CreatedAt = DateTime.UtcNow,
            CatalogItemId = "no-such-item",
            ImageData = new byte[] { 1 },
            ThumbnailData = new byte[] { 1 }
        });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void CatalogItem_WithSavedResults_CannotBeDeleted()
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
        {
            db.CatalogItems.Add(Item("chair-01", LightingLevel.MODERATE));
            db.SavedResults.Add(new SavedResult
            {
                CreatedAt = DateTime.UtcNow,
                CatalogItemId = "chair-01",
                ImageData = new byte[] { 1 },
                ThumbnailData = new byte[] { 1 }
            });
            db.SaveChanges();
        }

        using var check = testDb.CreateContext();
        check.CatalogItems.Remove(check.CatalogItems.Single());
        Assert.Throws<DbUpdateException>(() => check.SaveChanges());   // DeleteBehavior.Restrict
    }

    // ---------- S2: стиль предмета й аналіз роботи (FR-21) ----------

    private static ResultAnalysis Analysis(RoomStyle? style = RoomStyle.LOFT, decimal? confidence = 0.82m) => new()
    {
        Mode = AnalysisMode.CLOUD_AI,
        Lighting = LightingLevel.MODERATE,
        Colors = new[]
        {
            new DominantColor { Hex = "#D8C3A5", Hue = 36, Share = 61.2m },
            new DominantColor { Hex = "#322D28", Hue = 30, Share = 30.6m },
            new DominantColor { Hex = "#9A8F80", Hue = 35, Share = 8.2m }
        },
        Style = style,
        Confidence = confidence
    };

    private static SavedResult Result(string itemId, ResultAnalysis? analysis = null) => new()
    {
        CreatedAt = DateTime.UtcNow,
        CatalogItemId = itemId,
        ImageData = new byte[] { 1 },
        ThumbnailData = new byte[] { 1 },
        Analysis = analysis
    };

    [Theory]
    [InlineData(RoomStyle.LOFT)]
    [InlineData(null)]
    public void CatalogItem_Style_RoundTrip(RoomStyle? style)
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
        {
            var item = Item("chair-01", LightingLevel.DARK);
            db.CatalogItems.Add(new CatalogItem
            {
                Id = item.Id, Name = item.Name, Type = item.Type, ImageFile = item.ImageFile,
                MainColor = item.MainColor, SuitableLightings = item.SuitableLightings,
                LicenseSource = item.LicenseSource, Style = style
            });
            db.SaveChanges();
        }

        using var check = testDb.CreateContext();
        Assert.Equal(style, check.CatalogItems.Single().Style);
    }

    [Fact]
    public void SavedResult_WithAnalysis_SavedTogether_AndRoundTrips()
    {
        using var testDb = new TestDb();
        int id;
        using (var db = testDb.CreateContext())
        {
            db.CatalogItems.Add(Item("chair-01", LightingLevel.MODERATE));
            var result = Result("chair-01", Analysis());
            db.SavedResults.Add(result);
            db.SaveChanges();   // обидва записи — одним SaveChanges
            id = result.Id;
        }

        using var check = testDb.CreateContext();
        var saved = check.SavedResults.Include(r => r.Analysis).Single(r => r.Id == id);
        var analysis = saved.Analysis!;
        Assert.Equal(id, analysis.SavedResultId);
        Assert.Equal(AnalysisMode.CLOUD_AI, analysis.Mode);
        Assert.Equal(LightingLevel.MODERATE, analysis.Lighting);
        Assert.Equal(RoomStyle.LOFT, analysis.Style);
        Assert.Equal(0.82m, analysis.Confidence);
        Assert.Equal(new[] { "#D8C3A5", "#322D28", "#9A8F80" }, analysis.Colors.Select(c => c.Hex));
        Assert.Equal(new[] { 61.2m, 30.6m, 8.2m }, analysis.Colors.Select(c => c.Share));
    }

    [Fact]
    public void ResultAnalysis_StyleNotDetermined_StoredAsNull()
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
        {
            db.CatalogItems.Add(Item("chair-01", LightingLevel.MODERATE));
            db.SavedResults.Add(Result("chair-01", Analysis(style: null, confidence: null)));
            db.SaveChanges();
        }

        using var check = testDb.CreateContext();
        var analysis = check.ResultAnalyses.Single();
        Assert.Null(analysis.Style);
        Assert.Null(analysis.Confidence);
    }

    [Fact]
    public void SavedResult_WithoutAnalysis_IsAllowed()
    {
        // Роботи, збережені до S2, аналізу не мають.
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
        {
            db.CatalogItems.Add(Item("chair-01", LightingLevel.MODERATE));
            db.SavedResults.Add(Result("chair-01"));
            db.SaveChanges();
        }

        using var check = testDb.CreateContext();
        Assert.Null(check.SavedResults.Include(r => r.Analysis).Single().Analysis);
        Assert.Empty(check.ResultAnalyses);
    }

    [Fact]
    public void DeletingSavedResult_DeletesItsAnalysis()
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
        {
            db.CatalogItems.Add(Item("chair-01", LightingLevel.MODERATE));
            db.SavedResults.Add(Result("chair-01", Analysis()));
            db.SaveChanges();
        }

        using (var db = testDb.CreateContext())
        {
            // Так видаляє репозиторій (DELETE без завантаження): каскад робить сама БД
            db.SavedResults.ExecuteDelete();
        }

        using var check = testDb.CreateContext();
        Assert.Empty(check.ResultAnalyses);
    }

    [Fact]
    public void ResultAnalysis_WithoutSavedResult_IsRejected()
    {
        using var testDb = new TestDb();
        using var db = testDb.CreateContext();
        db.ResultAnalyses.Add(new ResultAnalysis
        {
            SavedResultId = 999, Mode = AnalysisMode.BASIC, Lighting = LightingLevel.DARK,
            Colors = Analysis().Colors
        });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());   // зовнішній ключ
    }

    [Fact]
    public void ResultAnalysis_StoresEnumsAndPaletteAsText()
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
        {
            db.CatalogItems.Add(Item("chair-01", LightingLevel.MODERATE));
            db.SavedResults.Add(Result("chair-01", Analysis()));
            db.SaveChanges();
        }

        using var check = testDb.CreateContext();
        var row = check.Database
            .SqlQuery<StoredAnalysis>($"SELECT Mode, Lighting, Style, Colors FROM ResultAnalyses")
            .Single();
        Assert.Equal("CLOUD_AI", row.Mode);
        Assert.Equal("MODERATE", row.Lighting);
        Assert.Equal("LOFT", row.Style);
        Assert.StartsWith("[{\"hex\":\"#D8C3A5\",\"hue\":36,\"share\":61.2}", row.Colors);
    }

    private sealed record StoredText(string Type, string SuitableLightings);

    private sealed record StoredAnalysis(string Mode, string Lighting, string? Style, string Colors);
}

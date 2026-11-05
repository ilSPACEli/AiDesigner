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

    private sealed record StoredText(string Type, string SuitableLightings);
}

using AiDesigner.Data;
using AiDesigner.Models;

namespace AiDesigner.Tests.Data;

// Деталі синхронізації каталогу, які не покривають сценарії AC-25 у CatalogSyncTests.
public class CatalogSeederTests
{
    private static readonly string CatalogPath =
        Path.Combine(RepoPaths.WebRoot, CatalogLoader.DefaultRelativePath);

    private static readonly int CatalogCount = CatalogLoader.LoadFromFile(CatalogPath).Count;

    [Fact]
    public void Sync_TableWithForeignItem_KeepsItAndAddsCatalog()
    {
        using var testDb = new TestDb();

        using (var db = testDb.CreateContext())
        {
            db.CatalogItems.Add(new CatalogItem
            {
                Id = "own-01",
                Name = "Свій предмет",
                Type = ItemType.LAMP,
                ImageFile = "images/catalog/own-01.png",
                MainColor = "#FFFFFF",
                SuitableLightings = new[] { LightingLevel.DARK },
                LicenseSource = "test"
            });
            db.SaveChanges();
        }

        CatalogSyncResult result;
        using (var db = testDb.CreateContext())
            result = CatalogSeeder.Sync(db, CatalogPath);

        Assert.Equal(CatalogCount, result.Added);
        Assert.Equal(new[] { "own-01" }, result.MissingInJson);

        using var check = testDb.CreateContext();
        Assert.Equal(CatalogCount + 1, check.CatalogItems.Count());
    }

    [Fact]
    public void Sync_SavesItemsExactlyAsInCatalogJson()
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
            CatalogSeeder.Sync(db, CatalogPath);

        var expected = CatalogLoader.LoadFromFile(CatalogPath).OrderBy(i => i.Id).ToList();

        using var check = testDb.CreateContext();
        var actual = check.CatalogItems.OrderBy(i => i.Id).ToList();

        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Id, actual[i].Id);
            Assert.Equal(expected[i].Name, actual[i].Name);
            Assert.Equal(expected[i].Type, actual[i].Type);
            Assert.Equal(expected[i].ImageFile, actual[i].ImageFile);
            Assert.Equal(expected[i].MainColor, actual[i].MainColor);
            Assert.Equal(expected[i].SuitableLightings, actual[i].SuitableLightings);
            Assert.Equal(expected[i].LicenseSource, actual[i].LicenseSource);
        }
    }

    [Fact]
    public void Sync_MissingCatalogFile_ThrowsCatalogException_AndChangesNothing()
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
            Assert.Throws<CatalogException>(() =>
                CatalogSeeder.Sync(db, Path.Combine(RepoPaths.Root, "no-such-catalog.json")));

        using var check = testDb.CreateContext();
        Assert.Empty(check.CatalogItems);
    }
}

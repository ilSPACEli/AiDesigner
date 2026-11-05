using AiDesigner.Data;
using AiDesigner.Models;

namespace AiDesigner.Tests.Data;

// Заповнення таблиці CatalogItems зі справжнього catalog.json
public class CatalogSeederTests
{
    private static readonly string CatalogPath =
        Path.Combine(RepoPaths.WebRoot, CatalogLoader.DefaultRelativePath);

    private static readonly int CatalogCount = CatalogLoader.LoadFromFile(CatalogPath).Count;

    [Fact]
    public void Seed_EmptyDatabase_AddsWholeCatalog()
    {
        using var testDb = new TestDb();

        using (var db = testDb.CreateContext())
            Assert.Equal(CatalogCount, CatalogSeeder.Seed(db, CatalogPath));

        using var check = testDb.CreateContext();
        Assert.Equal(CatalogCount, check.CatalogItems.Count());
    }

    [Fact]
    public void Seed_SecondRun_DoesNotDuplicate()
    {
        using var testDb = new TestDb();

        using (var db = testDb.CreateContext())
            CatalogSeeder.Seed(db, CatalogPath);

        using (var db = testDb.CreateContext())
            Assert.Equal(0, CatalogSeeder.Seed(db, CatalogPath));

        using var check = testDb.CreateContext();
        Assert.Equal(CatalogCount, check.CatalogItems.Count());
    }

    [Fact]
    public void Seed_TableNotEmpty_KeepsExistingItemsAndAddsNothing()
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

        using (var db = testDb.CreateContext())
            Assert.Equal(0, CatalogSeeder.Seed(db, CatalogPath));

        using var check = testDb.CreateContext();
        Assert.Equal("own-01", Assert.Single(check.CatalogItems).Id);
    }

    [Fact]
    public void Seed_SavesItemsExactlyAsInCatalogJson()
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
            CatalogSeeder.Seed(db, CatalogPath);

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
    public void Insert_TableFilledConcurrently_ReturnsZeroWithoutDuplicates()
    {
        // Імітація одночасного старту двох екземплярів: перший уже заповнив таблицю,
        // другий пройшов перевірку Any() раніше й тепер вставляє ті самі предмети.
        using var testDb = new TestDb();
        using (var first = testDb.CreateContext())
            CatalogSeeder.Seed(first, CatalogPath);

        var items = CatalogLoader.LoadFromFile(CatalogPath);
        using (var second = testDb.CreateContext())
            Assert.Equal(0, CatalogSeeder.Insert(second, items));

        using var check = testDb.CreateContext();
        Assert.Equal(CatalogCount, check.CatalogItems.Count());
    }

    [Fact]
    public void Seed_MissingCatalogFile_ThrowsCatalogException()
    {
        using var testDb = new TestDb();
        using var db = testDb.CreateContext();

        Assert.Throws<CatalogException>(() => CatalogSeeder.Seed(db, Path.Combine(RepoPaths.Root, "no-such-catalog.json")));
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using AiDesigner.Data;
using AiDesigner.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AiDesigner.Tests.Data;

// AC-25 (#48): каталог у БД синхронізується з catalog.json під час кожного старту, в усіх середовищах.
// catalog.json — джерело правди: нове — додати, змінене — оновити, відсутнє в JSON — не видаляти.
public class CatalogSyncTests
{
    private static readonly string CatalogPath =
        Path.Combine(RepoPaths.WebRoot, CatalogLoader.DefaultRelativePath);

    private static IReadOnlyList<CatalogItem> RealCatalog() => CatalogLoader.LoadFromFile(CatalogPath);

    private static readonly int CatalogCount = RealCatalog().Count;

    // ---------- основний сценарій ----------

    [Fact]
    public void AC25_EmptyDb_Sync_AddsWholeCatalog()
    {
        using var testDb = new TestDb();

        CatalogSyncResult result;
        using (var db = testDb.CreateContext())
            result = CatalogSeeder.Sync(db, CatalogPath);

        Assert.Equal(CatalogCount, result.Added);
        Assert.Equal(0, result.Updated);
        Assert.Empty(result.MissingInJson);

        using var check = testDb.CreateContext();
        Assert.Equal(CatalogCount, check.CatalogItems.Count());
    }

    [Fact]
    public void AC25_SecondRun_NoChanges()
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
            CatalogSeeder.Sync(db, CatalogPath);

        CatalogSyncResult second;
        using (var db = testDb.CreateContext())
            second = CatalogSeeder.Sync(db, CatalogPath);

        Assert.Equal(0, second.Added);
        Assert.Equal(0, second.Updated);
        Assert.Empty(second.MissingInJson);
    }

    [Fact]
    public void AC25_NewItemInJson_IsAdded()
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
            CatalogSeeder.Sync(db, CatalogPath);

        var newItem = new CatalogItem
        {
            Id = "test-new-01",
            Name = "Нова лампа",
            Type = ItemType.LAMP,
            ImageFile = "images/catalog/test-new-01.png",
            MainColor = "#ABCDEF",
            SuitableLightings = new[] { LightingLevel.MODERATE },
            LicenseSource = "test, CC0, https://example.com"
        };
        using var catalog = new TempCatalog(RealCatalog().Append(newItem));

        CatalogSyncResult result;
        using (var db = testDb.CreateContext())
            result = CatalogSeeder.Sync(db, catalog.Path);

        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.Updated);

        using var check = testDb.CreateContext();
        Assert.Equal(CatalogCount + 1, check.CatalogItems.Count());
        Assert.Equal("Нова лампа", check.CatalogItems.Single(i => i.Id == "test-new-01").Name);
    }

    [Fact]
    public void AC25_ChangedColorAndLightingsInJson_AreUpdated()
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
            CatalogSeeder.Sync(db, CatalogPath);

        var items = RealCatalog().ToList();
        var original = items[0];
        IReadOnlyList<LightingLevel> newLightings = original.SuitableLightings.Count == 3
            ? new[] { LightingLevel.DARK }
            : new[] { LightingLevel.DARK, LightingLevel.MODERATE, LightingLevel.BRIGHT };
        items[0] = Copy(original, mainColor: "#123456", lightings: newLightings);
        using var catalog = new TempCatalog(items);

        CatalogSyncResult result;
        using (var db = testDb.CreateContext())
            result = CatalogSeeder.Sync(db, catalog.Path);

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Updated);

        using var check = testDb.CreateContext();
        var stored = check.CatalogItems.Single(i => i.Id == original.Id);
        Assert.Equal("#123456", stored.MainColor);
        Assert.Equal(newLightings, stored.SuitableLightings);
        Assert.Equal(CatalogCount, check.CatalogItems.Count());
    }

    [Fact]
    public void AC25_ItemRemovedFromJson_IsNotDeleted_AndReported()
    {
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
            CatalogSeeder.Sync(db, CatalogPath);

        // Предмет прибрали з JSON. На нього можуть посилатися збережені роботи (FK Restrict),
        // тому з БД він не видаляється — лише потрапляє в MissingInJson (попередження в лог).
        var items = RealCatalog().ToList();
        var removedId = items[0].Id;
        using var catalog = new TempCatalog(items.Skip(1));

        CatalogSyncResult result;
        using (var db = testDb.CreateContext())
            result = CatalogSeeder.Sync(db, catalog.Path);

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Updated);
        Assert.Equal(new[] { removedId }, result.MissingInJson);

        using var check = testDb.CreateContext();
        Assert.Equal(CatalogCount, check.CatalogItems.Count());
        Assert.True(check.CatalogItems.Any(i => i.Id == removedId));
    }

    // ---------- старт застосунку ----------

    [Fact]
    public void AC25_ProductionStartup_SyncsCatalog_WithoutMigrations()
    {
        // Production: applyMigrations = false. Схему тут створив EnsureCreated; якби старт
        // усе ж викликав Migrate(), він спробував би створити таблиці ще раз і впав би.
        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
            DatabaseInitializer.Initialize(db, CatalogPath, applyMigrations: false, new ListLogger());

        using var check = testDb.CreateContext();
        Assert.Equal(CatalogCount, check.CatalogItems.Count());
    }

    [Fact]
    public void AC25_BrokenCatalog_DoesNotStopStartup_AndIsLoggedAsError()
    {
        // Зламаний або відсутній catalog.json не зупиняє застосунок: галерея працює далі,
        // добір повертає «каталог недоступний» (UC-03 E1), а причина — у лозі.
        using var testDb = new TestDb();
        var logger = new ListLogger();

        using (var db = testDb.CreateContext())
            DatabaseInitializer.Initialize(db, Path.Combine(RepoPaths.Root, "no-such-catalog.json"),
                applyMigrations: false, logger);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void Sync_OtherInstanceFillsTableFirst_NoDuplicatesAndNoError()
    {
        // Два екземпляри стартують одночасно з порожньою спільною БД: наш прочитав порожню таблицю,
        // а інший устиг вставити каталог до нашого SaveChanges. Синхронізація має завершитися без помилки.
        using var testDb = new TestDb();
        var otherInstance = new OtherInstanceSyncsFirst(testDb);

        using (var db = testDb.CreateContext(otherInstance))
            CatalogSeeder.Sync(db, CatalogPath);

        Assert.True(otherInstance.Fired);
        using var check = testDb.CreateContext();
        Assert.Equal(CatalogCount, check.CatalogItems.Count());
    }

    [Fact]
    public void AC25_CatalogWithNullEntry_DoesNotStopStartup_AndIsLoggedAsError()
    {
        // null замість об'єкта в catalog.json — теж зламаний каталог: CatalogException, а не збій старту.
        using var testDb = new TestDb();
        var logger = new ListLogger();
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"catalog-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "[null]");
        try
        {
            using (var db = testDb.CreateContext())
                DatabaseInitializer.Initialize(db, path, applyMigrations: false, logger);
        }
        finally
        {
            File.Delete(path);
        }

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void Sync_OtherSaveError_IsNotRetried()
    {
        // Помилка запису, не пов'язана з одночасним стартом (у БД не з'явилося наших предметів),
        // має дійти до виклику одразу, без повторної спроби.
        using var testDb = new TestDb();
        var failing = new FailingSave();

        using (var db = testDb.CreateContext(failing))
            Assert.Throws<DbUpdateException>(() => CatalogSeeder.Sync(db, CatalogPath));

        Assert.Equal(1, failing.Calls);
        using var check = testDb.CreateContext();
        Assert.Empty(check.CatalogItems);
    }

    // ---------- допоміжне ----------

    private static CatalogItem Copy(CatalogItem item, string? mainColor = null,
        IReadOnlyList<LightingLevel>? lightings = null) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Type = item.Type,
        ImageFile = item.ImageFile,
        MainColor = mainColor ?? item.MainColor,
        SuitableLightings = lightings ?? item.SuitableLightings,
        LicenseSource = item.LicenseSource
    };

    // Тимчасовий catalog.json у форматі справжнього (camelCase, enum словами); видаляється в Dispose.
    private sealed class TempCatalog : IDisposable
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"catalog-{Guid.NewGuid():N}.json");

        public TempCatalog(IEnumerable<CatalogItem> items) =>
            File.WriteAllText(Path, JsonSerializer.Serialize(items.ToList(), Options));

        public void Dispose()
        {
            try { File.Delete(Path); }
            catch (IOException) { }
        }
    }

    // Перед першим збереженням нашого контексту «інший екземпляр» вставляє каталог через власний контекст.
    private sealed class OtherInstanceSyncsFirst(TestDb testDb) : SaveChangesInterceptor
    {
        public bool Fired { get; private set; }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (!Fired)
            {
                Fired = true;
                using var other = testDb.CreateContext();
                other.CatalogItems.AddRange(CatalogLoader.LoadFromFile(CatalogPath));
                other.SaveChanges();
            }
            return result;
        }
    }

    // Кожне збереження завершується помилкою запису (імітація, напр., порушення обмеження БД).
    private sealed class FailingSave : SaveChangesInterceptor
    {
        public int Calls { get; private set; }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Calls++;
            throw new DbUpdateException("Імітована помилка запису.");
        }
    }

    // Логер, що запам'ятовує записи, — щоб перевірити, що помилку записано.
    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}

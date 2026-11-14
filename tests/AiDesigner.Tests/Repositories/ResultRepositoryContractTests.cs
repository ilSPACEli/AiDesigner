using AiDesigner.Models;
using AiDesigner.Repositories;

namespace AiDesigner.Tests.Repositories;

// «Контракт сховища» (ЛР №5): одні й ті самі тести виконуються для КОЖНОЇ реалізації
// IResultRepository. Нащадок лише створює сховище (у пам'яті або EF на TestDb).
// AC-19 (збереження, нові спершу), AC-22 (видалення), AC-23 (ліміт, R7).
public abstract class ResultRepositoryContractTests
{
    /// <summary>Предмет каталогу, на який посилаються тестові роботи (для EF він має бути в БД через зовнішній ключ).</summary>
    protected const string ItemId = "sofa-01";

    protected abstract IResultRepository CreateRepository();

    // ---------- Допоміжне ----------

    // Час із кроком у хвилину від фіксованої точки: порядок у тестах очевидний за номером
    protected static DateTime T(int minutes) =>
        new DateTime(2026, 11, 14, 18, 0, 0, DateTimeKind.Utc).AddMinutes(minutes);

    protected static SavedResult NewResult(DateTime createdAt, byte marker = 1) => new()
    {
        CreatedAt = createdAt,
        CatalogItemId = ItemId,
        ImageData = new[] { marker, (byte)2 },
        ThumbnailData = new[] { marker, (byte)3 }
    };

    private static int[] Ids(IEnumerable<SavedResult> results) => results.Select(r => r.Id).ToArray();

    // ---------- AC-19: збереження, нові спершу ----------

    [Fact]
    public async Task AC19_Add_AssignsId_AndStoresAllFields()
    {
        var repo = CreateRepository();
        var input = NewResult(T(0), marker: 7);

        var saved = await repo.AddAsync(input);

        Assert.True(saved.Id > 0);
        var loaded = await repo.GetByIdAsync(saved.Id);
        Assert.NotNull(loaded);
        Assert.Equal(T(0), loaded!.CreatedAt);
        Assert.Equal(ItemId, loaded.CatalogItemId);
        Assert.Equal(input.ImageData, loaded.ImageData);
        Assert.Equal(input.ThumbnailData, loaded.ThumbnailData);
    }

    [Fact]
    public async Task Repository_Add_AssignsDifferentIds()
    {
        var repo = CreateRepository();

        var first = await repo.AddAsync(NewResult(T(0)));
        var second = await repo.AddAsync(NewResult(T(1)));

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task AC19_GetAll_ReturnsNewestFirst()
    {
        var repo = CreateRepository();
        var a = await repo.AddAsync(NewResult(T(0)));
        var c = await repo.AddAsync(NewResult(T(2)));
        var b = await repo.AddAsync(NewResult(T(1)));

        var all = await repo.GetAllAsync();

        Assert.Equal(new[] { c.Id, b.Id, a.Id }, Ids(all));
    }

    [Fact]
    public async Task Repository_GetAll_SameDate_NewerIdFirst()
    {
        var repo = CreateRepository();
        var first = await repo.AddAsync(NewResult(T(0)));
        var second = await repo.AddAsync(NewResult(T(0)));

        var all = await repo.GetAllAsync();

        Assert.Equal(new[] { second.Id, first.Id }, Ids(all));
    }

    [Fact]
    public async Task Repository_GetAll_Empty_ReturnsEmptyList()
    {
        var repo = CreateRepository();

        var all = await repo.GetAllAsync();

        Assert.Empty(all);
    }

    [Fact]
    public async Task Repository_GetById_Unknown_ReturnsNull()
    {
        var repo = CreateRepository();

        var loaded = await repo.GetByIdAsync(999);

        Assert.Null(loaded);
    }

    // ---------- AC-22: видалення ----------

    [Fact]
    public async Task AC22_Delete_Existing_RemovesItAndReturnsTrue()
    {
        var repo = CreateRepository();
        var saved = await repo.AddAsync(NewResult(T(0)));

        var deleted = await repo.DeleteAsync(saved.Id);

        Assert.True(deleted);
        Assert.Null(await repo.GetByIdAsync(saved.Id));
        Assert.Empty(await repo.GetAllAsync());
    }

    [Fact]
    public async Task AC22_Delete_Twice_SecondReturnsFalse()
    {
        var repo = CreateRepository();
        var saved = await repo.AddAsync(NewResult(T(0)));
        await repo.DeleteAsync(saved.Id);

        var deletedAgain = await repo.DeleteAsync(saved.Id);

        Assert.False(deletedAgain);
    }

    [Fact]
    public async Task AC22_Delete_OnlyRemovesRequestedOne()
    {
        var repo = CreateRepository();
        var keep = await repo.AddAsync(NewResult(T(0)));
        var remove = await repo.AddAsync(NewResult(T(1)));

        await repo.DeleteAsync(remove.Id);

        Assert.Equal(new[] { keep.Id }, Ids(await repo.GetAllAsync()));
    }

    // ---------- Кількість і найстаріші ----------

    [Fact]
    public async Task Repository_Count_ReflectsAddAndDelete()
    {
        var repo = CreateRepository();
        Assert.Equal(0, await repo.CountAsync());

        var first = await repo.AddAsync(NewResult(T(0)));
        await repo.AddAsync(NewResult(T(1)));
        Assert.Equal(2, await repo.CountAsync());

        await repo.DeleteAsync(first.Id);
        Assert.Equal(1, await repo.CountAsync());
    }

    [Fact]
    public async Task Repository_GetOldest_ReturnsOldestFirst_LimitedToCount()
    {
        var repo = CreateRepository();
        var c = await repo.AddAsync(NewResult(T(2)));
        var a = await repo.AddAsync(NewResult(T(0)));
        var b = await repo.AddAsync(NewResult(T(1)));

        var oldest = await repo.GetOldestAsync(2);

        Assert.Equal(new[] { a.Id, b.Id }, Ids(oldest));
        Assert.DoesNotContain(c.Id, Ids(oldest));
    }

    [Fact]
    public async Task Repository_GetOldest_MoreThanExists_ReturnsAll()
    {
        var repo = CreateRepository();
        var a = await repo.AddAsync(NewResult(T(0)));

        var oldest = await repo.GetOldestAsync(5);

        Assert.Equal(new[] { a.Id }, Ids(oldest));
    }

    // ---------- AC-23 / R7: витіснення разом із записом ----------

    [Fact]
    public async Task AC23_Add_WithEvict_RemovesOldestAndKeepsNew()
    {
        // Ліміт 3, у сховищі вже 3 роботи: зберігаємо четверту й просимо витіснити одну.
        var repo = CreateRepository();
        var a = await repo.AddAsync(NewResult(T(0)));
        var b = await repo.AddAsync(NewResult(T(1)));
        var c = await repo.AddAsync(NewResult(T(2)));

        var d = await repo.AddAsync(NewResult(T(3)), evictOldest: 1);

        Assert.Equal(3, await repo.CountAsync());
        Assert.Equal(new[] { d.Id, c.Id, b.Id }, Ids(await repo.GetAllAsync()));
        Assert.Null(await repo.GetByIdAsync(a.Id));
    }

    [Fact]
    public async Task AC23_Add_EvictMoreThanExists_RemovesAllOldButKeepsNew()
    {
        var repo = CreateRepository();
        await repo.AddAsync(NewResult(T(0)));
        await repo.AddAsync(NewResult(T(1)));

        var c = await repo.AddAsync(NewResult(T(2)), evictOldest: 5);

        Assert.Equal(new[] { c.Id }, Ids(await repo.GetAllAsync()));
    }

    [Fact]
    public async Task AC23_Add_NewOneIsKept_EvenIfItsDateIsOlderThanExisting()
    {
        // Найстаріші вибираються лише серед тих, що вже були, тож нова не може бути витіснена.
        var repo = CreateRepository();
        var a = await repo.AddAsync(NewResult(T(5)));
        var b = await repo.AddAsync(NewResult(T(6)));

        var fresh = await repo.AddAsync(NewResult(T(0)), evictOldest: 1);

        var all = await repo.GetAllAsync();
        Assert.Contains(fresh.Id, Ids(all));
        Assert.DoesNotContain(a.Id, Ids(all));
        Assert.Contains(b.Id, Ids(all));
    }

    [Fact]
    public async Task Repository_Add_EvictZero_KeepsEverything()
    {
        var repo = CreateRepository();
        await repo.AddAsync(NewResult(T(0)));

        await repo.AddAsync(NewResult(T(1)), evictOldest: 0);

        Assert.Equal(2, await repo.CountAsync());
    }

    [Fact]
    public async Task Repository_Add_NegativeEvict_Throws_AndChangesNothing()
    {
        var repo = CreateRepository();
        await repo.AddAsync(NewResult(T(0)));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repo.AddAsync(NewResult(T(1)), evictOldest: -1));

        Assert.Equal(1, await repo.CountAsync());
    }

    // ---------- Окремі стовпці: лише PNG / лише мініатюра ----------

    [Fact]
    public async Task Repository_GetImage_ReturnsStoredPng()
    {
        var repo = CreateRepository();
        var saved = await repo.AddAsync(NewResult(T(0), marker: 7));

        var image = await repo.GetImageAsync(saved.Id);

        Assert.Equal(new byte[] { 7, 2 }, image);
    }

    [Fact]
    public async Task Repository_GetThumbnail_ReturnsStoredThumbnail()
    {
        var repo = CreateRepository();
        var saved = await repo.AddAsync(NewResult(T(0), marker: 7));

        var thumbnail = await repo.GetThumbnailAsync(saved.Id);

        Assert.Equal(new byte[] { 7, 3 }, thumbnail);
    }

    [Fact]
    public async Task Repository_GetImageAndThumbnail_Unknown_ReturnNull()
    {
        var repo = CreateRepository();

        Assert.Null(await repo.GetImageAsync(999));
        Assert.Null(await repo.GetThumbnailAsync(999));
    }

    [Fact]
    public async Task AC22_GetImageAndThumbnail_AfterDelete_ReturnNull()
    {
        var repo = CreateRepository();
        var saved = await repo.AddAsync(NewResult(T(0)));
        await repo.DeleteAsync(saved.Id);

        Assert.Null(await repo.GetImageAsync(saved.Id));
        Assert.Null(await repo.GetThumbnailAsync(saved.Id));
    }
}

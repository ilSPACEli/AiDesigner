using AiDesigner.Models;
using AiDesigner.Repositories;

namespace AiDesigner.Tests.Repositories;

public abstract class ResultRepositoryContractTests
{
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

    // ---------- AC-36: фільтр за стилем (S3, FR-22) ----------

    protected static SavedResult Styled(DateTime createdAt, RoomStyle? style, AnalysisMode mode = AnalysisMode.CLOUD_AI) => new()
    {
        CreatedAt = createdAt,
        CatalogItemId = ItemId,
        ImageData = new byte[] { 1, 2 },
        ThumbnailData = new byte[] { 1, 3 },
        Analysis = new ResultAnalysis
        {
            Mode = mode,
            Lighting = LightingLevel.MODERATE,
            Colors = new[] { new DominantColor { Hex = "#D8C3A5", Hue = 36, Share = 100m } },
            Style = style,
            Confidence = style is null ? null : 0.82m
        }
    };

    [Fact]
    public async Task AC36_GetAll_ByStyle_OnlyThatStyle_NewestFirst()
    {
        var repo = CreateRepository();
        var loftOld = await repo.AddAsync(Styled(T(0), RoomStyle.LOFT));
        await repo.AddAsync(Styled(T(1), RoomStyle.MINIMALISM));
        var loftNew = await repo.AddAsync(Styled(T(2), RoomStyle.LOFT));
        await repo.AddAsync(NewResult(T(3)));                           // без аналізу (до S2)
        await repo.AddAsync(Styled(T(4), null));                        // ШІ, стиль не визначено
        await repo.AddAsync(Styled(T(5), null, AnalysisMode.BASIC));    // без ШІ

        var loft = await repo.GetAllAsync(RoomStyle.LOFT);

        Assert.Equal(new[] { loftNew.Id, loftOld.Id }, Ids(loft));
        Assert.All(loft, r => Assert.Equal(RoomStyle.LOFT, r.Analysis?.Style));
    }

    [Fact]
    public async Task AC36_GetAll_WithoutStyle_ReturnsAllWorks_NewestFirst()
    {
        var repo = CreateRepository();
        var loft = await repo.AddAsync(Styled(T(0), RoomStyle.LOFT));
        var noAnalysis = await repo.AddAsync(NewResult(T(1)));
        var notDetermined = await repo.AddAsync(Styled(T(2), null));

        var all = await repo.GetAllAsync();

        Assert.Equal(new[] { notDetermined.Id, noAnalysis.Id, loft.Id }, Ids(all));
    }

    [Fact]
    public async Task AC37_GetAll_StyleWithoutWorks_ReturnsEmpty()
    {
        var repo = CreateRepository();
        await repo.AddAsync(Styled(T(0), RoomStyle.LOFT));
        await repo.AddAsync(NewResult(T(1)));

        Assert.Empty(await repo.GetAllAsync(RoomStyle.BOHO));
    }
}

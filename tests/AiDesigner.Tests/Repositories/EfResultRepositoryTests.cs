using AiDesigner.Data;
using AiDesigner.Models;
using AiDesigner.Repositories;
using AiDesigner.Tests.Data;
using Microsoft.EntityFrameworkCore;

namespace AiDesigner.Tests.Repositories;

// EfResultRepository на TestDb (SQLite у пам'яті): спільні контрактні тести + перевірки, властиві БД.
public sealed class EfResultRepositoryTests : ResultRepositoryContractTests, IDisposable
{
    private const string ItemName = "Сірий диван";

    private readonly TestDb _testDb = new();
    private readonly List<AppDbContext> _contexts = new();

    public EfResultRepositoryTests()
    {
        // Зовнішній ключ SavedResults → CatalogItems: предмет має існувати в БД.
        using var db = _testDb.CreateContext();
        db.CatalogItems.Add(new CatalogItem
        {
            Id = ItemId,
            Name = ItemName,
            Type = ItemType.SOFA,
            ImageFile = "images/catalog/sofa-01.png",
            MainColor = "#82817A",
            SuitableLightings = new[] { LightingLevel.MODERATE },
            LicenseSource = "test"
        });
        db.SaveChanges();
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
            context.Dispose();
        _testDb.Dispose();
    }

    // Кожне сховище — з окремим контекстом (як окремий HTTP-запит), але на спільній БД тесту.
    protected override IResultRepository CreateRepository()
    {
        var context = _testDb.CreateContext();
        _contexts.Add(context);
        return new EfResultRepository(context);
    }

    // ---------- Транзакція (R7): відкат разом із витісненням ----------

    [Fact]
    public async Task AC23_Add_WhenInsertFails_OldWorksAreNotEvicted()
    {
        // Вставка нової провалиться (предмета каталогу немає), а витіснення до неї вже виконано.
        // Одна транзакція має скасувати й видалення: користувач не втрачає стару роботу даремно.
        var repo = CreateRepository();
        var old = await repo.AddAsync(NewResult(T(0)));
        var broken = new SavedResult
        {
            CreatedAt = T(1),
            CatalogItemId = "no-such-item",
            ImageData = new byte[] { 1 },
            ThumbnailData = new byte[] { 1 }
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => repo.AddAsync(broken, evictOldest: 1));

        var check = CreateRepository();   // свіжий контекст: дивимося, що реально в БД
        Assert.Equal(1, await check.CountAsync());
        Assert.NotNull(await check.GetByIdAsync(old.Id));
    }

    [Fact]
    public async Task Ef_Add_AfterFailedAdd_SameRepositoryStillWorks()
    {
        // Невдала сутність не має лишитися в контексті й ламати наступне збереження.
        var repo = CreateRepository();
        var broken = new SavedResult
        {
            CreatedAt = T(0),
            CatalogItemId = "no-such-item",
            ImageData = new byte[] { 1 },
            ThumbnailData = new byte[] { 1 }
        };
        await Assert.ThrowsAsync<DbUpdateException>(() => repo.AddAsync(broken));

        var saved = await repo.AddAsync(NewResult(T(1)));

        Assert.Equal(new[] { saved.Id }, (await repo.GetAllAsync()).Select(r => r.Id));
    }

    // ---------- Збереження в БД ----------

    [Fact]
    public async Task Ef_Add_IsVisibleFromAnotherContext()
    {
        // Робота записана в БД, а не лише в пам'яті контексту (у застосунку так буде між запитами).
        var writer = CreateRepository();
        var saved = await writer.AddAsync(NewResult(T(0), marker: 9));

        var reader = CreateRepository();
        var loaded = await reader.GetByIdAsync(saved.Id);

        Assert.NotNull(loaded);
        Assert.Equal(new byte[] { 9, 2 }, loaded!.ImageData);
    }

    [Fact]
    public async Task Ef_GetById_CreatedAtIsUtc()
    {
        var repo = CreateRepository();
        var saved = await repo.AddAsync(NewResult(T(0)));

        var loaded = await CreateRepository().GetByIdAsync(saved.Id);

        Assert.Equal(DateTimeKind.Utc, loaded!.CreatedAt.Kind);
    }

    // ---------- Список: без важких стовпців, але з назвою предмета ----------

    [Fact]
    public async Task Ef_GetAll_HasItemName_ButNoImageBytes()
    {
        var repo = CreateRepository();
        await repo.AddAsync(NewResult(T(0)));

        var all = await CreateRepository().GetAllAsync();

        var item = Assert.Single(all);
        Assert.Equal(ItemName, item.CatalogItem?.Name);
        Assert.Empty(item.ImageData);
        Assert.Empty(item.ThumbnailData);
    }

    [Fact]
    public async Task Ef_GetById_HasItemName_AndImageBytes()
    {
        var repo = CreateRepository();
        var saved = await repo.AddAsync(NewResult(T(0), marker: 5));

        var loaded = await CreateRepository().GetByIdAsync(saved.Id);

        Assert.Equal(ItemName, loaded!.CatalogItem?.Name);
        Assert.Equal(new byte[] { 5, 2 }, loaded.ImageData);
        Assert.Equal(new byte[] { 5, 3 }, loaded.ThumbnailData);
    }
}
using AiDesigner.Models;
using AiDesigner.Repositories;
using AiDesigner.Services.Results;
using AiDesigner.Services.Stubs;
using AiDesigner.Tests.Analysis;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Services;

// ResultService на сховищі в пам'яті: AC-19 (збереження), AC-21 (роздільність), AC-22 (видалення),
// AC-23 (R7 у зв'язці з репозиторієм), AC-24 (розмір, некоректні дані).
public class ResultServiceTests
{
    // Предмет із StubCatalog: Id "stub-01", назва «Сірий диван»
    private const string ItemId = "stub-01";
    private const string ItemName = "Сірий диван";

    private static byte[] Png(int width = 40, int height = 30) => TestImages.Solid(width, height, new Rgb24(200, 100, 50));

    private static ResultService CreateService(IResultRepository? repository = null, GalleryOptions? options = null) =>
        new(repository ?? new InMemoryResultRepository(),
            new StubCatalog(),
            options ?? new GalleryOptions(),
            NullLogger<ResultService>.Instance);

    private static async Task<SavedResult> SaveOkAsync(ResultService service)
    {
        var outcome = await service.SaveAsync(Png(), ItemId);
        Assert.Equal(SaveStatus.Saved, outcome.Status);
        return outcome.Result!;
    }

    // Кладе роботи просто в сховище, мимо сервісу (наприклад, імітує галерею, що вже переповнена)
    private static async Task SeedAsync(IResultRepository repository, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await repository.AddAsync(new SavedResult
            {
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i),
                CatalogItemId = ItemId,
                ImageData = new byte[] { 1 },
                ThumbnailData = new byte[] { 1 }
            });
        }
    }

    // ---------- AC-19: збереження ----------

    [Fact]
    public async Task AC19_Save_ValidPng_ReturnsSavedWithIdAndItemName()
    {
        var repository = new InMemoryResultRepository();
        var service = CreateService(repository);

        var outcome = await service.SaveAsync(Png(), ItemId);

        Assert.Equal(SaveStatus.Saved, outcome.Status);
        Assert.True(outcome.IsSaved);
        Assert.True(outcome.Result!.Id > 0);
        Assert.Equal(ItemId, outcome.Result.CatalogItemId);
        Assert.Equal(ItemName, outcome.Result.CatalogItem!.Name);
        Assert.Equal(DateTimeKind.Utc, outcome.Result.CreatedAt.Kind);
        Assert.Equal(1, await repository.CountAsync());
    }

    [Fact]
    public async Task AC19_Save_StoresPngWithSameResolution()
    {
        // AC-21: «Завантажити» віддає PNG у роздільності оригіналу, тож стиснення не змінює розмірів.
        var service = CreateService();

        var saved = await SaveOkAsync(service);

        var info = Image.Identify(new MemoryStream(saved.ImageData));
        Assert.IsType<PngFormat>(info.Metadata.DecodedImageFormat);
        Assert.Equal(40, info.Width);
        Assert.Equal(30, info.Height);
    }

    [Fact]
    public async Task AC19_Save_StoresJpegThumbnail_ShrunkToLimit()
    {
        var service = CreateService();

        var outcome = await service.SaveAsync(Png(1000, 500), ItemId);

        var info = Image.Identify(new MemoryStream(outcome.Result!.ThumbnailData));
        Assert.IsType<JpegFormat>(info.Metadata.DecodedImageFormat);
        Assert.Equal(ResultService.ThumbnailMaxSide, info.Width);   // 1000×500 → 320×160
        Assert.Equal(160, info.Height);
    }

    [Fact]
    public async Task AC19_Save_SmallImage_ThumbnailIsNotEnlarged()
    {
        var service = CreateService();

        var saved = await SaveOkAsync(service);

        var info = Image.Identify(new MemoryStream(saved.ThumbnailData));
        Assert.Equal(40, info.Width);
        Assert.Equal(30, info.Height);
    }

    [Fact]
    public async Task AC19_GetAll_ReturnsLatestSavedFirst()
    {
        var service = CreateService();
        var first = await SaveOkAsync(service);
        var second = await SaveOkAsync(service);

        var all = await service.GetAllAsync();

        Assert.Equal(new[] { second.Id, first.Id }, all.Select(r => r.Id));
    }

    // ---------- AC-22: видалення ----------

    [Fact]
    public async Task AC22_Delete_RemovesWork_AndSecondDeleteReportsNotFound()
    {
        var service = CreateService();
        var saved = await SaveOkAsync(service);

        var first = await service.DeleteAsync(saved.Id);
        var second = await service.DeleteAsync(saved.Id);

        Assert.True(first);
        Assert.False(second);
        Assert.Null(await service.GetByIdAsync(saved.Id));
    }

    // ---------- AC-23: ліміт галереї (R7) ----------

    [Fact]
    public async Task AC23_Save_OverLimit_EvictsOldestAndKeepsNew()
    {
        // Ліміт N = 3: після збереження четвертої найстаршої немає, нова є, у галереї рівно N.
        var repository = new InMemoryResultRepository();
        var service = CreateService(repository, new GalleryOptions { MaxItems = 3 });
        var first = await SaveOkAsync(service);
        await SaveOkAsync(service);
        await SaveOkAsync(service);

        var fourth = await SaveOkAsync(service);

        Assert.Equal(3, await repository.CountAsync());
        Assert.Null(await service.GetByIdAsync(first.Id));
        Assert.NotNull(await service.GetByIdAsync(fourth.Id));
    }

    [Fact]
    public async Task AC23_Save_BelowLimit_EvictsNothing()
    {
        var repository = new InMemoryResultRepository();
        var service = CreateService(repository, new GalleryOptions { MaxItems = 3 });

        await SaveOkAsync(service);
        await SaveOkAsync(service);

        Assert.Equal(2, await repository.CountAsync());
    }

    [Fact]
    public async Task AC23_Save_LimitOne_NewWorkReplacesPrevious()
    {
        var repository = new InMemoryResultRepository();
        var service = CreateService(repository, new GalleryOptions { MaxItems = 1 });
        var first = await SaveOkAsync(service);

        var second = await SaveOkAsync(service);

        Assert.Equal(1, await repository.CountAsync());
        Assert.Null(await service.GetByIdAsync(first.Id));
        Assert.NotNull(await service.GetByIdAsync(second.Id));
    }

    [Fact]
    public async Task AC23_Save_GalleryAlreadyAboveLimit_TrimsDownToLimit()
    {
        // Ліміт зменшили (або два збереження зійшлися одночасно): у галереї 5 робіт при ліміті 3.
        // Наступне збереження витісняє достатньо, щоб лишилося рівно N, і нова серед них.
        var repository = new InMemoryResultRepository();
        await SeedAsync(repository, 5);
        var service = CreateService(repository, new GalleryOptions { MaxItems = 3 });

        var saved = await SaveOkAsync(service);

        Assert.Equal(3, await repository.CountAsync());
        Assert.NotNull(await service.GetByIdAsync(saved.Id));
    }

    // ---------- AC-24: розмір ----------

    [Fact]
    public async Task AC24_Save_TooLarge_ReturnsTooLarge_AndStoresNothing()
    {
        var png = Png();
        var repository = new InMemoryResultRepository();
        var service = CreateService(repository, new GalleryOptions { MaxImageBytes = png.Length - 1 });

        var outcome = await service.SaveAsync(png, ItemId);

        Assert.Equal(SaveStatus.TooLarge, outcome.Status);
        Assert.Contains("МБ", outcome.Message);
        Assert.Equal(0, await repository.CountAsync());
    }

    [Fact]
    public async Task AC24_Save_ExactlyMaxSize_IsAccepted()
    {
        var png = Png();
        var service = CreateService(options: new GalleryOptions { MaxImageBytes = png.Length });

        var outcome = await service.SaveAsync(png, ItemId);

        Assert.Equal(SaveStatus.Saved, outcome.Status);
    }

    [Fact]
    public async Task AC24_Save_OverDefaultLimit_MessageMatchesApiContract()
    {
        // Текст із docs/api.md: «Максимальний розмір — 5 МБ.»; тут розмір вмісту не важливий, лише довжина.
        var service = CreateService();

        var outcome = await service.SaveAsync(new byte[5 * 1024 * 1024 + 1], ItemId);

        Assert.Equal(SaveStatus.TooLarge, outcome.Status);
        Assert.Equal("Максимальний розмір — 5 МБ.", outcome.Message);
    }

    // ---------- 400: вміст і предмет ----------

    [Fact]
    public async Task Save_Jpeg_IsBadRequest_AndStoresNothing()
    {
        // Перевіряється ВМІСТ, а не розширення чи ContentType: JPEG не приймається.
        var repository = new InMemoryResultRepository();
        var service = CreateService(repository);

        var outcome = await service.SaveAsync(TestImages.RoomJpeg(64, 48), ItemId);

        Assert.Equal(SaveStatus.BadRequest, outcome.Status);
        Assert.Equal(ResultService.NotPngMessage, outcome.Message);
        Assert.Equal(0, await repository.CountAsync());
    }

    [Fact]
    public async Task Save_RandomBytes_IsBadRequest()
    {
        var outcome = await CreateService().SaveAsync(new byte[] { 1, 2, 3, 4, 5 }, ItemId);

        Assert.Equal(SaveStatus.BadRequest, outcome.Status);
    }

    [Fact]
    public async Task Save_EmptyData_IsBadRequest()
    {
        var outcome = await CreateService().SaveAsync(Array.Empty<byte>(), ItemId);

        Assert.Equal(SaveStatus.BadRequest, outcome.Status);
    }

    [Fact]
    public async Task Save_TruncatedPng_IsBadRequest()
    {
        // Сигнатура PNG є, а решта обрізана: лише сигнатури замало.
        var truncated = Png().Take(30).ToArray();

        var outcome = await CreateService().SaveAsync(truncated, ItemId);

        Assert.Equal(SaveStatus.BadRequest, outcome.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Save_NoItem_IsBadRequest(string? itemId)
    {
        var repository = new InMemoryResultRepository();
        var service = CreateService(repository);

        var outcome = await service.SaveAsync(Png(), itemId);

        Assert.Equal(SaveStatus.BadRequest, outcome.Status);
        Assert.Equal(ResultService.NoItemMessage, outcome.Message);
        Assert.Equal(0, await repository.CountAsync());
    }

    [Fact]
    public async Task Save_UnknownItem_IsBadRequest_AndStoresNothing()
    {
        var repository = new InMemoryResultRepository();
        var service = CreateService(repository);

        var outcome = await service.SaveAsync(Png(), "no-such-item");

        Assert.Equal(SaveStatus.BadRequest, outcome.Status);
        Assert.Equal(ResultService.UnknownItemMessage, outcome.Message);
        Assert.Equal(0, await repository.CountAsync());
    }

    // ---------- 500: збій сховища — це результат, а не виняток ----------

    [Fact]
    public async Task Save_RepositoryFails_ReturnsFailed_WithoutThrowing()
    {
        var service = CreateService(new FailingRepository());

        var outcome = await service.SaveAsync(Png(), ItemId);

        Assert.Equal(SaveStatus.Failed, outcome.Status);
        Assert.Equal(ResultService.SaveFailedMessage, outcome.Message);
        Assert.Null(outcome.Result);
    }

    [Fact]
    public async Task Save_Cancelled_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = CreateService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SaveAsync(Png(), ItemId, cts.Token));
    }

    // ---------- Налаштування ----------

    [Fact]
    public void Constructor_NonPositiveMaxItems_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateService(options: new GalleryOptions { MaxItems = 0 }));
    }

    [Fact]
    public void Constructor_NonPositiveMaxImageBytes_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateService(options: new GalleryOptions { MaxImageBytes = 0 }));
    }

    // Сховище, у якому запис завжди падає, як обрив зв'язку з БД
    private sealed class FailingRepository : IResultRepository
    {
        public Task<SavedResult> AddAsync(SavedResult result, int evictOldest = 0, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("БД недоступна");

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<IReadOnlyList<SavedResult>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SavedResult?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SavedResult>> GetOldestAsync(int count, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
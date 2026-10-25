using System.Text;
using AiDesigner.Models;
using AiDesigner.Services;
using AiDesigner.Services.Stubs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests;

/// <summary>
/// Тести UC-01 на рівні DesignService: перевірка за вмістом файла,
/// порядок «перевірка → полотно → сесія», скидання результатів (FR-13).
/// </summary>
public class PhotoUploadTests
{
    // ---------- Тестові файли генеруються в пам'яті ----------

    private static byte[] Png(int width, int height) => Save(width, height, (img, s) => img.SaveAsPng(s));

    private static byte[] Jpeg(int width, int height) => Save(width, height, (img, s) => img.SaveAsJpeg(s));

    private static byte[] Webp(int width, int height) => Save(width, height, (img, s) => img.SaveAsWebp(s));

    private static byte[] Save(int width, int height, Action<Image, Stream> save)
    {
        using var image = new Image<Rgb24>(width, height, new Rgb24(180, 120, 90));
        using var stream = new MemoryStream();
        save(image, stream);
        return stream.ToArray();
    }

    private static DesignService CreateService(IPhotoAnalyzer? analyzer = null) =>
        new(analyzer ?? new StubPhotoAnalyzer(), new StubCatalog(), new StubResultStorage());

    // Підробка полотна: запам'ятовує, чи його викликали.
    private sealed class FakeCanvas
    {
        public int Calls { get; private set; }

        public Task Show(byte[] content)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    // ---------- AC-01: коректне фото приймається ----------

    [Fact]
    public async Task AC01_ValidJpeg_IsAccepted_PhotoLoaded_SizeFilled()
    {
        var service = CreateService();
        var canvas = new FakeCanvas();

        var result = await service.LoadPhotoAsync("room.jpg", Jpeg(800, 600), canvas.Show);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionState.PHOTO_LOADED, service.Session.State);
        Assert.Equal(1, canvas.Calls);
        var photo = service.Session.Photo!;
        Assert.Equal("jpeg", photo.Format);
        Assert.Equal(800, photo.Width);
        Assert.Equal(600, photo.Height);
    }

    [Fact]
    public async Task AC01_ValidPng_IsAccepted()
    {
        var service = CreateService();

        var result = await service.LoadPhotoAsync("room.png", Png(480, 640), new FakeCanvas().Show);

        Assert.True(result.IsSuccess);
        Assert.Equal("png", service.Session.Photo!.Format);
        Assert.Equal(SessionState.PHOTO_LOADED, service.Session.State);
    }

    [Fact]
    public async Task AC01_FormatIsTakenFromContent_NotFromFileName()
    {
        // PNG із розширенням .jpg — формат визначається за вмістом.
        var service = CreateService();

        await service.LoadPhotoAsync("room.jpg", Png(800, 600), new FakeCanvas().Show);

        Assert.Equal("png", service.Session.Photo!.Format);
    }

    // ---------- AC-02: некоректне фото не приймається ----------

    [Fact]
    public async Task AC02_WebpRenamedToJpg_IsRejected_WithFormatReason()
    {
        var service = CreateService();
        var canvas = new FakeCanvas();

        var result = await service.LoadPhotoAsync("room.jpg", Webp(800, 600), canvas.Show);

        Assert.False(result.IsSuccess);
        Assert.Equal(Photo.WrongFormatMessage, result.Message);
        Assert.Equal(0, canvas.Calls);                              // на полотні нічого
        Assert.Equal(SessionState.EMPTY, service.Session.State);    // сесія не змінилась
        Assert.Null(service.Session.Photo);
    }

    [Fact]
    public async Task AC02_NotAnImage_IsRejected_WithFormatReason()
    {
        var service = CreateService();
        var canvas = new FakeCanvas();
        var text = Encoding.UTF8.GetBytes("це не фото, а текстовий файл");

        var result = await service.LoadPhotoAsync("room.png", text, canvas.Show);

        Assert.False(result.IsSuccess);
        Assert.Equal(Photo.WrongFormatMessage, result.Message);
        Assert.Equal(0, canvas.Calls);
        Assert.Equal(SessionState.EMPTY, service.Session.State);
    }

    [Fact]
    public void AC02_FileOver10Mb_IsRejectedBeforeReading()
    {
        var service = CreateService();

        var result = service.CheckFileSize(Photo.MaxSizeBytes + 1L);

        Assert.False(result.IsSuccess);
        Assert.Equal(Photo.TooLargeMessage, result.Message);
        Assert.Equal(SessionState.EMPTY, service.Session.State);
    }

    [Fact]
    public void AC02_FileOfExactly10Mb_PassesSizeCheck()
    {
        Assert.True(CreateService().CheckFileSize(Photo.MaxSizeBytes).IsSuccess);
    }

    [Fact]
    public async Task AC02_FileOver10Mb_PassedToLoad_IsRejected()
    {
        // Навіть якщо сторінка пропустила перевірку, сервіс не прийме завеликий файл.
        var service = CreateService();
        var canvas = new FakeCanvas();
        var big = new byte[Photo.MaxSizeBytes + 1];

        var result = await service.LoadPhotoAsync("big.jpg", big, canvas.Show);

        Assert.False(result.IsSuccess);
        Assert.Equal(Photo.TooLargeMessage, result.Message);
        Assert.Equal(0, canvas.Calls);
        Assert.Equal(SessionState.EMPTY, service.Session.State);
    }

    [Fact]
    public async Task AC02_SmallerSideUnder480_IsRejected_WithSmallReason()
    {
        var service = CreateService();
        var canvas = new FakeCanvas();

        var result = await service.LoadPhotoAsync("small.png", Png(640, 479), canvas.Show);

        Assert.False(result.IsSuccess);
        Assert.Equal(Photo.TooSmallMessage, result.Message);
        Assert.Equal(0, canvas.Calls);
        Assert.Equal(SessionState.EMPTY, service.Session.State);
    }

    [Fact]
    public async Task AC02_InvalidPhoto_KeepsPreviousPhoto()
    {
        // A4: відмова не чіпає вже завантажене фото.
        var service = CreateService();
        await service.LoadPhotoAsync("room.jpg", Jpeg(800, 600), new FakeCanvas().Show);
        var before = service.Session.Photo;

        var result = await service.LoadPhotoAsync("small.png", Png(300, 300), new FakeCanvas().Show);

        Assert.False(result.IsSuccess);
        Assert.Same(before, service.Session.Photo);
        Assert.Equal(SessionState.PHOTO_LOADED, service.Session.State);
    }

    [Fact]
    public async Task AC02_CanvasFails_SessionUnchanged()
    {
        var service = CreateService();

        var result = await service.LoadPhotoAsync("room.jpg", Jpeg(800, 600),
            _ => throw new InvalidOperationException("браузер не показав фото"));

        Assert.False(result.IsSuccess);
        Assert.Equal(DesignService.PhotoShowFailedMessage, result.Message);
        Assert.Equal(SessionState.EMPTY, service.Session.State);
    }

    // ---------- AC-13: нове фото скидає попередні результати ----------

    [Fact]
    public async Task AC13_NewPhotoAfterAnalysis_ClearsResults()
    {
        var service = CreateService();
        await service.LoadPhotoAsync("room.jpg", Jpeg(800, 600), new FakeCanvas().Show);
        await service.AnalyzeAsync();
        service.Suggest();
        Assert.NotNull(service.Session.Analysis);

        var result = await service.LoadPhotoAsync("room2.png", Png(1024, 768), new FakeCanvas().Show);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionState.PHOTO_LOADED, service.Session.State);
        Assert.Null(service.Session.Analysis);
        Assert.Empty(service.Session.Suggestions);
        Assert.Null(service.Session.Placement);
        Assert.Equal(1024, service.Session.Photo!.Width);
    }

    [Fact]
    public async Task AC13_DuringAnalysis_PhotoCannotBeReplaced_CanvasNotTouched()
    {
        var gate = new TaskCompletionSource<AnalysisOutcome>();
        var service = CreateService(new SlowAnalyzer(gate.Task));
        await service.LoadPhotoAsync("room.jpg", Jpeg(800, 600), new FakeCanvas().Show);
        var analysis = service.AnalyzeAsync();       // аналіз ще триває
        var canvas = new FakeCanvas();

        var result = await service.LoadPhotoAsync("room2.jpg", Jpeg(800, 600), canvas.Show);

        Assert.False(result.IsSuccess);
        Assert.Equal(DesignService.PhotoReplaceBlockedMessage, result.Message);
        Assert.Equal(0, canvas.Calls);

        gate.SetResult(AnalysisOutcome.Error("стоп"));
        await analysis;
    }

    // ---------- Одночасні завантаження (рев'ю PR) ----------

    // Полотно, яке «показує» фото лише тоді, коли тест відпустить gate.
    private sealed class SlowCanvas
    {
        private readonly TaskCompletionSource _gate = new();

        public Task Show(byte[] content) => _gate.Task;

        public void Release() => _gate.SetResult();
    }

    [Fact]
    public async Task AC01_SecondLoadWhileFirstInProgress_IsRejected()
    {
        var service = CreateService();
        var slow = new SlowCanvas();
        var first = service.LoadPhotoAsync("a.jpg", Jpeg(800, 600), slow.Show);   // чекає полотно
        Assert.True(service.IsLoadingPhoto);
        var canvas = new FakeCanvas();

        var second = await service.LoadPhotoAsync("b.png", Png(1024, 768), canvas.Show);

        Assert.False(second.IsSuccess);
        Assert.Equal(DesignService.PhotoLoadInProgressMessage, second.Message);
        Assert.Equal(0, canvas.Calls);

        slow.Release();
        Assert.True((await first).IsSuccess);
        Assert.Equal("a.jpg", service.Session.Photo!.FileName);   // у сесії перше фото
        Assert.False(service.IsLoadingPhoto);
    }

    [Fact]
    public async Task AC01_AnalysisWhilePhotoLoading_IsRejected_SessionUnchanged()
    {
        var service = CreateService();
        await service.LoadPhotoAsync("a.jpg", Jpeg(800, 600), new FakeCanvas().Show);
        var slow = new SlowCanvas();
        var loading = service.LoadPhotoAsync("b.jpg", Jpeg(1024, 768), slow.Show);

        var outcome = await service.AnalyzeAsync();

        Assert.Equal(AnalysisStatus.Error, outcome.Status);
        Assert.Equal(DesignService.PhotoLoadInProgressMessage, outcome.Message);
        Assert.False(service.Session.IsAnalyzing);
        Assert.Equal(SessionState.PHOTO_LOADED, service.Session.State);

        slow.Release();
        Assert.True((await loading).IsSuccess);
        Assert.Equal("b.jpg", service.Session.Photo!.FileName);
    }

    [Fact]
    public async Task AC02_CanvasFails_LoadingFlagIsReset()
    {
        var service = CreateService();

        await service.LoadPhotoAsync("a.jpg", Jpeg(800, 600), _ => throw new InvalidOperationException());
        var next = await service.LoadPhotoAsync("b.jpg", Jpeg(800, 600), new FakeCanvas().Show);

        Assert.False(service.IsLoadingPhoto);
        Assert.True(next.IsSuccess);
    }

    [Fact]
    public async Task AC13_LoadPhotoDuringAnalysis_ReturnsFail_DoesNotThrow()
    {
        var gate = new TaskCompletionSource<AnalysisOutcome>();
        var service = CreateService(new SlowAnalyzer(gate.Task));
        service.LoadPhoto(new Photo { FileName = "a.jpg", Format = "jpeg", Width = 800, Height = 600, SizeBytes = 10 });
        var analysis = service.AnalyzeAsync();

        var result = service.LoadPhoto(new Photo { FileName = "b.jpg", Format = "jpeg", Width = 800, Height = 600, SizeBytes = 10 });

        Assert.False(result.IsSuccess);
        Assert.Equal(DesignService.PhotoReplaceBlockedMessage, result.Message);
        Assert.Equal("a.jpg", service.Session.Photo!.FileName);

        gate.SetResult(AnalysisOutcome.Error("стоп"));
        await analysis;
    }

    private sealed class SlowAnalyzer(Task<AnalysisOutcome> result) : IPhotoAnalyzer
    {
        public Task<AnalysisOutcome> AnalyzeAsync(Photo photo, CancellationToken cancellationToken = default) => result;
    }
}

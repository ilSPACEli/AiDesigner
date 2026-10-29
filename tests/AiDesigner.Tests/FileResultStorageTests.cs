using AiDesigner.Models;
using AiDesigner.Services;
using AiDesigner.Services.Stubs;
using AiDesigner.Tests.Analysis;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests;

/// <summary>FileResultStorage: AC-11 (файл PNG) і E1 (помилка збереження, файлу немає).</summary>
public sealed class FileResultStorageTests : IDisposable
{
    // Кожен тест працює у власній тимчасовій папці, яку знищує Dispose.
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aidesigner-tests-" + Guid.NewGuid().ToString("N"));

    private string OutputDir => Path.Combine(_root, "output");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static byte[] CreatePng(int width = 40, int height = 30) =>
        TestImages.Solid(width, height, new Rgb24(200, 100, 50));

    private string[] FilesInOutput() =>
        Directory.Exists(OutputDir) ? Directory.GetFiles(OutputDir) : Array.Empty<string>();

    // ---------- AC-11 ----------

    [Fact]
    public async Task AC11_Save_CreatesFile_WithPngSignature()
    {
        var storage = new FileResultStorage(OutputDir);

        var result = await storage.SaveAsync(CreatePng());

        var path = Path.Combine(OutputDir, result.FileName);
        Assert.True(File.Exists(path));
        var head = File.ReadAllBytes(path).Take(4).ToArray();
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, head);
    }

    [Fact]
    public async Task AC11_Save_FileOpensInImageSharp_WithSameSize()
    {
        var storage = new FileResultStorage(OutputDir);

        var result = await storage.SaveAsync(CreatePng(40, 30));

        using var image = Image.Load(Path.Combine(OutputDir, result.FileName));
        Assert.Equal(40, image.Width);
        Assert.Equal(30, image.Height);
    }

    [Fact]
    public async Task AC11_Save_FileContentEqualsInput()
    {
        var storage = new FileResultStorage(OutputDir);
        var png = CreatePng();

        var result = await storage.SaveAsync(png);

        Assert.Equal(png, File.ReadAllBytes(Path.Combine(OutputDir, result.FileName)));
    }

    [Fact]
    public async Task AC11_Save_ReturnsResultImage_WithPngFormat()
    {
        var storage = new FileResultStorage(OutputDir);
        var before = DateTime.Now.AddSeconds(-1);

        var result = await storage.SaveAsync(CreatePng());

        Assert.Equal("png", result.Format);
        Assert.EndsWith(".png", result.FileName);
        Assert.InRange(result.CreatedAt, before, DateTime.Now.AddSeconds(1));
    }

    [Fact]
    public async Task AC11_Save_CreatesMissingDirectory()
    {
        var storage = new FileResultStorage(Path.Combine(OutputDir, "nested", "deeper"));

        await storage.SaveAsync(CreatePng());

        Assert.Single(Directory.GetFiles(Path.Combine(OutputDir, "nested", "deeper")));
    }

    [Fact]
    public async Task AC11_TwoSavesInSameSecond_GetDifferentNames()
    {
        var storage = new FileResultStorage(OutputDir);
        var png = CreatePng();

        var first = await storage.SaveAsync(png);
        var second = await storage.SaveAsync(png);

        Assert.NotEqual(first.FileName, second.FileName);
        Assert.Equal(2, FilesInOutput().Length);
    }

    [Fact]
    public async Task AC11_Save_LeavesNoTemporaryFile()
    {
        var storage = new FileResultStorage(OutputDir);

        await storage.SaveAsync(CreatePng());

        Assert.All(FilesInOutput(), f => Assert.EndsWith(".png", f));
    }

    // ---------- E1: помилка збереження ----------

    [Fact]
    public async Task E1_NotPng_Throws_AndNothingIsWritten()
    {
        var storage = new FileResultStorage(OutputDir);

        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(new byte[] { 1, 2, 3 }));

        Assert.Empty(FilesInOutput());
    }

    [Fact]
    public async Task E1_SignatureWithGarbage_Throws_AndNothingIsWritten()
    {
        var storage = new FileResultStorage(OutputDir);
        var fake = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8 };

        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(fake));

        Assert.Empty(FilesInOutput());
    }

    [Fact]
    public async Task E1_TruncatedPng_Throws_AndNothingIsWritten()
    {
        var storage = new FileResultStorage(OutputDir);
        var truncated = CreatePng().Take(20).ToArray();   // обірвано посеред заголовка IHDR

        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(truncated));

        Assert.Empty(FilesInOutput());
    }

    [Fact]
    public async Task E1_PngWithoutImageData_Throws_AndNothingIsWritten()
    {
        var storage = new FileResultStorage(OutputDir);
        var headerOnly = CreatePng().Take(33).ToArray();   // сигнатура + повний IHDR, без IDAT

        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(headerOnly));

        Assert.Empty(FilesInOutput());
    }

    [Fact]
    public async Task E1_Cancelled_Throws_AndNoFileLeft()
    {
        var storage = new FileResultStorage(OutputDir);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.SaveAsync(CreatePng(), cts.Token));

        Assert.Empty(FilesInOutput());
    }

    [Fact]
    public async Task E1_DirectoryCannotBeCreated_Throws()
    {
        // Замість папки — звичайний файл: створити під ним папку неможливо.
        Directory.CreateDirectory(_root);
        var blocker = Path.Combine(_root, "blocker");
        File.WriteAllText(blocker, "x");
        var storage = new FileResultStorage(blocker);

        await Assert.ThrowsAnyAsync<IOException>(() => storage.SaveAsync(CreatePng()));
    }

    [Fact]
    public async Task E1_ThroughDesignService_ReturnsFail_StaysItemPlaced_NoFile()
    {
        Directory.CreateDirectory(_root);
        var blocker = Path.Combine(_root, "blocker");
        File.WriteAllText(blocker, "x");

        var service = new DesignService(new StubPhotoAnalyzer(), new StubCatalog(), new FileResultStorage(blocker));
        service.LoadPhoto(new Photo
        {
            FileName = "room.jpg", Format = "jpeg", Width = 800, Height = 600, SizeBytes = 1000,
            Content = new byte[] { 1, 2, 3 }
        });
        await service.AnalyzeAsync();
        service.Suggest();
        service.PlaceItem(service.Session.Suggestions[0].Item, 0, 0);

        var result = await service.SaveAsync(CreatePng());

        Assert.False(result.IsSuccess);
        Assert.Equal(SessionState.ITEM_PLACED, service.Session.State);
        Assert.Empty(service.Session.SavedResults);
        Assert.Equal("x", File.ReadAllText(blocker));   // нічого поруч не створено й не зіпсовано
    }
}

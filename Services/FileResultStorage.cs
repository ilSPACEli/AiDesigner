using AiDesigner.Models;
using Microsoft.AspNetCore.Hosting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;

namespace AiDesigner.Services;

/// <summary>
/// Зберігає результат (PNG) у папку <c>output/</c> поруч із застосунком (FR-11, UC-04).
/// Це єдине, що застосунок пише на диск: вхідне фото лишається тільки в пам'яті (NFR-04).
/// </summary>
public class FileResultStorage : IResultStorage
{
    // Перші 8 байтів будь-якого PNG-файла.
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private readonly string _directory;

    /// <summary>Для DI: папка <c>output/</c> у корені застосунку.</summary>
    public FileResultStorage(IWebHostEnvironment environment)
        : this(Path.Combine(environment.ContentRootPath, "output"))
    {
    }

    /// <summary>Для тестів: довільна папка.</summary>
    public FileResultStorage(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        _directory = outputDirectory;
    }

    public async Task<ResultImage> SaveAsync(byte[] pngBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureValidPngAsync(pngBytes, cancellationToken);

        var now = DateTime.Now;
        var fileName = $"result-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.png";

        Directory.CreateDirectory(_directory);
        var finalPath = Path.Combine(_directory, fileName);
        var tempPath = finalPath + ".tmp";

        try
        {
            // Пишемо у тимчасовий файл і лише потім перейменовуємо: якщо запис обірветься,
            // готового .png не з'явиться (UC-04 E1: «файл не створюється»).
            await File.WriteAllBytesAsync(tempPath, pngBytes, cancellationToken);
            File.Move(tempPath, finalPath);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }

        return new ResultImage { FileName = fileName, Format = "png", CreatedAt = now };
    }

    // Сигнатури замало: обрізаний або пошкоджений файл теж починається з 89 50 4E 47.
    // Тому PNG декодується повністю, і на диск потрапляє лише те, що відкриється (AC-11).
    private static async Task EnsureValidPngAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        if (!HasPngSignature(bytes))
            throw new ArgumentException("Дані не є зображенням PNG.", nameof(bytes));

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var image = await Image.LoadAsync(stream, cancellationToken);
            if (image.Metadata.DecodedImageFormat is not PngFormat)
                throw new ArgumentException("Дані не є зображенням PNG.", nameof(bytes));
        }
        catch (ImageFormatException ex)
        {
            // UnknownImageFormatException і InvalidImageContentException (обрізаний чи пошкоджений файл).
            throw new ArgumentException("Зображення PNG пошкоджене або неповне.", nameof(bytes), ex);
        }
    }

    private static bool HasPngSignature(byte[] bytes) =>
        bytes.Length >= PngSignature.Length && bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Прибрати не вдалося — початкова помилка важливіша, тому її не підміняємо.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

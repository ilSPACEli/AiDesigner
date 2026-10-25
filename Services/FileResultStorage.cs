using AiDesigner.Models;
using Microsoft.AspNetCore.Hosting;

namespace AiDesigner.Services;


public class FileResultStorage : IResultStorage
{
    // Перші 8 байтів будь-якого PNG-файла.
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private readonly string _directory;

    public FileResultStorage(IWebHostEnvironment environment)
        : this(Path.Combine(environment.ContentRootPath, "output"))
    {
    }

    public FileResultStorage(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        _directory = outputDirectory;
    }

    public async Task<ResultImage> SaveAsync(byte[] pngBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        if (!HasPngSignature(pngBytes))
            throw new ArgumentException("Дані не є зображенням PNG.", nameof(pngBytes));

        var now = DateTime.Now;
        var fileName = $"result-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.png";

        Directory.CreateDirectory(_directory);
        var finalPath = Path.Combine(_directory, fileName);
        var tempPath = finalPath + ".tmp";

        try
        {
            // Пишемо у тимчасовий файл і лише потім перейменовуємо
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

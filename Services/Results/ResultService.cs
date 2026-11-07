using AiDesigner.Models;
using AiDesigner.Repositories;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace AiDesigner.Services.Results;

public class ResultService
{
    public const int ThumbnailMaxSide = 320;

    public const int ThumbnailJpegQuality = 80;

    public const string NoItemMessage = "Не вказано предмет каталогу.";
    public const string UnknownItemMessage = "Такого предмета немає в каталозі.";
    public const string NotPngMessage = "Дані не є зображенням PNG або файл пошкоджений.";
    public const string SaveFailedMessage = "Не вдалося зберегти зображення. Спробуйте ще раз.";

    private readonly IResultRepository _repository;
    private readonly ICatalog _catalog;
    private readonly GalleryOptions _options;
    private readonly GalleryRetentionPolicy _retention;
    private readonly ILogger<ResultService> _logger;

    public ResultService(IResultRepository repository, ICatalog catalog, GalleryOptions options, ILogger<ResultService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxImageBytes);

        _repository = repository;
        _catalog = catalog;
        _options = options;
        _retention = new GalleryRetentionPolicy(options.MaxItems);   
        _logger = logger;
    }


    public async Task<SaveOutcome> SaveAsync(byte[] pngBytes, string? catalogItemId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);

        if (pngBytes.Length > _options.MaxImageBytes)
            return SaveOutcome.TooLarge($"Максимальний розмір — {_options.MaxImageBytes / 1048576.0:0.#} МБ.");

        if (string.IsNullOrWhiteSpace(catalogItemId))
            return SaveOutcome.BadRequest(NoItemMessage);

        var item = _catalog.Items.FirstOrDefault(i => i.Id == catalogItemId);
        if (item is null)
            return SaveOutcome.BadRequest(UnknownItemMessage);

        try
        {
            var prepared = await PrepareAsync(pngBytes, cancellationToken);
            if (prepared is null)
                return SaveOutcome.BadRequest(NotPngMessage);

            var count = await _repository.CountAsync(cancellationToken);
            var evict = _retention.CountToEvict(count);

            var entity = new SavedResult
            {
                CreatedAt = DateTime.UtcNow,
                CatalogItemId = item.Id,
                ImageData = prepared.Image,
                ThumbnailData = prepared.Thumbnail
            };
            var saved = await _repository.AddAsync(entity, evict, cancellationToken);

            return SaveOutcome.Saved(WithItem(saved, item));
        }
        catch (OperationCanceledException)
        {
            throw;   // клієнт перервав запит: це не збій збереження
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося зберегти роботу в галерею (предмет {ItemId}).", catalogItemId);
            return SaveOutcome.Failed(SaveFailedMessage);
        }
    }


    public Task<IReadOnlyList<SavedResult>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<SavedResult?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);


    private sealed record PreparedImages(byte[] Image, byte[] Thumbnail);

    private static async Task<PreparedImages?> PrepareAsync(byte[] pngBytes, CancellationToken cancellationToken)
    {
        if (!HasPngSignature(pngBytes))
            return null;

        Image image;
        try
        {
            using var input = new MemoryStream(pngBytes, writable: false);
            image = await Image.LoadAsync(input, cancellationToken);
        }
        catch (ImageFormatException)
        {
            // Невідомий формат або пошкоджений вміст.
            return null;
        }

        using (image)
        {
            if (image.Metadata.DecodedImageFormat is not PngFormat)
                return null;


            var compressed = await EncodeAsync(
                image,
                new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression, SkipMetadata = true },
                cancellationToken);

            using var thumbnail = MakeThumbnail(image);
            var jpeg = await EncodeAsync(thumbnail, new JpegEncoder { Quality = ThumbnailJpegQuality }, cancellationToken);

            return new PreparedImages(compressed, jpeg);
        }
    }


    private static Image MakeThumbnail(Image source)
    {
        var scale = Math.Min(1.0, (double)ThumbnailMaxSide / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        return source.Clone(context =>
        {
            if (scale < 1.0)
                context.Resize(width, height);
            context.BackgroundColor(Color.White);
        });
    }

    private static async Task<byte[]> EncodeAsync(Image image, IImageEncoder encoder, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await image.SaveAsync(stream, encoder, cancellationToken);
        return stream.ToArray();
    }

    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static bool HasPngSignature(byte[] bytes) =>
        bytes.Length >= PngSignature.Length && bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);

    private static SavedResult WithItem(SavedResult saved, CatalogItem item) => new()
    {
        Id = saved.Id,
        CreatedAt = saved.CreatedAt,
        CatalogItemId = saved.CatalogItemId,
        CatalogItem = item,
        ImageData = saved.ImageData,
        ThumbnailData = saved.ThumbnailData
    };
}
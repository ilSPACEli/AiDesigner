using System.Globalization;
using AiDesigner.Api.Dto;

namespace AiDesigner.Services.Results;

// Те, що показує картка галереї: усе вже готове до виведення, сторінка нічого не форматує.
public sealed record GalleryItemView(
    int Id,
    string ItemName,
    string DateText,
    string ThumbnailUrl,
    string ImageUrl,
    string DownloadFileName)
{
    public const string DateFormat = "dd.MM.yyyy HH:mm";

    // zone — часовий пояс для показу дати; null — пояс комп'ютера, на якому працює застосунок.
    public static GalleryItemView From(ResultDto result, TimeZoneInfo? zone = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        // За контрактом (docs/api.md) createdAt — UTC; явно позначаємо Kind, щоб перетворення було чесним.
        var utc = DateTime.SpecifyKind(result.CreatedAt, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone ?? TimeZoneInfo.Local);

        return new GalleryItemView(
            result.Id,
            result.ItemName,
            local.ToString(DateFormat, CultureInfo.InvariantCulture),
            result.ThumbnailUrl,
            result.ImageUrl,
            $"result-{result.Id}.png");   // така сама умовна назва, як у ResultImage.FileName (docs/api.md, розділ 4)
    }
}

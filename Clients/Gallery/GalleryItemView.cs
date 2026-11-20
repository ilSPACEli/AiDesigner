using System.Globalization;
using AiDesigner.Api.Dto;
using AiDesigner.Clients.Design;

namespace AiDesigner.Clients.Gallery;


public sealed record GalleryItemView(
    int Id,
    string ItemName,
    string DateText,
    string ThumbnailUrl,
    string ImageUrl,
    string DownloadFileName,
    StyleLabel? Style = null)
{
    public const string DateFormat = "dd.MM.yyyy HH:mm";

    public static GalleryItemView From(ResultDto result, TimeZoneInfo? zone = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var utc = DateTime.SpecifyKind(result.CreatedAt, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone ?? TimeZoneInfo.Local);

        return new GalleryItemView(
            result.Id,
            result.ItemName,
            local.ToString(DateFormat, CultureInfo.InvariantCulture),
            result.ThumbnailUrl,
            result.ImageUrl,
            $"result-{result.Id}.png",    
            StyleLabel.For(result.AnalysisMode, result.Style));
    }
}

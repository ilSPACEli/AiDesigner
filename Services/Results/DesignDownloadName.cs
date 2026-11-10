using System.Globalization;

namespace AiDesigner.Services.Results;

// Назва файла, який користувач завантажує зі сторінки «Дизайн».
public static class DesignDownloadName
{
    public static string For(DateTime moment) =>
        $"design-{moment.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.png";
}

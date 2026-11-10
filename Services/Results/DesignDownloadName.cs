using System.Globalization;

namespace AiDesigner.Services.Results;

// Назва файла, який користувач завантажує зі сторінки «Дизайн».
// Момент передаємо в UTC: сервер не знає пояса користувача. Відоме обмеження — час у назві може
// відрізнятися від годинника користувача; на вміст файла це не впливає.
public static class DesignDownloadName
{
    public static string For(DateTime moment) =>
        $"design-{moment.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.png";
}

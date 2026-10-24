using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AiDesigner.Models;

namespace AiDesigner.Data;

// Читає каталог предметів із catalog.json і перевіряє кожну позицію (FR-12, NFR-05).
// Правила добору (R1–R5) тут НЕ реалізуються — це UC-03 (клас Catalog).
public static class CatalogLoader
{
    // Шлях до каталогу відносно wwwroot
    public const string DefaultRelativePath = "data/catalog.json";

    public const int MaxLightings = 3;

    // camelCase у JSON ↔ PascalCase у C#; enum тільки рядками ("SOFA"), числа заборонені,
    // щоб "type": 99 не перетворився на неіснуючий тип
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private static readonly Regex HexColor = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    // Завантажити з файлу. Будь-яка проблема — CatalogException (UC-03, E1: «каталог недоступний»)
    public static IReadOnlyList<CatalogItem> LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new CatalogException($"Файл каталогу не знайдено: {path}");

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new CatalogException($"Не вдалося прочитати файл каталогу: {path}", ex);
        }

        return Parse(json);
    }

    // Розібрати JSON і перевірити позиції
    public static IReadOnlyList<CatalogItem> Parse(string json)
    {
        List<CatalogItem>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<CatalogItem>>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new CatalogException($"Файл каталогу пошкоджений: {ex.Message}", ex);
        }

        if (items is null)
            throw new CatalogException("Файл каталогу порожній.");

        var problems = Validate(items);
        if (problems.Count > 0)
            throw new CatalogException("Каталог містить помилки:" + Environment.NewLine + string.Join(Environment.NewLine, problems));

        return items;
    }

    // Перевірка обов'язкових ознак кожної позиції (AC-12). Повертає список проблем; порожній — усе гаразд
    public static IReadOnlyList<string> Validate(IReadOnlyList<CatalogItem> items)
    {
        var problems = new List<string>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var label = string.IsNullOrWhiteSpace(item.Id) ? $"позиція №{i + 1}" : $"«{item.Id}»";

            if (string.IsNullOrWhiteSpace(item.Id))
                problems.Add($"{label}: порожній id.");
            else if (!seenIds.Add(item.Id))
                problems.Add($"{label}: id повторюється.");

            if (string.IsNullOrWhiteSpace(item.Name))
                problems.Add($"{label}: порожня назва.");

            if (!Enum.IsDefined(item.Type))
                problems.Add($"{label}: невідомий тип.");

            if (string.IsNullOrWhiteSpace(item.ImageFile))
                problems.Add($"{label}: не вказано зображення.");

            if (item.MainColor is null || !HexColor.IsMatch(item.MainColor))
                problems.Add($"{label}: основний колір має бути у форматі #RRGGBB.");

            var lightings = item.SuitableLightings;
            if (lightings is null || lightings.Count is < 1 or > MaxLightings)
                problems.Add($"{label}: має бути від 1 до {MaxLightings} придатних рівнів освітленості.");
            else if (lightings.Distinct().Count() != lightings.Count)
                problems.Add($"{label}: рівні освітленості повторюються.");

            if (string.IsNullOrWhiteSpace(item.LicenseSource))
                problems.Add($"{label}: не вказано джерело/ліцензію зображення (NFR-05).");
        }

        return problems;
    }
}

// Каталог неможливо завантажити або він містить помилки
public sealed class CatalogException : Exception
{
    public CatalogException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

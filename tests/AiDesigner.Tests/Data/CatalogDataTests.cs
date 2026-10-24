using AiDesigner.Data;
using AiDesigner.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace AiDesigner.Tests.Data;

// Перевірка СПРАВЖНЬОГО каталогу: wwwroot/data/catalog.json і картинок у wwwroot/images/catalog
public class CatalogDataTests
{
    private static readonly IReadOnlyList<CatalogItem> Items =
        CatalogLoader.LoadFromFile(Path.Combine(RepoPaths.WebRoot, CatalogLoader.DefaultRelativePath));

    public static IEnumerable<object[]> AllItems() => Items.Select(item => new object[] { item.Id });

    private static CatalogItem ById(string id) => Items.Single(i => i.Id == id);

    // ---------- AC-12: обсяг і обов'язкові ознаки ----------

    [Fact]
    public void AC12_CatalogLoads_WithoutProblems()
    {
        // LoadFromFile кидає виняток, якщо хоч одна позиція некоректна; тут — явна перевірка списку.
        Assert.Empty(CatalogLoader.Validate(Items));
    }

    [Fact]
    public void AC12_CatalogHas20To30Items()
    {
        Assert.InRange(Items.Count, 20, 30);
    }

    [Fact]
    public void AC12_AllSevenItemTypesArePresent()
    {
        Assert.Equal(Enum.GetValues<ItemType>().OrderBy(t => t), Items.Select(i => i.Type).Distinct().OrderBy(t => t));
    }

    [Theory]
    [MemberData(nameof(AllItems))]
    public void AC12_ImageFile_IsNamedAfterId(string id)
    {
        Assert.Equal($"images/catalog/{id}.png", ById(id).ImageFile);
    }

    [Theory]
    [MemberData(nameof(AllItems))]
    public void AC12_ImageFile_ExistsAndIsPngWithAlpha(string id)
    {
        var path = Path.Combine(RepoPaths.WebRoot, ById(id).ImageFile);
        Assert.True(File.Exists(path), $"Немає файлу {path}");

        var info = Image.Identify(path);
        var png = info.Metadata.GetPngMetadata();

        // Прозорий фон (FR-12, AC-08) можливий лише в PNG з альфа-каналом.
        Assert.Equal(PngColorType.RgbWithAlpha, png.ColorType);
        Assert.True(Math.Max(info.Width, info.Height) <= 512, "Зображення має бути зменшене до 512 px.");
    }

    // ---------- AC-18 / NFR-05: джерело й ліцензія ----------

    [Theory]
    [MemberData(nameof(AllItems))]
    public void AC18_LicenseSource_HasLicenseAndLink(string id)
    {
        var license = ById(id).LicenseSource;

        Assert.Contains("CC BY-NC 4.0", license);   // ліцензія, що дозволяє навчальне використання
        Assert.Contains("https://", license);       // посилання на джерело — вимога CC BY
    }

    // ---------- Достатність для добору (передумова AC-06 в UC-03) ----------

    [Theory]
    [InlineData(LightingLevel.DARK)]
    [InlineData(LightingLevel.MODERATE)]
    [InlineData(LightingLevel.BRIGHT)]
    public void EachLightingLevel_HasEnoughItemsForThreeToFourSuggestions(LightingLevel level)
    {
        var suitable = Items.Where(i => i.SuitableLightings.Contains(level)).ToList();

        // Для 3–4 пропозицій за R3 (≤ 2 одного типу) потрібно щонайменше 4 предмети й 2 типи;
        // беремо з запасом, бо R1 частину ще відсіє за кольором.
        Assert.True(suitable.Count >= 10, $"Для {level} лише {suitable.Count} предметів.");
        Assert.True(suitable.Select(i => i.Type).Distinct().Count() >= 4, $"Для {level} замало типів.");
    }

    [Fact]
    public void Catalog_HasNeutralAndColoredItems()
    {
        // R1 працює і з нейтральними, і з кольоровими предметами — у каталозі мають бути обидва види.
        static double Saturation(string hex)
        {
            var r = Convert.ToInt32(hex[1..3], 16) / 255.0;
            var g = Convert.ToInt32(hex[3..5], 16) / 255.0;
            var b = Convert.ToInt32(hex[5..7], 16) / 255.0;
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var l = (max + min) / 2;
            return max == min ? 0 : (max - min) / (1 - Math.Abs(2 * l - 1));
        }

        var saturations = Items.Select(i => Saturation(i.MainColor)).ToList();
        Assert.True(saturations.Count(s => s < 0.2) >= 5, "Замало нейтральних предметів.");
        Assert.True(saturations.Count(s => s >= 0.4) >= 8, "Замало кольорових предметів.");
    }
}

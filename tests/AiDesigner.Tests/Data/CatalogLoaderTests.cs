using System.Text.Json;
using AiDesigner.Data;
using AiDesigner.Models;

namespace AiDesigner.Tests.Data;

public class CatalogLoaderTests
{
    // Коректна позиція; окремі поля можна замінити або прибрати (значення null — прибрати поле)
    private static Dictionary<string, object?> Item(string id = "sofa-01", params (string Key, object? Value)[] overrides)
    {
        var item = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["name"] = "Сірий диван",
            ["type"] = "SOFA",
            ["imageFile"] = $"images/catalog/{id}.png",
            ["mainColor"] = "#82817A",
            ["suitableLightings"] = new[] { "DARK", "MODERATE" },
            ["licenseSource"] = "pngimg.com, CC BY-NC 4.0, https://pngimg.com/image/6923"
        };
        foreach (var (key, value) in overrides)
        {
            if (value is null)
                item.Remove(key);
            else
                item[key] = value;
        }
        return item;
    }

    private static string Json(params Dictionary<string, object?>[] items) => JsonSerializer.Serialize(items);

    // ---------- Коректний каталог ----------

    [Fact]
    public void Parse_ValidJson_MapsAllFields()
    {
        var items = CatalogLoader.Parse(Json(Item("sofa-01"), Item("lamp-01", ("type", "LAMP"))));

        Assert.Equal(2, items.Count);
        var sofa = items[0];
        Assert.Equal("sofa-01", sofa.Id);
        Assert.Equal("Сірий диван", sofa.Name);
        Assert.Equal(ItemType.SOFA, sofa.Type);
        Assert.Equal("images/catalog/sofa-01.png", sofa.ImageFile);
        Assert.Equal("#82817A", sofa.MainColor);
        Assert.Equal(new[] { LightingLevel.DARK, LightingLevel.MODERATE }, sofa.SuitableLightings);
        Assert.StartsWith("pngimg.com", sofa.LicenseSource);
        Assert.Equal(ItemType.LAMP, items[1].Type);
    }

    // ---------- Пошкоджений файл (UC-03, E1) ----------

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("{\"id\": \"sofa-01\"}")]   // об'єкт замість масиву
    public void Parse_BrokenJson_ThrowsCatalogException(string json)
    {
        Assert.Throws<CatalogException>(() => CatalogLoader.Parse(json));
    }

    [Fact]
    public void Parse_NullJson_ThrowsCatalogException()
    {
        Assert.Throws<CatalogException>(() => CatalogLoader.Parse("null"));
    }

    [Fact]
    public void LoadFromFile_MissingFile_ThrowsCatalogException()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        var ex = Assert.Throws<CatalogException>(() => CatalogLoader.LoadFromFile(path));
        Assert.Contains("не знайдено", ex.Message);
    }

    [Fact]
    public void LoadFromFile_ValidFile_Loads()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, Json(Item("sofa-01")));
        try
        {
            Assert.Single(CatalogLoader.LoadFromFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---------- Невідомий тип / освітленість ----------

    [Fact]
    public void Parse_UnknownType_ThrowsCatalogException()
    {
        // DRESSER немає в ItemType — без enum таку помилку довелося б ловити вручну.
        Assert.Throws<CatalogException>(() => CatalogLoader.Parse(Json(Item("sofa-01", ("type", "DRESSER")))));
    }

    [Fact]
    public void Parse_TypeAsNumber_IsRejected()
    {
        // Без заборони чисел "type": 99 став би неіснуючим (ItemType)99.
        Assert.Throws<CatalogException>(() => CatalogLoader.Parse(Json(Item("sofa-01", ("type", 99)))));
    }

    [Fact]
    public void Parse_UnknownLighting_ThrowsCatalogException()
    {
        Assert.Throws<CatalogException>(() =>
            CatalogLoader.Parse(Json(Item("sofa-01", ("suitableLightings", new[] { "DIM" })))));
    }

    // ---------- AC-12: обов'язкові ознаки ----------

    [Theory]
    [InlineData("id")]
    [InlineData("name")]
    [InlineData("imageFile")]
    [InlineData("mainColor")]
    [InlineData("suitableLightings")]
    public void AC12_MissingRequiredField_IsReported(string field)
    {
        var ex = Assert.Throws<CatalogException>(() => CatalogLoader.Parse(Json(Item("sofa-01", (field, null)))));
        Assert.Contains("помилки", ex.Message);
    }

    [Theory]
    [InlineData("name", "   ")]
    [InlineData("mainColor", "red")]
    [InlineData("mainColor", "#12345")]
    [InlineData("mainColor", "#GGGGGG")]
    public void AC12_InvalidValue_IsReported(string field, string value)
    {
        Assert.Throws<CatalogException>(() => CatalogLoader.Parse(Json(Item("sofa-01", (field, value)))));
    }

    [Fact]
    public void AC12_DuplicateId_IsReported()
    {
        var ex = Assert.Throws<CatalogException>(() => CatalogLoader.Parse(Json(Item("sofa-01"), Item("sofa-01"))));
        Assert.Contains("повторюється", ex.Message);
    }

    [Theory]
    [InlineData("")]                             // жодного рівня
    [InlineData("DARK,DARK")]                    // повтор
    [InlineData("DARK,MODERATE,BRIGHT,DARK")]    // більше трьох
    public void AC12_WrongLightingsCount_IsReported(string list)
    {
        var lightings = list.Split(',', StringSplitOptions.RemoveEmptyEntries);

        Assert.Throws<CatalogException>(() =>
            CatalogLoader.Parse(Json(Item("sofa-01", ("suitableLightings", lightings)))));
    }

    [Fact]
    public void AC12_AllProblemsAreListedTogether()
    {
        var broken = new CatalogItem
        {
            Id = "",
            Name = "",
            Type = ItemType.SOFA,
            ImageFile = "images/catalog/x.png",
            MainColor = "blue",
            SuitableLightings = new[] { LightingLevel.DARK },
            LicenseSource = "pngimg.com"
        };

        var problems = CatalogLoader.Validate(new[] { broken });

        Assert.Equal(3, problems.Count);   // id, назва, колір — одним списком, а не по одній помилці
    }

    // ---------- AC-18 / NFR-05: ліцензія ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AC18_MissingLicense_IsReported(string? license)
    {
        var item = license is null ? Item("sofa-01", ("licenseSource", null)) : Item("sofa-01", ("licenseSource", license));

        var ex = Assert.Throws<CatalogException>(() => CatalogLoader.Parse(Json(item)));
        Assert.Contains("NFR-05", ex.Message);
    }
}

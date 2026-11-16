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
            ["licenseSource"] = "pngimg.com, CC BY-NC 4.0, https://pngimg.com/image/6923",
            ["style"] = "SCANDINAVIAN"
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
        Assert.Equal(RoomStyle.SCANDINAVIAN, sofa.Style);
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

    [Theory]
    [InlineData("[null]")]
    [InlineData("[null, null]")]
    public void Parse_NullEntry_ThrowsCatalogException(string json)
    {
        var ex = Assert.Throws<CatalogException>(() => CatalogLoader.Parse(json));
        Assert.Contains("позиція №1: порожній запис (null)", ex.Message);
    }

    [Fact]
    public void Parse_NullEntryAmongValidItems_ReportsItsPosition()
    {
        var json = "[" + JsonSerializer.Serialize(Item("sofa-01")) + ", null]";

        var ex = Assert.Throws<CatalogException>(() => CatalogLoader.Parse(json));
        Assert.Contains("позиція №2: порожній запис (null)", ex.Message);
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
    [InlineData("style")]
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
            LicenseSource = "pngimg.com",
            Style = RoomStyle.LOFT
        };

        var problems = CatalogLoader.Validate(new[] { broken });

        Assert.Equal(3, problems.Count);   // id, назва, колір — одним списком, а не по одній помилці
    }

    // ---------- AC-12 (S2-5): стиль ----------

    public static IEnumerable<object[]> AllStyles() =>
        Enum.GetNames<RoomStyle>().Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(AllStyles))]
    public void AC12_EveryRoomStyle_IsAccepted(string style)
    {
        var items = CatalogLoader.Parse(Json(Item("sofa-01", ("style", style))));

        Assert.Equal(Enum.Parse<RoomStyle>(style), items[0].Style);
    }

    [Fact]
    public void AC12_MissingStyle_IsReported()
    {
        var ex = Assert.Throws<CatalogException>(() => CatalogLoader.Parse(Json(Item("sofa-01", ("style", null)))));

        Assert.Contains("не вказано стиль", ex.Message);
        Assert.Contains("«sofa-01»", ex.Message);
    }

    [Fact]
    public void AC12_NullStyle_IsReported()
    {
        // Явне "style": null — те саме, що відсутнє поле.
        var json = "[" + JsonSerializer.Serialize(Item("sofa-01", ("style", null))).TrimEnd('}') + ",\"style\":null}]";

        var ex = Assert.Throws<CatalogException>(() => CatalogLoader.Parse(json));
        Assert.Contains("не вказано стиль", ex.Message);
    }

    [Theory]
    [InlineData("Футуризм")]
    [InlineData("FUTURISM")]
    [InlineData("Скандинавський")]   // відображувана назва, а не значення enum
    public void AC12_UnknownStyle_ThrowsCatalogException(string style)
    {
        Assert.Throws<CatalogException>(() => CatalogLoader.Parse(Json(Item("sofa-01", ("style", style)))));
    }

    [Fact]
    public void AC12_StyleAsNumber_IsRejected()
    {
        // Без заборони чисел "style": 99 став би неіснуючим (RoomStyle)99.
        Assert.Throws<CatalogException>(() => CatalogLoader.Parse(Json(Item("sofa-01", ("style", 99)))));
    }

    [Fact]
    public void AC12_Validate_ReportsMissingAndUndefinedStyle()
    {
        CatalogItem Make(string id, RoomStyle? style) => new()
        {
            Id = id,
            Name = "Диван",
            Type = ItemType.SOFA,
            ImageFile = $"images/catalog/{id}.png",
            MainColor = "#82817A",
            SuitableLightings = new[] { LightingLevel.DARK },
            LicenseSource = "pngimg.com, CC BY-NC 4.0, https://pngimg.com/image/6923",
            Style = style
        };

        var problems = CatalogLoader.Validate(new[] { Make("sofa-01", null), Make("sofa-02", (RoomStyle)99) });

        Assert.Equal(2, problems.Count);
        Assert.Contains("не вказано стиль", problems[0]);
        Assert.Contains("невідомий стиль", problems[1]);
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

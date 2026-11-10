using AiDesigner.Api.Dto;
using AiDesigner.Services.Results;

namespace AiDesigner.Tests.Services;

// Сторінка «Галерея»: як робота з API перетворюється на дані картки.
public class GalleryItemViewTests
{
    private static ResultDto Dto(DateTime createdAt, int id = 7) => new(
        id, createdAt, "sofa-01", "Сірий диван",
        $"/api/results/{id}/image", $"/api/results/{id}/thumbnail");

    private static DateTime Utc(int y, int mo, int d, int h, int mi) =>
        new(y, mo, d, h, mi, 0, DateTimeKind.Utc);

    [Fact]
    public void AC19_From_MapsFieldsOfWork()
    {
        var dto = Dto(Utc(2026, 11, 14, 18, 20));

        var view = GalleryItemView.From(dto, TimeZoneInfo.Utc);

        Assert.Equal(7, view.Id);
        Assert.Equal("Сірий диван", view.ItemName);
        Assert.Equal("/api/results/7/thumbnail", view.ThumbnailUrl);
        Assert.Equal("/api/results/7/image", view.ImageUrl);
        Assert.Equal("14.11.2026 18:20", view.DateText);
    }

    [Fact]
    public void AC21_From_DownloadFileName_UsesWorkId()
    {
        var view = GalleryItemView.From(Dto(Utc(2026, 11, 14, 18, 20), id: 42), TimeZoneInfo.Utc);

        Assert.Equal("result-42.png", view.DownloadFileName);
    }

    [Fact]
    public void AC19_From_ConvertsUtcToGivenTimeZone()
    {
        var plusTwo = TimeZoneInfo.CreateCustomTimeZone("test+2", TimeSpan.FromHours(2), "test+2", "test+2");

        var view = GalleryItemView.From(Dto(Utc(2026, 11, 14, 23, 30)), plusTwo);

        // 23:30 UTC + 2 год = 01:30 наступної доби.
        Assert.Equal("15.11.2026 01:30", view.DateText);
    }

    [Fact]
    public void AC19_From_UnspecifiedKind_IsTreatedAsUtc()
    {
        var unspecified = new DateTime(2026, 11, 14, 18, 20, 0, DateTimeKind.Unspecified);

        var view = GalleryItemView.From(Dto(unspecified), TimeZoneInfo.Utc);

        Assert.Equal("14.11.2026 18:20", view.DateText);
    }

    [Fact]
    public void AC19_From_WithoutZone_UsesLocalTimeZone()
    {
        var dto = Dto(Utc(2026, 11, 14, 18, 20));

        var byDefault = GalleryItemView.From(dto);
        var explicitLocal = GalleryItemView.From(dto, TimeZoneInfo.Local);

        Assert.Equal(explicitLocal.DateText, byDefault.DateText);
    }

    [Fact]
    public void AC19_From_NullResult_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GalleryItemView.From(null!));
    }
}

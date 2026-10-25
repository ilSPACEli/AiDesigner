using AiDesigner.Models;

namespace AiDesigner.Tests;

public class PlacementTests
{
    // ---------- AC-08: початкове положення ----------

    [Theory]
    [InlineData(1000, 800)]
    [InlineData(640, 480)]
    [InlineData(4000, 3000)]
    [InlineData(481, 999)]   // непарні розміри
    public void AC08_Initial_ItemIsAtPhotoCenter(int photoW, int photoH)
    {
        var p = PlacementGeometry.Initial(photoW, photoH, 512, 492);

        Assert.Equal(photoW / 2, p.X);
        Assert.Equal(photoH / 2, p.Y);
    }

    [Fact]
    public void AC08_Initial_LargeItem_IsShrunkToThirdOfPhotoWidth()
    {
        var p = PlacementGeometry.Initial(1200, 900, 512, 492);

        Assert.Equal(400m, 512 * p.Scale, precision: 6);   // 1200 / 3
    }

    [Fact]
    public void AC08_Initial_SmallItem_IsNotEnlarged()
    {
        var p = PlacementGeometry.Initial(3000, 2000, 100, 100);

        Assert.Equal(1m, p.Scale);
    }

    [Fact]
    public void AC08_Initial_TallItem_FitsPhotoHeight()
    {
        // Вузький високий предмет: обмежує висота, а не ширина.
        var p = PlacementGeometry.Initial(3000, 600, 200, 512);

        Assert.True(512 * p.Scale <= 600m * PlacementGeometry.InitialHeightFraction);
    }

    [Theory]
    [InlineData(0, 800, 512, 492)]
    [InlineData(1000, 0, 512, 492)]
    [InlineData(1000, 800, 0, 492)]
    [InlineData(1000, 800, 512, -1)]
    public void AC08_Initial_NonPositiveSize_Throws(int pw, int ph, int iw, int ih)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlacementGeometry.Initial(pw, ph, iw, ih));
    }

    // ---------- AC-09: обмеження межами фото ----------

    private const int PhotoW = 1000, PhotoH = 800, ItemW = 512, ItemH = 492;

    // Скільки предмета (по одній осі) потрапляє на фото.
    private static decimal Overlap(decimal center, decimal size, int photo) =>
        Math.Max(0m, Math.Min(center + size / 2, photo) - Math.Max(center - size / 2, 0m));

    [Fact]
    public void AC09_InsideBounds_IsUnchanged()
    {
        var p = PlacementGeometry.Clamp(300, 250, 0.5m, PhotoW, PhotoH, ItemW, ItemH);

        Assert.Equal(new PlacementValues(300, 250, 0.5m), p);
    }

    [Theory]
    [InlineData(-10000, -10000)]
    [InlineData(10000, 10000)]
    [InlineData(-10000, 400)]
    [InlineData(500, 10000)]
    [InlineData(10000, -10000)]
    public void AC09_FarOutside_IsPulledBack_ItemStaysOnPhoto(int x, int y)
    {
        const decimal scale = 0.6m;

        var p = PlacementGeometry.Clamp(x, y, scale, PhotoW, PhotoH, ItemW, ItemH);

        var w = ItemW * scale;
        var h = ItemH * scale;
        Assert.True(Overlap(p.X, w, PhotoW) >= w * PlacementGeometry.MinVisibleFraction);
        Assert.True(Overlap(p.Y, h, PhotoH) >= h * PlacementGeometry.MinVisibleFraction);
        Assert.True(Overlap(p.X, w, PhotoW) > 0);   // не повністю поза фото
        Assert.True(Overlap(p.Y, h, PhotoH) > 0);
    }

    [Fact]
    public void AC09_Clamp_DoesNotChangeScale()
    {
        var p = PlacementGeometry.Clamp(-5000, 5000, 0.37m, PhotoW, PhotoH, ItemW, ItemH);

        Assert.Equal(0.37m, p.Scale);
    }

    [Fact]
    public void AC09_Clamp_OnlyMovesTheAxisThatIsOutside()
    {
        var p = PlacementGeometry.Clamp(-10000, 400, 0.5m, PhotoW, PhotoH, ItemW, ItemH);

        Assert.Equal(400, p.Y);
        Assert.NotEqual(-10000, p.X);
    }

    [Fact]
    public void AC09_ItemLargerThanPhoto_StillAllowedToCoverIt()
    {
        // Предмет ширший за фото: центр у межах фото допустимий.
        var p = PlacementGeometry.Clamp(500, 400, 3m, PhotoW, PhotoH, ItemW, ItemH);

        Assert.Equal(new PlacementValues(500, 400, 3m), p);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AC09_Clamp_NonPositiveScale_Throws(double scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlacementGeometry.Clamp(0, 0, (decimal)scale, PhotoW, PhotoH, ItemW, ItemH));
    }
}

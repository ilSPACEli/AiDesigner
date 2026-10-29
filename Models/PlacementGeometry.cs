namespace AiDesigner.Models;

public readonly record struct PlacementValues(int X, int Y, decimal Scale);


public static class PlacementGeometry
{
    public const decimal InitialWidthFraction = 1m / 3m;

    public const decimal InitialHeightFraction = 2m / 3m;

    public const decimal MinVisibleFraction = 0.2m;

    public const decimal MinItemSizeFraction = 0.1m;

    public static PlacementValues Initial(int photoWidth, int photoHeight, int itemWidth, int itemHeight)
    {
        RequirePositive(photoWidth, nameof(photoWidth));
        RequirePositive(photoHeight, nameof(photoHeight));
        RequirePositive(itemWidth, nameof(itemWidth));
        RequirePositive(itemHeight, nameof(itemHeight));

        var scale = Math.Min(1m, Math.Min(
            photoWidth * InitialWidthFraction / itemWidth,
            photoHeight * InitialHeightFraction / itemHeight));

        return new PlacementValues(photoWidth / 2, photoHeight / 2, scale);
    }

    public static decimal MinScale(int photoWidth, int photoHeight, int itemWidth, int itemHeight)
    {
        RequirePositive(photoWidth, nameof(photoWidth));
        RequirePositive(photoHeight, nameof(photoHeight));
        RequirePositive(itemWidth, nameof(itemWidth));
        RequirePositive(itemHeight, nameof(itemHeight));

        return Math.Min(1m,
            MinItemSizeFraction * Math.Min(photoWidth, photoHeight) / Math.Max(itemWidth, itemHeight));
    }

    public static PlacementValues Clamp(int x, int y, decimal scale,
        int photoWidth, int photoHeight, int itemWidth, int itemHeight)
    {
        RequirePositive(photoWidth, nameof(photoWidth));
        RequirePositive(photoHeight, nameof(photoHeight));
        RequirePositive(itemWidth, nameof(itemWidth));
        RequirePositive(itemHeight, nameof(itemHeight));
        if (scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), "Масштаб має бути більшим за нуль.");

        scale = Math.Max(scale, MinScale(photoWidth, photoHeight, itemWidth, itemHeight));

        return new PlacementValues(
            ClampAxis(x, itemWidth * scale, photoWidth),
            ClampAxis(y, itemHeight * scale, photoHeight),
            scale);
    }

    private static int ClampAxis(int center, decimal size, int photo)
    {
        var min = (int)Math.Ceiling(size * (MinVisibleFraction - 0.5m));
        var max = (int)Math.Floor(photo + size * (0.5m - MinVisibleFraction));
        return Math.Clamp(center, min, max);
    }

    private static void RequirePositive(int value, string name)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(name, "Розмір має бути більшим за нуль.");
    }
}
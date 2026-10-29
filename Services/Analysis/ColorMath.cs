using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Services.Analysis;


public static class ColorMath
{

    // Яскравість ×1000 у цілих числах: 299R + 587G + 114B (0…255000).
    // Цілі числа рахуються точно, без похибок double на межах 15/85/170/240
    public static int LuminanceX1000(Rgb24 color) =>
        299 * color.R + 587 * color.G + 114 * color.B;

    // Яскравість 0.299R + 0.587G + 0.114B (0…255)
    public static double Luminance(Rgb24 color) => LuminanceX1000(color) / 1000.0;

    public static int Hue(Rgb24 color)
    {
        int r = color.R, g = color.G, b = color.B;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        if (delta == 0)
            return 0;

        // Стандартна формула HSV
        double hue;
        if (max == r)
            hue = 60.0 * (g - b) / delta;          // між пурпуровим (−60°) і жовтим (60°)
        else if (max == g)
            hue = 60.0 * (b - r) / delta + 120;    // між жовтим (60°) і блакитним (180°)
        else
            hue = 60.0 * (r - g) / delta + 240;    // між блакитним (180°) і пурпуровим (300°)

        if (hue < 0)
            hue += 360;

        return (int)Math.Round(hue) % 360;
    }

    public static string ToHex(Rgb24 color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}

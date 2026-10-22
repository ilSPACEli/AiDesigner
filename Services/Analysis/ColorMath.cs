using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Services.Analysis;


public static class ColorMath
{

    public static double Luminance(Rgb24 color) =>
        0.299 * color.R + 0.587 * color.G + 0.114 * color.B;

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

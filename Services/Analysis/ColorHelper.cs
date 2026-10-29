using System;

namespace AiDesigner.Services.Analysis;

public static class ColorHelper
{
    public static double GetHueDistance(double hue1, double hue2)
    {
        double diff = Math.Abs(hue1 - hue2);
        return Math.Min(diff, 360.0 - diff);
    }

    public static bool IsAnalogous(double hue1, double hue2)
    {
        return GetHueDistance(hue1, hue2) <= 30.0;
    }

    public static bool IsComplementary(double hue1, double hue2)
    {
        double oppositeHue = (hue1 + 180.0) % 360.0;
        return GetHueDistance(oppositeHue, hue2) <= 30.0;
    }

    // Відтінок (0–360°) кольору "#RRGGBB"; для сірих — 0
    public static double GetHue(string hex)
    {
        ExtractHsl(hex, out double h, out _, out _);
        return h;
    }

    public static bool IsNeutral(string hex)
    {
        ExtractHsl(hex, out double h, out double s, out double l);

        if (l < 0.15 || l > 0.85 || s < 0.15)
            return true;

        if (h >= 20 && h <= 50 && s < 0.5 && l > 0.4)
            return true;

        return false;
    }

    private static void ExtractHsl(string hex, out double h, out double s, out double l)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) 
        { 
            h = 0; s = 0; l = 0; 
            return; 
        }

        double r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255.0;
        double g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255.0;
        double b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2.0;

        if (max == min)
        {
            h = 0;
            s = 0;
        }
        else
        {
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            
            h *= 60;
        }
    }
}
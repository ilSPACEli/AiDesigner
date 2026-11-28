using System.Text.RegularExpressions;

namespace AiDesigner.Models;

// Колір у форматі #RRGGBB — одна перевірка для запиту API й каталогу.
public static partial class HexColor
{
    public const int TimeoutMilliseconds = 100;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.None, TimeoutMilliseconds)]
    private static partial Regex Pattern();

    public static bool IsValid(string? value)
    {
        if (value is null)
            return false;

        try
        {
            return Pattern().IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}

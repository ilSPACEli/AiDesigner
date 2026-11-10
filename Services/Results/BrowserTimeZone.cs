namespace AiDesigner.Services.Results;

// Blazor Server виконується на сервері, тож TimeZoneInfo.Local — це пояс сервера, а не користувача.
// Браузер повідомляє свій пояс (IANA-ім'я, напр. "Europe/Kyiv"); тут його перетворюємо на TimeZoneInfo.
public static class BrowserTimeZone
{
    // Невідоме чи порожнє ім'я — не помилка: показуємо час за поясом сервера.
    public static TimeZoneInfo Resolve(string? ianaId)
    {
        if (string.IsNullOrWhiteSpace(ianaId))
            return TimeZoneInfo.Local;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }
}

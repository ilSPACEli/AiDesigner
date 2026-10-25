namespace AiDesigner.Models;


public class Photo
{
    // Обмеження вхідного фото (NFR-06).
    public const int MaxSizeBytes = 10 * 1024 * 1024;   // 10 МБ
    public const int MinSidePx = 480;

    // Причини відмови, які бачить Користувач (UC-01 A2).
    public const string WrongFormatMessage =
        "Фото не прийнято: підтримуються лише формати JPEG і PNG. Оберіть інше фото.";
    public const string TooLargeMessage =
        "Фото не прийнято: розмір файла більший за 10 МБ. Оберіть менший файл.";
    public const string TooSmallMessage =
        "Фото не прийнято: зображення замале, менша сторона має бути не менше 480 пікселів. Оберіть інше фото.";

    public string FileName { get; init; } = string.Empty;

    // Справжній формат, визначений за вмістом файла: "jpeg", "png" або інший.
    public string Format { get; init; } = string.Empty;

    public int Width { get; init; }

    public int Height { get; init; }

    public int SizeBytes { get; init; }

    public byte[] Content { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Причина, з якої фото не відповідає NFR-06, або <c>null</c>, якщо фото коректне.
    /// Обчислюється щоразу з властивостей, тому не має побічних ефектів.
    /// </summary>
    public string? RejectionReason
    {
        get
        {
            if (Format is not ("jpeg" or "png"))
                return WrongFormatMessage;
            if (SizeBytes > MaxSizeBytes)
                return TooLargeMessage;
            if (Math.Min(Width, Height) < MinSidePx)
                return TooSmallMessage;
            return null;
        }
    }

    // Сигнатура як на діаграмі класів: validate(): bool.
    public bool Validate() => RejectionReason is null;
}

using System.Net;

namespace AiDesigner.Services.Results;

// Тексти помилок сторінки «Галерея» (US-1). Клієнт (GalleryClient) кидає HttpRequestException,
// якщо сервер відповів помилкою або недоступний; цей клас перетворює її на зрозумілий текст,
// щоб у .razor не було логіки «який код — яке повідомлення».
public static class GalleryErrorMessages
{
    public const string WorkNotFoundMessage = "Цієї роботи вже немає: її видалено.";
    public const string TooManyRequestsMessage = "Забагато запитів. Зачекайте хвилину й спробуйте ще раз.";
    public const string NoConnectionMessage = "Немає зв'язку із сервером. Перевірте з'єднання й спробуйте ще раз.";
    public const string ServerErrorMessage = "Сервер не зміг виконати запит. Спробуйте ще раз пізніше.";
    public const string UnexpectedMessage = "Сталася неочікувана помилка. Спробуйте ще раз.";

    public static string FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is HttpRequestException http
            ? FromStatus(http.StatusCode)
            : UnexpectedMessage;
    }

    // null — відповіді від сервера не було взагалі (немає мережі, сервер вимкнено).
    public static string FromStatus(HttpStatusCode? status) => status switch
    {
        null => NoConnectionMessage,
        HttpStatusCode.NotFound => WorkNotFoundMessage,
        HttpStatusCode.TooManyRequests => TooManyRequestsMessage,
        _ => ServerErrorMessage
    };
}

using AiDesigner.Models;

namespace AiDesigner.Services.Results;

/// <summary>Чим закінчилося збереження. Кожне значення відповідає статусу HTTP (docs/api.md, розд. 1).</summary>
public enum SaveStatus
{
    /// <summary>201: роботу збережено.</summary>
    Saved,

    /// <summary>400: не PNG (за вмістом), немає предмета або такого предмета немає в каталозі.</summary>
    BadRequest,

    /// <summary>413: зображення більше за <c>Gallery:MaxImageBytes</c>.</summary>
    TooLarge,

    /// <summary>500: не вдалося обробити або записати.</summary>
    Failed
}

/// <summary>
/// Результат <see cref="ResultService.SaveAsync"/>. Очікувані відмови повертаються як значення,
/// а не як винятки (правило проєкту). <see cref="Message"/> — українською, для поля <c>detail</c> у ProblemDetails.
/// </summary>
public sealed record SaveOutcome(SaveStatus Status, string Message, SavedResult? Result = null)
{
    public bool IsSaved => Status == SaveStatus.Saved;

    public static SaveOutcome Saved(SavedResult result) => new(SaveStatus.Saved, string.Empty, result);

    public static SaveOutcome BadRequest(string message) => new(SaveStatus.BadRequest, message);

    public static SaveOutcome TooLarge(string message) => new(SaveStatus.TooLarge, message);

    public static SaveOutcome Failed(string message) => new(SaveStatus.Failed, message);
}
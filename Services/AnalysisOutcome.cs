using AiDesigner.Models;

namespace AiDesigner.Services;

public enum AnalysisStatus
{
    // Успіх: є кольори й освітленість (основний сценарій).
    Success,

    // Недостатньо даних: фото майже чорне або майже біле (A1).
    InsufficientData,

    // <summary>Помилка обробки або перевищено 30 с (E1).
    Error
}


public sealed class AnalysisOutcome
{
    public AnalysisStatus Status { get; }

    public AnalysisResult? Result { get; }

    public string Message { get; }

    private AnalysisOutcome(AnalysisStatus status, AnalysisResult? result, string message)
    {
        Status = status;
        Result = result;
        Message = message;
    }

    public static AnalysisOutcome Success(AnalysisResult result) =>
        new(AnalysisStatus.Success, result ?? throw new ArgumentNullException(nameof(result)), string.Empty);

    public static AnalysisOutcome InsufficientData() =>
        new(AnalysisStatus.InsufficientData, null,
            "За цим фото неможливо визначити колірну гаму й освітленість. Надайте інше фото.");

    public static AnalysisOutcome Error(string message) =>
        new(AnalysisStatus.Error, null, message);
}

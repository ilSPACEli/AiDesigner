using AiDesigner.Models;

namespace AiDesigner.Services;

public class DesignService
{
    public const int SuggestionCount = 4;

    private readonly IPhotoAnalyzer _analyzer;
    private readonly ICatalog _catalog;
    private readonly IResultStorage _storage;

    public DesignService(IPhotoAnalyzer analyzer, ICatalog catalog, IResultStorage storage)
    {
        _analyzer = analyzer;
        _catalog = catalog;
        _storage = storage;
    }

    public DesignSession Session { get; } = new();

    public TimeSpan AnalysisTimeout { get; init; } = TimeSpan.FromSeconds(30);


    public void LoadPhoto(Photo photo) => Session.ReplacePhoto(photo);

    public async Task<AnalysisOutcome> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        Session.BeginAnalysis();

        var outcome = await RunAnalyzerAsync(Session.Photo!, cancellationToken);

        if (outcome.Status == AnalysisStatus.Success)
        {
            try
            {
                Session.CompleteAnalysis(outcome.Result!);
                return outcome;
            }
            catch (ArgumentException)
            {
                // Аналізатор повернув не 3–5 кольорів — вважаємо це помилкою обробки (E1).
                outcome = AnalysisOutcome.Error("Аналіз повернув некоректний результат. Спробуйте ще раз.");
            }
        }

        Session.FailAnalysis();
        return outcome;
    }


    private async Task<AnalysisOutcome> RunAnalyzerAsync(Photo photo, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            // WaitAsync перестає чекати через AnalysisTimeout, навіть якщо аналізатор
            // не перевіряє токен; токен додатково просить аналізатор зупинитися.
            return await _analyzer
                .AnalyzeAsync(photo, timeoutCts.Token)
                .WaitAsync(AnalysisTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            timeoutCts.Cancel();
            return AnalysisOutcome.Error(
                $"Аналіз не завершився за {AnalysisTimeout.TotalSeconds:0} с. Спробуйте ще раз.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AnalysisOutcome.Error("Аналіз скасовано.");
        }
        catch (Exception)
        {
            timeoutCts.Cancel();
            return AnalysisOutcome.Error("Під час аналізу фото сталася помилка. Спробуйте ще раз.");
        }
    }

    public ServiceResult Suggest()
    {
        var analysis = Session.Analysis
            ?? throw new InvalidOperationException("Спочатку виконайте аналіз фото.");

        IReadOnlyList<Suggestion> suggestions;
        try
        {
            suggestions = _catalog.Select(analysis, SuggestionCount);
        }
        catch (Exception)
        {
            return ServiceResult.Fail("Каталог предметів недоступний. Спробуйте пізніше.");
        }

        if (suggestions.Count == 0)
            return ServiceResult.Fail("Придатних предметів немає. Спробуйте надати інше фото.");

        Session.ShowSuggestions(suggestions);
        return ServiceResult.Ok();
    }

    public void PlaceItem(CatalogItem item, int x, int y) =>
        Session.PlaceItem(new Placement(item, x, y));

    public async Task<ServiceResult> SaveAsync(byte[] pngBytes, CancellationToken cancellationToken = default)
    {
        // Перевіряємо стан ДО запису файлу, щоб не створити файл, який сесія не прийме.
        if (Session.State != SessionState.ITEM_PLACED)
            throw new InvalidOperationException($"Неможливо зберегти результат у стані {Session.State}.");

        ResultImage image;
        try
        {
            image = await _storage.SaveAsync(pngBytes, cancellationToken);
        }
        catch (Exception)
        {
            return ServiceResult.Fail("Не вдалося зберегти зображення. Спробуйте ще раз.");
        }

        Session.MarkSaved(image);
        return ServiceResult.Ok();
    }
}

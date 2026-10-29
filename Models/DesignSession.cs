namespace AiDesigner.Models;


public class DesignSession
{
    private readonly List<Suggestion> _suggestions = new();
    private readonly List<ResultImage> _savedResults = new();

    public string Id { get; private set; } = string.Empty;

    public SessionState State { get; private set; }

    public Photo? Photo { get; private set; }

    public AnalysisResult? Analysis { get; private set; }

    public IReadOnlyList<Suggestion> Suggestions => _suggestions;

    public Placement? Placement { get; private set; }

    public IReadOnlyList<ResultImage> SavedResults => _savedResults;

    public bool IsAnalyzing { get; private set; }

    public DesignSession()
    {
        StartNew();
    }

    public void StartNew()
    {
        EnsureNotAnalyzing("почати нову сесію");
        Id = Guid.NewGuid().ToString("N");
        Photo = null;
        _savedResults.Clear();
        ClearResults();
        State = SessionState.EMPTY;
    }


    public void ReplacePhoto(Photo photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        EnsureNotAnalyzing("замінити фото");
        Photo = photo;
        ClearResults();
        State = SessionState.PHOTO_LOADED;
    }

    public void BeginAnalysis()
    {
        EnsureNotAnalyzing("запустити аналіз");
        EnsureState("запустити аналіз", SessionState.PHOTO_LOADED);
        IsAnalyzing = true;
    }

    public void CompleteAnalysis(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!IsAnalyzing)
            throw new InvalidOperationException("Аналіз не було запущено.");
        if (result.Colors.Count is < 3 or > 5)
            throw new ArgumentException("Результат аналізу має містити 3–5 кольорів (FR-03).", nameof(result));

        Analysis = result;
        IsAnalyzing = false;
        State = SessionState.ANALYZED;
    }

    public void FailAnalysis()
    {
        if (!IsAnalyzing)
            throw new InvalidOperationException("Аналіз не було запущено.");
        IsAnalyzing = false;
    }


    public void ShowSuggestions(IReadOnlyList<Suggestion> suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        EnsureState("показати пропозиції", SessionState.ANALYZED);
        if (suggestions.Count is < 1 or > 4)
            throw new ArgumentException("Пропозицій має бути від 1 до 4.", nameof(suggestions));

        _suggestions.Clear();
        _suggestions.AddRange(suggestions);
        State = SessionState.SUGGESTIONS_SHOWN;
    }

    public void PlaceItem(Placement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        EnsureState("розмістити предмет",
            SessionState.SUGGESTIONS_SHOWN, SessionState.ITEM_PLACED, SessionState.SAVED);
        if (!_suggestions.Any(s => s.Item.Id == placement.Item.Id))
            throw new ArgumentException("Можна розмістити лише предмет із пропозицій.", nameof(placement));

        Placement = placement;
        State = SessionState.ITEM_PLACED;
    }

    public void MarkSaved(ResultImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        EnsureState("зберегти результат", SessionState.ITEM_PLACED);
        _savedResults.Add(image);
        State = SessionState.SAVED;
    }

    private void ClearResults()
    {
        Analysis = null;
        _suggestions.Clear();
        Placement = null;
    }

    private void EnsureNotAnalyzing(string action)
    {
        if (IsAnalyzing)
            throw new InvalidOperationException($"Неможливо {action}: триває аналіз фото.");
    }

    private void EnsureState(string action, params SessionState[] allowed)
    {
        if (!allowed.Contains(State))
            throw new InvalidOperationException($"Неможливо {action} у стані {State}.");
    }
}

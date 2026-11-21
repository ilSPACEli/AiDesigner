using AiDesigner.Api.Dto;
using AiDesigner.Clients;
using AiDesigner.Models;
using AiDesigner.Services.Style;

namespace AiDesigner.Services;

public class DesignService
{
    public const int SuggestionCount = 4;

    private readonly IPhotoAnalyzer _analyzer;
    private readonly ISuggestionClient _suggestions;
    private readonly IGalleryClient _gallery;
    private readonly IReadOnlyList<IStyleAnalyzer> _styleAnalyzers;
    private readonly ILogger<DesignService>? _logger;

    public DesignService(IPhotoAnalyzer analyzer, ISuggestionClient suggestions, IGalleryClient gallery,
        IEnumerable<IStyleAnalyzer>? styleAnalyzers = null, ILogger<DesignService>? logger = null)
    {
        _analyzer = analyzer;
        _suggestions = suggestions;
        _gallery = gallery;
        _styleAnalyzers = styleAnalyzers?.ToList() ?? new List<IStyleAnalyzer>();
        _logger = logger;
    }

    public DesignSession Session { get; } = new();

    public TimeSpan AnalysisTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan StyleTimeout { get; init; } = TimeSpan.FromSeconds(30);


    // ---------- UC-01. Завантажити фото приміщення ----------

    public const string PhotoReplaceBlockedMessage =
        "Під час аналізу замінити фото не можна. Дочекайтеся завершення аналізу.";

    public const string PhotoShowFailedMessage =
        "Не вдалося показати фото. Спробуйте інший файл.";

    public const string PhotoLoadInProgressMessage =
        "Зачекайте: попереднє фото ще завантажується.";


    public bool IsLoadingPhoto { get; private set; }


    public ServiceResult CheckFileSize(long sizeBytes) =>
        sizeBytes > Photo.MaxSizeBytes
            ? ServiceResult.Fail(Photo.TooLargeMessage)
            : ServiceResult.Ok();


    public async Task<ServiceResult> LoadPhotoAsync(string fileName, byte[] content, Func<byte[], Task> showOnCanvas)
    {
        if (IsLoadingPhoto)
            return ServiceResult.Fail(PhotoLoadInProgressMessage);

        // Під час аналізу заміна заблокована (FR-13): не показуємо фото, яке сесія все одно не прийме.
        if (Session.IsAnalyzing)
            return ServiceResult.Fail(PhotoReplaceBlockedMessage);

        var sizeCheck = CheckFileSize(content.Length);
        if (!sizeCheck.IsSuccess)
            return sizeCheck;

        var photo = ReadPhoto(fileName, content);
        if (!photo.Validate())
            return ServiceResult.Fail(photo.RejectionReason!);

        IsLoadingPhoto = true;
        try
        {
            try
            {
                await showOnCanvas(content);
            }
            catch (Exception)
            {
                // Браузер не зміг показати фото — сесію не змінюємо.
                return ServiceResult.Fail(PhotoShowFailedMessage);
            }

            // Повторна перевірка після await: поки чекали полотно, стан міг змінитися.
            if (Session.IsAnalyzing)
            {
                await RestoreCanvasAsync(showOnCanvas);
                return ServiceResult.Fail(PhotoReplaceBlockedMessage);
            }

            return LoadPhoto(photo);
        }
        finally
        {
            IsLoadingPhoto = false;   // скидаємо завжди, навіть після помилки
        }
    }

    // Повертає на полотно фото, яке зараз у сесії, щоб полотно й сесія не розійшлися.
    private async Task RestoreCanvasAsync(Func<byte[], Task> showOnCanvas)
    {
        if (Session.Photo is not { } current)
            return;
        try
        {
            await showOnCanvas(current.Content);
        }
        catch (Exception)
        {
            // Не вдалося показати фото — сесію не змінюємо.
        }
    }

    public ServiceResult LoadPhoto(Photo photo)
    {
        if (!photo.Validate())
            return ServiceResult.Fail(photo.RejectionReason!);

        try
        {
            Session.ReplacePhoto(photo);   // скидає аналіз, пропозиції й розміщення (FR-13, A1)
        }
        catch (InvalidOperationException)
        {
            // Сесія заблокувала заміну (іде аналіз) — повідомлення замість падіння сторінки.
            return ServiceResult.Fail(PhotoReplaceBlockedMessage);
        }
        return ServiceResult.Ok();
    }


    private static Photo ReadPhoto(string fileName, byte[] content)
    {
        string format;
        int width = 0, height = 0;
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            var info = SixLabors.ImageSharp.Image.Identify(stream);
            width = info.Width;
            height = info.Height;
            format = info.Metadata.DecodedImageFormat switch
            {
                SixLabors.ImageSharp.Formats.Jpeg.JpegFormat => "jpeg",
                SixLabors.ImageSharp.Formats.Png.PngFormat => "png",
                { } other => other.Name.ToLowerInvariant(),
                null => "unknown"
            };
        }
        catch (Exception)
        {
            // Не зображення або пошкоджений файл — це відмова за форматом.
            format = "unknown";
        }

        return new Photo
        {
            FileName = fileName,
            Format = format,
            Width = width,
            Height = height,
            SizeBytes = content.Length,
            Content = content
        };
    }

    // ---------- UC-02. Аналіз: режими й стиль ----------

    public const string CloudConsentRequiredMessage =
        "Для режиму «Хмарний ШІ» потрібна згода на передачу фото сервісу Google Gemini.";

    public const string ModeNotConfiguredReason = "Режим не налаштовано.";
    public const string ModeCheckFailedReason = "Не вдалося перевірити доступність режиму.";

    public static string StyleFailedNotice(AnalysisMode mode) =>
        $"{DisplayNames.Of(mode)} не відповів. Показано результат без ШІ.";

    public string StyleTimeoutNotice(AnalysisMode mode) =>
        $"{DisplayNames.Of(mode)} не відповів за {StyleTimeout.TotalSeconds:0} с. Показано результат без ШІ.";

    public bool HasCloudConsent { get; private set; }

    // Повторний аналіз (напр., в іншому режимі) — доки предмет не розміщено на фото.
    public bool CanStartAnalysis =>
        Session.Photo is not null && !Session.IsAnalyzing && !IsLoadingPhoto
        && Session.State is SessionState.PHOTO_LOADED or SessionState.ANALYZED or SessionState.SUGGESTIONS_SHOWN;

    public void GiveCloudConsent() => HasCloudConsent = true;

    public async Task<IReadOnlyList<ModeAvailability>> GetModesAsync(CancellationToken cancellationToken = default)
    {
        var checks = new[] { AnalysisMode.LOCAL_AI, AnalysisMode.CLOUD_AI }.Select(mode => CheckModeAsync(mode, cancellationToken));
        var aiModes = await Task.WhenAll(checks);
        return new[] { ModeAvailability.Available(AnalysisMode.BASIC) }.Concat(aiModes).ToList();
    }

    private async Task<ModeAvailability> CheckModeAsync(AnalysisMode mode, CancellationToken cancellationToken)
    {
        if (FindStyleAnalyzer(mode) is not { } analyzer)
            return ModeAvailability.Unavailable(mode, ModeNotConfiguredReason);
        try
        {
            return await analyzer.CheckAvailabilityAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return ModeAvailability.Unavailable(mode, ModeCheckFailedReason);
        }
    }

    private IStyleAnalyzer? FindStyleAnalyzer(AnalysisMode mode) => _styleAnalyzers.FirstOrDefault(a => a.Mode == mode);

    public Task<AnalysisOutcome> AnalyzeAsync(CancellationToken cancellationToken = default) =>
        AnalyzeAsync(AnalysisMode.BASIC, cancellationToken);

    public async Task<AnalysisOutcome> AnalyzeAsync(AnalysisMode mode, CancellationToken cancellationToken = default)
    {
        if (IsLoadingPhoto)
            return AnalysisOutcome.Error(PhotoLoadInProgressMessage);

        if (mode == AnalysisMode.CLOUD_AI && !HasCloudConsent)
            return AnalysisOutcome.Error(CloudConsentRequiredMessage);

        // Повторний аналіз — як те саме фото заново: попередні аналіз і пропозиції скидаються (FR-13).
        if (Session.State is SessionState.ANALYZED or SessionState.SUGGESTIONS_SHOWN)
            Session.ReplacePhoto(Session.Photo!);

        Session.BeginAnalysis();

        var photo = Session.Photo!;
        var outcome = await RunAnalyzerAsync(photo, cancellationToken);

        if (outcome.Status == AnalysisStatus.Success && mode != AnalysisMode.BASIC)
            outcome = await AddStyleAsync(outcome.Result!, mode, photo, cancellationToken);

        if (outcome.Status == AnalysisStatus.Success)
        {
            try
            {
                Session.CompleteAnalysis(outcome.Result!);
                return outcome;
            }
            catch (ArgumentException)
            {
                outcome = AnalysisOutcome.Error("Аналіз повернув некоректний результат. Спробуйте ще раз.");
            }
        }

        Session.FailAnalysis();
        return outcome;
    }

    private async Task<AnalysisOutcome> AddStyleAsync(AnalysisResult basic, AnalysisMode mode, Photo photo, CancellationToken cancellationToken)
    {
        if (FindStyleAnalyzer(mode) is not { } analyzer)
        {
            _logger?.LogWarning("Режим {Mode}: провайдера ШІ не налаштовано, показано результат без ШІ.", mode);
            return AnalysisOutcome.Success(basic, StyleFailedNotice(mode));
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var answer = await Task.Run(async () =>
                {
                    var image = StyleImage.Prepare(photo.Content);
                    return await analyzer.AnalyzeStyleAsync(image, timeoutCts.Token);
                }, timeoutCts.Token)
                .WaitAsync(StyleTimeout, cancellationToken);

            var decision = StyleAcceptanceRule.Apply(answer);
            return AnalysisOutcome.Success(new AnalysisResult
            {
                Colors = basic.Colors,
                Lighting = basic.Lighting,
                AnalyzedAt = basic.AnalyzedAt,
                Mode = mode,
                Style = decision.Style,
                Confidence = decision.Confidence
            });
        }
        catch (TimeoutException)
        {
            timeoutCts.Cancel();
            _logger?.LogWarning("Режим {Mode}: ШІ не відповів за {Seconds} с, показано результат без ШІ.", mode, StyleTimeout.TotalSeconds);
            return AnalysisOutcome.Success(basic, StyleTimeoutNotice(mode));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AnalysisOutcome.Error("Аналіз скасовано.");
        }
        catch (Exception ex)
        {
            timeoutCts.Cancel();
            _logger?.LogWarning(ex, "Режим {Mode}: помилка ШІ, показано результат без ШІ.", mode);
            return AnalysisOutcome.Success(basic, StyleFailedNotice(mode));
        }
    }

    private async Task<AnalysisOutcome> RunAnalyzerAsync(Photo photo, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
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


    public const string CatalogUnavailableMessage = "Каталог предметів недоступний. Спробуйте пізніше.";

    public const string NoSuitableItemsMessage = "Придатних предметів немає. Спробуйте надати інше фото.";

    public async Task<ServiceResult> SuggestAsync(CancellationToken cancellationToken = default)
    {
        var analysis = Session.Analysis
            ?? throw new InvalidOperationException("Спочатку виконайте аналіз фото.");

        IReadOnlyList<Suggestion> suggestions;
        try
        {
            var response = await _suggestions.SuggestAsync(AnalysisRequestDto.From(analysis), cancellationToken);

            if (response.Status == SuggestionClientStatus.NoSuitableItems)
                return ServiceResult.Fail(NoSuitableItemsMessage);
            if (response.Status != SuggestionClientStatus.Success)
                return ServiceResult.Fail(CatalogUnavailableMessage);

            suggestions = response.Suggestions
                .OrderBy(s => s.Rank)
                .Take(SuggestionCount)
                .Select(s => s.ToModel())
                .ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; 
        }
        catch (Exception)
        {
            return ServiceResult.Fail(CatalogUnavailableMessage);
        }

        Session.ShowSuggestions(suggestions);
        return ServiceResult.Ok();
    }

    public void PlaceItem(CatalogItem item, int x, int y) =>
        Session.PlaceItem(new Placement(item, x, y));

    // ---------- UC-04. Розмістити предмет на фото ----------

    public PlacementValues PlaceItemAtCenter(CatalogItem item, int itemWidth, int itemHeight)
    {
        var photo = Session.Photo
            ?? throw new InvalidOperationException($"Неможливо розмістити предмет у стані {Session.State}.");

        var start = PlacementGeometry.Initial(photo.Width, photo.Height, itemWidth, itemHeight);
        Session.PlaceItem(new Placement(item, start.X, start.Y, start.Scale));
        return start;
    }


    public PlacementValues UpdatePlacement(int x, int y, decimal scale, int itemWidth, int itemHeight)
    {
        if (Session.State != SessionState.ITEM_PLACED
            || Session.Placement is not { } placement
            || Session.Photo is not { } photo)
            throw new InvalidOperationException($"Неможливо змінити положення предмета у стані {Session.State}.");

        var clamped = PlacementGeometry.Clamp(x, y, scale, photo.Width, photo.Height, itemWidth, itemHeight);
        placement.Move(clamped.X - placement.X, clamped.Y - placement.Y);
        placement.Resize(clamped.Scale);
        return clamped;
    }

    public async Task<ServiceResult> SaveAsync(byte[] pngBytes, CancellationToken cancellationToken = default)
    {
        if (Session.State != SessionState.ITEM_PLACED || Session.Placement is not { } placement
            || Session.Analysis is not { } analysis)
            throw new InvalidOperationException($"Неможливо зберегти результат у стані {Session.State}.");

        GallerySaveResult saved;
        try
        {
            saved = await _gallery.SaveAsync(pngBytes, placement.Item.Id, ResultAnalysisDto.From(analysis), cancellationToken);
        }
        catch (Exception)
        {
            return ServiceResult.Fail("Не вдалося зберегти зображення. Спробуйте ще раз.");
        }

        if (!saved.IsSuccess)
            return ServiceResult.Fail(saved.Message);   
        var work = saved.Result!;
        Session.MarkSaved(new ResultImage
        {
            FileName = $"result-{work.Id}.png",
            Format = "png",
            CreatedAt = work.CreatedAt
        });
        return ServiceResult.Ok();
    }
}
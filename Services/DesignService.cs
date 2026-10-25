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


    // ---------- UC-01. Завантажити фото приміщення ----------

    public const string PhotoReplaceBlockedMessage =
        "Під час аналізу замінити фото не можна. Дочекайтеся завершення аналізу.";

    public const string PhotoShowFailedMessage =
        "Не вдалося показати фото. Спробуйте інший файл.";

    public const string PhotoLoadInProgressMessage =
        "Зачекайте: попереднє фото ще завантажується.";

    /// <summary>
    /// true, поки LoadPhotoAsync чекає на полотно. У цей час друге завантаження
    /// й аналіз відхиляються, щоб полотно й сесія не розійшлися.
    /// </summary>
    public bool IsLoadingPhoto { get; private set; }

    /// <summary>
    /// Перевіряє розмір файла ДО його читання (NFR-06): сторінка знає розмір одразу,
    /// тож завеликий файл відхиляється з нормальною причиною, а не з IOException.
    /// </summary>
    public ServiceResult CheckFileSize(long sizeBytes) =>
        sizeBytes > Photo.MaxSizeBytes
            ? ServiceResult.Fail(Photo.TooLargeMessage)
            : ServiceResult.Ok();

    /// <summary>
    /// Основний сценарій UC-01 у правильному порядку:
    /// перевірка (NFR-06) → показ на полотні → зміна сесії (PHOTO_LOADED, FR-13).
    /// Невалідне фото не потрапляє ні на полотно, ні в сесію (A2, A4).
    /// </summary>
    /// <param name="showOnCanvas">Як показати фото на екрані (на сторінці — PhotoCanvas.SetPhotoAsync).</param>
    public async Task<ServiceResult> LoadPhotoAsync(string fileName, byte[] content, Func<byte[], Task> showOnCanvas)
    {
        // Одночасно — лише одне завантаження: інакше вони можуть завершитися не по черзі
        // (на полотні фото B, а в сесії — A).
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
            // Не вдалося — користувач усе одно побачить повідомлення про відмову.
        }
    }

    /// <summary>Записує вже прочитане фото в сесію, якщо воно відповідає NFR-06.</summary>
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

    /// <summary>
    /// Визначає справжній формат і розміри за ВМІСТОМ файла, а не за ContentType з браузера,
    /// який легко підробити (напр., WebP, перейменований у .jpg).
    /// Image.Identify читає лише заголовок файла, без повного декодування. Усе — в пам'яті (NFR-04).
    /// </summary>
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

    public async Task<AnalysisOutcome> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        // UC-01: поки нове фото завантажується, аналіз не запускаємо — сесія лишається без змін.
        if (IsLoadingPhoto)
            return AnalysisOutcome.Error(PhotoLoadInProgressMessage);

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

        // Спершу обчислюємо (може кинути виняток), потім змінюємо: розміщення не лишиться «наполовину».
        var clamped = PlacementGeometry.Clamp(x, y, scale, photo.Width, photo.Height, itemWidth, itemHeight);
        placement.Move(clamped.X - placement.X, clamped.Y - placement.Y);
        placement.Resize(clamped.Scale);
        return clamped;
    }

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

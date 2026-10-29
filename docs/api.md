# Внутрішній API v1.0

HTTP API застосунок **не має**: Blazor Server обмінюється з браузером через власне з'єднання SignalR (`/_blazor`), а ззовні доступні лише статичні файли (`wwwroot/`). Нижче — програмні інтерфейси між шарами: їх викликає сторінка або вони реалізують Use Case. Реалізація — [implementation.md](implementation.md).

## 1. `DesignService` — оркестратор сценарію

Єдиний сервіс, який викликає сторінка `Home.razor`. Scoped: один екземпляр (і одна `DesignSession`) на з'єднання.

| Метод | UC | Що робить | Результат / помилки |
|-------|----|-----------|---------------------|
| `CheckFileSize(long sizeBytes)` | UC-01 | перевірка розміру до читання файла (NFR-06) | `ServiceResult`; відмова — «розмір файла більший за 10 МБ» |
| `LoadPhotoAsync(string fileName, byte[] content, Func<byte[], Task> showOnCanvas)` | UC-01 | перевірка за вмістом → показ на полотні → `PHOTO_LOADED` | `ServiceResult`; при відмові сесія не змінюється (A2) |
| `LoadPhoto(Photo photo)` | UC-01 | запис уже прочитаного фото в сесію | `ServiceResult` |
| `AnalyzeAsync(CancellationToken)` | UC-02 | блокує заміну фото, викликає аналізатор із тайм-аутом 30 с | `AnalysisOutcome`: `Success` → `ANALYZED`; `InsufficientData` (A1) / `Error` (E1) → `PHOTO_LOADED` |
| `Suggest()` | UC-03 | добір 4 пропозицій із каталогу | `ServiceResult`; немає предметів (R5) або каталог недоступний (E1) → стан не змінюється |
| `PlaceItemAtCenter(CatalogItem item, int itemWidth, int itemHeight)` | UC-04 | початкове положення по центру фото (AC-08) | `PlacementValues(X, Y, Scale)`; стан `ITEM_PLACED` |
| `UpdatePlacement(int x, int y, decimal scale, int itemWidth, int itemHeight)` | UC-04 | нове положення з обмеженням межами фото (AC-09) | `PlacementValues` — фактично застосовані значення |
| `SaveAsync(byte[] pngBytes, CancellationToken)` | UC-04 | збереження PNG (FR-11) | `ServiceResult`; успіх → `SAVED`, помилка → `ITEM_PLACED` (E1) |

Властивості: `Session` (поточна `DesignSession`), `IsLoadingPhoto`, `AnalysisTimeout` (30 с; у тестах можна зменшити).

## 2. Інтерфейси сервісів

```csharp
public interface IPhotoAnalyzer   // UC-02, реалізація — PhotoAnalyzer
{
    Task<AnalysisOutcome> AnalyzeAsync(Photo photo, CancellationToken cancellationToken = default);
}

public interface ICatalog         // UC-03, реалізація — Catalog
{
    IReadOnlyList<CatalogItem> Items { get; }
    IReadOnlyList<Suggestion> Select(AnalysisResult analysis, int count);
}

public interface IResultStorage   // UC-04, реалізація — FileResultStorage
{
    Task<ResultImage> SaveAsync(byte[] pngBytes, CancellationToken cancellationToken = default);
}
```

- `AnalysisOutcome` — `Success(result)`, `InsufficientData()` або `Error(message)`; аналізатор не кидає винятків назовні для очікуваних випадків.
- `Catalog.Select` повертає порожній список, якщо придатних предметів немає; `CatalogException` — якщо каталог не завантажується.
- Заміна реалізації — один рядок у `Program.cs`; заглушки — `Services/Stubs/`.

## 3. Полотно: JS Interop

`Components/Shared/PhotoCanvas.razor` — обгортка над модулем `wwwroot/js/photoCanvas.js` (Fabric.js 5.3.0).

| C# (`PhotoCanvas`) | JS (`photoCanvas.js`) | Призначення |
|--------------------|------------------------|-------------|
| `SetPhotoAsync(byte[])` | `setPhoto(streamRef)` | фото як фон; байти передаються потоком (`DotNetStreamReference`) |
| `SetItemAsync(string url, bool hidden)` → `ItemSize?` | `setItem(url, hidden)` → `{ width, height }` | показати предмет, повернути природні розміри PNG для розрахунку центру |
| `ApplyPlacementAsync(PlacementValues)` | `applyPlacement(x, y, scale)` | застосувати положення, обчислене в C# |
| `SetLockedAsync(bool)` | `setLocked(locked)` | заблокувати предмет після збереження |
| `ExportPngAsync()` → `byte[]` | `exportPng()` → `Uint8Array` | PNG «фото + предмет» у роздільності оригіналу; повертається потоком (`IJSStreamReference`) |
| `[JSInvokable] OnItemModified(x, y, scale, itemWidth, itemHeight)` → `PlacementValues` | виклик із `onModified` після відпускання предмета | синхронізація положення з `Placement` (FR-09) |
| `DisposeAsync()` | `dispose()` | звільнення полотна |

Модуль створюється функцією `create(host, dotNetRef)`. Координати в обидва боки — у пікселях **оригінального** фото; масштаб екрана враховує JS.

## 4. Формат даних

- **Каталог:** `wwwroot/data/catalog.json` — масив `CatalogItem` (camelCase, enum рядками); опис полів — [data.md](data.md#12-формат-catalogjson). Читання — `CatalogLoader.LoadFromFile(path)` → `IReadOnlyList<CatalogItem>` або `CatalogException` з переліком усіх помилок.
- **Результат:** PNG у `output/result-<yyyyMMdd-HHmmss>-<8 символів GUID>.png`; метадані — `ResultImage` (`FileName`, `Format`, `CreatedAt`).
- **Фото користувача** зберігається лише в пам'яті (`Photo.Content`) і на диск не пишеться (NFR-04).

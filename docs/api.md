# API v2.0 (чернетка S1)

> Чернетка контракту для спринту S1. Погоджується командою **до** паралельної роботи; зміни — через PR у цей файл.

Застосунок має два способи взаємодії:

- **HTTP API** (`/api/...`) — усе, що працює зі збереженими даними або з малими даними без фото: каталог, добір, галерея. Його викликають клієнти в `Clients/` (сторінки Blazor), а також будь-який зовнішній клієнт (JMeter, Postman, тести).
- **Внутрішні сервіси** (розділ 2) — фото, аналіз, полотно, стан сесії. Фото через HTTP API **не передається** (NFR-04).

## 1. HTTP API

Формат — JSON (UTF-8), дати — ISO 8601 UTC. Помилки — у форматі ProblemDetails:

```json
{ "status": 413, "title": "Зображення завелике", "detail": "Максимальний розмір — 5 МБ." }
```

| Метод і адреса | Успіх | Відмови | Задача |
|---|---|---|---|
| `GET /api/catalog` | `200` + `CatalogItemDto[]` | — | 2 |
| `POST /api/suggestions` | `200` + `SuggestionDto[]` | `400` некоректний запит · `422` придатних предметів немає · `503` каталог недоступний | 2 |
| `POST /api/results` | `201` + `ResultDto`, заголовок `Location` | `400` не PNG / немає предмета · `413` > 5 МБ · `429` забагато збережень · `500` помилка запису | 3 |
| `GET /api/results` | `200` + `ResultDto[]` (нові спершу) | — | 3 |
| `GET /api/results/{id}/image` | `200` + `image/png` | `404` | 3 |
| `GET /api/results/{id}/thumbnail` | `200` + `image/jpeg` | `404` | 3 |
| `DELETE /api/results/{id}` | `204` | `404` | 3 |

### 1.1 DTO

```jsonc
// CatalogItemDto
{
  "id": "sofa-01",
  "name": "Сірий диван",
  "type": "SOFA",
  "imageUrl": "/images/catalog/sofa-01.png",
  "mainColor": "#82817A",
  "suitableLightings": ["DARK", "MODERATE", "BRIGHT"],
  "licenseSource": "pngimg.com, CC BY-NC 4.0, ..."
}

// AnalysisRequestDto — тіло POST /api/suggestions (результат аналізу, без фото)
{
  "colors": [ { "hex": "#D8C3A5", "hue": 36, "share": 61.2 }, { "hex": "#322D28", "hue": 30, "share": 30.6 } ],
  "lighting": "MODERATE"
}

// SuggestionDto
{
  "rank": 1,
  "score": 2,
  "reason": "Аналогова гармонія з домінантним кольором.",
  "partialMatch": false,
  "item": { /* CatalogItemDto */ }
}

// ResultDto
{
  "id": 7,
  "createdAt": "2026-11-14T18:20:00Z",
  "catalogItemId": "sofa-01",
  "itemName": "Сірий диван",
  "imageUrl": "/api/results/7/image",
  "thumbnailUrl": "/api/results/7/thumbnail"
}
```

### 1.2 `POST /api/results`

`multipart/form-data`:

| Поле | Тип | Обов'язкове | Перевірка |
|---|---|---|---|
| `image` | файл `image/png` | так | PNG за вмістом, ≤ 5 МБ |
| `catalogItemId` | рядок | так | предмет існує в каталозі |

Після збереження діє правило **R7**: якщо робіт більше за ліміт (`Gallery:MaxItems`, за замовчуванням 100), найстаріші видаляються в тій самій транзакції; щойно збережена не видаляється. Частота — не більше `Gallery:SavesPerMinute` (5) з однієї адреси.

### 1.3 Налаштування (`.env`)

| Ключ | Приклад | Призначення |
|---|---|---|
| `ConnectionStrings__Default` | `Host=...;Database=aidesigner_dev;Username=...;Password=...;SSL Mode=Require` | рядок підключення до бази даних |
| `ASPNETCORE_ENVIRONMENT` | `Development` | у Development міграції застосовуються автоматично |
| `ApiBaseUrl` | `https://localhost:7003/` | адреса власного API для клієнтів |
| `Gallery__MaxItems` | `100` | ліміт галереї (R7) |
| `Gallery__MaxImageBytes` | `5242880` | максимальний розмір зображення |
| `Gallery__SavesPerMinute` | `5` | частота збережень з однієї адреси |


## 2. Внутрішні сервіси v1.0 (без змін)

### 2.1 `DesignService` — оркестратор сценарію

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

### 2.2 Інтерфейси сервісів

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

### 2.3 Полотно: JS Interop

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

> У S1 `ICatalog` і `IResultStorage` замінюються на `SuggestionService` / `ICatalogRepository` і `ResultService` / `IResultRepository` (задачі 2 і 3); цей розділ оновлюється разом із кодом.
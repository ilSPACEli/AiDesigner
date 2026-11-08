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
| `POST /api/results` | `201` + `ResultDto`, заголовок `Location` (адреса зображення, `/api/results/{id}/image`) | `400` не PNG, немає файла чи предмета, предмета немає в каталозі · `413` > 5 МБ або > 50 Мпікс · `429` забагато збережень (заголовок `Retry-After`) · `500` помилка запису | 3 |
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
| `image` | файл `image/png` | так | PNG за вмістом (не за розширенням і не за `Content-Type`), ≤ 5 МБ, ≤ 50 мегапікселів (ширина × висота) |
| `catalogItemId` | рядок | так | предмет існує в каталозі |

Після збереження діє правило **R7**: якщо робіт більше за ліміт (`Gallery:MaxItems`, за замовчуванням 100), найстаріші видаляються в тій самій транзакції; щойно збережена не видаляється. Частота — не більше `Gallery:SavesPerMinute` (5) з однієї адреси. Докладніше про сховище й ліміти — [data.md](data.md#35-збереження-результатів-r7-і-ліміти-us-1-44).

Усі відмови — `application/problem+json` з полями `status`, `title`, `detail`. Приклади `detail`:

| Статус | `title` | `detail` |
|---|---|---|
| `400` | Некоректний запит | «Не передано файл у полі image.» · «Не вказано предмет каталогу.» · «Такого предмета немає в каталозі.» · «Дані не є зображенням PNG або файл пошкоджений.» |
| `413` | Зображення завелике | «Максимальний розмір — 5 МБ.» · «Роздільність зображення завелика: не більше 50 мегапікселів.» |
| `429` | Забагато збережень | «Ліміт збережень — 5 за хвилину. Спробуйте пізніше.» + заголовок `Retry-After` (секунди) |
| `500` | Помилка збереження | «Не вдалося зберегти зображення. Спробуйте ще раз.» |

#### Відхилення від початкового контракту

Реалізація в #44 уточнює чернетку контракту. Що саме змінилося й чому:

- **`Location`** вказує на `/api/results/{id}/image`, а не на `/api/results/{id}`: окремого `GET` за `id` у контракті немає, а за адресою зображення збережену роботу можна одразу отримати.
- **`413` також за роздільністю.** Малий за розміром PNG може розпакуватися у величезне зображення («бомба декомпресії»), тому ширина × висота перевіряються за заголовком PNG ще до декодування (50 Мпікс, як в аналізі фото). Це другий привід для `413` поряд із розміром у байтах.
- **`429` містить `Retry-After`** — число секунд до наступної спроби.
- **`400` також для невідомого предмета** (`catalogItemId` не знайдено в каталозі) і для запиту, що не є `multipart/form-data`.
- **`id` у шляху — ціле число.** Адреси на кшталт `/api/results/abc/image` не відповідають жодному маршруту, відповідь — порожній `404` без тіла ProblemDetails.
- **Частота рахується за IP-адресою клієнта.** Збереження зі сторінки йде через `GalleryClient` на той самий сервер (`127.0.0.1`), тому для користувачів інтерфейсу ліміт спільний; перевіряти AC-24 слід прямими викликами API.
- **Антифорджері вимкнено на `POST /api/results`** (`DisableAntiforgery`): ендпоінт призначений для API-клієнтів без форм і cookie; для Blazor антифорджері лишається.

### 1.3 Налаштування (`.env`)

| Ключ | Приклад | Призначення |
|---|---|---|
| `ConnectionStrings__Default` | `Host=...;Database=aidesigner_dev;Username=...;Password=...;SSL Mode=Require` | рядок підключення до бази даних |
| `ASPNETCORE_ENVIRONMENT` | `Development` | у Development міграції застосовуються автоматично |
| `ApiBaseUrl` | `http://localhost:5245/` | адреса власного API для клієнтів; поки що HTTP, порт профілю `http` (`dotnet run`) |
| `Gallery__MaxItems` | `100` | ліміт галереї (R7) |
| `Gallery__MaxImageBytes` | `5242880` | максимальний розмір зображення |
| `Gallery__SavesPerMinute` | `5` | частота збережень з однієї адреси |

Значення `Gallery__*`, не більші за 0, зупиняють застосунок під час старту (`OptionsValidationException`).


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
| `SaveAsync(byte[] pngBytes, CancellationToken)` | UC-04 | збереження PNG у галерею через `IGalleryClient` (FR-11, US-1); `catalogItemId` береться з `Session.Placement` | `ServiceResult`; успіх → `SAVED`, помилка → `ITEM_PLACED` (E1); відмова API (`413`, `429`, `400`) повертає текст `detail` із ProblemDetails |

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

public interface IGalleryClient   // US-1, реалізація — GalleryClient (HTTP до власного API)
{
    Task<GallerySaveResult> SaveAsync(byte[] pngBytes, string catalogItemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResultDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);   // false — 404
}

public interface IResultStorage   // v1: з S1 (#44) DesignService його не використовує; лишається до окремого прибирання
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
- **Результат:** рядок таблиці `SavedResults` (PNG і мініатюра JPEG), див. [data.md](data.md#31-таблиці) і [3.5](data.md#35-збереження-результатів-r7-і-ліміти-us-1-44). У сесії лишається `ResultImage`: `FileName` = `result-<id>.png` (умовна назва для завантаження), `Format`, `CreatedAt`.
- **Фото користувача** зберігається лише в пам'яті (`Photo.Content`) і на диск не пишеться (NFR-04).

> У S1 `ICatalog` замінюється на `SuggestionService` / `ICatalogRepository` (задача 2); `IResultStorage` уже замінено на `ResultService` / `IResultRepository` / `GalleryClient` (задача 3, #44). Цей розділ оновлюється разом із кодом.
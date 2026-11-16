# API v2.0 (S1 + чернетка S2)

> Контракт S1 реалізовано. Зміни S2 (стиль кімнати, режими аналізу, аналіз разом із роботою) позначено **(S2)** — чернетка, погоджується командою **до** паралельної роботи; зміни — через PR у цей файл.

Застосунок має два способи взаємодії:

- **HTTP API** (`/api/...`) — усе, що працює зі збереженими даними або з малими даними без фото: каталог, добір, галерея. Його викликають клієнти в `Clients/` (сторінки Blazor), а також будь-який зовнішній клієнт (JMeter, Postman, тести).
- **Внутрішні сервіси** (розділ 2) — фото, аналіз, полотно, стан сесії. Фото через HTTP API **не передається** (NFR-04). **(S2)** Єдиний виняток для фото — режим «Хмарний ШІ»: сервер (не браузер) надсилає фото в Google Gemini лише після згоди Користувача (NFR-04, CR-05, AC-31); ключ зберігається тільки на сервері.

## 1. HTTP API

Формат — JSON (UTF-8), дати — ISO 8601 UTC. Помилки — у форматі ProblemDetails:

```json
{ "status": 413, "title": "Зображення завелике", "detail": "Максимальний розмір — 5 МБ." }
```

| Метод і адреса | Успіх | Відмови | Задача |
|---|---|---|---|
| `GET /api/catalog` | `200` + `CatalogItemDto[]` | — | 2 |
| `POST /api/suggestions` | `200` + `SuggestionDto[]` | `400` некоректний запит · `422` придатних предметів немає · `503` каталог недоступний | 2 |
| `POST /api/results` | `201` + `ResultDto`, заголовок `Location` (адреса зображення, `/api/results/{id}/image`) | `400` не PNG, немає файла чи предмета, предмета немає в каталозі, **(S2)** немає або некоректний `analysis` · `413` > 5 МБ або > 50 Мпікс · `429` забагато збережень (заголовок `Retry-After`) · `500` помилка запису (**(S2)** роботу й аналіз не збережено — FR-21) | 3 |
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
  "licenseSource": "pngimg.com, CC BY-NC 4.0, ...",
  "style": "MODERN"                        // (S2) обов'язкове, одне з переліку стилів (розд. 1.5)
}

// AnalysisRequestDto — тіло POST /api/suggestions (результат аналізу, без фото)
{
  "colors": [ { "hex": "#D8C3A5", "hue": 36, "share": 61.2 }, { "hex": "#322D28", "hue": 30, "share": 30.6 } ],
  "lighting": "MODERATE",
  "style": "LOFT"                          // (S2) необов'язкове: null або відсутнє — «стиль не визначено», R6 не діє
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
  "thumbnailUrl": "/api/results/7/thumbnail",
  "style": "LOFT",                         // (S2) null — «стиль не визначено» або робота збережена до S2
  "analysisMode": "CLOUD_AI"               // (S2) null — робота збережена до S2
}

// (S2) ResultAnalysisDto — поле analysis у POST /api/results (результат аналізу, без фото)
{
  "mode": "CLOUD_AI",                      // BASIC | LOCAL_AI | CLOUD_AI
  "lighting": "MODERATE",
  "colors": [ { "hex": "#D8C3A5", "hue": 36, "share": 61.2 }, { "hex": "#322D28", "hue": 30, "share": 30.6 }, { "hex": "#9A8F80", "hue": 35, "share": 8.2 } ],
  "style": "LOFT",                         // null — «стиль не визначено» (R8 не прийняв або режим BASIC)
  "confidence": 0.82                       // null для BASIC; для LOCAL_AI / CLOUD_AI — 0–1, null лише якщо ШІ не дав коректної відповіді
}
```

### 1.2 `POST /api/results`

`multipart/form-data`:

| Поле | Тип | Обов'язкове | Перевірка |
|---|---|---|---|
| `image` | файл `image/png` | так | PNG за вмістом (не за розширенням і не за `Content-Type`), ≤ 5 МБ, ≤ 50 мегапікселів (ширина × висота) |
| `catalogItemId` | рядок | так | предмет існує в каталозі |
| `analysis` **(S2)** | рядок JSON (`ResultAnalysisDto`) | так | ті самі перевірки `lighting` і `colors`, що для `POST /api/suggestions` (розд. 1.3); `mode` — з переліку (фактичний режим результату: якщо ШІ не відповів і показано результат без ШІ, AC-30 — `BASIC`); `BASIC` → `style` і `confidence` — null; `LOCAL_AI` / `CLOUD_AI` → `confidence` у межах 0–1 або null (зламана / порожня відповідь ШІ, AC-28); `style` ≠ null → `confidence` обов'язкова й ≥ 0,6 (узгоджено з R8) |

**(S2) Атомарність (FR-21).** Робота (`SavedResults`) і аналіз (`ResultAnalyses`) записуються в **одній транзакції** разом із витісненням за R7: якщо запис аналізу не вдався — немає ні роботи, ні аналізу, витіснення теж скасовано; відповідь `500`.

Після збереження діє правило **R7**: якщо робіт більше за ліміт (`Gallery:MaxItems`, за замовчуванням 100), найстаріші видаляються в тій самій транзакції; щойно збережена не видаляється. Частота — не більше `Gallery:SavesPerMinute` (5) з однієї адреси. Докладніше про сховище й ліміти — [data.md](data.md#35-збереження-результатів-r7-і-ліміти-us-1-44).

Усі відмови — `application/problem+json` з полями `status`, `title`, `detail`. Приклади `detail`:

| Статус | `title` | `detail` |
|---|---|---|
| `400` | Некоректний запит | «Не передано файл у полі image.» · «Не вказано предмет каталогу.» · «Такого предмета немає в каталозі.» · «Дані не є зображенням PNG або файл пошкоджений.» · **(S2)** «Не передано результат аналізу в полі analysis.» · «analysis.style: невідомий стиль.» (та ін. з назвою поля) |
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

### 1.3 `GET /api/catalog` і `POST /api/suggestions`

`GET /api/catalog` повертає всі предмети з таблиці `CatalogItems` (робоча копія `catalog.json`, [data.md](data.md#33-синхронізація-каталогу)), упорядковані за `id`.

`POST /api/suggestions` приймає результат аналізу (`AnalysisRequestDto`, без фото — NFR-04) і повертає 3–4 пропозиції за правилами R1–R5 (`SuggestionRules`). Перевірка запиту:

| Поле | Перевірка |
|---|---|
| `lighting` | точно `DARK`, `MODERATE` або `BRIGHT` |
| `colors` | від 3 до 5 елементів |
| `colors[i].hex` | `#RRGGBB` |
| `colors[i].hue` | ціле 0–359 |
| `colors[i].share` | 0–100 (відсоток) |
| `style` **(S2)** | відсутнє, `null` або одне зі значень розд. 1.5; інше — `400` «style: невідомий стиль.» |

**(S2)** Якщо `style` задано, до балу R4 додається R6 (+1 за збіг зі стилем предмета); `score` у `SuggestionDto` — уже з R6, у `reason` додається «Стиль збігається зі стилем кімнати.».

`partialMatch: true` — предмет додано лише за освітленістю (R5, бал 0); `reason` тоді — «Часткова відповідність за кольором: предмет підходить лише за освітленістю.»

| Статус | `title` | `detail` |
|---|---|---|
| `400` | Некоректний запит | «Очікується JSON із полями colors і lighting.» або назва поля з причиною, напр. «colors[2].hex: очікується формат #RRGGBB.» |
| `422` | Придатних предметів немає | «Для цього освітлення в каталозі немає придатних предметів.» |
| `503` | Каталог недоступний | «Каталог предметів недоступний. Спробуйте пізніше.» — БД не відповідає або таблиця `CatalogItems` порожня (каталог не синхронізовано) |

Клієнти: `SuggestionClient` повертає `SuggestionClientResult` зі статусом `Success` / `NoSuitableItems` (422) / `Unavailable` (503 і будь-яка інша відмова); помилки мережі не ковтає — їх обробляє `DesignService`. `CatalogClient.GetAllAsync()` — увесь каталог.

### 1.4 Налаштування (`.env`)

| Ключ | Приклад | Призначення |
|---|---|---|
| `ConnectionStrings__Default` | `Host=...;Database=aidesigner_dev;Username=...;Password=...;SSL Mode=Require` | рядок підключення до бази даних |
| `ASPNETCORE_ENVIRONMENT` | `Development` | у Development міграції застосовуються автоматично |
| `ApiBaseUrl` | `http://localhost:5245/` | адреса власного API для клієнтів; поки що HTTP, порт профілю `http` (`dotnet run`) |
| `Gallery__MaxItems` | `100` | ліміт галереї (R7) |
| `Gallery__MaxImageBytes` | `5242880` | максимальний розмір зображення |
| `Gallery__SavesPerMinute` | `5` | частота збережень з однієї адреси |

| `Ai__Gemini__ApiKey` **(S2)** | *(секрет, лише в `.env`)* | ключ Google AI Studio; порожній — «Хмарний ШІ» вимкнено |
| `Ai__Gemini__Model` **(S2)** | `gemini-3.5-flash` | модель хмарного ШІ; має бути в переліку моделей ключа з підтримкою `generateContent` (старі моделі Google з часом вимикає для нових ключів — відповідь 404) |
| `Ai__Ollama__BaseUrl` **(S2)** | `http://localhost:11434/` | адреса Ollama; не відповідає — «Локальний ШІ» вимкнено |
| `Ai__Ollama__Model` **(S2)** | `moondream` | необов'язково; порожній — перша модель із підтримкою зображень |

Значення `Gallery__*`, не більші за 0, зупиняють застосунок під час старту (`OptionsValidationException`). Поріг R8 (0,6) — константа в коді, не налаштування.

### 1.5 Перелік значень (S2)

| Перелік | JSON-значення | Українською (UI) |
|---|---|---|
| Стиль (`RoomStyle`) | `SCANDINAVIAN` · `MINIMALISM` · `MODERN` · `CLASSIC` · `LOFT` · `BOHO` | Скандинавський · Мінімалізм · Сучасний · Класичний · Лофт · Бохо |
| Режим аналізу (`AnalysisMode`) | `BASIC` · `LOCAL_AI` · `CLOUD_AI` | Аналіз без ШІ · Локальний ШІ · Хмарний ШІ |

Значення — рядками, як `LightingLevel` і `ItemType`. «Стиль не визначено» — `null`, окремого значення немає.


## 2. Внутрішні сервіси

### 2.1 `DesignService` — оркестратор сценарію

Єдиний сервіс, який викликає сторінка `Home.razor`. Scoped: один екземпляр (і одна `DesignSession`) на з'єднання.

| Метод | UC | Що робить | Результат / помилки |
|-------|----|-----------|---------------------|
| `CheckFileSize(long sizeBytes)` | UC-01 | перевірка розміру до читання файла (NFR-06) | `ServiceResult`; відмова — «розмір файла більший за 10 МБ» |
| `LoadPhotoAsync(string fileName, byte[] content, Func<byte[], Task> showOnCanvas)` | UC-01 | перевірка за вмістом → показ на полотні → `PHOTO_LOADED` | `ServiceResult`; при відмові сесія не змінюється (A2) |
| `LoadPhoto(Photo photo)` | UC-01 | запис уже прочитаного фото в сесію | `ServiceResult` |
| `AnalyzeAsync(CancellationToken)` | UC-02 | блокує заміну фото, викликає аналізатор із тайм-аутом 30 с | `AnalysisOutcome`: `Success` → `ANALYZED`; `InsufficientData` (A1) / `Error` (E1) → `PHOTO_LOADED` |
| `AnalyzeAsync(AnalysisMode mode, CancellationToken)` **(S2)** | UC-02 | палітра й освітленість — як у v1; у режимах ШІ — ще стиль (R8); тайм-аут 30 с на ШІ (`StyleTimeout`) | `AnalysisOutcome`: `Result.Mode`, `Style` (або `null` — «стиль не визначено»), `Confidence`; ШІ не відповів → `Result.Mode = BASIC` і `Notice` (AC-30); `CLOUD_AI` без згоди → відмова без виклику ШІ (AC-31). `AnalyzeAsync(CancellationToken)` = `BASIC` |
| `GetModesAsync()` **(S2)** | UC-02 | доступність режимів | `ModeAvailability[]` у порядку BASIC, LOCAL_AI, CLOUD_AI; BASIC доступний завжди (AC-26) |
| `GiveCloudConsent()`, `HasCloudConsent` **(S2)** | UC-02 | згода на передачу фото Google Gemini — до кінця сесії | без згоди `AnalyzeAsync(CLOUD_AI)` → `Error(CloudConsentRequiredMessage)`, сесія не змінюється (AC-31) |
| `SuggestAsync(CancellationToken)` | UC-03 | добір до 4 пропозицій через `ISuggestionClient` (`POST /api/suggestions`); на сервер іде лише результат аналізу | `ServiceResult`; `422` → «Придатних предметів немає…» (R5), `503`, інша відмова чи помилка мережі → «Каталог предметів недоступний…» (E1); стан не змінюється |
| `PlaceItemAtCenter(CatalogItem item, int itemWidth, int itemHeight)` | UC-04 | початкове положення по центру фото (AC-08) | `PlacementValues(X, Y, Scale)`; стан `ITEM_PLACED` |
| `UpdatePlacement(int x, int y, decimal scale, int itemWidth, int itemHeight)` | UC-04 | нове положення з обмеженням межами фото (AC-09) | `PlacementValues` — фактично застосовані значення |
| `SaveAsync(byte[] pngBytes, CancellationToken)` | UC-04 | збереження PNG у галерею через `IGalleryClient` (FR-11, US-1); `catalogItemId` береться з `Session.Placement`; **(S2)** аналіз — з `Session.Analysis` → `ResultAnalysisDto` → `IGalleryClient.SaveAsync(png, catalogItemId, analysis)` (FR-21) | `ServiceResult`; успіх → `SAVED`, помилка → `ITEM_PLACED` (E1); відмова API (`413`, `429`, `400`) повертає текст `detail` із ProblemDetails |

Властивості: `Session` (поточна `DesignSession`), `IsLoadingPhoto`, `AnalysisTimeout` (30 с; у тестах можна зменшити).

### 2.2 Інтерфейси сервісів

```csharp
public interface IPhotoAnalyzer   // UC-02, реалізація — PhotoAnalyzer
{
    Task<AnalysisOutcome> AnalyzeAsync(Photo photo, CancellationToken cancellationToken = default);
}

public interface ISuggestionClient   // UC-03, реалізація — SuggestionClient (HTTP до власного API)
{
    Task<SuggestionClientResult> SuggestAsync(AnalysisRequestDto request, CancellationToken cancellationToken = default);
}

public interface ICatalogClient      // GET /api/catalog, реалізація — CatalogClient
{
    Task<IReadOnlyList<CatalogItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
}

public interface ICatalogRepository  // таблиця CatalogItems; реалізації — EfCatalogRepository, InMemoryCatalogRepository (тести)
{
    Task<IReadOnlyList<CatalogItem>> GetAllAsync(CancellationToken cancellationToken = default);   // упорядковано за Id
    Task<CatalogItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
}

public interface IGalleryClient   // US-1, реалізація — GalleryClient (HTTP до власного API)
{
    // (S2) analysis — поле analysis у multipart (FR-21)
    Task<GallerySaveResult> SaveAsync(byte[] pngBytes, string catalogItemId, ResultAnalysisDto analysis, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResultDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);   // false — 404
}

public interface IStyleAnalyzer   // (S2) реалізації — GeminiStyleAnalyzer (CLOUD_AI), OllamaStyleAnalyzer (LOCAL_AI)
{
    AnalysisMode Mode { get; }
    Task<ModeAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default);   // (Mode, IsAvailable, Reason)
    Task<string> AnalyzeStyleAsync(byte[] image, CancellationToken cancellationToken = default);   // image — JPEG ≤ 1024 px (StyleImage); сира відповідь моделі; збій — виняток
}
// Відповідь ШІ перевіряє правило R8 (StyleAcceptanceRule) поза провайдером: однаково для всіх режимів.

public interface IResultStorage   // v1: з S1 (#44) DesignService його не використовує; лишається до окремого прибирання
{
    Task<ResultImage> SaveAsync(byte[] pngBytes, CancellationToken cancellationToken = default);
}
```

- `AnalysisOutcome` — `Success(result)`, `InsufficientData()` або `Error(message)`; аналізатор не кидає винятків назовні для очікуваних випадків.
- `SuggestionService.SuggestAsync` (сервер) повертає `SuggestionOutcome`: `Ok`, `NoSuitableItems` або `CatalogUnavailable` — очікувані відмови значенням, а не винятком. Правила R1–R5 — `SuggestionRules.Select` (перенесено з v1 `Catalog.Select` без змін логіки).
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

> У S1 `ICatalog` / `Catalog` замінено на `SuggestionClient` → `POST /api/suggestions` → `SuggestionService` → `ICatalogRepository` (задача 2, #43); `IResultStorage` — на `ResultService` / `IResultRepository` / `GalleryClient` (задача 3, #44). Заглушка добору для тестів — `Services/Stubs/StubSuggestionClient`.
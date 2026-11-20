# Діаграма класів реалізації

**Що показує:** фактичні класи коду (сторінки, клієнти, ендпоінти, сервіси, репозиторії) і залежності між ними — для частин, що змінюються в S3 (ЛР №7). Предметна модель і таблиці БД — [model/class-diagram.md](../../model/class-diagram.md).

**Стан:** розд. 1–2 — після S2, початковий коміт рефакторингу S3 `d9709ab`; розд. 3 — після рефакторингу (лише змінені класи й залежності).

## 1. Галерея — до рефакторингу (S2)

Сторінка `Gallery.razor` сама звертається до клієнта й трьох помічників; помічники лежать у серверному шарі `Services/Results`, `Services/Modes`.

```mermaid
classDiagram
    direction LR

    class Gallery {
        <<razor page>>
        -items: List~GalleryItemView~
        -zone: TimeZoneInfo
        -LoadAsync()
        -DeleteAsync(id: int)
        -ResolveZoneAsync() TimeZoneInfo
    }
    class IGalleryClient {
        <<interface>>
        +SaveAsync(png: byte[], catalogItemId: string, analysis: ResultAnalysisDto) GallerySaveResult
        +GetAllAsync() ResultDto[]
        +DeleteAsync(id: int) bool
    }
    class GalleryClient {
        -http: HttpClient
        -ReadProblemMessageAsync(response) string
    }
    class GalleryItemView {
        <<record>>
        +Id: int
        +ItemName: string
        +DateText: string
        +ThumbnailUrl: string
        +ImageUrl: string
        +DownloadFileName: string
        +Style: StyleLabel
        +From(result: ResultDto, zone: TimeZoneInfo)$ GalleryItemView
    }
    class GalleryErrorMessages {
        <<static>>
        +FromException(ex: Exception)$ string
        +FromStatus(status: HttpStatusCode)$ string
    }
    class BrowserTimeZone {
        <<static>>
        +Resolve(ianaId: string)$ TimeZoneInfo
    }
    class StyleLabel {
        <<record>>
        +For(mode: string, style: string)$ StyleLabel
    }
    class ResultEndpoints {
        <<static>>
        +MapResultEndpoints(app)$
        -GetAllAsync(service) IResult
        -DeleteAsync(id, service) IResult
    }
    class ResultService {
        +SaveAsync(...) SaveOutcome
        +GetAllAsync() SavedResult[]
        +DeleteAsync(id: int) bool
    }
    class IResultRepository {
        <<interface>>
        +AddAsync(result: SavedResult, evictOldest: int) SavedResult
        +GetAllAsync() SavedResult[]
        +DeleteAsync(id: int) bool
    }
    class EfResultRepository
    class InMemoryResultRepository

    Gallery ..> IGalleryClient : викликає
    Gallery ..> GalleryItemView : перетворює ResultDto
    Gallery ..> GalleryErrorMessages : текст помилки
    Gallery ..> BrowserTimeZone : часовий пояс
    GalleryItemView ..> StyleLabel
    GalleryClient ..|> IGalleryClient
    GalleryClient ..> ResultEndpoints : HTTP /api/results
    ResultEndpoints ..> ResultService
    ResultService --> IResultRepository
    EfResultRepository ..|> IResultRepository
    InMemoryResultRepository ..|> IResultRepository
```

## 2. Аналіз стилю (S2)

`DesignService` сам обирає провайдера ШІ за режимом, готує зображення, рахує тайм-аут і застосовує R8 (кандидат на другий фасад — за бажанням).

```mermaid
classDiagram
    direction LR

    class Home {
        <<razor page>>
    }
    class DesignService {
        +GetModesAsync() ModeAvailability[]
        +AnalyzeAsync(mode: AnalysisMode) AnalysisOutcome
        +GiveCloudConsent()
        +SuggestAsync() ServiceResult
        +SaveAsync(png: byte[]) ServiceResult
        -FindStyleAnalyzer(mode) IStyleAnalyzer
        -AddStyleAsync(basic, mode, photo) AnalysisOutcome
    }
    class IPhotoAnalyzer {
        <<interface>>
        +AnalyzeAsync(photo: Photo) AnalysisOutcome
    }
    class IStyleAnalyzer {
        <<interface>>
        +Mode: AnalysisMode
        +CheckAvailabilityAsync() ModeAvailability
        +AnalyzeStyleAsync(image: byte[]) string
    }
    class GeminiStyleAnalyzer
    class OllamaStyleAnalyzer
    class StyleImage {
        <<static>>
        +Prepare(content: byte[])$ byte[]
    }
    class StyleAcceptanceRule {
        <<static>>
        +MinConfidence: decimal = 0.6
        +Apply(answer: string)$ StyleDecision
    }
    class ISuggestionClient {
        <<interface>>
    }
    class IGalleryClient {
        <<interface>>
    }

    Home ..> DesignService
    DesignService --> IPhotoAnalyzer
    DesignService --> "0..*" IStyleAnalyzer : провайдери ШІ
    DesignService ..> StyleImage
    DesignService ..> StyleAcceptanceRule : R8
    DesignService --> ISuggestionClient
    DesignService --> IGalleryClient
    GeminiStyleAnalyzer ..|> IStyleAnalyzer
    OllamaStyleAnalyzer ..|> IStyleAnalyzer
```

## 3. Після рефакторингу (S3)

Шаблон **Фасад** (#52, коміти `refactor:`). `Gallery.razor` звертається лише до `GalleryFacade`; клієнт, перетворення карток, тексти помилок і часовий пояс — за фасадом. Обробка помилок, що повторювалась у кожній дії сторінки, — в одному місці. UI-помічники перенесено із серверного шару `Services/Results`, `Services/Modes` у клієнтський: `Clients/Gallery` (галерея) і `Clients/Design` (сторінка «Дизайн»). Поведінку зафіксовано тестами S3-2 (`GalleryScenarioTests`) — вони проходять до й після.

Показано лише змінене. Нова операція S3-3 (FR-22, коміт `feat:`) — параметр `style` по всьому ланцюжку: `GalleryFacade.LoadAsync` → `IGalleryClient.GetAllAsync` → `GET /api/results?style=` → `ResultService` → `IResultRepository` (`EfResultRepository`, `InMemoryResultRepository`). Невідомий стиль — `400` (AC-38); робіт стилю немає — `Notice` з назвою стилю (AC-37).

```mermaid
classDiagram
    direction LR

    class Gallery {
        <<razor page>>
        -items: List~GalleryItemView~
        -zoneId: string
        -LoadAsync()
        -DeleteAsync(id: int)
        -GetTimeZoneIdAsync() string
    }
    class GalleryFacade {
        -client: IGalleryClient
        +LoadAsync(timeZoneId: string, style: RoomStyle) GalleryLoadResult
        +DeleteAsync(id: int) GalleryDeleteResult
        +NoWorksOfStyleMessage(style: RoomStyle)$ string
    }
    class GalleryLoadResult {
        <<record>>
        +Items: GalleryItemView[]
        +Error: string
        +Notice: string
        +IsSuccess: bool
    }
    class GalleryDeleteResult {
        <<record>>
        +Status: GalleryDeleteStatus
        +Error: string
        +RemoveCard: bool
    }
    class GalleryDeleteStatus {
        <<enumeration>>
        Deleted
        NotFound
        Failed
    }
    class IGalleryClient {
        <<interface>>
        +GetAllAsync(style: RoomStyle) ResultDto[]
        +DeleteAsync(id: int) bool
    }
    class GalleryClient {
        +GetAllAsync(style: RoomStyle) ResultDto[]
    }
    class ResultEndpoints {
        <<static>>
        -GetAllAsync(style: string, service) IResult
    }
    class ResultService {
        +GetAllAsync(style: RoomStyle) SavedResult[]
    }
    class IResultRepository {
        <<interface>>
        +GetAllAsync(style: RoomStyle) SavedResult[]
    }
    class GalleryItemView {
        <<record>>
        +From(result: ResultDto, zone: TimeZoneInfo)$ GalleryItemView
    }
    class GalleryErrorMessages {
        <<static>>
        +FromException(ex: Exception)$ string
    }
    class BrowserTimeZone {
        <<static>>
        +Resolve(ianaId: string)$ TimeZoneInfo
    }
    class StyleLabel {
        <<record>>
    }

    Gallery ..> GalleryFacade : викликає
    Gallery ..> GalleryLoadResult
    Gallery ..> GalleryDeleteResult
    GalleryFacade --> IGalleryClient
    GalleryFacade ..> GalleryItemView : перетворює ResultDto
    GalleryFacade ..> GalleryErrorMessages : текст помилки
    GalleryFacade ..> BrowserTimeZone : часовий пояс
    GalleryDeleteResult --> GalleryDeleteStatus
    GalleryItemView ..> StyleLabel
    GalleryClient ..|> IGalleryClient
    GalleryClient ..> ResultEndpoints : GET /api/results?style=
    ResultEndpoints ..> ResultService
    ResultService --> IResultRepository
```

| Було | Стало |
|---|---|
| `Gallery` → `IGalleryClient`, `GalleryItemView`, `GalleryErrorMessages`, `BrowserTimeZone` | `Gallery` → `GalleryFacade` |
| `try/catch` у `LoadAsync` і `DeleteAsync` сторінки | у `GalleryFacade`; сторінка отримує готовий текст (`Error`) |
| `Services/Results`: `GalleryItemView`, `GalleryErrorMessages`, `BrowserTimeZone`, `DesignDownloadName` | `Clients/Gallery`: перші три; `Clients/Design`: `DesignDownloadName` |
| `Services/Modes`: `AnalysisModePicker`, `AnalysisResultView`, `StyleLabel` | `Clients/Design` |

## Позначення

`..>` — залежність (використовує), `-->` — асоціація (поле, отримане через конструктор), `..|>` — реалізує інтерфейс; `$` — статичний член; `<<razor page>>` — сторінка Blazor (`.razor`).
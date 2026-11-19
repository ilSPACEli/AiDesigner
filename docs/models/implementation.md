# Діаграма класів реалізації

**Що показує:** фактичні класи коду (сторінки, клієнти, ендпоінти, сервіси, репозиторії) і залежності між ними — для частин, що змінюються в S3 (ЛР №7). Предметна модель і таблиці БД — [model/class-diagram.md](../../model/class-diagram.md).

**Стан:** після S2 — початковий коміт рефакторингу S3 `d9709ab`. Після рефакторингу оновлюються лише змінені класи й залежності (розд. 3).

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

*Заповнюється в S3-1: `GalleryFacade` між `Gallery.razor` і клієнтом / помічниками; помічники — у клієнтському шарі; метод фільтра за стилем (S3-3).*

## Позначення

`..>` — залежність (використовує), `-->` — асоціація (поле, отримане через конструктор), `..|>` — реалізує інтерфейс; `$` — статичний член; `<<razor page>>` — сторінка Blazor (`.razor`).
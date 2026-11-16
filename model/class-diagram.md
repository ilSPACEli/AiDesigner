# Діаграма класів

**Інженерне питання:** які предметні сутності, атрибути й зв'язки потрібні застосунку і що з них зберігається в БД.
**Пов'язані вимоги:** FR-01, FR-03…FR-12, NFR-04…NFR-06; v2 (S1): US-1 «Галерея збережених робіт» (#40), #42; v2 (S2): FR-19, FR-21, CR-06 (#60).
**Пояснення й зміни:** [docs/design.md](../docs/design.md#2-діаграма-класів). Зображення: `docs/images/class-diagram.png` / `.svg`.

```mermaid
%%{init: {"fontFamily":"DejaVu Sans","theme":"base","themeVariables":{"fontFamily":"DejaVu Sans","primaryColor":"#e6ecf5","primaryBorderColor":"#333333","primaryTextColor":"#111111","lineColor":"#333333"}}}%%
classDiagram
    direction TB

    class Photo {
        fileName: string
        format: string
        width: int
        height: int
        sizeBytes: int
        validate() bool
    }
    class DesignSession {
        id: string
        state: SessionState
        startNew()
        replacePhoto(photo: Photo)
    }
    class AnalysisResult {
        lighting: LightingLevel
        analyzedAt: datetime
        mode: AnalysisMode
        style: RoomStyle [0..1]
        confidence: decimal [0..1]
    }
    class DominantColor {
        hex: string
        hue: int
        share: decimal
    }
    class Suggestion {
        rank: int
        score: int
        reason: string
    }
    class Placement {
        x: int
        y: int
        scale: decimal
        move(dx: int, dy: int)
        resize(scale: decimal)
    }
    class ResultImage {
        fileName: string
        format: string
        createdAt: datetime
    }
    class Catalog {
        select(analysis: AnalysisResult, count: int) Suggestion[]
    }
    class CatalogItem {
        id: string «PK»
        name: string
        type: ItemType
        imageFile: string
        mainColor: string
        suitableLightings: LightingLevel [1..3]
        licenseSource: string
        style: RoomStyle [0..1]
    }
    class SavedResult {
        id: int «PK»
        createdAt: datetime (UTC)
        catalogItemId: string «FK»
        imageData: byte[] (PNG)
        thumbnailData: byte[] (JPEG)
    }
    class ResultAnalysis {
        savedResultId: int «PK, FK»
        mode: AnalysisMode
        lighting: LightingLevel
        colors: DominantColor [3..5] (JSON)
        style: RoomStyle [0..1]
        confidence: decimal [0..1]
    }

    class SessionState {
        <<enumeration>>
        EMPTY
        PHOTO_LOADED
        ANALYZED
        SUGGESTIONS_SHOWN
        ITEM_PLACED
        SAVED
    }
    class LightingLevel {
        <<enumeration>>
        DARK
        MODERATE
        BRIGHT
    }
    class RoomStyle {
        <<enumeration>>
        SCANDINAVIAN
        MINIMALISM
        MODERN
        CLASSIC
        LOFT
        BOHO
    }
    class AnalysisMode {
        <<enumeration>>
        BASIC
        LOCAL_AI
        CLOUD_AI
    }
    class ItemType {
        <<enumeration>>
        SOFA
        ARMCHAIR
        TABLE
        LAMP
        SHELF
        PLANT
        RUG
    }

    DesignSession "1" *-- "0..1" Photo : містить
    DesignSession "1" *-- "0..1" AnalysisResult : має
    AnalysisResult "1" *-- "3..5" DominantColor : колірна гама
    DesignSession "1" *-- "0..4" Suggestion : показує
    DesignSession "1" *-- "0..1" Placement : розміщення
    DesignSession "1" o-- "0..*" ResultImage : зберігає
    Catalog "1" o-- "40..50" CatalogItem : складається з
    Suggestion "0..*" --> "1" CatalogItem : пропонує
    Placement "0..*" --> "1" CatalogItem : обраний предмет
    SavedResult "0..*" --> "1" CatalogItem : предмет на роботі
    SavedResult "1" *-- "0..1" ResultAnalysis : аналіз роботи
    DesignSession ..> SessionState
    AnalysisResult ..> LightingLevel
    CatalogItem ..> ItemType
    CatalogItem ..> RoomStyle
    AnalysisResult ..> AnalysisMode
    AnalysisResult ..> RoomStyle
    ResultAnalysis ..> AnalysisMode
    ResultAnalysis ..> RoomStyle
```

## Позначення

- `*--` — композиція (частина не існує без цілого), `o--` — агрегація, `-->` — асоціація з напрямком, `..>` — залежність (клас використовує перелічення).
- Числа на кінцях зв'язків — кратності, як у вимогах: `3..5` кольорів (FR-03), `0..4` пропозиції (FR-06), `40..50` позицій каталогу (FR-12, CR-06).
- `[0..1]` в атрибута — значення може бути відсутнім: `style` = null — «стиль не визначено» (R8), `confidence` = null — режим без ШІ.
- `«PK»` — первинний ключ, `«FK»` — зовнішній ключ таблиці БД.

## Відповідність таблицям БД (S1, S2)

| Клас | Таблиця | Ключ | Примітка |
|---|---|---|---|
| `CatalogItem` | `CatalogItems` | `Id` (рядок, напр. `sofa-01`) | заповнюється з `catalog.json` (`CatalogSeeder`) |
| `SavedResult` | `SavedResults` | `Id` (ціле, генерує БД) | `CatalogItemId` → `CatalogItems.Id`, видалення предмета — `Restrict` |
| `ResultAnalysis` (S2) | `ResultAnalyses` | `SavedResultId` = `SavedResults.Id` | 1:1 із роботою, видалення роботи — `Cascade`; палітра — JSON |

`AnalysisResult` (у сесії) і `ResultAnalysis` (у БД) мають ті самі ознаки: під час «Зберегти в галерею» перший записується як другий (FR-21). Решта класів у БД не зберігається: вони живуть у межах сесії користувача (NFR-04).

## Як оновити зображення

Блок `mermaid` вище — текстове джерело моделі (GitHub показує його як діаграму). Зображення для звіту `docs/images/class-diagram.svg` / `.png` намальоване в стилі діаграм v1 (ручне розташування класів): у SVG кожен клас — прямокутник із заголовком і рядками атрибутів, зв'язок — лінія з підписами кратностей.

> S2 (#60): блок `mermaid` оновлено (`RoomStyle`, `AnalysisMode`, `ResultAnalysis`, стиль у `CatalogItem` і `AnalysisResult`); `class-diagram.svg` / `.png` оновлюються окремим кроком.

Під час зміни моделі оновлюємо обидва: атрибути й зв'язки — у блоці `mermaid`, той самий клас або рядок — у `class-diagram.svg` (текстовий файл, редагується як XML); PNG — експорт SVG у браузері (1990×1260).

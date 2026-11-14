# Діаграма класів

**Інженерне питання:** які предметні сутності, атрибути й зв'язки потрібні застосунку і що з них зберігається в БД.
**Пов'язані вимоги:** FR-01, FR-03…FR-12, NFR-04…NFR-06; v2 (S1): US-1 «Галерея збережених робіт» (#40), #42.
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
    }
    class SavedResult {
        id: int «PK»
        createdAt: datetime (UTC)
        catalogItemId: string «FK»
        imageData: byte[] (PNG)
        thumbnailData: byte[] (JPEG)
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
    Catalog "1" o-- "20..30" CatalogItem : складається з
    Suggestion "0..*" --> "1" CatalogItem : пропонує
    Placement "0..*" --> "1" CatalogItem : обраний предмет
    SavedResult "0..*" --> "1" CatalogItem : предмет на роботі
    DesignSession ..> SessionState
    AnalysisResult ..> LightingLevel
    CatalogItem ..> ItemType
```

## Позначення

- `*--` — композиція (частина не існує без цілого), `o--` — агрегація, `-->` — асоціація з напрямком, `..>` — залежність (клас використовує перелічення).
- Числа на кінцях зв'язків — кратності, як у вимогах: `3..5` кольорів (FR-03), `0..4` пропозиції (FR-06), `20..30` позицій каталогу (FR-12).
- `«PK»` — первинний ключ, `«FK»` — зовнішній ключ таблиці БД.

## Відповідність таблицям БД (S1)

| Клас | Таблиця | Ключ | Примітка |
|---|---|---|---|
| `CatalogItem` | `CatalogItems` | `Id` (рядок, напр. `sofa-01`) | заповнюється з `catalog.json` (`CatalogSeeder`) |
| `SavedResult` | `SavedResults` | `Id` (ціле, генерує БД) | `CatalogItemId` → `CatalogItems.Id`, видалення предмета — `Restrict` |

Решта класів у БД не зберігається: вони живуть у межах сесії користувача (NFR-04).

## Як оновити зображення

Блок `mermaid` вище — текстове джерело моделі (GitHub показує його як діаграму). Зображення для звіту `docs/images/class-diagram.svg` / `.png` намальоване в стилі діаграм v1 (ручне розташування класів): у SVG кожен клас — прямокутник із заголовком і рядками атрибутів, зв'язок — лінія з підписами кратностей.

Під час зміни моделі оновлюємо обидва: атрибути й зв'язки — у блоці `mermaid`, той самий клас або рядок — у `class-diagram.svg` (текстовий файл, редагується як XML); PNG — експорт SVG у браузері (1990×1260).

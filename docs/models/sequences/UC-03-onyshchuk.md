# Sequence Diagram — UC-03. Отримати пропозиції предметів

**Автор:** Онищук Дмитро. **Вимоги:** FR-06, FR-07, R1–R5, AC-06, AC-07; **S2:** FR-20, R6, AC-29.
**Специфікація:** [UC-03](../../use-cases.md#uc-03-отримати-пропозиції-предметів), [Activity UC-03](../../../model/activity-uc-03.md).

Діаграми побудовано за фактичним кодом: імена учасників — класи, повідомлення — методи. Розділ 1 — шлях запиту через HTTP API (S1, S2), розділ 2 — правила добору всередині `SuggestionRules`. Математика кольорів і перевірки гармонії — у `ColorHelper`.

## 1. Запит пропозицій через HTTP API

```mermaid
sequenceDiagram
    autonumber
    actor U as Користувач
    participant P as Home.razor
    participant DS as DesignService
    participant SC as SuggestionClient
    participant E as SuggestionEndpoints
    participant SS as SuggestionService
    participant R as EfCatalogRepository
    participant SR as SuggestionRules

    U->>P: «Підібрати предмети»
    P->>DS: SuggestAsync()
    DS->>SC: SuggestAsync(AnalysisRequestDto.From(analysis))
    Note over DS,SC: лише кольори, освітленість і стиль кімнати — без фото (NFR-04)
    SC->>E: POST /api/suggestions
    E->>E: ToModel(): 3–5 кольорів, освітленість, style — null або з переліку
    alt запит некоректний
        E-->>SC: 400 ProblemDetails (напр., «style: невідомий стиль.»)
    else
        E->>SS: SuggestAsync(analysis)
        SS->>R: GetAllAsync()
        R-->>SS: предмети з БД (зі стилями)
        SS->>SR: Select(analysis, 4)
        Note over SR: R2 → R1 → R6 → R4 → R3 → R5 (розділ 2)
        SR-->>SS: пропозиції з Rank, Score, Reason
        alt є пропозиції
            SS-->>E: Ok
            E-->>SC: 200 SuggestionDto[]
        else придатних немає
            SS-->>E: NoSuitableItems
            E-->>SC: 422
        else БД недоступна або каталог порожній
            SS-->>E: CatalogUnavailable
            E-->>SC: 503
        end
    end
    SC-->>DS: SuggestionClientResult
    alt Success
        DS-->>P: ServiceResult.Ok() — стан SUGGESTIONS_SHOWN
        P-->>U: зображення, назва й пояснення кожної пропозиції
    else NoSuitableItems / Unavailable / помилка мережі
        DS-->>P: ServiceResult.Fail(…) — стан лишається ANALYZED
        P-->>U: повідомлення
    end
```

## 2. Правила добору (`SuggestionRules.Select`)

```mermaid
sequenceDiagram
    autonumber
    participant SR as SuggestionRules
    participant CH as ColorHelper
    participant SM as StyleMatchRule

    loop для кожного предмета
        Note over SR: R2: предмет підходить до освітленості кімнати, інакше пропускаємо
        SR->>CH: IsNeutral(item.MainColor)
        CH-->>SR: bool
        loop два найбільші ненейтральні кольори кімнати (CR-01)
            SR->>CH: IsAnalogous(itemHue, hue) / IsComplementary(itemHue, hue)
            CH-->>SR: bool
        end
        Note over SR: R1: гармонія — 2, нейтральний — 1, інакше 0 (запасний для R5)
        opt бал > 0
            SR->>SM: Apply(analysis.Style, item.Style)
            SM-->>SR: +1 або 0
            Note over SR: R6: стиль збігся → бал +1 і «Стиль збігається зі стилем кімнати.»
        end
    end
    Note over SR: R4: сортування за балом, за рівних — за Id
    Note over SR: R3 (CR-03): спершу різні типи, не більше 2 одного типу
    Note over SR: R5: якщо відібрано < 3 — доповнення предметами з балом 0 і позначкою «Часткова відповідність…»
    Note over SR: Rank = 1…n
```

## Відповідність коду

| Повідомлення | Код |
|---|---|
| `SuggestAsync` | `Services/DesignService.cs` |
| `SuggestAsync`, `POST /api/suggestions` | `Clients/SuggestionClient.cs` |
| `ToModel`, `From` | `Api/Dto/AnalysisRequestDto.cs` |
| ендпоінт, 200 / 400 / 422 / 503 | `Api/Endpoints/SuggestionEndpoints.cs` |
| `SuggestAsync`, `SuggestionOutcome` | `Services/Suggestions/SuggestionService.cs`, `SuggestionOutcome.cs` |
| `GetAllAsync` | `Repositories/EfCatalogRepository.cs` |
| `Select`, `PickDiverse`, `EvaluateItemColor` | `Services/Suggestions/SuggestionRules.cs` |
| `Apply` (R6) | `Services/Suggestions/StyleMatchRule.cs` |
| `IsNeutral`, `IsAnalogous`, `IsComplementary`, `GetHue` | `Services/Analysis/ColorHelper.cs` |

## Рішення S2

- **R6 — лише для предметів, що підійшли за кольором** (бал R1 > 0). Часткові відповідності R5 лишаються з балом 0 і позначкою, інакше бал 1 від стилю змішався б із нейтральним кольором і зламав би `partialMatch`.
- **Стиль не визначено** (`style` = null) → R6 не діє, добір той самий, що без стилю (AC-29).
- R1–R5 не змінено; наявні тести (`SuggestionRulesTests`, `SuggestionRulesSelectionTests`, AC-06, AC-25) проходять без змін.

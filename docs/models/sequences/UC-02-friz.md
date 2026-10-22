# Sequence Diagram — UC-02. Проаналізувати фото приміщення

**Автор:** Фріз Богдан. **Вимоги:** FR-03, FR-04, FR-05, FR-13, NFR-01, NFR-04.
**Специфікація:** [UC-02](../../use-cases.md#uc-02-проаналізувати-фото-приміщення), [Activity UC-02](../../../model/activity-uc-02.md).

Діаграма побудована за фактичним кодом: імена учасників — класи, повідомлення — методи.

```mermaid
sequenceDiagram
    autonumber
    actor U as Користувач
    participant P as Home.razor
    participant DS as DesignService
    participant S as DesignSession
    participant A as PhotoAnalyzer
    participant PS as PixelSampler
    participant LC as LightingClassifier
    participant KM as KMeans
    participant PB as PaletteBuilder

    U->>P: «Аналіз»
    P->>DS: AnalyzeAsync()
    DS->>S: BeginAnalysis()
    Note over S: IsAnalyzing = true<br/>заміна фото заблокована (FR-13)
    DS->>A: AnalyzeAsync(photo, token)<br/>.WaitAsync(30 с)
    Note over A: Task.Run — окремий потік
    A->>PS: Sample(content, token)
    PS-->>A: пікселі ~200 px по ширині
    A->>LC: IsInsufficientData(pixels)
    alt A1: > 95 % пікселів майже чорні / білі
        LC-->>A: true
        A-->>DS: AnalysisOutcome.InsufficientData()
    else достатньо даних
        A->>LC: Classify(pixels)
        LC-->>A: DARK / MODERATE / BRIGHT (FR-04)
        A->>KM: Cluster(pixels, 5, token)
        KM-->>A: до 5 кластерів
        A->>PB: TryBuild(clusters)
        alt менше 3 різних кольорів (розширення A1)
            PB-->>A: false
            A-->>DS: AnalysisOutcome.InsufficientData()
        else 3–5 кольорів, сума часток 100 %
            PB-->>A: палітра (FR-03)
            A-->>DS: AnalysisOutcome.Success(result)
        end
    end

    alt E1: виняток або тайм-аут 30 с (NFR-01)
        Note over DS: TimeoutException / будь-який виняток<br/>→ AnalysisOutcome.Error(...)
    end

    alt Success
        DS->>S: CompleteAnalysis(result)
        Note over S: стан ANALYZED, IsAnalyzing = false
        DS-->>P: Success
        P-->>U: палітра й освітленість (FR-05)
    else InsufficientData або Error
        DS->>S: FailAnalysis()
        Note over S: стан PHOTO_LOADED, результат не збережено,<br/>заміна фото знову дозволена
        DS-->>P: InsufficientData / Error
        P-->>U: повідомлення з причиною
    end
```

## Відповідність коду

| Повідомлення | Код |
|---|---|
| `AnalyzeAsync()`, тайм-аут, E1 | `Services/DesignService.cs` → `AnalyzeAsync`, `RunAnalyzerAsync` |
| `BeginAnalysis`, `CompleteAnalysis`, `FailAnalysis` | `Models/DesignSession.cs` |
| `AnalyzeAsync`, `Analyze` | `Services/Analysis/PhotoAnalyzer.cs` |
| `Sample` | `Services/Analysis/PixelSampler.cs` |
| `IsInsufficientData`, `Classify` | `Services/Analysis/LightingClassifier.cs` |
| `Cluster` | `Services/Analysis/KMeans.cs` |
| `TryBuild` | `Services/Analysis/PaletteBuilder.cs` |

## Відхилення від Activity Diagram

- Перевірка «Достатньо даних?» (A1) виконується **до** кластеризації, а не після: результат для користувача той самий, але на засвіченому фото не витрачається час на k-means.
- **Розширення A1:** однотонне фото (менше 3 різних кольорів після склеювання схожих) теж дає «недостатньо даних», бо FR-03 вимагає 3–5 кольорів.

## Відомі обмеження

- Яскраві акценти малої площі (подушка, картина) можуть не потрапити в палітру: k-means шукає домінантні кольори, дрібні кластери (< 5 %) відкидаються або поглинаються сусідніми.
- Відстань між кольорами рахується в RGB; простір Lab точніше відповідає сприйняттю ока — кандидат на покращення, якщо ручна перевірка AC-03 дасть гірший результат.
- Для сірих кольорів `Hue = 0`; нейтральність кольору треба визначати за насиченістю (`Hex`).

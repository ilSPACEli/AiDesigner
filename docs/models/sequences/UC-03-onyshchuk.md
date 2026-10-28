# Sequence Diagram — UC-03. Отримати пропозиції предметів

**Автор:** Онищук Дмитро. **Вимоги:** FR-06, FR-07, R1, R2, R3, R4, R5, AC-06, AC-07.
**Специфікація:** [UC-03](../../use-cases.md#uc-03-отримати-пропозиції-предметів), [Activity UC-03](../../../model/activity-uc-03.md).

Діаграма побудована за фактичним кодом: імена учасників — класи, повідомлення — методи. Уся математика кольорів та перевірки гармонії винесені в `ColorHelper`.

```mermaid
sequenceDiagram
    autonumber
    actor U as Користувач
    participant P as Home.razor
    participant DS as DesignService
    participant C as Catalog
    participant CH as ColorHelper

    U->>P: «Підібрати предмети»
    P->>DS: Suggest()
    activate DS
    Note over DS: Перевірка стану (має бути ANALYZED)
    DS->>C: Select(analysis, 4)
    activate C

    loop Для кожного предмета з _items
        Note over C: R2: Фільтрація за рівнем освітлення (SuitableLightings)
        
        C->>CH: IsNeutral(item.MainColor)
        CH-->>C: isItemNeutral (bool)
        
        loop Для домінантних кольорів (макс. 2 найбільші)
            C->>CH: IsNeutral(domColor.Hex)
            CH-->>C: isDomNeutral (bool)
            
            alt Домінантний колір НЕ нейтральний
                C->>CH: IsAnalogous(itemHue, domColor.Hue)
                CH-->>C: bool
                C->>CH: IsComplementary(itemHue, domColor.Hue)
                CH-->>C: bool
            end
        end
        Note over C: R1, R4: Нарахування балів (2 - гармонія, 1 - нейтральний)
    end
    
    Note over C: Сортування: OrderByDescending(Score).ThenBy(Id)
    Note over C: R3: Відбір кандидатів (не більше 2-х одного типу)
    Note over C: R5: Доповнення предметами (fallbacks), якщо відібрано < 3
    Note over C: Ініціалізація нових Suggestion для запису Rank (init-only)
    
    C-->>DS: IReadOnlyList<Suggestion>
    deactivate C

    alt count == 0
        Note over DS: Стан залишається ANALYZED
        DS-->>P: ServiceResult.Fail("Придатних предметів немає...")
        P-->>U: Повідомлення про відсутність предметів
    else count > 0
        Note over DS: Зміна стану на SUGGESTIONS_SHOWN
        DS-->>P: ServiceResult.Ok()
        P-->>U: Зображення, назва та Reason для кожної пропозиції
    end
    deactivate DS
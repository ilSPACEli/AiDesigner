# Матриця простежуваності (RTM) v1.0

Ланцюг: вимога → Use Case → критерій приймання → проєктний артефакт. У ЛР №4 останній стовпець буде продовжено до реалізації та перевірки.

| Вимога | Use Case | Критерій приймання | Проєктний артефакт | Перевірка |
|--------|----------|--------------------|--------------------|-----------|
| FR-01 Надати фото | UC-01 | AC-01 | [Activity UC-01](../model/activity-uc-01.md); Use Case Diagram; Class `Photo`; State `EMPTY → PHOTO_LOADED` | ЛР №4 |
| FR-03 Колірна гама | UC-02 | AC-03 | [Activity UC-02](../model/activity-uc-02.md); Class `AnalysisResult`, `DominantColor` | ЛР №4 |
| FR-04 Рівень освітленості | UC-02 | AC-04 | Activity UC-02; Class `AnalysisResult`, `LightingLevel` | ЛР №4 |
| FR-05 Показ результату аналізу | UC-02 | AC-05 | Activity UC-02; State `ANALYZED` | ЛР №4 |
| FR-06 Добір 3–4 предметів | UC-03 | AC-06 | [Activity UC-03](../model/activity-uc-03.md); Class `Catalog.select()`, `Suggestion`; правила R1–R4 ([requirements.md](requirements.md#4-правила-добору-початкова-версія)) | ЛР №4 |
| FR-07 Показ пропозицій | UC-03 | AC-07 | Class `Suggestion`; State `SUGGESTIONS_SHOWN` | ЛР №4 |
| FR-08 Накласти предмет | UC-04 | AC-08 | [Activity UC-04](../model/activity-uc-04.md); Class `Placement`; State `ITEM_PLACED` | ЛР №4 |
| FR-09 Переміщення й розмір | UC-04 | AC-09 | Class `Placement.move()`, `resize()` | ЛР №4 |
| FR-10 Обрати інший предмет | UC-04 | AC-10 | State Machine (`ITEM_PLACED → SUGGESTIONS_SHOWN`) | ЛР №4 |
| FR-11 Зберегти результат | UC-04 | AC-11 | Class `ResultImage`; State `SAVED` | ЛР №4 |
| FR-12 Каталог предметів | UC-03 | AC-12 | Class `Catalog`, `CatalogItem`, `ItemType` | ЛР №4 |
| FR-13 Замінити фото | UC-01; UC-02 (блокування заміни під час аналізу) | AC-13 | State Machine (переходи до `PHOTO_LOADED`) | ЛР №4 |
| NFR-01 Аналіз ≤ 30 с | UC-02 (E1) | AC-14 | Activity UC-02 (рішення «не довше 30 с?») | План перевірки у ЛР №4 |
| NFR-02 Відгук ≤ 200 мс | UC-04 | AC-15 | — (проєктне рішення на етапі реалізації) | План перевірки у ЛР №4 |
| NFR-03 Сценарій ≤ 5 хв | UC-01…UC-04 | AC-16 | Use Case Diagram | Тест на 3+ користувачах, ЛР №4 |
| NFR-04 Приватність | UC-02 | AC-17 | Class `Photo` (не зберігається після сесії); Activity UC-02 | ЛР №4 |
| NFR-05 Ліцензії | UC-03 | AC-18 | Class `CatalogItem.licenseSource` | Перевірка каталогу, ЛР №4 |
| NFR-06 Обмеження вхідного фото | UC-01 (A2) | AC-02 | [Activity UC-01](../model/activity-uc-01.md) (рішення «Фото відповідає обмеженням?»); Class `Photo.validate()` | ЛР №4 |

**FR-02** вилучено: обмеження на вхідне фото перенесено до NFR-06 (критерій AC-02 збережено).

**Пояснення порожніх клітинок.** У NFR-02 проєктного артефакту немає: вимога стосується поведінки інтерфейсу й перевіряється лише на реалізації, тому в UML-моделі її показувати немає сенсу. Стовпець «Перевірка» заповнюється в ЛР №4.

**Приклад простежування (FR-04).** Вимога «визначати рівень освітленості» → UC-02, крок 4 → AC-04 (збіг із експертною оцінкою для ≥ 5 з 6 контрольних фото) → Activity UC-02 і клас `AnalysisResult.lighting` → тест у ЛР №4.

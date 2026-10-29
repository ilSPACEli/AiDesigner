# ШІ-дизайнер інтер'єру

Навчальний проєкт команди «Натурали» (ТР-41): застосунок, що за фото частини приміщення визначає колірну гаму й освітленість та пропонує предмети інтер'єру, накладаючи обраний на це фото.

**Версія:** v1.0.0 · Blazor Server, .NET 8, C# 12 · без БД і зовнішніх сервісів.

## Сценарій

1. Завантажити фото приміщення (JPEG/PNG, до 10 МБ, менша сторона ≥ 480 px) або зробити знімок камерою.
2. «Аналіз» — 3–5 домінантних кольорів і рівень освітленості.
3. «Підібрати предмети» — 3–4 предмети з каталогу з причиною добору.
4. «Обрати» — предмет на фото; перетягнути, змінити розмір, «Зберегти» як PNG у папку `output/`.

## Запуск

Потрібен [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
git clone https://github.com/ilSPACEli/AiDesigner.git
cd AiDesigner
dotnet run
```

Відкрити адресу, яку покаже консоль (напр., `http://localhost:5245`).

Тести:

```
dotnet test
```

Збірка для розгортання: `dotnet publish AiDesigner.csproj -c Release -o publish`, запуск — `publish/AiDesigner.exe` (Windows) або `dotnet publish/AiDesigner.dll`.


## Структура

| Папка | Що всередині |
|-------|--------------|
| `Models/` | предметні класи за діаграмою класів; `DesignSession` — State Machine сесії |
| `Services/` | `DesignService` (оркестратор), інтерфейси `IPhotoAnalyzer`, `ICatalog`, `IResultStorage` та реалізації |
| `Services/Analysis/` | аналіз фото (UC-02): k-means, освітленість |
| `Data/` | `CatalogLoader` — читання й перевірка каталогу |
| `Components/` | сторінка `Home.razor`, полотно `PhotoCanvas.razor` |
| `wwwroot/` | `js/photoCanvas.js` (Fabric.js), каталог `data/catalog.json`, `images/catalog/` |
| `tests/AiDesigner.Tests/` | xUnit-тести (379) |
| `docs/`, `model/` | документація й UML-моделі |

## Команда

| Учасник | Частина |
|---------|---------|
| Фріз Богдан | каркас, UC-02 «Аналіз фото», каталог, інтеграція й випуск |
| Блонський Іван | UC-01 «Завантажити фото» |
| Онищук Дмитро | UC-03 «Отримати пропозиції» |
| Семенюта Іван | UC-04 «Розмістити предмет і зберегти» |

## Документація

- [Вимоги (SRS)](docs/requirements.md) — зокрема зміни після baseline (CR-01…CR-03)
- [Специфікації Use Case](docs/use-cases.md)
- [Проєктування: UML-моделі](docs/design.md), sequence-діаграми — [`docs/models/sequences/`](docs/models/sequences/)
- [Реалізація](docs/implementation.md) · [Внутрішній API](docs/api.md) · [Дані й каталог](docs/data.md)
- [Матриця простежуваності (RTM)](docs/traceability.md)
- [Приймальні перевірки](docs/acceptance.md)
- [План реалізації](docs/implementation-plan.md)
- Редаговані джерела діаграм: [`model/`](model/)

## Ліцензії

Код — див. [LICENSE](LICENSE). Зображення каталогу — pngimg.com, CC BY-NC 4.0 (некомерційне використання), джерело кожного — у [docs/data.md](docs/data.md#16-позиції-й-ліцензії-nfr-05-ac-18). Fabric.js 5.3.0 — MIT (`wwwroot/lib/fabric/LICENSE`).

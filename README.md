# ШІ-дизайнер інтер'єру

Навчальний проєкт команди «Натурали» (ТР-41): застосунок, що за фото частини приміщення визначає колірну гаму й освітленість та пропонує предмети інтер'єру, накладаючи обраний на це фото.

**Версія:** v1.0.0 (випущено) · у розробці — v2, спринт S1 (база даних, галерея збережених робіт) · Blazor Server, .NET 8, C# 12, EF Core 8 + PostgreSQL.

## Сценарій

1. Завантажити фото приміщення (JPEG/PNG, до 10 МБ, менша сторона ≥ 480 px) або зробити знімок камерою.
2. «Аналіз» — 3–5 домінантних кольорів і рівень освітленості.
3. «Підібрати предмети» — 3–4 предмети з каталогу з причиною добору.
4. «Обрати» — предмет на фото; перетягнути, змінити розмір, «Зберегти в галерею» — результат (PNG і мініатюра) записується в базу даних, у таблицю `SavedResults`. Галерея вміщує не більше `Gallery__MaxItems` робіт (за замовчуванням 100): коли місця немає, найстаріші витісняються (правило R7).

## Запуск

Потрібні [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) і доступ до бази PostgreSQL (рядок підключення й роль видає координатор).

1. Клонувати репозиторій і відновити локальні інструменти (`dotnet-ef` — версія з `dotnet-tools.json`):

   ```
   git clone https://github.com/ilSPACEli/AiDesigner.git
   cd AiDesigner
   dotnet tool restore
   ```

2. Створити `.env` із шаблону й заповнити `ConnectionStrings__Default`:

   ```
   copy .env.example .env        # Windows
   cp .env.example .env          # Linux / macOS
   ```

   `.env` містить пароль і **не комітиться** (є в `.gitignore`). Змінні, задані в системі, мають пріоритет над `.env`.

   Збереження в галерею сторінка виконує через власний HTTP API за адресою `ApiBaseUrl`. Поки що працюємо по HTTP: у `.env` має бути `ApiBaseUrl=http://localhost:5245/` (так у `.env.example`), тобто порт профілю `http`, який запускає `dotnet run`. Якщо запускаєте іншим профілем чи портом, змініть адресу. Інші налаштування галереї — `Gallery__MaxItems`, `Gallery__MaxImageBytes`, `Gallery__SavesPerMinute`: значення не більші за 0 зупиняють застосунок під час старту. Усі ключі — у [docs/api.md](docs/api.md#13-налаштування-env).

3. Запустити:

   ```
   dotnet run
   ```

   Під час старту застосунок синхронізує таблицю `CatalogItems` із `catalog.json` (в усіх середовищах), а в `Development` ще й сам застосовує нові міграції. Відкрити адресу, яку покаже консоль (напр., `http://localhost:5245`).

### База даних

| БД | Для чого | Роль |
|----|----------|------|
| `aidesigner_dev` | спільна для розробки, відповідає гілці `dev` | `app_dev` |
| `aidesigner_sandbox` | задачі з міграціями до мержу | окрема роль на учасника |
| `aidesigner_prod` | демонстрація й захист, відповідає `main` | лише координатор |

Схема БД — міграції в `Data/Migrations/`:

```
dotnet ef migrations add <Назва> -o Data/Migrations   # створити міграцію
dotnet ef database update                             # застосувати до БД із .env
dotnet ef migrations list                             # які міграції є і які застосовано
```

- Міграції до `aidesigner_dev` застосовуються від ролі `app_dev`, щоб таблиці належали їй.
- У спринті S1 міграції створює лише автор задачі #42; зміни схеми в інших задачах — через нього.
- До `aidesigner_prod` — лише вручну після мержу в `main`; у `Production` застосунок міграцій не запускає.

Каталог предметів змінюється **лише** через `wwwroot/data/catalog.json` (+ PNG у `wwwroot/images/catalog/`) і PR: після мержу БД оновиться сама під час наступного старту; ручні зміни в таблиці `CatalogItems` перезапишуться. Як додати предмет — [docs/data.md](docs/data.md#17-як-додати-предмет).

Опис таблиць — [docs/data.md](docs/data.md#3-база-даних-v2-s1).

### Тести

```
dotnet test
```

Справжня БД тестам не потрібна: тести бази даних працюють на SQLite у пам'яті (`tests/AiDesigner.Tests/Data/TestDb.cs`), тож проходять і в CI.

Збірка для розгортання: `dotnet publish AiDesigner.csproj -c Release -o publish`, запуск — `publish/AiDesigner.exe` (Windows) або `dotnet publish/AiDesigner.dll`.


## Структура

| Папка | Що всередині |
|-------|--------------|
| `Models/` | предметні класи за діаграмою класів; `DesignSession` — State Machine сесії |
| `Services/` | `DesignService` (оркестратор), інтерфейси `IPhotoAnalyzer`, `ICatalog` та реалізації |
| `Services/Results/` | галерея (US-1): `ResultService`, правило R7 `GalleryRetentionPolicy`, налаштування `GalleryOptions` |
| `Repositories/` | сховище робіт: `IResultRepository`, реалізації `InMemory…` (тести) і `Ef…` (PostgreSQL) |
| `Api/` | HTTP API галереї (`/api/results`), ProblemDetails, обмеження частоти збережень |
| `Clients/` | `GalleryClient` — типізований `HttpClient` до власного API |
| `Services/Analysis/` | аналіз фото (UC-02): k-means, освітленість |
| `Data/` | `CatalogLoader` — читання й перевірка каталогу; `AppDbContext`, `CatalogSeeder`, міграції `Migrations/` |
| `Components/` | сторінка `Home.razor`, полотно `PhotoCanvas.razor` |
| `wwwroot/` | `js/photoCanvas.js` (Fabric.js), каталог `data/catalog.json`, `images/catalog/` |
| `tests/AiDesigner.Tests/` | xUnit-тести (397) |
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
- [Спринт S1](docs/sprints/s1.md) — план і підсумок
- [План реалізації](docs/implementation-plan.md)
- Редаговані джерела діаграм: [`model/`](model/)

## Ліцензії

Код — див. [LICENSE](LICENSE). Зображення каталогу — pngimg.com, CC BY-NC 4.0 (некомерційне використання), джерело кожного — у [docs/data.md](docs/data.md#16-позиції-й-ліцензії-nfr-05-ac-18). Fabric.js 5.3.0 — MIT (`wwwroot/lib/fabric/LICENSE`).

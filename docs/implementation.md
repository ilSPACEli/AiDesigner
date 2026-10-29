# Реалізація v1.0

Як модель із [design.md](design.md) втілено в коді. Вимоги — [requirements.md](requirements.md), внутрішній API — [api.md](api.md), дані — [data.md](data.md).

## 1. Технології

| Що | Чим | Чому |
|----|-----|------|
| Застосунок | Blazor Server, .NET 8, C# 12 | один проєкт, логіка на сервері, UI без окремого фронтенд-фреймворку |
| Зображення | SixLabors.ImageSharp **3.1.12** | розкодування й перевірка фото, аналіз; 4.x вимагає ліцензійного ключа для Release-збірки |
| Полотно | Fabric.js 5.3.0 (локально, `wwwroot/lib/fabric/`) через JS Interop | перетягування й масштаб без CDN і зовнішніх запитів (NFR-04) |
| Тести | xUnit, проєкт `tests/AiDesigner.Tests` | 379 тестів; CI — GitHub Actions |
| Дані | `wwwroot/data/catalog.json`, PNG у `wwwroot/images/catalog/`, результати в `output/` | БД у v1.0 не потрібна (24 позиції каталогу) |

## 2. Архітектура

```
Components/Pages/Home.razor          ← лише викликає сервіс і показує результат
   │                 └─ Components/Shared/PhotoCanvas.razor ⇄ wwwroot/js/photoCanvas.js (Fabric.js)
   ▼
Services/DesignService (Scoped — одна сесія на з'єднання Blazor)
   ├─ Models/DesignSession          — State Machine: дозволені переходи станів
   ├─ IPhotoAnalyzer → Services/Analysis/PhotoAnalyzer      (UC-02)
   ├─ ICatalog       → Services/Catalog → Data/CatalogLoader (UC-03)
   └─ IResultStorage → Services/FileResultStorage            (UC-04)
```

- **Логіка — лише в `Services/`, `Models/`, `Data/`.** `.razor` не ухвалює рішень: кнопки активні залежно від стану сесії, тож порушити State Machine через UI неможливо.
- **Стан змінюється тільки через методи `DesignSession`** (`ReplacePhoto`, `BeginAnalysis`/`CompleteAnalysis`/`FailAnalysis`, `ShowSuggestions`, `PlaceItem`, `MarkSaved`); недозволений перехід — `InvalidOperationException`.
- **Сервіси — за інтерфейсами** й реєструються в DI одним рядком у `Program.cs`. На етапі каркаса за ними стояли заглушки (`Services/Stubs/`), тож UC-01…UC-04 розроблялися паралельно; заглушки лишилися для тестів оркестратора.
- **Life-time:** аналізатор, каталог і сховище — Singleton (не мають стану користувача); `DesignService` — Scoped (тримає сесію).

## 3. Реалізація Use Case

| UC | Ключові класи | Як працює |
|----|---------------|-----------|
| UC-01 | `Photo.Validate`, `DesignService.LoadPhotoAsync` | розмір перевіряється до читання файла; формат і розміри — за вмістом (`Image.Identify`), а не за `ContentType`; порядок «перевірка → полотно → сесія», тож відхилене фото нічого не змінює (A2); одночасні завантаження блокуються (`IsLoadingPhoto`) |
| UC-02 | `PixelSampler` → `LightingClassifier` → `KMeans` → `PaletteBuilder` → `PhotoAnalyzer` | фото зменшується до 200 px; A1 (> 95 % майже чорних/білих пікселів); освітленість — середня яскравість 0.299R + 0.587G + 0.114B у цілих числах, пороги 55/125; k-means (k = 5) з детермінованою ініціалізацією maximin; склеювання схожих кольорів, відсів < 5 % (але ≥ 3), частки методом найбільших залишків — рівно 100 %; аналіз у `Task.Run`, тайм-аут 30 с у `DesignService` |
| UC-03 | `Catalog.Select`, `ColorHelper`, `CatalogLoader` | R2 → R1 (аналогова / комплементарна гармонія, відстань відтінків по колу; нейтральні домінантні кольори не враховуються) → R4 (бал, за рівних — `id`) → R3 (спершу різні типи, ≤ 2 одного типу) → R5 (доповнення з позначкою) |
| UC-04 | `PlacementGeometry`, `DesignService.PlaceItemAtCenter`/`UpdatePlacement`, `FileResultStorage` | центр і межі рахуються в C# (тестовані), JS лише застосовує; під час руху — локальне обмеження в JS, виклик C# один раз після відпускання (NFR-02); PNG експортується в роздільності оригіналу й пишеться через тимчасовий файл (E1: часткового файла немає) |

Послідовність викликів кожного UC — у sequence-діаграмах [`docs/models/sequences/`](models/sequences/).

## 4. Відхилення від моделі й рішення, ухвалені під час реалізації

| Що | Чому | Де зафіксовано |
|----|------|----------------|
| `CatalogItem.Id` (на діаграмі класів немає) | R4 сортує за id за рівних балів (відтворюваність, AC-06) | [data.md](data.md), розд. 1.2 |
| Технічні класи (`DesignService`, інтерфейси, `PhotoCanvas`, `CatalogLoader`, `PlacementGeometry`) | діаграма класів свідомо не моделює технічні класи | цей документ, [api.md](api.md) |
| Розширення A1 в UC-02: однотонне фото → «недостатньо даних» | FR-03 вимагає 3–5 кольорів, а на однотонному фото їх немає | [UC-02 sequence](models/sequences/UC-02-friz.md) |
| Перевірка A1 до кластеризації (на Activity UC-02 — після) | результат для користувача той самий, але без зайвої роботи на засвіченому фото | там само |
| Межа 50 Мп на розкодування фото | захист від «декомпресійної бомби» (малий PNG → гігабайти пам'яті) | `PixelSampler.MaxPixels` |
| CR-01: нейтральні домінантні кольори не беруть участі в гармонії R1 | у сірого немає відтінку (hue = 0 ≠ «червоний») | [requirements.md](requirements.md#зміни-після-baseline) |
| CR-02: пороги освітленості 85/170 → 55/125 | приймальна перевірка AC-04: автоекспозиція камери | там само |
| CR-03: R3 «спершу різні типи» | відгук користувача про однакові пропозиції | там само |
| Статичні файли з `Cache-Control: no-cache` | браузер брав застарілий `photoCanvas.js` після оновлення | `Program.cs` |
| `.gitattributes` (`* text=auto`) | різні закінчення рядків у Windows/Linux давали «змінені» файли | корінь репозиторію |

## 5. Тести

| Область | Файли | Що покрито |
|---------|-------|------------|
| State Machine | `DesignSessionTests`, `DesignServiceTests` | дозволені/заборонені переходи, FR-13, невдалі гілки UC-02…UC-04 |
| UC-01 | `PhotoValidationTests`, `PhotoUploadTests` | AC-01, AC-02 (усі причини й межі), AC-13, одночасні завантаження |
| UC-02 | `Analysis/*Tests` | AC-03, AC-04 (межі), AC-05, AC-14 (20 запусків), A1, E1, відтворюваність |
| UC-03 | `Services/CatalogTests`, `Services/CatalogRulesTests` | R1–R5, AC-06 на справжньому каталозі (5 палітр × 3 рівні), AC-07 |
| Каталог | `Data/CatalogLoaderTests`, `Data/CatalogDataTests` | AC-12, AC-18, перевірка кожного зображення |
| UC-04 | `PlacementTests`, `DesignServicePlacementTests`, `FileResultStorageTests` | AC-08…AC-11, E1 |

Тестові зображення генеруються в пам'яті (`Analysis/TestImages`), без файлів у репозиторії. Назви тестів починаються з номера критерію (`AC06_*`), тож простежування — за пошуком; повна матриця — [traceability.md](traceability.md).

## 6. Відомі обмеження

Перелік і причини — у висновку [приймальних перевірок](acceptance.md#4-висновок): AC-04 (точність освітленості через автоекспозицію), домінантний колір за площею, повтори в пропозиціях для «теплих» кімнат, дрібні акценти поза палітрою; ImageSharp 3.1.x має відомі вразливості (NU1902/NU1903), виправлені лише в ліцензованій 4.x.

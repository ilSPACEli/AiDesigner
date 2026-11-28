# Статичний аналіз — SonarQube (ЛР №9, Q-2)

Критерій — [profile.md](../profile.md): немає відкритих повідомлень **Security** / **Reliability** (раніше — Vulnerability / Bug) важливості **High** або **Blocker** у коді застосунку; кожне досліджене повідомлення має висновок.

## Умови аналізу

| | |
|---|---|
| Коміт | `609f384` (`dev` після злиття профілю якості, PR #117) |
| Сервер | SonarQube Community Build `26.11.0.134175`, локально в Docker (`sonarqube:community`) |
| Сканер | SonarScanner for .NET `12.1` (JRE 21 — від сервера), плагін C# `10.35.0.4138` |
| Профіль правил | «Sonar way core» (C#), без змін |
| Область | `AiDesigner.csproj` — основний код; `tests/AiDesigner.Tests` — тестовий код |
| Команди | `dotnet sonarscanner begin /k:"aidesigner" …` → `dotnet build tests/AiDesigner.Tests/AiDesigner.Tests.csproj --no-incremental` → `dotnet sonarscanner end …` |

## Результат першого аналізу

Quality Gate — **Passed**. Повідомлень — **44**:

| Якість | Кількість | Важливість | Правила |
|---|---|---|---|
| Security | 3 | Medium — 1, Low — 2 | S5693 «Limit the content length of HTTP requests» — 1; S6444 «Pass a timeout to limit the execution time» (регулярні вирази) — 2 |
| Reliability | 8 | Medium — 8 | S6966 «Await … Async instead» — 5; S1244 «Do not check floating point equality with exact values» — 3 |
| Maintainability | 33 | Info — 33 | зовнішні правила Roslyn (CA…, SYSLIB1045), здебільшого в тестах |

Blocker — 0, High — 0. Security Hotspots — 0 (у цій версії колишні hotspot-и показуються як Security-повідомлення, напр. S5693 із позначкою `former-hotspot`).

**Висновок за критерієм:** виконано — High / Blocker серед Security і Reliability немає.

## Досліджене повідомлення: S6444 — регулярний вираз без тайм-ауту

- **Де:** `Api/Dto/AnalysisRequestDto.cs:13` і `Data/CatalogLoader.cs:24` — той самий вираз `^#[0-9A-Fa-f]{6}$` (перевірка кольору `#RRGGBB`), скопійований у два класи. Security, Low.
- **Що каже правило:** регулярний вираз без обмеження часу на рядку ззовні може виконуватися дуже довго (ReDoS) і займати потік сервера.
- **Звідки дані:** у `AnalysisRequestDto` — з тіла запиту `POST /api/suggestions`, тобто від будь-якого клієнта API; у `CatalogLoader` — з `catalog.json` (наш файл).
- **Оцінка ризику:** низька. Вираз прив'язаний до початку й кінця, фіксованої довжини, без вкладених повторень — катастрофічного перебору немає, час лінійний. Проте вираз на вході API без тайм-ауту лишається пасткою на майбутнє (зміна шаблону — і ризик з'являється непомітно), а виправлення дешеве.
- **Рішення:** виправити — задача #111 (Q-3): одна спільна перевірка `#RRGGBB` з тайм-аутом, згенерована на етапі компіляції (`[GeneratedRegex]`), для DTO й завантажувача каталогу; заодно зникає дублювання й повідомлення SYSLIB1045 «Use GeneratedRegexAttribute» у тих самих рядках. Після виправлення — повторний аналіз.

## Інші повідомлення Security / Reliability

| Правило | Де | Висновок |
|---|---|---|
| S5693 — розмір HTTP-запиту (Medium) | `Components/Pages/Home.razor:261` — читання завантаженого фото з лімітом `Photo.MaxSizeBytes` = 10 МБ | **обґрунтовано не виправляється**: ліміт 10 МБ — вимога NFR-06 (поріг правила менший); розмір перевіряється до читання, формат — за вмістом. У SonarQube — **Accept** з коментарем «Limit is NFR-06 (10 MB)» |
| S6966 — `await CancelAsync` / `RunAsync` (Medium) | `Services/DesignService.cs:290, 300, 317, 327`, `Program.cs:90` | на коректність не впливає (синхронне скасування токена тайм-ауту, запуск застосунку); у беклог, кандидата не блокує |
| S1244 — точне порівняння `double` (Medium) | `Services/Analysis/ColorHelper.cs:61, 71, 72` — перетворення RGB → HSL | порівнюються значення, взяті з тих самих змінних (`max == min` — сірий колір, `max == r` / `max == g` — який канал найбільший), тож точна рівність тут правильна; у беклог, кандидата не блокує |

## Повторний аналіз після виправлення


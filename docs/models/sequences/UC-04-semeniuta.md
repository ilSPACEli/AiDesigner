# Sequence Diagram — UC-04. Розмістити предмет на фото й зберегти результат

**Автор:** Семенюта. **Вимоги:** FR-08, FR-09, FR-10, FR-11, NFR-02, NFR-04.
**Специфікація:** [UC-04](../../use-cases.md#uc-04-розмістити-предмет-на-фото-й-зберегти-результат), [Activity UC-04](../../../model/activity-uc-04.md).

Діаграма побудована за фактичним кодом: імена учасників — класи, повідомлення — методи.

## 1. Вибір предмета й початкове розміщення (кроки 1–2, A1)

```mermaid
sequenceDiagram
    autonumber
    actor U as Користувач
    participant P as Home.razor
    participant C as PhotoCanvas.razor
    participant JS as photoCanvas.js (Fabric)
    participant DS as DesignService
    participant G as PlacementGeometry
    participant S as DesignSession

    U->>P: «Обрати» (FR-08, A1: також інший предмет)
    P->>C: SetItemAsync(item.ImageFile, hidden: true)
    C->>JS: setItem(url, true)
    Note over JS: попередній предмет прибрано (A1)<br/>новий додано прихованим
    JS-->>C: { width, height } — природні розміри PNG
    C-->>P: ItemSize
    P->>DS: PlaceItemAtCenter(item, width, height)
    DS->>G: Initial(photo.Width, photo.Height, width, height)
    G-->>DS: PlacementValues(X = Width/2, Y = Height/2, Scale)
    Note over G: Scale: предмет ≈ 1/3 ширини фото,<br/>малий предмет не збільшується (AC-08)
    DS->>S: PlaceItem(new Placement(item, X, Y, Scale))
    Note over S: State = ITEM_PLACED<br/>фото й аналіз не змінюються (FR-10, AC-10)<br/>зі стану SAVED теж дозволено
    DS-->>P: PlacementValues
    P->>C: ApplyPlacementAsync(values)
    C->>JS: applyPlacement(X, Y, Scale)
    Note over JS: предмет стає видимим на своєму місці
    P-->>U: «Обрано «…»»
```

## 2. Переміщення й зміна розміру (кроки 3–4, A2, NFR-02)

```mermaid
sequenceDiagram
    autonumber
    actor U as Користувач
    participant JS as photoCanvas.js (Fabric)
    participant C as PhotoCanvas.razor
    participant P as Home.razor
    participant DS as DesignService
    participant G as PlacementGeometry
    participant PL as Placement

    loop поки користувач тягне предмет або кутовий маркер
        U->>JS: object:moving / object:scaling
        JS->>JS: keepOnPhoto(item), minScaleLimit
        Note over JS: обмеження виконується локально,<br/>без звернення до сервера (NFR-02, AC-15)<br/>предмет лишається на фото (A2)<br/>і не стає меншим за мінімум
    end
    U->>JS: відпускає предмет (object:modified)
    JS->>C: invokeMethodAsync("OnItemModified", x, y, scale, w, h)
    C->>P: OnPlacementChanged(x, y, scale, w, h)
    P->>DS: UpdatePlacement(x, y, scale, w, h)
    alt стан не ITEM_PLACED (наприклад, SAVED)
        DS-->>P: InvalidOperationException
        P-->>C: PlacementValues із Session.Placement
    else стан ITEM_PLACED
        DS->>G: Clamp(x, y, scale, photoW, photoH, w, h)
        G-->>DS: PlacementValues (центр на фото, масштаб ≥ MinScale)
        DS->>PL: Move(dx, dy), Resize(scale)
        DS-->>P: PlacementValues
        P-->>C: PlacementValues
    end
    C-->>JS: PlacementValues
    JS->>JS: applyPlacement(X, Y, Scale)
    Note over JS: на екрані саме те, що збережено в сесії
```

## 3. Збереження результату (кроки 5–7, E1)

```mermaid
sequenceDiagram
    autonumber
    actor U as Користувач
    participant P as Home.razor
    participant C as PhotoCanvas.razor
    participant JS as photoCanvas.js (Fabric)
    participant DS as DesignService
    participant FS as FileResultStorage
    participant S as DesignSession

    U->>P: «Зберегти» (FR-11)
    P->>C: ExportPngAsync()
    C->>JS: exportPng()
    Note over JS: фото + предмет малюються на canvas<br/>у розмірах оригіналу, toBlob("image/png")
    JS-->>C: PNG (Uint8Array)
    C-->>P: byte[]
    P->>DS: SaveAsync(png)
    DS->>DS: перевірка State == ITEM_PLACED
    DS->>FS: SaveAsync(png, token)
    FS->>FS: перевірка сигнатури PNG, CreateDirectory(output)
    alt E1: не PNG, помилка диска або скасування
        FS->>FS: видалити тимчасовий .tmp
        FS-->>DS: виняток
        DS-->>P: ServiceResult.Fail("Не вдалося зберегти зображення…")
        P-->>U: повідомлення про помилку<br/>файлу немає, стан ITEM_PLACED, можна повторити
    else запис вдався
        FS->>FS: WriteAllBytesAsync(result-….png.tmp)
        FS->>FS: File.Move(.tmp → .png)
        FS-->>DS: ResultImage (FileName, "png", CreatedAt)
        DS->>S: MarkSaved(image)
        Note over S: State = SAVED<br/>SavedResults += image
        DS-->>P: ServiceResult.Ok()
        P->>C: SetLockedAsync(true)
        C->>JS: setLocked(true)
        Note over JS: предмет більше не рухається,<br/>збережений файл збігається з екраном
        P-->>U: «Результат збережено: result-….png»
    end
```

## Пояснення

- **Положення рахує сервіс, а не JS.** Початкове центрування (`Initial`), обмеження межами фото й мінімальний розмір (`Clamp`, `MinScale`) — чисті функції `PlacementGeometry`, покриті тестами `AC08_*`, `AC09_*`. JS містить лише дзеркало правила для плавності під час руху, а остаточні значення завжди повертає C# (діаграма 2, кроки 9–15).
- **NFR-02 / AC-15:** під час перетягування звернень до сервера немає. Виклик `OnItemModified` відбувається один раз, коли користувач відпустив предмет. Затримку вимірювано вручну в DevTools (Performance).
- **Предмет з'являється вже на місці:** `setItem(url, hidden: true)` додає його прихованим, а видимим він стає після `applyPlacement`, тому немає «стрибка» зі старого положення.
- **Збереження двоетапне:** файл пишеться як `.tmp`, а потім перейменовується в `.png`. Тому при помилці готового `.png` не з'являється (E1, тести `E1_*` у `FileResultStorageTests`).
- **Стан `SAVED`:** рух предмета заблоковано (`setLocked`), а `UpdatePlacement` у цьому стані кидає виняток, тож збережений файл завжди збігається з екраном. Вибір іншого предмета (A1) повертає сесію в `ITEM_PLACED`.
- **Заміна фото (A3)** — це UC-01: `ReplacePhoto` скидає розміщення (див. [UC-01](UC-01-blonskyi.md)). **Завершення без збереження (A4)** не створює файлу, бо запис відбувається лише в `FileResultStorage.SaveAsync`.
- **NFR-04:** на диск потрапляє лише результат, який користувач зберіг явно. Вхідне фото існує тільки в пам'яті.

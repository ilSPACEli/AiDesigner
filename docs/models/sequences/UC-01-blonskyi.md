# Sequence Diagram — UC-01. Завантажити фото приміщення

**Автор:** Блонський Іван. **Вимоги:** FR-01, FR-13, NFR-04, NFR-06.
**Специфікація:** [UC-01](../../use-cases.md#uc-01-завантажити-фото-приміщення), [Activity UC-01](../../../model/activity-uc-01.md).

Діаграма побудована за фактичним кодом: імена учасників — класи, повідомлення — методи.

```mermaid
sequenceDiagram
    autonumber
    actor U as Користувач
    participant P as Home.razor
    participant DS as DesignService
    participant IS as ImageSharp (Image)
    participant PH as Photo
    participant C as PhotoCanvas
    participant S as DesignSession

    U->>P: обирає файл або робить знімок (FR-01)
    P->>DS: CheckFileSize(file.Size)
    alt A2: файл > 10 МБ
        DS-->>P: ServiceResult.Fail(TooLargeMessage)
        P-->>U: причина відмови, сесія без змін
    else розмір допустимий
        DS-->>P: ServiceResult.Ok()
        P->>P: OpenReadStream → byte[] (лише в пам'яті, NFR-04)
        P->>DS: LoadPhotoAsync(fileName, content, _canvas.SetPhotoAsync)
        alt іде аналіз (FR-13)
            DS-->>P: Fail(PhotoReplaceBlockedMessage)
        else
            DS->>DS: ReadPhoto(fileName, content)
            DS->>IS: Image.Identify(stream)
            IS-->>DS: Width, Height, DecodedImageFormat
            DS->>PH: new Photo { Format, Width, Height, SizeBytes, Content }
            DS->>PH: Validate()
            alt A2: не JPEG/PNG або менша сторона < 480 px (NFR-06)
                PH-->>DS: false, RejectionReason
                DS-->>P: ServiceResult.Fail(RejectionReason)
                P-->>U: причина відмови; полотно й сесія без змін (A4)
            else фото коректне
                PH-->>DS: true
                DS->>C: showOnCanvas(content) = SetPhotoAsync(content)
                alt браузер не показав фото
                    C-->>DS: виняток
                    DS-->>P: Fail(PhotoShowFailedMessage)
                else фото на екрані
                    C-->>DS: ok
                    DS->>DS: LoadPhoto(photo)
                    DS->>S: ReplacePhoto(photo)
                    Note over S: ClearResults() — скидає аналіз,<br/>пропозиції й розміщення (FR-13, A1)<br/>State = PHOTO_LOADED
                    DS-->>P: ServiceResult.Ok()
                    P-->>U: «Фото завантажено. Натисніть «Аналіз».»
                end
            end
        end
    end
```

## Пояснення

- **Порядок «перевірка → полотно → сесія»** задає `DesignService.LoadPhotoAsync`, а не сторінка. Невалідне фото не потрапляє ні на полотно, ні в сесію (A2, A4); це перевіряють тести `AC02_*` у `PhotoUploadTests`.
- **Формат і розміри — за вмістом файла** (`Image.Identify`), а не за `ContentType` з браузера: WebP, перейменований у `.jpg`, відхиляється.
- **Камера (FR-01, A3)** — кнопка з `capture="environment"`, показується лише на сенсорних пристроях. Якщо камера недоступна, браузер відкриває вибір файла, а підказка пропонує обрати фото з галереї.
- **NFR-04:** фото існує лише як `byte[]` у пам'яті сесії; на диск і в зовнішні сервіси не передається.

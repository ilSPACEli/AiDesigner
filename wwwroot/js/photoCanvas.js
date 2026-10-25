

const MIN_VISIBLE_FRACTION = 0.2;

const MIN_ITEM_SIZE_FRACTION = 0.1;

class PhotoCanvas {
    constructor(host, dotNetRef) {
        this.host = host;
        this.dotNetRef = dotNetRef ?? null;   // посилання на PhotoCanvas.razor (може не бути)
        const element = document.createElement("canvas");
        host.appendChild(element);
        this.canvas = new fabric.Canvas(element, { selection: false, preserveObjectStacking: true });
        this.canvas.setDimensions({ width: 0, height: 0 });
        this.scale = 1;        // у скільки разів фото на екрані менше за оригінал
        this.photoWidth = 0;   // розміри оригіналу, px
        this.photoHeight = 0;
        this.item = null;      // поточний предмет на полотні

        // Під час руху — лише локальне обмеження, без викликів сервера (NFR-02).
        this.canvas.on("object:moving", e => this.keepOnPhoto(e.target));
        this.canvas.on("object:scaling", e => this.keepOnPhoto(e.target));
        // Коли користувач відпустив предмет — один виклик у C#.
        this.canvas.on("object:modified", e => this.onModified(e.target));
    }

    // Показати нове фото. Попередній предмет прибирається (FR-13).
    async setPhoto(streamRef) {
        const buffer = await streamRef.arrayBuffer();
        const url = URL.createObjectURL(new Blob([buffer]));
        try {
            const photo = await loadImage(url);
            const maxWidth = this.host.clientWidth || 800;
            this.scale = Math.min(1, maxWidth / photo.width);
            this.photoWidth = photo.width;
            this.photoHeight = photo.height;

            this.canvas.clear();
            this.item = null;
            this.canvas.setDimensions({
                width: Math.round(photo.width * this.scale),
                height: Math.round(photo.height * this.scale)
            });
            photo.set({ scaleX: this.scale, scaleY: this.scale });
            this.canvas.setBackgroundImage(photo, this.canvas.renderAll.bind(this.canvas));
        } finally {
            URL.revokeObjectURL(url);
        }
    }

    // Показати предмет. Якщо інший уже був — замінити його (FR-10).
    // Повертає природні розміри PNG: за ними C# рахує положення (AC-08).
    // hidden = true: не показувати, доки C# не надішле положення через applyPlacement.
    async setItem(url, hidden = false) {
        const item = await loadImage(url);
        if (this.item) {
            this.canvas.remove(this.item);
        }
        item.set({
            originX: "center",
            originY: "center",
            transparentCorners: false,
            cornerColor: "#3b6fd8",
            lockUniScaling: true,    // один масштаб на обидві осі, як у Placement.Scale
            lockRotation: true,      // повороту в Placement немає
            lockScalingFlip: true,   // не дзеркалити й не доводити масштаб до нуля
            visible: !hidden
        });
        // Лише кутові маркери: бокові змінювали б пропорції.
        item.setControlsVisibility({ mt: false, mb: false, ml: false, mr: false, mtr: false });

        // Найменший розмір: Fabric сам не дає зменшити предмет нижче цього масштабу
        // (minScaleLimit — у масштабі полотна, тому множимо на this.scale).
        item.minScaleLimit = this.minItemScale(item.width, item.height) * this.scale;

        // Тимчасово: по центру фото, природний розмір. Остаточні значення дасть C# (applyPlacement).
        item.set({ scaleX: this.scale, scaleY: this.scale });
        item.setPositionByOrigin(
            new fabric.Point(this.photoWidth / 2 * this.scale, this.photoHeight / 2 * this.scale),
            "center", "center");
        item.setCoords();

        this.item = item;
        this.canvas.add(item);
        this.canvas.setActiveObject(item);
        this.canvas.requestRenderAll();
        return { width: item.width, height: item.height };
    }

    // Застосувати положення й масштаб, обчислені в C# (AC-08, AC-09).
    applyPlacement(x, y, scale) {
        if (!this.item) return;
        const k = this.scale;
        this.item.set({ scaleX: scale * k, scaleY: scale * k, visible: true });
        this.item.setPositionByOrigin(new fabric.Point(x * k, y * k), "center", "center");
        this.item.setCoords();
        this.canvas.requestRenderAll();
    }

    // Заблокувати/розблокувати предмет. Після збереження (SAVED) рух заборонено,
    // щоб збережений файл завжди збігався з екраном.
    setLocked(locked) {
        if (!this.item) return;
        this.item.set({
            selectable: !locked,
            evented: !locked,
            hasControls: !locked,
            hasBorders: !locked
        });
        if (locked) {
            this.canvas.discardActiveObject();
        } else {
            this.canvas.setActiveObject(this.item);
        }
        this.canvas.requestRenderAll();
    }

    // Сформувати PNG у роздільності оригінального фото (FR-11, AC-11).
    async exportPng() {
        if (!this.photoWidth) {
            throw new Error("Фото ще не завантажено.");
        }
        this.canvas.discardActiveObject();
        this.canvas.renderAll();

        // Малюємо на полотно точно оригінальних розмірів: заокруглення розмірів
        // екранного полотна не повинно зсувати результат на піксель.
        const source = this.canvas.toCanvasElement(this.photoWidth / this.canvas.getWidth());
        const output = document.createElement("canvas");
        output.width = this.photoWidth;
        output.height = this.photoHeight;
        output.getContext("2d").drawImage(source, 0, 0, output.width, output.height);

        const blob = await new Promise((resolve, reject) =>
            output.toBlob(b => (b ? resolve(b) : reject(new Error("Не вдалося сформувати PNG."))), "image/png"));
        return new Uint8Array(await blob.arrayBuffer());
    }

    // Дзеркало PlacementGeometry.MinScale: довша сторона предмета на фото —
    // не менше за MIN_ITEM_SIZE_FRACTION від меншої сторони фото; не більше 1.
    minItemScale(itemWidth, itemHeight) {
        const shorterSide = Math.min(this.photoWidth, this.photoHeight);
        return Math.min(1, MIN_ITEM_SIZE_FRACTION * shorterSide / Math.max(itemWidth, itemHeight));
    }

    // Не дати предмету повністю піти за межі фото (UC-04 A2).
    keepOnPhoto(target) {
        if (!target || target !== this.item || !this.photoWidth) return;
        const k = this.scale;
        const center = target.getCenterPoint();
        const itemScale = target.scaleX / k;   // масштаб у «оригінальних» пікселях
        const x = clampAxis(center.x / k, target.width * itemScale, this.photoWidth);
        const y = clampAxis(center.y / k, target.height * itemScale, this.photoHeight);
        target.setPositionByOrigin(new fabric.Point(x * k, y * k), "center", "center");
        target.setCoords();
    }

    // Користувач відпустив предмет: передати положення в C# і прийняти остаточне.
    async onModified(target) {
        if (!target || target !== this.item || !this.dotNetRef) return;
        this.keepOnPhoto(target);

        const k = this.scale;
        const center = target.getCenterPoint();
        const itemScale = target.scaleX / k;
        if (!(itemScale > 0)) return;

        try {
            const result = await this.dotNetRef.invokeMethodAsync(
                "OnItemModified",
                Math.round(center.x / k),
                Math.round(center.y / k),
                itemScale,
                target.width,
                target.height);

            // Якщо за цей час предмет замінили чи користувач знову його тягне — не чіпаємо.
            if (result && target === this.item && !this.canvas._currentTransform) {
                this.applyPlacement(result.x ?? result.X, result.y ?? result.Y, result.scale ?? result.Scale);
            }
        } catch (error) {
            // C# не прийняв зміну (напр., стан SAVED) — лишаємо предмет як є.
        }
    }

    dispose() {
        this.canvas.dispose();
        this.host.innerHTML = "";
    }
}

// Одна вісь: центр, розмір предмета, розмір фото — усе в пікселях оригіналу.
// Те саме правило, що в PlacementGeometry (C#), але без округлення до цілих:
// під час руху потрібна плавність, а до цілих значення доведе C# після відпускання.
function clampAxis(center, size, photo) {
    const min = size * (MIN_VISIBLE_FRACTION - 0.5);
    const max = photo + size * (0.5 - MIN_VISIBLE_FRACTION);
    return Math.min(Math.max(center, min), max);
}

// Fabric 5 працює з колбеками; обгортаємо в Promise, щоб писати await.
function loadImage(url) {
    return new Promise((resolve, reject) => {
        fabric.Image.fromURL(url, (image, isError) => {
            if (isError || !image) {
                reject(new Error("Не вдалося завантажити зображення: " + url));
            } else {
                resolve(image);
            }
        });
    });
}

export function create(host, dotNetRef) {
    return new PhotoCanvas(host, dotNetRef);
}
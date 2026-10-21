// Полотно Fabric.js: фото приміщення як фон і один предмет поверх нього.
// Фото надходить із сервера через JS Interop і нікуди не відправляється (NFR-04).
// Бібліотека fabric підключена глобально в App.razor (wwwroot/lib/fabric/fabric.min.js).

class PhotoCanvas {
    constructor(host) {
        this.host = host;
        const element = document.createElement("canvas");
        host.appendChild(element);
        this.canvas = new fabric.Canvas(element, { selection: false, preserveObjectStacking: true });
        this.canvas.setDimensions({ width: 0, height: 0 });
        this.scale = 1;   // у скільки разів фото на екрані менше за оригінал
        this.item = null; // поточний предмет на полотні
    }

    // Показати нове фото. Попередній предмет прибирається (FR-13).
    async setPhoto(streamRef) {
        const buffer = await streamRef.arrayBuffer();
        const url = URL.createObjectURL(new Blob([buffer]));
        try {
            const photo = await loadImage(url);
            const maxWidth = this.host.clientWidth || 800;
            this.scale = Math.min(1, maxWidth / photo.width);

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
    // Положення по центру (AC-08) і межі фото (AC-09) додає UC-04.
    async setItem(url) {
        const item = await loadImage(url);
        if (this.item) {
            this.canvas.remove(this.item);
        }
        item.set({ left: 0, top: 0, transparentCorners: false, cornerColor: "#3b6fd8" });
        this.item = item;
        this.canvas.add(item);
        this.canvas.setActiveObject(item);
        this.canvas.requestRenderAll();
    }

    // Сформувати PNG у роздільності оригінального фото (FR-11, AC-11).
    async exportPng() {
        this.canvas.discardActiveObject();
        this.canvas.renderAll();
        const dataUrl = this.canvas.toDataURL({ format: "png", multiplier: 1 / this.scale });
        const response = await fetch(dataUrl);
        return new Uint8Array(await response.arrayBuffer());
    }

    dispose() {
        this.canvas.dispose();
        this.host.innerHTML = "";
    }
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

export function create(host) {
    return new PhotoCanvas(host);
}

// Потребує Fabric.js v6 (UMD-збірка, глобальний об'єкт `fabric`), підключеної ПЕРЕД цим файлом.
window.canvasEditor = (() => {
    const MAX_WIDTH = 960;
    const MAX_HEIGHT = 640;

    let canvas = null;
    let pendingDataUrl = null; // фото могло прийти раніше, ніж полотно ініціалізовано

    async function applyBackground(dataUrl) {
        const img = await fabric.FabricImage.fromURL(dataUrl);

        // Вписуємо в межі полотна, без збільшення.
        const scale = Math.min(MAX_WIDTH / img.width, MAX_HEIGHT / img.height, 1);
        canvas.setDimensions({ width: img.width * scale, height: img.height * scale });

        img.scale(scale);
        canvas.backgroundImage = img;
        canvas.requestRenderAll();
    }

    return {
        async init(container) {
            if (canvas) {
                canvas.dispose();
                canvas = null;
            }
            container.replaceChildren();

            const el = document.createElement("canvas");
            container.appendChild(el);
            canvas = new fabric.Canvas(el, { preserveObjectStacking: true });

            if (pendingDataUrl) {
                const dataUrl = pendingDataUrl;
                pendingDataUrl = null;
                await applyBackground(dataUrl);
            }
        },

        async setBackground(dataUrl) {
            if (!canvas) {
                pendingDataUrl = dataUrl;
                return;
            }
            await applyBackground(dataUrl);
        },

        dispose() {
            if (canvas) {
                canvas.dispose();
                canvas = null;
            }
        }
    };
})();

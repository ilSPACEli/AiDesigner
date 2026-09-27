// Потребує Fabric.js v6 (UMD-збірка, глобальний `fabric`), підключеної ПЕРЕД цим файлом.

window.editorCanvas = (() => {
    const instances = new Map();

    function getInstance(editorId) {
        const instance = instances.get(editorId);
        if (!instance) {
            throw new Error(`editorCanvas: немає інстансу з id "${editorId}". Спочатку викличте init().`);
        }
        return instance;
    }

    async function applyBackground(instance) {
        if (!instance.pendingDataUrl) return;

        const dataUrl = instance.pendingDataUrl;
        instance.pendingDataUrl = null;

        const img = await fabric.FabricImage.fromURL(dataUrl);
        instance.backgroundImage = img;
        instance.naturalWidth = img.width;
        instance.naturalHeight = img.height;

        fitBackgroundToContainer(instance);
    }

    function fitBackgroundToContainer(instance) {
        const { canvas, container, backgroundImage, naturalWidth, naturalHeight } = instance;
        if (!backgroundImage || !naturalWidth || !naturalHeight) return;

        const width = container.clientWidth;
        const scale = width / naturalWidth;
        const height = naturalHeight * scale;

        canvas.setDimensions({ width, height });

        backgroundImage.scale(scale);
        backgroundImage.set({ left: 0, top: 0, selectable: false, evented: false });
        canvas.backgroundImage = backgroundImage;
        canvas.requestRenderAll();
    }

    return {
        init(container, editorId) {
            if (instances.has(editorId)) {
                this.dispose(editorId);
            }

            const el = document.createElement("canvas");
            container.replaceChildren(el);

            const canvas = new fabric.Canvas(el, {
                preserveObjectStacking: true,
                selection: false, 
            });

            const instance = {
                canvas,
                container,
                backgroundImage: null,
                naturalWidth: 0,
                naturalHeight: 0,
                pendingDataUrl: null,
                resizeObserver: null,
            };

            instance.resizeObserver = new ResizeObserver(() => fitBackgroundToContainer(instance));
            instance.resizeObserver.observe(container);

            instances.set(editorId, instance);
        },

        async setBackground(editorId, dataUrl) {
            const instance = getInstance(editorId);
            instance.pendingDataUrl = dataUrl;
            await applyBackground(instance);
        },

        dispose(editorId) {
            const instance = instances.get(editorId);
            if (!instance) return;

            instance.resizeObserver?.disconnect();
            instance.canvas.dispose();
            instances.delete(editorId);
        },
    };
})();
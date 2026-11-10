// Зберігає PNG, який надіслав .NET (DotNetStreamReference), як файл на комп'ютері користувача.
export async function downloadFile(fileName, contentStreamReference) {
    const buffer = await contentStreamReference.arrayBuffer();
    const url = URL.createObjectURL(new Blob([buffer], { type: "image/png" }));

    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();

    setTimeout(() => URL.revokeObjectURL(url), 10000);
}

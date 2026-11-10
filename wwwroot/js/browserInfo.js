// Пояс часу браузера (IANA-ім'я), щоб сервер показував дати за часом користувача.
export function getTimeZone() {
    return Intl.DateTimeFormat().resolvedOptions().timeZone;
}

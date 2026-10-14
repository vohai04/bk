/* DOM and UTC time helpers shared by every reader page. */
window.ReaderUI = (() => {
    const formatter = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });
    function el(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = String(text);
        if (tag === 'i' && className?.includes('bi')) node.setAttribute('aria-hidden', 'true');
        return node;
    }
    function time(value, prefix = '') {
        const node = el('time');
        const date = new Date(value);
        if (!value || Number.isNaN(date.getTime())) { node.textContent = 'Chưa cập nhật'; return node; }
        node.dateTime = date.toISOString();
        node.textContent = prefix + formatter.format(date);
        node.title = formatter.format(date) + ' (giờ Việt Nam, UTC+7)';
        return node;
    }
    function formatTimes() {
        document.querySelectorAll('time[datetime]').forEach(node => {
            const formatted = time(node.dateTime);
            node.textContent = formatted.textContent; node.title = formatted.title;
        });
    }
    async function request(url, data, json = false) {
        const options = { credentials: 'same-origin', headers: { 'Accept': 'application/json' } };
        if (data !== undefined) {
            options.method = 'POST';
            options.headers.RequestVerificationToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
            options.headers['Content-Type'] = json ? 'application/json' : 'application/x-www-form-urlencoded';
            options.body = json ? JSON.stringify(data) : new URLSearchParams(data);
        }
        const response = await fetch(url, options);
        const result = await response.json();
        if (!response.ok || result.success === false) throw new Error(result.message || 'Không thể thực hiện. Vui lòng thử lại.');
        return result;
    }
    function button(text, className, action) {
        const node = el('button', className, text); node.type = 'button';
        node.addEventListener('click', action); return node;
    }
    function empty(container, message, icon = 'book') {
        const node = el('div', 'reader-empty');
        node.append(el('i', `bi bi-${icon}`), el('p', '', message));
        container.replaceChildren(node);
    }
    function formError(form, message) {
        let error = form.querySelector('.form-error');
        if (!error) { error = el('p', 'form-error text-danger mt-3'); error.setAttribute('role', 'alert'); form.append(error); }
        error.textContent = message;
        form.querySelector('input:not([type="hidden"]),textarea')?.focus();
    }
    function setAvatar(node, url) {
        node.querySelector('img')?.remove();
        if (!/^\/avatars\/\d+\?v=[a-f0-9]{32}$/.test(url || '')) return;
        const img = el('img'); img.width = 256; img.height = 256; img.alt = '';
        img.addEventListener('error', () => img.remove(), { once: true });
        img.src = url; node.append(img);
    }
    function avatar(userId, name, url, className = '') {
        const node = el('span', `user-avatar ${className}`);
        node.dataset.avatarUser = userId; node.setAttribute('aria-hidden', 'true');
        node.append(el('span', 'avatar-initial', (name || 'B').slice(0, 1).toUpperCase()));
        setAvatar(node, url); return node;
    }
    function updateAvatars(userId, url) {
        document.querySelectorAll(`[data-avatar-user="${Number(userId)}"]`).forEach(node => setAvatar(node, url));
    }
    function cover(value) {
        if (/^data:image\/(png|jpeg|webp|gif);base64,/i.test(value || '')) return value;
        if (value && /^[A-Za-z0-9+/=\r\n]+$/.test(value)) return 'data:image/jpeg;base64,' + value;
        if (value?.startsWith('/images/')) return value;
        return '/images/default-book.svg';
    }
    document.addEventListener('DOMContentLoaded', () => {
        formatTimes();
        document.querySelectorAll('img[data-cover]').forEach(img => { img.src = cover(img.dataset.cover); img.addEventListener('error', () => img.src = '/images/default-book.svg', { once: true }); });
        document.querySelectorAll('.user-avatar img').forEach(img => {
            if (img.complete && !img.naturalWidth) img.remove();
            else img.addEventListener('error', () => img.remove(), { once: true });
        });
    });
    return { el, time, request, button, empty, formError, cover, formatTimes, avatar, updateAvatars };
})();

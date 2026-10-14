'use strict';
window.AdminUI = (() => {
    const number = new Intl.NumberFormat('vi-VN');
    const date = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
    function el(tag, className, text) { const node = document.createElement(tag); if (className) node.className = className; if (text !== undefined) node.textContent = text; return node; }
    function icon(name) { const node = el('i', `bi bi-${name}`); node.setAttribute('aria-hidden', 'true'); return node; }
    async function request(url, data) {
        const options = { headers: { 'X-Requested-With': 'XMLHttpRequest' } };
        if (data !== undefined) {
            options.method = 'POST';
            options.headers.RequestVerificationToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
            options.body = data instanceof FormData ? data : new URLSearchParams(data);
        }
        const response = await fetch(url, options);
        if (!response.ok) throw new Error(response.status === 403 || response.status === 401 ? 'Phiên đăng nhập hoặc quyền truy cập đã thay đổi. Đăng nhập lại để tiếp tục.' : 'Chưa hoàn tất thao tác. Hãy thử lại.');
        const result = await response.json();
        if (result.success === false) throw new Error(result.message || 'Chưa hoàn tất thao tác. Kiểm tra dữ liệu rồi thử lại.');
        return result;
    }
    function action(label, name, callback, className = '') {
        const button = el('button', `row-action ${className}`); button.type = 'button'; button.setAttribute('aria-label', label); button.title = label; button.append(icon(name)); button.addEventListener('click', callback); return button;
    }
    function link(label, name, href) { const a = el('a', 'row-action'); a.href = href; a.title = label; a.setAttribute('aria-label', label); a.append(icon(name)); return a; }
    function cover(src, title) {
        const img = el('img', 'table-cover'); img.alt = `Bìa sách ${title}`; img.width = 38; img.height = 54; img.loading = 'lazy';
        try { const url = new URL(src || '/images/default-book.svg', location.origin); img.src = /https?:/.test(url.protocol) || /^data:image\//.test(src || '') ? url.href : '/images/default-book.svg'; } catch (_) { img.src = '/images/default-book.svg'; }
        img.addEventListener('error', () => { img.src = '/images/default-book.svg'; }, { once: true }); return img;
    }
    function pagination(container, pages, page, change) {
        container.replaceChildren();
        for (const [label, next] of [['Trước', page - 1], [`${page} / ${Math.max(1, pages)}`, null], ['Sau', page + 1]]) {
            if (next === null) { container.append(el('span', '', label)); continue; }
            const button = el('button', '', label); button.type = 'button'; button.disabled = next < 1 || next > pages; button.addEventListener('click', () => change(next)); container.append(button);
        }
    }
    function empty(container, title, message, retry) {
        const box = el('div', 'admin-empty'); box.append(icon(retry ? 'wifi-off' : 'search'), el('h3', '', title), el('p', '', message));
        if (retry) { const button = el('button', 'admin-button secondary', 'Thử lại'); button.type = 'button'; button.addEventListener('click', retry); box.append(button); }
        container.replaceChildren(box);
    }
    document.querySelectorAll('time[datetime]').forEach(time => { const value = new Date(time.dateTime); if (!Number.isNaN(value.valueOf())) time.textContent = date.format(value); });
    document.querySelectorAll('[data-cover-fallback]').forEach(img => img.addEventListener('error', () => { img.src = '/images/default-book.svg'; }, { once: true }));
    return { el, icon, request, action, link, cover, pagination, empty, number, date };
})();

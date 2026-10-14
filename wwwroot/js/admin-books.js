'use strict';
(() => {
    const ui = AdminUI, area = document.getElementById('adminBooksTable');
    const search = document.getElementById('bookSearch'), category = document.getElementById('bookCategory');
    let page = 1, version = 0;
    function readUrl() { const p = new URLSearchParams(location.search); page = Math.max(1, parseInt(p.get('page'), 10) || 1); search.value = p.get('search') || ''; category.value = p.get('category') || ''; }
    function syncUrl() { const p = new URLSearchParams(); if (search.value.trim()) p.set('search', search.value.trim()); if (category.value) p.set('category', category.value); if (page > 1) p.set('page', page); const url = location.pathname + (p.size ? '?' + p : ''); if (url !== location.pathname + location.search) history.pushState(null, '', url); }
    async function remove(book, button) {
        showConfirmModal('Xóa sách', `Xóa “${book.title}” khỏi thư viện?\n\nĐánh giá, bình luận và lượt lưu liên quan cũng sẽ bị xóa. Thao tác này không thể hoàn tác.`, async () => {
            button.disabled = true;
            try { await ui.request('/Admin/ManageBook?handler=Delete', { id: book.bookId }); showToast('Đã xóa sách khỏi thư viện.'); await load(); }
            catch (e) { showToast(e.message, 'error'); button.disabled = false; }
        });
    }
    function render(books) {
        if (!books.length) { ui.empty(area, 'Chưa có cuốn sách phù hợp', 'Thay đổi từ khóa hoặc thêm một cuốn sách vào thư viện.'); return; }
        const scroll = ui.el('div', 'admin-data-scroll'); scroll.tabIndex = 0; scroll.setAttribute('role', 'region'); scroll.setAttribute('aria-label', 'Bảng sách, cuộn ngang để xem thêm');
        const table = ui.el('table', 'admin-data-table book-table'), head = ui.el('thead'), row = ui.el('tr');
        for (const title of ['Sách / tác giả', 'ISBN', 'Thể loại', 'Nhà xuất bản', 'Năm XB', 'Thao tác']) row.append(ui.el('th', '', title));
        head.append(row); table.append(head);
        const body = ui.el('tbody');
        for (const book of books) {
            const tr = ui.el('tr'); tr.dataset.bookId = book.bookId;
            const cell = ui.el('td'), content = ui.el('div', 'book-cell'), text = ui.el('div');
            const title = ui.el('a', '', book.title); title.href = `/BookDetail/${book.bookId}`;
            text.append(title, ui.el('small', '', book.author?.name || 'Chưa rõ tác giả')); content.append(ui.cover(book.imageBase64, book.title), text); cell.append(content);
            const isbn = ui.el('td', 'isbn-cell', book.isbn || '—');
            const type = ui.el('td'); type.append(ui.el('span', 'admin-pill neutral', book.category?.name || 'Chưa phân loại'));
            const publisher = ui.el('td', 'table-meta', book.publisher?.name || '—');
            const year = ui.el('td', 'table-meta', book.publicationYear || '—');
            const actions = ui.el('td'), buttons = ui.el('div', 'row-actions');
            const params = new URLSearchParams({ search: search.value.trim(), category: category.value });
            buttons.append(ui.link(`Sửa sách ${book.title}`, 'pencil', `/Admin/UpdateBook/${book.bookId}?${params}`), ui.link(`Xem sách ${book.title}`, 'arrow-up-right', `/BookDetail/${book.bookId}`));
            let removeButton; removeButton = ui.action(`Xóa sách ${book.title}`, 'trash3', () => remove(book, removeButton), 'danger'); buttons.append(removeButton); actions.append(buttons);
            tr.append(cell, isbn, type, publisher, year, actions); body.append(tr);
        }
        table.append(body); scroll.append(table); area.replaceChildren(scroll);
    }
    async function load(sync = true) {
        const current = ++version; if (sync) syncUrl(); area.setAttribute('aria-busy', 'true');
        document.getElementById('csvSearch').value = search.value.trim(); document.getElementById('csvCategory').value = category.value;
        try {
            const query = new URLSearchParams({ handler: 'AjaxSearch', search: search.value.trim(), category: category.value, page, pageSize: 10 });
            const data = await ui.request(`/Admin/ManageBook?${query}`); if (current !== version) return;
            if (page > Math.max(1, data.totalPages)) { page = Math.max(1, data.totalPages); return load(); }
            if (!Array.isArray(data.books)) throw new Error('Dữ liệu sách chưa sẵn sàng. Hãy thử lại.');
            render(data.books); document.getElementById('bookTotal').textContent = `${ui.number.format(data.totalCount)} cuốn sách`;
            document.getElementById('bookSummary').textContent = `Hiển thị ${data.books.length} / ${ui.number.format(data.totalCount)} sách`;
            ui.pagination(document.getElementById('bookPagination'), data.totalPages, page, next => { page = next; load(); });
        } catch (e) { if (current === version) { document.getElementById('bookTotal').textContent = 'Chưa tải được danh sách'; ui.empty(area, 'Chưa tải được thư viện', e.message, () => load(false)); } }
        finally { if (current === version) area.setAttribute('aria-busy', 'false'); }
    }
    document.getElementById('bookSearchForm').addEventListener('submit', e => { e.preventDefault(); page = 1; load(); });
    category.addEventListener('change', () => { page = 1; load(); });
    document.getElementById('bookReset').addEventListener('click', () => { search.value = ''; category.value = ''; page = 1; load(); });
    window.addEventListener('popstate', () => { readUrl(); load(false); });
    readUrl(); load(false);
})();

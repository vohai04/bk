(() => {
    const ui = ReaderUI, root = document.getElementById('readerCollection');
    const kind = root.dataset.kind, isBooks = kind === 'Favorites';
    const list = document.getElementById('collectionList');
    const endpoint = handler => `/${kind}?handler=${handler}`;
    let currentPage = 1;
    const fail = error => showToast(error.message, 'error');
    function remove(handler, data, message) {
        showConfirmModal('Xác nhận xóa', message, async () => {
            try { await ui.request(endpoint(handler), data, isBooks); await load(currentPage); showToast('Đã xóa.'); }
            catch (error) { fail(error); }
        });
    }
    function renderBook(book) {
        const article = ui.el('article', 'saved-book'), link = ui.el('a');
        link.href = `/BookDetail/${book.bookId}`;
        const image = ui.el('img'); image.src = ui.cover(book.imageBase64); image.alt = `Bìa sách ${book.title}`;
        image.width = 130; image.height = 190; image.loading = 'lazy';
        image.addEventListener('error', () => image.src = '/images/default-book.svg', { once: true }); link.append(image);
        const body = ui.el('div', 'saved-book-body'), heading = ui.el('h2'), title = ui.el('a', '', book.title);
        title.href = link.href; heading.append(title);
        body.append(heading, ui.el('p', '', book.author), ui.time(book.createdAt, 'Đã lưu · '), ui.button('Bỏ khỏi tủ sách', '', () => remove('RemoveFavorite', { BookId: book.bookId }, `Bỏ “${book.title}” khỏi tủ sách?`)));
        article.append(link, body); return article;
    }
    function renderHistory(item) {
        const article = ui.el('article', 'history-item'), body = ui.el('div');
        const link = ui.el('a', '', item.searchQuery); link.href = `/?q=${encodeURIComponent(item.searchQuery)}`;
        body.append(link, ui.el('p', '', `${item.resultCount} kết quả`), ui.time(item.searchedAt));
        article.append(ui.el('i', 'bi bi-search'), body, ui.button('Xóa', '', () => remove('AjaxDelete', { id: item.searchHistoryId }, 'Xóa lượt tìm kiếm này?')));
        return article;
    }
    async function load(page = 1) {
        const result = await ui.request(`${endpoint(isBooks ? 'AjaxFavorites' : 'AjaxGet')}&page=${page}`);
        currentPage = result.currentPage || page;
        const items = isBooks ? result.books : result.histories;
        if (!items.length && page > 1) return load(Math.max(1, result.totalPages));
        const url = new URL(location.href);
        if (currentPage > 1) url.searchParams.set('page', currentPage); else url.searchParams.delete('page');
        history.replaceState(null, '', url);
        if (items.length) { list.style.display = ''; list.replaceChildren(...items.map(isBooks ? renderBook : renderHistory)); }
        else {
            ui.empty(list, isBooks ? 'Tủ sách đang trống. Khám phá và lưu một cuốn sách bạn thích.' : 'Chưa có lịch sử tìm kiếm.', isBooks ? 'bookmark-heart' : 'clock-history');
            if (isBooks) list.style.display = 'block';
        }
        renderPagination('#collectionPager', result.totalPages, currentPage, p => load(p).catch(fail));
    }
    document.getElementById('clearHistoryBtn')?.addEventListener('click', () => remove('AjaxDeleteAll', {}, 'Xóa toàn bộ lịch sử tìm kiếm của bạn?'));
    load(Math.max(1, Number(new URL(location.href).searchParams.get('page')) || 1)).catch(error => { ui.empty(list, 'Không tải được dữ liệu. Vui lòng đăng nhập hoặc tải lại trang.'); fail(error); });
})();

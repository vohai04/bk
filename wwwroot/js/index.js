'use strict';

(function () {
    const defaults = { page: 1, pageSize: 9, q: '', title: '', author: '', category: '', year: '', tag: '', filter: '', sort: '' };
    const filterSort = { 'top-rated': 'rating', trending: 'favorites', 'most-searched': 'searched' };
    const filterTitles = { 'top-rated': 'Những cuốn sách được đánh giá cao', trending: 'Những cuốn sách được yêu thích', 'most-searched': 'Khám phá xu hướng' };
    const input = document.getElementById('mainSearchInput');
    const suggestions = document.getElementById('searchSuggestions');
    const formatNumber = new Intl.NumberFormat('vi-VN');
    let state, request, suggestionController, suggestionTimer, suggestionVersion = 0;

    function readState() {
        const params = new URLSearchParams(location.search);
        state = { ...defaults };
        for (const key of Object.keys(defaults)) {
            if (params.has(key) && !['page', 'pageSize'].includes(key)) state[key] = params.get(key);
        }
        state.page = Math.max(1, Number.parseInt(params.get('page'), 10) || 1);
        state.sort = state.sort || filterSort[state.filter] || '';
        input.value = state.q || state.title || state.author || state.category || '';
        document.getElementById('bookSort').value = state.sort;
        document.querySelectorAll('[data-collection]').forEach(link => {
            const active = link.dataset.collection === state.filter;
            link.classList.toggle('active', active);
            if (active) link.setAttribute('aria-current', 'page'); else link.removeAttribute('aria-current');
        });
    }

    function updateUrl() {
        const params = new URLSearchParams();
        for (const [key, value] of Object.entries(state)) {
            if (key !== 'pageSize' && value && !(key === 'page' && value === 1)) params.set(key, value);
        }
        const query = params.toString();
        const url = location.pathname + (query ? `?${query}` : '');
        if (url !== location.pathname + location.search) history.pushState(null, '', url);
    }

    function hideSuggestions() {
        suggestionVersion++;
        clearTimeout(suggestionTimer);
        suggestionController?.abort();
        suggestions.hidden = true;
        input.setAttribute('aria-expanded', 'false');
    }

    function safeCover(value) {
        if (!value) return '/images/default-book.svg';
        try {
            const url = new URL(value, location.origin);
            if (/^https?:$/.test(url.protocol) || /^data:image\/(png|jpeg|jpg|webp|gif);base64,/i.test(value)) return url.href;
        } catch (_) { /* Invalid cover URLs use the library placeholder. */ }
        return '/images/default-book.svg';
    }

    function renderBooks(items) {
        const list = $('#bookList').empty();
        document.getElementById('emptyState').hidden = items.length > 0;
        for (const book of items) {
            const title = book.title || 'Chưa có tên sách';
            const href = `/BookDetail/${encodeURIComponent(book.id || book.bookId)}`;
            const image = $('<img>').addClass('book-image').attr({ src: safeCover(book.coverUrl), alt: `Bìa sách ${title}`, width: 116, height: 173, loading: 'lazy' });
            image.on('error', function () { this.onerror = null; this.src = '/images/default-book.svg'; });
            const card = $('<article>').addClass('book-card');
            card.append($('<a>').addClass('cover-stage').attr({ href, 'aria-label': `Xem sách ${title}` }).append(image));
            const content = $('<div>').addClass('book-content');
            content.append($('<h3>').addClass('book-title').append($('<a>').attr('href', href).text(title)));
            content.append($('<p>').addClass('book-author').text(book.author || 'Chưa rõ tác giả'));
            const stats = $('<div>').addClass('book-stats');
            const rating = Number(book.rating) || 0;
            stats.append($('<span>').addClass('book-rating').append($('<i>').addClass('bi bi-star-fill').attr('aria-hidden', 'true'), document.createTextNode(rating ? `${formatNumber.format(rating)} / 5` : 'Chưa đánh giá')));
            stats.append($('<span>').addClass('book-favorites').attr('aria-label', `${book.favorites || 0} lượt yêu thích`).append($('<i>').addClass('bi bi-heart').attr('aria-hidden', 'true'), document.createTextNode(formatNumber.format(Number(book.favorites) || 0))));
            list.append(card.append(content.append(stats)));
        }
    }

    function loadBooks(syncUrl = true) {
        request?.abort();
        if (syncUrl) updateUrl();
        $('#loadingState').show();
        document.getElementById('bookList').setAttribute('aria-busy', 'true');
        $('#bookList').hide();
        for (const id of ['emptyState', 'searchError', 'paginationNav']) document.getElementById(id).hidden = true;
        $('#collectionTitle').text(state.tag ? `Thẻ sách: ${state.tag}` : filterTitles[state.filter] || 'Khám phá thư viện');
        $('#resultSummary').text('Đang tải sách…');
        request = $.getJSON('/Index?handler=Search', state)
            .done(res => {
                const items = Array.isArray(res.items) ? res.items : [];
                renderBooks(items);
                $('#bookList').show();
                $('#resultSummary').text(`${formatNumber.format(res.total || 0)} cuốn sách${state.q ? ` cho “${state.q}”` : ''}`);
                const pages = res.totalPages || 0;
                document.getElementById('paginationNav').hidden = pages <= 1;
                renderPagination('#paginationButtons', pages, state.page, page => { state.page = page; loadBooks(); });
            })
            .fail((xhr, status) => {
                if (status === 'abort') return;
                document.getElementById('searchError').hidden = false;
                $('#resultSummary').text('Không thể tải danh sách sách.');
            })
            .always(() => { $('#loadingState').hide(); document.getElementById('bookList').setAttribute('aria-busy', 'false'); });
    }

    $('#mainSearchForm').on('submit', event => {
        event.preventDefault();
        state = { ...defaults, q: input.value.trim(), sort: document.getElementById('bookSort').value };
        hideSuggestions();
        loadBooks();
    });
    $('#bookSort').on('change', function () { state.sort = this.value; state.filter = ''; state.page = 1; loadBooks(); readState(); });
    $('#clearSearch').on('click', () => { state = { ...defaults }; input.value = ''; loadBooks(); readState(); });
    $('#retrySearch').on('click', () => loadBooks(false));
    window.addEventListener('popstate', () => { readState(); hideSuggestions(); loadBooks(false); });
    input.addEventListener('input', () => {
        hideSuggestions();
        const keyword = input.value.trim();
        if (keyword.length < 2) return;
        const version = suggestionVersion;
        suggestionTimer = setTimeout(async () => {
            suggestionController = new AbortController();
            try {
                const groups = await Promise.all(['TitleSuggest', 'AuthorSuggest', 'CategorySuggest'].map(async handler => {
                    const res = await fetch(`/Index?handler=${handler}&keyword=${encodeURIComponent(keyword)}`, { signal: suggestionController.signal });
                    if (!res.ok) throw new Error('Suggestions unavailable');
                    return res.json();
                }));
                if (version !== suggestionVersion || input.value.trim() !== keyword) return;
                suggestions.replaceChildren();
                groups.forEach((group, index) => {
                    if (!Array.isArray(group)) return;
                    group.slice(0, 3).forEach(text => {
                        const button = document.createElement('button');
                        button.type = 'button'; button.className = 'suggestion-item';
                        const label = document.createElement('span'); label.textContent = text;
                        const type = document.createElement('small'); type.textContent = ['Sách', 'Tác giả', 'Thể loại'][index];
                        button.append(label, type);
                        button.addEventListener('click', () => {
                            state = { ...defaults, [['title', 'author', 'category'][index]]: text };
                            input.value = text; hideSuggestions(); input.focus(); loadBooks();
                        });
                        suggestions.append(button);
                    });
                });
                suggestions.hidden = suggestions.childElementCount === 0;
                input.setAttribute('aria-expanded', String(!suggestions.hidden));
            } catch (error) { if (error.name !== 'AbortError') hideSuggestions(); }
        }, 250);
    });
    document.getElementById('mainSearchForm').addEventListener('keydown', event => {
        if (event.key === 'Escape') { hideSuggestions(); input.focus(); return; }
        if (!['ArrowDown', 'ArrowUp'].includes(event.key) || suggestions.hidden) return;
        const buttons = [...suggestions.querySelectorAll('button')];
        const index = buttons.indexOf(document.activeElement);
        const next = event.key === 'ArrowDown' ? (index + 1) % buttons.length : (index - 1 + buttons.length) % buttons.length;
        event.preventDefault(); buttons[next]?.focus();
    });
    document.addEventListener('click', event => { if (!event.target.closest('#mainSearchForm')) hideSuggestions(); });
    readState();
    loadBooks(false);
})();

(() => {
    const ui = ReaderUI;
    const bookId = Number(document.getElementById('bookDetail').dataset.bookId);
    const endpoint = handler => `${location.pathname}?handler=${handler}`;
    const list = document.getElementById('commentsList');
    let userId = null, currentPage = 1;
    const fail = error => showToast(error.message, 'error');

    function editor(item, article, editing = false) {
        article.querySelector('.inline-comment-form')?.remove();
        const form = ui.el('form', 'inline-comment-form');
        const label = ui.el('label', 'form-label', editing ? 'Sửa bình luận' : `Trả lời ${item.userName}`);
        const input = ui.el('textarea', 'form-control');
        input.id = `comment-input-${item.bookCommentId}`; label.htmlFor = input.id;
        input.name = editing ? 'newComment' : 'Comment'; input.autocomplete = 'off';
        input.rows = 2; input.maxLength = 500; input.required = true;
        input.value = editing ? item.comment : '';
        const actions = ui.el('div');
        const submit = ui.el('button', 'btn btn-primary', editing ? 'Lưu thay đổi' : 'Gửi phản hồi');
        submit.type = 'submit';
        actions.append(submit, ui.button('Hủy', 'btn btn-secondary', () => form.remove()));
        form.append(label, input, actions);
        article.append(form); input.focus();
        form.addEventListener('submit', async event => {
            event.preventDefault(); submit.disabled = true;
            try {
                await ui.request(endpoint(editing ? 'EditComment' : 'AddReply'), editing
                    ? { commentId: item.bookCommentId, newComment: input.value }
                    : { BookId: bookId, ParentCommentId: item.bookCommentId, Comment: input.value });
                await load(currentPage);
                showToast(editing ? 'Đã cập nhật bình luận.' : 'Đã gửi phản hồi.');
            } catch (error) { ui.formError(form, error.message); submit.disabled = false; }
        });
    }
    function render(item, isReply = false) {
        const article = ui.el('article', 'comment-thread');
        article.id = `comment-content-${item.bookCommentId}`;
        const header = ui.el('div', 'comment-header');
        const author = ui.el('div');
        author.append(ui.el('span', 'comment-author', item.userName), ui.time(item.createdAt));
        header.append(ui.avatar(item.userId, item.userName, item.avatarUrl, 'comment-avatar'), author);
        if (item.star) {
            const stars = ui.el('span', 'rating-stars', '★'.repeat(item.star) + '☆'.repeat(5 - item.star));
            stars.setAttribute('aria-label', `${item.star} trên 5 sao`); header.append(stars);
        }
        article.append(header, ui.el('p', 'comment-text', item.comment));
        if (item.updatedAt) {
            const edited = ui.el('p', 'comment-edited'); edited.append(ui.time(item.updatedAt, 'Đã chỉnh sửa · ')); article.append(edited);
        }
        const actions = ui.el('div', 'comment-actions');
        if (userId) actions.append(ui.button('Trả lời', '', () => editor(item, article)));
        if (item.userId === userId) {
            actions.append(ui.button('Sửa', '', () => editor(item, article, true)), ui.button('Xóa', 'comment-delete', () => {
                showConfirmModal('Xóa bình luận?', 'Bình luận và các phản hồi bên dưới sẽ bị xóa.', async () => {
                    try { await ui.request(endpoint('DeleteComment'), { commentId: item.bookCommentId }); await load(currentPage); showToast('Đã xóa bình luận.'); }
                    catch (error) { fail(error); }
                });
            }));
        }
        article.append(actions);
        if (!isReply && item.replies?.length) {
            const replies = ui.el('div', 'comment-replies');
            function add(items) { items.forEach(reply => { replies.append(render(reply, true)); if (reply.replies?.length) add(reply.replies); }); }
            add(item.replies); article.append(replies);
        }
        return article;
    }
    async function load(page = 1) {
        const result = await ui.request(`${endpoint('GetBookDetail')}&id=${bookId}&page=${page}`);
        if (!result.comments.length && page > 1) return load(Math.max(1, result.totalPages));
        userId = result.currentUserId; currentPage = result.currentPage;
        const url = new URL(location.href);
        if (currentPage > 1) url.searchParams.set('page', currentPage); else url.searchParams.delete('page');
        history.replaceState(null, '', url);
        document.getElementById('commentCount').textContent = `${result.totalComments} bình luận`;
        document.getElementById('averageRating').textContent = Number(result.book.averageRating).toFixed(1);
        document.getElementById('ratingCount').textContent = `${result.book.ratingCount} đánh giá`;
        if (result.comments.length) list.replaceChildren(...result.comments.map(item => render(item)));
        else ui.empty(list, 'Chưa có bình luận. Hãy chia sẻ cảm nhận đầu tiên.', 'chat-square-text');
        renderPagination('#commentsPager', result.totalPages, currentPage, p => load(p).catch(fail));
    }
    document.getElementById('commentForm')?.addEventListener('submit', async event => {
        event.preventDefault(); const form = event.currentTarget, button = form.querySelector('button'); button.disabled = true;
        try {
            await ui.request(endpoint('AddComment'), { BookId: bookId, Star: document.getElementById('commentStar').value, Comment: document.getElementById('commentText').value });
            form.reset(); await load(); showToast('Đã gửi bình luận và đánh giá.');
        } catch (error) { ui.formError(form, error.message); }
        finally { button.disabled = false; }
    });
    const save = document.getElementById('saveBookBtn');
    let isSaved = false, favoriteLoaded = false;
    function renderFavorite() {
        save.replaceChildren(ui.el('i', `bi bi-bookmark-${isSaved ? 'check-fill' : 'plus'}`), document.createTextNode(isSaved ? ' Đã lưu vào tủ sách' : ' Lưu vào tủ sách'));
        save.setAttribute('aria-pressed', String(isSaved));
        save.title = isSaved ? 'Bấm để bỏ khỏi tủ sách' : 'Lưu cuốn sách này';
        save.classList.toggle('btn-primary', !isSaved);
        save.classList.toggle('btn-secondary', isSaved);
    }
    async function readFavorite() {
        const result = await ui.request(`${endpoint('CheckFavorite')}&bookId=${bookId}`);
        isSaved = result.isFavorite; favoriteLoaded = true; renderFavorite();
    }
    save.disabled = true;
    readFavorite().catch(fail).finally(() => { save.disabled = false; });
    save.addEventListener('click', async () => {
        save.disabled = true;
        try {
            if (!favoriteLoaded) await readFavorite();
            if (isSaved) await ui.request('/Favorites?handler=RemoveFavorite', { bookId }, true);
            else await ui.request(endpoint('AddFavorite'), { bookId });
            await readFavorite();
            showToast(isSaved ? 'Đã lưu vào tủ sách.' : 'Đã bỏ khỏi tủ sách.');
        } catch (error) { favoriteLoaded = false; fail(error); }
        finally { save.disabled = false; }
    });
    document.querySelectorAll('.entity-detail').forEach(button => button.addEventListener('click', async () => {
        try {
            const result = await ui.request(`${endpoint('EntityDetail')}&type=${button.dataset.type}&id=${button.dataset.id}`);
            document.getElementById('entityDetailTitle').textContent = result.data.name;
            const content = document.getElementById('entityDetailContent');
            content.replaceChildren(ui.el('p', '', result.data.biography || result.data.description || result.data.address || 'Chưa có thông tin bổ sung.'));
            bootstrap.Modal.getOrCreateInstance(document.getElementById('entityDetailModal')).show();
        } catch (error) { fail(error); }
    }));
    (async () => {
        try {
            const target = /comment-content-(\d+)/.exec(location.hash);
            const page = target ? (await ui.request(`${endpoint('GetCommentPage')}&commentId=${target[1]}`)).page : Math.max(1, Number(new URL(location.href).searchParams.get('page')) || 1);
            await load(page);
            if (target) document.getElementById(`comment-content-${target[1]}`)?.scrollIntoView({ block: 'center' });
        } catch (error) { ui.empty(list, 'Không tải được bình luận. Vui lòng tải lại trang.'); fail(error); }
    })();
})();

'use strict';
(() => {
    const ui = AdminUI, area = document.getElementById('adminUsersTable');
    const search = document.getElementById('userSearch'), role = document.getElementById('userRole'), status = document.getElementById('userStatus');
    const form = document.getElementById('userEditorForm'), save = document.getElementById('userSave'), error = document.getElementById('userFormError');
    const modal = bootstrap.Modal.getOrCreateInstance(document.getElementById('userEditor'));
    let page = 1, version = 0, editing = null;
    function readUrl() { const p = new URLSearchParams(location.search); page = Math.max(1, parseInt(p.get('page'), 10) || 1); search.value = p.get('search') || ''; role.value = p.get('role') || ''; status.value = p.get('status') || ''; }
    function syncUrl() { const p = new URLSearchParams(); if (search.value.trim()) p.set('search', search.value.trim()); if (role.value) p.set('role', role.value); if (status.value !== '') p.set('status', status.value); if (page > 1) p.set('page', page); const url = location.pathname + (p.size ? '?' + p : ''); if (url !== location.pathname + location.search) history.pushState(null, '', url); }
    function edit(record) {
        editing = record; form.reset(); error.hidden = true;
        for (const key of ['userName', 'fullName', 'email']) form.elements[key].value = record?.[key] || '';
        form.elements.roleStr.value = record?.role || 'User';
        form.elements.password.required = !record; form.elements.password.disabled = !!record;
        document.getElementById('userPasswordField').hidden = !!record;
        document.getElementById('userEditorTitle').textContent = record ? 'Sửa người dùng' : 'Thêm người dùng';
        save.textContent = record ? 'Lưu thay đổi' : 'Thêm người dùng'; modal.show();
    }
    async function mutate(user, kind, button) {
        const lock = user.status === 1;
        const title = kind === 'delete' ? 'Xóa người dùng' : lock ? 'Khóa tài khoản' : 'Kích hoạt tài khoản';
        const message = kind === 'delete' ? `Xóa tài khoản “${user.userName}”?\n\nDữ liệu cá nhân liên quan sẽ bị xóa. Nếu bạn chỉ muốn ngừng truy cập, hãy dùng Khóa tài khoản.` : `${lock ? 'Khóa' : 'Kích hoạt'} tài khoản “${user.userName}”?`;
        showConfirmModal(title, message, async () => {
            button.disabled = true;
            try {
                await ui.request(`/Admin/Users?handler=${kind === 'delete' ? 'AjaxDelete' : 'AjaxSetUserStatus'}`, kind === 'delete' ? { userId: user.userId } : { userId: user.userId, status: lock ? 0 : 1 });
                showToast(kind === 'delete' ? 'Đã xóa người dùng.' : lock ? 'Đã khóa tài khoản.' : 'Đã kích hoạt tài khoản.'); await load();
            } catch (e) { showToast(e.message, 'error'); button.disabled = false; }
        });
    }
    function render(users) {
        if (!users.length) { ui.empty(area, 'Chưa có tài khoản phù hợp', 'Thay đổi bộ lọc hoặc thêm tài khoản mới.'); return; }
        const scroll = ui.el('div', 'admin-data-scroll'); scroll.tabIndex = 0; scroll.setAttribute('role', 'region'); scroll.setAttribute('aria-label', 'Bảng tài khoản, cuộn ngang để xem thêm');
        const table = ui.el('table', 'admin-data-table user-table'), head = ui.el('thead'), row = ui.el('tr');
        for (const title of ['Người dùng', 'Email', 'Vai trò', 'Trạng thái', 'Thao tác']) row.append(ui.el('th', '', title)); head.append(row); table.append(head);
        const body = ui.el('tbody');
        for (const user of users) {
            const tr = ui.el('tr'); tr.dataset.userId = user.userId;
            const name = ui.el('td'), person = ui.el('div', 'user-cell'), label = ui.el('div');
            person.append(ui.el('span', 'person-initial', (user.fullName || user.userName || '?').slice(0, 1).toUpperCase()));
            label.append(ui.el('strong', '', user.fullName || user.userName), ui.el('small', '', `@${user.userName}`)); person.append(label); name.append(person);
            const email = ui.el('td', 'table-meta', user.email);
            const roleCell = ui.el('td'); roleCell.append(ui.el('span', 'admin-pill neutral', user.role === 'Admin' ? 'Quản trị viên' : 'Người đọc'));
            const statusCell = ui.el('td'); statusCell.append(ui.el('span', `admin-pill ${user.status === 1 ? 'green' : 'danger'}`, user.status === 1 ? 'Đang hoạt động' : 'Đã khóa'));
            const actions = ui.el('td'), group = ui.el('div', 'row-actions');
            group.append(ui.action(`Sửa người dùng ${user.userName}`, 'pencil', () => edit(user)));
            let toggle, remove; toggle = ui.action(`${user.status === 1 ? 'Khóa' : 'Kích hoạt'} tài khoản ${user.userName}`, user.status === 1 ? 'lock' : 'unlock', () => mutate(user, 'status', toggle));
            remove = ui.action(`Xóa người dùng ${user.userName}`, 'trash3', () => mutate(user, 'delete', remove), 'danger');
            group.append(toggle, remove); actions.append(group); tr.append(name, email, roleCell, statusCell, actions); body.append(tr);
        }
        table.append(body); scroll.append(table); area.replaceChildren(scroll);
    }
    async function load(sync = true) {
        const current = ++version; if (sync) syncUrl(); area.setAttribute('aria-busy', 'true');
        try {
            const p = new URLSearchParams({ handler: 'AjaxSearch', search: search.value.trim(), role: role.value, status: status.value, page, pageSize: 10 });
            const data = await ui.request(`/Admin/Users?${p}`); if (current !== version) return;
            if (page > Math.max(1, data.totalPages)) { page = Math.max(1, data.totalPages); return load(); }
            if (!Array.isArray(data.users)) throw new Error('Dữ liệu tài khoản chưa sẵn sàng. Hãy thử lại.');
            render(data.users); document.getElementById('userTotal').textContent = `${ui.number.format(data.totalCount)} tài khoản`;
            document.getElementById('userSummary').textContent = `Hiển thị ${data.users.length} / ${ui.number.format(data.totalCount)} tài khoản`;
            ui.pagination(document.getElementById('userPagination'), data.totalPages, page, next => { page = next; load(); });
        } catch (e) { if (current === version) ui.empty(area, 'Chưa tải được tài khoản', e.message, () => load(false)); }
        finally { if (current === version) area.setAttribute('aria-busy', 'false'); }
    }
    form.addEventListener('submit', async e => {
        e.preventDefault(); if (!form.reportValidity()) return;
        const body = new FormData(form); if (editing) body.set('userId', editing.userId);
        save.disabled = true; error.hidden = true; const label = save.textContent; save.textContent = 'Đang lưu…';
        try { await ui.request(`/Admin/Users?handler=${editing ? 'AjaxEdit' : 'AjaxAdd'}`, body); modal.hide(); showToast(editing ? 'Đã cập nhật người dùng.' : 'Đã thêm người dùng.'); await load(); }
        catch (e) { error.textContent = e.message; error.hidden = false; error.focus(); }
        finally { save.disabled = false; save.textContent = label; }
    });
    document.getElementById('userAdd').addEventListener('click', () => edit(null));
    document.getElementById('userSearchForm').addEventListener('submit', e => { e.preventDefault(); page = 1; load(); });
    for (const select of [role, status]) select.addEventListener('change', () => { page = 1; load(); });
    document.getElementById('userReset').addEventListener('click', () => { search.value = ''; role.value = ''; status.value = ''; page = 1; load(); });
    document.getElementById('userEditor').addEventListener('shown.bs.modal', () => form.elements.userName.focus());
    window.addEventListener('popstate', () => { readUrl(); load(false); });
    readUrl(); load(false);
})();

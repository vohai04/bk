'use strict';
(() => {
    const ui = AdminUI;
    const config = JSON.parse(document.getElementById('catalogDefinition').textContent);
    const tableArea = document.getElementById('catalogTable');
    const search = document.getElementById('catalogSearch');
    const form = document.getElementById('catalogEditorForm');
    const fields = document.getElementById('catalogFields');
    const error = document.getElementById('catalogFormError');
    const save = document.getElementById('catalogSave');
    const modal = bootstrap.Modal.getOrCreateInstance(document.getElementById('catalogEditor'));
    let page = 1, version = 0, editing = null;
    const endpoint = location.pathname;

    for (const definition of config.fields) {
        const group = ui.el('div', 'field-group');
        const label = ui.el('label', '', definition.label); label.htmlFor = `catalog_${definition.name}`;
        if (definition.required) label.append(ui.el('span', 'required', '*'));
        const control = ui.el(definition.type === 'textarea' ? 'textarea' : 'input');
        if (control.tagName === 'INPUT') control.type = definition.type;
        else control.rows = 3;
        control.name = definition.name; control.id = label.htmlFor; control.required = definition.required;
        control.maxLength = definition.maxLength; control.autocomplete = 'off';
        if (definition.type !== 'date') control.placeholder = `${definition.label}…`;
        group.append(label, control); fields.append(group);
    }

    function readUrl() {
        const params = new URLSearchParams(location.search);
        page = Math.max(1, parseInt(params.get('page'), 10) || 1);
        search.value = params.get('search') || '';
    }
    function syncUrl() {
        const params = new URLSearchParams();
        if (search.value.trim()) params.set('search', search.value.trim());
        if (page > 1) params.set('page', page);
        const next = endpoint + (params.size ? '?' + params : '');
        if (next !== location.pathname + location.search) history.pushState(null, '', next);
    }
    function edit(record) {
        editing = record;
        form.reset(); error.hidden = true;
        for (const definition of config.fields) {
            let value = record?.[definition.name] || '';
            if (definition.type === 'date' && value) value = value.slice(0, 10);
            form.elements[definition.name].value = value;
        }
        document.getElementById('catalogEditorTitle').textContent = `${record ? 'Sửa' : 'Thêm'} ${config.singular}`;
        save.textContent = record ? 'Lưu thay đổi' : `Thêm ${config.singular}`;
        modal.show();
    }
    async function remove(record, button) {
        const usage = record.bookCount > 0 ? `\n\n${record.bookCount} cuốn sách đang sử dụng mục này.` : '';
        showConfirmModal(`Xóa ${config.singular}`, `Xóa “${record.name}” khỏi danh mục?${usage}\n\nThao tác này không thể hoàn tác.`, async () => {
            button.disabled = true;
            try {
                await ui.request(`${endpoint}?handler=AjaxDelete`, { [config.idKey]: record[config.idKey] });
                showToast(`Đã xóa ${config.singular}.`); await load();
            } catch (e) { showToast(record.bookCount > 0 && config.kind !== 'tags' ? 'Mục này đang có sách. Chuyển sách sang mục khác trước khi xóa.' : e.message, 'error'); button.disabled = false; }
        });
    }
    function render(records) {
        if (!records.length) { ui.empty(tableArea, `Chưa có ${config.singular} phù hợp`, 'Thêm một mục mới hoặc thay đổi từ khóa tìm kiếm.'); return; }
        const scroll = ui.el('div', 'admin-data-scroll'); scroll.tabIndex = 0; scroll.setAttribute('role', 'region'); scroll.setAttribute('aria-label', `Bảng ${config.singular}, cuộn ngang để xem thêm`);
        const table = ui.el('table', 'admin-data-table catalog-table');
        const head = ui.el('thead'), hr = ui.el('tr');
        const detailTitle = config.kind === 'authors' ? 'Quốc tịch / tiểu sử' : config.kind === 'publishers' ? 'Địa chỉ / liên hệ' : 'Mô tả';
        for (const title of ['Tên', detailTitle, 'Số sách', 'Ngày tạo', 'Thao tác']) hr.append(ui.el('th', '', title));
        head.append(hr); table.append(head);
        const body = ui.el('tbody');
        for (const record of records) {
            const tr = ui.el('tr'); tr.dataset.recordId = record[config.idKey];
            const nameCell = ui.el('td'); nameCell.append(ui.el('strong', 'catalog-name', record.name), ui.el('small', 'catalog-id', `#${record[config.idKey]}`));
            const detail = config.kind === 'authors' ? [record.nationality, record.biography].filter(Boolean).join(' — ') : config.kind === 'publishers' ? [record.address, record.contactInfo].filter(Boolean).join(' — ') : record.description;
            const description = ui.el('td', 'description-cell', detail || 'Chưa có thông tin');
            const count = ui.el('td'); count.append(ui.el('span', 'admin-pill neutral', ui.number.format(record.bookCount || 0)));
            const date = ui.el('td', 'table-meta', record.createdAt ? ui.date.format(new Date(record.createdAt)).split(',')[0] : record.createdAtFormatted || '—');
            const actions = ui.el('td'); const actionGroup = ui.el('div', 'row-actions');
            actionGroup.append(ui.action(`Sửa ${record.name}`, 'pencil', () => edit(record)));
            let deleteButton; deleteButton = ui.action(`Xóa ${record.name}`, 'trash3', () => remove(record, deleteButton), 'danger btn-delete');
            actionGroup.append(deleteButton); actions.append(actionGroup);
            tr.append(nameCell, description, count, date, actions); body.append(tr);
        }
        table.append(body); scroll.append(table); tableArea.replaceChildren(scroll);
    }
    async function load(sync = true) {
        const current = ++version;
        if (sync) syncUrl();
        tableArea.setAttribute('aria-busy', 'true');
        document.getElementById('catalogCount').textContent = 'Đang tải…';
        try {
            const params = new URLSearchParams({ handler: 'AjaxSearch', search: search.value.trim(), page, pageSize: 10 });
            const data = await ui.request(`${endpoint}?${params}`);
            if (current !== version) return;
            if (page > Math.max(1, data.totalPages)) { page = Math.max(1, data.totalPages); return load(); }
            const records = data[config.listKey];
            if (!Array.isArray(records)) throw new Error('Dữ liệu danh mục chưa sẵn sàng. Hãy thử lại.');
            render(records);
            document.getElementById('catalogCount').textContent = `${ui.number.format(data.totalCount || 0)} ${config.singular}`;
            document.getElementById('catalogSummary').textContent = `Hiển thị ${records.length} / ${ui.number.format(data.totalCount || 0)} mục`;
            ui.pagination(document.getElementById('catalogPagination'), data.totalPages || 0, page, next => { page = next; load(); });
        } catch (e) {
            if (current !== version) return;
            document.getElementById('catalogCount').textContent = 'Chưa tải được danh sách';
            ui.empty(tableArea, 'Chưa tải được danh mục', e.message, () => load(false));
        } finally { if (current === version) tableArea.setAttribute('aria-busy', 'false'); }
    }
    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (!form.reportValidity()) return;
        const body = new FormData(form);
        if (editing) body.set(config.idKey, editing[config.idKey]);
        save.disabled = true; error.hidden = true;
        const label = save.textContent; save.textContent = 'Đang lưu…';
        try { await ui.request(`${endpoint}?handler=${editing ? 'AjaxEdit' : 'AjaxAdd'}`, body); modal.hide(); showToast(`Đã ${editing ? 'cập nhật' : 'thêm'} ${config.singular}.`); await load(); }
        catch (e) { error.textContent = e.message; error.hidden = false; error.focus(); }
        finally { save.disabled = false; save.textContent = label; }
    });
    document.getElementById('catalogAdd').addEventListener('click', () => edit(null));
    document.getElementById('catalogSearchForm').addEventListener('submit', event => { event.preventDefault(); page = 1; load(); });
    document.getElementById('catalogReset').addEventListener('click', () => { search.value = ''; page = 1; load(); });
    document.getElementById('catalogEditor').addEventListener('shown.bs.modal', () => fields.querySelector('input,textarea').focus());
    window.addEventListener('popstate', () => { readUrl(); load(false); });
    readUrl(); load(false);
})();

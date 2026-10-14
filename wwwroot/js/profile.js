(() => {
    const ui = ReaderUI;
    function bind(formId, handler, payload, success) {
        const form = document.getElementById(formId);
        form.addEventListener('submit', async event => {
            event.preventDefault();
            const button = form.querySelector('button[type="submit"]'); button.disabled = true;
            try {
                const data = payload();
                data.userId = Number(form.dataset.userId);
                const result = await ui.request(`${location.pathname}?handler=${handler}`, data, true);
                success(result);
                bootstrap.Modal.getOrCreateInstance(form.closest('.modal')).hide();
            } catch (error) { ui.formError(form, error.message); }
            finally { button.disabled = false; }
        });
    }
    bind('editProfileForm', 'Update', () => ({ fullName: document.getElementById('profileFullNameInput').value.trim(), email: document.getElementById('profileEmailInput').value.trim() }), result => {
        document.getElementById('profileFullName').textContent = result.user.fullName;
        document.getElementById('profileEmail').textContent = result.user.email;
        document.getElementById('profileUpdatedAt').replaceChildren(ui.time(result.user.updatedAt));
        showToast('Đã cập nhật hồ sơ.');
    });
    bind('changePasswordForm', 'ChangePassword', () => {
        const newPassword = document.getElementById('newPassword').value;
        if (newPassword !== document.getElementById('confirmNewPassword').value) throw new Error('Mật khẩu xác nhận không khớp.');
        return { currentPassword: document.getElementById('currentPassword').value, newPassword };
    }, () => { document.getElementById('changePasswordForm').reset(); showToast('Đã đổi mật khẩu.'); });

    const modal = document.getElementById('avatarModal');
    const form = document.getElementById('avatarForm');
    const fileInput = document.getElementById('avatarFile');
    const preview = document.getElementById('avatarPreviewImage');
    const upload = document.getElementById('uploadAvatarBtn');
    const remove = document.getElementById('deleteAvatarBtn');
    const error = document.getElementById('avatarError');
    const filename = document.getElementById('avatarFileName');
    let avatarUrl = form.dataset.avatarUrl || null, objectUrl = null, selected = null, busy = false;
    document.querySelector('#avatarPreview .avatar-initial').textContent = form.dataset.initial;

    function revokePreview() { if (objectUrl) { URL.revokeObjectURL(objectUrl); objectUrl = null; } }
    function resetAvatarForm() {
        revokePreview(); selected = null; fileInput.value = ''; filename.textContent = '';
        error.hidden = true; error.textContent = ''; form.querySelector('.avatar-delete-confirm')?.remove();
        preview.hidden = !avatarUrl;
        if (avatarUrl) preview.src = avatarUrl; else preview.removeAttribute('src');
        upload.disabled = true; remove.disabled = busy || !avatarUrl;
    }
    function avatarError(message) { error.textContent = message; error.hidden = false; }
    function setBusy(value) {
        busy = value; fileInput.disabled = value; upload.disabled = value || !selected; remove.disabled = value || !avatarUrl;
        upload.textContent = value ? 'Đang lưu…' : 'Lưu ảnh';
        form.querySelectorAll('[data-bs-dismiss="modal"]').forEach(button => { button.disabled = value; });
    }
    function saved(result) {
        avatarUrl = result.user.avatarUrl;
        form.dataset.avatarUrl = avatarUrl || '';
        ui.updateAvatars(result.user.userId, avatarUrl);
        document.getElementById('profileUpdatedAt').replaceChildren(ui.time(result.user.updatedAt));
        setBusy(false);
        resetAvatarForm(); bootstrap.Modal.getOrCreateInstance(modal).hide();
    }
    modal.addEventListener('show.bs.modal', resetAvatarForm);
    modal.addEventListener('hidden.bs.modal', resetAvatarForm);
    modal.addEventListener('hide.bs.modal', event => { if (busy) event.preventDefault(); });
    preview.addEventListener('error', () => {
        preview.hidden = true;
        if (selected) { selected = null; upload.disabled = true; avatarError('Không xem trước được ảnh. Chọn ảnh JPG, PNG hoặc WebP khác.'); }
    });
    fileInput.addEventListener('change', () => {
        resetAvatarSelection(fileInput.files[0]);
    });
    function resetAvatarSelection(file) {
        revokePreview(); selected = null; upload.disabled = true;
        form.querySelector('.avatar-delete-confirm')?.remove();
        filename.textContent = ''; error.hidden = true;
        if (!file) { resetAvatarForm(); return; }
        if (file.size > 2 * 1024 * 1024 || !file.size) { resetAvatarForm(); avatarError('Chọn ảnh có dung lượng tối đa 2 MB.'); return; }
        if (!/\.(jpe?g|png|webp)$/i.test(file.name)) { resetAvatarForm(); avatarError('Chỉ nhận ảnh JPG, PNG hoặc WebP.'); return; }
        selected = file; objectUrl = URL.createObjectURL(file); preview.src = objectUrl; preview.hidden = false;
        filename.textContent = file.name; upload.disabled = false;
    }
    form.addEventListener('submit', async event => {
        event.preventDefault(); if (!selected || busy) return;
        error.hidden = true; setBusy(true);
        try {
            const body = new FormData(); body.append('avatar', selected);
            const response = await fetch(`${location.pathname}?handler=UploadAvatar`, { method: 'POST', body, credentials: 'same-origin', headers: { RequestVerificationToken: document.querySelector('input[name="__RequestVerificationToken"]').value } });
            const result = await response.json().catch(() => { throw new Error('Không lưu được ảnh. Vui lòng thử lại.'); });
            if (!response.ok || !result.success) throw new Error(result.message || 'Không lưu được ảnh. Vui lòng thử lại.');
            saved(result); showToast('Đã cập nhật ảnh đại diện.');
        } catch (failure) { avatarError(failure instanceof TypeError ? 'Không kết nối được. Kiểm tra kết nối rồi thử lại.' : failure.message); }
        finally { setBusy(false); }
    });
    remove.addEventListener('click', () => {
        if (!avatarUrl || busy || form.querySelector('.avatar-delete-confirm')) return;
        const confirm = ui.el('div', 'avatar-delete-confirm');
        confirm.append(ui.el('p', '', 'Xóa ảnh đại diện và dùng lại chữ cái tên tài khoản?'));
        const cancel = ui.button('Giữ ảnh', 'btn btn-secondary', () => confirm.remove());
        const accept = ui.button('Xóa ảnh', 'btn btn-danger', async () => {
            if (busy) return; accept.disabled = true; cancel.disabled = true; setBusy(true);
            try {
                const result = await ui.request(`${location.pathname}?handler=DeleteAvatar`, {});
                saved(result); showToast('Đã xóa ảnh đại diện.');
            } catch (failure) { avatarError(failure.message); accept.disabled = false; cancel.disabled = false; }
            finally { setBusy(false); }
        });
        accept.id = 'confirmDeleteAvatarBtn'; confirm.append(cancel, accept);
        form.querySelector('.modal-body').append(confirm); cancel.focus();
    });
    window.addEventListener('pagehide', revokePreview);
})();

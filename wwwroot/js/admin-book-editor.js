'use strict';
(() => {
    const form = document.getElementById('bookEditorForm');
    const fileInput = document.getElementById('ImageFile');
    const preview = document.getElementById('bookCoverPreview');
    const error = document.getElementById('coverError');
    const save = document.getElementById('bookEditorSave');
    const initialCover = preview.src;
    let dirty = false, submitting = false, previewUrl;
    form.addEventListener('input', () => { dirty = true; });
    form.addEventListener('change', () => { dirty = true; });
    fileInput.addEventListener('change', () => {
        error.textContent = ''; fileInput.setCustomValidity('');
        const file = fileInput.files[0];
        if (previewUrl) URL.revokeObjectURL(previewUrl);
        preview.src = initialCover;
        if (!file) return;
        if (file.size > 5 * 1024 * 1024 || !/\.(jpg|jpeg|png|gif|webp)$/i.test(file.name)) {
            const message = 'Chọn ảnh JPG, PNG, GIF hoặc WebP không quá 5 MB.';
            error.textContent = message; fileInput.setCustomValidity(message); return;
        }
        previewUrl = URL.createObjectURL(file); preview.src = previewUrl;
    });
    form.addEventListener('submit', event => {
        if (!form.reportValidity() || (window.jQuery && !$(form).valid())) { event.preventDefault(); return; }
        submitting = true; save.disabled = true; save.textContent = 'Đang lưu…';
    });
    window.addEventListener('beforeunload', event => { if (dirty && !submitting) { event.preventDefault(); event.returnValue = ''; } });
})();

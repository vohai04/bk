// Toast thông báo
function showToast(message, type = "success", duration = 3000) {
    const iconMap = {
        success: "bi-check-circle-fill text-success",
        error: "bi-x-circle-fill text-danger",
        warning: "bi-exclamation-triangle-fill text-warning",
        info: "bi-info-circle-fill text-info"
    };
    $("#toastMessage").empty().append($('<i>').addClass(`${iconMap[type] || iconMap.info} me-2`).attr('aria-hidden', 'true'), document.createTextNode(String(message)));
    const toast = new bootstrap.Toast(document.getElementById("sharedToast"), { delay: duration });
    toast.show();
}

// Modal xác nhận (dùng Bootstrap modal runtime)
function showConfirmModal(title, message, onConfirm) {
    const modalHtml = `
        <div class="modal fade" id="confirmModal" tabindex="-1" aria-labelledby="confirmModalTitle">
            <div class="modal-dialog">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title" id="confirmModalTitle"></h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Đóng xác nhận"></button>
                    </div>
                    <div class="modal-body" style="white-space: pre-line"></div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Hủy</button>
                        <button type="button" class="btn btn-danger" id="confirmBtn" disabled>Xác nhận</button>
                    </div>
                </div>
            </div>
        </div>`;
    $("#confirmModal").remove();
    $("body").append(modalHtml);
    $('#confirmModalTitle').text(title);
    $('#confirmModal .modal-body').text(message);
    const element = document.getElementById('confirmModal');
    const modal = new bootstrap.Modal(element);
    const confirm = element.querySelector('#confirmBtn');
    element.addEventListener('shown.bs.modal', () => { confirm.disabled = false; }, { once: true });
    element.addEventListener('hidden.bs.modal', () => { modal.dispose(); element.remove(); }, { once: true });
    $("#confirmBtn").on('click', function () {
        if (confirm.disabled) return;
        confirm.disabled = true;
        try { onConfirm && onConfirm(); } finally { modal.hide(); }
    });
    modal.show();
}

// Khởi tạo tooltip Popper/Bootstrap toàn cục
function initTooltips() {
    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) {
        bootstrap.Tooltip.getOrCreateInstance(el, { trigger: 'hover focus' });
    });
}

// Phân trang tiện ích
function renderPagination(containerSelector, totalPages, currentPage, onPageClick) {
    const $pagination = $(containerSelector);
    $pagination.empty();
    if (totalPages <= 1) return;
    $pagination.append(`<li class="page-item ${currentPage === 1 ? "disabled" : ""}"><a class="page-link" href="#" data-page="${currentPage - 1}">&laquo;</a></li>`);
    const maxPages = 5;
    let start = Math.max(1, currentPage - Math.floor(maxPages / 2));
    let end = Math.min(totalPages, start + maxPages - 1);
    if (end - start < maxPages - 1) start = Math.max(1, end - maxPages + 1);
    for (let i = start; i <= end; i++) {
        $pagination.append(`<li class="page-item ${i === currentPage ? "active" : ""}"><a class="page-link" href="#" data-page="${i}">${i}</a></li>`);
    }
    $pagination.append(`<li class="page-item ${currentPage === totalPages ? "disabled" : ""}"><a class="page-link" href="#" data-page="${currentPage + 1}">&raquo;</a></li>`);
    $pagination.off("click").on("click", ".page-link", function (e) {
        e.preventDefault();
        const page = parseInt($(this).data("page"), 10);
        if (!isNaN(page) && page >= 1 && page <= totalPages) onPageClick(page);
    });
}

// Khởi chạy chung
$(document).ready(function () {
    initTooltips();
});


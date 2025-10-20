// Toast thông báo
function showToast(message, type = "success", duration = 3000) {
    const iconMap = {
        success: "bi-check-circle-fill text-success",
        error: "bi-x-circle-fill text-danger",
        warning: "bi-exclamation-triangle-fill text-warning",
        info: "bi-info-circle-fill text-info"
    };
    $("#toastMessage").html(`<i class="${iconMap[type]} me-2"></i>${message}`);
    const toast = new bootstrap.Toast(document.getElementById("sharedToast"), { delay: duration });
    toast.show();
}

// Modal xác nhận (dùng Bootstrap modal runtime)
function showConfirmModal(title, message, onConfirm) {
    const modalHtml = `
        <div class="modal fade" id="confirmModal" tabindex="-1">
            <div class="modal-dialog">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title">${title}</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">${message}</div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Hủy</button>
                        <button type="button" class="btn btn-danger" id="confirmBtn">Xác nhận</button>
                    </div>
                </div>
            </div>
        </div>`;
    $("#confirmModal").remove();
    $("body").append(modalHtml);
    const modal = new bootstrap.Modal(document.getElementById('confirmModal'));
    $("#confirmBtn").on('click', function () {
        try { onConfirm && onConfirm(); } finally { modal.hide(); }
    });
    modal.show();
}

// Khởi tạo tooltip Popper/Bootstrap toàn cục
function initTooltips() {
    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) {
        new bootstrap.Tooltip(el, { trigger: 'hover focus' });
    });
}

// Header hide/show on scroll (hide when scrolling down, show when scrolling up)
(function () {
    const $header = $(".modern-header");
    if ($header.length === 0) return;

    let lastScroll = window.pageYOffset || document.documentElement.scrollTop;
    let ticking = false;
    const delta = 10; // minimum change to trigger

    function update() {
        const current = window.pageYOffset || document.documentElement.scrollTop;
        if (Math.abs(current - lastScroll) <= delta) {
            ticking = false;
            return;
        }

        if (current > lastScroll && current > ($header.outerHeight() || 64)) {
            // scrolling down
            $header.addClass('header-hidden');
        } else if (current < lastScroll) {
            // scrolling up
            $header.removeClass('header-hidden');
        }

        lastScroll = current;
        ticking = false;
    }

    window.addEventListener('scroll', function () {
        if (!ticking) {
            window.requestAnimationFrame(update);
            ticking = true;
        }
    }, { passive: true });
})();

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

// Sidebar search form submit => phát event toàn cục
$(document).on('submit', '#sidebarSearchForm', function (e) {
    e.preventDefault();
    const payload = {
        title: $('#sb_title').val() || '',
        author: $('#sb_author').val() || '',
        category: $('#sb_category').val() || '',
        year: $('#sb_year').val() || ''
    };
    $(document).trigger('book:search', payload);
    const offcanvasEl = document.getElementById('offcanvasSearch');
    if (offcanvasEl && offcanvasEl.classList.contains('show')) {
        bootstrap.Offcanvas.getInstance(offcanvasEl)?.hide();
    }
});

// Khởi chạy chung
$(document).ready(function () {
    initTooltips();
});

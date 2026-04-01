/**
 * layout.js - Layout-wide JavaScript
 * BookInfoFinder
 *
 * Handles: header filters, dropdown positioning,
 * admin sidebar toggle, coming-soon toast.
 */

'use strict';

/* ── Coming Soon ── */
function showComingSoon() {
    if (typeof showToast === 'function') {
        showToast('Tính năng này sẽ sớm được cập nhật! 🚀', 'info');
    } else {
        alert('Tính năng này sẽ sớm được cập nhật! 🚀');
    }
}

/* ── Header Tag Filter ── */
let selectedTags = [];
let availableTagsData = [];

function initHeaderFilters() {
    loadAvailableTags();

    $('#tagSearchInput').on('input', function () {
        filterAvailableTags($(this).val().toLowerCase());
    });

    $('#applyTagFilter').on('click', function () {
        if (selectedTags.length > 0) {
            window.location.href = `/Index?tag=${encodeURIComponent(selectedTags.join(','))}`;
        } else {
            if (typeof showToast === 'function') showToast('Vui lòng chọn ít nhất một thẻ', 'warning');
        }
    });

    $('#clearTagFilter').on('click', function () {
        selectedTags = [];
        updateSelectedTagsDisplay();
        filterAvailableTags('');
    });

    $('.filter-item').on('click', function (e) {
        e.preventDefault();
        const filter = $(this).data('filter');
        if (filter) window.location.href = `/Index?filter=${filter}`;
    });
}

function loadAvailableTags() {
    $.get('/Index?handler=TagCounts')
        .done(function (tags) {
            availableTagsData = tags || [];
            displayAvailableTags(availableTagsData);
        })
        .catch(function () { console.log('Could not load tags'); });
}

function displayAvailableTags(tags) {
    const $container = $('#availableTags');
    $container.empty();

    tags.forEach(function (tag, index) {
        const isSelected = selectedTags.includes(tag.name);
        const $wrap = $(`
            <div class="mb-2">
                <div class="form-check">
                    <input class="form-check-input" type="checkbox"
                           id="filter_tag_${index}"
                           data-tag="${tag.name}"
                           ${isSelected ? 'checked' : ''} />
                    <label class="form-check-label" for="filter_tag_${index}">
                        ${tag.name} <span class="text-muted">(${tag.count || 0})</span>
                    </label>
                </div>
            </div>
        `);
        $wrap.find('input').on('change', function () { toggleTag(tag.name); });
        $container.append($wrap);
    });
}

function filterAvailableTags(searchTerm) {
    displayAvailableTags(availableTagsData.filter(t =>
        t.name.toLowerCase().includes(searchTerm)
    ));
}

function toggleTag(tagName) {
    const idx = selectedTags.indexOf(tagName);
    if (idx > -1) {
        selectedTags.splice(idx, 1);
    } else {
        selectedTags.push(tagName);
    }
    updateSelectedTagsDisplay();
    displayAvailableTags(availableTagsData.filter(t =>
        t.name.toLowerCase().includes($('#tagSearchInput').val().toLowerCase())
    ));
}

function updateSelectedTagsDisplay() {
    const $container = $('#selectedTags');
    $container.empty();

    selectedTags.forEach(function (tagName) {
        const $tag = $(`
            <span class="selected-tag">
                ${tagName}
                <span class="remove-tag" data-tag="${tagName}">&times;</span>
            </span>
        `);
        $tag.find('.remove-tag').on('click', function () { toggleTag(tagName); });
        $container.append($tag);
    });
}

/* ── Dropdown Positioning ── */
function initDropdownPositioning() {
    $('.dropdown').on('show.bs.dropdown', function () {
        const $menu = $(this).find('.dropdown-menu');

        // Reset
        $menu.removeClass('dropdown-menu-right dropdown-menu-left dropdown-menu-end');
        $menu.css({ left: '', right: '', 'max-width': '', 'min-width': '', position: '', top: '', transform: '' });

        setTimeout(function () {
            if ($(window).width() <= 991) {
                $menu.css({ position: 'static', width: '100%', transform: 'none',
                            'margin-top': '0.25rem', 'margin-bottom': '0.25rem',
                            float: 'none', left: '0', right: 'auto' });
                $menu.removeClass('dropdown-menu-end');
            } else {
                const offset = $menu.offset();
                const mw = $menu.outerWidth();
                if (offset && (offset.left + mw > $(window).width() - 20)) {
                    $menu.addClass('dropdown-menu-end');
                }
            }
        }, 10);
    });

    // Navbar collapse always left-aligned on mobile
    $('.navbar-collapse').on('show.bs.collapse', function () {
        if ($(window).width() <= 991) {
            $(this).css({ left: '1rem', right: 'auto' });
        }
    });

    // Hide dropdowns on resize
    $(window).on('resize.dropdowns', function () {
        $('.dropdown-menu.show').each(function () {
            $(this).closest('.dropdown')
                   .find('[data-bs-toggle="dropdown"]')
                   .dropdown('hide');
        });
    });
}

/* ── Admin Sidebar Toggle ── */
function syncSidebarStateToBody() {
    if ($(window).width() < 768) {
        $('body').removeClass('admin-sidebar-open');
        return;
    }
    if ($('.admin-sidebar').length && !$('.admin-sidebar').hasClass('collapsed')) {
        $('body').addClass('admin-sidebar-open');
    } else {
        $('body').removeClass('admin-sidebar-open');
    }
}

// Delegated handler (works even if layout re-renders)
$(document).on('click', '#sidebarToggle', function () {
    const $icon = $(this).find('i');

    if ($(window).width() < 768) {
        // Mobile: overlay
        $('.admin-sidebar').toggleClass('show');
    } else {
        // Desktop: collapse / expand
        $('.admin-sidebar').toggleClass('collapsed');
        $('.admin-main').toggleClass('expanded');
        const isCollapsed = $('.admin-sidebar').hasClass('collapsed');

        $('body').toggleClass('admin-sidebar-open', !isCollapsed);

        $icon.toggleClass('bi-chevron-left', !isCollapsed)
             .toggleClass('bi-chevron-right', isCollapsed);
    }
});

// Close sidebar when clicking outside on mobile
$(document).on('click', function (e) {
    if ($(window).width() < 768) {
        if (!$(e.target).closest('.admin-sidebar, #sidebarToggle').length) {
            $('.admin-sidebar').removeClass('show');
        }
    }
});

$(window).on('resize.sidebar', syncSidebarStateToBody);

/* ── Init on DOM Ready ── */
$(document).ready(function () {
    console.log('Layout scripts loaded');
    $('[data-bs-toggle="tooltip"]').tooltip();
    initHeaderFilters();
    initDropdownPositioning();
    syncSidebarStateToBody();
});

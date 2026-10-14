'use strict';

// Share the existing Razor antiforgery token with every same-origin jQuery mutation.
$.ajaxPrefilter(function (options, original, xhr) {
    const url = new URL(options.url, window.location.href);
    if (url.origin === window.location.origin && !/^(GET|HEAD|OPTIONS)$/i.test(options.type)) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (token) xhr.setRequestHeader('RequestVerificationToken', token);
    }
});

(function () {
    const toggle = document.getElementById('workspaceMenuToggle');
    const sidebar = document.getElementById('workspaceSidebar');
    const backdrop = document.querySelector('.sidebar-backdrop');
    if (!toggle || !sidebar || !backdrop) return;
    const mobile = window.matchMedia('(max-width: 800px)');

    function setOpen(open, restoreFocus = false) {
        document.body.classList.toggle('menu-open', open);
        toggle.setAttribute('aria-expanded', String(open));
        backdrop.hidden = !open;
        sidebar.inert = mobile.matches && !open;
        if (open) sidebar.querySelector('a')?.focus();
        if (restoreFocus) toggle.focus();
    }
    toggle.addEventListener('click', () => setOpen(!document.body.classList.contains('menu-open')));
    backdrop.addEventListener('click', () => setOpen(false, true));
    document.addEventListener('keydown', event => {
        if (!document.body.classList.contains('menu-open')) return;
        if (event.key === 'Escape') setOpen(false, true);
        if (event.key === 'Tab') {
            const links = Array.from(sidebar.querySelectorAll('a,button'));
            const first = links[0], last = links[links.length - 1];
            if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
            else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
        }
    });
    mobile.addEventListener('change', () => setOpen(false));
    setOpen(false);
})();

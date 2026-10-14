'use strict';
(() => {
    const ui = AdminUI;
    const status = document.getElementById('reportStatus');
    const refresh = document.getElementById('refreshReport');
    function distribution(key, data) {
        const area = document.getElementById(`report_${key}`); area.replaceChildren();
        const labels = data?.labels || [], values = data?.data || [];
        if (!labels.length) { area.append(ui.el('p', 'table-meta', 'Chưa có dữ liệu cho báo cáo này.')); return; }
        const maximum = Math.max(1, ...values.map(Number));
        for (let index = 0; index < labels.length; index++) {
            const row = ui.el('div', 'report-bar-row');
            const name = ui.el('span', 'report-bar-label', labels[index]); name.title = labels[index];
            const track = ui.el('div', 'report-bar-track'), bar = ui.el('div');
            bar.style.width = `${Math.max(0, (Number(values[index]) || 0) / maximum * 100)}%`; track.append(bar);
            row.append(name, track, ui.el('strong', '', ui.number.format(values[index] || 0))); area.append(row);
        }
    }
    async function load() {
        refresh.disabled = true; status.textContent = 'Đang tải báo cáo…';
        try {
            const data = await ui.request('/Admin/Report?handler=GetStatistics');
            document.querySelectorAll('[data-stat]').forEach(node => { node.textContent = ui.number.format(data.statistics[node.dataset.stat] || 0); });
            for (const key of ['yearData', 'categoryData', 'authorData', 'publisherData']) distribution(key, data.chartData[key]);
            status.textContent = 'Báo cáo từ dữ liệu hiện có trong thư viện.';
        } catch (e) { status.textContent = e.message; showToast(e.message, 'error'); }
        finally { refresh.disabled = false; }
    }
    document.getElementById('reportExportForm').addEventListener('submit', function () { const period = document.getElementById('reportPeriod').value; this.action = `/Admin/Report?handler=Export${period}Pdf`; });
    refresh.addEventListener('click', load); load();
})();

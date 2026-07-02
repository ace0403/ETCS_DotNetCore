'use strict';

(function () {
    function readUrls() {
        var el = document.getElementById('dashboard-urls');
        if (!el) return { overviewUrl: '/Dashboard/GetOverviewJson' };
        try {
            return JSON.parse(el.textContent || '{}');
        } catch (e) {
            return { overviewUrl: '/Dashboard/GetOverviewJson' };
        }
    }

    function apiUrl(relativePath) {
        var path = relativePath || '';
        if (!path.startsWith('/')) path = '/' + path;
        var base = (typeof SiteUrl !== 'undefined' && SiteUrl) ? String(SiteUrl).replace(/\/?$/, '') : '';
        return base + path;
    }

    function setText(id, value) {
        var n = document.getElementById(id);
        if (n) n.textContent = value;
    }

    function showDashboardLoader() {
        var el = document.getElementById('dashboard-title-loader');
        if (!el) return;
        el.classList.remove('d-none');
        el.setAttribute('aria-busy', 'true');
    }

    function hideDashboardLoader() {
        var el = document.getElementById('dashboard-title-loader');
        if (!el) return;
        el.classList.add('d-none');
        el.setAttribute('aria-busy', 'false');
    }

    function todayIso() {
        var d = new Date();
        return formatDateYmd(d);
    }

    function formatDateYmd(d) {
        if (!d || !(d instanceof Date) || isNaN(d.getTime())) return todayIso();
        var y = d.getFullYear();
        var m = String(d.getMonth() + 1).padStart(2, '0');
        var day = String(d.getDate()).padStart(2, '0');
        return y + '-' + m + '-' + day;
    }

    function addDaysIso(isoDate, deltaDays) {
        var p = isoDate.split('-');
        var dt = new Date(parseInt(p[0], 10), parseInt(p[1], 10) - 1, parseInt(p[2], 10));
        dt.setDate(dt.getDate() + deltaDays);
        return formatDateYmd(dt);
    }

    function getRangeBounds() {
        var rangeEl = document.getElementById('dashboard-range');
        var range = rangeEl ? rangeEl.value : 'today';
        var t = todayIso();
        var start;
        var end = t;

        if (range === 'custom') {
            var fp = window.dashboardFlatpickr;
            if (fp && fp.selectedDates && fp.selectedDates.length === 2) {
                start = formatDateYmd(fp.selectedDates[0]);
                end = formatDateYmd(fp.selectedDates[1]);
                if (start > end) {
                    var tmp = start;
                    start = end;
                    end = tmp;
                }
                return { start: start, end: end };
            }
            if (fp && fp.selectedDates && fp.selectedDates.length === 1) {
                start = end = formatDateYmd(fp.selectedDates[0]);
                return { start: start, end: end };
            }
            start = addDaysIso(t, -6);
            return { start: start, end: t };
        }

        switch (range) {
            case 'today':
                start = t;
                break;
            case 'lastWeek':
                start = addDaysIso(t, -6);
                break;
            case 'lastMonth':
                start = addDaysIso(t, -29);
                break;
            case 'lastYear':
                start = addDaysIso(t, -364);
                break;
            default:
                start = t;
        }
        return { start: start, end: end };
    }

    function buildQueryParams() {
        var rangeEl = document.getElementById('dashboard-range');
        var range = rangeEl ? rangeEl.value : 'today';
        var bounds = getRangeBounds();
        var params = new URLSearchParams();
        params.set('range', range);
        params.set('startDate', bounds.start);
        params.set('endDate', bounds.end);
        var cc = document.getElementById('dashboard-cost-center');
        if (cc && cc.value) params.set('costCenter', cc.value);
        var mt = document.getElementById('dashboard-meal-type');
        if (mt && mt.value) params.set('mealType', mt.value);
        return params.toString();
    }

    function formatMoneyAed(n) {
        if (n == null || isNaN(n)) return '—';
        var v = Number(n);
        return 'AED ' + v.toLocaleString(undefined, { maximumFractionDigits: 0, minimumFractionDigits: 0 });
    }

    function formatMoneyShort(n) {
        if (n == null || isNaN(n)) return '—';
        return Number(n).toLocaleString(undefined, { maximumFractionDigits: 0, minimumFractionDigits: 0 });
    }

    function mergeCostCenterOptions(options, selectEl) {
        if (!selectEl || !options || !options.length) return;
        var current = selectEl.value;
        var existing = {};
        for (var i = 0; i < selectEl.options.length; i++) {
            existing[selectEl.options[i].value] = true;
        }
        options.forEach(function (o) {
            if (!o || existing[o]) return;
            var opt = document.createElement('option');
            opt.value = o;
            opt.textContent = o;
            selectEl.appendChild(opt);
            existing[o] = true;
        });
        if (current && existing[current]) selectEl.value = current;
    }

    function renderKpis(data) {
        var s = data.summary || {};
        var totalU = s.totalUsers != null ? s.totalUsers : 0;
        var withMeal = data.usersWithMealInPeriod != null ? data.usersWithMealInPeriod : 0;
        setText('dash-users-active', String(withMeal));
        setText('dash-users-total', String(totalU));

        var trendEl = document.getElementById('dash-meal-trend');
        var trend = data.mealCountTrendPercentVsPrior;
        if (trendEl) {
            if (trend != null && !isNaN(trend)) {
                trendEl.classList.remove('d-none');
                var up = trend >= 0;
                trendEl.className = 'dashboard-trend-pill ' + (up ? 'dashboard-trend-up' : 'dashboard-trend-down');
                trendEl.textContent = (up ? '▲ ' : '▼ ') + Math.abs(trend).toFixed(1) + '% vs prior period';
            } else {
                trendEl.classList.add('d-none');
            }
        }

        var mt = data.mealsToday != null ? data.mealsToday : 0;
        setText('dash-meals-today', String(mt));
        var bar = document.getElementById('dash-meals-today-bar');
        if (bar) {
            var denom = Math.max(s.mealCount || 0, 1);
            var pct = Math.min(100, Math.round((mt / denom) * 100));
            bar.style.width = pct + '%';
        }

        var emp = Number(s.totalEmployeePay) || 0;
        var co = Number(s.totalHiltiPay) || 0;
        var tot = emp + co;
        setText('dash-total-cost', formatMoneyAed(tot));
        setText('dash-cost-emp', formatMoneyAed(emp));
        setText('dash-cost-co', formatMoneyAed(co));

        var sub = data.companySubsidyPercent != null ? Number(data.companySubsidyPercent) : 0;
        setText('dash-subsidy-pct', sub.toFixed(0) + '%');
        var empPct = tot > 0 ? Math.round(1000 * (emp / tot)) / 10 : 0;
        var coPct = tot > 0 ? Math.round(1000 * (co / tot)) / 10 : 0;
        setText('dash-subsidy-legend', empPct + '% employee · ' + coPct + '% company');

        var avg = data.avgCostPerMeal;
        setText('dash-avg-cost', avg != null ? formatMoneyAed(avg) : '—');
        var delta = document.getElementById('dash-avg-cost-delta');
        if (delta) {
            var avt = data.avgCostTrendPercentVsPrior;
            if (avt != null && !isNaN(avt)) {
                var dir = avt >= 0 ? 'up' : 'down';
                delta.textContent = (avt >= 0 ? '+' : '') + avt.toFixed(1) + '% avg cost vs prior period';
                delta.className = 'small mt-2 ' + (avt >= 0 ? 'text-danger' : 'text-success');
            } else {
                delta.textContent = '';
            }
        }

        renderSubsidyDonut(empPct, coPct);
    }

    function renderSubsidyDonut(empPct, coPct) {
        var el = document.getElementById('dashboard-subsidy-donut');
        if (!el || typeof ApexCharts === 'undefined') return;
        var s = [empPct, coPct];
        if (s[0] + s[1] <= 0) {
            el.innerHTML = '';
            return;
        }
        var options = {
            chart: { type: 'donut', height: 120, sparkline: { enabled: false } },
            labels: ['Employee', 'Company'],
            series: s,
            colors: ['#4680FF', '#E58A00'],
            legend: { show: false },
            dataLabels: { enabled: false },
            plotOptions: { pie: { donut: { size: '65%' } } }
        };
        if (window.dashboardSubsidyChart) window.dashboardSubsidyChart.destroy();
        window.dashboardSubsidyChart = new ApexCharts(el, options);
        window.dashboardSubsidyChart.render();
    }

    function renderComboChart(data) {
        var el = document.getElementById('dashboard-combo-chart');
        var empty = document.getElementById('dashboard-combo-empty');
        var hint = document.getElementById('dashboard-combo-hint');
        if (!el || typeof ApexCharts === 'undefined') return;

        var monthly = data.useMonthlySeries;
        var points = monthly ? (data.monthlyDetails || []) : (data.dailyDetails || []);

        if (hint) {
            hint.textContent = monthly
                ? 'Meals (bars) and total cost (line) by month'
                : 'Meals (bars) and total cost (line) by day';
        }

        if (!points.length) {
            el.classList.add('d-none');
            if (empty) empty.classList.remove('d-none');
            if (window.dashboardComboChart) window.dashboardComboChart.destroy();
            return;
        }
        el.classList.remove('d-none');
        if (empty) empty.classList.add('d-none');

        var categories = points.map(function (p) {
            return monthly ? (p.label || p.month) : formatDisplayDate(p.day);
        });
        var meals = points.map(function (p) { return Number(p.mealCount) || 0; });
        var costs = points.map(function (p) { return Math.round(Number(p.totalCost) || 0); });

        var options = {
            chart: { type: 'line', height: 300, toolbar: { show: false }, zoom: { enabled: false } },
            stroke: { width: [0, 3], curve: 'smooth' },
            plotOptions: { bar: { columnWidth: monthly ? '70%' : '55%', borderRadius: 4 } },
            dataLabels: { enabled: false },
            colors: ['#4680FF', '#E58A00'],
            series: [
                { name: 'Meals', type: 'column', data: meals },
                { name: 'Total cost (AED)', type: 'line', data: costs }
            ],
            xaxis: {
                categories: categories,
                labels: { rotate: monthly ? 0 : -45, rotateAlways: categories.length > 10 }
            },
            yaxis: [
                {
                    seriesName: 'Meals',
                    title: { text: 'Meals' },
                    labels: { formatter: function (v) { return Math.round(v); } }
                },
                {
                    seriesName: 'Total cost (AED)',
                    opposite: true,
                    title: { text: 'AED' },
                    labels: { formatter: function (v) { return Math.round(v); } }
                }
            ],
            tooltip: {
                shared: true,
                intersect: false,
                y: [
                    { formatter: function (v) { return v + ' meals'; } },
                    { formatter: function (v) { return 'AED ' + v; } }
                ]
            },
            legend: { position: 'top', horizontalAlign: 'right' },
            grid: { strokeDashArray: 4 }
        };

        if (window.dashboardComboChart) window.dashboardComboChart.destroy();
        window.dashboardComboChart = new ApexCharts(el, options);
        window.dashboardComboChart.render();
    }

    function formatDisplayDate(iso) {
        if (!iso) return '';
        var p = String(iso).split('-');
        if (p.length !== 3) return iso;
        return p[2] + '/' + p[1] + '/' + p[0];
    }

    function renderStackedChart(data) {
        var el = document.getElementById('dashboard-stacked-chart');
        var empty = document.getElementById('dashboard-stacked-empty');
        if (!el || typeof ApexCharts === 'undefined') return;

        var monthly = data.useMonthlySeries;
        var points = monthly ? (data.monthlyDetails || []) : (data.dailyDetails || []);

        if (!points.length) {
            el.classList.add('d-none');
            if (empty) empty.classList.remove('d-none');
            if (window.dashboardStackedChart) window.dashboardStackedChart.destroy();
            return;
        }
        el.classList.remove('d-none');
        if (empty) empty.classList.add('d-none');

        var categories = points.map(function (p) {
            return monthly ? (p.label || p.month) : formatDisplayDate(p.day);
        });
        var emp = points.map(function (p) { return Math.round(Number(p.totalEmployeePay) || 0); });
        var co = points.map(function (p) { return Math.round(Number(p.totalHiltiPay) || 0); });

        var options = {
            chart: { type: 'bar', height: 300, stacked: true, toolbar: { show: false } },
            plotOptions: { bar: { horizontal: false, columnWidth: monthly ? '70%' : '55%', borderRadius: 2 } },
            colors: ['#4680FF', '#E58A00'],
            series: [
                { name: 'Employee', data: emp },
                { name: 'Company', data: co }
            ],
            xaxis: {
                categories: categories,
                labels: { rotate: monthly ? 0 : -45, rotateAlways: categories.length > 10 }
            },
            yaxis: { labels: { formatter: function (v) { return Math.round(v); } } },
            dataLabels: {
                enabled: false
            },
            tooltip: { y: { formatter: function (v) { return 'AED ' + v; } } },
            legend: { position: 'top', horizontalAlign: 'right' },
            grid: { strokeDashArray: 4 }
        };

        if (window.dashboardStackedChart) window.dashboardStackedChart.destroy();
        window.dashboardStackedChart = new ApexCharts(el, options);
        window.dashboardStackedChart.render();
    }

    function renderTopCcList(rows) {
        var ul = document.getElementById('dashboard-top-cc-list');
        var empty = document.getElementById('dashboard-top-cc-empty');
        if (!ul) return;
        ul.innerHTML = '';
        if (!rows || !rows.length) {
            if (empty) empty.classList.remove('d-none');
            return;
        }
        if (empty) empty.classList.add('d-none');
        rows.slice(0, 8).forEach(function (r) {
            var li = document.createElement('li');
            li.className = 'list-group-item px-2 py-2 d-flex justify-content-between align-items-center';
            var left = document.createElement('div');
            left.className = 'small fw-medium text-truncate me-2';
            left.style.maxWidth = '60%';
            left.textContent = r.costCenterName || '';
            var right = document.createElement('div');
            right.className = 'text-end small';
            var span = document.createElement('span');
            span.className = 'fw-semibold';
            span.textContent = formatMoneyShort(r.totalCost);
            right.appendChild(span);
            if (r.trendPercentVsPrior != null && !isNaN(r.trendPercentVsPrior)) {
                var t = document.createElement('span');
                t.className = r.trendPercentVsPrior >= 0 ? 'text-success ms-1' : 'text-danger ms-1';
                t.textContent = (r.trendPercentVsPrior >= 0 ? '▲' : '▼') + Math.abs(r.trendPercentVsPrior).toFixed(0) + '%';
                right.appendChild(t);
            }
            li.appendChild(left);
            li.appendChild(right);
            ul.appendChild(li);
        });
    }

    function renderInsights(lines) {
        var ul = document.getElementById('dashboard-insights');
        if (!ul) return;
        ul.innerHTML = '';
        (lines || []).forEach(function (txt) {
            var li = document.createElement('li');
            li.textContent = txt;
            ul.appendChild(li);
        });
    }

    function renderCcTable(rows) {
        var tbody = document.getElementById('dashboard-cc-table-body');
        if (!tbody) return;
        tbody.innerHTML = '';
        if (!rows || !rows.length) {
            tbody.innerHTML = '<tr><td colspan="8" class="text-center text-muted py-4">No data</td></tr>';
            return;
        }
        var maxAvg = 0;
        rows.forEach(function (r) {
            if (r.costCenterName !== 'Total' && r.avgCostPerMeal > maxAvg) maxAvg = r.avgCostPerMeal;
        });
        function appendRowCells(tr, r, heat) {
            function td(val, align, isAvg) {
                var cell = document.createElement('td');
                if (align) cell.className = align;
                if (isAvg && heat) cell.className = (cell.className || '') + ' dashboard-avg-heat';
                cell.textContent = val;
                return cell;
            }

            tr.appendChild(td(r.unitName || '-'));
            tr.appendChild(td(String(r.userCount), 'text-end'));
            tr.appendChild(td(String(r.mealCount), 'text-end'));
            tr.appendChild(td(formatMoneyShort(r.employeePay), 'text-end'));
            tr.appendChild(td(formatMoneyShort(r.companyPay), 'text-end'));

            var totTd = document.createElement('td');
            totTd.className = 'text-end';
            totTd.textContent = formatMoneyShort(r.totalCost);
            if (r.trendPercentVsPrior != null && !isNaN(r.trendPercentVsPrior) && r.costCenterName !== 'Total') {
                var sp = document.createElement('span');
                sp.className = r.trendPercentVsPrior >= 0 ? 'text-success ms-1' : 'text-danger ms-1';
                sp.textContent = (r.trendPercentVsPrior >= 0 ? ' ▲' : ' ▼') + Math.abs(r.trendPercentVsPrior).toFixed(0) + '%';
                totTd.appendChild(sp);
            }
            tr.appendChild(totTd);

            var avgCell = document.createElement('td');
            avgCell.className = 'text-end' + (heat ? ' dashboard-avg-heat' : '');
            avgCell.textContent = r.avgCostPerMeal != null ? r.avgCostPerMeal.toFixed(2) : '—';
            tr.appendChild(avgCell);
        }

        var totalRow = null;
        var dataRows = rows.filter(function (r) {
            var isTotal = r.costCenterName === 'Total';
            if (isTotal) totalRow = r;
            return !isTotal;
        });

        var grouped = {};
        var order = [];
        dataRows.forEach(function (r) {
            var key = r.costCenterName || '';
            if (!grouped[key]) {
                grouped[key] = { rows: [], mealCount: 0 };
                order.push(key);
            }
            grouped[key].rows.push(r);
            grouped[key].mealCount += Number(r.mealCount) || 0;
        });

        // Cost centers ordered by spend (totalCost) descending.
        order.sort(function (a, b) {
            var sa = 0;
            var sb = 0;
            grouped[a].rows.forEach(function (r) { sa += Number(r.totalCost) || 0; });
            grouped[b].rows.forEach(function (r) { sb += Number(r.totalCost) || 0; });
            if (sb !== sa) return sb - sa;
            var ca = (a || '').toLowerCase();
            var cb = (b || '').toLowerCase();
            if (ca < cb) return -1;
            if (ca > cb) return 1;
            return 0;
        });

        order.forEach(function (cc) {
            var groupRows = grouped[cc].rows;
            groupRows.sort(function (a, b) {
                var sa = Number(a.totalCost) || 0;
                var sb = Number(b.totalCost) || 0;
                if (sb !== sa) return sb - sa;
                var ua = (a.unitName || '').toLowerCase();
                var ub = (b.unitName || '').toLowerCase();
                if (ua < ub) return -1;
                if (ua > ub) return 1;
                return 0;
            });
            groupRows.forEach(function (r, idx) {
                var tr = document.createElement('tr');
                var heat = maxAvg > 0 && r.avgCostPerMeal >= maxAvg * 0.85;

                if (idx === 0) {
                    var ccCell = document.createElement('td');
                    ccCell.textContent = cc;
                    ccCell.rowSpan = groupRows.length;
                    ccCell.className = 'fw-semibold align-middle';
                    tr.appendChild(ccCell);
                }

                appendRowCells(tr, r, heat);
                tbody.appendChild(tr);
            });
        });

        if (totalRow) {
            var totalTr = document.createElement('tr');
            totalTr.className = 'table-secondary fw-semibold';

            var totalCc = document.createElement('td');
            totalCc.textContent = totalRow.costCenterName || 'Total';
            totalTr.appendChild(totalCc);

            appendRowCells(totalTr, totalRow, false);
            tbody.appendChild(totalTr);
        }
    }

    function renderBudget(budget) {
        var card = document.getElementById('dashboard-budget-card');
        if (!card) return;
        if (!budget || budget.budgetAmount == null) {
            card.classList.add('d-none');
            return;
        }
        card.classList.remove('d-none');
        setText('dash-budget-actual', formatMoneyAed(budget.actualAmount));
        setText('dash-budget-cap', formatMoneyAed(budget.budgetAmount));
        var rem = budget.remaining !== undefined ? budget.remaining : budget.budgetAmount - budget.actualAmount;
        setText('dash-budget-remaining', 'Remaining: ' + formatMoneyAed(rem));
        var bar = document.getElementById('dash-budget-bar');
        if (bar) {
            var cap = Number(budget.budgetAmount) || 1;
            var act = Math.min(Number(budget.actualAmount) || 0, cap * 2);
            var pct = Math.min(100, Math.round((act / cap) * 100));
            bar.style.width = pct + '%';
        }
    }

    function renderFrequentUsers(rows) {
        var el = document.getElementById('dashboard-frequent-users');
        if (!el) return;
        el.innerHTML = '';
        if (!rows || !rows.length) {
            el.innerHTML = '<p class="text-muted small mb-0">No data</p>';
            return;
        }
        var maxC = 0;
        rows.forEach(function (u) {
            if ((u.mealCount || 0) > maxC) maxC = u.mealCount;
        });
        rows.slice(0, 8).forEach(function (u) {
            var name = ((u.firstname || '') + ' ' + (u.lastname || '')).trim() || u.employeeKey || '';
            var wrap = document.createElement('div');
            wrap.className = 'mb-2';
            var row = document.createElement('div');
            row.className = 'd-flex justify-content-between small mb-1';
            var n = document.createElement('span');
            n.className = 'text-truncate';
            n.style.maxWidth = '70%';
            n.textContent = name;
            var c = document.createElement('span');
            c.className = 'fw-medium';
            c.textContent = String(u.mealCount != null ? u.mealCount : 0);
            row.appendChild(n);
            row.appendChild(c);
            wrap.appendChild(row);
            var prog = document.createElement('div');
            prog.className = 'progress';
            prog.style.height = '4px';
            var inner = document.createElement('div');
            inner.className = 'progress-bar bg-primary';
            var w = maxC > 0 ? Math.round((u.mealCount / maxC) * 100) : 0;
            inner.style.width = w + '%';
            prog.appendChild(inner);
            wrap.appendChild(prog);
            el.appendChild(wrap);
        });
    }

    function renderOverview(data) {
        if (!data) return;
        renderKpis(data);
        renderComboChart(data);
        renderStackedChart(data);
        renderTopCcList(data.topCostCenters || []);
        renderInsights(data.smartInsights || []);
        renderCcTable(data.costCenterTable || []);
        renderBudget(data.budget);
        renderFrequentUsers(data.topMealConsumers || []);
        var ccSel = document.getElementById('dashboard-cost-center');
        if (ccSel) mergeCostCenterOptions(data.costCenterOptions || [], ccSel);
    }

    function ensureFlatpickr() {
        if (window.dashboardFlatpickr) return;
        var el = document.getElementById('dashboard-daterange');
        if (!el || typeof flatpickr === 'undefined') return;



        var t = todayIso();

        var startDefault = addDaysIso(t, -6);



        window.dashboardFlatpickr = flatpickr(el, {

            mode: 'range',

            dateFormat: 'Y-m-d',

            defaultDate: [startDefault, t],

            allowInput: false,

            clickOpens: true

        });

    }



    function loadDashboard() {

        var urls = readUrls();

        var q = buildQueryParams();

        var overviewUrl = apiUrl(urls.overviewUrl || '/Dashboard/GetOverviewJson');



        showDashboardLoader();



        fetch(overviewUrl + '?' + q, { credentials: 'same-origin', headers: { Accept: 'application/json' } })

            .then(function (r) {

                if (!r.ok) throw new Error('overview');

                return r.json();

            })

            .then(renderOverview)

            .catch(function () {

                renderKpis({

                    summary: {},

                    usersWithMealInPeriod: 0,

                    mealsToday: 0,

                    companySubsidyPercent: 0,

                    avgCostPerMeal: null

                });

                renderComboChart({});

                renderStackedChart({});

                renderTopCcList([]);

                renderInsights([]);

                renderCcTable([]);

                renderBudget(null);

                renderFrequentUsers([]);

                var tbody = document.getElementById('dashboard-cc-table-body');

                if (tbody) tbody.innerHTML = '<tr><td colspan="8" class="text-center text-danger py-4">Failed to load dashboard.</td></tr>';

            })

            .finally(function () {

                hideDashboardLoader();

            });

    }



    function exportDashboardPdf() {

        var el = document.getElementById('dashboard-pdf-capture');

        if (!el) {

            window.alert('Dashboard content not found.');

            return;

        }

        if (typeof html2canvas === 'undefined') {

            return;

        }

        var jspdfNs = window.jspdf;

        if (!jspdfNs || !jspdfNs.jsPDF) {

            return;

        }

        var JsPDF = jspdfNs.jsPDF;



        var btn = document.getElementById('dashboard-export-pdf');

        var origHtml = btn ? btn.innerHTML : '';

        if (btn) {

            btn.disabled = true;

            btn.innerHTML = 'Generating…';

        }



        var bounds = getRangeBounds();

        var fileName = 'Hilti-Meal-Dashboard-' + bounds.start + '-' + bounds.end + '.pdf';

        var scrollY = window.scrollY || window.pageYOffset || 0;



        window.scrollTo(0, 0);



        window.setTimeout(function () {

            html2canvas(el, {

                useCORS: true,

                scale: 2,

                allowTaint: false,

                logging: false,

                scrollX: 0,

                scrollY: 0,

                onclone: function (clonedDoc) {

                    var tips = clonedDoc.querySelectorAll('.apexcharts-tooltip, .apexcharts-xcrosshairs, .apexcharts-ycrosshairs');

                    for (var i = 0; i < tips.length; i++) {

                        tips[i].style.display = 'none';

                    }

                }

            }).then(function (canvas) {

                var imgData = canvas.toDataURL('image/png');

                var pdf = new JsPDF({ compress: true });

                var pageWidth = pdf.internal.pageSize.getWidth();

                var pageHeight = pdf.internal.pageSize.getHeight();

                var imgWidth = pageWidth;

                var imgHeight = (canvas.height * imgWidth) / canvas.width;

                var heightLeft = imgHeight;

                var position = 10;



                pdf.setFont('helvetica', 'bold');

                pdf.setFontSize(8);

                pdf.text('Employee Meal Consumption & Cost Overview', 3, 8);

                pdf.addImage(imgData, 'PNG', 3, position, imgWidth - 8, imgHeight - 4);

                heightLeft -= pageHeight;



                while (heightLeft > 0) {

                    position = heightLeft - imgHeight;

                    pdf.addPage();

                    pdf.addImage(imgData, 'PNG', 3, position, imgWidth - 8, imgHeight - 4);

                    heightLeft -= pageHeight;

                }



                pdf.save(fileName);

            })

                .catch(function (err) {

                    console.error(err);

                    window.alert('Could not create PDF. Wait until the dashboard finishes loading, then try again.');

                })

                .finally(function () {

                    window.scrollTo(0, scrollY);

                    if (btn) {

                        btn.disabled = false;

                        btn.innerHTML = origHtml;

                    }

                });

        }, 200);

    }



    document.addEventListener('DOMContentLoaded', function () {

        var rangeEl = document.getElementById('dashboard-range');

        var customWrap = document.getElementById('dashboard-custom-dates');

        var applyBtn = document.getElementById('dashboard-apply-custom');

        var ccEl = document.getElementById('dashboard-cost-center');

        var mtEl = document.getElementById('dashboard-meal-type');



        if (rangeEl) {

            rangeEl.addEventListener('change', function () {

                var v = rangeEl.value;

                if (v === 'custom') {

                    if (customWrap) customWrap.classList.remove('d-none');

                    ensureFlatpickr();

                } else {

                    if (customWrap) customWrap.classList.add('d-none');

                    loadDashboard();

                }

            });

        }



        if (ccEl) ccEl.addEventListener('change', function () { loadDashboard(); });

        if (mtEl) mtEl.addEventListener('change', function () { loadDashboard(); });



        if (applyBtn) {

            applyBtn.addEventListener('click', function () {

                loadDashboard();

            });

        }



        var exportPdfBtn = document.getElementById('dashboard-export-pdf');

        if (exportPdfBtn) {

            exportPdfBtn.addEventListener('click', function (e) {

                e.preventDefault();

                exportDashboardPdf();

            });

        }



        loadDashboard();

    });

})();


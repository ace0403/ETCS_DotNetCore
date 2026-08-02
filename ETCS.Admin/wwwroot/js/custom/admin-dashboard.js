'use strict';

(function () {
    var trendChart = null;
    var typeChart = null;
    var terminalsChart = null;
    var activeRange = 'today';
    var loadRequestId = 0;

    function destroyChartInstance(chartInstance, container) {
        if (chartInstance) {
            try {
                chartInstance.destroy();
            } catch (e) {
                // ignore destroy errors on stale instances
            }
        }
        if (container) {
            container.innerHTML = '';
        }
        return null;
    }

    function readUrls() {
        var el = document.getElementById('dashboard-urls');
        if (!el) return { overviewUrl: 'dashboard/getoverviewjson' };
        try {
            return JSON.parse(el.textContent || '{}');
        } catch (e) {
            return { overviewUrl: 'dashboard/getoverviewjson' };
        }
    }

    function apiUrl(relativePath) {
        var path = relativePath || '';
        if (!path.startsWith('/')) path = '/' + path;
        var base = (typeof SiteUrl !== 'undefined' && SiteUrl) ? String(SiteUrl).replace(/\/?$/, '') : '';
        return base + path;
    }

    function todayIso() {
        return formatDateYmd(new Date());
    }

    function formatDateYmd(d) {
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
        var t = todayIso();
        var start;
        var end = t;

        if (activeRange === 'custom') {
            start = $('#txtDashStartDate').val() || t;
            end = $('#txtDashEndDate').val() || t;
            if (start > end) {
                var tmp = start;
                start = end;
                end = tmp;
            }
            return { start: start, end: end };
        }

        switch (activeRange) {
            case 'today':
                start = t;
                break;
            case 'last7':
                start = addDaysIso(t, -6);
                break;
            case 'last30':
                start = addDaysIso(t, -29);
                break;
            default:
                start = t;
        }
        return { start: start, end: end };
    }

    function setLoading(isLoading) {
        $('#dashboardLoader').toggleClass('d-none', !isLoading);
        $('body').toggleClass('canteen-report-loading', isLoading);
        $('#btnRefreshDashboard').prop('disabled', isLoading);
    }

    function formatMoneyAed(n) {
        if (n == null || isNaN(n)) return '—';
        return 'AED ' + Number(n).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function formatNumber(n) {
        if (n == null || isNaN(n)) return '—';
        return Number(n).toLocaleString();
    }

    function formatDateTime(value) {
        if (!value) return '';
        var d = new Date(value);
        if (isNaN(d.getTime())) return value;
        return d.toLocaleString(undefined, {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    function escapeHtml(value) {
        return $('<div/>').text(value || '').html();
    }

    function renderTrendPill(elementId, trend) {
        var $el = $('#' + elementId);
        if (trend == null || isNaN(trend)) {
            $el.addClass('d-none').text('');
            return;
        }
        var up = Number(trend) >= 0;
        $el.removeClass('d-none dashboard-trend-up dashboard-trend-down')
            .addClass(up ? 'dashboard-trend-up' : 'dashboard-trend-down')
            .text((up ? '▲ ' : '▼ ') + Math.abs(Number(trend)).toFixed(1) + '% vs prior period');
    }

    function calcTrendPercent(current, prior) {
        var c = Number(current) || 0;
        var p = Number(prior) || 0;
        if (p === 0) {
            return c === 0 ? 0 : null;
        }
        return Math.round((c - p) / p * 1000) / 10;
    }

    function renderKpis(summary) {
        summary = summary || {};
        $('#kpi-total-sales').text(formatMoneyAed(summary.TotalSales));
        $('#kpi-txn-count').text(formatNumber(summary.TransactionCount));
        $('#kpi-student-card').text(formatMoneyAed(summary.StudentCardSales));
        $('#kpi-cash-card').text(formatMoneyAed(summary.CashCardSales));

        renderTrendPill('kpi-total-sales-trend', calcTrendPercent(summary.TotalSales, summary.PriorTotalSales));
        renderTrendPill('kpi-txn-count-trend', calcTrendPercent(summary.TransactionCount, summary.PriorTransactionCount));
        renderTrendPill('kpi-student-card-trend', calcTrendPercent(summary.StudentCardSales, summary.PriorStudentCardSales));
        renderTrendPill('kpi-cash-card-trend', calcTrendPercent(summary.CashCardSales, summary.PriorCashCardSales));
    }

    function renderTrendChart(dailySeries) {
        var el = document.querySelector('#dashboard-trend-chart');
        var $empty = $('#dashboard-trend-empty');
        if (!el || typeof ApexCharts === 'undefined') return;

        dailySeries = dailySeries || [];
        if (!dailySeries.length) {
            trendChart = destroyChartInstance(trendChart, el);
            $empty.removeClass('d-none');
            return;
        }
        $empty.addClass('d-none');

        trendChart = destroyChartInstance(trendChart, el);

        var categories = dailySeries.map(function (p) {
            var d = new Date(p.Day);
            return isNaN(d.getTime()) ? p.Day : d.toLocaleDateString(undefined, { day: '2-digit', month: 'short' });
        });
        var sales = dailySeries.map(function (p) { return Number(p.SalesAmount) || 0; });
        var counts = dailySeries.map(function (p) { return Number(p.TransactionCount) || 0; });

        var options = {
            chart: { type: 'line', height: 320, toolbar: { show: false }, zoom: { enabled: false } },
            stroke: { width: [0, 3], curve: 'smooth' },
            plotOptions: { bar: { columnWidth: categories.length > 14 ? '70%' : '55%', borderRadius: 4 } },
            dataLabels: { enabled: false },
            colors: ['#4680FF', '#E58A00'],
            series: [
                { name: 'Sales (AED)', type: 'column', data: sales },
                { name: 'Transactions', type: 'line', data: counts }
            ],
            xaxis: {
                categories: categories,
                labels: { rotate: categories.length > 10 ? -45 : 0 }
            },
            yaxis: [
                {
                    seriesName: 'Sales (AED)',
                    labels: { formatter: function (v) { return Math.round(v); } }
                },
                {
                    seriesName: 'Transactions',
                    opposite: true,
                    labels: { formatter: function (v) { return Math.round(v); } }
                }
            ],
            tooltip: {
                shared: true,
                y: [
                    { formatter: function (v) { return 'AED ' + Number(v).toFixed(2); } },
                    { formatter: function (v) { return v + ' txns'; } }
                ]
            },
            legend: { position: 'top', horizontalAlign: 'right' },
            grid: { strokeDashArray: 4 }
        };

        trendChart = new ApexCharts(el, options);
        trendChart.render();
    }

    function renderTypeChart(typeBreakdown) {
        var el = document.querySelector('#dashboard-type-chart');
        var $empty = $('#dashboard-type-empty');
        if (!el || typeof ApexCharts === 'undefined') return;

        typeBreakdown = typeBreakdown || [];
        var filtered = typeBreakdown.filter(function (x) { return Number(x.Amount) > 0; });
        if (!filtered.length) {
            typeChart = destroyChartInstance(typeChart, el);
            $empty.removeClass('d-none');
            return;
        }
        $empty.addClass('d-none');

        typeChart = destroyChartInstance(typeChart, el);

        var options = {
            chart: { type: 'donut', height: 320 },
            labels: filtered.map(function (x) { return x.Label; }),
            series: filtered.map(function (x) { return Number(x.Amount) || 0; }),
            colors: ['#4680FF', '#E58A00', '#2CA87F', '#FFC107', '#1ABC9C', '#DC2626', '#6366F1'],
            legend: { position: 'bottom' },
            dataLabels: { enabled: false },
            plotOptions: {
                pie: {
                    donut: {
                        size: '68%',
                        labels: {
                            show: true,
                            name: { show: false },
                            value: { show: false },
                            total: {
                                show: true,
                                showAlways: true,
                                label: 'Total',
                                fontSize: '12px',
                                fontWeight: 500,
                                color: '#5b6b79',
                                formatter: function (w) {
                                    var sum = w.globals.seriesTotals.reduce(function (a, b) { return a + b; }, 0);
                                    if (sum >= 1000000) {
                                        return 'AED ' + (sum / 1000000).toLocaleString(undefined, { maximumFractionDigits: 1 }) + 'M';
                                    }
                                    if (sum >= 1000) {
                                        return 'AED ' + (sum / 1000).toLocaleString(undefined, { maximumFractionDigits: 1 }) + 'K';
                                    }
                                    return 'AED ' + sum.toLocaleString(undefined, { maximumFractionDigits: 0 });
                                }
                            }
                        }
                    }
                }
            },
            tooltip: {
                y: { formatter: function (v) { return 'AED ' + Number(v).toFixed(2); } }
            }
        };

        typeChart = new ApexCharts(el, options);
        typeChart.render();
    }

    function renderTerminalsChart(topTerminals) {
        var el = document.querySelector('#dashboard-terminals-chart');
        var $empty = $('#dashboard-terminals-empty');
        if (!el || typeof ApexCharts === 'undefined') return;

        topTerminals = topTerminals || [];
        if (!topTerminals.length) {
            terminalsChart = destroyChartInstance(terminalsChart, el);
            $empty.removeClass('d-none');
            return;
        }
        $empty.addClass('d-none');

        terminalsChart = destroyChartInstance(terminalsChart, el);

        var categories = topTerminals.map(function (t) {
            var name = (t.TerminalName || t.TerminalCode || '').toString();
            return name.length > 28 ? name.substring(0, 25) + '...' : name;
        });
        var amounts = topTerminals.map(function (t) { return Number(t.SalesAmount) || 0; });

        var options = {
            chart: { type: 'bar', height: 280, toolbar: { show: false } },
            plotOptions: { bar: { horizontal: true, borderRadius: 4, barHeight: '60%' } },
            dataLabels: { enabled: false },
            colors: ['#4680FF'],
            series: [{ name: 'Sales (AED)', data: amounts }],
            xaxis: {
                categories: categories,
                labels: { formatter: function (v) { return Math.round(v); } }
            },
            tooltip: {
                y: { formatter: function (v) { return 'AED ' + Number(v).toFixed(2); } }
            },
            grid: { strokeDashArray: 4 }
        };

        terminalsChart = new ApexCharts(el, options);
        terminalsChart.render();
    }

    function renderRecentTable(rows) {
        var $body = $('#dashboard-recent-body');
        rows = rows || [];
        if (!rows.length) {
            $body.html('<tr><td colspan="6" class="text-muted text-center">No recent transactions.</td></tr>');
            return;
        }

        var html = rows.map(function (r) {
            return '<tr>' +
                '<td>' + escapeHtml(formatDateTime(r.Datetime)) + '</td>' +
                '<td>' + escapeHtml(r.StudentCardNo) + '</td>' +
                '<td><span class="canteen-cell-ellipsis" title="' + escapeHtml(r.StudentName) + '">' + escapeHtml(r.StudentName) + '</span></td>' +
                '<td>' + escapeHtml(r.TransactionType) + '</td>' +
                '<td class="text-end">' + escapeHtml(formatMoneyAed(r.Amount)) + '</td>' +
                '<td><span class="canteen-cell-ellipsis" title="' + escapeHtml(r.TerminalName) + '">' + escapeHtml(r.TerminalName) + '</span></td>' +
                '</tr>';
        }).join('');
        $body.html(html);
    }

    function loadDashboard() {
        var urls = readUrls();
        var bounds = getRangeBounds();
        var params = {
            startDate: bounds.start,
            endDate: bounds.end,
            schoolCode: $('#ddlDashSchool').val() || ''
        };
        var requestId = ++loadRequestId;

        setLoading(true);

        $.get(apiUrl(urls.overviewUrl || 'dashboard/getoverviewjson'), params)
            .done(function (data) {
                if (requestId !== loadRequestId) {
                    return;
                }
                if (data.Success === false) {
                    toastMsg(data.Message || 'Unable to load dashboard.', false);
                    return;
                }
                renderKpis(data.Summary);
                renderTrendChart(data.DailySeries);
                renderTypeChart(data.TypeBreakdown);
                renderTerminalsChart(data.TopTerminals);
                renderRecentTable(data.RecentTransactions);
            })
            .fail(function () {
                toastMsg('Failed to load dashboard data.', false);
            })
            .always(function () {
                if (requestId === loadRequestId) {
                    setLoading(false);
                }
            });
    }

    function setActiveRange(range) {
        activeRange = range;
        $('.dashboard-range-group [data-range]').removeClass('active');
        $('.dashboard-range-group [data-range="' + range + '"]').addClass('active');
        $('#dashboardCustomDates').toggleClass('d-none', range !== 'custom');
    }

    $(function () {
        setActiveRange('today');

        $('.dashboard-range-group [data-range]').on('click', function () {
            var range = $(this).data('range');
            setActiveRange(range);
            if (range !== 'custom') {
                loadDashboard();
            }
        });

        $('#btnRefreshDashboard').on('click', loadDashboard);
        $('#ddlDashSchool').on('change', loadDashboard);
        $('#txtDashStartDate, #txtDashEndDate').on('change', function () {
            if (activeRange === 'custom') loadDashboard();
        });

        loadDashboard();
    });
})();

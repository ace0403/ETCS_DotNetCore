var reportTable = null;
var lastSalesSummaryGrandTotals = null;

function escapeHtml(value) {
    return $('<div/>').text(value || '').html();
}

function escapeAttr(value) {
    return escapeHtml(value).replace(/"/g, '&quot;');
}

function truncateText(value, maxLength) {
    var text = (value || '').toString();
    if (text.length <= maxLength) {
        return text;
    }
    return text.substring(0, maxLength - 3) + '...';
}

function renderEllipsisCell(value, maxLength) {
    var text = (value || '').toString();
    if (!text) {
        return '';
    }
    var display = truncateText(text, maxLength);
    return '<span class="canteen-cell-ellipsis" title="' + escapeAttr(text) + '">' + escapeHtml(display) + '</span>';
}

function setTerminalSalesReportLoading(isLoading, message) {
    var $loader = $('#terminalSalesReportLoader');
    var $wrap = $('#terminalSalesReportGridWrap');
    var $viewBtn = $('#btnViewReport');

    if (message) {
        $loader.find('.canteen-report-loader-text').text(message);
    }

    $loader.toggleClass('d-none', !isLoading);
    $wrap.toggleClass('is-loading', isLoading);
    $('body').toggleClass('canteen-report-loading', isLoading);
    $viewBtn.prop('disabled', isLoading);
    $viewBtn.find('.btn-spinner').toggleClass('d-none', !isLoading);
    $('#btnExportReport').prop('disabled', isLoading);
    $('#btnPrintReport').prop('disabled', isLoading);
}

function syncTerminalSelectTitle() {
    var $terminal = $('#ddlTerminal');
    var selected = $terminal.find('option:selected');
    $terminal.attr('title', selected.attr('title') || selected.text() || 'All Terminals');
}

function getTerminalSalesReportFilters() {
    return {
        StartDate: $('#txtStartDate').val(),
        EndDate: $('#txtEndDate').val(),
        SchoolCode: $('#ddlSchool').val() || '',
        TerminalCode: $('#ddlTerminal').val() || '',
        TransactionType: $('#ddlTransactionType').val() || ''
    };
}

function validateTerminalSalesReportFilters(filters) {
    if (!filters.StartDate || !filters.EndDate) {
        toastMsg('Start date and end date are required.', false);
        return false;
    }
    if (filters.StartDate > filters.EndDate) {
        toastMsg('Start date should be less than End date.', false);
        return false;
    }
    return true;
}

function formatReportCurrency(value) {
    var num = Number(value || 0);
    return num.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function parseReportNumber(value) {
    if (typeof value === 'string') {
        value = value.replace(/,/g, '').trim();
    }
    var num = Number(value);
    return Number.isFinite(num) ? num : 0;
}

function sumNumericColumn(api, columnIndex) {
    return api
        .column(columnIndex, { page: 'current' })
        .data()
        .reduce(function (total, value) {
            return total + parseReportNumber(value);
        }, 0);
}

function readRowNumber(row, fieldName) {
    if (!row) {
        return 0;
    }
    var raw = row[fieldName];
    if (raw == null && fieldName) {
        var camel = fieldName.charAt(0).toLowerCase() + fieldName.slice(1);
        raw = row[camel];
    }
    if (typeof raw === 'string') {
        raw = raw.replace(/,/g, '').trim();
    }
    var num = Number(raw);
    return Number.isFinite(num) ? num : 0;
}

function sumRowsField(rows, fieldName) {
    return (rows || []).reduce(function (total, row) {
        return total + readRowNumber(row, fieldName);
    }, 0);
}

function getSalesSummaryFooterTables(api) {
    return $(api.table().container()).find('table.dataTable tfoot, .dt-scroll-body tfoot, .dataTables_scrollBody tfoot');
}

function buildSalesSummaryFooterRow(rowIndex, label) {
    var $tr = $('<tr class="sales-summary-total-row"/>');
    $tr.toggleClass('sales-summary-page-total', rowIndex === 0);
    $tr.toggleClass('sales-summary-grand-total', rowIndex === 1);
    $tr.append(
        $('<th class="text-start" colspan="4"/>').text(label || '')
    );
    for (var i = 0; i < 8; i++) {
        $tr.append($('<th class="text-end"/>'));
    }
    return $tr;
}

function ensureSalesSummaryFooterRows(api) {
    getSalesSummaryFooterTables(api).each(function () {
        var $tfoot = $(this);
        var expectedLabels = ['Page Total', 'Grand Total'];
        expectedLabels.forEach(function (label, rowIndex) {
            var $tr = $tfoot.children('tr').eq(rowIndex);
            var needsRebuild = !$tr.length
                || $tr.children('th').length !== 9
                || Number($tr.children('th').first().attr('colspan') || 1) !== 4;

            if (needsRebuild) {
                var $newRow = buildSalesSummaryFooterRow(rowIndex, label);
                if ($tr.length) {
                    $tr.replaceWith($newRow);
                } else {
                    $tfoot.append($newRow);
                }
            } else {
                $tr.addClass('sales-summary-total-row');
                $tr.toggleClass('sales-summary-page-total', rowIndex === 0);
                $tr.toggleClass('sales-summary-grand-total', rowIndex === 1);
                $tr.children('th').first().attr('colspan', 4).addClass('text-start');
            }
        });

        $tfoot.children('tr').slice(2).remove();
    });
}

/** Map DataTables column index to footer <th> index when cols 0-3 are merged. */
function salesSummaryFooterThIndex(dataColumnIndex) {
    if (dataColumnIndex < 4) {
        return 0;
    }
    return dataColumnIndex - 3;
}

function setSalesSummaryFooterCell(api, rowIndex, dataColumnIndex, html) {
    var thIndex = salesSummaryFooterThIndex(dataColumnIndex);
    getSalesSummaryFooterTables(api).each(function () {
        $(this)
            .children('tr')
            .eq(rowIndex)
            .children('th')
            .eq(thIndex)
            .html(html);
    });
}

function readGrandTotalField(totals, fieldName) {
    if (!totals) {
        return 0;
    }
    return readRowNumber(totals, fieldName);
}

function applySalesSummaryFooterTotals(api, hasRows, grandTotals) {
    ensureSalesSummaryFooterRows(api);
    $(api.table().container()).toggleClass('sales-summary-no-totals', !hasRows);

    setSalesSummaryFooterCell(api, 0, 0, hasRows ? 'Page Total' : '');
    setSalesSummaryFooterCell(api, 1, 0, hasRows ? 'Grand Total' : '');

    setSalesSummaryFooterCell(api, 0, 4, hasRows ? String(sumNumericColumn(api, 4)) : '');
    setSalesSummaryFooterCell(
        api,
        1,
        4,
        hasRows ? String(readGrandTotalField(grandTotals, 'StudentsCount')) : '');

    var moneyFields = [
        'StudentCardPurchase',
        'CashPurchase',
        'CreditCardPurchase',
        'StudentCardManualTopup',
        'StudentCardUndoTopup',
        'OnlineStudentCardTopup',
        'UndoCashPurchase'
    ];

    moneyFields.forEach(function (fieldName, index) {
        var columnIndex = 5 + index;
        setSalesSummaryFooterCell(
            api,
            0,
            columnIndex,
            hasRows ? formatReportCurrency(sumNumericColumn(api, columnIndex)) : '');
        setSalesSummaryFooterCell(
            api,
            1,
            columnIndex,
            hasRows ? formatReportCurrency(readGrandTotalField(grandTotals, fieldName)) : '');
    });
}

function loadTerminalSalesTerminals(schoolCode) {
    var $terminal = $('#ddlTerminal');
    $terminal.prop('disabled', true).html('<option value="">Loading terminals...</option>').attr('title', 'Loading terminals...');

    $.get(SiteUrl + 'report/getcanteenbranches', { schoolCode: schoolCode || '' })
        .done(function (branches) {
            var html = '<option value="" title="All Terminals">All Terminals</option>';
            (branches || []).forEach(function (b) {
                var fullName = (b.Description || '').toString();
                var label = truncateText(fullName, 70);
                html += '<option value="' + escapeAttr(b.TerminalCode) + '" title="' + escapeAttr(fullName) + '">' +
                    escapeHtml(label) + '</option>';
            });
            $terminal.html(html);
            syncTerminalSelectTitle();
        })
        .fail(function () {
            $terminal.html('<option value="" title="All Terminals">All Terminals</option>');
            syncTerminalSelectTitle();
            toastMsg('Unable to load terminals.', false);
        })
        .always(function () {
            $terminal.prop('disabled', false);
        });
}

function bindTerminalSalesReportTableEvents(table) {
    table.on('preXhr.dt', function () {
        setTerminalSalesReportLoading(true, 'Loading report...');
    });

    table.on('draw.dt', function () {
        setTerminalSalesReportLoading(false);
        table.columns.adjust();
        var pageInfo = table.page.info();
        var hasRows = !!(pageInfo && pageInfo.recordsDisplay > 0 && pageInfo.end > pageInfo.start);
        applySalesSummaryFooterTotals(table, hasRows, lastSalesSummaryGrandTotals);
    });

    table.on('error.dt', function () {
        setTerminalSalesReportLoading(false);
    });
}

function initTerminalSalesReportTable() {
    if (reportTable) {
        reportTable.ajax.reload(null, false);
        return;
    }

    var ajaxConfig = adminDataTableAjax('report/getterminalsalessummarylist');
    ajaxConfig.data = function (payload) {
        var filters = getTerminalSalesReportFilters();
        payload.StartDate = filters.StartDate;
        payload.EndDate = filters.EndDate;
        payload.SchoolCode = filters.SchoolCode;
        payload.TerminalCode = filters.TerminalCode;
        payload.TransactionType = filters.TransactionType;
    };

    ajaxConfig.dataFilter = function (raw) {
        var j = JSON.parse(raw);
        if (j.Success === false && j.Message) {
            toastMsg(j.Message, false);
        }
        lastSalesSummaryGrandTotals = j.Totals || j.totals || null;
        var filteredCount = Number(j.RecordsFiltered != null ? j.RecordsFiltered : j.recordsFiltered);
        if (!Number.isFinite(filteredCount) || filteredCount <= 0) {
            $('#reportEmptyMessage').text('No data available..').show();
            lastSalesSummaryGrandTotals = null;
        } else {
            $('#reportEmptyMessage').hide();
        }
        return JSON.stringify({
            draw: j.Draw,
            recordsTotal: j.RecordsTotal,
            recordsFiltered: j.RecordsFiltered,
            data: j.Data || []
        });
    };

    reportTable = $('#grid_table').DataTable({
        processing: false,
        serverSide: true,
        searching: false,
        ordering: false,
        responsive: false,
        scrollX: true,
        pageLength: 50,
        autoWidth: false,
        deferLoading: 0,
        language: {
            emptyTable: 'No data available..'
        },
        layout: {
            topStart: null,
            topEnd: null,
            bottomStart: 'info',
            bottomEnd: 'paging'
        },
        ajax: ajaxConfig,
        columnDefs: [
            { targets: 0, width: '56px', className: 'text-center' },
            { targets: 1, width: '110px' },
            { targets: 2, width: '200px', className: 'canteen-branch-cell' },
            { targets: 3, width: '100px' },
            { targets: 4, width: '110px', className: 'text-end' },
            { targets: [5, 6, 7, 8, 9, 10, 11], className: 'text-end', width: '130px' }
        ],
        columns: [
            {
                data: null,
                orderable: false,
                searchable: false,
                render: function (data, type, row, meta) {
                    return meta.settings._iDisplayStart + meta.row + 1;
                }
            },
            { data: 'TerminalCode', render: function (d) { return renderEllipsisCell(d, 16); } },
            { data: 'TerminalName', render: function (d) { return renderEllipsisCell(d, 42); } },
            { data: 'Date' },
            { data: 'StudentsCount' },
            { data: 'StudentCardPurchase', render: function (d) { return formatReportCurrency(d); } },
            { data: 'CashPurchase', render: function (d) { return formatReportCurrency(d); } },
            { data: 'CreditCardPurchase', render: function (d) { return formatReportCurrency(d); } },
            { data: 'StudentCardManualTopup', render: function (d) { return formatReportCurrency(d); } },
            { data: 'StudentCardUndoTopup', render: function (d) { return formatReportCurrency(d); } },
            { data: 'OnlineStudentCardTopup', render: function (d) { return formatReportCurrency(d); } },
            { data: 'UndoCashPurchase', render: function (d) { return formatReportCurrency(d); } }
        ],
        footerCallback: function () {
            var api = this.api();
            var pageInfo = api.page.info();
            var hasRows = pageInfo && pageInfo.recordsDisplay > 0 && pageInfo.end > pageInfo.start;
            applySalesSummaryFooterTotals(api, hasRows, lastSalesSummaryGrandTotals);
        }
    });

    bindTerminalSalesReportTableEvents(reportTable);
    reportTable.draw();
}

function viewTerminalSalesReport() {
    var filters = getTerminalSalesReportFilters();
    if (!validateTerminalSalesReportFilters(filters)) return;
    setTerminalSalesReportLoading(true, 'Loading report...');
    initTerminalSalesReportTable();
}

function exportTerminalSalesReport() {
    var filters = getTerminalSalesReportFilters();
    if (!validateTerminalSalesReportFilters(filters)) return;

    $('#exportStartDate').val(filters.StartDate);
    $('#exportEndDate').val(filters.EndDate);
    $('#exportSchoolCode').val(filters.SchoolCode);
    $('#exportTerminalCode').val(filters.TerminalCode);
    $('#exportTransactionType').val(filters.TransactionType);
    $('#frmExport').trigger('submit');
}

function getReportPrintFrame() {
    var frame = document.getElementById('reportPrintFrame');
    if (frame) {
        return frame;
    }

    frame = document.createElement('iframe');
    frame.id = 'reportPrintFrame';
    frame.setAttribute('title', 'Sales Summary Print');
    frame.setAttribute('aria-hidden', 'true');
    frame.style.position = 'fixed';
    frame.style.right = '0';
    frame.style.bottom = '0';
    frame.style.width = '0';
    frame.style.height = '0';
    frame.style.border = '0';
    frame.style.visibility = 'hidden';
    document.body.appendChild(frame);
    return frame;
}

function buildTerminalSalesPrintHtml(filters, rows) {
    var schoolLabel = $('#ddlSchool option:selected').text() || 'All Schools';
    var terminalLabel = $('#ddlTerminal option:selected').text() || 'All Terminals';
    var transactionLabel = $('#ddlTransactionType option:selected').text() || 'ALL';

    var html = '<!DOCTYPE html><html><head><title>Sales Summary</title>';
    html += '<style>';
    html += 'body{font-family:Arial,sans-serif;font-size:12px;color:#111;margin:24px;}';
    html += 'h1{font-size:18px;margin:0 0 8px;}';
    html += '.meta{margin:0 0 16px;color:#444;}';
    html += 'table{border-collapse:collapse;width:100%;}';
    html += 'th,td{border:1px solid #ccc;padding:6px 8px;text-align:left;}';
    html += 'th{background:#f3f4f6;}';
    html += '.text-end{text-align:right;}';
    html += '.total-row th{background:#f3f4f6;font-weight:700;}';
    html += '</style></head><body>';
    html += '<h1>Sales Summary</h1>';
    html += '<p class="meta">Date range: ' + escapeHtml(filters.StartDate) + ' to ' + escapeHtml(filters.EndDate) + '<br>';
    html += 'School: ' + escapeHtml(schoolLabel) + '<br>';
    html += 'Terminal: ' + escapeHtml(terminalLabel) + '<br>';
    html += 'Transaction type: ' + escapeHtml(transactionLabel) + '</p>';
    html += '<table><thead><tr>';
    html += '<th>S No</th><th>Terminal Code</th><th>Terminal Name</th><th>Date</th><th>Students Count</th>';
    html += '<th class="text-end">Student-Card Purchase</th><th class="text-end">Cash Purchase</th>';
    html += '<th class="text-end">Credit Card Purchase</th><th class="text-end">Manual Topup</th>';
    html += '<th class="text-end">Undo Topup</th><th class="text-end">Online Topup</th><th class="text-end">Undo Cash</th>';
    html += '</tr></thead><tbody>';

    (rows || []).forEach(function (row, index) {
        html += '<tr>';
        html += '<td>' + (index + 1) + '</td>';
        html += '<td>' + escapeHtml(row.TerminalCode || row.terminalCode) + '</td>';
        html += '<td>' + escapeHtml(row.TerminalName || row.terminalName) + '</td>';
        html += '<td>' + escapeHtml(row.Date || row.date) + '</td>';
        html += '<td class="text-end">' + escapeHtml(readRowNumber(row, 'StudentsCount')) + '</td>';
        html += '<td class="text-end">' + formatReportCurrency(readRowNumber(row, 'StudentCardPurchase')) + '</td>';
        html += '<td class="text-end">' + formatReportCurrency(readRowNumber(row, 'CashPurchase')) + '</td>';
        html += '<td class="text-end">' + formatReportCurrency(readRowNumber(row, 'CreditCardPurchase')) + '</td>';
        html += '<td class="text-end">' + formatReportCurrency(readRowNumber(row, 'StudentCardManualTopup')) + '</td>';
        html += '<td class="text-end">' + formatReportCurrency(readRowNumber(row, 'StudentCardUndoTopup')) + '</td>';
        html += '<td class="text-end">' + formatReportCurrency(readRowNumber(row, 'OnlineStudentCardTopup')) + '</td>';
        html += '<td class="text-end">' + formatReportCurrency(readRowNumber(row, 'UndoCashPurchase')) + '</td>';
        html += '</tr>';
    });

    // Use a final body row (not tfoot) so browsers do not repeat Grand Total on every printed page.
    html += '<tr class="total-row">';
    html += '<th colspan="4">Grand Total</th>';
    html += '<th class="text-end">' + sumRowsField(rows, 'StudentsCount') + '</th>';
    html += '<th class="text-end">' + formatReportCurrency(sumRowsField(rows, 'StudentCardPurchase')) + '</th>';
    html += '<th class="text-end">' + formatReportCurrency(sumRowsField(rows, 'CashPurchase')) + '</th>';
    html += '<th class="text-end">' + formatReportCurrency(sumRowsField(rows, 'CreditCardPurchase')) + '</th>';
    html += '<th class="text-end">' + formatReportCurrency(sumRowsField(rows, 'StudentCardManualTopup')) + '</th>';
    html += '<th class="text-end">' + formatReportCurrency(sumRowsField(rows, 'StudentCardUndoTopup')) + '</th>';
    html += '<th class="text-end">' + formatReportCurrency(sumRowsField(rows, 'OnlineStudentCardTopup')) + '</th>';
    html += '<th class="text-end">' + formatReportCurrency(sumRowsField(rows, 'UndoCashPurchase')) + '</th>';
    html += '</tr></tbody></table></body></html>';
    return html;
}

function printTerminalSalesReport() {
    var filters = getTerminalSalesReportFilters();
    if (!validateTerminalSalesReportFilters(filters)) return;

    setTerminalSalesReportLoading(true, 'Preparing print...');

    $.ajax({
        url: SiteUrl + 'report/getterminalsalessummarylist',
        type: 'POST',
        data: {
            Draw: 1,
            Start: 0,
            Length: 0,
            StartDate: filters.StartDate,
            EndDate: filters.EndDate,
            SchoolCode: filters.SchoolCode,
            TerminalCode: filters.TerminalCode,
            TransactionType: filters.TransactionType
        }
    }).done(function (response) {
        var rows = response.Data || response.data || [];
        if (!rows.length) {
            toastMsg('No data available..', false);
            return;
        }

        var printFrame = getReportPrintFrame();
        var printWindow = printFrame.contentWindow;
        if (!printWindow || !printWindow.document) {
            toastMsg('Unable to prepare the print view.', false);
            return;
        }

        printWindow.document.open();
        printWindow.document.write(buildTerminalSalesPrintHtml(filters, rows));
        printWindow.document.close();

        setTimeout(function () {
            printWindow.focus();
            printWindow.print();
        }, 250);
    }).fail(function () {
        toastMsg('Unable to load report data for printing.', false);
    }).always(function () {
        setTerminalSalesReportLoading(false);
    });
}

$(function () {
    syncTerminalSelectTitle();

    $('#ddlSchool').on('change', function () {
        loadTerminalSalesTerminals($(this).val());
    });

    $('#ddlTerminal').on('change', syncTerminalSelectTitle);

    $('#btnViewReport').on('click', viewTerminalSalesReport);
    $('#btnExportReport').on('click', exportTerminalSalesReport);
    $('#btnPrintReport').on('click', printTerminalSalesReport);
});

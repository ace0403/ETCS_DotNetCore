var reportTable = null;

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
        if (j.RecordsFiltered === 0) {
            $('#reportEmptyMessage').text('No data available..').show();
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
        ]
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

$(function () {
    syncTerminalSelectTitle();

    $('#ddlSchool').on('change', function () {
        loadTerminalSalesTerminals($(this).val());
    });

    $('#ddlTerminal').on('change', syncTerminalSelectTitle);

    $('#btnViewReport').on('click', viewTerminalSalesReport);
    $('#btnExportReport').on('click', exportTerminalSalesReport);
});

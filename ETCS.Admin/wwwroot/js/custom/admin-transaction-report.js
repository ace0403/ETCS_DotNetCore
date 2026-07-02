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

function setAdminTxnReportLoading(isLoading, message) {
    var $loader = $('#adminTxnReportLoader');
    var $wrap = $('#adminTxnReportGridWrap');
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

function getAdminTxnReportFilters() {
    return {
        StartDate: $('#txtStartDate').val(),
        EndDate: $('#txtEndDate').val(),
        SchoolCode: $('#ddlSchool').val() || '',
        TerminalCode: $('#ddlTerminal').val() || '',
        TransactionType: $('#ddlTransactionType').val() || '',
        StudentCardNo: $.trim($('#txtStudCode').val())
    };
}

function validateAdminTxnReportFilters(filters) {
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

function formatReportDate(value) {
    if (!value) return '';
    var d = new Date(value);
    if (isNaN(d.getTime())) return value;
    return d.toLocaleString();
}

function loadAdminTxnTerminals(schoolCode) {
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

function bindAdminTxnReportTableEvents(table) {
    table.on('preXhr.dt', function () {
        setAdminTxnReportLoading(true, 'Loading report...');
    });

    table.on('draw.dt', function () {
        setAdminTxnReportLoading(false);
        table.columns.adjust();
    });

    table.on('error.dt', function () {
        setAdminTxnReportLoading(false);
    });
}

function initAdminTxnReportTable() {
    if (reportTable) {
        reportTable.ajax.reload(null, false);
        return;
    }

    var ajaxConfig = adminDataTableAjax('report/getadmintransactionslist');
    ajaxConfig.data = function (payload) {
        var filters = getAdminTxnReportFilters();
        payload.StartDate = filters.StartDate;
        payload.EndDate = filters.EndDate;
        payload.SchoolCode = filters.SchoolCode;
        payload.TerminalCode = filters.TerminalCode;
        payload.TransactionType = filters.TransactionType;
        payload.StudentCardNo = filters.StudentCardNo;
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
            { targets: 1, width: '170px' },
            { targets: 2, width: '120px' },
            { targets: 3, width: '160px', className: 'canteen-name-cell' },
            { targets: 4, width: '90px' },
            { targets: 5, width: '100px', className: 'text-end' },
            { targets: 6, width: '90px', className: 'text-end' },
            { targets: 7, width: '200px', className: 'canteen-branch-cell' },
            { targets: 8, width: '180px', className: 'canteen-item-cell' },
            { targets: 9, width: '140px' }
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
            { data: 'DateTime', render: function (d) { return formatReportDate(d); } },
            { data: 'StudentId', render: function (d) { return renderEllipsisCell(d, 20); } },
            { data: 'Name', render: function (d) { return renderEllipsisCell(d, 32); } },
            { data: 'Class', render: function (d) { return renderEllipsisCell(d, 16); } },
            { data: 'Amount', render: function (d) { return formatReportCurrency(d); } },
            { data: 'Vat', render: function (d) { return formatReportCurrency(d); } },
            {
                data: 'Terminal',
                render: function (d) { return renderEllipsisCell(d, 42); }
            },
            { data: 'TransactionType', render: function (d) { return renderEllipsisCell(d, 36); } },
            { data: 'TransactionId', render: function (d) { return renderEllipsisCell(d, 24); } }
        ]
    });

    bindAdminTxnReportTableEvents(reportTable);
    reportTable.draw();
}

function viewAdminTxnReport() {
    var filters = getAdminTxnReportFilters();
    if (!validateAdminTxnReportFilters(filters)) return;
    setAdminTxnReportLoading(true, 'Loading report...');
    initAdminTxnReportTable();
}

function exportAdminTxnReport() {
    var filters = getAdminTxnReportFilters();
    if (!validateAdminTxnReportFilters(filters)) return;

    $('#exportStartDate').val(filters.StartDate);
    $('#exportEndDate').val(filters.EndDate);
    $('#exportSchoolCode').val(filters.SchoolCode);
    $('#exportTerminalCode').val(filters.TerminalCode);
    $('#exportTransactionType').val(filters.TransactionType);
    $('#exportStudentCardNo').val(filters.StudentCardNo);
    $('#frmExport').trigger('submit');
}

$(function () {
    syncTerminalSelectTitle();

    $('#ddlSchool').on('change', function () {
        loadAdminTxnTerminals($(this).val());
    });

    $('#ddlTerminal').on('change', syncTerminalSelectTitle);

    $('#btnViewReport').on('click', viewAdminTxnReport);
    $('#btnExportReport').on('click', exportAdminTxnReport);
});

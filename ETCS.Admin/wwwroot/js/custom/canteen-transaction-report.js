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

function setCanteenReportLoading(isLoading, message) {
    var $loader = $('#canteenReportLoader');
    var $wrap = $('#canteenReportGridWrap');
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

function syncBranchSelectTitle() {
    var $branch = $('#ddlBranch');
    var selected = $branch.find('option:selected');
    $branch.attr('title', selected.attr('title') || selected.text() || 'All Branches');
}

function getCanteenReportFilters() {
    return {
        StartDate: $('#txtStartDate').val(),
        EndDate: $('#txtEndDate').val(),
        SchoolCode: $('#ddlSchool').val() || '',
        Branch: $('#ddlBranch').val() || '',
        TransactionType: $('#ddlTransactionType').val() || '',
        StudentCardNo: $.trim($('#txtStudCode').val())
    };
}

function validateCanteenReportFilters(filters) {
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

function loadCanteenBranches(schoolCode) {
    var $branch = $('#ddlBranch');
    $branch.prop('disabled', true).html('<option value="">Loading branches...</option>').attr('title', 'Loading branches...');

    $.get(SiteUrl + 'report/getcanteenbranches', { schoolCode: schoolCode || '' })
        .done(function (branches) {
            var html = '<option value="" title="All Branches">All Branches</option>';
            (branches || []).forEach(function (b) {
                var fullName = (b.Description || '').toString();
                var label = truncateText(fullName, 70);
                html += '<option value="' + escapeAttr(b.TerminalCode) + '" title="' + escapeAttr(fullName) + '">' +
                    escapeHtml(label) + '</option>';
            });
            $branch.html(html);
            syncBranchSelectTitle();
        })
        .fail(function () {
            $branch.html('<option value="" title="All Branches">All Branches</option>');
            syncBranchSelectTitle();
            toastMsg('Unable to load branches.', false);
        })
        .always(function () {
            $branch.prop('disabled', false);
        });
}

function bindCanteenReportTableEvents(table) {
    table.on('preXhr.dt', function () {
        setCanteenReportLoading(true, 'Loading report...');
    });

    table.on('draw.dt', function () {
        setCanteenReportLoading(false);
        table.columns.adjust();
    });

    table.on('error.dt', function () {
        setCanteenReportLoading(false);
    });
}

function initCanteenReportTable() {
    if (reportTable) {
        reportTable.ajax.reload(null, false);
        return;
    }

    var ajaxConfig = adminDataTableAjax('report/getcanteentransactionslist');
    ajaxConfig.data = function (payload) {
        var filters = getCanteenReportFilters();
        payload.StartDate = filters.StartDate;
        payload.EndDate = filters.EndDate;
        payload.SchoolCode = filters.SchoolCode;
        payload.Branch = filters.Branch;
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
            { targets: 2, width: '140px' },
            { targets: 3, width: '160px', className: 'canteen-name-cell' },
            { targets: 4, width: '180px', className: 'canteen-item-cell' },
            { targets: 5, width: '90px', className: 'text-end' },
            { targets: 6, width: '70px', className: 'text-center' },
            { targets: 7, width: '110px', className: 'text-end' },
            { targets: 8, width: '110px', className: 'text-end' },
            { targets: 9, width: '200px', className: 'canteen-branch-cell' }
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
            { data: 'StudCode', render: function (d) { return renderEllipsisCell(d, 24); } },
            { data: 'StudFirstName', render: function (d) { return renderEllipsisCell(d, 32); } },
            { data: 'TransactionType', render: function (d) { return renderEllipsisCell(d, 36); } },
            { data: 'Price', render: function (d) { return d != null ? formatReportCurrency(d) : ''; } },
            { data: 'Quantity' },
            { data: 'Amount', render: function (d) { return formatReportCurrency(d); } },
            { data: 'BalPrepaid', render: function (d) { return d != null ? formatReportCurrency(d) : ''; } },
            {
                data: 'Location',
                render: function (d) { return renderEllipsisCell(d, 42); }
            }
        ]
    });

    bindCanteenReportTableEvents(reportTable);
    reportTable.draw();
}

function viewCanteenReport() {
    var filters = getCanteenReportFilters();
    if (!validateCanteenReportFilters(filters)) return;
    setCanteenReportLoading(true, 'Loading report...');
    initCanteenReportTable();
}

function exportCanteenReport() {
    var filters = getCanteenReportFilters();
    if (!validateCanteenReportFilters(filters)) return;

    $('#exportStartDate').val(filters.StartDate);
    $('#exportEndDate').val(filters.EndDate);
    $('#exportSchoolCode').val(filters.SchoolCode);
    $('#exportBranch').val(filters.Branch);
    $('#exportTransactionType').val(filters.TransactionType);
    $('#exportStudentCardNo').val(filters.StudentCardNo);
    $('#frmExport').trigger('submit');
}

$(function () {
    syncBranchSelectTitle();

    $('#ddlSchool').on('change', function () {
        loadCanteenBranches($(this).val());
    });

    $('#ddlBranch').on('change', syncBranchSelectTitle);

    $('#btnViewReport').on('click', viewCanteenReport);
    $('#btnExportReport').on('click', exportCanteenReport);
});

var reportTable = null;

function formatReportCurrency(value) {
    if (value === null || value === undefined || value === '') {
        return '0.00';
    }
    var num = Number(value);
    if (isNaN(num)) {
        return value;
    }
    return num.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function setStudentConsumptionReportLoading(isLoading, message) {
    var $loader = $('#studentConsumptionReportLoader');
    var $wrap = $('#studentConsumptionReportGridWrap');
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

function parseMonthInput(value) {
    if (!value || value.indexOf('-') < 0) {
        return null;
    }
    var parts = value.split('-');
    var year = parseInt(parts[0], 10);
    var month = parseInt(parts[1], 10);
    if (!year || !month || month < 1 || month > 12) {
        return null;
    }
    return { year: year, month: month };
}

function monthInputToDateRange(fromValue, toValue) {
    var from = parseMonthInput(fromValue);
    var to = parseMonthInput(toValue);
    if (!from || !to) {
        return null;
    }

    var startDate = from.year + '-' + String(from.month).padStart(2, '0') + '-01';
    var lastDay = new Date(to.year, to.month, 0).getDate();
    var endDate = to.year + '-' + String(to.month).padStart(2, '0') + '-' + String(lastDay).padStart(2, '0');

    return { StartDate: startDate, EndDate: endDate };
}

function getStudentConsumptionFilters() {
    var fromMonth = $('#txtStartDate').val() || '';
    var toMonth = $('#txtEndDate').val() || '';
    var range = monthInputToDateRange(fromMonth, toMonth);

    return {
        FromMonth: fromMonth,
        ToMonth: toMonth,
        StartDate: range ? range.StartDate : '',
        EndDate: range ? range.EndDate : '',
        StudentUserId: $('#ddlStudent').val() || ''
    };
}

function validateStudentConsumptionFilters(filters) {
    if (!filters.FromMonth || !filters.ToMonth) {
        toastMsg('From and to month are required.', false);
        return false;
    }
    if (!filters.StartDate || !filters.EndDate) {
        toastMsg('Enter a valid mm/yyyy month.', false);
        return false;
    }
    if (filters.FromMonth > filters.ToMonth) {
        toastMsg('From month should be less than or equal to to month.', false);
        return false;
    }
    if (!filters.StudentUserId) {
        toastMsg('Student is required.', false);
        return false;
    }
    return true;
}

function initStudentConsumptionSelect() {
    var $student = $('#ddlStudent');
    if (!$student.length || typeof $student.select2 !== 'function') {
        return;
    }

    if ($student.hasClass('select2-hidden-accessible')) {
        $student.select2('destroy');
    }

    $student.select2({
        width: '100%',
        minimumInputLength: 1,
        placeholder: '- Select Student -',
        allowClear: true,
        dropdownCssClass: 'student-consumption-select2-dropdown',
        containerCssClass: 'student-consumption-select2-container',
        ajax: {
            url: SiteUrl + 'report/studentconsumptionsearchstudents',
            dataType: 'json',
            delay: 300,
            data: function (params) {
                return { term: params.term || '' };
            },
            processResults: function (data) {
                return { results: data.results || [] };
            },
            transport: function (params, success, failure) {
                var request = $.ajax(params);
                request.then(success);
                request.fail(function (jqXHR, textStatus) {
                    if (textStatus === 'abort') {
                        return;
                    }
                    failure(jqXHR);
                });
                return request;
            },
            cache: true
        }
    });
}

function mergeStudentConsumptionCells() {
    if (!reportTable) {
        return;
    }

    var blocks = [];
    var current = [];

    reportTable.rows({ page: 'current' }).every(function () {
        current.push(this.node());
        var data = this.data();
        if (data && data.RowKind === 2) {
            blocks.push(current);
            current = [];
        }
    });

    if (current.length > 0) {
        blocks.push(current);
    }

    blocks.forEach(function (blockRows) {
        if (blockRows.length < 2) {
            return;
        }

        var $first = $(blockRows[0]);
        var rowspan = blockRows.length;
        var lastData = reportTable.row(blockRows[blockRows.length - 1]).data();
        var hasTotal = lastData && lastData.RowKind === 2;
        var detailRowspan = hasTotal ? blockRows.length - 1 : blockRows.length;

        $first.find('td:eq(0)').attr('rowspan', rowspan).addClass('align-top');
        $first.find('td:eq(1)').attr('rowspan', rowspan).addClass('align-top');

        if (detailRowspan > 1) {
            $first.find('td:eq(2)').attr('rowspan', detailRowspan).addClass('align-top');
        }

        for (var i = 1; i < blockRows.length; i++) {
            var $row = $(blockRows[i]);
            var rowData = reportTable.row(blockRows[i]).data();
            // Remove by original column index (high to low) so indices do not shift mid-loop.
            if (rowData && rowData.RowKind !== 2) {
                $row.find('td:eq(2)').remove();
            }
            $row.find('td:eq(1)').remove();
            $row.find('td:eq(0)').remove();
        }
    });
}

function bindStudentConsumptionReportTable() {
    if (reportTable) {
        reportTable.ajax.reload();
        return;
    }

    var ajaxConfig = adminDataTableAjax('report/getstudentconsumptionlist');
    ajaxConfig.data = function (payload) {
        var filters = getStudentConsumptionFilters();
        payload.StartDate = filters.StartDate;
        payload.EndDate = filters.EndDate;
        payload.StudentUserId = filters.StudentUserId;
    };

    ajaxConfig.dataFilter = function (raw) {
        var j = JSON.parse(raw);
        if (j.Success === false && j.Message) {
            toastMsg(j.Message, false);
        }
        if (!j.RecordsFiltered) {
            $('#reportEmptyMessage').text('No data available..').removeClass('d-none');
        } else {
            $('#reportEmptyMessage').addClass('d-none');
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
        paging: false,
        info: false,
        lengthChange: false,
        autoWidth: false,
        deferLoading: 0,
        language: {
            emptyTable: 'No data available..'
        },
        layout: {
            topStart: null,
            topEnd: null,
            bottomStart: null,
            bottomEnd: null
        },
        ajax: ajaxConfig,
        columns: [
            { data: 'StudentName', defaultContent: '' },
            { data: 'StudCode', defaultContent: '' },
            { data: 'CustomerDetails', defaultContent: '' },
            { data: 'TransDate' },
            { data: 'Debit', className: 'text-end', render: formatReportCurrency },
            { data: 'Credit', className: 'text-end', render: formatReportCurrency },
            { data: 'Amount', className: 'text-end', render: formatReportCurrency }
        ],
        rowCallback: function (row, data) {
            if (data.RowKind === 2) {
                $(row).addClass('fw-bold');
            }
        }
    });

    reportTable.on('preXhr.dt', function () {
        setStudentConsumptionReportLoading(true, 'Loading report...');
    });

    reportTable.on('draw.dt', function () {
        setStudentConsumptionReportLoading(false);
        mergeStudentConsumptionCells();
        reportTable.columns.adjust();
    });

    reportTable.on('error.dt', function () {
        setStudentConsumptionReportLoading(false);
    });
}

function loadStudentConsumptionReport() {
    var filters = getStudentConsumptionFilters();
    if (!validateStudentConsumptionFilters(filters)) {
        return;
    }

    bindStudentConsumptionReportTable();
}

function exportStudentConsumptionReport() {
    var filters = getStudentConsumptionFilters();
    if (!validateStudentConsumptionFilters(filters)) {
        return;
    }

    $('#exportStartDate').val(filters.StartDate);
    $('#exportEndDate').val(filters.EndDate);
    $('#exportStudentUserId').val(filters.StudentUserId);
    $('#frmExport').trigger('submit');
}

$(function () {
    initStudentConsumptionSelect();
    $('#btnViewReport').on('click', loadStudentConsumptionReport);
    $('#btnExportReport').on('click', exportStudentConsumptionReport);
});

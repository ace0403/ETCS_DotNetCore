var reportTable = null;
var studentConsumptionPager = {
    pageIndex: 0,
    pageSize: 10,
    totalStudents: 0
};

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
    $('#btnStudentPagePrev, #btnStudentPageNext, #ddlStudentPageSize').prop('disabled', isLoading);
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

function getSelectedSchoolId() {
    return $('#ddlSchool').val() || '';
}

function isStudentConsumptionSchoolSelected() {
    return !!getSelectedSchoolId();
}

function clearStudentConsumptionSelection() {
    var $student = $('#ddlStudent');
    if (!$student.length) {
        return;
    }

    $student.val(null).trigger('change');
}

function setStudentConsumptionStudentEnabled(isEnabled) {
    var $student = $('#ddlStudent');
    if (!$student.length) {
        return;
    }

    $student.prop('disabled', !isEnabled);
    if ($student.hasClass('select2-hidden-accessible')) {
        $student.trigger('change.select2');
    }
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
        SchoolId: getSelectedSchoolId(),
        StudentUserId: $('#ddlStudent').val() || ''
    };
}

function isStudentConsumptionSingleStudentMode() {
    return !!getStudentConsumptionFilters().StudentUserId;
}

function resetStudentConsumptionPaging() {
    studentConsumptionPager.pageIndex = 0;
    studentConsumptionPager.pageSize = parseInt($('#ddlStudentPageSize').val(), 10) || 10;
    studentConsumptionPager.totalStudents = 0;
    updateStudentConsumptionPager();
}

function getStudentConsumptionPaginationParams() {
    studentConsumptionPager.pageSize = parseInt($('#ddlStudentPageSize').val(), 10) || 10;

    if (isStudentConsumptionSingleStudentMode()) {
        return { start: 0, length: 1 };
    }

    return {
        start: studentConsumptionPager.pageIndex * studentConsumptionPager.pageSize,
        length: studentConsumptionPager.pageSize
    };
}

function applyStudentConsumptionPaginationToPayload(payload) {
    var paging = getStudentConsumptionPaginationParams();
    // DataTables always sends its own start/length (defaults to 0/10) — override both casings for model binding.
    payload.start = paging.start;
    payload.length = paging.length;
    payload.Start = paging.start;
    payload.Length = paging.length;
}

function updateStudentConsumptionPager() {
    var $pager = $('#studentConsumptionPager');
    var total = studentConsumptionPager.totalStudents;
    var pageSize = studentConsumptionPager.pageSize;
    var pageIndex = studentConsumptionPager.pageIndex;
    var singleStudent = isStudentConsumptionSingleStudentMode();

    if (singleStudent || total <= pageSize) {
        $pager.addClass('d-none');
    } else {
        $pager.removeClass('d-none');
    }

    if (total <= 0) {
        $('#studentConsumptionPageInfo').text('');
        $('#btnStudentPagePrev, #btnStudentPageNext').prop('disabled', true);
        return;
    }

    var startStudent = pageIndex * pageSize + 1;
    var endStudent = Math.min((pageIndex + 1) * pageSize, total);
    $('#studentConsumptionPageInfo').text(
        'Showing students ' + startStudent + ' to ' + endStudent + ' of ' + total
    );

    $('#btnStudentPagePrev').prop('disabled', pageIndex <= 0);
    $('#btnStudentPageNext').prop('disabled', endStudent >= total);
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
    if (!filters.SchoolId) {
        toastMsg('School is required.', false);
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

    setStudentConsumptionStudentEnabled(isStudentConsumptionSchoolSelected());

    $student.select2({
        width: '100%',
        minimumInputLength: 1,
        placeholder: 'All Students',
        allowClear: true,
        dropdownCssClass: 'student-consumption-select2-dropdown',
        containerCssClass: 'student-consumption-select2-container',
        ajax: {
            url: SiteUrl + 'report/studentconsumptionsearchstudents',
            dataType: 'json',
            delay: 300,
            data: function (params) {
                return {
                    term: params.term || '',
                    schoolId: getSelectedSchoolId()
                };
            },
            processResults: function (data) {
                return { results: data.results || [] };
            },
            transport: function (params, success, failure) {
                if (!isStudentConsumptionSchoolSelected()) {
                    success({ results: [] });
                    return null;
                }

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

function onStudentConsumptionSchoolChanged() {
    clearStudentConsumptionSelection();
    setStudentConsumptionStudentEnabled(isStudentConsumptionSchoolSelected());
    resetStudentConsumptionPaging();
}

function onStudentConsumptionStudentChanged() {
    resetStudentConsumptionPaging();
}

function mergeStudentConsumptionCells() {
    if (!reportTable) {
        return;
    }

    $('#grid_table td[rowspan]').removeAttr('rowspan').removeClass('align-top');

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
        payload.SchoolId = filters.SchoolId;
        payload.StudentUserId = filters.StudentUserId;
        applyStudentConsumptionPaginationToPayload(payload);
    };

    ajaxConfig.dataFilter = function (raw) {
        var j = JSON.parse(raw);
        if (j.Success === false && j.Message) {
            toastMsg(j.Message, false);
        }

        studentConsumptionPager.totalStudents = j.RecordsFiltered || j.RecordsTotal || 0;
        updateStudentConsumptionPager();

        if (!j.Data || j.Data.length === 0) {
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

    resetStudentConsumptionPaging();
    bindStudentConsumptionReportTable();
}

function goToStudentConsumptionPage(nextPageIndex) {
    if (!reportTable || nextPageIndex < 0) {
        return;
    }

    studentConsumptionPager.pageSize = parseInt($('#ddlStudentPageSize').val(), 10) || 10;
    var maxPageIndex = Math.max(0, Math.ceil(studentConsumptionPager.totalStudents / studentConsumptionPager.pageSize) - 1);
    if (nextPageIndex > maxPageIndex) {
        return;
    }

    studentConsumptionPager.pageIndex = nextPageIndex;
    updateStudentConsumptionPager();
    reportTable.ajax.reload(null, false);
}

function exportStudentConsumptionReport() {
    var filters = getStudentConsumptionFilters();
    if (!validateStudentConsumptionFilters(filters)) {
        return;
    }

    $('#exportStartDate').val(filters.StartDate);
    $('#exportEndDate').val(filters.EndDate);
    $('#exportSchoolId').val(filters.SchoolId);
    $('#exportStudentUserId').val(filters.StudentUserId);
    $('#frmExport').trigger('submit');
}

$(function () {
    initStudentConsumptionSelect();
    $('#ddlSchool').on('change', onStudentConsumptionSchoolChanged);
    $('#ddlStudent').on('change', onStudentConsumptionStudentChanged);
    $('#ddlStudentPageSize').on('change', function () {
        studentConsumptionPager.pageSize = parseInt($(this).val(), 10) || 10;
        studentConsumptionPager.pageIndex = 0;
        if (reportTable) {
            updateStudentConsumptionPager();
            reportTable.ajax.reload(null, false);
        } else {
            updateStudentConsumptionPager();
        }
    });
    $('#btnStudentPagePrev').on('click', function () {
        goToStudentConsumptionPage(studentConsumptionPager.pageIndex - 1);
    });
    $('#btnStudentPageNext').on('click', function () {
        goToStudentConsumptionPage(studentConsumptionPager.pageIndex + 1);
    });
    $('#btnViewReport').on('click', loadStudentConsumptionReport);
    $('#btnExportReport').on('click', exportStudentConsumptionReport);
});

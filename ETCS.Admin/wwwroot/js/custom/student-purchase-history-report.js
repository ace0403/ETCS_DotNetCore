var reportTable = null;

function formatReportCurrency(value) {
    var num = Number(value || 0);
    return num.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function getSelectedSchoolId() {
    return parseInt($('#ddlSchool').val() || '0', 10) || 0;
}

function isSchoolSelected() {
    return getSelectedSchoolId() > 0;
}

function getSelectedStudentUserId() {
    return parseInt($('#ddlStudent').val() || '0', 10) || 0;
}

function clearStudentSelection() {
    var $student = $('#ddlStudent');
    $student.val(null).trigger('change');
    if ($student.find('option[value=""]').length === 0) {
        $student.prepend('<option value="">All Students</option>');
    }
    $student.val('').trigger('change.select2');
}

function setStudentEnabled(isEnabled) {
    var $student = $('#ddlStudent');
    $student.prop('disabled', !isEnabled);
    if (!isEnabled) {
        clearStudentSelection();
    }
}

function getReportFilters() {
    return {
        StartDate: $('#txtStartDate').val() || '',
        EndDate: $('#txtEndDate').val() || '',
        SchoolId: getSelectedSchoolId(),
        StudentUserId: getSelectedStudentUserId()
    };
}

function validateReportFilters(filters) {
    if (!filters.StartDate || !filters.EndDate) {
        toastMsg('Start date and end date are required.', false);
        return false;
    }
    if (filters.StartDate > filters.EndDate) {
        toastMsg('Start date should be less than or equal to end date.', false);
        return false;
    }
    if (!filters.SchoolId) {
        toastMsg('School is required.', false);
        return false;
    }
    return true;
}

function setReportLoading(isLoading, message) {
    var $loader = $('#studentPurchaseHistoryReportLoader');
    var $wrap = $('#studentPurchaseHistoryReportGridWrap');
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

function initStudentSelect() {
    var $student = $('#ddlStudent');
    if (!$student.length || typeof $student.select2 !== 'function') {
        return;
    }

    if ($student.hasClass('select2-hidden-accessible')) {
        $student.select2('destroy');
    }

    setStudentEnabled(isSchoolSelected());

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
                if (!isSchoolSelected()) {
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

function bindReportTableEvents(table) {
    table.on('preXhr.dt', function () {
        setReportLoading(true, 'Loading report...');
    });

    table.on('draw.dt', function () {
        setReportLoading(false);
        table.columns.adjust();
    });

    table.on('error.dt', function () {
        setReportLoading(false);
    });
}

function initReportTable() {
    if (reportTable) {
        reportTable.ajax.reload(null, false);
        return;
    }

    var ajaxConfig = adminDataTableAjax('report/getstudentpurchasehistorylist');
    ajaxConfig.data = function (payload) {
        var filters = getReportFilters();
        payload.StartDate = filters.StartDate;
        payload.EndDate = filters.EndDate;
        payload.SchoolId = filters.SchoolId;
        payload.StudentUserId = filters.StudentUserId;
    };

    ajaxConfig.dataFilter = function (raw) {
        var j = JSON.parse(raw);
        if (j.Success === false && j.Message) {
            toastMsg(j.Message, false);
        }
        if ((j.RecordsFiltered || 0) === 0) {
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
            { targets: [6, 7], className: 'text-end' }
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
            { data: 'StudentName' },
            { data: 'StudentCode' },
            { data: 'Grade' },
            { data: 'Source' },
            { data: 'ItemName', render: function (d) { return formatItemName(d); } },
            { data: 'Quantity' },
            { data: 'Amount', render: function (d) { return formatReportCurrency(d); } },
            { data: 'PurchaseDateTime' },
            { data: 'Reference' }
        ]
    });

    bindReportTableEvents(reportTable);
    reportTable.draw();
}

function viewReport() {
    var filters = getReportFilters();
    if (!validateReportFilters(filters)) {
        return;
    }
    setReportLoading(true, 'Loading report...');
    initReportTable();
}

function exportReport() {
    var filters = getReportFilters();
    if (!validateReportFilters(filters)) {
        return;
    }

    $('#exportStartDate').val(filters.StartDate);
    $('#exportEndDate').val(filters.EndDate);
    $('#exportSchoolId').val(filters.SchoolId);
    $('#exportStudentUserId').val(filters.StudentUserId);
    $('#frmExport').trigger('submit');
}

$(function () {
    initStudentSelect();

    $('#ddlSchool').on('change', function () {
        clearStudentSelection();
        setStudentEnabled(isSchoolSelected());
    });

    $('#btnViewReport').on('click', viewReport);
    $('#btnExportReport').on('click', exportReport);
});

function formatItemName(value) {
    return value.split(',').join('<br>');
}
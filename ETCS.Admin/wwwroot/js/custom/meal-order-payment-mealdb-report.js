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

function formatAmount(value) {
    var amount = parseFloat(value);
    if (isNaN(amount)) {
        return '';
    }
    return amount.toFixed(2);
}

function setMealOrderReportLoading(isLoading, message) {
    var $loader = $('#mealOrderReportLoader');
    var $wrap = $('#mealOrderReportGridWrap');
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

function syncSchoolSelectTitle() {
    var $school = $('#ddlSchool');
    var selected = $school.find('option:selected');
    $school.attr('title', selected.attr('title') || selected.text() || 'All Schools');
}

function getMealOrderReportFilters() {
    return {
        StartDate: $('#txtStartDate').val(),
        EndDate: $('#txtEndDate').val(),
        SchoolId: $('#ddlSchool').val() || '',
        MealSessionId: $('#ddlMealSession').val() || '',
        MealTypeId: $('#ddlMealType').val() || ''
    };
}

function validateMealOrderReportFilters(filters) {
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

function bindMealOrderReportTableEvents(table) {
    table.on('preXhr.dt', function () {
        setMealOrderReportLoading(true, 'Loading report...');
    });

    table.on('draw.dt', function () {
        setMealOrderReportLoading(false);
        table.columns.adjust();
    });

    table.on('error.dt', function () {
        setMealOrderReportLoading(false);
    });
}

function initMealOrderReportTable() {
    if (reportTable) {
        reportTable.ajax.reload(null, false);
        return;
    }

    var ajaxConfig = adminDataTableAjax('report/getmealorderpaymentsmealdblist');
    ajaxConfig.data = function (payload) {
        var filters = getMealOrderReportFilters();
        payload.StartDate = filters.StartDate;
        payload.EndDate = filters.EndDate;
        payload.SchoolId = filters.SchoolId;
        payload.MealSessionId = filters.MealSessionId;
        payload.MealTypeId = filters.MealTypeId;
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
            { targets: 1, width: '100px' },
            { targets: 2, width: '120px' },
            { targets: [3, 4, 10], width: '70px' },
            { targets: 5, width: '110px' },
            { targets: 6, width: '100px' },
            { targets: 7, width: '160px', className: 'canteen-branch-cell' },
            { targets: 8, width: '90px', className: 'text-end' },
            { targets: 9, width: '100px' },
            { targets: 11, width: '220px', className: 'canteen-branch-cell' }
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
            { data: 'OrderDate' },
            { data: 'StudCode', render: function (d) { return renderEllipsisCell(d, 18); } },
            { data: 'StudFullName', render: function (d) { return renderEllipsisCell(d, 36); } },
            { data: 'StudStd' },
            { data: 'PaymentStatus' },
            { data: 'MealSession', render: function (d) { return renderEllipsisCell(d, 20); } },
            { data: 'TransactionId', render: function (d) { return renderEllipsisCell(d, 24); } },
            { data: 'Amount', className: 'text-end', render: function (d) { return formatAmount(d); } },
            { data: 'DeliveryDate' },
            { data: 'Day' },
            { data: 'Items', render: function (d) { return renderEllipsisCell(d, 48); } }
        ]
    });

    bindMealOrderReportTableEvents(reportTable);
    reportTable.draw();
}

function viewMealOrderReport() {
    var filters = getMealOrderReportFilters();
    if (!validateMealOrderReportFilters(filters)) return;
    setMealOrderReportLoading(true, 'Loading report...');
    initMealOrderReportTable();
}

function exportMealOrderReport() {
    var filters = getMealOrderReportFilters();
    if (!validateMealOrderReportFilters(filters)) return;

    $('#exportStartDate').val(filters.StartDate);
    $('#exportEndDate').val(filters.EndDate);
    $('#exportSchoolId').val(filters.SchoolId);
    $('#exportMealSessionId').val(filters.MealSessionId);
    $('#exportMealTypeId').val(filters.MealTypeId);
    $('#frmExport').trigger('submit');
}

function populateMealTypeOptions(types) {
    var $type = $('#ddlMealType');
    $type.empty().append('<option value="">All Meal Types</option>');
    (types || []).forEach(function (item) {
        $type.append('<option value="' + item.id + '">' + escapeHtml(item.name) + '</option>');
    });
}

function loadMealTypesForSession(sessionId) {
    if (!sessionId) {
        populateMealTypeOptions([]);
        $('#ddlMealType').prop('disabled', true);
        return;
    }

    $.get('/report/getmealordermealtypes', { sessionId: sessionId }).done(function (response) {
        populateMealTypeOptions(response.data || []);
        $('#ddlMealType').prop('disabled', false);
    }).fail(function () {
        populateMealTypeOptions([]);
        $('#ddlMealType').prop('disabled', true);
    });
}

$(function () {
    syncSchoolSelectTitle();
    $('#ddlSchool').on('change', syncSchoolSelectTitle);
    $('#ddlMealSession').on('change', function () {
        loadMealTypesForSession($(this).val());
    });
    $('#btnViewReport').on('click', viewMealOrderReport);
    $('#btnExportReport').on('click', exportMealOrderReport);
});

function formatExceptionDate(value) {
    if (!value) return '';
    var date = new Date(value);
    if (isNaN(date.getTime())) return value;
    return date.toLocaleDateString();
}

var myTable = initAdminDataTable('#grid_table', 'schoolcalendar/getexceptionlist', [
    {
        data: 'SchoolId',
        render: function (d, type, row) { return row.SchoolName || d; }
    },
    { data: 'ExceptionDate', render: function (d) { return formatExceptionDate(d); } },
    {
        data: 'DayStatus',
        render: function (d, type, row) { return row.DayStatusLabel || d; }
    },
    { data: 'Title' },
    {
        data: 'Id',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (d) { return adminActionEditDelete(d); }
    }
], { order: [[1, 'desc']], schoolFilterSelector: '#adminGridSchoolFilter' });

(function syncSchoolFilters() {
    var pageSchoolId = $('#schoolCalendarSchoolId').val();
    if (pageSchoolId && $('#adminGridSchoolFilter').length) {
        $('#adminGridSchoolFilter').val(pageSchoolId);
        if (myTable) {
            myTable.ajax.reload();
        }
    }
})();

$('#schoolCalendarSchoolId').on('change', function () {
    var schoolId = $(this).val();
    window.location.href = SiteUrl + 'schoolcalendar/index?schoolId=' + encodeURIComponent(schoolId || '');
});

$('#btnSaveWeeklySchedule').on('click', function () {
    var $form = $('#frmWeeklySchedule');
    $('#weeklySchoolId').val($('#schoolCalendarSchoolId').val() || '0');
    $.post(SiteUrl + 'schoolcalendar/saveweekly', $form.serialize(), function (r) {
        toastMsg(r.Message, r.Success);
    });
});

function loadData(id) {
    $.get(SiteUrl + 'schoolcalendar/getexception?id=' + id, function (h) {
        $('#div_add').html(h);
        var selectedSchoolId = $('#schoolCalendarSchoolId').val();
        if (id === 0 && selectedSchoolId) {
            $('#frmSchoolCalendarException select[name="SchoolId"]').val(selectedSchoolId);
        }
        $('#addDataModal').modal('show');
        bindAdminFormSave('#frmSchoolCalendarException', function ($form) {
            $.post(SiteUrl + 'schoolcalendar/saveexception', $form.serialize(), function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) {
                    myTable.ajax.reload();
                    $('#addDataModal').modal('hide');
                }
            });
        });
    });
}

function deleteData(id) {
    showConfirmation('Delete this holiday?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'schoolcalendar/deleteexception?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) myTable.ajax.reload();
        });
    });
}

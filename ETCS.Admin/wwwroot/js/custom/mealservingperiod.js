function formatServingPeriodDate(value) {
    if (!value) return '';
    var date = new Date(value);
    if (isNaN(date.getTime())) return value;
    return date.toLocaleDateString();
}

var myTable = initAdminDataTable('#grid_table', 'mealservingperiod/getlist', [
    {
        data: 'SchoolId',
        render: function (d, type, row) { return row.SchoolName || d; }
    },
    { data: 'StartDate', render: function (d) { return formatServingPeriodDate(d); } },
    { data: 'CutoffDate', render: function (d) { return formatServingPeriodDate(d); } },
    {
        data: 'Id',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (d) { return adminActionEditDelete(d); }
    }
], { order: [[1, 'desc']], schoolFilterSelector: '#adminGridSchoolFilter' });

function loadData(id) {
    $.get(SiteUrl + 'mealservingperiod/get?id=' + id, function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        bindAdminFormSave('#frmMealServingPeriod', function ($form) {
            $.post(SiteUrl + 'mealservingperiod/save', $form.serialize(), function (r) {
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
    showConfirmation('Delete this serving period?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'mealservingperiod/delete?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) myTable.ajax.reload();
        });
    });
}

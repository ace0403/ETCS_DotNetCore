var myTable = initAdminDataTable('#grid_table', 'student/getlist', [
    { data: 'UserId', visible: false },
    { data: 'StudCode' }, { data: 'Name' }, { data: 'SchoolName' }, { data: 'GuardianName' }, { data: 'Balance' },
    { data: 'CreatedAt', render: function (d) { return formatReportDate(d); } },
    {
        data: 'UserId',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (d) { return adminActionEditDelete(d); }
    }
], { order: [[0, 'desc']], schoolFilterSelector: '#adminGridSchoolFilter' });

function initStudentAllergyMultiselect() {
    initAllergyMultiselect();
}

function loadData(id) {
    $.get(SiteUrl + 'student/get?id=' + id, function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        initStudentAllergyMultiselect();
        initStudentCardNo($('#frmStudent'));
        bindAdminFormSave('#frmStudent', function ($form) {
            $.post(SiteUrl + 'student/save', serializeStudentCardForm($form), function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) { myTable.ajax.reload(); $('#addDataModal').modal('hide'); }
            });
        });
    });
}

function deleteData(id) {
    showConfirmation('Delete this student?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'student/delete?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) myTable.ajax.reload();
        });
    });
}

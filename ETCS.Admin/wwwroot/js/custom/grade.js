var myTable = initAdminDataTable('#grid_table', 'grade/getlist', [
    { data: 'Grade' },
    {
        data: 'Id',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (d) { return adminActionEditDelete(d); }
    }
], { order: [[0, 'asc']] });

function loadData(id) {
    $.get(SiteUrl + 'grade/get?id=' + id, function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        bindAdminFormSave('#frmGrade', function ($form) {
            $.post(SiteUrl + 'grade/save', $form.serialize(), function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) { myTable.ajax.reload(); $('#addDataModal').modal('hide'); }
            });
        });
    });
}

function deleteData(id) {
    showConfirmation('Delete this grade?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'grade/delete?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) myTable.ajax.reload();
        });
    });
}

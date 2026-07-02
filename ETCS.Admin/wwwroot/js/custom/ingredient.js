var myTable = initAdminDataTable('#grid_table', 'ingredient/getlist', [
    { data: 'IngredientName' },
    { data: 'SortOrder' },
    { data: 'IsActive', render: function (d) { return d ? 'Yes' : 'No'; } },
    {
        data: 'Id',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (d) { return adminActionEditDelete(d); }
    }
], { order: [[1, 'asc'], [0, 'asc']] });

function loadData(id) {
    $.get(SiteUrl + 'ingredient/get?id=' + id, function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        bindAdminFormSave('#frmIngredient', function ($form) {
            $.post(SiteUrl + 'ingredient/save', $form.serialize(), function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) { myTable.ajax.reload(); $('#addDataModal').modal('hide'); }
            });
        });
    });
}

function deleteData(id) {
    showConfirmation('Delete this ingredient?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'ingredient/delete?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) myTable.ajax.reload();
        });
    });
}

var myTable = initAdminDataTable('#grid_table', 'category/getlist', [
    { data: 'CategoryName' },
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
    $.get(SiteUrl + 'category/get?id=' + id, function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        bindAdminFormSave('#frmCategory', function ($form) {
            $.post(SiteUrl + 'category/save', $form.serialize(), function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) { myTable.ajax.reload(); $('#addDataModal').modal('hide'); }
            });
        });
    });
}

function deleteData(id) {
    showConfirmation('Delete this category?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'category/delete?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) myTable.ajax.reload();
        });
    });
}

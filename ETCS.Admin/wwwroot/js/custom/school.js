var myTable = initAdminDataTable('#grid_table', 'school/getlist', [
    { data: 'Name' }, { data: 'Code' }, { data: 'CountryName' }, { data: 'MinimumTopupAmount' },
    { data: 'HasEmailNotification', render: function (d) { return d ? 'Yes' : 'No'; } },
    {
        data: 'Id',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (data) { return adminActionEditDelete(data); }
    }
]);

function loadData(id) {
    $.get(SiteUrl + 'school/get?id=' + id, function (html) {
        $('#div_add').html(html);
        $('#addDataModal').modal('show');
        bindSave();
    });
}

function bindSave() {
    bindAdminFormSave('#frmSchool', function ($form) {
        var formData = new FormData($form[0]);
        $.ajax({
            url: SiteUrl + 'school/save',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) { myTable.ajax.reload(); $('#addDataModal').modal('hide'); }
            }
        });
    });
}

function deleteData(id) {
    showConfirmation('Delete this school?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'school/delete?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) myTable.ajax.reload();
        });
    });
}

$(function () { bindSave(); });

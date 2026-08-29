var sessionTable = initAdminDataTable('#session_grid_table', 'mealtype/getlist', [
    { data: 'Name' },
    { data: 'SortOrder' },
    { data: 'IsActive', render: function (d) { return d ? 'Yes' : 'No'; } },
    {
        data: 'Id',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (d) { return adminActionEditDelete(d, 'loadSession', 'deleteSession'); }
    }
], {
    order: [[1, 'asc'], [0, 'asc']],
    searchSelector: '#sessionGridSearch',
    searchClearSelector: '#sessionGridSearchClear',
    extraAjaxData: function (payload) {
        payload.kind = 'session';
    }
});

var typeTable = initAdminDataTable('#type_grid_table', 'mealtype/getlist', [
    { data: 'Name' },
    { data: 'SessionName' },
    { data: 'SortOrder' },
    { data: 'IsActive', render: function (d) { return d ? 'Yes' : 'No'; } },
    {
        data: 'Id',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (d) { return adminActionEditDelete(d, 'loadType', 'deleteType'); }
    }
], {
    order: [[2, 'asc'], [1, 'asc'], [0, 'asc']],
    searchSelector: '#typeGridSearch',
    searchClearSelector: '#typeGridSearchClear',
    extraAjaxData: function (payload) {
        payload.kind = 'type';
        var sessionId = $('#typeSessionFilter').val();
        if (sessionId) {
            payload.sessionId = sessionId;
        }
    }
});

$('#typeSessionFilter').on('change', function () {
    typeTable.draw();
});

$('button[data-bs-toggle="tab"]').on('shown.bs.tab', function () {
    sessionTable.columns.adjust();
    typeTable.columns.adjust();
});

function loadSession(id) {
    $.get(SiteUrl + 'mealtype/get?id=' + id + '&kind=session', function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        bindAdminFormSave('#frmMealSession', function ($form) {
            $.post(SiteUrl + 'mealtype/save', $form.serialize(), function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) {
                    sessionTable.ajax.reload();
                    typeTable.ajax.reload(null, false);
                    $('#addDataModal').modal('hide');
                }
            });
        });
    });
}

function loadType(id) {
    $.get(SiteUrl + 'mealtype/get?id=' + id + '&kind=type', function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        bindAdminFormSave('#frmMealType', function ($form) {
            $.post(SiteUrl + 'mealtype/save', $form.serialize(), function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) {
                    typeTable.ajax.reload();
                    $('#addDataModal').modal('hide');
                }
            });
        });
    });
}

function deleteSession(id) {
    showConfirmation('Delete this meal session?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'mealtype/delete?id=' + id + '&kind=session', function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) {
                sessionTable.ajax.reload();
                typeTable.ajax.reload(null, false);
            }
        });
    });
}

function deleteType(id) {
    showConfirmation('Delete this meal type?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'mealtype/delete?id=' + id + '&kind=type', function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) typeTable.ajax.reload();
        });
    });
}

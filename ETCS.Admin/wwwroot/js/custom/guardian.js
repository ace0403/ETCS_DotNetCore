function actionCol(id) {
    return renderAdminActionMenu([
        { label: 'Edit', icon: 'ti ti-edit', onclick: 'loadData(' + id + '); return false;' },
        { label: 'View children', icon: 'ti ti-users', onclick: 'childrenData(' + id + '); return false;' },
        { label: 'Add student', icon: 'ti ti-user-plus', onclick: 'addStudentData(' + id + '); return false;' },
        { label: 'Transfer balance', icon: 'ti ti-exchange', onclick: 'transferData(' + id + '); return false;' },
        { label: 'Delete', icon: 'ti ti-trash', onclick: 'deleteData(' + id + '); return false;', className: 'text-danger' }
    ]);
}

var myTable = initAdminDataTable('#grid_table', 'guardian/getlist', [
    { data: 'Id', visible: false },
    { data: 'Name' }, { data: 'Email' }, { data: 'MobileNo' }, { data: 'Username' },
    {
        data: 'Id',
        width: '70px',
        className: 'text-center admin-action-cell',
        orderable: false,
        render: function (d) { return actionCol(d); }
    }
], { order: [[0, 'desc']] });

function loadData(id) {
    $.get(SiteUrl + 'guardian/get?id=' + id, function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        bindSave('guardian', 'frmGuardian');
    });
}

function deleteData(id) {
    showConfirmation('Delete this parent?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'guardian/delete?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) myTable.ajax.reload();
        });
    });
}

function bindSave(ctrl, frm) {
    bindAdminFormSave('#' + frm, function ($form) {
        $.post(SiteUrl + ctrl + '/save', $form.serialize(), function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) { myTable.ajax.reload(); $('#addDataModal').modal('hide'); }
        });
    });
}

function updateFromBalance() {
    var balance = $('#ddlFromStudent option:selected').data('balance');
    $('#lblFromBalance').text(balance !== undefined && balance !== '' ? balance : '0.00');
}

function syncToStudentOptions() {
    var fromId = $('#ddlFromStudent').val();
    $('#ddlToStudent option').each(function () {
        var opt = $(this);
        if (!opt.val()) return;
        opt.prop('disabled', opt.val() === fromId);
    });
    if ($('#ddlToStudent').val() === fromId) {
        $('#ddlToStudent').val('');
    }
}

function childrenData(guardianId) {
    $.get(SiteUrl + 'guardian/getchildrenview?id=' + guardianId, function (html) {
        $('#div_children').html(html);
        $('#childrenModal').modal('show');
    });
}

function editChildData(guardianId, userId) {
    $.get(SiteUrl + 'guardian/geteditstudentview?guardianId=' + guardianId + '&userId=' + userId, function (html) {
        $('#div_child_edit').html(html);
        $('#childEditModal').modal('show');
        initAllergyMultiselect();
        initStudentCardNo($('#frmEditStudent'));
        bindAdminFormSave('#frmEditStudent', function ($form) {
            $.post(SiteUrl + 'guardian/editstudent', serializeStudentCardForm($form), function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) {
                    $('#childEditModal').modal('hide');
                    childrenData(guardianId);
                    myTable.ajax.reload();
                }
            });
        });
    });
}

function deleteChildData(guardianId, userId) {
    showConfirmation('Delete this student?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'student/delete?id=' + userId, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) {
                childrenData(guardianId);
                myTable.ajax.reload();
            }
        });
    });
}

function addStudentData(guardianId) {
    $.get(SiteUrl + 'guardian/getaddstudentview?id=' + guardianId, function (html) {
        $('#div_add_student').html(html);
        $('#studentModal').modal('show');
        initAllergyMultiselect();
        initStudentCardNo($('#frmAddStudent'));
        bindAdminFormSave('#frmAddStudent', function ($form) {
            var firstName = $form.find('[name="FirstName"]').val();
            var cardNo = getStudentCardDisplayValue($form);
            showConfirmation('Add student ' + firstName + ' (' + cardNo + ')?', 'Add Student').then(function (result) {
                if (!result.isConfirmed) return;
                $.post(SiteUrl + 'guardian/addstudent', serializeStudentCardForm($form), function (r) {
                    toastMsg(r.Message, r.Success);
                    if (r.Success) {
                        $('#studentModal').modal('hide');
                        myTable.ajax.reload();
                    }
                });
            });
        });
    });
}

function transferData(guardianId) {
    $.get(SiteUrl + 'guardian/gettransferview?id=' + guardianId, function (html) {
        $('#div_transfer').html(html);
        $('#transferModal').modal('show');

        $('#ddlFromStudent').on('change', function () {
            updateFromBalance();
            syncToStudentOptions();
        });

        bindAdminFormSave('#frmTransfer', function ($form) {
            var amount = parseFloat($form.find('[name="Amount"]').val());
            showConfirmation('Transfer ' + amount.toFixed(2) + ' to the selected student?', 'Transfer').then(function (result) {
                if (!result.isConfirmed) return;
                $.post(SiteUrl + 'guardian/transfer', $form.serialize(), function (r) {
                    toastMsg(r.Message, r.Success);
                    if (r.Success) {
                        $('#transferModal').modal('hide');
                        myTable.ajax.reload();
                    }
                });
            });
        });
    });
}

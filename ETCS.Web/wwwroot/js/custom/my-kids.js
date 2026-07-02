function refreshKidsList() {
    $.get(SiteUrl + 'mykids/getlist', function (html) {
        $('#kidsListContainer').html(html);
    });
}

function openAddKid() {
    $.get(SiteUrl + 'mykids/getaddview', function (html) {
        $('#div_add_kid').html(html);
        $('#addKidModal').modal('show');
        initAllergyMultiselect();
        bindAdminFormSave('#frmAddStudent', function ($form) {
            var firstName = $form.find('[name="FirstName"]').val();
            var cardNo = $form.find('[name="StudentCardNo"]').val();
            showConfirmation('Add child ' + firstName + ' (' + cardNo + ')?', 'Add Child').then(function (result) {
                if (!result.isConfirmed) return;
                $.post(SiteUrl + 'mykids/addstudent', $form.serialize(), function (r) {
                    toastMsg(r.Message, r.Success);
                    if (r.Success) {
                        $('#addKidModal').modal('hide');
                        refreshKidsList();
                    }
                });
            });
        });
    });
}

function openEditKid(userId) {
    $.get(SiteUrl + 'mykids/geteditview?userId=' + userId, function (html) {
        $('#div_edit_kid').html(html);
        $('#editKidModal').modal('show');
        initAllergyMultiselect();
        bindAdminFormSave('#frmEditStudent', function ($form) {
            $.post(SiteUrl + 'mykids/editstudent', $form.serialize(), function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) {
                    $('#editKidModal').modal('hide');
                    refreshKidsList();
                }
            });
        });
    });
}

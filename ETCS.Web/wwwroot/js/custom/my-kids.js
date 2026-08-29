function refreshKidsList() {
    $.get(SiteUrl + 'mykids/getlist', function (html) {
        $('#kidsListContainer').html(html);
        var count = $('#kidsListContainer .my-kids-card').length;
        var subtitle = count === 1 ? '1 child registered' : (count + ' children registered');
        $('.etcs-topbar-subtitle').text(subtitle);
    });
}

function readAdminResult(r) {
    return {
        success: !!(r && (r.Success === true || r.success === true)),
        message: (r && (r.Message || r.message)) || ''
    };
}

function prepareKidModal($modal, contentSelector) {
    $modal
        .off('shown.bs.modal.allergyInit')
        .one('shown.bs.modal.allergyInit', function () {
            initAllergyMultiselect(contentSelector);
        })
        .off('hidden.bs.modal.clearKidForm')
        .one('hidden.bs.modal.clearKidForm', function () {
            $(contentSelector).empty();
        });
}

function openAddKid() {
    $.get(SiteUrl + 'mykids/getaddview', function (html) {
        $('#div_add_kid').html(html);
        var $modal = $('#addKidModal');
        prepareKidModal($modal, '#div_add_kid');
        $modal.modal('show');
        initAllergyMultiselect('#div_add_kid');
        initStudentCardNo($('#frmAddStudent'));
        bindAdminFormSave('#frmAddStudent', function ($form) {
            var firstName = $form.find('[name="FirstName"]').val();
            var cardNo = getStudentCardDisplayValue($form);
            showConfirmation('Add child ' + firstName + ' (' + cardNo + ')?', 'Add Child').then(function (result) {
                if (!result.isConfirmed) return;
                $.post(SiteUrl + 'mykids/addstudent', serializeStudentCardForm($form), function (r) {
                    var result = readAdminResult(r);
                    if (result.success) {
                        $('#addKidModal').one('hidden.bs.modal', function () {
                            toastMsg(result.message || 'Child added successfully.', true);
                            refreshKidsList();
                        }).modal('hide');
                        return;
                    }
                    toastMsg(result.message || 'Unable to add child.', false);
                });
            });
        });
    });
}

function openEditKid(userId) {
    $.get(SiteUrl + 'mykids/geteditview?userId=' + userId, function (html) {
        $('#div_edit_kid').html(html);
        var $modal = $('#editKidModal');
        prepareKidModal($modal, '#div_edit_kid');
        $modal.modal('show');
        initAllergyMultiselect('#div_edit_kid');
        initStudentCardNo($('#frmEditStudent'));
        bindAdminFormSave('#frmEditStudent', function ($form) {
            $.post(SiteUrl + 'mykids/editstudent', serializeStudentCardForm($form), function (r) {
                var result = readAdminResult(r);
                if (result.success) {
                    // Show toast after close so it isn't trapped under the modal backdrop.
                    $('#editKidModal').one('hidden.bs.modal', function () {
                        toastMsg(result.message || 'Child updated successfully.', true);
                        refreshKidsList();
                    }).modal('hide');
                    return;
                }
                toastMsg(result.message || 'Unable to update child.', false);
            });
        });
    });
}

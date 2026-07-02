function toastMsg(msg, isSuccess = true, title = undefined, timeOut = 5000) {
    if (title == undefined) { title = isSuccess ? "Success!" : "Error!"; }
    toastr.options = {
        "closeButton": true,
        "debug": false,
        "newestOnTop": true,
        "progressBar": true,
        "positionClass": "toast-bottom-right",
        "preventDuplicates": true,
        "onclick": null,
        "showDuration": 300,
        "hideDuration": 100,
        "timeOut": timeOut,
        "extendedTimeOut": 1000,
        "showEasing": "swing",
        "hideEasing": "linear",
        "showMethod": "fadeIn",
        "hideMethod": "fadeOut"
    }
    if (isSuccess)
        toastr.success(msg, title);
    else
        toastr.error(msg, title);
}

function removeURLParameter(url, parameter) {
    var urlparts = url.split('?');
    if (urlparts.length >= 2) {
        var prefix = encodeURIComponent(parameter) + '=';
        var pars = urlparts[1].split(/[&;]/g);
        for (var i = pars.length; i-- > 0;) {
            if (pars[i].lastIndexOf(prefix, 0) !== -1) {
                pars.splice(i, 1);
            }
        }

        url = urlparts[0] + (pars.length == 0 ? '' : '?') + pars.join('&');
        return url;
    }
    else {
        return url;
    }
}

async function showConfirmation(message, buttonText) {
    var result = await Swal.fire({
        title: "Are you sure?",
        text: message,
        icon: "warning",
        showCancelButton: true,
        confirmButtonText: buttonText,
        returnFocus: false,
        didOpen: function () {
            $(document).off('focusin.bs.modal');
        }
    });

    return {
        isConfirmed: result.isConfirmed === true || (typeof result.value !== 'undefined' && !result.dismiss),
        value: result.value,
        dismiss: result.dismiss
    };
}

function getQueryString(n) {
    for (urlStr = window.location.search.substring(1), sv = urlStr.split("&"), i = 0; i < sv.length; i++) {
        if (ft = sv[i].split("="), ft[0] == n) return ft[1]
    }
}

function randomInteger(min, max) {
    return Math.floor(Math.random() * (max - min + 1)) + min;
}

function generatePassword(fieldId) {
    var length = randomInteger(8, 12),
        charset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789",
        retVal = "";

    for (var i = 0, n = charset.length; i < length; ++i) {
        retVal += charset.charAt(Math.floor(Math.random() * n));
    }

    $('#' + fieldId).val(retVal);
}

function resetFrom(formId) {
    $(`#${formId} input`).each(function () {
        $(this).val('');
    })
    $(`#${formId} select`).each(function () {
        $(this).val('');
    });
}

function initMultiSelect(id) {
    $('#' + id).multiselect({
        templates: {
            button: '<button type="button" class="multiselect" data-bs-toggle="dropdown" aria-expanded="false"><span class="multiselect-selected-text"></span></button>',
        },
    });

    var validator = $("form").data("validator");
    if (validator) {
        validator.settings.ignore = ':hidden:not(select), .ignore-validation';
    }
}

function initAllergyMultiselect() {
    var $el = $('#AllergyItemIds');
    if (!$el.length) return;
    if ($el.data('multiselect')) {
        $el.multiselect('destroy');
    }
    initMultiSelect('AllergyItemIds');
}

function initAdminFormValidation(formSelector) {
    var $form = $(formSelector);
    if (!$form.length) return $form;

    $form.removeData('validator').removeData('unobtrusiveValidation');
    $.validator.unobtrusive.parse($form);

    var validator = $form.data('validator');
    if (validator) {
        validator.settings.ignore = ':hidden:not(select), .ignore-validation';
    }

    return $form;
}

function validateAdminForm(formSelector) {
    var $form = $(formSelector);
    if (!$form.length) return true;

    var validator = $form.data('validator');
    if (validator) {
        return $form.valid();
    }

    if (typeof $form[0].reportValidity === 'function') {
        return $form[0].reportValidity();
    }

    return true;
}

function runAdminFormCustomRules(formSelector) {
    var $form = $(formSelector);
    if (!$form.length) return true;

    if ($form.is('#frmGuardian')) {
        var guardianId = $form.find('[name="Id"]').val();
        if ((!guardianId || guardianId === '0') && !$.trim($form.find('[name="Password"]').val())) {
            toastMsg('Password is required when adding a parent.', false);
            return false;
        }
    }

    if ($form.is('#frmStaff')) {
        var isNew = $form.find('[name="IsNew"]').val();
        if ((isNew === 'True' || isNew === 'true') && !$.trim($form.find('[name="Password"]').val())) {
            toastMsg('Password is required when adding staff.', false);
            return false;
        }
    }

    if ($form.is('#frmTransfer')) {
        var fromId = $form.find('[name="FromUserId"]').val();
        var toId = $form.find('[name="ToUserId"]').val();
        if (fromId && toId && fromId === toId) {
            toastMsg('From and to student must be different.', false);
            return false;
        }
    }

    if ($form.is('#frmMealCombo')) {
        var selectedItems = $form.find('#MealItemIds option:selected').length;
        if (selectedItems === 0) {
            toastMsg('Select at least one meal item.', false);
            return false;
        }
    }

    return true;
}

function bindAdminFormSave(formSelector, submitCallback) {
    var $form = initAdminFormValidation(formSelector);

    $form.find('#btnSave, #btnAddStudentSave, #btnEditStudentSave, #btnTransferSave, #btnUpdatePassword').off('click.adminSave').on('click.adminSave', function (e) {
        e.preventDefault();
        if (!validateAdminForm(formSelector)) return;
        if (!runAdminFormCustomRules(formSelector)) return;
        submitCallback($form);
    });

    $form.off('submit.adminSave').on('submit.adminSave', function (e) {
        e.preventDefault();
        if (!validateAdminForm(formSelector)) return;
        if (!runAdminFormCustomRules(formSelector)) return;
        submitCallback($form);
    });

    return $form;
}
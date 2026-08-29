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

async function showConfirmation(message, buttonText, options) {
    options = options || {};
    var result = await fireStyledSwal({
        title: options.title || 'Are you sure?',
        text: message,
        icon: options.icon || 'warning',
        showCancelButton: true,
        confirmButtonText: buttonText || 'Yes',
        cancelButtonText: options.cancelButtonText || 'Cancel',
        variant: 'simple'
    });

    return {
        isConfirmed: isSwalConfirmed(result),
        value: result.value,
        dismiss: result.dismiss
    };
}

/** SweetAlert2 v8/v9 compatibility — bundled v9.10.x uses `value`, not `isConfirmed`. */
function isSwalConfirmed(result) {
    if (!result) {
        return false;
    }

    if (result.isConfirmed === true) {
        return true;
    }

    return typeof result.value !== 'undefined' && !result.dismiss;
}

/** Shared options so Bootstrap modals can open after a SweetAlert closes. */
function getSwalBootstrapSafeOptions() {
    return {
        returnFocus: false,
        didOpen: function () {
            $(document).off('focusin.bs.modal');
        }
    };
}

function getSwalCustomClass(variant) {
    var popupClass = 'etcs-swal';
    if (variant === 'allergen') {
        popupClass += ' etcs-swal-allergen';
    } else if (variant === 'alert') {
        popupClass += ' etcs-swal-alert';
    } else {
        popupClass += ' etcs-swal-simple';
    }

    return {
        popup: popupClass,
        title: 'etcs-swal-title',
        content: 'etcs-swal-content',
        confirmButton: 'etcs-swal-confirm',
        cancelButton: 'etcs-swal-cancel',
        actions: 'etcs-swal-actions',
        icon: 'etcs-swal-icon'
    };
}

/**
 * Fire a themed SweetAlert.
 * Pass variant: 'simple' | 'allergen' | 'alert'
 */
function fireStyledSwal(options) {
    options = options || {};
    var variant = options.variant || 'simple';
    var merged = $.extend(true, {}, getSwalBootstrapSafeOptions(), {
        icon: 'warning',
        focusConfirm: false,
        buttonsStyling: false,
        reverseButtons: false,
        customClass: getSwalCustomClass(variant)
    }, options);

    delete merged.variant;
    merged.customClass = $.extend({}, getSwalCustomClass(variant), options.customClass || {});
    if (options.customClass && options.customClass.popup) {
        merged.customClass.popup = options.customClass.popup;
    } else {
        merged.customClass.popup = getSwalCustomClass(variant).popup;
    }

    return Swal.fire(merged);
}

/** One-button themed alert (validation / info). */
async function showStyledAlert(title, options) {
    options = options || {};
    return fireStyledSwal({
        title: title,
        text: options.text,
        html: options.html,
        icon: options.icon || 'warning',
        showCancelButton: false,
        confirmButtonText: options.confirmButtonText || 'OK',
        variant: 'alert'
    });
}

function escapeHtml(value) {
    return $('<div>').text(value == null ? '' : String(value)).html();
}

/**
 * Allergen consent dialog for A La Carte / Meal Plans.
 * @param {string} childName
 * @param {string[]} allergenNames
 * @param {'meal'|'combo'} [itemKind='meal']
 * @returns {Promise<{isConfirmed:boolean}>}
 */
async function showAllergenConsent(childName, allergenNames, itemKind) {
    var kind = itemKind === 'combo' ? 'combo' : 'meal';
    var safeName = escapeHtml((childName || 'your child').trim() || 'your child');
    var uniqueAllergens = [];
    (allergenNames || []).forEach(function (name) {
        var trimmed = (name || '').trim();
        if (!trimmed) return;
        var exists = uniqueAllergens.some(function (x) {
            return x.toLowerCase() === trimmed.toLowerCase();
        });
        if (!exists) uniqueAllergens.push(trimmed);
    });

    var chipsHtml = uniqueAllergens.length
        ? uniqueAllergens.map(function (name) {
            return '<span class="etcs-allergen-chip">' + escapeHtml(name) + '</span>';
        }).join('')
        : '<span class="etcs-allergen-chip">Listed allergens</span>';

    var html = ''
        + '<div class="etcs-allergen-dialog">'
        +   '<p class="etcs-allergen-lead">This ' + kind + ' includes items that <strong>' + safeName + '</strong> is sensitive to.</p>'
        +   '<div class="etcs-allergen-section">'
        +     '<div class="etcs-allergen-section-label">Allergens</div>'
        +     '<div class="etcs-allergen-chips">' + chipsHtml + '</div>'
        +   '</div>'
        +   '<p class="etcs-allergen-consent">I consent to <strong>' + safeName + '</strong> receiving this ' + kind
        +   ' despite the allergens it contains.</p>'
        + '</div>';

    var result = await fireStyledSwal({
        title: 'Allergen warning',
        html: html,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Agree to Serve',
        cancelButtonText: 'Don\'t Add',
        variant: 'allergen'
    });

    return { isConfirmed: isSwalConfirmed(result) };
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

function initMultiSelect(idOrElement) {
    var $el = typeof idOrElement === 'string' ? $('#' + idOrElement) : $(idOrElement);
    if (!$el.length || typeof $.fn.multiselect !== 'function') {
        return;
    }

    $el.multiselect({
        templates: {
            button: '<button type="button" class="multiselect" data-bs-toggle="dropdown" aria-expanded="false"><span class="multiselect-selected-text"></span></button>',
        },
    });

    var validator = $el.closest('form').data('validator');
    if (validator) {
        validator.settings.ignore = ':hidden:not(select), .ignore-validation';
    }
}

function initNamedMultiselect(scope, fieldName, fallbackId) {
    var $scope = scope ? $(scope) : $(document);
    var $selects = $scope.find('select[name="' + fieldName + '"]');
    if (!$selects.length && !scope) {
        $selects = $('#' + fallbackId);
    }

    $selects.each(function () {
        var $el = $(this);
        if ($el.data('multiselect')) {
            try {
                $el.multiselect('destroy');
            } catch (e) {
                // Ignore stale plugin state after modal HTML swaps.
            }
            $el.removeData('multiselect');
        }

        // Drop leftover widgets from a previous failed init.
        $el.siblings('.btn-group').has('.multiselect').remove();
        initMultiSelect($el);
    });
}

function initAllergyMultiselect(scope) {
    initNamedMultiselect(scope, 'AllergyItemIds', 'AllergyItemIds');
    initNamedMultiselect(scope, 'OrderTypeIds', 'OrderTypeIds');
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

function studentCardDigitsOnly(value) {
    return (value || '').replace(/\D/g, '');
}

function isNumericSchoolCode(code) {
    return /^[0-9]+$/.test(code || '');
}

function getStudentCardInput($form) {
    return $form.find('.js-student-card-input').first();
}

function getStudentCardPrefix($form) {
    return $.trim($form.find('.js-student-card-prefix').text());
}

function syncStudentCardPrefix($form) {
    var $prefix = $form.find('.js-student-card-prefix');
    var $suffix = $form.find('.js-student-card-suffix');
    if (!$prefix.length || !$suffix.length) {
        return;
    }

    var code = ($form.find('.js-student-card-school option:selected').attr('data-school-code') || '').trim();
    if (isNumericSchoolCode(code)) {
        $prefix.text(code);
        $suffix.prop('disabled', false);
        $suffix.attr('maxlength', Math.max(1, 50 - code.length));
    } else {
        $prefix.text('—');
        $suffix.prop('disabled', true).val('');
        $suffix.attr('maxlength', 50);
    }
}

function initStudentCardNo($form) {
    if (!$form || !$form.length) {
        return;
    }

    $form.find('.js-student-card-input').off('.studentCard').on('input.studentCard', function () {
        var $input = $(this);
        var digits = studentCardDigitsOnly($input.val());
        if ($input.val() !== digits) {
            $input.val(digits);
        }
    }).on('paste.studentCard', function (e) {
        e.preventDefault();
        var clipboard = (e.originalEvent && e.originalEvent.clipboardData)
            ? e.originalEvent.clipboardData.getData('text')
            : '';
        $(this).val(studentCardDigitsOnly(clipboard)).trigger('input');
    }).on('keypress.studentCard', function (e) {
        if (!e.which) {
            return;
        }
        var ch = String.fromCharCode(e.which);
        if (!/[0-9]/.test(ch)) {
            e.preventDefault();
        }
    });

    $form.find('.js-student-card-school').off('change.studentCard').on('change.studentCard', function () {
        syncStudentCardPrefix($form);
    });
    syncStudentCardPrefix($form);
}

function validateStudentCardNo($form) {
    var $input = getStudentCardInput($form);
    if (!$input.length) {
        return true;
    }

    if ($input.hasClass('js-student-card-suffix')) {
        var prefix = getStudentCardPrefix($form);
        if (!isNumericSchoolCode(prefix)) {
            toastMsg('Selected school does not have a numeric school code.', false);
            return false;
        }
        var suffix = studentCardDigitsOnly($input.val());
        if (!suffix) {
            toastMsg('Student card number is required.', false);
            return false;
        }
        if (prefix.length + suffix.length > 50) {
            toastMsg('Student card number is too long.', false);
            return false;
        }
        return true;
    }

    var card = studentCardDigitsOnly($input.val());
    if (!card) {
        toastMsg('Student card number is required.', false);
        return false;
    }
    $input.val(card);
    return true;
}

function getStudentCardDisplayValue($form) {
    var $input = getStudentCardInput($form);
    if (!$input.length) {
        return '';
    }
    if ($input.hasClass('js-student-card-suffix')) {
        return getStudentCardPrefix($form) + studentCardDigitsOnly($input.val());
    }
    return studentCardDigitsOnly($input.val());
}

function serializeStudentCardForm($form) {
    var $suffix = $form.find('.js-student-card-suffix');
    if (!$suffix.length) {
        var $input = getStudentCardInput($form);
        if ($input.length) {
            $input.val(studentCardDigitsOnly($input.val()));
        }
        return $form.serialize();
    }

    var suffix = studentCardDigitsOnly($suffix.val());
    var prefix = getStudentCardPrefix($form);
    var wasDisabled = $suffix.prop('disabled');
    $suffix.prop('disabled', false);
    $suffix.val(prefix + suffix);
    var data = $form.serialize();
    $suffix.val(suffix);
    $suffix.prop('disabled', wasDisabled);
    return data;
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

    if ($form.is('#frmStudent, #frmAddStudent, #frmEditStudent')) {
        if (!validateStudentCardNo($form)) {
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

function getMenuCutoffHour() {
    var raw = $('#date-container').attr('data-cutoff-hour');
    var hour = parseInt(raw, 10);
    if (isNaN(hour) || hour < 0 || hour > 23) {
        return 15;
    }
    return hour;
}

function getEarliestMenuDate() {
    var now = dayjs();
    var start = now.startOf('day').add(1, 'day');
    if (now.hour() >= getMenuCutoffHour()) {
        start = start.add(1, 'day');
    }
    return start;
}

function fetchSchoolCalendarDays(studentId, startDate, endDate) {
    var empty = { holidays: {}, halfDays: {}, days: {} };
    if (!studentId || typeof SiteUrl === 'undefined' || !SiteUrl) {
        return $.Deferred().resolve(empty).promise();
    }

    return $.getJSON(SiteUrl + 'ordercalendar/schooldays', {
        studentId: studentId,
        start: startDate,
        end: endDate
    }).then(function (days) {
        var holidays = {};
        var halfDays = {};
        var dayMap = {};
        (days || []).forEach(function (day) {
            var status = day.Status != null ? day.Status : day.status;
            var date = day.Date || day.date || '';
            if (!date) {
                return;
            }

            var key = String(date).substring(0, 10);
            var title = normalizeMenuDayTitle(day.Title || day.title || '');
            dayMap[key] = { status: status, title: title };
            if (status === 0) {
                holidays[key] = { title: title };
            } else if (status === 2) {
                halfDays[key] = { title: title || 'Half day' };
            }
        });
        window.menuSchoolCalendarDays = dayMap;
        return { holidays: holidays, halfDays: halfDays, days: dayMap };
    }).catch(function () {
        window.menuSchoolCalendarDays = {};
        return empty;
    });
}

var MENU_DAY_STATUS_HOLIDAY = 0;
var MENU_DAY_STATUS_FULL = 1;
var MENU_DAY_STATUS_HALFDAY = 2;

function normalizeMenuDateValue(value) {
    if (value == null || value === '') {
        return '';
    }

    if (value instanceof Date) {
        return dayjs(value).format('YYYY-MM-DD');
    }

    if (typeof value === 'object' && typeof value.getTime === 'function') {
        return dayjs(value).format('YYYY-MM-DD');
    }

    if (dayjs(value).isValid()) {
        return dayjs(value).format('YYYY-MM-DD');
    }

    return String(value).substring(0, 10);
}

function getMenuDayInfo(dateValue) {
    dateValue = normalizeMenuDateValue(dateValue);
    if (!dateValue) {
        return null;
    }
    return (window.menuSchoolCalendarDays || {})[dateValue] || null;
}

function isMenuDateClosed(dateValue) {
    var day = getMenuDayInfo(dateValue);
    return !!day && (day.status === MENU_DAY_STATUS_HOLIDAY || day.status === MENU_DAY_STATUS_HALFDAY);
}

function isMenuWeekendDate(dateValue) {
    if (!dateValue || !dayjs(dateValue).isValid()) {
        return false;
    }

    var dow = dayjs(dateValue).day();
    return dow === 0 || dow === 6;
}

function normalizeMenuDayTitle(title) {
    var value = title ? String(title).trim() : '';
    if (!value) {
        return '';
    }

    var lowered = value.toLowerCase();
    if (lowered === 'holiday' || lowered === 'half day') {
        return '';
    }

    return value;
}

function getMenuDayBadgeLabel(dateValue, dayInfo) {
    dayInfo = dayInfo || getMenuDayInfo(dateValue);
    if (!dayInfo) {
        return '';
    }

    if (dayInfo.status === MENU_DAY_STATUS_HALFDAY) {
        return 'Half day';
    }

    if (dayInfo.status === MENU_DAY_STATUS_HOLIDAY) {
        if (isMenuWeekendDate(dateValue)) {
            return 'Weekend';
        }

        var title = normalizeMenuDayTitle(dayInfo.title);
        return title || 'Holiday';
    }

    return '';
}

function createMenuDateSlide(date, tomorrow, onSelect) {
    var dateValue = date.format('YYYY-MM-DD');
    var dayInfo = getMenuDayInfo(dateValue);
    var badgeLabel = getMenuDayBadgeLabel(dateValue, dayInfo);
    var slide = document.createElement('div');
    slide.classList.add('swiper-slide', 'date-slide-with-badge');

    if (dayInfo && dayInfo.status === MENU_DAY_STATUS_HALFDAY) {
        slide.classList.add('is-halfday');
        slide.title = normalizeMenuDayTitle(dayInfo.title) || 'Half day';
    } else if (dayInfo && dayInfo.status === MENU_DAY_STATUS_HOLIDAY) {
        slide.classList.add('is-holiday');
        slide.title = isMenuWeekendDate(dateValue)
            ? 'Weekend'
            : (normalizeMenuDayTitle(dayInfo.title) || 'No meal service');
    }

    slide.dataset.date = dateValue;
    slide.setAttribute('role', 'option');
    slide.setAttribute('aria-selected', 'false');

    var dayLabel = date.isSame(tomorrow, 'day') ? 'TOMORROW' : date.format('ddd').toUpperCase();
    var card = document.createElement('div');
    card.className = 'date-slide-card';
    card.innerHTML =
        '<div class="date-day">' + dayLabel + '</div>' +
        '<div class="date-number">' + date.format('DD') + '</div>' +
        '<div class="date-month">' + date.format('MMM') + '</div>';
    slide.appendChild(card);

    if (badgeLabel) {
        var tag = document.createElement('span');
        tag.className = 'date-status-tag' +
            (dayInfo && dayInfo.status === MENU_DAY_STATUS_HALFDAY ? ' is-halfday' : ' is-closed');
        tag.textContent = badgeLabel;
        slide.appendChild(tag);
    }

    slide.addEventListener('click', function () {
        onSelect(dateValue);
    });

    return slide;
}

function getMenuClosedDayContent(dateValue) {
    var dayInfo = getMenuDayInfo(dateValue) || {};
    var dayName = dayjs(dateValue).isValid() ? dayjs(dateValue).format('dddd') : dateValue;
    var isWeekend = isMenuWeekendDate(dateValue);
    var isHalfDay = dayInfo.status === MENU_DAY_STATUS_HALFDAY;
    var title = normalizeMenuDayTitle(dayInfo.title);

    if (isHalfDay) {
        return {
            dayName: dayName,
            badgeText: 'Half day',
            messageLine1: 'Meal ordering is not available on half day.',
            messageLine2: 'Please select another day.',
            badgeClass: ' is-halfday',
            closedType: 'halfday'
        };
    }

    if (isWeekend && !title) {
        return {
            dayName: dayName,
            badgeText: 'No meal service today',
            messageLine1: 'Meal ordering is not available on weekend.',
            messageLine2: 'Please select another day.',
            badgeClass: '',
            closedType: 'weekend'
        };
    }

    if (title) {
        return {
            dayName: dayName,
            badgeText: 'No meal service today',
            messageLine1: 'Meal ordering is not available on holiday.',
            messageLine2: 'Please select another day.',
            badgeClass: '',
            closedType: 'holiday'
        };
    }

    return {
        dayName: dayName,
        badgeText: 'No meal service today',
        messageLine1: 'Meal ordering is not available on school holidays.',
        messageLine2: 'Please select another day.',
        badgeClass: '',
        closedType: 'holiday'
    };
}

function getMenuClosedDayMessage(dateValue, dayInfo) {
    var content = getMenuClosedDayContent(dateValue);
    return content.messageLine1 + ' ' + content.messageLine2;
}

function getMenuClosedDayIllustrationUrl() {
    var baseUrl = typeof SiteUrl !== 'undefined' && SiteUrl ? SiteUrl : '/';
    return baseUrl + 'images/calendar-with-cross-mark.png';
}

function buildMenuClosedDayHtml(dateValue) {
    var content = getMenuClosedDayContent(dateValue);
    var illustrationUrl = getMenuClosedDayIllustrationUrl();

    return (
        '<div class="menu-closed-day-card">' +
            '<div class="menu-closed-day-state" data-closed-date="' + dateValue + '">' +
                '<div class="menu-closed-day-illustration" aria-hidden="true">' +
                    '<img src="' + illustrationUrl + '" alt="" class="menu-closed-day-illustration-img" />' +
                '</div>' +
                '<h2 class="menu-closed-day-title">' + content.dayName + '</h2>' +
                '<div class="menu-closed-day-badge' + content.badgeClass + '">' +
                    '<i class="ti ti-calendar-event" aria-hidden="true"></i><span>' + content.badgeText + '</span>' +
                '</div>' +
                '<p class="menu-closed-day-message">' +
                    '<span>' + content.messageLine1 + '</span>' +
                    '<span>' + content.messageLine2 + '</span>' +
                '</p>' +
                '<div class="menu-closed-day-footer">' +
                    '<button type="button" class="btn btn-outline-primary menu-closed-day-action js-choose-another-date">' +
                        '<i class="ti ti-arrow-left" aria-hidden="true"></i> Choose another date' +
                    '</button>' +
                '</div>' +
            '</div>' +
        '</div>'
    );
}

function renderMenuClosedDayState($container, dateValue) {
    if (!$container || !$container.length || !dateValue) {
        return;
    }

    $container.html(buildMenuClosedDayHtml(dateValue));
}

function findNextOpenMenuDate(fromDateValue) {
    fromDateValue = normalizeMenuDateValue(fromDateValue);
    var slides = document.querySelectorAll('#date-container .swiper-slide[data-date]');
    var firstOpen = null;

    for (var i = 0; i < slides.length; i++) {
        var dateValue = normalizeMenuDateValue(slides[i].getAttribute('data-date'));
        if (!dateValue || isMenuDateClosed(dateValue)) {
            continue;
        }

        if (!firstOpen) {
            firstOpen = dateValue;
        }

        if (!fromDateValue || dateValue > fromDateValue) {
            return dateValue;
        }
    }

    return firstOpen;
}

$(document).on('click', '.js-choose-another-date', function (e) {
    e.preventDefault();

    var closedDate = $(this).closest('.menu-closed-day-state').attr('data-closed-date')
        || (typeof getSelectedMealDate === 'function' ? getSelectedMealDate() : '');
    var nextOpen = findNextOpenMenuDate(closedDate);

    if (nextOpen && typeof selectDateSlide === 'function') {
        selectDateSlide(nextOpen, true);
    }
});

/** @deprecated Use fetchSchoolCalendarDays */
function fetchSchoolHolidayDates(studentId, startDate, endDate) {
    return fetchSchoolCalendarDays(studentId, startDate, endDate).then(function (result) {
        return result.holidays;
    });
}
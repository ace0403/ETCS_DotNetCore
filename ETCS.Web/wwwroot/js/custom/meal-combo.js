var selectedIds = [];
var dateSwiper = null;

$(function () {
    $('#StudentId').on('change', function () {
        resetSelection();
        var currentDate = getSelectedMealDate();
        if (currentDate) {
            selectDateSlide(currentDate, true);
        }
    });

    $('#Duration').on('change', function () {
        $('#date-container').empty();
        initDates();
    });

    $(document).on('click', '#div-packagelist .meal-item', function () {
        var $item = $(this);
        var packageId = parseInt($item.find('input[type="checkbox"]').val(), 10);
        var mealDate = getSelectedMealDate();
        var $checkbox = $item.find('input[type="checkbox"]');

        if ($checkbox.is(':checked')) {
            $checkbox.prop('checked', false);
            $item.removeClass('selected').attr('aria-pressed', 'false');
            selectedIds = excludeSelection(selectedIds, packageId, mealDate);
        } else {
            $checkbox.prop('checked', true);
            $item.addClass('selected').attr('aria-pressed', 'true');
            selectedIds = excludeSelection(selectedIds, packageId, mealDate);
            selectedIds.push({ PackageId: packageId, MealDate: mealDate, Id: GUID() });
        }

        updateSelectionBar();
    });

    $(document).on('keydown', '#div-packagelist .meal-item', function (e) {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            $(this).trigger('click');
        }
    });

    $(document).on('click', '#btnPlaceOrder', async function () {
        if (selectedIds.length === 0) {
            await Swal.fire({ title: 'Please select at least one combo.', icon: 'warning' });
            return;
        }

        var allergenItems = $('#div_meal .is-allergen-item[value="true"]').closest('.meal-item')
            .filter(function () { return $(this).find('input[type="checkbox"]').is(':checked'); });

        if (allergenItems.length > 0) {
            var allergies = [];
            allergenItems.each(function () {
                var name = $(this).find('.allergies-name').val();
                if (name) allergies.push(name);
            });
            var childName = $('#StudentId option:selected').text();
            var message = '<b>Allergens: ' + allergies.join(', ') + '</b><br/><br/>I consent to ' + childName +
                ' receiving this combo despite the allergens it contains.';

            var consent = await Swal.fire({
                title: 'The selected combo contains allergens that ' + childName + ' is sensitive to',
                html: message,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Agree to Serve',
                cancelButtonText: 'Cancel Selection'
            });

            if (!consent.isConfirmed) return;
        }

        getOrderSummary(selectedIds);
    });

    $(document).on('click', '#btnConfirmOrder', function () {
        var $btn = $(this);
        if ($btn.prop('disabled') || $btn.data('loading')) {
            return;
        }
        if (selectedIds.length === 0) {
            Swal.fire({ title: 'Please select at least one combo.', icon: 'warning' });
            return;
        }
        showPageOverlay('Processing your order...');
        setButtonLoading($btn, true, 'Processing...');
        placeOrder(selectedIds, $btn);
    });

    initDates();
});

function resetSelection() {
    selectedIds = [];
    updateSelectionBar();
    $('#summary_div').empty();
}

function excludeSelection(arrayList, packageId, mealDate) {
    return arrayList.filter(function (x) {
        return !(x.PackageId == packageId && x.MealDate == mealDate);
    });
}

function searchPackages() {
    var studentId = $('#StudentId').val();
    var mealDate = $('#MealDate').val();
    if (!studentId || !mealDate) return;

    $.ajax({
        url: SiteUrl + 'mealcombo/searchpackages',
        type: 'POST',
        data: {
            StudentId: studentId,
            MealDate: mealDate
        },
        success: function (result) {
            $('#div-packagelist').html(result);
            initSelection();
            updateSelectionBar();
        },
        error: function () {
            toastMsg('Error loading meal combos', false);
        }
    });
}

function getSelectedMealDate() {
    return $('#MealDate').val() || '';
}

function serializeOrderItems(packageList, listKey) {
    var payload = {
        studentId: parseInt($('#StudentId').val(), 10)
    };

    packageList.forEach(function (item, index) {
        payload[listKey + '[' + index + '].PackageId'] = parseInt(item.PackageId, 10);
        payload[listKey + '[' + index + '].MealDate'] = item.MealDate;
        payload[listKey + '[' + index + '].Id'] = item.Id;
    });

    return payload;
}

function highlightDateSlide(dateValue) {
    document.querySelectorAll('#date-container .swiper-slide').forEach(function (slide) {
        var isSelected = slide.dataset.date === dateValue;
        slide.classList.toggle('selected', isSelected);
        slide.setAttribute('aria-selected', isSelected ? 'true' : 'false');
    });
}

function selectDateSlide(dateValue, reloadMenu) {
    if (!dateValue) return;

    $('#MealDate').val(dateValue);
    updateSelectedDateDisplay(dateValue);
    highlightDateSlide(dateValue);

    if (reloadMenu) {
        searchPackages();
    }
}

function updateSelectedDateDisplay(dateValue) {
    var $wrap = $('#mealComboSelectedDate');
    var $text = $('#mealComboSelectedDateText');
    if (!$wrap.length || !dateValue) {
        $wrap.attr('hidden', 'hidden');
        return;
    }

    var formatted = dayjs(dateValue).isValid()
        ? dayjs(dateValue).format('dddd, DD MMM YYYY')
        : dateValue;
    $text.text(formatted);
    $wrap.removeAttr('hidden');
}

function getOrderSummary(packageList) {
    $.ajax({
        url: SiteUrl + 'mealcombo/getordersummary',
        type: 'POST',
        traditional: true,
        data: serializeOrderItems(packageList, 'items'),
        success: function (result) {
            $('#summary_div').html(result);
            var modalEl = document.getElementById('summary_modal');
            if (!modalEl) {
                toastMsg('Unable to load order summary', false);
                return;
            }
            var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
            modal.show();
        },
        error: function () {
            toastMsg('Error loading order summary', false);
        }
    });
}

function initDates() {
    var numberOfDays = parseInt($('#Duration').val(), 10) || 5;
    var $wrapper = $('.swiper-wrapper');
    if (numberOfDays > 10) {
        $wrapper.removeClass('flex-center');
    } else {
        $wrapper.addClass('flex-center');
    }

    if (dateSwiper) {
        dateSwiper.destroy(true, true);
        dateSwiper = null;
    }

    dateSwiper = new Swiper('.ala-carte-swiper', {
        slidesPerView: numberOfDays > 10 ? 10 : numberOfDays,
        spaceBetween: 10,
        centeredSlides: false,
        loop: false
    });

    var dateContainer = document.getElementById('date-container');
    dateContainer.innerHTML = '';
    var today = dayjs();

    for (var i = 0; i < numberOfDays; i++) {
        var date = today.add(i, 'day');
        var dateValue = date.format('YYYY-MM-DD');
        var slide = document.createElement('div');
        slide.classList.add('swiper-slide');
        slide.dataset.date = dateValue;
        slide.setAttribute('role', 'option');
        slide.setAttribute('aria-selected', 'false');
        slide.innerHTML = '<div class="date-day">' + date.format('ddd').toUpperCase() + '</div>' +
            '<div class="date-number">' + date.format('DD/MM') + '</div>';

        slide.addEventListener('click', function (selectedDateValue) {
            return function () {
                selectDateSlide(selectedDateValue, true);
            };
        }(dateValue));

        dateContainer.appendChild(slide);
    }

    var preservedDate = getSelectedMealDate();
    var firstDateValue = today.format('YYYY-MM-DD');
    var dateToSelect = preservedDate && dayjs(preservedDate).isValid() ? preservedDate : firstDateValue;
    var hasMatchingSlide = !!dateContainer.querySelector('.swiper-slide[data-date="' + dateToSelect + '"]');
    selectDateSlide(hasMatchingSlide ? dateToSelect : firstDateValue, true);

    document.getElementById('prev-button').onclick = function () { dateSwiper.slidePrev(); };
    document.getElementById('next-button').onclick = function () { dateSwiper.slideNext(); };
}

function initSelection() {
    $('#div-packagelist .meal-item').each(function () {
        var $item = $(this);
        var packageId = parseInt($item.find('input[type="checkbox"]').val(), 10);
        var mealDate = getSelectedMealDate();
        var exists = selectedIds.some(function (x) {
            return parseInt(x.PackageId, 10) === packageId && x.MealDate === mealDate;
        });
        $item.find('input[type="checkbox"]').prop('checked', exists);
        $item.toggleClass('selected', exists).attr('aria-pressed', exists ? 'true' : 'false');
    });
}

function updateSelectionBar() {
    var count = selectedIds.length;
    var $bar = $('#mealComboOrderBar');
    var $count = $('#mealComboSelectionCount');
    var $page = $('.meal-combo-page');

    if (!$bar.length) return;

    if (count > 0) {
        $bar.removeAttr('hidden');
        $page.addClass('meal-combo-has-selection');
        $count.text(count === 1 ? '1 combo selected' : count + ' combos selected');
    } else {
        $bar.attr('hidden', 'hidden');
        $page.removeClass('meal-combo-has-selection');
        $count.text('0 combos');
    }
}

function readJsonFlag(result, pascalKey, camelKey) {
    if (!result) return false;
    if (result[pascalKey] === true) return true;
    if (result[camelKey] === true) return true;
    return false;
}

function readJsonValue(result, pascalKey, camelKey) {
    if (!result) return '';
    return result[pascalKey] || result[camelKey] || '';
}

function showPageOverlay(message) {
    var $overlay = $('#mealComboPageOverlay');
    if (!$overlay.length) return;

    $('#mealComboPageOverlayText').text(message || 'Please wait...');
    $overlay.removeAttr('hidden');
    $('body').addClass('ala-carte-overlay-open');
}

function hidePageOverlay() {
    var $overlay = $('#mealComboPageOverlay');
    if (!$overlay.length) return;

    $overlay.attr('hidden', 'hidden');
    $('body').removeClass('ala-carte-overlay-open');
}

function setButtonLoading($btn, isLoading, loadingText) {
    if (!$btn || !$btn.length) return;

    if (isLoading) {
        if (!$btn.data('original-html')) {
            $btn.data('original-html', $btn.html());
        }
        $btn.data('loading', true).prop('disabled', true).html(
            '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>' +
            (loadingText || 'Please wait...')
        );
        return;
    }

    $btn.data('loading', false).prop('disabled', false);
    if ($btn.data('original-html')) {
        $btn.html($btn.data('original-html'));
    }
}

function placeOrder(packageList, $btn) {
    $.ajax({
        url: SiteUrl + 'mealcombo/placeorder',
        type: 'POST',
        dataType: 'json',
        traditional: true,
        data: serializeOrderItems(packageList, 'mealList'),
        success: function (result) {
            var isSuccess = readJsonFlag(result, 'Success', 'success');
            var redirectUrl = readJsonValue(result, 'RedirectUrl', 'redirectUrl');
            var message = readJsonValue(result, 'Message', 'message');

            if (isSuccess && redirectUrl) {
                showPageOverlay('Redirecting to payment gateway...');
                setButtonLoading($btn, true, 'Redirecting to payment...');
                window.location.href = redirectUrl;
                return;
            }

            hidePageOverlay();
            setButtonLoading($btn, false);
            toastMsg(message || 'Unable to place order', false);
        },
        error: function () {
            hidePageOverlay();
            setButtonLoading($btn, false);
            toastMsg('Error placing order', false);
        }
    });
}

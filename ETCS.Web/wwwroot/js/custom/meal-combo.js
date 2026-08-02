var selectedIds = [];
var dateSwiper = null;
var MENU_DURATION_DAYS = 30;
var activeMealTypeFilter = 'all';

$(function () {
    initChildSelect();

    $('#StudentId').on('change', function () {
        resetSelection();
        var currentDate = getSelectedMealDate();
        if (currentDate) {
            selectDateSlide(currentDate, true);
        }
    });

    $(document).on('input', '#menuSearchInput', function () {
        applyMenuFilters();
    });

    $(document).on('click', '.menu-category-chip', function () {
        var $chip = $(this);
        activeMealTypeFilter = String($chip.data('meal-type') || 'all');
        $('.menu-category-chip').removeClass('is-active').attr('aria-selected', 'false');
        $chip.addClass('is-active').attr('aria-selected', 'true');
        applyMenuFilters();
    });

    $(document).on('click', '#div-packagelist .meal-item', async function () {
        var $item = $(this);
        var packageId = parseInt($item.find('input[type="checkbox"]').val(), 10);
        var mealDate = getSelectedMealDate();
        var $checkbox = $item.find('input[type="checkbox"]');

        if ($checkbox.is(':checked')) {
            $checkbox.prop('checked', false);
            $item.removeClass('selected').attr('aria-pressed', 'false');
            selectedIds = excludeSelection(selectedIds, packageId, mealDate);
            updateSelectionBar();
            return;
        }

        var isAllergenItem = String($item.find('.is-allergen-item').val() || '').toLowerCase() === 'true';
        if (isAllergenItem) {
            var allergies = [];
            var name = $item.find('.allergies-name').val();
            if (name) {
                String(name).split(',').forEach(function (part) {
                    var trimmed = part.trim();
                    if (trimmed) allergies.push(trimmed);
                });
            }
            var childName = $('#StudentId option:selected').text();
            var consent = await showAllergenConsent(childName, allergies, 'combo');
            if (!consent.isConfirmed) {
                return;
            }
        }

        $checkbox.prop('checked', true);
        $item.addClass('selected').attr('aria-pressed', 'true');
        selectedIds = excludeSelection(selectedIds, packageId, mealDate);
        selectedIds.push({ PackageId: packageId, MealDate: mealDate, Id: GUID() });
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
            await showStyledAlert('Please select at least one combo.');
            return;
        }

        getOrderSummary(selectedIds);
    });

    $(document).on('click', '#btnConfirmOrder', function () {
        var $btn = $(this);
        if ($btn.prop('disabled') || $btn.data('loading')) {
            return;
        }
        if (selectedIds.length === 0) {
            showStyledAlert('Please select at least one combo.');
            return;
        }
        showPageOverlay('Processing your order...');
        setButtonLoading($btn, true, 'Processing...');
        placeOrder(selectedIds, $btn);
    });

    $(document).on('click', '#summary_modal .ala-carte-summary-remove', function (e) {
        e.preventDefault();
        e.stopPropagation();
        removeSummarySelection($(this));
    });

    initDates();
});

function initChildSelect() {
    var $student = $('#StudentId');
    if (!$student.length || typeof $student.select2 !== 'function') {
        return;
    }

    if ($student.hasClass('select2-hidden-accessible')) {
        $student.select2('destroy');
    }

    $student.select2({
        width: '100%',
        minimumResultsForSearch: 6,
        placeholder: 'Select child',
        allowClear: false,
        dropdownParent: $(document.body),
        // Dropdown is attached to body (scroll-jump fix), so theme via this class — not .menu-browse-child nesting.
        dropdownCssClass: 'menu-child-select2-dropdown'
    });

    // Prevent Select2's hidden <select> from pulling the page scroll to the top.
    $student.on('select2:opening select2:open select2:closing select2:close select2:select', function () {
        var pos = captureMenuScroll();
        window.requestAnimationFrame(function () {
            restoreMenuScroll(pos);
        });
    });
}

function getMenuScrollContainer() {
    var $container = $('.pc-container');
    if ($container.length && $container.css('overflow-y') === 'auto') {
        return $container;
    }
    return $(window);
}

function captureMenuScroll() {
    var $scroller = getMenuScrollContainer();
    if ($scroller[0] === window) {
        return {
            type: 'window',
            top: window.scrollY || document.documentElement.scrollTop || 0
        };
    }
    return {
        type: 'element',
        top: $scroller.scrollTop()
    };
}

function restoreMenuScroll(pos) {
    if (!pos) return;
    if (pos.type === 'window') {
        window.scrollTo(0, pos.top);
        return;
    }
    $('.pc-container').scrollTop(pos.top);
}

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

    var scrollPos = captureMenuScroll();

    $.ajax({
        url: SiteUrl + 'mealcombo/searchpackages',
        type: 'POST',
        data: {
            StudentId: studentId,
            MealDate: mealDate
        },
        success: function (result) {
            $('#div-packagelist').html(result);
            activeMealTypeFilter = 'all';
            $('#menuSearchInput').val('');
            initSelection();
            applyMenuFilters();
            updateSelectionBar();
            restoreMenuScroll(scrollPos);
        },
        error: function () {
            toastMsg('Error loading meal combos', false);
        }
    });
}

function applyMenuFilters() {
    var query = String($('#menuSearchInput').val() || '').trim().toLowerCase();
    var visibleCount = 0;
    var showingAll = activeMealTypeFilter === 'all';

    $('.menu-browse-inner').toggleClass('is-showing-all', showingAll);

    $('#div-packagelist .meal-item').each(function () {
        var $card = $(this);
        var typeId = String($card.data('meal-type-id') || '');
        var searchText = String($card.data('search-text') || '');
        var typeOk = showingAll || typeId === String(activeMealTypeFilter);
        var searchOk = !query || searchText.indexOf(query) >= 0;
        var show = typeOk && searchOk;
        $card.toggleClass('is-filtered-out', !show);
        if (show) visibleCount += 1;
    });

    var $empty = $('#menuFilterEmpty');
    if ($empty.length) {
        if (visibleCount === 0 && $('#div-packagelist .meal-item').length > 0) {
            $empty.removeAttr('hidden');
        } else {
            $empty.attr('hidden', 'hidden');
        }
    }
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

function getOrderSummary(packageList, preferredMealDate) {
    $.ajax({
        url: SiteUrl + 'mealcombo/getordersummary',
        type: 'POST',
        traditional: true,
        data: serializeOrderItems(packageList, 'items'),
        success: function (result) {
            var $existing = $('#summary_modal');
            var activeMealDate = preferredMealDate
                || $existing.find('.ala-carte-summary-date-tab.active').attr('data-meal-date')
                || '';

            if ($existing.length && $existing.hasClass('show')) {
                var $incoming = $('<div>').html(result);
                var $newModal = $incoming.find('#summary_modal');
                if (!$newModal.length) {
                    toastMsg('Unable to load order summary', false);
                    return;
                }

                $existing.find('.modal-header').replaceWith($newModal.find('.modal-header'));
                $existing.find('.modal-body').replaceWith($newModal.find('.modal-body'));
                $existing.find('.modal-footer').replaceWith($newModal.find('.modal-footer'));
                activateSummaryDateTab(activeMealDate);
                return;
            }

            disposeSummaryModal();
            $('#summary_div').html(result);

            var modalEl = document.getElementById('summary_modal');
            if (!modalEl) {
                toastMsg('Unable to load order summary', false);
                return;
            }

            bootstrap.Modal.getOrCreateInstance(modalEl).show();
            activateSummaryDateTab(activeMealDate);
        },
        error: function () {
            toastMsg('Error loading order summary', false);
        }
    });
}

function disposeSummaryModal() {
    var modalEl = document.getElementById('summary_modal');
    if (!modalEl) {
        return;
    }

    var instance = bootstrap.Modal.getInstance(modalEl);
    if (instance) {
        instance.dispose();
    }
}

function activateSummaryDateTab(mealDate) {
    if (!mealDate) {
        return;
    }

    var tabBtn = document.querySelector('#summary_modal .ala-carte-summary-date-tab[data-meal-date="' + mealDate + '"]');
    if (!tabBtn) {
        return;
    }

    bootstrap.Tab.getOrCreateInstance(tabBtn).show();
}

function removeSummarySelection($btn) {
    var selectionId = String($btn.data('selection-id') || '').toLowerCase();
    var packageId = parseInt($btn.data('package-id'), 10);
    var mealDate = String($btn.data('meal-date') || '');

    selectedIds = selectedIds.filter(function (x) {
        if (selectionId && String(x.Id || '').toLowerCase() === selectionId) {
            return false;
        }
        if (!selectionId && parseInt(x.PackageId, 10) === packageId && String(x.MealDate) === mealDate) {
            return false;
        }
        return true;
    });

    initSelection();
    updateSelectionBar();

    if (selectedIds.length === 0) {
        var modalEl = document.getElementById('summary_modal');
        var modal = modalEl ? bootstrap.Modal.getInstance(modalEl) : null;
        if (modal) {
            modal.hide();
        }
        disposeSummaryModal();
        $('#summary_div').empty();
        return;
    }

    getOrderSummary(selectedIds, mealDate);
}

function initDates() {
    var numberOfDays = MENU_DURATION_DAYS;
    var $wrapper = $('.swiper-wrapper');
    $wrapper.removeClass('flex-center');

    if (dateSwiper) {
        dateSwiper.destroy(true, true);
        dateSwiper = null;
    }

    dateSwiper = new Swiper('.ala-carte-swiper', {
        slidesPerView: 'auto',
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
        var dayLabel = i === 0
            ? 'TODAY'
            : (i === 1 ? 'TOMORROW' : date.format('ddd').toUpperCase());
        slide.innerHTML =
            '<div class="date-day">' + dayLabel + '</div>' +
            '<div class="date-number">' + date.format('DD') + '</div>' +
            '<div class="date-month">' + date.format('MMM') + '</div>';

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
    var $badge = $('#menuCartBadge');
    var $page = $('.meal-combo-page');

    if (count > 0) {
        $page.addClass('meal-combo-has-selection');
        if ($badge.length) {
            $badge.text(String(count)).removeAttr('hidden');
        }
    } else {
        $page.removeClass('meal-combo-has-selection');
        if ($badge.length) {
            $badge.text('0').attr('hidden', 'hidden');
        }
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

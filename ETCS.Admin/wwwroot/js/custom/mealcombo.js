function formatComboAmount(value) {
    var amount = parseFloat(value);
    return isNaN(amount) ? '0.00' : amount.toFixed(2);
}

var myTable = initAdminDataTable('#grid_table', 'mealcombo/getlist', [
    { data: 'PackageName' },
    { data: 'Price', render: function (d) { return formatComboAmount(d); } },
    { data: 'ProcessingFee', render: function (d) { return formatComboAmount(d); } },
    {
        data: null,
        orderable: false,
        render: function (d, type, row) {
            return formatComboAmount((parseFloat(row.Price) || 0) + (parseFloat(row.ProcessingFee) || 0));
        }
    },
    { data: 'IsActive', render: function (d) { return d ? 'Yes' : 'No'; } },
    {
        data: 'Id',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (d) { return adminActionEditDelete(d); }
    }
], { order: [[0, 'asc']], schoolFilterSelector: '#adminGridSchoolFilter' });

function initMealComboMultiSelect(id) {
    var $el = $('#' + id);
    if (!$el.length || typeof $.fn.multiselect !== 'function') return;
    if ($el.data('multiselect')) {
        $el.multiselect('destroy');
    }
    initMultiSelect(id);
}

function initMealComboMultiSelects() {
    initMealComboMultiSelect('MealItemIds');
    initMealComboMultiSelect('WeekNos');
    initMealComboMultiSelect('DayIds');
}

function loadMealComboItems(schoolId, selectedIds) {
    var $sel = $('#MealItemIds');
    if (!schoolId) {
        $sel.empty();
        initMealComboMultiSelects();
        return;
    }
    $.get(SiteUrl + 'mealcombo/getmealitems?schoolId=' + schoolId, function (r) {
        $sel.empty();
        var selected = {};
        if (selectedIds && selectedIds.length) {
            selectedIds.forEach(function (id) { selected[String(id)] = true; });
        }
        (r.data || []).forEach(function (x) {
            var $opt = $('<option>', { value: x.Id, text: x.ItemName });
            if (selected[String(x.Id)]) {
                $opt.prop('selected', true);
            }
            $sel.append($opt);
        });
        initMealComboMultiSelects();
    });
}

function bindMealComboSchoolChange() {
    var $school = $('#ddlMealComboSchool');
    $school.off('change.mealCombo').on('change.mealCombo', function () {
        loadMealComboItems($(this).val(), null);
    });
    if ($school.val() && !$('#MealItemIds option').length) {
        loadMealComboItems($school.val(), null);
    } else {
        initMealComboMultiSelects();
    }
}

function validateMealComboSchedule() {
    var weekCount = $('#WeekNos option:selected').length;
    var dayCount = $('#DayIds option:selected').length;
    var itemCount = $('#MealItemIds option:selected').length;
    if (weekCount === 0) {
        toastMsg('Select at least one week.', false);
        return false;
    }
    if (dayCount === 0) {
        toastMsg('Select at least one day.', false);
        return false;
    }
    if (itemCount === 0) {
        toastMsg('Select at least one meal item.', false);
        return false;
    }
    return true;
}

function bindMealComboSave() {
    bindAdminFormSave('#frmMealCombo', function ($form) {
        if (!validateMealComboSchedule()) return;

        var formData = new FormData($form[0]);
        $.ajax({
            url: SiteUrl + 'mealcombo/save',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) {
                    myTable.ajax.reload();
                    $('#addDataModal').modal('hide');
                }
            }
        });
    });
}

function loadData(id) {
    $.get(SiteUrl + 'mealcombo/get?id=' + id, function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        bindMealComboSchoolChange();
        bindMealComboSave();
    });
}

function deleteData(id) {
    showConfirmation('Delete this combo?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'mealcombo/delete?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) myTable.ajax.reload();
        });
    });
}

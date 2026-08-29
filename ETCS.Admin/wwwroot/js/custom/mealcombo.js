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
    initMealComboMultiSelect('WeekNos');
    initMealComboMultiSelect('DayIds');
    initMealComboMultiSelect('IngredientIds');
}

function populateMealComboTypeOptions($typeSelect, types, selectedTypeId) {
    $typeSelect.empty().append('<option value="">- Select meal type -</option>');
    (types || []).forEach(function (type) {
        var $option = $('<option></option>').val(type.Id).text(type.Name);
        if (selectedTypeId && String(type.Id) === String(selectedTypeId)) {
            $option.prop('selected', true);
        }
        $typeSelect.append($option);
    });
    $typeSelect.prop('disabled', !(types && types.length));
}

function loadMealComboTypesForSession(sessionId, selectedTypeId, $typeSelect) {
    if (!$typeSelect || !$typeSelect.length) {
        return $.Deferred().resolve().promise();
    }

    if (!sessionId) {
        populateMealComboTypeOptions($typeSelect, [], null);
        return $.Deferred().resolve().promise();
    }

    return $.getJSON(SiteUrl + 'mealcombo/getmealtypes?sessionId=' + encodeURIComponent(sessionId), function (response) {
        populateMealComboTypeOptions($typeSelect, response.data || [], selectedTypeId);
    }).fail(function () {
        populateMealComboTypeOptions($typeSelect, [], null);
        toastMsg('Meal types could not be loaded.', false);
    });
}

function bindMealComboSessionTypeCascade() {
    var $session = $('#MealSessionId');
    var $type = $('#MealTypeId');
    if (!$session.length || !$type.length) {
        return;
    }

    var initialSessionId = $session.val();
    var initialTypeId = $type.data('selected-type-id') || $type.val();

    $session.off('change.mealComboSession').on('change.mealComboSession', function () {
        var sessionId = $(this).val();
        loadMealComboTypesForSession(sessionId, null, $type);
    });

    if (initialSessionId) {
        loadMealComboTypesForSession(initialSessionId, initialTypeId, $type);
    } else {
        populateMealComboTypeOptions($type, [], null);
    }
}

function createNutritionRow(line) {
    var $template = $('#nutritionRowTemplate');
    if (!$template.length) return $();

    var $row = $($template.html());
    $row.find('[data-name]').each(function () {
        var field = $(this).data('name');
        $(this).attr('name', 'NutritionLines[0].' + field);
        $(this).removeAttr('data-name');
        if (!line) return;
        if (field === 'NutritionId') $(this).val(line.NutritionId || '');
        if (field === 'MeasureValue') $(this).val(line.MeasureValue != null ? line.MeasureValue : 0);
        if (field === 'MeasureTypeId') $(this).val(line.MeasureTypeId || '');
    });
    return $row;
}

function reindexNutritionRows() {
    $('#nutritionRows .nutrition-row').each(function (index) {
        $(this).find('[name^="NutritionLines"]').each(function () {
            var field = $(this).attr('name').split('.').pop();
            $(this).attr('name', 'NutritionLines[' + index + '].' + field);
        });
    });
}

function prepareMealComboFormForSubmit() {
    $('#MealTypeId').prop('disabled', false);

    $('#nutritionRows .nutrition-row').each(function () {
        var $row = $(this);
        var nutritionId = $row.find('[name$=".NutritionId"]').val();
        var measureTypeId = $row.find('[name$=".MeasureTypeId"]').val();
        var $measure = $row.find('[name$=".MeasureValue"]');

        if (!nutritionId || !measureTypeId) {
            $row.remove();
            return;
        }

        if ($measure.val() === '') {
            $measure.val('0');
        }
    });

    reindexNutritionRows();
}

function bindNutritionRows() {
    $('#btnAddNutrition').off('click.mealCombo').on('click.mealCombo', function () {
        var $row = createNutritionRow(null);
        if (!$row.length) return;
        $('#nutritionRows').append($row);
        reindexNutritionRows();
    });

    $('#nutritionRows').off('click.mealCombo', '.btn-remove-nutrition').on('click.mealCombo', '.btn-remove-nutrition', function () {
        var $rows = $('#nutritionRows .nutrition-row');
        if ($rows.length <= 1) {
            $rows.find('select').val('');
            $rows.find('input').val('0');
            return;
        }
        $(this).closest('.nutrition-row').remove();
        reindexNutritionRows();
    });
}

function validateMealComboSchedule() {
    if (!$('#MealSessionId').val()) {
        toastMsg('Select a meal session.', false);
        return false;
    }

    if (!$('#MealTypeId').val()) {
        toastMsg('Select a meal type.', false);
        return false;
    }

    var weekCount = $('#WeekNos option:selected').length;
    var dayCount = $('#DayIds option:selected').length;
    if (weekCount === 0) {
        toastMsg('Select at least one week.', false);
        return false;
    }
    if (dayCount === 0) {
        toastMsg('Select at least one day.', false);
        return false;
    }

    return true;
}

function bindMealComboSave() {
    bindAdminFormSave('#frmMealCombo', function ($form) {
        if (!validateMealComboSchedule()) return;

        prepareMealComboFormForSubmit();
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
        initMealComboMultiSelects();
        bindMealComboSessionTypeCascade();
        bindNutritionRows();
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

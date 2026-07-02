var myTable = initAdminDataTable('#grid_table', 'mealitem/getlist', [
    { data: 'ItemName' },
    { data: 'CategoryName' },
    { data: 'Price' },
    { data: 'IsActive', render: function (d) { return d ? 'Yes' : 'No'; } },
    {
        data: 'Id',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (d) { return adminActionEditDelete(d); }
    }
], { order: [[0, 'asc']], schoolFilterSelector: '#adminGridSchoolFilter' });

function initMealItemMultiSelect(id) {
    var $el = $('#' + id);
    if (!$el.length || typeof $.fn.multiselect !== 'function') return;
    if ($el.data('multiselect')) {
        $el.multiselect('destroy');
    }
    initMultiSelect(id);
}

function initMealItemMultiSelects() {
    initMealItemMultiSelect('IngredientIds');
    initMealItemMultiSelect('WeekNos');
    initMealItemMultiSelect('DayIds');
}

function reindexNutritionRows() {
    $('#nutritionRows .nutrition-row').each(function (index) {
        $(this).find('[name^="NutritionLines"]').each(function () {
            var field = $(this).attr('name').split('.').pop();
            $(this).attr('name', 'NutritionLines[' + index + '].' + field);
        });
    });
}

function bindNutritionRows() {
    $('#btnAddNutrition').off('click.mealItem').on('click.mealItem', function () {
        var $template = $('#nutritionRowTemplate');
        if (!$template.length) return;

        var $row = $($template.html());
        $row.find('[data-name]').each(function () {
            var field = $(this).data('name');
            $(this).attr('name', 'NutritionLines[0].' + field);
            $(this).removeAttr('data-name');
        });
        $('#nutritionRows').append($row);
        reindexNutritionRows();
    });

    $('#nutritionRows').off('click.mealItem', '.btn-remove-nutrition').on('click.mealItem', '.btn-remove-nutrition', function () {
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

function validateMealItemSchedule() {
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

    var validNutrition = false;
    $('#nutritionRows .nutrition-row').each(function () {
        var nutritionId = $(this).find('[name$=".NutritionId"]').val();
        var measureTypeId = $(this).find('[name$=".MeasureTypeId"]').val();
        if (nutritionId && measureTypeId) {
            validNutrition = true;
        }
    });
    if (!validNutrition) {
        toastMsg('Add at least one nutrition row.', false);
        return false;
    }
    return true;
}

function bindMealItemSave() {
    bindAdminFormSave('#frmMealItem', function ($form) {
        if (!validateMealItemSchedule()) return;

        reindexNutritionRows();
        var formData = new FormData($form[0]);
        $.ajax({
            url: SiteUrl + 'mealitem/save',
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
    $.get(SiteUrl + 'mealitem/get?id=' + id, function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        initMealItemMultiSelects();
        bindNutritionRows();
        bindMealItemSave();
    });
}

function deleteData(id) {
    showConfirmation('Delete this item?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'mealitem/delete?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) myTable.ajax.reload();
        });
    });
}

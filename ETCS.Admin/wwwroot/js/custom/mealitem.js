var myTable = initAdminDataTable('#grid_table', 'mealitem/getlist', [
    { data: 'ItemName' },
    { data: 'CategoryName' },
    { data: 'OrderTypeNames' },
    { data: 'Price' },
    { data: 'IsActive', render: function (d) { return d ? 'Yes' : 'No'; } },
    {
        data: 'Id',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (d) { return adminActionEditDelete(d); }
    }
], {
    order: [[0, 'asc']],
    schoolFilterSelector: '#adminGridSchoolFilter',
    orderTypeFilterSelector: '#adminGridOrderTypeFilter'
});

var MEAL_ITEM_CHANNEL = window.MEAL_ITEM_CHANNEL || {};

var mealItemLastChannelSelection = [];

function getSelectedMealItemChannelIds() {
    return $('#OrderTypeIds').val() || [];
}

function mealItemHasPosChannel(selected) {
    selected = selected || getSelectedMealItemChannelIds();
    return selected.indexOf(MEAL_ITEM_CHANNEL.POS) >= 0;
}

function mealItemHasMenuChannels(selected) {
    selected = selected || getSelectedMealItemChannelIds();
    return selected.some(function (v) {
        return v === MEAL_ITEM_CHANNEL.MEAL_PLAN || v === MEAL_ITEM_CHANNEL.ALA_CARTE;
    });
}

function applyMealItemMenuChannelPairing(selected, previousSelected) {
    selected = (selected || []).slice();
    previousSelected = previousSelected || [];

    var hadMealPlan = previousSelected.indexOf(MEAL_ITEM_CHANNEL.MEAL_PLAN) >= 0;
    var hadAlaCarte = previousSelected.indexOf(MEAL_ITEM_CHANNEL.ALA_CARTE) >= 0;
    var hasMealPlan = selected.indexOf(MEAL_ITEM_CHANNEL.MEAL_PLAN) >= 0;
    var hasAlaCarte = selected.indexOf(MEAL_ITEM_CHANNEL.ALA_CARTE) >= 0;

    if (hadMealPlan && !hasMealPlan) {
        selected = selected.filter(function (v) { return v !== MEAL_ITEM_CHANNEL.ALA_CARTE; });
        hasAlaCarte = false;
    }

    if (hadAlaCarte && !hasAlaCarte) {
        selected = selected.filter(function (v) { return v !== MEAL_ITEM_CHANNEL.MEAL_PLAN; });
        hasMealPlan = false;
    }

    if (hasMealPlan || hasAlaCarte) {
        if (selected.indexOf(MEAL_ITEM_CHANNEL.MEAL_PLAN) < 0) {
            selected.push(MEAL_ITEM_CHANNEL.MEAL_PLAN);
        }
        if (selected.indexOf(MEAL_ITEM_CHANNEL.ALA_CARTE) < 0) {
            selected.push(MEAL_ITEM_CHANNEL.ALA_CARTE);
        }
    }

    return selected;
}

function isPosOnlyMealItemChannels(selected) {
    selected = selected || getSelectedMealItemChannelIds();
    return selected.length === 1 && selected[0] === MEAL_ITEM_CHANNEL.POS;
}

function refreshMealItemMultiselect(id) {
    var $el = $('#' + id);
    if ($el.length && $el.data('multiselect')) {
        $el.multiselect('refresh');
    }
}

function syncMealItemChannelOptionState() {
    var $select = $('#OrderTypeIds');
    if (!$select.length) return;

    var selected = getSelectedMealItemChannelIds();
    var hasPos = mealItemHasPosChannel(selected);
    var hasMenu = mealItemHasMenuChannels(selected);

    $select.find('option').each(function () {
        var $opt = $(this);
        var val = $opt.val();
        var disable = (hasPos && val !== MEAL_ITEM_CHANNEL.POS)
            || (hasMenu && val === MEAL_ITEM_CHANNEL.POS);
        $opt.prop('disabled', disable);
    });

    refreshMealItemMultiselect('OrderTypeIds');
}

function syncMealItemScheduleRequirements() {
    var posOnly = isPosOnlyMealItemChannels();
    var $weekBlock = $('#WeekNos').closest('.multiselect-custom');
    var $dayBlock = $('#DayIds').closest('.multiselect-custom');

    $weekBlock.find('.form-label').text(posOnly ? 'Week No. (optional)' : 'Week No.');
    $dayBlock.find('.form-label').text(posOnly ? 'Day (optional)' : 'Day');
    $weekBlock.toggleClass('opacity-75', posOnly);
    $dayBlock.toggleClass('opacity-75', posOnly);
}

function bindMealItemChannelRules() {
    var $select = $('#OrderTypeIds');
    if (!$select.length) return;

    var selected = getSelectedMealItemChannelIds();
    if (mealItemHasPosChannel(selected) && mealItemHasMenuChannels(selected)) {
        $select.val(selected.filter(function (v) {
            return v !== MEAL_ITEM_CHANNEL.POS;
        }));
        refreshMealItemMultiselect('OrderTypeIds');
        selected = getSelectedMealItemChannelIds();
    }

    selected = applyMealItemMenuChannelPairing(selected, []);
    if (selected.join(',') !== getSelectedMealItemChannelIds().join(',')) {
        $select.val(selected);
        refreshMealItemMultiselect('OrderTypeIds');
        selected = getSelectedMealItemChannelIds();
    }

    mealItemLastChannelSelection = selected.slice();

    $select.off('change.mealItemChannels').on('change.mealItemChannels', function () {
        var previous = mealItemLastChannelSelection.slice();
        var selected = getSelectedMealItemChannelIds();

        if (mealItemHasPosChannel(selected) && mealItemHasMenuChannels(selected)) {
            var added = selected.filter(function (v) {
                return previous.indexOf(v) < 0;
            });

            if (added.indexOf(MEAL_ITEM_CHANNEL.POS) >= 0) {
                selected = [MEAL_ITEM_CHANNEL.POS];
            } else {
                selected = selected.filter(function (v) {
                    return v !== MEAL_ITEM_CHANNEL.POS;
                });
            }
        }

        selected = applyMealItemMenuChannelPairing(selected, previous);

        $select.val(selected);
        refreshMealItemMultiselect('OrderTypeIds');
        selected = getSelectedMealItemChannelIds();

        mealItemLastChannelSelection = selected.slice();
        syncMealItemChannelOptionState();
        syncMealItemScheduleRequirements();
    });

    syncMealItemChannelOptionState();
    syncMealItemScheduleRequirements();
}

function initMealItemMultiSelect(id) {
    initAdminMultiSelect(id);
}

function initMealItemMultiSelects() {
    initMealItemMultiSelect('SchoolIds');
    initMealItemMultiSelect('IngredientIds');
    initMealItemMultiSelect('WeekNos');
    initMealItemMultiSelect('DayIds');
    initOrderTypeMultiselect();
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
    if ($('#SchoolIds option:selected').length === 0) {
        toastMsg('Select at least one school.', false);
        return false;
    }

    if (!$('#MealSessionId').val()) {
        toastMsg('Select a meal session.', false);
        return false;
    }

    if (!$('#MealTypeId').val()) {
        toastMsg('Select a meal type.', false);
        return false;
    }

    var selectedChannels = getSelectedMealItemChannelIds();
    if (selectedChannels.length === 0) {
        toastMsg('Select at least one channel.', false);
        return false;
    }

    if (mealItemHasPosChannel(selectedChannels) && mealItemHasMenuChannels(selectedChannels)) {
        toastMsg('POS cannot be combined with Meal Plans or A La Carte.', false);
        return false;
    }

    if (!isPosOnlyMealItemChannels(selectedChannels)) {
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

function populateMealTypeOptions($typeSelect, types, selectedTypeId) {
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

function loadMealTypesForSession(sessionId, selectedTypeId, $typeSelect) {
    if (!$typeSelect || !$typeSelect.length) {
        return $.Deferred().resolve().promise();
    }

    if (!sessionId) {
        populateMealTypeOptions($typeSelect, [], null);
        return $.Deferred().resolve().promise();
    }

    return $.getJSON(SiteUrl + 'mealitem/getmealtypes?sessionId=' + encodeURIComponent(sessionId), function (response) {
        populateMealTypeOptions($typeSelect, response.data || [], selectedTypeId);
    }).fail(function () {
        populateMealTypeOptions($typeSelect, [], null);
        toastMsg('Meal types could not be loaded.', false);
    });
}

function bindMealItemSessionTypeCascade() {
    var $session = $('#MealSessionId');
    var $type = $('#MealTypeId');
    if (!$session.length || !$type.length) {
        return;
    }

    var initialSessionId = $session.val();
    var initialTypeId = $type.data('selected-type-id') || $type.val();

    $session.off('change.mealItemSession').on('change.mealItemSession', function () {
        var sessionId = $(this).val();
        loadMealTypesForSession(sessionId, null, $type);
    });

    if (initialSessionId) {
        loadMealTypesForSession(initialSessionId, initialTypeId, $type);
    } else {
        populateMealTypeOptions($type, [], null);
    }
}

function bindImportMealSessionTypeCascade() {
    var $session = $('#importMealSessionId');
    var $type = $('#importMealTypeId');
    if (!$session.length || !$type.length) {
        return;
    }

    $session.off('change.mealItemImportSession').on('change.mealItemImportSession', function () {
        var sessionId = $(this).val();
        loadMealTypesForSession(sessionId, null, $type);
    });
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
        bindMealItemChannelRules();
        bindMealItemSessionTypeCascade();
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

var mealItemImportToken = null;

function resetMealItemImportPreview() {
    mealItemImportToken = null;
    $('#importStepReview').addClass('d-none');
    $('#importStepUpload').removeClass('d-none');
    $('#btnImportBack').addClass('d-none');
    $('#btnImportPreview').removeClass('d-none');
    $('#btnImportConfirm').addClass('d-none').prop('disabled', true);
    $('#importPreviewRows').empty();
    $('#importWarnings, #importCategoriesCreated').addClass('d-none').empty();
    $('#importCountToInsert, #importCountExists, #importCountInvalid, #importCountParsed, #importCountCategoriesCreated').text('0');
}

function renderImportStatus(status) {
    if (status === 0 || status === 'Ready') return '<span class="badge bg-success">Ready</span>';
    if (status === 1 || status === 'Exists') return '<span class="badge bg-warning text-dark">Exists</span>';
    return '<span class="badge bg-danger">Invalid</span>';
}

function bindMealItemImport() {
    $('#btnOpenImport').off('click.mealItemImport').on('click.mealItemImport', function () {
        $.get(SiteUrl + 'mealitem/import', function (html) {
            $('#div_import').html(html);
            resetMealItemImportPreview();
            $('#importDataModal').modal('show');
            bindMealItemImportHandlers();
        });
    });
}

function bindMealItemImportHandlers() {
    bindImportMealSessionTypeCascade();

    $('#importSchoolId, #importMealSessionId, #importMealTypeId, #importFile, #importCreateMissingCategories').off('change.mealItemImport').on('change.mealItemImport', function () {
        resetMealItemImportPreview();
    });

    $('#btnImportBack').off('click.mealItemImport').on('click.mealItemImport', function () {
        resetMealItemImportPreview();
    });

    $('#btnImportPreview').off('click.mealItemImport').on('click.mealItemImport', function () {
        var schoolId = $('#importSchoolId').val();
        var mealSessionId = $('#importMealSessionId').val();
        var mealTypeId = $('#importMealTypeId').val();
        var fileInput = $('#importFile')[0];

        if (!schoolId || !mealSessionId || !mealTypeId) {
            toastMsg('Select school, meal session, and meal type.', false);
            return;
        }
        if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
            toastMsg('Select an Excel file.', false);
            return;
        }

        var formData = new FormData();
        formData.append('schoolId', schoolId);
        formData.append('mealSessionId', mealSessionId);
        formData.append('mealTypeId', mealTypeId);
        formData.append('createMissingCategories', $('#importCreateMissingCategories').is(':checked'));
        formData.append('file', fileInput.files[0]);

        $('#btnImportPreview').prop('disabled', true);
        $.ajax({
            url: SiteUrl + 'mealitem/importpreview',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (r) {
                if (!r.Success) {
                    toastMsg(r.Message || 'Preview failed.', false);
                    return;
                }

                mealItemImportToken = r.ImportToken || null;
                $('#importCountToInsert').text(r.ToInsert || 0);
                $('#importCountExists').text(r.SkippedExisting || 0);
                $('#importCountInvalid').text(r.SkippedInvalid || 0);
                $('#importCountParsed').text(r.ParsedCount || 0);
                $('#importCountCategoriesCreated').text(r.CategoriesCreated || 0);

                var $categoriesCreated = $('#importCategoriesCreated');
                if (r.CreatedCategoryNames && r.CreatedCategoryNames.length > 0) {
                    $categoriesCreated.removeClass('d-none').html('<strong>Categories created:</strong><ul class="mb-0 ps-3">' +
                        r.CreatedCategoryNames.map(function (name) { return '<li>' + $('<div>').text(name).html() + '</li>'; }).join('') +
                        '</ul>');
                } else {
                    $categoriesCreated.addClass('d-none').empty();
                }

                var $warnings = $('#importWarnings');
                if (r.Warnings && r.Warnings.length > 0) {
                    $warnings.removeClass('d-none').html('<strong>Warnings:</strong><ul class="mb-0 ps-3">' +
                        r.Warnings.map(function (w) { return '<li>' + $('<div>').text(w).html() + '</li>'; }).join('') +
                        '</ul>');
                } else {
                    $warnings.addClass('d-none').empty();
                }

                var rowsHtml = (r.Rows || []).map(function (row) {
                    return '<tr>' +
                        '<td>' + $('<div>').text(row.ItemName || '').html() + '</td>' +
                        '<td>' + $('<div>').text(row.CategoryName || '').html() + '</td>' +
                        '<td>' + $('<div>').text((row.WeekNos || []).join(', ')).html() + '</td>' +
                        '<td>' + $('<div>').text((row.DayNames || []).join(', ')).html() + '</td>' +
                        '<td>' + renderImportStatus(row.Status) + '</td>' +
                        '<td>' + $('<div>').text(row.Message || '').html() + '</td>' +
                        '</tr>';
                }).join('');
                $('#importPreviewRows').html(rowsHtml);

                $('#importStepUpload').addClass('d-none');
                $('#importStepReview').removeClass('d-none');
                $('#btnImportPreview').addClass('d-none');
                $('#btnImportBack').removeClass('d-none');
                $('#btnImportConfirm').removeClass('d-none').prop('disabled', !mealItemImportToken);
                toastMsg(r.Message || 'Preview ready.', true);
            },
            error: function () {
                toastMsg('Preview failed.', false);
            },
            complete: function () {
                $('#btnImportPreview').prop('disabled', false);
            }
        });
    });

    $('#btnImportConfirm').off('click.mealItemImport').on('click.mealItemImport', function () {
        if (!mealItemImportToken) {
            toastMsg('Preview is required before import.', false);
            return;
        }

        $('#btnImportConfirm').prop('disabled', true);
        $.post(SiteUrl + 'mealitem/importconfirm', { importToken: mealItemImportToken }, function (r) {
            toastMsg(r.Message || (r.Success ? 'Import completed.' : 'Import failed.'), r.Success);
            if (r.Success || (r.Inserted && r.Inserted > 0)) {
                myTable.ajax.reload();
                $('#importDataModal').modal('hide');
            }
        }).fail(function () {
            toastMsg('Import failed.', false);
        }).always(function () {
            $('#btnImportConfirm').prop('disabled', false);
        });
    });
}

bindMealItemImport();

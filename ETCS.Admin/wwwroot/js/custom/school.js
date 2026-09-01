var myTable = initAdminDataTable('#grid_table', 'school/getlist', [
    { data: 'Name' }, { data: 'Code' }, { data: 'CountryName' }, { data: 'MinimumTopupAmount' },
    { data: 'HasEmailNotification', render: function (d) { return d ? 'Yes' : 'No'; } },
    {
        data: 'Id',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (data) { return adminActionEditDelete(data); }
    }
]);

function initSchoolMultiSelect(id) {
    initAdminMultiSelect(id);
}

function initSchoolMultiSelects() {
    initSchoolMultiSelect('OrderTypeIds');
    $('#gradeOrderTypeTable .grade-order-types').each(function () {
        initSchoolMultiSelect(this.id);
    });
}

function bindGradeOrderTypeToggles() {
    $('#gradeOrderTypeTable').off('change.gradeNoService').on('change.gradeNoService', '.grade-no-service', function () {
        var $row = $(this).closest('tr');
        var $select = $row.find('.grade-order-types');
        if (this.checked) {
            $select.prop('disabled', true).val([]).trigger('change');
        } else {
            $select.prop('disabled', false);
        }
    });
}

function appendGradeOrderTypeConfigs(formData, $form) {
    var configIndex = 0;
    $('#gradeOrderTypeTable .grade-order-type-row').each(function () {
        var $row = $(this);
        var gradeId = parseInt($row.data('grade-id'), 10);
        var isNoService = $row.find('.grade-no-service').is(':checked');
        var orderTypeIds = isNoService ? [] : ($row.find('.grade-order-types').val() || []);

        if (!isNoService && orderTypeIds.length === 0) {
            return;
        }

        formData.append('GradeOrderTypeConfigs[' + configIndex + '].GradeId', gradeId);
        formData.append('GradeOrderTypeConfigs[' + configIndex + '].IsNoService', isNoService ? 'true' : 'false');
        orderTypeIds.forEach(function (orderTypeId) {
            formData.append('GradeOrderTypeConfigs[' + configIndex + '].OrderTypeIds', orderTypeId);
        });
        configIndex++;
    });
}

function loadData(id) {
    $.get(SiteUrl + 'school/get?id=' + id, function (html) {
        $('#div_add').html(html);
        $('#addDataModal').modal('show');
        initSchoolMultiSelects();
        bindGradeOrderTypeToggles();
        bindSave();
    });
}

function bindSave() {
    bindAdminFormSave('#frmSchool', function ($form) {
        var formData = new FormData($form[0]);
        formData.delete('GradeOrderTypeConfigs');
        $form.find('[name^="GradeOrderTypeConfigs"]').each(function () {
            formData.delete(this.name);
        });
        appendGradeOrderTypeConfigs(formData, $form);
        $.ajax({
            url: SiteUrl + 'school/save',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) { myTable.ajax.reload(); $('#addDataModal').modal('hide'); }
            }
        });
    });
}

function deleteData(id) {
    showConfirmation('Delete this school?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'school/delete?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) myTable.ajax.reload();
        });
    });
}

$(function () {
    bindGradeOrderTypeToggles();
    bindSave();
});

var myTable = initAdminDataTable('#grid_table', 'student/getlist', [
    { data: 'UserId', visible: false },
    { data: 'StudCode' }, { data: 'Name' }, { data: 'SchoolName' }, { data: 'Grade' }, { data: 'GuardianName' }, { data: 'Balance' },
    { data: 'CreatedAt', render: function (d) { return formatReportDate(d); } },
    {
        data: 'UserId',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (d) { return adminActionEditDelete(d); }
    }
], { order: [[0, 'desc']], schoolFilterSelector: '#adminGridSchoolFilter' });

function exportStudents() {
    $('#studentExportSearch').val($('#adminGridSearch').val() || '');
    var schoolId = $('#adminGridSchoolFilter').val();
    $('#studentExportSchoolId').val(schoolId || '');
    $('#frmStudentExport').submit();
}

function initStudentAllergyMultiselect() {
    initAllergyMultiselect();
}

function initStudentGuardianSelect() {
    var $guardian = $('#frmStudent .js-student-guardian-select');
    if (!$guardian.length || typeof $guardian.select2 !== 'function') {
        return;
    }

    if ($guardian.hasClass('select2-hidden-accessible')) {
        $guardian.select2('destroy');
    }

    $guardian.select2({
        width: '100%',
        minimumResultsForSearch: 0,
        placeholder: '- Select Parent -',
        allowClear: true,
        dropdownParent: $('#addDataModal'),
        dropdownCssClass: 'student-parent-select2-dropdown',
        containerCssClass: 'student-parent-select2-container'
    });
}

function loadData(id) {
    $.get(SiteUrl + 'student/get?id=' + id, function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        $('#addDataModal').one('shown.bs.modal', function () {
            initStudentGuardianSelect();
            initStudentAllergyMultiselect();
            initStudentCardNo($('#frmStudent'));
        });
        bindAdminFormSave('#frmStudent', function ($form) {
            $.post(SiteUrl + 'student/save', serializeStudentCardForm($form), function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) { myTable.ajax.reload(); $('#addDataModal').modal('hide'); }
            });
        });
    });
}

function deleteData(id) {
    showConfirmation('Delete this student?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'student/delete?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) myTable.ajax.reload();
        });
    });
}

var myTable = initAdminDataTable('#grid_table', 'staff/getlist', [
    { data: 'StaffId' },
    { data: 'LoginName' },
    { data: 'Name' },
    { data: 'Email' },
    { data: 'RoleName' },
    { data: 'IsActive', render: function (d) { return d ? 'Yes' : 'No'; } },
    {
        data: 'Id',
        width: '70px',
        className: 'text-center admin-action-cell',
        orderable: false,
        render: function (d) { return adminActionEditDelete(d); }
    }
], { order: [[2, 'asc']], schoolFilterSelector: '#adminGridSchoolFilter' });

function parseIdList(raw) {
    return (raw || '')
        .split(',')
        .map(function (value) { return parseInt(value, 10); })
        .filter(function (value) { return !isNaN(value) && value > 0; });
}

function initStaffMultiSelects() {
    initAdminMultiSelect('ddlStaffSchool');
}

function loadStaffSchools(countryId, selectedSchoolIds) {
    var $school = $('#ddlStaffSchool');
    if (!countryId) {
        $school.html('');
        initStaffMultiSelects();
        return;
    }
    $.get(SiteUrl + 'staff/schoolsbycountry?countryId=' + countryId, function (schools) {
        var html = '';
        (schools || []).forEach(function (s) {
            var selected = selectedSchoolIds.indexOf(s.Id) >= 0 ? ' selected' : '';
            html += '<option value="' + s.Id + '"' + selected + '>' + $('<div/>').text(s.Name).html() + '</option>';
        });
        $school.html(html);
        initStaffMultiSelects();
    });
}

function bindStaffCountrySchool() {
    var $country = $('#ddlStaffCountry');
    var selectedSchoolIds = parseIdList($('#hdnSelectedSchoolIds').val());
    $country.off('change.staffSchool').on('change.staffSchool', function () {
        loadStaffSchools($(this).val(), []);
    });
    if ($country.val()) {
        loadStaffSchools($country.val(), selectedSchoolIds);
    } else {
        initStaffMultiSelects();
    }
}

function serializeStaffForm($form) {
    var data = $form.serializeArray().filter(function (item) {
        return item.name !== 'SchoolIds';
    });
    ($('#ddlStaffSchool').val() || []).forEach(function (schoolId) {
        data.push({ name: 'SchoolIds', value: schoolId });
    });
    return $.param(data);
}

function validateStaffForm() {
    if (($('#ddlStaffSchool').val() || []).length === 0) {
        toastMsg('Select at least one school.', false);
        return false;
    }
    if (!$('#ddlStaffRole').val()) {
        toastMsg('Select a role.', false);
        return false;
    }
    return true;
}

function loadData(id) {
    $.get(SiteUrl + 'staff/get?id=' + id, function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        bindStaffCountrySchool();
        bindAdminFormSave('#frmStaff', function ($form) {
            if (!validateStaffForm()) return;
            $.post(SiteUrl + 'staff/save', serializeStaffForm($form), function (r) {
                toastMsg(r.Message, r.Success);
                if (r.Success) {
                    myTable.ajax.reload();
                    $('#addDataModal').modal('hide');
                }
            });
        });
    });
}

function deleteData(id) {
    showConfirmation('Delete this staff record?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'staff/delete?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) myTable.ajax.reload();
        });
    });
}

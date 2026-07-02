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

function loadStaffSchools(countryId, selectedSchoolId) {
    var $school = $('#ddlStaffSchool');
    if (!countryId) {
        $school.html('<option value="">- Select -</option>');
        return;
    }
    $.get(SiteUrl + 'staff/schoolsbycountry?countryId=' + countryId, function (schools) {
        var html = '<option value="">- Select -</option>';
        (schools || []).forEach(function (s) {
            html += '<option value="' + s.Id + '">' + $('<div/>').text(s.Name).html() + '</option>';
        });
        $school.html(html);
        if (selectedSchoolId) {
            $school.val(String(selectedSchoolId));
        }
    });
}

function bindStaffCountrySchool() {
    var $country = $('#ddlStaffCountry');
    var selectedSchoolId = $('#hdnSelectedSchoolId').val() || $('#ddlStaffSchool').val();
    $country.off('change.staffSchool').on('change.staffSchool', function () {
        loadStaffSchools($(this).val(), null);
    });
    if ($country.val()) {
        loadStaffSchools($country.val(), selectedSchoolId);
    }
}

function loadData(id) {
    $.get(SiteUrl + 'staff/get?id=' + id, function (h) {
        $('#div_add').html(h);
        $('#addDataModal').modal('show');
        bindStaffCountrySchool();
        bindAdminFormSave('#frmStaff', function ($form) {
            $.post(SiteUrl + 'staff/save', $form.serialize(), function (r) {
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

var roleModuleKey = 'Role';

var roleTable = initAdminDataTable('#grid_table', 'role/getlist', [
    { data: 'RoleName' },
    { data: 'IsSuperAdmin', render: function (d) { return d ? 'Yes' : 'No'; } },
    { data: 'IsSystem', render: function (d) { return d ? 'Yes' : 'No'; } },
    { data: 'IsActive', render: function (d) { return d ? 'Yes' : 'No'; } },
    { data: 'UserCount' },
    {
        data: 'RoleId',
        orderable: false,
        width: '70px',
        className: 'text-center admin-action-cell',
        render: function (d, t, row) {
            var items = [];
            if (adminCan(roleModuleKey, 'edit')) {
                items.push({ label: 'Edit', icon: 'ti ti-edit', onclick: 'loadRole(' + d + '); return false;' });
            }
            if (adminCan(roleModuleKey, 'delete') && !row.IsSystem) {
                items.push({ label: 'Delete', icon: 'ti ti-trash', onclick: 'deleteRole(' + d + '); return false;', className: 'text-danger' });
            }
            return items.length ? renderAdminActionMenu(items) : '';
        }
    }
], { order: [[0, 'asc']] });

function collectRolePermissions() {
    var permissions = [];
    $('#tblPermissions tbody tr[data-module-id]').each(function () {
        var $row = $(this);
        permissions.push({
            ModuleId: parseInt($row.data('module-id'), 10),
            CanView: $row.find('.perm-view').is(':checked'),
            CanAdd: $row.find('.perm-add').is(':checked'),
            CanEdit: $row.find('.perm-edit').is(':checked'),
            CanDelete: $row.find('.perm-delete').is(':checked')
        });
    });
    return permissions;
}

function loadRole(id) {
    $.get(SiteUrl + 'role/get?id=' + id, function (html) {
        $('#div_add').html(html);
        $('#addDataModal').modal('show');
        $('#btnSaveRole').off('click').on('click', saveRole);
    });
}

function saveRole() {
    var payload = {
        RoleId: parseInt($('#hdnRoleId').val(), 10) || 0,
        RoleName: $.trim($('#txtRoleName').val()),
        IsActive: $('#chkIsActive').is(':checked'),
        Permissions: collectRolePermissions()
    };

    $.ajax({
        url: SiteUrl + 'role/save',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(payload),
        success: function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) {
                roleTable.ajax.reload();
                $('#addDataModal').modal('hide');
            }
        }
    });
}

function deleteRole(id) {
    showConfirmation('Delete this role?', 'Delete').then(function (result) {
        if (!result.isConfirmed) return;
        $.get(SiteUrl + 'role/delete?id=' + id, function (r) {
            toastMsg(r.Message, r.Success);
            if (r.Success) roleTable.ajax.reload();
        });
    });
}

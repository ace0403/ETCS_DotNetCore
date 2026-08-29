/** Maps PascalCase server JSON to DataTables server-side format. */

function adminDataTableAjax(relativeUrl) {
    var lastDraw = 1;

    return {
        url: SiteUrl + relativeUrl,
        type: 'POST',
        data: function (payload) {
            if (payload && payload.draw) {
                lastDraw = payload.draw;
            }
        },
        dataFilter: function (raw) {
            var j = JSON.parse(raw);
            var draw = j.Draw != null ? j.Draw : j.draw;
            if (!draw) {
                draw = lastDraw;
            }

            return JSON.stringify({
                draw: draw,
                recordsTotal: j.RecordsTotal != null ? j.RecordsTotal : j.recordsTotal,
                recordsFiltered: j.RecordsFiltered != null ? j.RecordsFiltered : j.recordsFiltered,
                data: j.Data || j.data || []
            });
        }
    };
}

function renderAdminActionMenu(items) {
    // Fixed strategy escapes DataTables scroll wrappers that clip menus on short tables.
    var html = '<div class="dropdown admin-table-action">';
    html += '<button type="button" class="btn admin-action-toggle" data-bs-toggle="dropdown"';
    html += ' data-bs-popper-config=\'{"strategy":"fixed"}\' aria-expanded="false" title="Actions">';
    html += '<i class="ti ti-dots-vertical"></i></button>';
    html += '<ul class="dropdown-menu dropdown-menu-end admin-action-menu shadow-sm">';

    for (var i = 0; i < items.length; i++) {
        var item = items[i];
        var itemClass = 'dropdown-item' + (item.className ? ' ' + item.className : '');
        html += '<li><a class="' + itemClass + '" href="javascript:;" onclick="' + item.onclick + '">';
        html += '<i class="' + item.icon + ' me-2"></i>' + item.label + '</a></li>';
    }

    html += '</ul></div>';
    return html;
}

$(document).on('mousedown.adminActionMenu', '.admin-table-action .admin-action-toggle', function () {
    var $dropdown = $(this).closest('.admin-table-action');
    var toggle = this;
    var $menu = $dropdown.find('.admin-action-menu');
    var toggleRect = toggle.getBoundingClientRect();
    var menuHeight = $menu.outerHeight() || ($menu.children().length * 42) || 96;
    var spaceBelow = window.innerHeight - toggleRect.bottom;
    $dropdown.toggleClass('dropup', spaceBelow < menuHeight + 12);
});

$(document).on('hidden.bs.dropdown', '.admin-table-action', function () {
    $(this).removeClass('dropup');
});

function adminCan(moduleKey, action) {
    if (window.adminIsFullAccess) return true;
    if (!window.adminPermissions) return true;
    var module = window.adminPermissions[moduleKey];
    if (!module) return false;
    return !!module[action];
}

function adminActionEditDelete(id, editFn, deleteFn, moduleKey) {
    editFn = editFn || 'loadData';
    deleteFn = deleteFn || 'deleteData';
    moduleKey = moduleKey || window.adminModuleKey || '';

    var items = [];
    if (!moduleKey || adminCan(moduleKey, 'edit')) {
        items.push({ label: 'Edit', icon: 'ti ti-edit', onclick: editFn + '(' + id + '); return false;' });
    }
    if (!moduleKey || adminCan(moduleKey, 'delete')) {
        items.push({ label: 'Delete', icon: 'ti ti-trash', onclick: deleteFn + '(' + id + '); return false;', className: 'text-danger' });
    }

    if (!items.length) return '';
    return renderAdminActionMenu(items);
}

function bindAdminGridSearch(table, searchSelector, clearSelector, delay) {
    var $input = $(searchSelector);
    if (!$input.length) return;

    var $clear = $(clearSelector || '#adminGridSearchClear');
    var timer = null;

    function applySearch() {
        table.search($input.val()).draw();
        $clear.toggle(!!$.trim($input.val()));
    }

    $input.off('.adminGridSearch').on('input.adminGridSearch', function () {
        clearTimeout(timer);
        timer = setTimeout(applySearch, delay);
    }).on('keydown.adminGridSearch', function (e) {
        if (e.key === 'Enter') e.preventDefault();
    });

    $clear.off('.adminGridSearch').on('click.adminGridSearch', function () {
        $input.val('');
        applySearch();
        $input.trigger('focus');
    });

    $clear.toggle(!!$.trim($input.val()));
}

function bindAdminGridSchoolFilter(table, filterSelector) {
    var $filter = $(filterSelector || '#adminGridSchoolFilter');
    if (!$filter.length) return;

    $filter.off('.adminGridSchoolFilter').on('change.adminGridSchoolFilter', function () {
        table.draw();
    });
}

function bindAdminDataTableEmptyStateFix(table) {
    function clearStuckProcessing() {
        if (table.page.info().recordsDisplay !== 0) {
            return;
        }

        $(table.table().container()).find('.dt-processing').remove();
    }

    table.on('xhr.dt.adminEmptyFix', clearStuckProcessing);
    table.on('draw.dt.adminEmptyFix', clearStuckProcessing);
}

function bindAdminGridOrderTypeFilter(table, filterSelector) {
    var $filter = $(filterSelector || '#adminGridOrderTypeFilter');
    if (!$filter.length) return;

    $filter.off('.adminGridOrderTypeFilter').on('change.adminGridOrderTypeFilter', function () {
        table.draw();
    });
}

function initAdminDataTable(selector, relativeUrl, columns, options) {
    options = options || {};
    var searchDelay = options.searchDelay || 400;
    var ajaxConfig = adminDataTableAjax(relativeUrl);

    if (options.schoolFilterSelector) {
        var schoolFilterSelector = options.schoolFilterSelector;
        var baseDataFn = ajaxConfig.data;

        ajaxConfig.data = function (payload) {
            if (typeof baseDataFn === 'function') {
                baseDataFn(payload);
            }

            var schoolId = $(schoolFilterSelector).val();
            if (schoolId) {
                payload.SchoolId = schoolId;
            }
        };
    }

    if (options.orderTypeFilterSelector) {
        var orderTypeFilterSelector = options.orderTypeFilterSelector;
        var previousDataFn = ajaxConfig.data;

        ajaxConfig.data = function (payload) {
            if (typeof previousDataFn === 'function') {
                previousDataFn(payload);
            }

            var orderTypeId = $(orderTypeFilterSelector).val();
            if (orderTypeId) {
                payload.OrderTypeId = orderTypeId;
            }
        };
    }

    if (typeof options.extraAjaxData === 'function') {
        var extraAjaxData = options.extraAjaxData;
        var extraPreviousDataFn = ajaxConfig.data;

        ajaxConfig.data = function (payload) {
            if (typeof extraPreviousDataFn === 'function') {
                extraPreviousDataFn(payload);
            }

            extraAjaxData(payload);
        };
    }

    var table = $(selector).DataTable({
        processing: true,
        serverSide: true,
        searching: true,
        searchDelay: searchDelay,
        scrollX: options.scrollX !== false,
        responsive: false,
        pageLength: options.pageLength || 25,
        autoWidth: false,
        language: {
            emptyTable: 'No records found',
            zeroRecords: 'No matching records found'
        },
        layout: {
            topStart: null,
            topEnd: null,
            bottomStart: 'info',
            bottomEnd: 'paging'
        },
        ajax: ajaxConfig,
        columns: columns,
        order: options.order || [[0, 'asc']]
    });

    bindAdminGridSearch(
        table,
        options.searchSelector || '#adminGridSearch',
        options.searchClearSelector || '#adminGridSearchClear',
        searchDelay
    );

    bindAdminGridSchoolFilter(table, options.schoolFilterSelector);
    bindAdminGridOrderTypeFilter(table, options.orderTypeFilterSelector);
    bindAdminDataTableEmptyStateFix(table);

    if (window.adminSchoolScope && window.adminSchoolScope.restricted && options.schoolFilterSelector) {
        var allowed = window.adminSchoolScope.schoolIds || [];
        var $filter = $(options.schoolFilterSelector);
        if (allowed.length === 1) {
            $filter.val(String(allowed[0])).prop('disabled', true);
            table.draw();
        }
    }

    return table;
}

function formatReportDate(value) {
    if (!value) return '';
    var d = new Date(value);
    if (isNaN(d.getTime())) return value;
    return d.toLocaleString();
}
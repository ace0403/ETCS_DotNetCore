/** Maps PascalCase server JSON to DataTables server-side format. */

function adminDataTableAjax(relativeUrl) {

    return {

        url: SiteUrl + relativeUrl,

        type: 'POST',

        dataFilter: function (raw) {

            var j = JSON.parse(raw);

            return JSON.stringify({

                draw: j.Draw,

                recordsTotal: j.RecordsTotal,

                recordsFiltered: j.RecordsFiltered,

                data: j.Data || []

            });

        }

    };

}



function renderAdminActionMenu(items) {

    var html = '<div class="dropdown admin-table-action">';

    html += '<button type="button" class="btn admin-action-toggle" data-bs-toggle="dropdown" data-bs-display="static" aria-expanded="false" title="Actions">';

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



function adminActionEditDelete(id, editFn, deleteFn) {

    editFn = editFn || 'loadData';

    deleteFn = deleteFn || 'deleteData';

    return renderAdminActionMenu([

        { label: 'Edit', icon: 'ti ti-edit', onclick: editFn + '(' + id + '); return false;' },

        { label: 'Delete', icon: 'ti ti-trash', onclick: deleteFn + '(' + id + '); return false;', className: 'text-danger' }

    ]);

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



function initAdminDataTable(selector, relativeUrl, columns, options) {

    options = options || {};

    var searchDelay = options.searchDelay || 400;

    var ajaxConfig = adminDataTableAjax(relativeUrl);

    if (options.schoolFilterSelector) {

        var schoolFilterSelector = options.schoolFilterSelector;

        ajaxConfig.data = function (payload) {

            var schoolId = $(schoolFilterSelector).val();

            if (schoolId) {

                payload.SchoolId = schoolId;

            }

        };

    }

    var table = $(selector).DataTable({

        processing: true,

        serverSide: true,

        searching: true,

        searchDelay: searchDelay,

        responsive: options.responsive !== false,

        pageLength: options.pageLength || 25,

        autoWidth: false,

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



    return table;

}



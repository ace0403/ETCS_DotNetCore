function auditFilterValue(selector) {
    var value = $(selector).val();
    return value ? String(value).trim() : '';
}

function escapeAuditText(value) {
    return $('<div/>').text(value == null ? '' : String(value)).html();
}

function parseAuditPayload(value) {
    if (value == null || String(value).trim() === '') {
        return {};
    }

    try {
        return { data: JSON.parse(value) };
    } catch (e) {
        return { text: String(value) };
    }
}

function auditFieldLabel(key) {
    var spaced = String(key).replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/_/g, ' ');
    return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

function isAuditObject(value) {
    return value != null && typeof value === 'object' && !Array.isArray(value);
}

function formatAuditScalar(value) {
    if (typeof value === 'boolean') {
        return value ? 'Yes' : 'No';
    }

    return String(value);
}

function renderAuditValue(value) {
    if (value == null || value === '') {
        return '<span class="text-muted">—</span>';
    }

    if (Array.isArray(value)) {
        if (!value.length) {
            return '<span class="text-muted">—</span>';
        }

        if (value.every(function (item) { return !isAuditObject(item); })) {
            if (value.every(function (item) { return typeof item === 'number'; })) {
                return escapeAuditText(value.join(', '));
            }

            return value.map(function (item) {
                return escapeAuditText(formatAuditScalar(item));
            }).join('<br>');
        }

        return renderAuditObjectTable(value);
    }

    if (isAuditObject(value)) {
        return Object.keys(value).map(function (key) {
            return '<div><span class="text-muted">' + escapeAuditText(auditFieldLabel(key)) + ':</span> ' + renderAuditValue(value[key]) + '</div>';
        }).join('');
    }

    return escapeAuditText(formatAuditScalar(value));
}

function renderAuditObjectTable(items) {
    var keys = [];
    items.forEach(function (item) {
        if (!isAuditObject(item)) {
            return;
        }

        Object.keys(item).forEach(function (key) {
            if (keys.indexOf(key) < 0) {
                keys.push(key);
            }
        });
    });

    var html = '<table class="table table-sm table-bordered mb-0 audit-nested-table"><thead><tr>';
    keys.forEach(function (key) {
        html += '<th>' + escapeAuditText(auditFieldLabel(key)) + '</th>';
    });
    html += '</tr></thead><tbody>';
    items.forEach(function (item) {
        html += '<tr>';
        keys.forEach(function (key) {
            html += '<td>' + renderAuditValue(isAuditObject(item) ? item[key] : item) + '</td>';
        });
        html += '</tr>';
    });
    html += '</tbody></table>';
    return html;
}

function auditValuesEqual(left, right) {
    return JSON.stringify(left) === JSON.stringify(right);
}

function renderAuditFieldTable(headers, rows) {
    var html = '<div class="table-responsive"><table class="table table-sm table-bordered audit-value-table mb-0"><thead><tr>';
    headers.forEach(function (header) {
        html += '<th>' + escapeAuditText(header) + '</th>';
    });
    html += '</tr></thead><tbody>';
    rows.forEach(function (row) {
        html += '<tr' + (row.changed ? ' class="audit-value-changed"' : '') + '>';
        row.cells.forEach(function (cell) {
            html += '<td>' + cell + '</td>';
        });
        html += '</tr>';
    });
    html += '</tbody></table></div>';
    return html;
}

function auditObjectKeys(left, right) {
    var keys = [];
    [left, right].forEach(function (source) {
        if (!isAuditObject(source)) {
            return;
        }

        Object.keys(source).forEach(function (key) {
            if (keys.indexOf(key) < 0) {
                keys.push(key);
            }
        });
    });
    return keys;
}

function renderAuditValues(oldRaw, newRaw) {
    var previous = parseAuditPayload(oldRaw);
    var next = parseAuditPayload(newRaw);
    var hasPrevious = Object.prototype.hasOwnProperty.call(previous, 'data');
    var hasNext = Object.prototype.hasOwnProperty.call(next, 'data');

    if (!hasPrevious && !hasNext) {
        var text = previous.text || next.text;
        return text
            ? '<pre class="bg-light p-2 small mb-0">' + escapeAuditText(text) + '</pre>'
            : '<p class="text-muted mb-0">No values recorded.</p>';
    }

    if (hasPrevious && hasNext && isAuditObject(previous.data) && isAuditObject(next.data)) {
        var rows = auditObjectKeys(previous.data, next.data).map(function (key) {
            var left = previous.data[key];
            var right = next.data[key];
            return {
                changed: !auditValuesEqual(left, right),
                cells: [
                    escapeAuditText(auditFieldLabel(key)),
                    renderAuditValue(left),
                    renderAuditValue(right)
                ]
            };
        });
        return renderAuditFieldTable(['Field', 'Previous', 'New'], rows);
    }

    var single = hasNext ? next.data : previous.data;
    var heading = hasNext && !hasPrevious ? 'New' : 'Previous';
    if (isAuditObject(single)) {
        return renderAuditFieldTable(['Field', heading], Object.keys(single).map(function (key) {
            return { cells: [escapeAuditText(auditFieldLabel(key)), renderAuditValue(single[key])] };
        }));
    }

    return renderAuditFieldTable(['Field', heading], [{ cells: ['Value', renderAuditValue(single)] }]);
}

var auditTable = initAdminDataTable('#audit_table', 'auditlog/getlist', [
    {
        data: 'OccurredOnUtc',
        render: function (value) {
            if (!value) return '';
            var date = new Date(value);
            return isNaN(date.getTime()) ? escapeAuditText(value) : escapeAuditText(date.toLocaleString());
        }
    },
    { data: 'SourceApp' },
    {
        data: 'ActorName',
        render: function (value, type, row) {
            var name = value || '';
            var id = row.ActorId ? ' (' + row.ActorId + ')' : '';
            return escapeAuditText((name + id).trim() || row.ActorType || '');
        }
    },
    { data: 'Action' },
    { data: 'EntityType' },
    { data: 'EntityId' },
    {
        data: 'Outcome',
        render: function (value) {
            if (value === 'Success') return '<span class="badge bg-success">Success</span>';
            if (value === 'Failure') return '<span class="badge bg-danger">Failure</span>';
            return escapeAuditText(value);
        }
    },
    {
        data: 'Message',
        className: 'audit-message',
        render: function (value, type) {
            var text = value == null ? '' : String(value);
            if (type && type !== 'display') {
                return text;
            }
            if (!text) {
                return '';
            }
            return '<span class="audit-message-text" title="' + escapeAuditText(text).replace(/"/g, '&quot;') + '">' + escapeAuditText(text) + '</span>';
        }
    },
    {
        data: 'AuditLogId',
        orderable: false,
        searchable: false,
        render: function (id) {
            return '<button type="button" class="btn btn-outline-primary btn-sm audit-detail" data-id="' + id + '">View</button>';
        }
    }
], {
    order: [[0, 'desc']],
    pageLength: 25,
    searching: false,
    extraAjaxData: function (payload) {
        payload.FromDate = auditFilterValue('#auditFrom');
        payload.ToDate = auditFilterValue('#auditTo');
        payload.SourceApp = auditFilterValue('#auditSourceApp');
        payload.Actor = auditFilterValue('#auditActor');
        payload.Action = auditFilterValue('#auditAction');
        payload.Outcome = auditFilterValue('#auditOutcome');
        payload.EntityType = auditFilterValue('#auditEntityType');
        payload.EntityId = auditFilterValue('#auditEntityId');
        payload.CorrelationId = auditFilterValue('#auditCorrelationId');
    }
});

$('#auditApply').on('click', function () {
    auditTable.draw();
});

$('#auditClear').on('click', function () {
    $('#auditFrom, #auditTo, #auditActor, #auditAction, #auditEntityType, #auditEntityId, #auditCorrelationId').val('');
    $('#auditSourceApp, #auditOutcome').val('');
    auditTable.draw();
});

$('#audit_table').on('click', '.audit-detail', function () {
    var id = $(this).data('id');
    $.get(SiteUrl + 'auditlog/detail', { id: id })
        .done(function (row) {
            var actor = (row.ActorName || '') + (row.ActorId ? ' (' + row.ActorId + ')' : '');
            var meta = [
                ['When (UTC)', row.OccurredOnUtc],
                ['App', row.SourceApp],
                ['Actor', actor || row.ActorType],
                ['Action', row.Action],
                ['Entity', (row.EntityType || '') + (row.EntityId ? ' ' + row.EntityId : '')],
                ['Outcome', row.Outcome],
                ['Message', row.Message],
                ['Correlation', row.CorrelationId],
                ['Request', ((row.HttpMethod || '') + ' ' + (row.Path || '')).trim()],
                ['IP', row.IpAddress]
            ];
            var html = '';
            for (var i = 0; i < meta.length; i++) {
                html += '<dt class="col-sm-3">' + escapeAuditText(meta[i][0]) + '</dt>';
                html += '<dd class="col-sm-9">' + escapeAuditText(meta[i][1]) + '</dd>';
            }
            $('#auditDetailMeta').html(html);
            var valuesHtml = renderAuditValues(row.OldValues, row.NewValues);
            if ($('#auditValues').length) {
                $('#auditValues').html(valuesHtml);
            } else {
                $('#auditOldValues, #auditNewValues').prev('h6').addBack().remove();
                $('#auditDetailMeta').after(valuesHtml);
            }
            bootstrap.Modal.getOrCreateInstance(document.getElementById('auditDetailModal')).show();
        });
});

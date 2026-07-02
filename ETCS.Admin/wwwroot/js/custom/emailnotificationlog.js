var logTable = initAdminDataTable('#log_table', 'emailtemplate/getnotificationlog', [
    {
        data: 'CreatedOn',
        render: function (d) {
            if (!d) return '';
            var date = new Date(d);
            return isNaN(date.getTime()) ? d : date.toLocaleString();
        }
    },
    { data: 'TemplateKey' },
    { data: 'ToEmail' },
    { data: 'Subject' },
    {
        data: 'Status',
        render: function (d) {
            if (d === 'Sent') return '<span class="badge bg-success">Sent</span>';
            if (d === 'Failed') return '<span class="badge bg-danger">Failed</span>';
            return '<span class="badge bg-warning text-dark">' + (d || 'Queued') + '</span>';
        }
    },
    {
        data: 'ErrorMessage',
        render: function (d) { return d ? $('<div/>').text(d).html() : ''; }
    }
], { order: [[0, 'desc']], paging: true, pageLength: 25 });

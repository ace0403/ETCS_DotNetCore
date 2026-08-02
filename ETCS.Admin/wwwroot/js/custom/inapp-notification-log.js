var inAppNotificationTable = initAdminDataTable('#inapp_notification_table', 'inappnotification/getlog', [
    {
        data: 'CreatedOn',
        render: function (d) {
            if (!d) return '';
            var date = new Date(d);
            return isNaN(date.getTime()) ? d : date.toLocaleString();
        }
    },
    { data: 'GuardianId' },
    {
        data: 'SchoolId',
        render: function (d) { return d == null ? '' : d; }
    },
    // { data: 'Type' },
    { data: 'Title' },
    {
        data: 'Message',
        render: function (d) { return d ? $('<div/>').text(d).html() : ''; }
    },
    {
        data: 'IsRead',
        render: function (d) {
            return d
                ? '<span class="badge bg-success">Read</span>'
                : '<span class="badge bg-warning text-dark">Unread</span>';
        }
    },
    { data: 'CreatedBy' }
], { order: [[0, 'desc']], paging: true, pageLength: 25 });

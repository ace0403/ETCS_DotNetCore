(function ($) {
    'use strict';

    function antiforgeryToken() {
        return $('#etcsAntiforgeryForm input[name="__RequestVerificationToken"]').val()
            || $('input[name="__RequestVerificationToken"]').first().val();
    }

    function setBadge(count) {
        var $badge = $('#etcsNotificationBadge');
        if (!$badge.length) {
            return;
        }

        if (count > 0) {
            $badge.text(count > 99 ? '99+' : String(count)).addClass('is-visible');
        } else {
            $badge.text('').removeClass('is-visible');
        }
    }

    function renderItems(items) {
        var $list = $('#etcsNotificationList');
        if (!$list.length) {
            return;
        }

        if (!items || !items.length) {
            $list.html('<div class="etcs-notification-dropdown-empty">No notifications yet</div>');
            return;
        }

        var html = items.map(function (item) {
            var unreadClass = item.IsRead ? '' : ' is-unread';
            var title = $('<div>').text(item.Title || '').html();
            var message = $('<div>').text(item.Message || '').html();
            var time = $('<div>').text(item.RelativeTime || '').html();
            var href = item.DetailUrl || '#';
            return ''
                + '<a href="' + href + '" class="etcs-notification-dropdown-item' + unreadClass + '">'
                + '<div class="etcs-notification-dropdown-title">' + title + '</div>'
                + '<div class="etcs-notification-dropdown-message">' + message + '</div>'
                + '<div class="etcs-notification-dropdown-time">' + time + '</div>'
                + '</a>';
        }).join('');

        $list.html(html);
    }

    function loadRecent() {
        return $.getJSON(SiteUrl + 'Notifications/Recent')
            .done(function (data) {
                setBadge(data.unreadCount || 0);
                renderItems(data.items || []);
            })
            .fail(function () {
                $('#etcsNotificationList').html(
                    '<div class="etcs-notification-dropdown-empty">Unable to load notifications</div>');
            });
    }

    function loadUnreadCount() {
        return $.getJSON(SiteUrl + 'Notifications/UnreadCount')
            .done(function (data) {
                setBadge(data.unreadCount || 0);
            });
    }

    $(function () {
        if (!$('#etcsNotificationBell').length) {
            return;
        }

        loadUnreadCount();

        $('#etcsNotificationBell').on('show.bs.dropdown', function () {
            loadRecent();
        });

        $('#etcsMarkAllNotificationsRead').on('click', function (e) {
            e.preventDefault();
            e.stopPropagation();

            $.ajax({
                url: SiteUrl + 'Notifications/MarkAllRead',
                type: 'POST',
                data: { __RequestVerificationToken: antiforgeryToken() }
            }).done(function () {
                setBadge(0);
                loadRecent();
            });
        });
    });
})(jQuery);

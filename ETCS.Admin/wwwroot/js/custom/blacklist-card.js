(function () {
    var moduleKey = window.adminModuleKey || 'BlacklistCard';
    var lastLookupCard = '';

    function escapeHtml(value) {
        return $('<div>').text(value == null ? '' : String(value)).html();
    }

    function formatBalance(value) {
        var amount = Number(value);
        if (!isFinite(amount)) {
            return '0.00';
        }
        return amount.toFixed(2);
    }

    function statusBadge(status) {
        var text = status || '';
        var css = text.toLowerCase() === 'active' ? 'bg-success' : 'bg-danger';
        return '<span class="badge ' + css + '">' + escapeHtml(text) + '</span>';
    }

    function canEdit() {
        return adminCan(moduleKey, 'edit');
    }

    function setBusy($button, busy, idleText) {
        if (!$button.length) {
            return;
        }
        $button.prop('disabled', busy);
        $button.text(busy ? 'Please wait...' : idleText);
    }

    function renderRows(items) {
        var $tbody = $('#blacklistResultsTable tbody');
        $tbody.empty();

        if (!items || !items.length) {
            $('#blacklistResultsCard').addClass('d-none');
            return;
        }

        items.forEach(function (row) {
            var transferCell = '';
            if (row.CanTransfer && canEdit()) {
                transferCell = '<button type="button" class="btn btn-sm btn-outline-primary btn-transfer-balance"'
                    + ' data-card-sn="' + escapeHtml(row.CardSn) + '"'
                    + ' data-customer-id="' + escapeHtml(row.CustomerId) + '">Transfer Balance</button>';
            }

            var html = '<tr>'
                + '<td>' + escapeHtml(row.CardSn) + '</td>'
                + '<td>' + escapeHtml(row.CustomerId) + '</td>'
                + '<td>' + escapeHtml(row.LastUsed) + '</td>'
                + '<td class="text-end">' + escapeHtml(formatBalance(row.Balance)) + '</td>'
                + '<td>' + statusBadge(row.Status) + '</td>'
                + '<td>' + escapeHtml(row.BalanceTransfer) + '</td>'
                + '<td class="text-center">' + transferCell + '</td>'
                + '</tr>';
            $tbody.append(html);
        });

        $('#blacklistResultsCard').removeClass('d-none');
    }

    function loadLinkedCards(customerId, $button, idleText) {
        var card = $.trim(customerId || '');
        if (!card) {
            toastMsg('Student card number required.', false);
            return;
        }

        lastLookupCard = card;
        setBusy($button, true, idleText);
        $.post(SiteUrl + 'blacklistcard/getlist', { customerId: card })
            .done(function (result) {
                if (!result || !result.Success) {
                    renderRows([]);
                    toastMsg((result && result.Message) || 'No data available.', false);
                    return;
                }
                renderRows(result.Items || []);
            })
            .fail(function () {
                renderRows([]);
                toastMsg('Unable to load card information. Please try again.', false);
            })
            .always(function () {
                setBusy($button, false, idleText);
            });
    }

    $('#frmBlacklist').on('submit', function (e) {
        e.preventDefault();
        var $form = $(this);
        if (typeof $form[0].reportValidity === 'function' && !$form[0].reportValidity()) {
            return;
        }

        var customerId = $.trim($('#blacklistCustomerId').val());
        var $button = $('#btnBlacklist');
        setBusy($button, true, 'Blacklist Card');
        $.post(SiteUrl + 'blacklistcard/blacklist', { customerId: customerId })
            .done(function (result) {
                toastMsg(result.Message, result.Success);
                if (result.Success) {
                    $('#viewCustomerId').val(customerId);
                    loadLinkedCards(customerId, $('#btnViewBlacklist'), 'View Blacklist Card Info');
                }
            })
            .fail(function () {
                toastMsg('Unable to blacklist the card. Please try again.', false);
            })
            .always(function () {
                setBusy($button, false, 'Blacklist Card');
            });
    });

    $('#frmViewBlacklist').on('submit', function (e) {
        e.preventDefault();
        var $form = $(this);
        if (typeof $form[0].reportValidity === 'function' && !$form[0].reportValidity()) {
            return;
        }

        loadLinkedCards($('#viewCustomerId').val(), $('#btnViewBlacklist'), 'View Blacklist Card Info');
    });

    $('#blacklistResultsTable').on('click', '.btn-transfer-balance', function () {
        var $button = $(this);
        var cardSn = $button.attr('data-card-sn');
        var customerId = $button.attr('data-customer-id') || lastLookupCard || $('#viewCustomerId').val();

        showConfirmation(
            'Transfer the full prepaid balance from this blocked card to the newly activated card?',
            'Transfer'
        ).then(function (result) {
            if (!result.isConfirmed) {
                return;
            }

            $button.prop('disabled', true);
            $.post(SiteUrl + 'blacklistcard/transfer', { customerId: customerId, cardSn: cardSn })
                .done(function (response) {
                    toastMsg(response.Message, response.Success);
                    if (response.Success) {
                        loadLinkedCards(customerId, $('#btnViewBlacklist'), 'View Blacklist Card Info');
                    }
                })
                .fail(function () {
                    toastMsg('Unable to transfer the balance. Please try again.', false);
                })
                .always(function () {
                    $button.prop('disabled', false);
                });
        });
    });
})();

(function () {
    var currency = 'AED';

    function selectedOption() {
        return $('#StudentId option:selected');
    }

    function selectedMinimum() {
        var value = parseFloat(selectedOption().data('minimum'));
        return isNaN(value) ? 0 : value;
    }

    function selectedBalance() {
        var value = parseFloat(selectedOption().data('balance'));
        return isNaN(value) ? 0 : value;
    }

    function parseAmount() {
        var raw = ($('#Amount').val() || '').toString().trim();
        if (!raw) return null;
        var amount = parseFloat(raw);
        return isNaN(amount) ? null : amount;
    }

    function meetsMinimum(amount, minimum) {
        return amount > 0 && (minimum <= 0 || amount >= minimum);
    }

    function syncActiveChip() {
        var amount = parseAmount();
        $('.topup-chip').removeClass('is-active');
        if (amount === null) return;

        $('.topup-chip').each(function () {
            var chipAmount = parseFloat($(this).data('amount'));
            if (!isNaN(chipAmount) && Math.abs(chipAmount - amount) < 0.001) {
                $(this).addClass('is-active');
            }
        });
    }

    function updateHints() {
        var studentId = $('#StudentId').val();
        var $panel = $('#balancePanel');
        var $minimum = $('#minimumHint');
        var $amountError = $('#amountError');

        $amountError.attr('hidden', 'hidden').text('');

        if (!studentId) {
            $panel.attr('hidden', 'hidden');
            $minimum.attr('hidden', 'hidden').text('');
            $('#selectedChildName').text('');
            $('#balanceAmount').text(currency + ' 0.00');
            updateChips(0);
            syncActiveChip();
            return;
        }

        var balance = selectedBalance();
        var minimum = selectedMinimum();
        var childName = selectedOption().text().trim();

        $panel.removeAttr('hidden');
        $('#selectedChildName').text(childName);
        $('#balanceAmount').text(currency + ' ' + balance.toFixed(2));

        if (minimum > 0) {
            $minimum
                .removeAttr('hidden')
                .html('<i class="ti ti-info-circle"></i><span>Minimum top-up: ' + currency + ' ' + minimum.toFixed(2) + '</span>');
            $('#Amount').attr('placeholder', 'Min ' + minimum.toFixed(2));
        } else {
            $minimum.attr('hidden', 'hidden').empty();
            $('#Amount').attr('placeholder', '0.00');
        }

        updateChips(minimum);
        validateAmountLive(false);
        syncActiveChip();
    }

    function updateChips(minimum) {
        $('.topup-chip').each(function () {
            var amount = parseFloat($(this).data('amount'));
            var enabled = minimum <= 0 || amount >= minimum;
            $(this).prop('disabled', !enabled);
            if (!enabled) {
                $(this).removeClass('is-active');
            }
        });
    }

    function validateAmountLive(requireValue) {
        var amount = parseAmount();
        var minimum = selectedMinimum();
        var $amountError = $('#amountError');

        if (amount === null) {
            if (requireValue) {
                $amountError.removeAttr('hidden').text('Please enter a valid amount.');
                return false;
            }

            $amountError.attr('hidden', 'hidden').text('');
            return true;
        }

        if (!meetsMinimum(amount, minimum)) {
            var message = minimum > 0
                ? 'Minimum top-up amount is ' + currency + ' ' + minimum.toFixed(2) + '.'
                : 'Please enter a valid amount.';
            $amountError.removeAttr('hidden').text(message);
            return false;
        }

        $amountError.attr('hidden', 'hidden').text('');
        return true;
    }

    function readJsonFlag(result, pascalKey, camelKey) {
        if (!result) return false;
        if (result[pascalKey] === true) return true;
        if (result[camelKey] === true) return true;
        return false;
    }

    function readJsonValue(result, pascalKey, camelKey) {
        if (!result) return '';
        return result[pascalKey] || result[camelKey] || '';
    }

    function setConfirmLoading(isLoading) {
        var $btn = $('#btnTopupConfirm');
        if (isLoading) {
            $btn.prop('disabled', true).data('loading', true)
                .html('<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span><span>Redirecting...</span>');
            return;
        }

        $btn.prop('disabled', false).data('loading', false)
            .html('<i class="ti ti-credit-card"></i><span>Confirm Top-up</span>');
    }

    function requestTopup() {
        var studentId = $('#StudentId').val();
        var amount = parseAmount();
        var minimum = selectedMinimum();

        if (!studentId) {
            toastMsg('Please select a child.', false);
            return;
        }

        if (amount === null || amount <= 0) {
            toastMsg('Please enter a valid amount.', false);
            return;
        }

        if (!meetsMinimum(amount, minimum)) {
            toastMsg('Minimum top-up amount is ' + currency + ' ' + minimum.toFixed(2) + '.', false);
            return;
        }

        var token = $('#frmTopup input[name="__RequestVerificationToken"]').val();
        setConfirmLoading(true);

        $.ajax({
            url: SiteUrl + 'topup/requesttopup',
            type: 'POST',
            data: {
                StudentId: studentId,
                Amount: amount,
                __RequestVerificationToken: token
            },
            success: function (result) {
                var isSuccess = readJsonFlag(result, 'Success', 'success');
                var redirectUrl = readJsonValue(result, 'RedirectUrl', 'redirectUrl');
                var message = readJsonValue(result, 'Message', 'message');
                var minAmount = result.MinimumTopupAmount || result.minimumTopupAmount;

                if (isSuccess && redirectUrl) {
                    window.location.href = redirectUrl;
                    return;
                }

                setConfirmLoading(false);
                toastMsg(message || 'Unable to start top-up.', false);
                if (minAmount && minAmount > 0) {
                    selectedOption().data('minimum', minAmount);
                    updateHints();
                }
            },
            error: function () {
                setConfirmLoading(false);
                toastMsg('Unable to start top-up. Please try again.', false);
            }
        });
    }

    $(function () {
        var currencyEl = $('.topup-currency').first();
        if (currencyEl.length) {
            currency = currencyEl.text().trim() || 'AED';
        }

        $('#StudentId').on('change', updateHints);
        $('#Amount').on('input', function () {
            validateAmountLive(false);
            syncActiveChip();
        });

        $('.topup-chip').on('click', function () {
            if ($(this).prop('disabled')) return;
            $('#Amount').val($(this).data('amount'));
            validateAmountLive(false);
            syncActiveChip();
        });

        $('#btnTopupConfirm').on('click', async function () {
            var $btn = $(this);
            if ($btn.prop('disabled') || $btn.data('loading')) {
                return;
            }

            if (!$('#StudentId').val()) {
                toastMsg('Please select a child.', false);
                return;
            }

            if (!validateAmountLive(true)) {
                $('#Amount').trigger('focus');
                return;
            }

            var amount = parseAmount();
            var confirmation = await showConfirmation(
                'Top up ' + currency + ' ' + amount.toFixed(2) + '? You will be redirected to the payment page.',
                'YES'
            );

            if (!confirmation.isConfirmed) {
                return;
            }

            requestTopup();
        });

        $('#topupModal').on('shown.bs.modal', function () {
            updateHints();
        });

        $('#topupModal').on('hidden.bs.modal', function () {
            $('#Amount').val('');
            $('#amountError').attr('hidden', 'hidden').text('');
            $('.topup-chip').removeClass('is-active');
            setConfirmLoading(false);
            updateHints();
        });

        updateHints();
    });
})();

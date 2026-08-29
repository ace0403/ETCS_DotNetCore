(function ($) {
    'use strict';

    var otpExpiresAt = 0;
    var countdownTimer = null;

    function toastMsg(message, isSuccess) {
        if (typeof toastr === 'undefined') {
            window.alert(message);
            return;
        }
        if (isSuccess) {
            toastr.success(message);
        } else {
            toastr.error(message);
        }
    }

    function antiforgeryToken() {
        return $('#etcsAntiforgeryForm input[name="__RequestVerificationToken"]').val() || '';
    }

    function page() {
        return $('#accountSettingsPage');
    }

    function setStep(step) {
        $('.delete-account-step').addClass('d-none');
        $('.delete-account-step[data-step="' + step + '"]').removeClass('d-none');
    }

    function clearCountdown() {
        if (countdownTimer) {
            window.clearInterval(countdownTimer);
            countdownTimer = null;
        }
        $('#deleteOtpCountdown').text('');
        $('#btnResendDeleteOtp').prop('disabled', false);
    }

    function startCountdown(expiresInSeconds) {
        clearCountdown();
        var seconds = parseInt(expiresInSeconds, 10);
        if (!seconds || seconds < 1) {
            seconds = 300;
        }
        otpExpiresAt = Date.now() + (seconds * 1000);
        $('#btnResendDeleteOtp').prop('disabled', true);

        function tick() {
            var remaining = Math.max(0, Math.ceil((otpExpiresAt - Date.now()) / 1000));
            var mins = Math.floor(remaining / 60);
            var secs = remaining % 60;
            $('#deleteOtpCountdown').text(
                remaining > 0
                    ? ('Code expires in ' + mins + ':' + (secs < 10 ? '0' : '') + secs)
                    : 'Code expired. Please resend.'
            );
            if (remaining <= 0) {
                clearCountdown();
                $('#btnResendDeleteOtp').prop('disabled', false);
            }
        }

        tick();
        countdownTimer = window.setInterval(tick, 1000);
    }

    function setButtonLoading($btn, isLoading, loadingHtml, idleHtml) {
        if (isLoading) {
            $btn.prop('disabled', true).data('loading', true).html(loadingHtml);
            return;
        }
        $btn.prop('disabled', false).data('loading', false).html(idleHtml);
    }

    function sendOtp() {
        var $btn = $('#btnSendDeleteOtp');
        var $resend = $('#btnResendDeleteOtp');
        var isResend = $(document.activeElement).is('#btnResendDeleteOtp');
        var $active = isResend ? $resend : $btn;

        setButtonLoading(
            $active,
            true,
            '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span><span> Sending...</span>',
            isResend
                ? 'Resend code'
                : '<span>Send verification code</span><i class="ti ti-mail-forward ms-1"></i>'
        );

        $.ajax({
            url: page().data('send-otp-url'),
            type: 'POST',
            data: {
                __RequestVerificationToken: antiforgeryToken()
            },
            success: function (result) {
                setButtonLoading(
                    $active,
                    false,
                    '',
                    isResend
                        ? 'Resend code'
                        : '<span>Send verification code</span><i class="ti ti-mail-forward ms-1"></i>'
                );
                if (!result || !result.success) {
                    toastMsg((result && result.message) || 'Unable to send verification code.', false);
                    return;
                }

                if (result.maskedEmail) {
                    $('#deleteOtpEmailLabel').text(result.maskedEmail);
                }
                $('#DeleteOtpCode').val('');
                setStep(2);
                startCountdown(result.expiresInSeconds);
                toastMsg(result.message || 'Verification code sent.', true);
            },
            error: function () {
                setButtonLoading(
                    $active,
                    false,
                    '',
                    isResend
                        ? 'Resend code'
                        : '<span>Send verification code</span><i class="ti ti-mail-forward ms-1"></i>'
                );
                toastMsg('Unable to send verification code. Please try again.', false);
            }
        });
    }

    function confirmDelete() {
        var otp = ($('#DeleteOtpCode').val() || '').trim();
        if (!/^\d{6}$/.test(otp)) {
            toastMsg('Enter the 6-digit verification code.', false);
            return;
        }

        if (typeof showConfirmation !== 'function') {
            if (!window.confirm('Delete your account permanently? This cannot be undone.')) {
                return;
            }
            performDelete(otp);
            return;
        }

        // Use showConfirmation (handles SweetAlert2 v9 value vs isConfirmed).
        showConfirmation(
            'Your parent account will be deleted. This cannot be undone from the portal.',
            'Yes, delete',
            { title: 'Delete account?', icon: 'warning' }
        ).then(function (result) {
            if (result && result.isConfirmed) {
                performDelete(otp);
            }
        });
    }

    function performDelete(otp) {
        var $btn = $('#btnConfirmDeleteAccount');
        setButtonLoading(
            $btn,
            true,
            '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span><span> Deleting...</span>',
            '<span>Confirm delete</span><i class="ti ti-trash ms-1"></i>'
        );

        $.ajax({
            url: page().data('delete-url'),
            type: 'POST',
            data: {
                otp: otp,
                __RequestVerificationToken: antiforgeryToken()
            },
            success: function (result) {
                if (!result || !result.success) {
                    setButtonLoading(
                        $btn,
                        false,
                        '',
                        '<span>Confirm delete</span><i class="ti ti-trash ms-1"></i>'
                    );
                    toastMsg((result && result.message) || 'Unable to delete account.', false);
                    return;
                }

                clearCountdown();
                toastMsg(result.message || 'Account deleted.', true);
                window.setTimeout(function () {
                    window.location.href = result.redirectUrl || (SiteUrl + 'home/index?msg=account-deleted');
                }, 600);
            },
            error: function () {
                setButtonLoading(
                    $btn,
                    false,
                    '',
                    '<span>Confirm delete</span><i class="ti ti-trash ms-1"></i>'
                );
                toastMsg('Unable to delete account. Please try again.', false);
            }
        });
    }

    $(function () {
        if (!page().length) {
            return;
        }

        $('#btnSendDeleteOtp, #btnResendDeleteOtp').on('click', sendOtp);
        $('#btnConfirmDeleteAccount').on('click', confirmDelete);
        $('#btnCancelDeleteOtp').on('click', function () {
            clearCountdown();
            $('#DeleteOtpCode').val('');
            setStep(1);
        });
    });
})(jQuery);

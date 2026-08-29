(function ($) {
    'use strict';

    var otpExpiresAt = 0;
    var countdownTimer = null;
    var emailLocked = false;

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
        return $('#registerForm input[name="__RequestVerificationToken"]').val() || '';
    }

    function setStep(step) {
        $('.register-step-panel').addClass('d-none');
        $('.register-step-panel[data-step="' + step + '"]').removeClass('d-none');
        $('[data-step-indicator]').removeClass('is-active is-done');
        $('[data-step-indicator]').each(function () {
            var n = parseInt($(this).attr('data-step-indicator'), 10);
            if (n < step) {
                $(this).addClass('is-done');
            } else if (n === step) {
                $(this).addClass('is-active');
            }
        });
    }

    function setEmailLocked(locked) {
        emailLocked = locked;
        $('#Email').prop('readonly', locked);
    }

    function clearCountdown() {
        if (countdownTimer) {
            window.clearInterval(countdownTimer);
            countdownTimer = null;
        }
        $('#otpCountdown').text('');
        $('#btnResendOtp').prop('disabled', false);
    }

    function startCountdown(expiresInSeconds) {
        clearCountdown();
        var seconds = parseInt(expiresInSeconds, 10);
        if (!seconds || seconds < 1) {
            seconds = 300;
        }
        otpExpiresAt = Date.now() + (seconds * 1000);
        $('#btnResendOtp').prop('disabled', true);

        function tick() {
            var remaining = Math.max(0, Math.ceil((otpExpiresAt - Date.now()) / 1000));
            var mins = Math.floor(remaining / 60);
            var secs = remaining % 60;
            $('#otpCountdown').text(
                remaining > 0
                    ? ('Code expires in ' + mins + ':' + (secs < 10 ? '0' : '') + secs)
                    : 'Code expired. Please resend.'
            );
            if (remaining <= 0) {
                clearCountdown();
                $('#btnResendOtp').prop('disabled', false);
            }
        }

        tick();
        countdownTimer = window.setInterval(tick, 1000);
    }

    function validateDetailsForm() {
        var $form = $('#registerForm');
        // Temporarily show details so jquery.validate does not ignore :hidden fields.
        var $details = $('#registerStepDetails');
        var wasHidden = $details.hasClass('d-none');
        if (wasHidden) {
            $details.removeClass('d-none');
        }
        var isValid = true;
        if ($form.length && $form.valid) {
            isValid = $form.valid();
        }
        if (wasHidden) {
            $details.addClass('d-none');
        }
        return isValid;
    }

    function setButtonLoading($btn, isLoading, loadingHtml, idleHtml) {
        if (isLoading) {
            $btn.prop('disabled', true).data('loading', true).html(loadingHtml);
            return;
        }
        $btn.prop('disabled', false).data('loading', false).html(idleHtml);
    }

    function submitRegistration() {
        // Native submit bypasses jquery :hidden ignore rules; field values remain in the DOM.
        var form = document.getElementById('registerForm');
        if (!form) {
            return;
        }
        // Ensure profile fields are not display:none during submit (some browsers skip them).
        $('#registerStepDetails').removeClass('d-none').addClass('visually-hidden');
        form.submit();
    }

    function sendOtp() {
        if (!validateDetailsForm()) {
            setStep(1);
            return;
        }

        var email = ($('#Email').val() || '').trim();
        if (!email) {
            toastMsg('Email is required.', false);
            return;
        }

        var $btn = $('#btnSendOtp');
        setButtonLoading(
            $btn,
            true,
            '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span><span> Sending...</span>',
            '<span>Send verification code</span><i class="ti ti-mail-forward ms-1"></i>'
        );

        $.ajax({
            url: $('#registerForm').data('send-otp-url'),
            type: 'POST',
            data: {
                email: email,
                __RequestVerificationToken: antiforgeryToken()
            },
            success: function (result) {
                setButtonLoading(
                    $btn,
                    false,
                    '',
                    '<span>Send verification code</span><i class="ti ti-mail-forward ms-1"></i>'
                );
                if (!result || !result.success) {
                    toastMsg((result && result.message) || 'Unable to send verification code.', false);
                    return;
                }

                setEmailLocked(true);
                $('#otpEmailLabel').text(email);
                $('#OtpCode').val('');
                $('#VerificationToken').val('');
                setStep(2);
                startCountdown(result.expiresInSeconds);
                toastMsg(result.message || 'Verification code sent.', true);
            },
            error: function () {
                setButtonLoading(
                    $btn,
                    false,
                    '',
                    '<span>Send verification code</span><i class="ti ti-mail-forward ms-1"></i>'
                );
                toastMsg('Unable to send verification code. Please try again.', false);
            }
        });
    }

    function verifyOtp() {
        var email = ($('#Email').val() || '').trim();
        var otp = ($('#OtpCode').val() || '').trim();

        if (!email) {
            toastMsg('Email is required.', false);
            return;
        }
        if (!/^\d{6}$/.test(otp)) {
            toastMsg('Enter the 6-digit verification code.', false);
            return;
        }

        var $btn = $('#btnVerifyOtp');
        setButtonLoading(
            $btn,
            true,
            '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span><span> Creating account...</span>',
            '<span>Verify &amp; create account</span><i class="ti ti-check ms-1"></i>'
        );

        $.ajax({
            url: $('#registerForm').data('verify-otp-url'),
            type: 'POST',
            data: {
                email: email,
                otp: otp,
                __RequestVerificationToken: antiforgeryToken()
            },
            success: function (result) {
                if (!result || !result.success || !result.verificationToken) {
                    setButtonLoading(
                        $btn,
                        false,
                        '',
                        '<span>Verify &amp; create account</span><i class="ti ti-check ms-1"></i>'
                    );
                    toastMsg((result && result.message) || 'Invalid verification code.', false);
                    return;
                }

                $('#VerificationToken').val(result.verificationToken);
                clearCountdown();
                toastMsg(result.message || 'Email verified. Creating your account...', true);
                submitRegistration();
            },
            error: function () {
                setButtonLoading(
                    $btn,
                    false,
                    '',
                    '<span>Verify &amp; create account</span><i class="ti ti-check ms-1"></i>'
                );
                toastMsg('Unable to verify code. Please try again.', false);
            }
        });
    }

    $(function () {
        if (!$('#registerForm').length) {
            return;
        }

        if (typeof initAdminFormValidation === 'function') {
            initAdminFormValidation('#registerForm');
        }

        // Server validation errors: return to details (token kept if still present).
        if ($('.validation-summary-errors').length || $('.field-validation-error').filter(function () {
            return $(this).text().trim().length > 0;
        }).length) {
            setEmailLocked(!!($('#VerificationToken').val() || '').trim());
            setStep(1);
        } else {
            setStep(1);
        }

        $('#btnSendOtp').on('click', sendOtp);
        $('#btnResendOtp').on('click', sendOtp);
        $('#btnVerifyOtp').on('click', verifyOtp);
        $('#btnBackToDetails').on('click', function () {
            clearCountdown();
            setEmailLocked(false);
            $('#VerificationToken').val('');
            $('#registerStepDetails').removeClass('visually-hidden');
            setStep(1);
        });

        $('#OtpCode').on('input', function () {
            this.value = (this.value || '').replace(/\D/g, '').slice(0, 6);
        });
    });
})(jQuery);

using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Models;
using ETCS.PaymentGateway.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ETCS.PaymentGateway.Repositories;

public sealed class ComtrustPaymentGatewayRepository : IPaymentGatewayRepository
{
    private const string RequiredAccept = "text/xml-standard-api";

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly PaymentGatewayOptions _options;
    private readonly ILogger<ComtrustPaymentGatewayRepository> _logger;

    public ComtrustPaymentGatewayRepository(
        HttpClient httpClient,
        IOptions<PaymentGatewayOptions> options,
        ILogger<ComtrustPaymentGatewayRepository> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PaymentSessionCreateResult> CreateTopupSessionAsync(
        StudentTopupPaymentRequest request,
        string orderId,
        CancellationToken cancellationToken,
        string? returnUrl = null)
    {
        return await CreateSessionInternalAsync(
            orderId,
            request.Amount,
            request.StudentId,
            "student topup",
            cancellationToken,
            returnUrl);
    }

    public async Task<PaymentSessionCreateResult> CreateOrderSessionAsync(
        OrderPaymentSessionRequest request,
        CancellationToken cancellationToken)
    {
        return await CreateSessionInternalAsync(
            request.OrderId,
            request.Total,
            request.StudentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "meal order",
            cancellationToken);
    }

    public async Task<PaymentCaptureResult> CapturePaymentAsync(
        PaymentCaptureRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return new PaymentCaptureResult
            {
                IsSuccess = false,
                Message = "Payment gateway base URL is not configured.",
                TransactionId = request.TransactionId
            };
        }

        var finalization = new ComtrustFinalizationRequest
        {
            Finalization = new ComtrustFinalizationPayload
            {
                TransactionId = request.TransactionId,
                Customer = _options.CustomerName,
                UserName = _options.UserName,
                Password = _options.Password
            }
        };

        var captureTimeoutSeconds = _options.CaptureTimeoutSeconds > 0
            ? _options.CaptureTimeoutSeconds
            : 90;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(captureTimeoutSeconds));

            using var httpRequest = CreateJsonPostRequest(finalization);
            using var httpResponse = await _httpClient.SendAsync(httpRequest, timeoutCts.Token);

            var rawResponse = await ReadResponseBodyAsync(httpResponse, timeoutCts.Token);
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                _logger.LogWarning(
                    "Payment gateway returned an empty response for transaction {TransactionId}. Status={StatusCode}",
                    request.TransactionId,
                    (int)httpResponse.StatusCode);
            }

            ComtrustRegistrationResponse? parsed = null;
            if (!string.IsNullOrWhiteSpace(rawResponse))
            {
                parsed = JsonSerializer.Deserialize<ComtrustRegistrationResponse>(
                    rawResponse,
                    ResponseJsonOptions);
            }

            var transactionRef = parsed?.Transaction?.TransactionId ?? request.TransactionId;
            var responseDescription = parsed?.Transaction?.ResponseDescription ?? string.Empty;
            var responseClass = parsed?.Transaction?.ResponseClassDescription ?? string.Empty;
            var normalizedClass = responseClass.Trim().ToLowerInvariant();

            var isSuccess = normalizedClass is "success" or "closed";
            var isPending = normalizedClass == "pending";
            if (isPending)
            {
                isSuccess = true;
            }

            if (!httpResponse.IsSuccessStatusCode && !isSuccess && !isPending)
            {
                _logger.LogWarning(
                    "Payment capture failed for transaction {TransactionId}. Status={StatusCode}; Class={ResponseClass}; Description={Description}",
                    request.TransactionId,
                    (int)httpResponse.StatusCode,
                    responseClass,
                    responseDescription);
            }

            return new PaymentCaptureResult
            {
                IsSuccess = isSuccess,
                IsPending = isPending,
                Message = string.IsNullOrWhiteSpace(responseDescription)
                    ? (isSuccess ? "Payment captured." : "Payment capture failed.")
                    : responseDescription,
                TransactionId = transactionRef,
                Status = responseClass
            };
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(
                ex,
                "Timeout/cancel while capturing payment for transaction {TransactionId}. CaptureTimeout={TimeoutSeconds}s; RequestCancelled={RequestCancelled}",
                request.TransactionId,
                captureTimeoutSeconds,
                cancellationToken.IsCancellationRequested);
            return new PaymentCaptureResult
            {
                IsSuccess = false,
                Message = cancellationToken.IsCancellationRequested
                    ? "Payment capture request was cancelled."
                    : $"Payment gateway timeout after {captureTimeoutSeconds} seconds.",
                TransactionId = request.TransactionId
            };
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Transport error while capturing payment for transaction {TransactionId}.", request.TransactionId);
            return new PaymentCaptureResult
            {
                IsSuccess = false,
                Message = "Payment gateway connection was interrupted. Please retry.",
                TransactionId = request.TransactionId
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while capturing payment for transaction {TransactionId}.", request.TransactionId);
            return new PaymentCaptureResult
            {
                IsSuccess = false,
                Message = "Unable to reach payment gateway. Please retry.",
                TransactionId = request.TransactionId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing payment status for transaction {TransactionId}.", request.TransactionId);
            return new PaymentCaptureResult
            {
                IsSuccess = false,
                Message = "Unable to capture payment status at the moment.",
                TransactionId = request.TransactionId
            };
        }
    }

    public async Task<GenerateTokenResult> GenerateTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return new GenerateTokenResult
            {
                IsSuccess = false,
                Message = "Payment gateway base URL is not configured."
            };
        }

        var tokenRequest = new ComtrustGenerateTokenRequest
        {
            GenerateToken = new ComtrustGenerateTokenPayload
            {
                UserName = _options.UserName,
                Password = _options.Password
            }
        };

        try
        {
            using var httpRequest = CreateJsonPostRequest(tokenRequest);
            using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var rawResponse = await ReadResponseBodyAsync(httpResponse, cancellationToken);
            var parsed = DeserializeTransactionResponse(rawResponse);
            var token = parsed?.Transaction?.AuthenticationToken ?? string.Empty;
            var isSuccess = IsGatewaySuccess(parsed?.Transaction) && !string.IsNullOrWhiteSpace(token);

            return new GenerateTokenResult
            {
                IsSuccess = isSuccess,
                Message = parsed?.Transaction?.ResponseDescription
                           ?? (isSuccess ? "Token generated." : "Token generation failed."),
                AuthenticationToken = token
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Comtrust authentication token.");
            return new GenerateTokenResult
            {
                IsSuccess = false,
                Message = "Unable to generate authentication token at the moment."
            };
        }
    }

    public Task<NativePaymentSessionResult> CreateNativeTopupSessionAsync(
        StudentTopupPaymentRequest request,
        string orderId,
        CancellationToken cancellationToken,
        string? returnUrl = null) =>
        CreateNativeSessionInternalAsync(
            orderId,
            request.Amount,
            request.StudentId,
            "student topup",
            cancellationToken,
            returnUrl);

    public Task<NativePaymentSessionResult> CreateNativeOrderSessionAsync(
        OrderPaymentSessionRequest request,
        CancellationToken cancellationToken) =>
        CreateNativeSessionInternalAsync(
            request.OrderId,
            request.Total,
            request.StudentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "meal order",
            cancellationToken,
            request.ReturnUrl);

    public async Task<NativeWalletSessionResult> CreateWalletRegistrationAsync(
        WalletRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return FailWallet(request.OrderId, "Payment gateway base URL is not configured.");
        }

        var tokenResult = await GenerateTokenAsync(cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            return FailWallet(request.OrderId, tokenResult.Message);
        }

        var walletName = request.WalletName;
        if (string.IsNullOrWhiteSpace(walletName))
        {
            walletName = request.PaymentMethod.Equals("SamsungPay", StringComparison.OrdinalIgnoreCase)
                ? "Samsung Pay"
                : "Apple Pay";
        }

        var walletRequest = new ComtrustWalletRegistrationRequest
        {
            WalletRegistration = new ComtrustWalletRegistrationPayload
            {
                OrderID = request.OrderId,
                OrderName = _options.OrderName,
                OrderInfo = string.IsNullOrWhiteSpace(request.OrderInfo) ? request.OrderId : request.OrderInfo,
                Channel = ResolveMobileChannel(),
                Amount = request.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                Currency = _options.Currency,
                TransactionHint = _options.WalletTransactionHint,
                Customer = _options.CustomerName,
                UserName = _options.UserName,
                Password = _options.Password,
                WalletName = walletName
            }
        };

        try
        {
            using var httpRequest = CreateJsonPostRequest(walletRequest);
            using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var rawResponse = await ReadResponseBodyAsync(httpResponse, cancellationToken);
            var parsed = DeserializeTransactionResponse(rawResponse);
            var transaction = parsed?.Transaction;
            var isSuccess = IsGatewaySuccess(transaction)
                            && !string.IsNullOrWhiteSpace(transaction?.TransactionId)
                            && !string.IsNullOrWhiteSpace(transaction?.SessionId);

            if (!isSuccess)
            {
                _logger.LogWarning(
                    "Wallet registration failed for OrderId={OrderId}. Description={Description}",
                    request.OrderId,
                    transaction?.ResponseDescription);
            }

            var callbackUrl = ResolveNativeReturnUrl(request.OrderId, request.ReturnUrl);

            return new NativeWalletSessionResult
            {
                IsSuccess = isSuccess,
                Message = transaction?.ResponseDescription
                           ?? (isSuccess ? "Wallet session created." : "Wallet registration failed."),
                OrderId = request.OrderId,
                TransactionId = transaction?.TransactionId ?? string.Empty,
                AuthenticationToken = tokenResult.AuthenticationToken,
                SessionId = transaction?.SessionId ?? string.Empty,
                MerchantUserName = _options.UserName,
                CustomerName = _options.CustomerName,
                BaseUrl = ToNativeSdkBaseUrl(_options.BaseUrl),
                CallbackUrl = callbackUrl,
                Amount = request.Amount,
                Currency = _options.Currency,
                PaymentMethod = request.PaymentMethod,
                MerchantIdentifier = _options.ApplePayMerchantIdentifier,
                SamsungPayMerchantId = _options.SamsungPayMerchantId,
                SamsungPayServiceId = _options.SamsungPayServiceId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating wallet registration for OrderId={OrderId}.", request.OrderId);
            return FailWallet(request.OrderId, "Unable to create wallet session at the moment.");
        }
    }

    private async Task<NativePaymentSessionResult> CreateNativeSessionInternalAsync(
        string orderId,
        decimal amount,
        string orderInfo,
        string context,
        CancellationToken cancellationToken,
        string? returnUrlOverride = null)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return FailNative(orderId, amount, "Payment gateway base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.Password))
        {
            return FailNative(orderId, amount, "Payment gateway password is not configured.");
        }

        var returnUrl = ResolveNativeReturnUrl(orderId, returnUrlOverride);

        var tokenResult = await GenerateTokenAsync(cancellationToken);
        if (!tokenResult.IsSuccess || string.IsNullOrWhiteSpace(tokenResult.AuthenticationToken))
        {
            return FailNative(
                orderId,
                amount,
                string.IsNullOrWhiteSpace(tokenResult.Message)
                    ? "Unable to generate EPG authentication token."
                    : tokenResult.Message);
        }

        // Native SDK flow matches EPG merchant sample: GenerateToken, then Registration with
        // AuthenticationToken + Store/Terminal (MobileSDK), not Password-only web registration.
        var registration = new ComtrustRegistrationRequest
        {
            Registration = new ComtrustRegistrationPayload
            {
                Customer = _options.CustomerName,
                Channel = ResolveMobileChannel(),
                Amount = amount,
                Currency = _options.Currency,
                OrderID = orderId,
                OrderName = _options.OrderName,
                OrderInfo = orderInfo,
                TransactionHint = _options.TransactionHint,
                UserName = _options.UserName,
                AuthenticationToken = tokenResult.AuthenticationToken,
                ReturnPath = returnUrl.TrimEnd('/')
            }
        };

        var sessionTimeoutSeconds = _options.SessionTimeoutSeconds > 0
            ? _options.SessionTimeoutSeconds
            : 60;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(sessionTimeoutSeconds));

            using var httpRequest = CreateJsonPostRequest(registration);
            using var httpResponse = await _httpClient.SendAsync(httpRequest, timeoutCts.Token);
            var rawResponse = await ReadResponseBodyAsync(httpResponse, timeoutCts.Token);
            var parsed = DeserializeTransactionResponse(rawResponse);
            var transaction = parsed?.Transaction;
            var isSuccess = IsGatewaySuccess(transaction)
                            && !string.IsNullOrWhiteSpace(transaction?.TransactionId);

            if (!isSuccess)
            {
                _logger.LogWarning(
                    "Native payment session creation failed for {Context}. OrderId={OrderId}; Description={Description}",
                    context,
                    orderId,
                    transaction?.ResponseDescription);
            }

            var authenticationToken = tokenResult.AuthenticationToken;

            return new NativePaymentSessionResult
            {
                IsSuccess = isSuccess,
                Message = transaction?.ResponseDescription
                           ?? (isSuccess ? "Native payment session created." : "Native payment session creation failed."),
                OrderId = orderId,
                TransactionId = transaction?.TransactionId ?? string.Empty,
                AuthenticationToken = authenticationToken,
                MerchantUserName = _options.UserName,
                CustomerName = _options.CustomerName,
                BaseUrl = ToNativeSdkBaseUrl(_options.BaseUrl),
                CallbackUrl = returnUrl.TrimEnd('/'),
                Amount = amount,
                Currency = _options.Currency,
                PaymentMethod = "Card"
            };
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Timeout creating native session for {Context}. OrderId={OrderId}.", context, orderId);
            return FailNative(
                orderId,
                amount,
                cancellationToken.IsCancellationRequested
                    ? "Payment session request was cancelled."
                    : $"Payment gateway timeout after {sessionTimeoutSeconds} seconds.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating native session for {Context}. OrderId={OrderId}.", context, orderId);
            return FailNative(orderId, amount, "Unable to create native payment session at the moment.");
        }
    }

    private string ResolveNativeReturnUrl(string orderId, string? returnUrlOverride)
    {
        var template = !string.IsNullOrWhiteSpace(returnUrlOverride)
            ? returnUrlOverride
            : !string.IsNullOrWhiteSpace(_options.NativeReturnBaseUrl)
                ? _options.NativeReturnBaseUrl
                : _options.ReturnBaseUrl;

        return string.Format(template, orderId).TrimEnd('/');
    }

    private static NativePaymentSessionResult FailNative(string orderId, decimal amount, string message) =>
        new()
        {
            IsSuccess = false,
            Message = message,
            OrderId = orderId,
            Amount = amount
        };

    private static NativeWalletSessionResult FailWallet(string orderId, string message) =>
        new()
        {
            IsSuccess = false,
            Message = message,
            OrderId = orderId
        };

    private string ResolveMobileChannel() =>
        string.IsNullOrWhiteSpace(_options.MobileChannel) ? "Web" : _options.MobileChannel;

    private static ComtrustRegistrationResponse? DeserializeTransactionResponse(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ComtrustRegistrationResponse>(rawResponse, ResponseJsonOptions);
    }

    private static bool IsGatewaySuccess(ComtrustTransaction? transaction)
    {
        if (transaction is null)
        {
            return false;
        }

        if (string.Equals(transaction.ResponseCode, "0", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var responseClass = transaction.ResponseClassDescription?.Trim() ?? string.Empty;
        return responseClass.Equals("success", StringComparison.OrdinalIgnoreCase)
               || responseClass.Equals("pending", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<PaymentSessionCreateResult> CreateSessionInternalAsync(
        string orderId,
        decimal amount,
        string orderInfo,
        string context,
        CancellationToken cancellationToken,
        string? returnUrlOverride = null)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return new PaymentSessionCreateResult
            {
                IsSuccess = false,
                Message = "Payment gateway base URL is not configured.",
                OrderId = orderId
            };
        }

        string returnUrl = string.IsNullOrWhiteSpace(returnUrlOverride)
            ? string.Format(_options.ReturnBaseUrl, orderId)
            : string.Format(returnUrlOverride, orderId);

        var registration = new ComtrustRegistrationRequest
        {
            Registration = new ComtrustRegistrationPayload
            {
                Customer = _options.CustomerName,
                Channel = _options.Channel,
                Amount = amount,
                Currency = _options.Currency,
                OrderID = orderId,
                OrderName = _options.OrderName,
                OrderInfo = orderInfo,
                TransactionHint = _options.TransactionHint,
                UserName = _options.UserName,
                Password = _options.Password,
                ReturnPath = returnUrl.TrimEnd('/')
            }
        };

        var sessionTimeoutSeconds = _options.SessionTimeoutSeconds > 0
            ? _options.SessionTimeoutSeconds
            : 60;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(sessionTimeoutSeconds));

            using var httpRequest = CreateJsonPostRequest(registration);
            using var httpResponse = await _httpClient.SendAsync(httpRequest, timeoutCts.Token);
            var rawResponse = await ReadResponseBodyAsync(httpResponse, timeoutCts.Token);
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                _logger.LogWarning(
                    "Payment gateway returned an empty response for {Context}. OrderId={OrderId}; Status={StatusCode}",
                    context,
                    orderId,
                    (int)httpResponse.StatusCode);
            }

            ComtrustRegistrationResponse? parsed = null;
            if (!string.IsNullOrWhiteSpace(rawResponse))
            {
                parsed = JsonSerializer.Deserialize<ComtrustRegistrationResponse>(
                    rawResponse,
                    ResponseJsonOptions);
            }

            var transactionRef = parsed?.Transaction?.TransactionId ?? string.Empty;
            var redirectUrl = parsed?.Transaction?.PaymentPage ?? string.Empty;
            var responseDescription = parsed?.Transaction?.ResponseDescription ?? string.Empty;
            var responseClass = parsed?.Transaction?.ResponseClassDescription ?? string.Empty;
            var isSuccess =
                httpResponse.IsSuccessStatusCode &&
                !string.IsNullOrWhiteSpace(redirectUrl) &&
                Uri.TryCreate(redirectUrl, UriKind.Absolute, out var redirectUri) &&
                (redirectUri.Scheme == Uri.UriSchemeHttps || redirectUri.Scheme == Uri.UriSchemeHttp) &&
                (string.IsNullOrWhiteSpace(responseClass) ||
                 responseClass.Equals("success", StringComparison.OrdinalIgnoreCase) ||
                 responseClass.Equals("pending", StringComparison.OrdinalIgnoreCase));

            if (!isSuccess)
            {
                _logger.LogWarning(
                    "Comtrust payment session creation failed for {Context}. OrderId={OrderId}; Status={StatusCode}; Class={ResponseClass}; Description={Description}; RedirectUrl={RedirectUrl}",
                    context,
                    orderId,
                    (int)httpResponse.StatusCode,
                    responseClass,
                    responseDescription,
                    redirectUrl);
            }

            return new PaymentSessionCreateResult
            {
                IsSuccess = isSuccess,
                Message = string.IsNullOrWhiteSpace(responseDescription)
                    ? (isSuccess ? "Payment session created." : "Payment session creation failed.")
                    : responseDescription,
                TransactionId = transactionRef,
                OrderId = orderId,
                RedirectUrl = redirectUrl
            };
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(
                ex,
                "Timeout/cancel while creating Comtrust payment session for {Context}. OrderId={OrderId}. SessionTimeout={TimeoutSeconds}s; RequestCancelled={RequestCancelled}",
                context,
                orderId,
                sessionTimeoutSeconds,
                cancellationToken.IsCancellationRequested);
            return new PaymentSessionCreateResult
            {
                IsSuccess = false,
                Message = cancellationToken.IsCancellationRequested
                    ? "Payment session request was cancelled."
                    : $"Payment gateway timeout after {sessionTimeoutSeconds} seconds. Please retry.",
                OrderId = orderId
            };
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Transport error while creating Comtrust payment session for {Context}. OrderId={OrderId}.", context, orderId);
            return new PaymentSessionCreateResult
            {
                IsSuccess = false,
                Message = "Payment gateway connection was interrupted. Please retry.",
                OrderId = orderId
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while creating Comtrust payment session for {Context}. OrderId={OrderId}.", context, orderId);
            return new PaymentSessionCreateResult
            {
                IsSuccess = false,
                Message = "Unable to reach payment gateway. Please retry.",
                OrderId = orderId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Comtrust payment session for {Context}. OrderId={OrderId}.", context, orderId);
            return new PaymentSessionCreateResult
            {
                IsSuccess = false,
                Message = "Unable to create payment session at the moment.",
                OrderId = orderId
            };
        }
    }

    private HttpRequestMessage CreateJsonPostRequest<TPayload>(TPayload payload)
    {
        var json = JsonSerializer.Serialize(payload, RequestJsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
        {
            // Matches working Postman/HttpClient call: null encoding => UTF-8, application/json only.
            Content = new StringContent(json, encoding: null, mediaType: "application/json")
        };

        request.Headers.TryAddWithoutValidation("Accept", RequiredAccept);
        return request;
    }

    private static async Task<string> ReadResponseBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Retrofit inside the native EPG Android SDK requires a trailing slash.
    /// </summary>
    private static string ToNativeSdkBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return string.Empty;
        }

        return $"{baseUrl.Trim().TrimEnd('/')}/";
    }
}

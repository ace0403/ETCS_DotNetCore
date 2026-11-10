using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Models;
using ETCS.PaymentGateway.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ETCS.PaymentGateway.Repositories;

public sealed partial class ComtrustPaymentGatewayRepository : IPaymentGatewayRepository
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
            cancellationToken,
            request.ReturnUrl);
    }

    public async Task<PaymentCaptureResult> CapturePaymentAsync(
        PaymentCaptureRequest request,
        CancellationToken cancellationToken)
    {
        var gatewayBaseUrl = ResolveGatewayBaseUrl(request.UseNativeCredentials);
        if (string.IsNullOrWhiteSpace(gatewayBaseUrl))
        {
            return new PaymentCaptureResult
            {
                IsSuccess = false,
                Message = "Payment gateway base URL is not configured.",
                TransactionId = request.TransactionId
            };
        }

        if (request.UseNativeCredentials && !TryGetMobileMerchant(out _))
        {
            return new PaymentCaptureResult
            {
                IsSuccess = false,
                Message = "Payment gateway mobile credentials are not configured.",
                TransactionId = request.TransactionId
            };
        }

        var merchant = ResolveMerchant(request.UseNativeCredentials);
        var finalization = new ComtrustFinalizationRequest
        {
            Finalization = new ComtrustFinalizationPayload
            {
                TransactionId = request.TransactionId,
                Customer = merchant.CustomerName,
                UserName = merchant.UserName,
                Password = merchant.Password
            }
        };

        var captureTimeoutSeconds = _options.CaptureTimeoutSeconds > 0
            ? _options.CaptureTimeoutSeconds
            : 90;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(captureTimeoutSeconds));

            using var httpRequest = CreateJsonPostRequest(finalization, gatewayBaseUrl);
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

            // V1 hosted checkout: EPG Finalization 6824 confirms cancellation.
            // Preserve this terminal state for V1 recovery; native SDK handling is unchanged.
            if (!request.UseNativeCredentials
                && httpResponse.IsSuccessStatusCode
                && parsed?.Transaction?.ResponseCode?.Trim() == "6824"
                && (string.IsNullOrWhiteSpace(parsed?.Transaction?.TransactionId)
                    || string.Equals(parsed.Transaction.TransactionId, request.TransactionId, StringComparison.Ordinal)))
            {
                return new PaymentCaptureResult
                {
                    IsSuccess = false,
                    IsPending = false,
                    TransactionId = request.TransactionId,
                    Status = "cancelled-confirmed",
                    Message = "Payment cancelled by the gateway."
                };
            }
            var isSuccess = normalizedClass is "success" or "closed";
            var isPending = normalizedClass == "pending";
            if (isPending)
            {
                isSuccess = true;
            }

            // Hosted V1 checkout must have positive, matching gateway evidence.
            // Keep native SDK behavior in its existing validation path.
            if (!request.UseNativeCredentials)
            {
                var matches = !string.IsNullOrWhiteSpace(parsed?.Transaction?.TransactionId)
                    && string.Equals(parsed.Transaction.TransactionId, request.TransactionId, StringComparison.Ordinal);
                isPending = httpResponse.IsSuccessStatusCode && matches && normalizedClass == "pending";
                isSuccess = httpResponse.IsSuccessStatusCode && matches
                    && parsed?.Transaction?.ResponseCode?.Trim() == "0"
                    && normalizedClass is "success" or "closed";
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

    public Task<GenerateTokenResult> GenerateTokenAsync(CancellationToken cancellationToken) =>
        GenerateTokenAsync(useNativeCredentials: true, cancellationToken);

    private async Task<GenerateTokenResult> GenerateTokenAsync(
        bool useNativeCredentials,
        CancellationToken cancellationToken)
    {
        var gatewayBaseUrl = ResolveGatewayBaseUrl(useNativeCredentials);
        if (string.IsNullOrWhiteSpace(gatewayBaseUrl))
            return new() { Message = "Payment gateway base URL is not configured." };
        if (useNativeCredentials && !TryGetMobileMerchant(out _))
            return new() { Message = "Payment gateway mobile credentials are not configured." };

        var merchant = ResolveMerchant(useNativeCredentials);
        var tokenRequest = new ComtrustGenerateTokenRequest
        {
            GenerateToken = new ComtrustGenerateTokenPayload { UserName = merchant.UserName, Password = merchant.Password }
        };
        int? status = null;
        string? contentType = null;
        Uri.TryCreate(gatewayBaseUrl, UriKind.Absolute, out var endpoint);

        GenerateTokenResult Failed(string failure, string message, string? code = null, Exception? error = null)
        {
            // Metadata only: never log the request/response body, credentials, token or raw exception text.
            var safeCode = code is { Length: > 0 and <= 12 } && code.All(c => c is >= '0' and <= '9') ? code : null;
            _logger.LogWarning("Native EPG GenerateToken failed. Failure={Failure}; GatewayHost={GatewayHost}; GatewayPort={GatewayPort}; HttpStatus={HttpStatus}; ContentType={ContentType}; ResponseCode={ResponseCode}; ExceptionType={ExceptionType}; HttpRequestError={HttpRequestError}",
                failure, endpoint?.Host, endpoint?.Port, status, contentType, safeCode, error?.GetType().Name,
                error is HttpRequestException http ? http.HttpRequestError.ToString() : null);
            return new() { Message = message };
        }

        try
        {
            using var sessionTimeout = CreateNativeSessionTimeout(cancellationToken);
            using var httpRequest = CreateNativeJsonPostRequest(tokenRequest, gatewayBaseUrl, merchant);
            using var httpResponse = await _httpClient.SendAsync(httpRequest, sessionTimeout.Token);
            status = (int)httpResponse.StatusCode;
            contentType = httpResponse.Content.Headers.ContentType?.MediaType;
            // An HTML proxy/error page must not be parsed as a successful EPG response.
            if (!httpResponse.IsSuccessStatusCode)
                return Failed("http-error", "The payment gateway is currently unavailable. Please try again later.");
            var rawResponse = await ReadResponseBodyAsync(httpResponse, sessionTimeout.Token);
            var parsed = DeserializeNativeTransactionResponse(rawResponse);
            if (parsed?.Transaction is null)
                return Failed("invalid-envelope", "The payment gateway returned an invalid authentication response. Please try again later.");
            var code = Code(parsed.Transaction.ResponseCode);
            if (code != "0")
                return Failed("response-code", "The payment gateway could not authenticate this session. Please try again later.", code);
            var token = parsed.Transaction.AuthenticationToken;
            if (string.IsNullOrWhiteSpace(token))
                return Failed("missing-token", "The payment gateway did not return an authentication token. Please try again later.", code);
            return new() { IsSuccess = true, Message = "Token generated.", AuthenticationToken = token };
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            return Failed("cancelled", "Payment setup was interrupted. Please try again.", error: ex);
        }
        catch (OperationCanceledException ex)
        {
            return Failed("timeout", "The payment gateway took too long to prepare payment. Please try again.", error: ex);
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.SecureConnectionError)
        {
            return Failed("tls-error", "The payment service could not establish a secure connection to the gateway. Please try again later.", error: ex);
        }
        catch (HttpRequestException ex)
        {
            return Failed("connection-error", "The payment service could not reach the payment gateway. Please try again later.", error: ex);
        }
        catch (IOException ex)
        {
            return Failed("interrupted-connection", "The payment gateway connection was interrupted. Please try again.", error: ex);
        }
        catch (JsonException ex)
        {
            return Failed("invalid-json", "The payment gateway returned an invalid authentication response. Please try again later.", error: ex);
        }
        catch (InvalidOperationException ex)
        {
            return Failed("configuration-error", "Native payment gateway configuration is invalid. Please contact support.", error: ex);
        }
        catch (Exception ex)
        {
            return Failed("unexpected-error", "Unable to prepare payment authentication at the moment. Please try again later.", error: ex);
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
        var configurationError = _options.NativeConfigurationError(request.PaymentMethod);
        if (configurationError is not null) return FailWallet(request.OrderId, configurationError);
        if (string.IsNullOrWhiteSpace(request.OrderId) || request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount)
            return FailWallet(request.OrderId, "A valid order reference and amount are required.");
        var gatewayBaseUrl = ResolveGatewayBaseUrl(useNativeCredentials: true);
        if (string.IsNullOrWhiteSpace(gatewayBaseUrl))
        {
            return FailWallet(request.OrderId, "Payment gateway base URL is not configured.");
        }

        if (!TryGetMobileMerchant(out var merchant))
        {
            return FailWallet(request.OrderId, "Payment gateway mobile credentials are not configured.");
        }

        var tokenResult = await GenerateTokenAsync(useNativeCredentials: true, cancellationToken);
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
                Customer = merchant.CustomerName,
                UserName = merchant.UserName,
                Password = merchant.Password,
                AuthenticationToken = tokenResult.AuthenticationToken,
                Store = NullIfWhiteSpace(merchant.Store),
                Terminal = NullIfWhiteSpace(merchant.Terminal),
                WalletName = walletName
            }
        };

        try
        {
            using var sessionTimeout = CreateNativeSessionTimeout(cancellationToken);
            using var httpRequest = CreateNativeJsonPostRequest(walletRequest, gatewayBaseUrl, merchant);
            using var httpResponse = await _httpClient.SendAsync(httpRequest, sessionTimeout.Token);
            var rawResponse = await ReadResponseBodyAsync(httpResponse, sessionTimeout.Token);
            var parsed = DeserializeNativeTransactionResponse(rawResponse);
            var transaction = parsed?.Transaction;
            var isSuccess = httpResponse.IsSuccessStatusCode && IsGatewaySuccess(transaction)
                            && !string.IsNullOrWhiteSpace(transaction?.TransactionId)
                            && !string.IsNullOrWhiteSpace(transaction?.SessionId)
                            && RegistrationResponseMatches(transaction!, request.OrderId, request.Amount, _options.Currency);

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
                Message = !isSuccess && httpResponse.IsSuccessStatusCode && IsGatewaySuccess(transaction)
                    ? "Gateway returned incomplete or mismatched wallet session details."
                    : transaction?.ResponseDescription ?? (isSuccess ? "Wallet session created." : "Wallet registration failed."),
                OrderId = request.OrderId,
                TransactionId = transaction?.TransactionId ?? string.Empty,
                AuthenticationToken = tokenResult.AuthenticationToken,
                SessionId = transaction?.SessionId ?? string.Empty,
                MerchantUserName = merchant.UserName,
                CustomerName = merchant.CustomerName,
                BaseUrl = ToNativeSdkBaseUrl(gatewayBaseUrl),
                CallbackUrl = callbackUrl,
                Amount = request.Amount,
                Currency = _options.Currency,
                PaymentMethod = request.PaymentMethod,
                ManualCapture = NativeEpgTransport.RequiresManualCapture(_options.WalletTransactionHint),
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
        var configurationError = _options.NativeConfigurationError("Card");
        if (configurationError is not null) return FailNative(orderId, amount, configurationError);
        if (string.IsNullOrWhiteSpace(orderId) || amount <= 0 || decimal.Round(amount, 2) != amount)
            return FailNative(orderId, amount, "A valid order reference and amount are required.");
        var gatewayBaseUrl = ResolveGatewayBaseUrl(useNativeCredentials: true);
        if (string.IsNullOrWhiteSpace(gatewayBaseUrl))
        {
            return FailNative(orderId, amount, "Payment gateway base URL is not configured.");
        }

        if (!TryGetMobileMerchant(out var merchant))
        {
            return FailNative(orderId, amount, "Payment gateway mobile credentials are not configured.");
        }

        var returnUrl = ResolveNativeReturnUrl(orderId, returnUrlOverride);

        var tokenResult = await GenerateTokenAsync(useNativeCredentials: true, cancellationToken);
        if (!tokenResult.IsSuccess || string.IsNullOrWhiteSpace(tokenResult.AuthenticationToken))
        {
            return FailNative(
                orderId,
                amount,
                string.IsNullOrWhiteSpace(tokenResult.Message)
                    ? "Unable to generate EPG authentication token."
                    : tokenResult.Message);
        }

        // Native SDK: GenerateToken, then Registration with AuthenticationToken + Store/Terminal.
        // Server-side merchant authentication follows the configured native endpoint's mode.
        // The app receives the SDK token, never the merchant password. V1 serialization is unchanged.
        var registration = new ComtrustRegistrationRequest
        {
            Registration = new ComtrustRegistrationPayload
            {
                Customer = merchant.CustomerName,
                Channel = ResolveMobileChannel(),
                Amount = amount,
                Currency = _options.Currency,
                OrderID = orderId,
                OrderName = _options.OrderName,
                OrderInfo = orderInfo,
                TransactionHint = _options.TransactionHint,
                UserName = merchant.UserName,
                Store = NullIfWhiteSpace(merchant.Store),
                Terminal = NullIfWhiteSpace(merchant.Terminal),
                Password = merchant.Password,
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

            using var httpRequest = CreateNativeJsonPostRequest(registration, gatewayBaseUrl, merchant);
            using var httpResponse = await _httpClient.SendAsync(httpRequest, timeoutCts.Token);
            var rawResponse = await ReadResponseBodyAsync(httpResponse, timeoutCts.Token);
            var parsed = DeserializeNativeTransactionResponse(rawResponse);
            var transaction = parsed?.Transaction;
            var isSuccess = httpResponse.IsSuccessStatusCode && IsGatewaySuccess(transaction)
                            && !string.IsNullOrWhiteSpace(transaction?.TransactionId)
                            && RegistrationResponseMatches(transaction!, orderId, amount, _options.Currency);

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
                Message = !isSuccess && httpResponse.IsSuccessStatusCode && IsGatewaySuccess(transaction)
                    ? "Gateway returned incomplete or mismatched native session details."
                    : transaction?.ResponseDescription ?? (isSuccess ? "Native payment session created." : "Native payment session creation failed."),
                OrderId = orderId,
                TransactionId = transaction?.TransactionId ?? string.Empty,
                AuthenticationToken = authenticationToken,
                MerchantUserName = merchant.UserName,
                CustomerName = merchant.CustomerName,
                BaseUrl = ToNativeSdkBaseUrl(gatewayBaseUrl),
                CallbackUrl = returnUrl.TrimEnd('/'),
                Amount = amount,
                Currency = _options.Currency,
                PaymentMethod = "Card",
                ManualCapture = NativeEpgTransport.RequiresManualCapture(_options.TransactionHint)
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
            ? returnUrlOverride : _options.NativeReturnBaseUrl;
        template = template.Replace("%7B0%7D", "{0}", StringComparison.OrdinalIgnoreCase);
        var resolved = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            template, Uri.EscapeDataString(orderId.Trim()));
        if (!Uri.TryCreate(resolved, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Native payments require an HTTPS return URL.");
        return resolved;
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

    /// <summary>
    /// Native mobile flows prefer PaymentGateway:Mobile:BaseUrl; otherwise PaymentGateway:BaseUrl.
    /// </summary>
    private string ResolveGatewayBaseUrl(bool useNativeCredentials)
    {
        if (useNativeCredentials
            && !string.IsNullOrWhiteSpace(_options.Mobile?.BaseUrl))
        {
            return _options.Mobile.BaseUrl.Trim();
        }

        return _options.BaseUrl?.Trim() ?? string.Empty;
    }

    private bool TryGetMobileMerchant(out MobileMerchant merchant)
    {
        var mobile = _options.Mobile;
        if (mobile is null || !mobile.IsConfigured)
        {
            merchant = default;
            return false;
        }

        merchant = new MobileMerchant(
            CustomerName: mobile.CustomerName,
            UserName: mobile.UserName,
            Password: mobile.Password,
            Store: mobile.Store,
            Terminal: mobile.Terminal);
        return true;
    }

    private MobileMerchant ResolveMerchant(bool useNativeCredentials) =>
        useNativeCredentials && TryGetMobileMerchant(out var mobile)
            ? mobile
            : new MobileMerchant(
                CustomerName: _options.CustomerName,
                UserName: _options.UserName,
                Password: _options.Password,
                Store: null,
                Terminal: null);

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private readonly record struct MobileMerchant(
        string CustomerName,
        string UserName,
        string Password,
        string? Store,
        string? Terminal);

    private static NativeEpgResponse? DeserializeNativeTransactionResponse(string? rawResponse) =>
        string.IsNullOrWhiteSpace(rawResponse) ? null : DeserializeValidatedNativeResponse(rawResponse);

    private static bool IsGatewaySuccess(NativeEpgTransaction? transaction) =>
        transaction is not null && Code(transaction.ResponseCode) == "0";
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

    private HttpRequestMessage CreateJsonPostRequest<TPayload>(TPayload payload, string? baseUrl = null)
    {
        var endpoint = string.IsNullOrWhiteSpace(baseUrl) ? _options.BaseUrl : baseUrl.Trim();
        var json = JsonSerializer.Serialize(payload, RequestJsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
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

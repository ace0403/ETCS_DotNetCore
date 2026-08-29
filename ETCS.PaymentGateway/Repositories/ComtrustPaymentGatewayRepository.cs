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
}

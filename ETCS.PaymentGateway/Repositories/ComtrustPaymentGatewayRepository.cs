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
        CancellationToken cancellationToken)
    {
        return await CreateSessionInternalAsync(
            orderId,
            request.Amount,
            request.StudentId,
            "student topup",
            cancellationToken);
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
                Message = "Payment gateway base URL is not configured."
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

        try
        {
            using var httpRequest = CreateJsonPostRequest(finalization);
            using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);

            var rawResponse = await ReadResponseBodyAsync(httpResponse, cancellationToken);
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
                    "Payment capture failed for transaction {TransactionId}. Status={StatusCode}; Class={ResponseClass}",
                    request.TransactionId,
                    (int)httpResponse.StatusCode,
                    responseClass);
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
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout while capturing payment status for transaction {TransactionId}. Timeout={TimeoutSeconds}s", request.TransactionId, _options.TimeoutSeconds);
            return new PaymentCaptureResult
            {
                IsSuccess = false,
                Message = $"Payment gateway timeout after {_options.TimeoutSeconds} seconds.",
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
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return new PaymentSessionCreateResult
            {
                IsSuccess = false,
                Message = "Payment gateway base URL is not configured."
            };
        }

        string returnUrl = string.Format(_options.ReturnBaseUrl, orderId);

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

        try
        {
            using var httpRequest = CreateJsonPostRequest(registration);
            using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var rawResponse = await ReadResponseBodyAsync(httpResponse, cancellationToken);
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
                (string.IsNullOrWhiteSpace(responseClass) ||
                 responseClass.Equals("success", StringComparison.OrdinalIgnoreCase) ||
                 responseClass.Equals("pending", StringComparison.OrdinalIgnoreCase));

            if (!isSuccess)
            {
                _logger.LogWarning(
                    "Comtrust payment session creation failed for {Context}. OrderId={OrderId}; Status={StatusCode}; Class={ResponseClass}",
                    context,
                    orderId,
                    (int)httpResponse.StatusCode,
                    responseClass);
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
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout while creating Comtrust payment session for {Context}. OrderId={OrderId}. Timeout={TimeoutSeconds}s", context, orderId, _options.TimeoutSeconds);
            return new PaymentSessionCreateResult
            {
                IsSuccess = false,
                Message = $"Payment gateway timeout after {_options.TimeoutSeconds} seconds. Please retry."
            };
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Transport error while creating Comtrust payment session for {Context}. OrderId={OrderId}.", context, orderId);
            return new PaymentSessionCreateResult
            {
                IsSuccess = false,
                Message = "Payment gateway connection was interrupted. Please retry."
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while creating Comtrust payment session for {Context}. OrderId={OrderId}.", context, orderId);
            return new PaymentSessionCreateResult
            {
                IsSuccess = false,
                Message = "Unable to reach payment gateway. Please retry."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Comtrust payment session for {Context}. OrderId={OrderId}.", context, orderId);
            return new PaymentSessionCreateResult
            {
                IsSuccess = false,
                Message = "Unable to create payment session at the moment."
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

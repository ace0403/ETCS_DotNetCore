using System.Net.Http.Json;
using System.Text.Json;
using ETCS.Pos.Web.Options;
using Microsoft.Extensions.Options;

namespace ETCS.Pos.Web.Services;

public interface IPosApiProxyService
{
    Task<(T? Data, string? Error)> GetAsync<T>(string path, CancellationToken cancellationToken);

    Task<PosApiProxyResponse> ProxyGetAsync(string path, CancellationToken cancellationToken);

    Task<PosApiProxyResponse> ProxyPostAsync(string path, object body, CancellationToken cancellationToken);
}

public sealed class PosApiProxyResponse
{
    public int StatusCode { get; init; }
    public string Content { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/json";
}

public sealed class PosApiProxyService : IPosApiProxyService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PosWebOptions _options;
    private readonly ILogger<PosApiProxyService> _logger;

    public PosApiProxyService(
        IHttpClientFactory httpClientFactory,
        IOptions<PosWebOptions> options,
        ILogger<PosApiProxyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<(T? Data, string? Error)> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var result = await ProxyGetAsync(path, cancellationToken);
        if (result.StatusCode < 200 || result.StatusCode >= 300)
        {
            var message = $"API returned {result.StatusCode} for {path}.";
            _logger.LogWarning("{Message} Body: {Body}", message, result.Content);
            return (default, message);
        }

        try
        {
            var data = JsonSerializer.Deserialize<T>(result.Content, JsonOptions);
            return (data, null);
        }
        catch (Exception ex)
        {
            var message = $"Invalid API response for {path}. ({ex.Message})";
            _logger.LogWarning(ex, "Failed to deserialize POS API response for {Path}", path);
            return (default, message);
        }
    }

    public async Task<PosApiProxyResponse> ProxyGetAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PosApi");
            using var response = await client.GetAsync(path, cancellationToken);
            return await ToProxyResponseAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return ConnectionFailed(path, ex);
        }
    }

    public async Task<PosApiProxyResponse> ProxyPostAsync(string path, object body, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PosApi");
            using var response = await client.PostAsJsonAsync(path, body, cancellationToken);
            return await ToProxyResponseAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return ConnectionFailed(path, ex);
        }
    }

    private static async Task<PosApiProxyResponse> ToProxyResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
        return new PosApiProxyResponse
        {
            StatusCode = (int)response.StatusCode,
            Content = string.IsNullOrWhiteSpace(content) ? "{}" : content,
            ContentType = contentType
        };
    }

    private PosApiProxyResponse ConnectionFailed(string path, Exception ex)
    {
        var message = $"Cannot reach ETCS.API at {_options.ApiBaseUrl}. Start ETCS.API and verify PosWeb:ApiBaseUrl. ({ex.Message})";
        _logger.LogWarning(ex, "POS API call failed for {Path}", path);
        return new PosApiProxyResponse
        {
            StatusCode = StatusCodes.Status502BadGateway,
            Content = JsonSerializer.Serialize(new { message }),
            ContentType = "application/json"
        };
    }
}

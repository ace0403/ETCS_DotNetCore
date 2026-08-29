using System.IO;
using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Options;
using ETCS.PaymentGateway.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace ETCS.PaymentGateway.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PaymentGatewayOptions>(configuration.GetSection(PaymentGatewayOptions.SectionName));

        services.AddHttpClient<IPaymentGatewayRepository, ComtrustPaymentGatewayRepository>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentGatewayOptions>>().Value;
            // Overall HttpClient ceiling; session create also applies SessionTimeoutSeconds per call.
            var timeoutSeconds = options.TimeoutSeconds <= 0 ? 120 : Math.Min(options.TimeoutSeconds, 120);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        })
        .AddPolicyHandler(GetRetryPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        // One short retry only — demo PG hangs make multi-retry very slow.
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<IOException>()
            .WaitAndRetryAsync(1, _ => TimeSpan.FromSeconds(2));
    }
}

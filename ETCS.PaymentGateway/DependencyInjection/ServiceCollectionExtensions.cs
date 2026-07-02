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
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds <= 0 ? 600 : options.TimeoutSeconds);
        })
        .AddPolicyHandler(GetRetryPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<IOException>()
            .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}

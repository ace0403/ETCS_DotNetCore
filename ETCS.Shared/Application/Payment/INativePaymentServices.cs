namespace ETCS.Shared.Application.Payment;

public interface INativeTopupInitiateService
{
    Task<NativeTopupInitiateResponse> InitiateAsync(
        NativeTopupInitiateRequest request,
        CancellationToken cancellationToken);
}

public interface INativeOrderInitiateService
{
    Task<NativeOrderInitiateResponse> InitiateAsync(
        NativeOrderInitiateRequest request,
        CancellationToken cancellationToken);
}

public interface INativeWalletRegistrationService
{
    Task<NativeWalletRegisterResponse> RegisterAsync(
        NativeWalletRegisterRequest request,
        CancellationToken cancellationToken);
}

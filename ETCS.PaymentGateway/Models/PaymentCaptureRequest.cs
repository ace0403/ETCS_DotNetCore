namespace ETCS.PaymentGateway.Models;

public sealed record PaymentCaptureRequest(
    string TransactionId,
    string OrderId,
    int StudentId,
    bool UseNativeCredentials = false);

namespace ETCS.PaymentGateway.Models;

public sealed record PaymentCaptureRequest(
    string TransactionId,
    string OrderId, 
    int StudentId);

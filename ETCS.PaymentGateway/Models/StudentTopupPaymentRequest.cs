namespace ETCS.PaymentGateway.Models;

public sealed record StudentTopupPaymentRequest(string StudentId, decimal Amount);

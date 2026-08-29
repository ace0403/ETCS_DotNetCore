namespace ETCS.Shared.Infrastructure.Students;

public sealed class ReplaceCardSubmitRequest
{
    public string CustomerId { get; set; } = string.Empty;

    public string CardNumber { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}

using ETCS.Shared.Application.Background;
using ETCS.Shared.Infrastructure.Transaction;

namespace ETCS.Admin.Infrastructure.Payments;

// Scoped to one recovery request. Flush is awaited, so no request-owned work is
// abandoned when the HTTP response finishes.
public sealed class AdminPaymentLogBuffer(ITransactionRepository transactions, ILogger<AdminPaymentLogBuffer> logger)
    : IPaymentBackgroundQueue
{
    private readonly List<(string Reference, string Result)> _logs = [];
    private readonly List<QueueEmailNotificationRequest> _emails = [];
    public void EnqueuePaymentLog(string transactionId, string result) => _logs.Add((transactionId, result));
    public void EnqueueEmail(QueueEmailNotificationRequest request) => _emails.Add(request);

    public async Task FlushAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            foreach (var log in _logs)
                await transactions.LogPaymentRequestAsync(log.Reference, log.Result, timeout.Token);
            foreach (var email in _emails)
                await transactions.QueueEmailNotificationAsync(email, timeout.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "An admin top-up recovery log could not be saved.");
        }
        finally { _logs.Clear(); _emails.Clear(); }
    }
}

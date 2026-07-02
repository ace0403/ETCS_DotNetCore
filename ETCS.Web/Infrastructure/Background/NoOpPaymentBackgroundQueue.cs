using ETCS.Shared.Application.Background;
using ETCS.Shared.Infrastructure.Transaction;

namespace ETCS.Web.Infrastructure.Background;

public sealed class NoOpPaymentBackgroundQueue : IPaymentBackgroundQueue
{
    public void EnqueuePaymentLog(string transactionId, string result)
    {
    }

    public void EnqueueEmail(QueueEmailNotificationRequest request)
    {
    }
}

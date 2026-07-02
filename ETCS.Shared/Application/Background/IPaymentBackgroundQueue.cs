using ETCS.Shared.Infrastructure.Transaction;

namespace ETCS.Shared.Application.Background;

public interface IPaymentBackgroundQueue
{
    void EnqueuePaymentLog(string transactionId, string result);

    void EnqueueEmail(QueueEmailNotificationRequest request);
}

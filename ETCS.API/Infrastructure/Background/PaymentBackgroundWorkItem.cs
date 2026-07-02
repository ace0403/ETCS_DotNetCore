using ETCS.Shared.Infrastructure.Transaction;

namespace ETCS.API.Infrastructure.Background;

public abstract record PaymentBackgroundWorkItem;

public sealed record PaymentLogWorkItem(string TransactionId, string Result) : PaymentBackgroundWorkItem;

public sealed record EmailNotificationWorkItem(QueueEmailNotificationRequest Request) : PaymentBackgroundWorkItem;

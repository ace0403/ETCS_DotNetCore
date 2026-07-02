using System.Threading.Channels;
using ETCS.Shared.Application.Background;
using ETCS.Shared.Infrastructure.Transaction;

namespace ETCS.API.Infrastructure.Background;

public sealed class PaymentBackgroundQueue : IPaymentBackgroundQueue
{
    private const int MaxPaymentLogBacklog = 5000;

    private readonly Channel<PaymentLogWorkItem> _paymentLogChannel =
        Channel.CreateBounded<PaymentLogWorkItem>(new BoundedChannelOptions(MaxPaymentLogBacklog)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    public void EnqueuePaymentLog(string transactionId, string result)
    {
        _paymentLogChannel.Writer.TryWrite(new PaymentLogWorkItem(transactionId, result));
    }

    public void EnqueueEmail(QueueEmailNotificationRequest request)
    {
    }

    internal ChannelReader<PaymentLogWorkItem> PaymentLogReader => _paymentLogChannel.Reader;
}

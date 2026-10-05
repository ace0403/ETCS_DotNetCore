namespace ETCS.Shared.Enumeration;

public enum TransactionSourceChannel
{
    Api,
    Web,
    Pos
}

public static class TransactionSourceChannelExtensions
{
    public static string ToDbValue(this TransactionSourceChannel channel) =>
        channel switch
        {
            TransactionSourceChannel.Api => "Api",
            TransactionSourceChannel.Web => "Web",
            TransactionSourceChannel.Pos => "Pos",
            _ => "Api"
        };
}

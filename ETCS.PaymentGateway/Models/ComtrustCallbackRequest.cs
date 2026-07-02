namespace ETCS.PaymentGateway.Models
{
    public class ComtrustCallbackRequest
    {
        public string OrderID { get; set; } = string.Empty;
        public string TransactionID { get; set; } = string.Empty;
        public string ResponseCode { get; set; } = string.Empty;
        public string ResponseDescription { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string AuthCode { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
    }
}

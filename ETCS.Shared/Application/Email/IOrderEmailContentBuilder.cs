namespace ETCS.Shared.Application.Email;

public interface IOrderEmailContentBuilder
{
    Task<string> BuildOrderSuccessContentAsync(
        int guardianId,
        int studentId,
        int orderTypeId,
        string orderId,
        decimal total,
        CancellationToken cancellationToken);
}

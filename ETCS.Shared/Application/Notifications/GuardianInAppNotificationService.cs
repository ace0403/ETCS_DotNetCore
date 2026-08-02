using System.Globalization;
using ETCS.Shared.Infrastructure.Notifications;
using Microsoft.Extensions.Logging;

namespace ETCS.Shared.Application.Notifications;

public sealed class GuardianInAppNotificationService : IGuardianInAppNotificationService
{
    private readonly IGuardianNotificationRepository _repository;
    private readonly ILogger<GuardianInAppNotificationService> _logger;

    public GuardianInAppNotificationService(
        IGuardianNotificationRepository repository,
        ILogger<GuardianInAppNotificationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task CreateTopupSuccessAsync(
        int studentId,
        int guardianId,
        decimal amount,
        string orderId,
        int? schoolId,
        CancellationToken cancellationToken)
    {
        await CreateSafeAsync(
            new CreateGuardianNotificationRequest
            {
                GuardianId = guardianId,
                StudentId = studentId > 0 ? studentId : null,
                SchoolId = schoolId,
                Type = GuardianNotificationTypes.TopupSuccess,
                Title = "Wallet Top-Up Successful",
                Message = $"AED {amount.ToString("0.00", CultureInfo.InvariantCulture)} has been added to your wallet.",
                ReferenceType = GuardianNotificationReferenceTypes.Topup,
                ReferenceId = orderId,
                CreatedBy = "System"
            },
            cancellationToken);
    }

    public async Task CreateOrderSuccessAsync(
        int studentId,
        int guardianId,
        string studentName,
        string orderId,
        string mealLabel,
        int? schoolId,
        CancellationToken cancellationToken)
    {
        var child = string.IsNullOrWhiteSpace(studentName) ? "your child" : studentName.Trim();
        var label = string.IsNullOrWhiteSpace(mealLabel) ? "Order" : mealLabel.Trim();
        var refId = string.IsNullOrWhiteSpace(orderId) ? string.Empty : orderId.Trim();

        await CreateSafeAsync(
            new CreateGuardianNotificationRequest
            {
                GuardianId = guardianId,
                StudentId = studentId > 0 ? studentId : null,
                SchoolId = schoolId,
                Type = GuardianNotificationTypes.OrderConfirmed,
                Title = "Order Confirmed",
                Message = string.IsNullOrWhiteSpace(refId)
                    ? $"{label} order for {child} has been confirmed."
                    : $"{label} order for {child} has been confirmed. Ref: {refId}",
                ReferenceType = GuardianNotificationReferenceTypes.Order,
                ReferenceId = refId,
                CreatedBy = "System"
            },
            cancellationToken);
    }

    public Task CreateAsync(CreateGuardianNotificationRequest request, CancellationToken cancellationToken) =>
        CreateSafeAsync(request, cancellationToken);

    public async Task<int> BroadcastToSchoolAsync(
        CreateSchoolBroadcastNotificationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _repository.CreateForSchoolAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to broadcast in-app notification for school {SchoolId}",
                request.SchoolId);
            return 0;
        }
    }

    private async Task CreateSafeAsync(CreateGuardianNotificationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _repository.CreateAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create in-app notification for guardian {GuardianId}, type {Type}",
                request.GuardianId,
                request.Type);
        }
    }
}

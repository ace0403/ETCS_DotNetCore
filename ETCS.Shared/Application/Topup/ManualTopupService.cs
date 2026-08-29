using System.Globalization;
using System.Text.Json;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Pos;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;

namespace ETCS.Shared.Application.Topup;

public sealed class ManualTopupService : IManualTopupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IStudentRepository _studentRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMainOrderRepository _mainOrderRepository;

    public ManualTopupService(
        IStudentRepository studentRepository,
        ITransactionRepository transactionRepository,
        IMainOrderRepository mainOrderRepository)
    {
        _studentRepository = studentRepository;
        _transactionRepository = transactionRepository;
        _mainOrderRepository = mainOrderRepository;
    }

    public async Task<PosCardCheckResponse> CheckCardAsync(
        string cardNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            return new PosCardCheckResponse
            {
                IsSuccess = false,
                Message = "Card number is required."
            };
        }

        var student = await _studentRepository.GetStudentIdentityByCustomerIdAsync(
            cardNumber.Trim(),
            cancellationToken);

        if (student is null || student.UserId <= 0)
        {
            return new PosCardCheckResponse
            {
                IsSuccess = false,
                Message = "Student not found for the given card number."
            };
        }

        var balance = await _studentRepository.GetPrepaidBalanceByCustomerIdAsync(
            student.CustomerId,
            cancellationToken);

        return new PosCardCheckResponse
        {
            IsSuccess = true,
            Message = "Card found.",
            CustomerId = student.CustomerId,
            StudentName = student.StudentName,
            Balance = balance
        };
    }

    public async Task<PosManualTopupResponse> ProcessAsync(
        PosManualTopupRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CardNumber))
        {
            return Fail("Card number is required.");
        }

        if (request.Amount <= 0)
        {
            return Fail("Amount must be greater than zero.");
        }

        var cardNumber = request.CardNumber.Trim();
        var student = await _studentRepository.GetStudentIdentityByCustomerIdAsync(
            cardNumber,
            cancellationToken);

        if (student is null || student.UserId <= 0)
        {
            return Fail("Student not found for the given card number.");
        }

        var transactionId = string.IsNullOrWhiteSpace(request.TransactionId)
            ? Guid.NewGuid().ToString("N")
            : request.TransactionId.Trim();

        var orderId = OrderIdGenerator.GenerateForStudent(student.UserId);
        var remarks = string.IsNullOrWhiteSpace(request.Remarks)
            ? "Manual top-up from kiosk."
            : request.Remarks.Trim();
        var creby = string.IsNullOrWhiteSpace(student.Email) ? "KIOSK" : student.Email;
        var nowText = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture);
        var amountText = request.Amount.ToString(CultureInfo.InvariantCulture);

        try
        {
            var topupTransactionPkId = await _transactionRepository.CreateTopupPendingTransactionAsync(
                new TopupTransactionCreateRequest
                {
                    GuardianId = student.GuardianId,
                    StudentId = student.UserId,
                    Amount = request.Amount,
                    Remarks = orderId,
                    StatusId = (int)TransactionStatusEnum.Pending,
                    CreatedBy = student.GuardianId
                },
                cancellationToken);

            var requestObject = JsonSerializer.Serialize(
                new
                {
                    GUID = orderId,
                    TransactionId = transactionId,
                    GrdId = student.GuardianId,
                    CustomerId = student.CustomerId,
                    GuardianEmail = student.Email,
                    Amount = amountText,
                    TransactionType = "manual-topup",
                    Remarks = remarks
                },
                JsonOptions);

            await _transactionRepository.InsertPendingTransactionAsync(
                new PendingTransactionRequest
                {
                    CustomerID = student.CustomerId,
                    Creby = creby,
                    Amount = amountText,
                    Loaded = "0",
                    TransDate = nowText,
                    Remarks = orderId,
                    Mode = "M",
                    BankName = "MANUAL",
                    PaymentDetails = transactionId,
                    Billdate = nowText,
                    RequestObject = requestObject
                },
                cancellationToken);

            await _transactionRepository.UpdateTopupTransactionStatusAsync(
                new TopupTransactionUpdateRequest
                {
                    TransactionPkId = topupTransactionPkId,
                    GatewayTransactionId = transactionId,
                    StatusId = (int)TransactionStatusEnum.Success,
                    IsTransactionCompleted = true,
                    Remarks = remarks,
                    UpdatedBy = student.GuardianId
                },
                cancellationToken);

            var accessLogId = await _mainOrderRepository.InsertAccessLogAsync(
                student.CustomerId,
                request.Amount,
                (short)AccessLogTypeEnum.Topup,
                "TOPUP RECHARGE",
                transactionId,
                "777",
                "240",
                cancellationToken);

            await _transactionRepository.AttachAccessLogIdByTransactionPkAsync(
                topupTransactionPkId,
                accessLogId,
                cancellationToken);

            await _transactionRepository.UpdatePendingAndTopupTransactionAsync(
                new UpdatePendingTransactionRequest
                {
                    CustomerID = student.CustomerId,
                    Loaded = "1",
                    Creby = creby,
                    PaymentDetails = transactionId,
                    Remarks = orderId
                },
                new UpdateTopupTransactionRequest
                {
                    CustomerID = student.CustomerId,
                    Remarks = orderId
                },
                cancellationToken);

            var balance = await _studentRepository.GetPrepaidBalanceByCustomerIdAsync(
                student.CustomerId,
                cancellationToken);

            return new PosManualTopupResponse
            {
                IsSuccess = true,
                Message = "Manual top-up completed.",
                OrderId = orderId,
                TransactionId = transactionId,
                CustomerId = student.CustomerId,
                StudentName = student.StudentName,
                Amount = request.Amount,
                Balance = balance
            };
        }
        catch (Exception ex)
        {
            return Fail(string.IsNullOrWhiteSpace(ex.Message)
                ? "Manual top-up failed."
                : ex.Message);
        }
    }

    private static PosManualTopupResponse Fail(string message) =>
        new() { IsSuccess = false, Message = message };
}

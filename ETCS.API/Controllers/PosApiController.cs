using ETCS.Shared.Application.Pos;
using ETCS.Shared.Infrastructure.Pos;
using ETCS.Shared.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ETCS.API.Controllers;

[ApiController]
[Route("api/pos")]
public sealed class PosApiController : ControllerBase
{
    private readonly IPosTerminalRepository _terminalRepository;
    private readonly IPosCatalogRepository _catalogRepository;
    private readonly IPosSpendRepository _spendRepository;
    private readonly IPosLegacyTransactionRepository _legacyRepository;
    private readonly PosOptions _posOptions;

    public PosApiController(
        IPosTerminalRepository terminalRepository,
        IPosCatalogRepository catalogRepository,
        IPosSpendRepository spendRepository,
        IPosLegacyTransactionRepository legacyRepository,
        IOptions<PosOptions> posOptions)
    {
        _terminalRepository = terminalRepository;
        _catalogRepository = catalogRepository;
        _spendRepository = spendRepository;
        _legacyRepository = legacyRepository;
        _posOptions = posOptions.Value;
    }

    [HttpGet("schools")]
    public async Task<IActionResult> GetSchools(CancellationToken cancellationToken)
    {
        var schools = await _terminalRepository.GetSchoolsAsync(cancellationToken);
        return Ok(schools);
    }

    [HttpGet("terminals")]
    public async Task<IActionResult> GetTerminals([FromQuery] int? schoolId, CancellationToken cancellationToken)
    {
        var terminals = await _terminalRepository.GetTerminalsAsync(schoolId, cancellationToken);
        return Ok(terminals);
    }

    [HttpGet("terminals/{terminalCode}")]
    public async Task<IActionResult> GetTerminal(string terminalCode, CancellationToken cancellationToken)
    {
        var terminal = await _terminalRepository.GetTerminalByCodeAsync(terminalCode, cancellationToken);
        if (terminal is null)
        {
            return NotFound(new { message = "Terminal not found." });
        }

        return Ok(terminal);
    }

    [HttpGet("schools/{schoolId:int}/categories")]
    public async Task<IActionResult> GetCategories(int schoolId, CancellationToken cancellationToken)
    {
        var categories = await _catalogRepository.GetCategoriesAsync(schoolId, cancellationToken);
        return Ok(categories);
    }

    [HttpGet("schools/{schoolId:int}/items")]
    public async Task<IActionResult> GetItems(int schoolId, [FromQuery] int? categoryId, CancellationToken cancellationToken)
    {
        var items = await _catalogRepository.GetItemsAsync(schoolId, categoryId, cancellationToken);
        return Ok(items);
    }

    [HttpGet("schools/{schoolId:int}/categories/{categoryId:int}/items")]
    public async Task<IActionResult> GetItemsByCategory(int schoolId, int categoryId, CancellationToken cancellationToken)
    {
        var items = await _catalogRepository.GetItemsAsync(schoolId, categoryId, cancellationToken);
        return Ok(items);
    }

    [HttpGet("students/{customerId}/spend-info")]
    public async Task<IActionResult> GetSpendInfo(string customerId, CancellationToken cancellationToken)
    {
        var info = await _spendRepository.GetSpendInfoByCustomerIdAsync(
            customerId,
            _posOptions.OrderTypeId,
            cancellationToken);
        if (info is null)
        {
            return NotFound(new { message = "Student not found for customer ID." });
        }

        var now = DateTime.Now;
        var legacy = await _legacyRepository.GetSpendLimitInfoAsync(
            customerId,
            now.Date,
            PosSpendWeekHelper.GetWeekStartDate(now),
            cancellationToken);

        return Ok(new
        {
            info.CustomerId,
            info.StudentId,
            info.DailySpent,
            info.WeeklySpent,
            info.DailySpendLimit,
            info.WeeklySpendLimit,
            info.DailyRemaining,
            info.WeeklyRemaining,
            info.IsDailyLimitExceeded,
            info.IsWeeklyLimitExceeded,
            legacyDailyNet = legacy?.DailyNetSpent ?? 0m,
            legacyWeeklyNet = legacy?.WeeklyNetSpent ?? 0m,
            legacyDailyLimit = legacy?.DailySpendLimit ?? 0m,
            legacyWeeklyLimit = legacy?.WeeklySpendLimit ?? 0m,
            legacyIsDailyLimitExceeded = legacy?.IsDailyLimitExceeded ?? false,
            legacyIsWeeklyLimitExceeded = legacy?.IsWeeklyLimitExceeded ?? false
        });
    }

    [HttpPost("spend-limit/rollback")]
    public async Task<IActionResult> RollbackSpendLimit(
        [FromBody] PosSpendLimitRollbackRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            return BadRequest(new { message = "CustomerId is required." });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount is required." });
        }

        var ok = await _legacyRepository.RollbackSpendLimitAsync(
            request.CustomerId.Trim(),
            request.Amount,
            cancellationToken);

        return Ok(new PosLegacyOperationResponse
        {
            IsSuccess = ok,
            Message = ok ? "Spend limit rollback recorded." : "Spend limit rollback failed."
        });
    }

    [HttpPost("purchases/post-lines")]
    public async Task<IActionResult> PostPurchaseLines(
        [FromBody] PosPostPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            return BadRequest(new { message = "CustomerId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.TransactionId))
        {
            return BadRequest(new { message = "TransactionId is required." });
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            return BadRequest(new { message = "At least one purchase line is required." });
        }

        var now = DateTime.Now;
        var legacy = await _legacyRepository.GetSpendLimitInfoAsync(
            request.CustomerId.Trim(),
            now.Date,
            PosSpendWeekHelper.GetWeekStartDate(now),
            cancellationToken);

        if (legacy is not null)
        {
            if (legacy.IsWeeklyLimitExceeded)
            {
                await _legacyRepository.RollbackSpendLimitAsync(
                    request.CustomerId.Trim(),
                    request.Lines.Sum(l => l.Amount),
                    cancellationToken);
                return BadRequest(new { message = "Weekly spending limit exceeded!", code = "WEEKLY_LIMIT" });
            }

            if (legacy.IsDailyLimitExceeded)
            {
                await _legacyRepository.RollbackSpendLimitAsync(
                    request.CustomerId.Trim(),
                    request.Lines.Sum(l => l.Amount),
                    cancellationToken);
                return BadRequest(new { message = "Daily spending limit exceeded!", code = "DAILY_LIMIT" });
            }
        }

        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.SkuCode) || line.Amount <= 0)
            {
                continue;
            }

            await _legacyRepository.InsertPosPurchaseLineAsync(
                request.CustomerId.Trim(),
                line.SkuCode.Trim(),
                line.Amount,
                now,
                request.TransactionId.Trim(),
                request.IpAddress?.Trim() ?? string.Empty,
                cancellationToken);
        }

        return Ok(new PosLegacyOperationResponse
        {
            IsSuccess = true,
            Message = "POS purchase lines recorded."
        });
    }

    [HttpPost("purchases/cash")]
    public async Task<IActionResult> CashPurchase(
        [FromBody] PosCashPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount is required." });
        }

        var branchCode = ResolveBranchCode(request.BranchCode);
        var terminalNumeric = ResolveTerminalNumeric(request.TerminalCodeNumeric, request.TerminalCode);
        var transactionId = string.IsNullOrWhiteSpace(request.TransactionId)
            ? BuildLegacyTransactionId(terminalNumeric)
            : request.TransactionId.Trim();

        var ok = await _legacyRepository.InsertCashPurchaseAsync(
            request.Amount,
            branchCode,
            terminalNumeric,
            transactionId,
            cancellationToken);

        return Ok(new PosLegacyOperationResponse
        {
            IsSuccess = ok,
            Message = ok ? "Cash purchase recorded." : "Cash purchase failed."
        });
    }

    [HttpPost("purchases/cash/undo")]
    public async Task<IActionResult> UndoCashPurchase(
        [FromBody] PosCashPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount is required." });
        }

        var branchCode = ResolveBranchCode(request.BranchCode);
        var terminalNumeric = ResolveTerminalNumeric(request.TerminalCodeNumeric, request.TerminalCode);
        var transactionId = string.IsNullOrWhiteSpace(request.TransactionId)
            ? BuildLegacyTransactionId(terminalNumeric)
            : request.TransactionId.Trim();

        var ok = await _legacyRepository.UndoCashPurchaseAsync(
            request.Amount,
            branchCode,
            terminalNumeric,
            transactionId,
            cancellationToken);

        return Ok(new PosLegacyOperationResponse
        {
            IsSuccess = ok,
            Message = ok ? "Undo cash purchase recorded." : "Undo cash purchase failed."
        });
    }

    [HttpPost("purchases/card")]
    public async Task<IActionResult> CardPurchase(
        [FromBody] PosCashPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount is required." });
        }

        if (string.IsNullOrWhiteSpace(request.CreditCardNumber))
        {
            return BadRequest(new { message = "Credit card number is required." });
        }

        var branchCode = ResolveBranchCode(request.BranchCode);
        var terminalNumeric = ResolveTerminalNumeric(request.TerminalCodeNumeric, request.TerminalCode);
        var transactionId = string.IsNullOrWhiteSpace(request.TransactionId)
            ? BuildLegacyTransactionId(terminalNumeric)
            : request.TransactionId.Trim();

        var ok = await _legacyRepository.InsertCardPurchaseAsync(
            request.Amount,
            branchCode,
            terminalNumeric,
            transactionId,
            request.CreditCardNumber.Trim(),
            cancellationToken);

        return Ok(new PosLegacyOperationResponse
        {
            IsSuccess = ok,
            Message = ok ? "Card purchase recorded." : "Card purchase failed."
        });
    }

    private string ResolveBranchCode(string? branchCode) =>
        string.IsNullOrWhiteSpace(branchCode)
            ? (_posOptions.DefaultBranchCode ?? "1")
            : branchCode.Trim();

    private static int ResolveTerminalNumeric(int terminalCodeNumeric, string? terminalCode)
    {
        if (terminalCodeNumeric > 0)
        {
            return terminalCodeNumeric;
        }

        if (!string.IsNullOrWhiteSpace(terminalCode))
        {
            var digits = new string(terminalCode.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var parsed) && parsed > 0)
            {
                return parsed;
            }
        }

        return 1;
    }

    private static string BuildLegacyTransactionId(int terminalNumeric)
    {
        var now = DateTime.Now;
        return terminalNumeric + now.ToString("ddMMyymmss");
    }
}

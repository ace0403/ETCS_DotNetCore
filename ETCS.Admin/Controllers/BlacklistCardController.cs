using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Admin.Master.BlacklistCards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class BlacklistCardController : Controller
{
    private readonly IBlacklistCardAdminRepository _repository;
    private readonly IAdminSchoolScopeService _schoolScope;

    public BlacklistCardController(
        IBlacklistCardAdminRepository repository,
        IAdminSchoolScopeService schoolScope)
    {
        _repository = repository;
        _schoolScope = schoolScope;
    }

    public IActionResult Index() => View();

    [HttpPost]
    public async Task<JsonResult> GetList(string customerId, CancellationToken cancellationToken)
    {
        var validationError = ValidateCustomerId(customerId);
        if (validationError is not null)
        {
            return validationError;
        }

        var scopeError = await EnsureCardInScopeAsync(customerId, cancellationToken);
        if (scopeError is not null)
        {
            return scopeError;
        }

        var result = await _repository.GetLinkedCardsAsync(customerId, cancellationToken);
        return Json(result);
    }

    [HttpPost]
    public async Task<JsonResult> Blacklist(string customerId, CancellationToken cancellationToken)
    {
        var validationError = ValidateCustomerId(customerId);
        if (validationError is not null)
        {
            return validationError;
        }

        var scopeError = await EnsureCardInScopeAsync(customerId, cancellationToken);
        if (scopeError is not null)
        {
            return scopeError;
        }

        if (!User.TryGetLoginAccountId(out var accountId))
        {
            return Json(new { Success = false, Message = "Your session has expired. Please sign in again." });
        }

        var result = await _repository.BlacklistAsync(
            new BlacklistCardRequest
            {
                CustomerId = customerId,
                PerformedBy = accountId.ToString()
            },
            cancellationToken);

        return Json(new { Success = result.Success, Message = result.Message });
    }

    [HttpPost]
    public async Task<JsonResult> Transfer(string customerId, string cardSn, CancellationToken cancellationToken)
    {
        var validationError = ValidateCustomerId(customerId) ?? ValidateCustomerId(cardSn);
        if (validationError is not null)
        {
            return validationError;
        }

        var scopeError = await EnsureCardInScopeAsync(customerId, cancellationToken);
        if (scopeError is not null)
        {
            return scopeError;
        }

        if (!User.TryGetLoginAccountId(out var accountId))
        {
            return Json(new { Success = false, Message = "Your session has expired. Please sign in again." });
        }

        var result = await _repository.TransferBalanceAsync(
            new BlacklistCardTransferRequest
            {
                CustomerId = customerId,
                CardSn = cardSn,
                PerformedBy = accountId.ToString()
            },
            cancellationToken);

        return Json(new { Success = result.Success, Message = result.Message });
    }

    private static JsonResult? ValidateCustomerId(string? customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return new JsonResult(new { Success = false, Message = "Student card number required." });
        }

        return null;
    }

    private async Task<JsonResult?> EnsureCardInScopeAsync(string customerId, CancellationToken cancellationToken)
    {
        if (_schoolScope.IsUnrestricted)
        {
            return null;
        }

        var schoolId = await _repository.GetStudentSchoolIdAsync(customerId, cancellationToken);
        if (schoolId is null)
        {
            return Json(new { Success = false, Message = "You do not have access to this card." });
        }

        try
        {
            _schoolScope.EnsureInScope(schoolId.Value);
        }
        catch (UnauthorizedAccessException)
        {
            return Json(new { Success = false, Message = "You do not have access to this school." });
        }

        return null;
    }
}

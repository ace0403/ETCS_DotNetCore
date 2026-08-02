using ETCS.Pos.Web.Services;
using ETCS.Shared.Infrastructure.Pos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Pos.Web.Controllers;

[Authorize]
[Route("Pos/Api")]
public sealed class PosApiProxyController : ControllerBase
{
    private readonly IPosApiProxyService _proxy;

    public PosApiProxyController(IPosApiProxyService proxy)
    {
        _proxy = proxy;
    }

    [HttpGet("Students/{customerId}/SpendInfo")]
    public Task<IActionResult> SpendInfo(string customerId, CancellationToken cancellationToken)
    {
        var path = "api/pos/students/" + Uri.EscapeDataString(customerId) + "/spend-info";
        return ProxyGetAsync(path, cancellationToken);
    }

    [HttpPost("SpendLimit/Rollback")]
    public Task<IActionResult> RollbackSpendLimit(
        [FromBody] PosSpendLimitRollbackRequest request,
        CancellationToken cancellationToken)
    {
        return ProxyPostAsync("api/pos/spend-limit/rollback", request, cancellationToken);
    }

    [HttpPost("Purchases/PostLines")]
    public Task<IActionResult> PostPurchaseLines(
        [FromBody] PosPostPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        return ProxyPostAsync("api/pos/purchases/post-lines", request, cancellationToken);
    }

    [HttpPost("Purchases/Cash")]
    public Task<IActionResult> CashPurchase(
        [FromBody] PosCashPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        return ProxyPostAsync("api/pos/purchases/cash", request, cancellationToken);
    }

    [HttpPost("Purchases/Cash/Undo")]
    public Task<IActionResult> UndoCashPurchase(
        [FromBody] PosCashPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        return ProxyPostAsync("api/pos/purchases/cash/undo", request, cancellationToken);
    }

    [HttpPost("Purchases/Card")]
    public Task<IActionResult> CardPurchase(
        [FromBody] PosCashPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        return ProxyPostAsync("api/pos/purchases/card", request, cancellationToken);
    }

    private async Task<IActionResult> ProxyGetAsync(string path, CancellationToken cancellationToken)
    {
        var result = await _proxy.ProxyGetAsync(path, cancellationToken);
        return ToActionResult(result);
    }

    private async Task<IActionResult> ProxyPostAsync(string path, object body, CancellationToken cancellationToken)
    {
        var result = await _proxy.ProxyPostAsync(path, body, cancellationToken);
        return ToActionResult(result);
    }

    private static ContentResult ToActionResult(PosApiProxyResponse result)
    {
        return new ContentResult
        {
            StatusCode = result.StatusCode,
            Content = result.Content,
            ContentType = result.ContentType
        };
    }
}

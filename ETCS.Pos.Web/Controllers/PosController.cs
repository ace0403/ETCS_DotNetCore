using ETCS.Pos.Web.Models;
using ETCS.Pos.Web.Options;
using ETCS.Pos.Web.Services;
using ETCS.Shared.Infrastructure.Pos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace ETCS.Pos.Web.Controllers;

[Authorize]
public sealed class PosController : Controller
{
    private readonly IPosApiProxyService _proxy;
    private readonly IBridgeSetupFileResolver _bridgeSetupResolver;
    private readonly PosWebOptions _options;

    public PosController(
        IPosApiProxyService proxy,
        IBridgeSetupFileResolver bridgeSetupResolver,
        IOptions<PosWebOptions> options)
    {
        _proxy = proxy;
        _bridgeSetupResolver = bridgeSetupResolver;
        _options = options.Value;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var (schools, apiError) = await _proxy.GetAsync<List<PosSchoolDto>>("api/pos/schools", cancellationToken);
        schools ??= [];
        var apiOnline = string.IsNullOrWhiteSpace(apiError) && schools.Count > 0;
        var schoolItems = schools.Count == 0
            ? [new SelectListItem("Select school", "")]
            : schools
                .Select((s, i) => new SelectListItem(s.SchoolName, s.SchoolId.ToString(), selected: i == 0))
                .ToList();

        var bridgeSetupAvailable = _bridgeSetupResolver.IsAvailable;
        var model = new PosPageViewModel
        {
            Schools = schoolItems,
            Terminals = [new SelectListItem("Select terminal", "")],
            BridgeBaseUrl = _options.BridgeBaseUrl.TrimEnd('/'),
            BridgeSetupAvailable = bridgeSetupAvailable,
            BridgeSetupDownloadUrl = bridgeSetupAvailable
                ? Url.Action(nameof(DownloadBridgeSetup), "Pos") ?? string.Empty
                : string.Empty,
            VatPercent = _options.VatPercent,
            DefaultDiscount = _options.DefaultDiscount,
            ApiOnline = apiOnline,
            ApiStatusMessage = apiOnline ? string.Empty : ToStaffApiMessage(apiError),
            ApiStatusDetail = apiError ?? string.Empty
        };

        return View(model);
    }

    private static string ToStaffApiMessage(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return "Branch menus could not be loaded. Try again or ask your supervisor.";
        }

        if (error.Contains("Cannot reach ETCS.API", StringComparison.OrdinalIgnoreCase)
            || error.Contains("502", StringComparison.Ordinal))
        {
            return "This terminal cannot reach the ETCS server. Branch menus and sales are unavailable until the connection is restored. The local bridge may still be connected.";
        }

        return "Sales data is temporarily unavailable. Try again or ask your supervisor.";
    }

    [HttpGet]
    public IActionResult DownloadBridgeSetup()
    {
        var path = _bridgeSetupResolver.Resolve();
        if (path is null)
        {
            return NotFound();
        }

        return PhysicalFile(path, "application/octet-stream", BridgeSetupFileResolver.SetupFileName);
    }

    [HttpPost]
    public async Task<IActionResult> LoadTerminals(int schoolId, CancellationToken cancellationToken)
    {
        var (terminals, _) = await _proxy.GetAsync<List<PosTerminalDto>>(
            "api/pos/terminals?schoolId=" + schoolId,
            cancellationToken);
        terminals ??= [];

        ViewData["SelectedSchoolId"] = schoolId;
        return PartialView("_TerminalList", terminals);
    }

    [HttpPost]
    public async Task<IActionResult> LoadCategories(int schoolId, CancellationToken cancellationToken)
    {
        var (categories, _) = await _proxy.GetAsync<List<PosCategoryDto>>(
            "api/pos/schools/" + schoolId + "/categories",
            cancellationToken);
        categories ??= [];

        ViewData["SelectedSchoolId"] = schoolId;
        return PartialView("_CategoryList", categories);
    }

    [HttpPost]
    public async Task<IActionResult> LoadProducts(int schoolId, int categoryId, CancellationToken cancellationToken)
    {
        var path = categoryId > 0
            ? "api/pos/schools/" + schoolId + "/categories/" + categoryId + "/items"
            : "api/pos/schools/" + schoolId + "/items";

        var (items, _) = await _proxy.GetAsync<List<PosCatalogItemDto>>(path, cancellationToken);
        items ??= [];

        return PartialView("_ProductGrid", items);
    }

    [HttpPost]
    public IActionResult UpdateCart([FromForm] List<PosCartItemViewModel>? items, [FromForm] decimal discountPercent, [FromForm] decimal vatPercent)
    {
        var model = new PosCartViewModel
        {
            Items = items ?? [],
            DiscountPercent = discountPercent,
            VatPercent = vatPercent
        };
        return PartialView("_Cart", model);
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}

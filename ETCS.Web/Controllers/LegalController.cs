using ETCS.Shared.Infrastructure.Legal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ETCS.Web.Controllers;

[AllowAnonymous]
public sealed class LegalController : Controller
{
    private readonly ILegalContentRepository _legalContentRepository;
    private readonly LegalContentCacheClearOptions _cacheClearOptions;

    public LegalController(
        ILegalContentRepository legalContentRepository,
        IOptions<LegalContentCacheClearOptions> cacheClearOptions)
    {
        _legalContentRepository = legalContentRepository;
        _cacheClearOptions = cacheClearOptions.Value;
    }

    [HttpGet]
    [Route("terms")]
    public async Task<IActionResult> Terms(CancellationToken cancellationToken)
    {
        return await RenderAsync(LegalContentKeys.Terms, cancellationToken).ConfigureAwait(false);
    }

    [HttpGet]
    [Route("privacy")]
    public async Task<IActionResult> Privacy(CancellationToken cancellationToken)
    {
        return await RenderAsync(LegalContentKeys.Privacy, cancellationToken).ConfigureAwait(false);
    }

    [HttpGet]
    [Route("cancellation")]
    public async Task<IActionResult> Cancellation(CancellationToken cancellationToken)
    {
        return await RenderAsync(LegalContentKeys.Cancellation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears the in-process legal content cache. Requires header X-Cache-Clear-Key.
    /// </summary>
    [HttpPost]
    [Route("legal/cache/clear")]
    [IgnoreAntiforgeryToken]
    public IActionResult ClearCache()
    {
        var providedKey = Request.Headers[LegalContentCacheClearOptions.HeaderName].ToString();
        if (!LegalContentCacheClearGuard.IsAuthorized(providedKey, _cacheClearOptions.CacheClearKey))
        {
            return Unauthorized(new { message = "Invalid or missing cache clear key." });
        }

        _legalContentRepository.ClearCache();
        return Ok(new { message = "Legal content cache cleared." });
    }

    private async Task<IActionResult> RenderAsync(string contentKey, CancellationToken cancellationToken)
    {
        var content = await _legalContentRepository
            .GetByKeyAsync(contentKey, cancellationToken)
            .ConfigureAwait(false);

        if (content is null)
        {
            return NotFound();
        }

        return View("Content", content);
    }
}

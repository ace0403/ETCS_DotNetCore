using ETCS.Shared.Infrastructure.Legal;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ETCS.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/[controller]")]
public sealed class LegalController : ControllerBase
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

    /// <summary>
    /// Returns all active legal content (Privacy, Terms, Cancellation).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var rows = await _legalContentRepository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false);
        return Ok(rows);
    }

    /// <summary>
    /// Returns a single legal content document by key (Privacy, Terms, or Cancellation).
    /// </summary>
    [HttpGet("{contentKey}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByKey(
        string contentKey,
        CancellationToken cancellationToken = default)
    {
        if (!LegalContentKeys.IsKnown(contentKey))
        {
            return BadRequest(new
            {
                message = $"Unknown contentKey '{contentKey}'. Expected one of: {string.Join(", ", LegalContentKeys.All)}."
            });
        }

        var row = await _legalContentRepository.GetByKeyAsync(contentKey, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return NotFound(new { message = $"Legal content '{contentKey}' was not found." });
        }

        return Ok(row);
    }

    /// <summary>
    /// Clears the in-process legal content cache. Requires header X-Cache-Clear-Key.
    /// </summary>
    [HttpPost("cache/clear")]
    [AllowAnonymous]
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
}

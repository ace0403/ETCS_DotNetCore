using ETCS.Shared.Infrastructure.Data;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/[controller]")]
[Authorize]
public sealed class DbDiagnosticsController : ControllerBase
{
    private readonly IDbHealthRepository _dbHealthRepository;

    public DbDiagnosticsController(IDbHealthRepository dbHealthRepository)
    {
        _dbHealthRepository = dbHealthRepository;
    }

    [HttpGet("ping")]
    public async Task<IActionResult> Ping(CancellationToken cancellationToken)
    {
        var result = await _dbHealthRepository.PingAsync(cancellationToken);
        return Ok(new { sqlServerPing = result, utcNow = DateTime.UtcNow });
    }
}

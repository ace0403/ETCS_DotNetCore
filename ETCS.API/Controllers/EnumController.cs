using ETCS.Shared.Infrastructure.Enums;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/[controller]")]
[Authorize]
public sealed class EnumController : ControllerBase
{
    private readonly IEnumRepository _enumRepository;

    public EnumController(IEnumRepository enumRepository)
    {
        _enumRepository = enumRepository;
    }

    /// <summary>
    /// Gets active enum types from EnumTypes table.
    /// </summary>
    [HttpGet("types")]
    public async Task<IActionResult> GetActiveTypes(CancellationToken cancellationToken)
    {
        var rows = await _enumRepository.GetActiveTypeListAsync(cancellationToken);
        return Ok(rows);
    }

    /// <summary>
    /// Gets enum details by one or more type ids.
    /// </summary>
    [HttpGet("by-type-ids")]
    public async Task<IActionResult> GetByTypeIds(
        [FromQuery] int[] id,
        CancellationToken cancellationToken)
    {
        var typeIds = id?.Where(x => x > 0).Distinct().ToArray() ?? [];
        if (typeIds.Length == 0)
        {
            return BadRequest(new { message = "At least one valid type id is required." });
        }

        var rows = await _enumRepository.GetByTypeIdsAsync(typeIds, cancellationToken);
        return Ok(rows);
    }

    /// <summary>
    /// Gets enum detail by primary key id.
    /// </summary>
    [HttpGet("by-id/{id:int}")]
    public async Task<IActionResult> GetById(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Id must be greater than zero." });
        }

        var row = await _enumRepository.GetByIdAsync(id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Enum detail not found." });
        }

        return Ok(row);
    }
}

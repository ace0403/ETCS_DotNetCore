using Asp.Versioning;
using Azure.Core;
using ETCS.API.Infrastructure.Caching;
using ETCS.Shared.Infrastructure.Meals;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Students;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace ETCS.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/[controller]")]
[Authorize]
public sealed class MealController : ControllerBase
{
    private readonly IMealRepository _mealRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MealController> _logger;
    private readonly MealOrderBookingWindow _bookingWindow;

    public MealController(
        IMealRepository mealRepository,
        IStudentRepository studentRepository,
        IMemoryCache cache,
        MealOrderBookingWindow bookingWindow,
        ILogger<MealController> logger)
    {
        _mealRepository = mealRepository;
        _studentRepository = studentRepository;
        _cache = cache;
        _logger = logger;
        _bookingWindow = bookingWindow;
    }

    /// <summary>
    /// Gets meal items for a student on a specific date.
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetMealList(
        [FromQuery] int studentId,
        [FromQuery] DateTime mealDate,
        [FromQuery] int? mealSessionId = null,
        [FromQuery] int? mealTypeId = null,
        [FromQuery] bool slim = false,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0)
        {
            return BadRequest(new { message = "StudentId is required" });
        }

        if (mealDate == default)
        {
            return BadRequest(new { message = "MealDate is required." });
        }

        var schoolId = await _studentRepository.GetStudentSchoolIdAsync(studentId, cancellationToken);
        if (schoolId is null or <= 0)
        {
            return BadRequest(new { message = "Unable to resolve school for this student." });
        }

        if (!_bookingWindow.IsBookable(mealDate))
        {
            return BadRequest(new { message = "No items available for this date. The 3:00 PM cutoff has passed." });
        }

        var cacheKey = CachedMealRepository.BuildItemsCacheKey(studentId, schoolId.Value, mealDate, mealSessionId, mealTypeId);
        var cacheHit = _cache.TryGetValue(cacheKey, out _);
        var stopwatch = Stopwatch.StartNew();

        var items = await _mealRepository.GetMealItemsForStudentAsync(
            studentId,
            schoolId.Value,
            mealDate,
            mealSessionId,
            mealTypeId,
            cancellationToken);

        stopwatch.Stop();
        _logger.LogDebug(
            "Meal list studentId={StudentId} date={MealDate} mealSessionId={MealSessionId} mealTypeId={MealTypeId} took {ElapsedMs}ms ({CacheState})",
            studentId,
            mealDate.Date,
            mealSessionId,
            mealTypeId,
            stopwatch.ElapsedMilliseconds,
            cacheHit ? "cache hit" : "cache miss");

        Console.Write(string.Format("Meal list studentId={0} date={1} mealSessionId={2} mealTypeId={3} took {4}ms ({5})",
            studentId,
            mealDate.Date,
            mealSessionId,
            mealTypeId,
            stopwatch.ElapsedMilliseconds,
            cacheHit ? "cache hit" : "cache miss"));

        if (slim)
        {
            return Ok(items.Select(MealDtoMapper.ToSlim));
        }

        return Ok(items);
    }

    /// <summary>
    /// Gets meal packages for a student on a specific date.
    /// </summary>
    [HttpGet("packages")]
    public async Task<IActionResult> GetMealPackages(
        [FromQuery] int studentId,
        [FromQuery] DateTime mealDate,
        [FromQuery] int? mealSessionId = null,
        [FromQuery] int? mealTypeId = null,
        [FromQuery] bool slim = false,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0)
        {
            return BadRequest(new { message = "StudentId is required" });
        }

        if (mealDate == default)
        {
            return BadRequest(new { message = "MealDate is required." });
        }

        if (!_bookingWindow.IsBookable(mealDate))
        {
            return BadRequest(new { message = "No items available for this date. The 3:00 PM cutoff has passed." });
        }

        var schoolId = await _studentRepository.GetStudentSchoolIdAsync(studentId, cancellationToken);
        if (schoolId is null or <= 0)
        {
            return BadRequest(new { message = "Unable to resolve school for this student." });
        }

        var cacheKey = CachedMealRepository.BuildPackagesCacheKey(studentId, schoolId.Value, mealDate, mealSessionId, mealTypeId);
        var cacheHit = _cache.TryGetValue(cacheKey, out _);
        var stopwatch = Stopwatch.StartNew();

        var packages = await _mealRepository.GetMealPackagesForStudentAsync(
            studentId,
            schoolId.Value,
            mealDate,
            mealSessionId,
            mealTypeId,
            cancellationToken);

        stopwatch.Stop();
        _logger.LogDebug(
            "Meal packages studentId={StudentId} date={MealDate} mealSessionId={MealSessionId} mealTypeId={MealTypeId} took {ElapsedMs}ms ({CacheState})",
            studentId,
            mealDate.Date,
            mealSessionId,
            mealTypeId,
            stopwatch.ElapsedMilliseconds,
            cacheHit ? "cache hit" : "cache miss");

        if (slim)
        {
            return Ok(packages.Select(MealDtoMapper.ToSlim));
        }

        return Ok(packages);
    }
}

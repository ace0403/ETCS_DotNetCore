using ETCS.Admin.Infrastructure.Auth;
using ETCS.Admin.Infrastructure.MealItems;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;
using ETCS.Shared.Infrastructure.Admin.Master.Students;
using ETCS.Shared.Infrastructure.Admin.Inventory.Categories;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Media;
using ETCS.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class MealItemController : Controller
{
    private readonly IMealItemAdminRepository _repository;
    private readonly IStudentAdminRepository _studentAdminRepository;
    private readonly ICategoryAdminRepository _categoryAdminRepository;
    private readonly IMealEnumAdminRepository _mealEnumAdminRepository;
    private readonly IMealImageStorageService _imageStorageService;
    private readonly IAdminSchoolScopeService _schoolScope;
    private readonly IMealItemExcelImportService _importService;
    private readonly IMealItemImportPreviewCache _importPreviewCache;

    public MealItemController(
        IMealItemAdminRepository repository,
        IStudentAdminRepository studentAdminRepository,
        ICategoryAdminRepository categoryAdminRepository,
        IMealEnumAdminRepository mealEnumAdminRepository,
        IMealImageStorageService imageStorageService,
        IAdminSchoolScopeService schoolScope,
        IMealItemExcelImportService importService,
        IMealItemImportPreviewCache importPreviewCache)
    {
        _repository = repository;
        _studentAdminRepository = studentAdminRepository;
        _categoryAdminRepository = categoryAdminRepository;
        _mealEnumAdminRepository = mealEnumAdminRepository;
        _imageStorageService = imageStorageService;
        _schoolScope = schoolScope;
        _importService = importService;
        _importPreviewCache = importPreviewCache;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var schools = await _studentAdminRepository.SchoolLookupsAsync(cancellationToken);
        ViewBag.Schools = _schoolScope.FilterSchools(schools, s => s.Id);
        return View();
    }

    [HttpPost]
    public async Task<JsonResult> GetList([FromForm] DataTableRequest request, CancellationToken cancellationToken)
    {
        _schoolScope.ApplyListScope(request);
        var response = await _repository.GetDataAsync(request, cancellationToken);
        return Json(response);
    }

    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        await PopulateLookupsAsync(cancellationToken);
        var model = id > 0
            ? await _repository.GetAsync(id, cancellationToken) ?? new MealItemSaveRequest()
            : new MealItemSaveRequest
            {
                OrderTypeIds = MealItemChannelOptionIds.MenuPair.ToList()
            };
        return PartialView("_AddUpdate", model);
    }

    [HttpPost]
    public async Task<JsonResult> Save(
        [FromForm] MealItemSaveRequest model,
        IFormFile? imageFile,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { Success = false, Message = "Required fields are missing." });
        }

        try
        {
            EnsureSchoolsInScope(model.SchoolIds);
        }
        catch (UnauthorizedAccessException)
        {
            return Json(new { Success = false, Message = "You do not have access to this school." });
        }

        if (imageFile is { Length: > 0 })
        {
            if (!string.IsNullOrWhiteSpace(model.ImageName))
            {
                await _imageStorageService.DeleteAsync(MealImageKind.MealItem, model.ImageName, cancellationToken);
            }

            var imageFileName = await _imageStorageService.SaveAsync(imageFile, MealImageKind.MealItem, cancellationToken);
            if (imageFileName is null)
            {
                return Json(new { Success = false, Message = "Image could not be saved." });
            }

            model.ImageName = imageFileName;
        }

        if (User.TryGetLoginAccountId(out var accountId))
        {
            model.CreatedBy ??= accountId;
            model.UpdatedBy = accountId;
        }

        var result = await _repository.SaveAsync(model, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    public async Task<JsonResult> Delete(int id, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetAsync(id, cancellationToken);
        if (existing is not null)
        {
            try
            {
                EnsureAnySchoolInScope(existing.SchoolIds);
            }
            catch (UnauthorizedAccessException)
            {
                return Json(new { Success = false, Message = "You do not have access to this school." });
            }
        }

        var result = await _repository.DeleteAsync(id, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    public async Task<JsonResult> GetMealTypes(int sessionId, CancellationToken cancellationToken)
    {
        var data = sessionId > 0
            ? await _mealEnumAdminRepository.GetMealTypesBySessionAsync(sessionId, cancellationToken)
            : [];
        return Json(new { data });
    }

    public async Task<IActionResult> Import(CancellationToken cancellationToken)
    {
        var schools = await _studentAdminRepository.SchoolLookupsAsync(cancellationToken);
        ViewBag.Schools = _schoolScope.FilterSchools(schools, s => s.Id);
        ViewBag.MealSessions = await _mealEnumAdminRepository.GetMealSessionsAsync(cancellationToken);
        return PartialView("_Import");
    }

    [HttpPost]
    public async Task<JsonResult> ImportPreview(
        IFormFile? file,
        [FromForm] int schoolId,
        [FromForm] int mealSessionId,
        [FromForm] int mealTypeId,
        [FromForm] bool createMissingCategories,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Json(new MealItemImportPreviewResult
            {
                Success = false,
                Message = "Please select an Excel file."
            });
        }

        if (!IsExcelFile(file))
        {
            return Json(new MealItemImportPreviewResult
            {
                Success = false,
                Message = "Only .xlsx files are supported."
            });
        }

        try
        {
            _schoolScope.EnsureInScope(schoolId);
        }
        catch (UnauthorizedAccessException)
        {
            return Json(new MealItemImportPreviewResult
            {
                Success = false,
                Message = "You do not have access to this school."
            });
        }

        if (schoolId <= 0 || mealSessionId <= 0 || mealTypeId <= 0)
        {
            return Json(new MealItemImportPreviewResult
            {
                Success = false,
                Message = "School, meal session, and meal type are required."
            });
        }

        if (!await _mealEnumAdminRepository.IsMealTypeInSessionAsync(mealTypeId, mealSessionId, cancellationToken))
        {
            return Json(new MealItemImportPreviewResult
            {
                Success = false,
                Message = "Selected meal type does not belong to the chosen meal session."
            });
        }

        await using var stream = file.OpenReadStream();

        int? createdBy = null;
        if (User.TryGetLoginAccountId(out var accountId))
        {
            createdBy = accountId;
        }

        var parseResult = await _importService.ParseAsync(
            stream,
            schoolId,
            mealSessionId,
            mealTypeId,
            createMissingCategories,
            createdBy,
            cancellationToken);
        if (!parseResult.Success)
        {
            return Json(new MealItemImportPreviewResult
            {
                Success = false,
                Message = parseResult.Message,
                Warnings = parseResult.Warnings
            });
        }

        var previewRows = new List<MealItemImportPreviewRow>();
        var toInsert = new List<MealItemSaveRequest>();
        var skippedExisting = 0;
        var skippedInvalid = 0;

        foreach (var item in parseResult.Items)
        {
            if (!item.IsValid || item.Request is null)
            {
                skippedInvalid++;
                previewRows.Add(new MealItemImportPreviewRow
                {
                    ItemName = item.ItemName,
                    CategoryName = item.CategoryName,
                    WeekNos = item.WeekNos,
                    DayNames = item.DayNames,
                    Status = MealItemImportRowStatus.Invalid,
                    Message = string.IsNullOrWhiteSpace(item.Message) ? "Invalid row." : item.Message
                });
                continue;
            }

            var mealCategoryId = item.Request.MealCategoryId!.Value;
            if (await _repository.ExistsAsync(schoolId, mealTypeId, item.ItemName, mealCategoryId, cancellationToken))
            {
                skippedExisting++;
                previewRows.Add(new MealItemImportPreviewRow
                {
                    ItemName = item.ItemName,
                    CategoryName = item.CategoryName,
                    WeekNos = item.WeekNos,
                    DayNames = item.DayNames,
                    Status = MealItemImportRowStatus.Exists,
                    Message = "Already exists."
                });
                continue;
            }

            toInsert.Add(item.Request);
            previewRows.Add(new MealItemImportPreviewRow
            {
                ItemName = item.ItemName,
                CategoryName = item.CategoryName,
                WeekNos = item.WeekNos,
                DayNames = item.DayNames,
                Status = MealItemImportRowStatus.Ready,
                Message = "Ready to import."
            });
        }

        int? previewCreatedBy = null;
        if (User.TryGetLoginAccountId(out var previewAccountId))
        {
            previewCreatedBy = previewAccountId;
        }

        var importToken = toInsert.Count > 0
            ? _importPreviewCache.Store(new MealItemImportCacheEntry
            {
                SchoolId = schoolId,
                MealSessionId = mealSessionId,
                MealTypeId = mealTypeId,
                CreatedBy = previewCreatedBy,
                Items = toInsert
            })
            : null;

        return Json(new MealItemImportPreviewResult
        {
            Success = true,
            Message = "Preview generated successfully.",
            ParsedCount = parseResult.Items.Count,
            ToInsert = toInsert.Count,
            SkippedExisting = skippedExisting,
            SkippedInvalid = skippedInvalid,
            CategoriesCreated = parseResult.CategoriesCreated.Count,
            CreatedCategoryNames = parseResult.CategoriesCreated,
            Warnings = parseResult.Warnings,
            Rows = previewRows,
            ImportToken = importToken
        });
    }

    [HttpPost]
    public async Task<JsonResult> ImportConfirm([FromForm] string importToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(importToken))
        {
            return Json(new MealItemImportConfirmResult
            {
                Success = false,
                Message = "Import preview has expired. Please preview again."
            });
        }

        var cacheEntry = _importPreviewCache.Get(importToken);
        if (cacheEntry is null)
        {
            return Json(new MealItemImportConfirmResult
            {
                Success = false,
                Message = "Import preview has expired. Please preview again."
            });
        }

        try
        {
            _schoolScope.EnsureInScope(cacheEntry.SchoolId);
        }
        catch (UnauthorizedAccessException)
        {
            return Json(new MealItemImportConfirmResult
            {
                Success = false,
                Message = "You do not have access to this school."
            });
        }

        var createdBy = cacheEntry.CreatedBy ?? 0;
        if (User.TryGetLoginAccountId(out var accountId))
        {
            createdBy = accountId;
        }

        var result = await _repository.ImportAsync(cacheEntry.Items, createdBy, cancellationToken);
        _importPreviewCache.Remove(importToken);

        var success = result.Failed == 0;
        var message = result.Inserted > 0
            ? $"Imported {result.Inserted} item(s)."
            : result.SkippedExisting > 0
                ? "No new items were imported. All previewed items already exist."
                : "No items were imported.";

        if (result.Failed > 0)
        {
            message = $"Imported {result.Inserted} item(s) with {result.Failed} failure(s).";
        }

        return Json(new MealItemImportConfirmResult
        {
            Success = success,
            Message = message,
            Inserted = result.Inserted,
            SkippedExisting = result.SkippedExisting,
            Failed = result.Failed,
            Errors = result.Errors
        });
    }

    private static bool IsExcelFile(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        return string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase);
    }

    private async Task PopulateLookupsAsync(CancellationToken cancellationToken)
    {
        var schools = await _studentAdminRepository.SchoolLookupsAsync(cancellationToken);
        ViewBag.Schools = _schoolScope.FilterSchools(schools, s => s.Id);
        ViewBag.Categories = await _categoryAdminRepository.ListAsync(cancellationToken);
        ViewBag.MealSessions = await _mealEnumAdminRepository.GetMealSessionsAsync(cancellationToken);
        ViewBag.Ingredients = await _mealEnumAdminRepository.GetByTypeIdAsync(MealEnumTypeIds.FoodAllergy, cancellationToken);
        ViewBag.WeekDays = await _mealEnumAdminRepository.GetByTypeIdAsync(MealEnumTypeIds.WeekDays, cancellationToken);
        ViewBag.NutritionTypes = await _mealEnumAdminRepository.GetByTypeIdAsync(MealEnumTypeIds.Nutrition, cancellationToken);
        ViewBag.MeasureTypes = await _mealEnumAdminRepository.GetByTypeIdAsync(MealEnumTypeIds.MeasureType, cancellationToken);
        ViewBag.WeekNumbers = Enumerable.Range(1, 5).ToList();
        ViewBag.MealItemChannels = MealItemChannelOptionIds.Ordered
            .Select(id => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                MealItemChannelOptionIds.DisplayName(id),
                id.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
    }

    private void EnsureSchoolsInScope(IEnumerable<int> schoolIds)
    {
        foreach (var schoolId in schoolIds.Where(id => id > 0).Distinct())
        {
            _schoolScope.EnsureInScope(schoolId);
        }
    }

    private void EnsureAnySchoolInScope(IEnumerable<int> schoolIds)
    {
        var ids = schoolIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        foreach (var schoolId in ids)
        {
            try
            {
                _schoolScope.EnsureInScope(schoolId);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                // Try next linked school.
            }
        }

        throw new UnauthorizedAccessException();
    }
}

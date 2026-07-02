using ETCS.Admin.Infrastructure.Auth;
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

    public MealItemController(
        IMealItemAdminRepository repository,
        IStudentAdminRepository studentAdminRepository,
        ICategoryAdminRepository categoryAdminRepository,
        IMealEnumAdminRepository mealEnumAdminRepository,
        IMealImageStorageService imageStorageService,
        IAdminSchoolScopeService schoolScope)
    {
        _repository = repository;
        _studentAdminRepository = studentAdminRepository;
        _categoryAdminRepository = categoryAdminRepository;
        _mealEnumAdminRepository = mealEnumAdminRepository;
        _imageStorageService = imageStorageService;
        _schoolScope = schoolScope;
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
            : new MealItemSaveRequest();
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
            _schoolScope.EnsureInScope(model.SchoolId);
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
                _schoolScope.EnsureInScope(existing.SchoolId);
            }
            catch (UnauthorizedAccessException)
            {
                return Json(new { Success = false, Message = "You do not have access to this school." });
            }
        }

        var result = await _repository.DeleteAsync(id, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    private async Task PopulateLookupsAsync(CancellationToken cancellationToken)
    {
        var schools = await _studentAdminRepository.SchoolLookupsAsync(cancellationToken);
        ViewBag.Schools = _schoolScope.FilterSchools(schools, s => s.Id);
        ViewBag.Categories = await _categoryAdminRepository.ListAsync(cancellationToken);
        ViewBag.MealTypes = await _mealEnumAdminRepository.GetByTypeIdAsync(MealEnumTypeIds.MealType, cancellationToken);
        ViewBag.Ingredients = await _mealEnumAdminRepository.GetByTypeIdAsync(MealEnumTypeIds.FoodAllergy, cancellationToken);
        ViewBag.WeekDays = await _mealEnumAdminRepository.GetByTypeIdAsync(MealEnumTypeIds.WeekDays, cancellationToken);
        ViewBag.NutritionTypes = await _mealEnumAdminRepository.GetByTypeIdAsync(MealEnumTypeIds.Nutrition, cancellationToken);
        ViewBag.MeasureTypes = await _mealEnumAdminRepository.GetByTypeIdAsync(MealEnumTypeIds.MeasureType, cancellationToken);
        ViewBag.WeekNumbers = Enumerable.Range(1, 5).ToList();
    }
}

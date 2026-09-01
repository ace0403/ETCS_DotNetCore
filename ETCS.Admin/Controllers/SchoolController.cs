using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Admin.Master.Schools;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class SchoolController : Controller
{
    private readonly ISchoolAdminRepository _repository;
    private readonly IAdminSchoolScopeService _schoolScope;
    private readonly AdminOptions _adminOptions;
    private readonly IMealEnumAdminRepository _mealEnumRepository;
    private readonly IStudentRepository _studentRepository;

    public SchoolController(
        ISchoolAdminRepository repository,
        IAdminSchoolScopeService schoolScope,
        IOptions<AdminOptions> adminOptions,
        IMealEnumAdminRepository mealEnumRepository,
        IStudentRepository studentRepository)
    {
        _repository = repository;
        _schoolScope = schoolScope;
        _adminOptions = adminOptions.Value;
        _mealEnumRepository = mealEnumRepository;
        _studentRepository = studentRepository;
    }

    public IActionResult Index() => View(new SchoolSaveRequest());

    [HttpPost]
    public async Task<JsonResult> GetList([FromForm] DataTableRequest request, CancellationToken cancellationToken)
    {
        _schoolScope.ApplyListScope(request);
        var response = await _repository.GetDataAsync(request, cancellationToken);
        return Json(response);
    }

    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        ViewBag.Countries = await _repository.CountryLookupsAsync(cancellationToken);
        var orderTypes = await _mealEnumRepository.GetStudentOrderTypesAsync(cancellationToken);
        ViewBag.OrderTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(orderTypes, "Id", "Name");
        var model = id > 0
            ? await _repository.GetAsync(id, cancellationToken) ?? new SchoolSaveRequest()
            : new SchoolSaveRequest();
        var grades = await _studentRepository.GetAllGradesAsync(cancellationToken);
        ViewBag.Grades = FilterGradesForSchool(grades, model);

        if (id > 0)
        {
            _schoolScope.EnsureInScope(model.Id);
        }

        return PartialView("_AddUpdate", model);
    }

    [HttpPost]
    public async Task<JsonResult> Save(
        [FromForm] SchoolSaveRequest model,
        IFormFile? logoFile,
        IFormFile? userGuideFile,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { Success = false, Message = "Required fields are missing." });
        }

        try
        {
            _schoolScope.EnsureInScope(model.Id);
        }
        catch (UnauthorizedAccessException)
        {
            return Json(new { Success = false, Message = "You do not have access to this school." });
        }

        if (logoFile is { Length: > 0 })
        {
            var logoFileName = await SaveUploadAsync(logoFile, "SchoolLogo", cancellationToken);
            if (logoFileName is null)
            {
                return Json(new { Success = false, Message = "School logo could not be saved." });
            }

            model.LogoFileName = logoFileName;
        }

        if (userGuideFile is { Length: > 0 })
        {
            var guideFileName = await SaveUploadAsync(userGuideFile, "UserGuide", cancellationToken);
            if (guideFileName is null)
            {
                return Json(new { Success = false, Message = "User guide could not be saved." });
            }

            model.PdfPath = guideFileName;
        }

        var result = await _repository.SaveAsync(model, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    public async Task<JsonResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            _schoolScope.EnsureInScope(id);
        }
        catch (UnauthorizedAccessException)
        {
            return Json(new { Success = false, Message = "You do not have access to this school." });
        }

        var result = await _repository.DeleteAsync(id, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    private async Task<string?> SaveUploadAsync(IFormFile file, string subFolder, CancellationToken cancellationToken)
    {
        var storePath = _adminOptions.StorePath?.Trim();
        if (string.IsNullOrWhiteSpace(storePath))
        {
            return null;
        }

        var targetDir = Path.Combine(storePath, subFolder);
        Directory.CreateDirectory(targetDir);

        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(targetDir, fileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return fileName;
    }

    private static IReadOnlyList<GradeListItemDto> FilterGradesForSchool(
        IReadOnlyList<GradeListItemDto> grades,
        SchoolSaveRequest model)
    {
        int? schoolCode = null;
        if (!string.IsNullOrWhiteSpace(model.Code)
            && int.TryParse(model.Code.Trim(), out var parsedSchoolCode))
        {
            schoolCode = parsedSchoolCode;
        }

        return grades
            .Where(grade => grade.SchoolCode is null || schoolCode is null || grade.SchoolCode == schoolCode)
            .OrderBy(grade => grade.Grade, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

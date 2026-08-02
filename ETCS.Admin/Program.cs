using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Admin.Auth;
using ETCS.Shared.Infrastructure.Admin.Security;
using ETCS.Shared.Infrastructure.Admin.Inventory.Categories;
using ETCS.Shared.Infrastructure.Admin.Inventory.Ingredients;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealCombos;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;
using ETCS.Shared.Application.Email;
using ETCS.Shared.Application.Notifications;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealServingPeriods;
using ETCS.Shared.Infrastructure.Email;
using ETCS.Shared.Infrastructure.Admin.Master.Guardians;
using ETCS.Shared.Infrastructure.Admin.Master.Schools;
using ETCS.Shared.Infrastructure.Admin.Reports.AdminTransactions;
using ETCS.Shared.Infrastructure.Admin.Reports.CanteenTransactions;
using ETCS.Shared.Infrastructure.Admin.Dashboard;
using ETCS.Shared.Infrastructure.Admin.Reports.MealOrders;
using ETCS.Shared.Infrastructure.Admin.Reports.TerminalSalesSummary;
using ETCS.Shared.Infrastructure.Admin.Master.Staff;
using ETCS.Shared.Infrastructure.Admin.Master.Students;
using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
using ETCS.Shared.Options;
using ETCS.Shared.Media;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<MealDatabaseOptions>(builder.Configuration.GetSection(MealDatabaseOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.ConfigureMediaOptions(builder.Configuration, AdminOptions.SectionName);

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton<IMealDbConnectionFactory, SqlMealConnectionFactory>();

builder.Services.AddScoped<IAdminLoginRepository, AdminLoginRepository>();
builder.Services.AddScoped<IAdminPermissionRepository, AdminPermissionRepository>();
builder.Services.AddScoped<IRoleAdminRepository, RoleAdminRepository>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAdminSchoolScopeService, AdminSchoolScopeService>();
builder.Services.AddScoped<IAdminNavigationService, AdminNavigationService>();
builder.Services.AddScoped<AdminPermissionAuthorizationFilter>();
builder.Services.AddScoped<ISchoolAdminRepository, SchoolAdminRepository>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<IStudentAllergyAdminRepository, StudentAllergyAdminRepository>();
builder.Services.AddScoped<IGuardianAdminRepository, GuardianAdminRepository>();
builder.Services.AddScoped<IStudentAdminRepository, StudentAdminRepository>();
builder.Services.AddScoped<IStaffAdminRepository, StaffAdminRepository>();
builder.Services.AddScoped<ICategoryAdminRepository, CategoryAdminRepository>();
builder.Services.AddScoped<IIngredientAdminRepository, IngredientAdminRepository>();
builder.Services.AddScoped<IMealEnumAdminRepository, MealEnumAdminRepository>();
builder.Services.AddScoped<IMealItemAdminRepository, MealItemAdminRepository>();
builder.Services.AddScoped<IMealComboAdminRepository, MealComboAdminRepository>();
builder.Services.AddScoped<IMealServingPeriodAdminRepository, MealServingPeriodAdminRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddEmailNotificationInfrastructure();
builder.Services.AddGuardianInAppNotificationServices();
builder.Services.AddScoped<ICanteenTransactionReportRepository, CanteenTransactionReportRepository>();
builder.Services.AddScoped<IAdminTransactionReportRepository, AdminTransactionReportRepository>();
builder.Services.AddScoped<ITerminalSalesSummaryReportRepository, TerminalSalesSummaryReportRepository>();
builder.Services.AddScoped<IMealOrderReportRepository, MealOrderReportRepository>();
builder.Services.AddScoped<IMealOrderMealDbReportRepository, MealOrderMealDbReportRepository>();
builder.Services.AddScoped<IAdminDashboardRepository, AdminDashboardRepository>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/Index";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add<AdminPermissionAuthorizationFilter>();
    })
    .AddJsonOptions(options =>
    {
        // DataTables and admin JS expect PascalCase property names (Name, Email, Success).
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.DictionaryKeyPolicy = null;
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

var adminOptions = builder.Configuration.GetSection(AdminOptions.SectionName).Get<AdminOptions>() ?? new AdminOptions();
app.MapMealImageStaticFiles(adminOptions.StorePath);

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? string.Empty;
        if (path.Contains("js/custom", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache";
        }
        else
        {
            ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
    }
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

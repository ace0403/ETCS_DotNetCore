using ETCS.PaymentGateway.DependencyInjection;

using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;

using ETCS.Shared.Infrastructure.Admin.Master.Guardians;

using ETCS.Shared.Infrastructure.Admin.Master.Schools;

using ETCS.Shared.Infrastructure.Admin.Master.Students;

using ETCS.Shared.Infrastructure.Auth;

using ETCS.Shared.Infrastructure.Data;

using ETCS.Shared.Infrastructure.Enums;

using ETCS.Shared.Infrastructure.Legal;

using ETCS.Shared.Infrastructure.Meals;

using ETCS.Shared.Infrastructure.Orders;

using ETCS.Shared.Infrastructure.Students;

using ETCS.Shared.Infrastructure.Schools.Calendar;

using ETCS.Shared.Infrastructure.Transaction;

using ETCS.Shared.Options;

using ETCS.Shared.Application.Background;

using ETCS.Shared.Application.Email;
using ETCS.Shared.Application.Notifications;
using ETCS.Shared.Application.Orders;
using ETCS.Shared.Application.Topup;

using ETCS.Web.Infrastructure.Background;

using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Infrastructure.Navigation;
using ETCS.Web.Infrastructure.Orders;

using ETCS.Web.Options;

using ETCS.Shared.Media;

using Microsoft.AspNetCore.Authentication.Cookies;

using Microsoft.AspNetCore.Rewrite;

using Microsoft.Extensions.FileProviders;



var builder = WebApplication.CreateBuilder(args);



builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services.Configure<MealDatabaseOptions>(builder.Configuration.GetSection(MealDatabaseOptions.SectionName));

builder.Services.Configure<OrderFlowOptions>(builder.Configuration.GetSection(OrderFlowOptions.SectionName));

builder.Services.Configure<WebOptions>(builder.Configuration.GetSection(WebOptions.SectionName));
builder.Services.Configure<ParentPortalOptions>(options =>
{
    options.PublicBaseUrl = builder.Configuration["Web:PublicBaseUrl"] ?? string.Empty;
});
builder.Services.ConfigureMediaOptions(builder.Configuration, WebOptions.SectionName);



builder.Services.AddMemoryCache();

builder.Services.Configure<LegalContentCacheClearOptions>(builder.Configuration.GetSection(LegalContentCacheClearOptions.SectionName));

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

builder.Services.AddSingleton<IMealDbConnectionFactory, SqlMealConnectionFactory>();

builder.Services.AddScoped<IGuardianPasswordResetTokenStore, SqlGuardianPasswordResetTokenStore>();
builder.Services.AddScoped<IGuardianOtpStore, SqlGuardianOtpStore>();
builder.Services.AddScoped<IRegistrationOtpService, RegistrationOtpService>();
builder.Services.AddScoped<IDeleteAccountOtpService, DeleteAccountOtpService>();
builder.Services.AddScoped<IParentLoginRepository, ParentLoginRepository>();

builder.Services.AddScoped<IStudentRepository, StudentRepository>();

builder.Services.AddScoped<IStudentAllergyAdminRepository, StudentAllergyAdminRepository>();

builder.Services.AddScoped<IStudentOrderTypeAdminRepository, StudentOrderTypeAdminRepository>();
builder.Services.AddScoped<ISchoolOrderTypeAdminRepository, SchoolOrderTypeAdminRepository>();

builder.Services.AddScoped<IGuardianAdminRepository, GuardianAdminRepository>();

builder.Services.AddScoped<IMealEnumAdminRepository, MealEnumAdminRepository>();

builder.Services.AddScoped<IEnumRepository, EnumRepository>();

builder.Services.AddScoped<ILegalContentRepository, LegalContentRepository>();

builder.Services.AddScoped<IMealRepository, MealRepository>();

builder.Services.AddScoped<IMealOrderRepository, MealOrderRepository>();

builder.Services.AddScoped<IMainOrderRepository, MainOrderRepository>();

builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

builder.Services.AddScoped<ISchoolCalendarRepository, SchoolCalendarRepository>();
builder.Services.AddScoped<ISchoolCalendarService, SchoolCalendarService>();

builder.Services.AddScoped<IParentPortalNavigationService, ParentPortalNavigationService>();

builder.Services.AddOrderFlowServices();
builder.Services.AddTopupFlowServices();
builder.Services.AddGuardianEmailServices();
builder.Services.AddGuardianInAppNotificationServices();
builder.Services.AddScoped<OrderPaymentSummaryBuilder>();
builder.Services.AddSingleton<MealOrderBookingWindow>();

builder.Services.AddSingleton<IPaymentBackgroundQueue, NoOpPaymentBackgroundQueue>();

builder.Services.AddPaymentGateway(builder.Configuration);



builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)

    .AddCookie(options =>

    {

        options.LoginPath = "/Home/Index";

        options.AccessDeniedPath = "/Home/Index?msg=unauthorize";

        options.ExpireTimeSpan = TimeSpan.FromHours(8);

        options.SlidingExpiration = true;

    });

builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Match Admin / existing custom JS (Success, Message, etc.).
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.DictionaryKeyPolicy = null;
    });



var app = builder.Build();



if (!app.Environment.IsDevelopment())

{

    app.UseExceptionHandler("/Home/ServerError");

    app.UseHsts();

}



app.UseHttpsRedirection();



var webOptions = builder.Configuration.GetSection(WebOptions.SectionName).Get<WebOptions>() ?? new WebOptions();
app.MapMealImageStaticFiles(webOptions.StorePath);



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



app.UseRewriter(new RewriteOptions()
    .AddRedirect(@"^.*\.aspx$", "/", statusCode: StatusCodes.Status301MovedPermanently));

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.UseStatusCodePagesWithReExecute("/Home/PageNotFound", "?code={0}");

app.MapStaticAssets();

app.MapControllerRoute(

    name: "default",

    pattern: "{controller=Home}/{action=Index}/{id?}")

    .WithStaticAssets();



app.Run();



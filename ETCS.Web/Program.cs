using ETCS.PaymentGateway.DependencyInjection;

using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;

using ETCS.Shared.Infrastructure.Admin.Master.Guardians;

using ETCS.Shared.Infrastructure.Admin.Master.Students;

using ETCS.Shared.Infrastructure.Auth;

using ETCS.Shared.Infrastructure.Data;

using ETCS.Shared.Infrastructure.Enums;

using ETCS.Shared.Infrastructure.Meals;

using ETCS.Shared.Infrastructure.Orders;

using ETCS.Shared.Infrastructure.Students;

using ETCS.Shared.Infrastructure.Transaction;

using ETCS.Shared.Options;

using ETCS.Shared.Application.Background;

using ETCS.Shared.Application.Email;
using ETCS.Shared.Application.Orders;

using ETCS.Web.Infrastructure.Background;

using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Infrastructure.Orders;

using ETCS.Web.Options;

using ETCS.Shared.Media;

using Microsoft.AspNetCore.Authentication.Cookies;

using Microsoft.Extensions.FileProviders;



var builder = WebApplication.CreateBuilder(args);



builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services.Configure<MealDatabaseOptions>(builder.Configuration.GetSection(MealDatabaseOptions.SectionName));

builder.Services.Configure<OrderFlowOptions>(builder.Configuration.GetSection(OrderFlowOptions.SectionName));

builder.Services.Configure<WebOptions>(builder.Configuration.GetSection(WebOptions.SectionName));
builder.Services.ConfigureMediaOptions(builder.Configuration, WebOptions.SectionName);



builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

builder.Services.AddSingleton<IMealDbConnectionFactory, SqlMealConnectionFactory>();

builder.Services.AddScoped<IParentLoginRepository, ParentLoginRepository>();

builder.Services.AddScoped<IStudentRepository, StudentRepository>();

builder.Services.AddScoped<IStudentAllergyAdminRepository, StudentAllergyAdminRepository>();

builder.Services.AddScoped<IGuardianAdminRepository, GuardianAdminRepository>();

builder.Services.AddScoped<IMealEnumAdminRepository, MealEnumAdminRepository>();

builder.Services.AddScoped<IEnumRepository, EnumRepository>();

builder.Services.AddScoped<IMealRepository, MealRepository>();

builder.Services.AddScoped<IMealOrderRepository, MealOrderRepository>();

builder.Services.AddScoped<IMainOrderRepository, MainOrderRepository>();

builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

builder.Services.AddOrderFlowServices();
builder.Services.AddGuardianEmailServices();
builder.Services.AddScoped<OrderPaymentSummaryBuilder>();

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

builder.Services.AddControllersWithViews();



var app = builder.Build();



if (!app.Environment.IsDevelopment())

{

    app.UseExceptionHandler("/Home/Error");

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



app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();



app.MapStaticAssets();

app.MapControllerRoute(

    name: "default",

    pattern: "{controller=Home}/{action=Index}/{id?}")

    .WithStaticAssets();



app.Run();



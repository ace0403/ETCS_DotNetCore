using ETCS.Pos.Web.Options;
using ETCS.Pos.Web.Services;
using ETCS.Shared.Infrastructure.Admin.Auth;
using ETCS.Shared.Infrastructure.Admin.Security;
using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Media;
using ETCS.Shared.Options;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PosWebOptions>(builder.Configuration.GetSection(PosWebOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<MealDatabaseOptions>(builder.Configuration.GetSection(MealDatabaseOptions.SectionName));
builder.Services.ConfigureMediaOptions(builder.Configuration, PosWebOptions.SectionName);

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton<IMealDbConnectionFactory, SqlMealConnectionFactory>();
builder.Services.AddScoped<IAdminLoginRepository, AdminLoginRepository>();
builder.Services.AddScoped<IAdminPermissionRepository, AdminPermissionRepository>();

builder.Services.AddScoped<IPosApiProxyService, PosApiProxyService>();
builder.Services.AddScoped<IBridgeSetupFileResolver, BridgeSetupFileResolver>();

var posApiBuilder = builder.Services.AddHttpClient("PosApi", (sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PosWebOptions>>().Value;
    client.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
    if (!string.IsNullOrWhiteSpace(options.ApiKey))
    {
        client.DefaultRequestHeaders.Add("X-API-KEY", options.ApiKey);
    }
});

if (builder.Environment.IsDevelopment())
{
    posApiBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/Index";
        options.AccessDeniedPath = "/Home/Index";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Pos/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

var posWebOptions = builder.Configuration.GetSection(PosWebOptions.SectionName).Get<PosWebOptions>() ?? new PosWebOptions();
app.MapMealImageStaticFiles(posWebOptions.StorePath);

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Pos}/{action=Index}/{id?}");

app.Run();

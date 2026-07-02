using ETCS.Pos.Web.Options;
using ETCS.Pos.Web.Services;
using ETCS.Shared.Media;
using ETCS.Shared.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PosWebOptions>(builder.Configuration.GetSection(PosWebOptions.SectionName));
builder.Services.ConfigureMediaOptions(builder.Configuration, PosWebOptions.SectionName);
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Pos}/{action=Index}/{id?}");

app.Run();

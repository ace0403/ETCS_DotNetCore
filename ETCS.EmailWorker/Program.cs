using ETCS.Shared.Application.Email;
using ETCS.Shared.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ETCS Email Worker";
});

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<MealDatabaseOptions>(builder.Configuration.GetSection(MealDatabaseOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.AddEmailDeliveryWorker();

var host = builder.Build();
await host.RunAsync();

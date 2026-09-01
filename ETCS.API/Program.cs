using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using ETCS.API.Features.Auth;
using ETCS.API.Features.Payment;
using ETCS.Shared.Application.Email;
using ETCS.Shared.Application.Notifications;
using ETCS.Shared.Application.Orders;
using ETCS.Shared.Application.Topup;
using ETCS.Shared.Application.Pos;
using ETCS.API.Infrastructure.ApiVersioning;
using ETCS.API.Infrastructure.Auth;
using ETCS.API.Infrastructure.Background;
using ETCS.API.Infrastructure.Caching;
using ETCS.API.Infrastructure.Errors;
using ETCS.Shared.Application.Background;
using ETCS.Shared.Application.Payment;
using ETCS.PaymentGateway.DependencyInjection;
using ETCS.Shared.Auth;
using ETCS.Shared.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Infrastructure.HealthChecks;
using ETCS.Shared.Infrastructure.Enums;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Legal;
using ETCS.Shared.Infrastructure.Meals;
using ETCS.Shared.Infrastructure.Meals.Menu;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Schools.Calendar;
using ETCS.Shared.Infrastructure.Admin.Master.Schools;
using ETCS.Shared.Infrastructure.Admin.Master.Students;
using ETCS.Shared.Infrastructure.Transaction;
using ETCS.Shared.Options;
using ETCS.Shared.Media;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<MealDatabaseOptions>(builder.Configuration.GetSection(MealDatabaseOptions.SectionName));
builder.Services.Configure<OrderFlowOptions>(builder.Configuration.GetSection(OrderFlowOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));
builder.Services.Configure<PosOptions>(builder.Configuration.GetSection(PosOptions.SectionName));
builder.Services.ConfigureMediaOptions(builder.Configuration);
builder.Services.Configure<RefreshTokenStoreOptions>(builder.Configuration.GetSection(RefreshTokenStoreOptions.SectionName));
builder.Services.Configure<ParentPortalOptions>(builder.Configuration.GetSection(ParentPortalOptions.SectionName));
builder.Services.Configure<PendingPaymentReconcileOptions>(builder.Configuration.GetSection(PendingPaymentReconcileOptions.SectionName));
builder.Services.Configure<LegalContentCacheClearOptions>(builder.Configuration.GetSection(LegalContentCacheClearOptions.SectionName));
builder.Services.AddPaymentGateway(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<IDbHealthRepository, DbHealthRepository>();
builder.Services.AddScoped<IGuardianPasswordResetTokenStore, SqlGuardianPasswordResetTokenStore>();
builder.Services.AddScoped<IGuardianOtpStore, SqlGuardianOtpStore>();
builder.Services.AddScoped<IRegistrationOtpService, RegistrationOtpService>();
builder.Services.AddScoped<IDeleteAccountOtpService, DeleteAccountOtpService>();
builder.Services.AddScoped<IParentLoginRepository, ParentLoginRepository>();
builder.Services.AddScoped<IEnumRepository, EnumRepository>();
builder.Services.AddScoped<IMealEnumAdminRepository, MealEnumAdminRepository>();
builder.Services.AddScoped<ILegalContentRepository, LegalContentRepository>();
builder.Services.AddScoped<MealRepository>();
builder.Services.AddScoped<IMealRepository, CachedMealRepository>();
builder.Services.AddScoped<MealOrderBookingWindow>(sp =>
{
    var cutoff = sp.GetRequiredService<IOptions<OrderFlowOptions>>().Value.MealOrderCutoffHour;
    return new MealOrderBookingWindow(cutoff);
});
builder.Services.AddScoped<IMealMenuComposer, MealMenuComposer>();
builder.Services.AddScoped<IMealOrderRepository, MealOrderRepository>();
builder.Services.AddScoped<IMainOrderRepository, MainOrderRepository>();
builder.Services.AddScoped<ISchoolCalendarRepository, SchoolCalendarRepository>();
builder.Services.AddScoped<ISchoolCalendarService, SchoolCalendarService>();
builder.Services.AddOrderFlowServices();
builder.Services.AddTopupFlowServices();
builder.Services.AddPosServices();
builder.Services.AddGuardianEmailServices();
builder.Services.AddGuardianInAppNotificationServices();
builder.Services.AddPendingPaymentReconcileServices();
builder.Services.AddScoped<IPaymentStatusService, PaymentStatusService>();
builder.Services.AddScoped<StudentRepository>();
builder.Services.AddScoped<IStudentRepository, CachedStudentRepository>();
builder.Services.AddScoped<IStudentAllergyAdminRepository, StudentAllergyAdminRepository>();
builder.Services.AddScoped<IStudentOrderTypeAdminRepository, StudentOrderTypeAdminRepository>();
builder.Services.AddScoped<ISchoolOrderTypeAdminRepository, SchoolOrderTypeAdminRepository>();
builder.Services.AddScoped<ISchoolGradeOrderTypeAdminRepository, SchoolGradeOrderTypeAdminRepository>();
builder.Services.AddScoped<IGuardianChildEnrollmentService, GuardianChildEnrollmentService>();
builder.Services.AddScoped<IReplaceCardRequestRepository, ReplaceCardRequestRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddSingleton<PaymentBackgroundQueue>();
builder.Services.AddSingleton<IPaymentBackgroundQueue>(sp => sp.GetRequiredService<PaymentBackgroundQueue>());
builder.Services.AddHostedService<PaymentBackgroundService>();
builder.Services.AddHostedService<PendingPaymentReconcileBackgroundService>();
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton<IMealDbConnectionFactory, SqlMealConnectionFactory>();

var refreshStoreOptions = builder.Configuration.GetSection(RefreshTokenStoreOptions.SectionName).Get<RefreshTokenStoreOptions>()
    ?? new RefreshTokenStoreOptions();
if (string.Equals(refreshStoreOptions.Provider, "Sql", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IRefreshTokenStore, SqlRefreshTokenStore>();
}
else
{
    builder.Services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
}

builder.Services.AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sqlserver")
    .AddCheck<SqlMealDatabaseHealthCheck>("sqlserver_meal");

builder.Services.AddResponseCompression();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    throw new InvalidOperationException("Jwt:SigningKey is required. Use user secrets, environment variables, or a secure configuration provider.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Accept tokens pasted with or without a "Bearer " prefix.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (!string.IsNullOrWhiteSpace(context.Token) &&
                    context.Token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    context.Token = context.Token["Bearer ".Length..].Trim();
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.AddPolicy("AuthPolicy", httpContext =>
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"),
        new QueryStringApiVersionReader("api-version"));
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Keep `&` unescaped in payment RedirectUrl so iOS/Android WebViews get a clean query string.
        options.JsonSerializerOptions.Encoder =
            System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var validationProblem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred."
            };

            return new BadRequestObjectResult(validationProblem);
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<SwaggerDefaultValuesOperationFilter>();

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Paste JWT token only (without the Bearer prefix).",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-API-KEY",
        Description = "Required for /api/Auth and /api/v{version}/Auth endpoints. Use the key from ApiKey:Keys in appsettings.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    options.OperationFilter<AuthApiKeyOperationFilter>();
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseCors("DefaultCorsPolicy");
app.UseHttpsRedirection();
app.UseResponseCompression();

var mediaOptions = builder.Configuration.GetSection(MediaOptions.SectionName).Get<MediaOptions>() ?? new MediaOptions();
app.MapMealImageStaticFiles(mediaOptions.StorePath);

if (true) // (app.Environment.IsDevelopment())
{
    app.UseSwagger(options =>
    {
        options.PreSerializeFilters.Add((swagger, request) =>
        {
            swagger.Servers =
            [
#if DEBUG
                new OpenApiServer { Url = "https://localhost:7204" }
#else
                new OpenApiServer { Url = "https://dev.api.etcs.acasea.ae" }
#endif
            ];
        });
    });
    app.UseSwaggerUI(options =>
    {
        var descriptions = app.DescribeApiVersions();
        foreach (var description in descriptions)
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                $"ETCS API {description.GroupName}");
        }

        options.UseRequestInterceptor(
            "(req) => {" +
            " try {" +
            "  const auth = window.ui?.authSelectors?.authorized?.();" +
            "  const bearer = auth?.get ? auth.get('Bearer') : auth?.Bearer;" +
            "  const rawBearer = bearer?.get ? bearer.get('value') : bearer?.value;" +
            "  let token = rawBearer;" +
            "  if (rawBearer && typeof rawBearer === 'object') {" +
            "    token = rawBearer.token || rawBearer.accessToken || rawBearer.value || '';" +
            "  }" +
            "  if (typeof token === 'string' && token.length > 0) {" +
            "    req.headers = req.headers || {};" +
            "    req.headers['Authorization'] = token.startsWith('Bearer ') ? token : `Bearer ${token}`;" +
            "  }" +
            "  const apiKey = auth?.get ? auth.get('ApiKey') : auth?.ApiKey;" +
            "  const rawKey = apiKey?.get ? apiKey.get('value') : apiKey?.value;" +
            "  if (typeof rawKey === 'string' && rawKey.length > 0) {" +
            "    req.headers = req.headers || {};" +
            "    req.headers['X-API-KEY'] = rawKey;" +
            "  }" +
            " } catch (e) { }" +
            " return req;" +
            "}");
    });
}

app.UseMiddleware<ApiKeyMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

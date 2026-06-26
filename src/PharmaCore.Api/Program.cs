using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Configuration;
using PharmaCore.Application.CustomerApp;
using PharmaCore.Infrastructure;
using PharmaCore.Infrastructure.Data;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt configuration is missing.");

var customerAppAuth = builder.Configuration.GetSection(CustomerAppAuthSettings.SectionName).Get<CustomerAppAuthSettings>()
    ?? new CustomerAppAuthSettings();

if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || jwtSettings.Secret.Length < 32)
{
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");
}

if (!builder.Environment.IsDevelopment())
{
    ValidateProductionConfiguration(builder.Configuration, jwtSettings, customerAppAuth);
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudiences = new[] { jwtSettings.Audience, customerAppAuth.Audience },
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddCatalogAuthorization();
    options.AddInventoryAuthorization();
    options.AddProcurementAuthorization();
    options.AddSystemAuthorization();
    options.AddSalesAuthorization();
    options.AddCustomerAppAuthorization();
    options.AddDashboardAuthorization();
    options.AddIdentityAuthorization();
});
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

var corsSettings = builder.Configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()
    ?? new CorsSettings();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminWeb", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(static origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;
                return uri.Host is "localhost" or "127.0.0.1";
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
        }
        else
        {
            var origins = corsSettings.AllowedOrigins
                .Where(static o => !string.IsNullOrWhiteSpace(o))
                .Select(static o => o.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (origins.Length == 0)
            {
                throw new InvalidOperationException(
                    "Cors:AllowedOrigins phải có ít nhất một origin trong Production.");
            }

            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "PharmaCore", Version = "v1", Description = "ERP Nhà thuốc đa quốc gia" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT token: Bearer {token}",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AdminWeb");

var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads",
});

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        if (context.Response.HasStarted)
        {
            app.Logger.LogWarning(
                ex,
                "Unhandled exception after response started for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
            return;
        }

        var statusCode = ex switch
        {
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };

        var message = ex switch
        {
            UnauthorizedAccessException => ex.Message,
            InvalidOperationException => ex.Message,
            _ when TryGetPostgresSqlState(ex, out var sqlState) => sqlState switch
            {
                "42P01" => "Thiếu bảng database. Chạy .\\scripts\\run-migrations.ps1 (hoặc setup-and-migrate.ps1).",
                "3D000" => "Database 'pharmacore' chưa tồn tại. Chạy scripts\\setup-and-migrate.bat.",
                "28P01" => "Sai user/mật khẩu PostgreSQL. Kiểm tra appsettings.Development.json.",
                "57P03" or "53300" => "PostgreSQL đang khởi động hoặc quá tải. Thử lại sau vài giây.",
                _ when IsConnectionException(ex) =>
                    "Không kết nối được PostgreSQL. Hãy bật PostgreSQL và kiểm tra database 'pharmacore'.",
                _ => app.Environment.IsDevelopment()
                    ? $"Lỗi SQL PostgreSQL ({sqlState}): {GetInnermostMessage(ex)}"
                    : "Lỗi truy vấn database.",
            },
            _ => app.Environment.IsDevelopment() ? ex.Message : "Lỗi server nội bộ",
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            message,
            detail = app.Environment.IsDevelopment() ? ex.ToString() : null,
        });
    }
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api")
        && !path.StartsWithSegments("/api/auth")
        && !path.StartsWithSegments("/api/health")
        && context.User.Identity?.IsAuthenticated == true)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantId, out _))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Phiên đăng nhập không hợp lệ. Vui lòng đăng xuất và đăng nhập lại.",
            });
            return;
        }
    }

    await next();
});

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

try
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    await using var conn = await db.CreateOpenConnectionAsync();
    app.Logger.LogInformation("PostgreSQL connection OK");
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "PostgreSQL connection FAILED — API sẽ trả lỗi 500 khi gọi dữ liệu");
}

app.Run();

static bool IsConnectionException(Exception ex)
{
    var text = ex.ToString();
    return text.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase)
        || text.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
        || text.Contains("timeout", StringComparison.OrdinalIgnoreCase);
}

static bool TryGetPostgresSqlState(Exception ex, out string sqlState)
{
    for (var current = ex; current is not null; current = current.InnerException)
    {
        var type = current.GetType();
        if (type.Name is "PostgresException" && type.GetProperty("SqlState")?.GetValue(current) is string state)
        {
            sqlState = state;
            return true;
        }
    }

    sqlState = "";
    return false;
}

static string GetInnermostMessage(Exception ex)
{
    while (ex.InnerException is not null)
        ex = ex.InnerException;
    return ex.Message;
}

static void ValidateProductionConfiguration(
    IConfiguration configuration,
    JwtSettings jwtSettings,
    CustomerAppAuthSettings customerAppAuth)
{
    if (!string.IsNullOrWhiteSpace(customerAppAuth.DevBypassCode))
    {
        throw new InvalidOperationException(
            "CustomerAppAuth:DevBypassCode không được dùng trong Production.");
    }

    if (jwtSettings.Secret.Contains("dev-secret", StringComparison.OrdinalIgnoreCase)
        || jwtSettings.Secret.Contains("change-in-production", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Đặt Jwt:Secret mạnh qua biến môi trường Jwt__Secret.");
    }

    var connectionString = configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(connectionString)
        || connectionString.Contains("pharmacore_dev", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Đặt ConnectionStrings__Default cho database production.");
    }

    var sms = configuration.GetSection(CustomerAppSmsSettings.SectionName).Get<CustomerAppSmsSettings>()
        ?? new CustomerAppSmsSettings();
    if (!sms.Provider.Equals("Http", StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(sms.HttpUrl))
    {
        throw new InvalidOperationException(
            "Production cần CustomerAppSms:Provider=Http và CustomerAppSms:HttpUrl (gateway SMS).");
    }
}

public partial class Program;

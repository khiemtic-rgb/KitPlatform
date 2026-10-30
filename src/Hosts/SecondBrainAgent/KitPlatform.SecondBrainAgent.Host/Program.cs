using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using KitPlatform.Api.Authorization;
using KitPlatform.Api.Controllers.PersonalIntelligence;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure;
using KitPlatform.Packs.PersonalIntelligence.Intent;
using KitPlatform.Packs.PersonalIntelligence.Research;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using KitPlatform.SecondBrainAgent.Host;

var builder = WebApplication.CreateBuilder(args);
SecondBrainDatabaseGuard.RequireLoopbackUrls(
    builder.Configuration["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));
SecondBrainDatabaseGuard.RequireWorkersDisabled(builder.Configuration);
var connectionString = SecondBrainDatabaseGuard.RequireConnection(builder.Configuration);
var approvedPort = new NpgsqlConnectionStringBuilder(connectionString).Port;
var expectedSystemIdentifier = SecondBrainDatabaseGuard.RequireSystemIdentifier(
    builder.Configuration["SecondBrain:SystemIdentifier"]);
var jwtSecret = SecondBrainDatabaseGuard.RequireSecret(builder.Configuration["SecondBrain:JwtSecret"]);
(builder.Configuration.GetSection(BusinessIntentModelOptions.SectionName).Get<BusinessIntentModelOptions>()
    ?? new BusinessIntentModelOptions()).EnsureStartupSafe();

await using (var probe = new NpgsqlConnection(connectionString))
{
    await probe.OpenAsync();
    await using var command = new NpgsqlCommand(
        "SELECT current_database(), inet_server_port(), system_identifier::text FROM pg_control_system()",
        probe);
    await using var reader = await command.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        throw new InvalidOperationException("Connected PostgreSQL endpoint was not reported.");
    var database = reader.IsDBNull(0) ? null : reader.GetString(0);
    int? port = reader.IsDBNull(1) ? null : reader.GetInt32(1);
    var systemIdentifier = reader.IsDBNull(2) ? null : reader.GetString(2);
    SecondBrainDatabaseGuard.RequireActualEndpoint(
        database, port, systemIdentifier, approvedPort, expectedSystemIdentifier);
}

builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(new NpgsqlConnectionFactory(connectionString));
builder.Services.AddScoped<IDbConnectionFactory>(sp => sp.GetRequiredService<NpgsqlConnectionFactory>());
builder.Services.AddScoped<ITenantContext, AgentTenantContext>();
builder.Services.AddScoped<AgentPlatformSettings>();
builder.Services.AddScoped<ITenantPlatformSettings>(sp => sp.GetRequiredService<AgentPlatformSettings>());
builder.Services.AddPersonalIntelligenceServices();
builder.Services.Configure<ResearchPublicationOptions>(
    builder.Configuration.GetSection(ResearchPublicationOptions.SectionName));
builder.Services.Configure<BusinessIntentModelOptions>(
    builder.Configuration.GetSection(BusinessIntentModelOptions.SectionName));
builder.Services.Configure<ExchangeBackfillOptions>(
    builder.Configuration.GetSection(ExchangeBackfillOptions.SectionName));
builder.Services.PostConfigure<ExchangeBackfillOptions>(options => options.WorkerEnabled = false);

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
            ValidIssuer = "SecondBrainAgent",
            ValidAudience = "SecondBrainAgent",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            NameClaimType = "sub",
            RoleClaimType = "role",
        };
    });
builder.Services.AddAuthorization(options => options.AddPersonalIntelligenceAuthorization());
builder.Services.AddControllers()
    .AddApplicationPart(typeof(TradingDeskController).Assembly)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

var app = builder.Build();
var hosted = app.Services.GetServices<IHostedService>()
    .Select(service => service.GetType().FullName ?? service.GetType().Name)
    .OrderBy(name => name, StringComparer.Ordinal)
    .ToArray();
var forbiddenHost = hosted.FirstOrDefault(name =>
    name.Contains("ExchangeBackfill", StringComparison.Ordinal)
    || name.Contains("Medication", StringComparison.Ordinal)
    || name.Contains("Pharmacy", StringComparison.Ordinal)
    || name.Contains("Content", StringComparison.Ordinal)
    || name.Contains("Famixa", StringComparison.Ordinal));
if (forbiddenHost is not null)
    throw new InvalidOperationException("Second Brain agent host registered a forbidden hosted service.");
if (app.Services.GetRequiredService<IOptions<ExchangeBackfillOptions>>().Value.WorkerEnabled)
    throw new InvalidOperationException("Exchange backfill worker is enabled at runtime.");

await AgentAuth.EnsureOperatorAsync(
    app.Services.GetRequiredService<NpgsqlConnectionFactory>(),
    builder.Configuration["SecondBrain:BootstrapAdminPassword"],
    CancellationToken.None);

app.UseAuthentication();
app.UseAuthorization();

var webRoot = builder.Configuration["SecondBrain:WebRoot"];
PhysicalFileProvider? files = null;
if (!string.IsNullOrWhiteSpace(webRoot))
{
    var fullWeb = Path.GetFullPath(webRoot);
    if (!Directory.Exists(fullWeb) || !File.Exists(Path.Combine(fullWeb, "index.html")))
        throw new InvalidOperationException("SecondBrain:WebRoot must contain index.html.");
    files = new PhysicalFileProvider(fullWeb);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
}

app.MapControllers();
AgentAuth.Map(app, jwtSecret);

var report = new AgentRuntimeReport(
    "ok",
    "second-brain-agent",
    SecondBrainDatabaseGuard.DatabaseName,
    WorkerEnabled: false,
    BackfillWorkerRegistered: false,
    hosted);
app.MapGet("/api/health", () => Results.Ok(report));
app.MapGet("/api/health/db", async (IDbConnectionFactory db, CancellationToken cancellationToken) =>
{
    await using var conn = await db.CreateOpenConnectionAsync(cancellationToken);
    var row = await conn.QuerySingleAsync<(string Database, int? Port, string SystemIdentifier, int Value)>(
        new Dapper.CommandDefinition(
            "SELECT current_database(), inet_server_port(), system_identifier::text, 1 FROM pg_control_system()",
            cancellationToken: cancellationToken));
    SecondBrainDatabaseGuard.RequireActualEndpoint(
        row.Database, row.Port, row.SystemIdentifier, approvedPort, expectedSystemIdentifier);
    return Results.Ok(new { status = "ok", database = row.Database, value = row.Value, workerEnabled = false });
});

if (files is not null)
{
    var index = Path.Combine(files.Root, "index.html");
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(index, context.RequestAborted);
    });
}

app.Run();

public partial class Program;

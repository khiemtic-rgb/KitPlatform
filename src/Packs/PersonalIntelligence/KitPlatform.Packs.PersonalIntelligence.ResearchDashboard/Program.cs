using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.ResearchDashboard;
using KitPlatform.Packs.PersonalIntelligence.Trading;

var builder = WebApplication.CreateBuilder(args);
var configuredUrls = builder.Configuration["ASPNETCORE_URLS"] ?? builder.Configuration["urls"];
DashboardEndpoint.Require(configuredUrls);
builder.WebHost.UseUrls(DashboardEndpoint.DefaultUrl);

var fixture = builder.Configuration["Dashboard:Fixture"];
var connectionFile = builder.Configuration["Dashboard:ConnectionFile"];
if (!string.IsNullOrWhiteSpace(connectionFile) && !string.IsNullOrWhiteSpace(fixture))
    throw new InvalidOperationException("Choose either a fixture or a research connection file.");

IResearchReader reader;
string source;
if (!string.IsNullOrWhiteSpace(connectionFile))
{
    reader = new SqlResearchReader(DashboardConnection.ReadFile(connectionFile));
    source = "research_trading";
}
else
{
    fixture = string.IsNullOrWhiteSpace(fixture) ? Path.Combine(builder.Environment.ContentRootPath, "fixture", "pilot.json") : fixture;
    reader = new FixtureResearchReader(fixture);
    source = "fixture";
}

var token = Environment.GetEnvironmentVariable("TRADING_RESEARCH_DASHBOARD_TOKEN");
var deskRoot = builder.Configuration["Dashboard:DeskRoot"];
var allowExecute = string.Equals(builder.Configuration["Dashboard:AllowExecute"], "true", StringComparison.OrdinalIgnoreCase);
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var app = builder.Build();
app.Use(async (context, next) =>
{
    if (!string.IsNullOrEmpty(token) && context.Request.Path.StartsWithSegments("/api/dashboard"))
    {
        if (!TokenMatches(context.Request.Headers.Authorization.ToString(), token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    if (context.Request.Path.StartsWithSegments("/api/personal-intelligence/trading-research"))
    {
        var access = ResearchDeskJwt.Authorize(context.Request, builder.Configuration);
        if (access != DeskAccess.Allowed)
        {
            context.Response.StatusCode = access switch
            {
                DeskAccess.Unconfigured => StatusCodes.Status503ServiceUnavailable,
                DeskAccess.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status401Unauthorized,
            };
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"research_desk_auth\"}");
            return;
        }
    }

    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/dashboard", () =>
{
    var document = ResearchReadModel.Build(reader.Read(), DateTimeOffset.UtcNow, source);
    return Results.Json(document, json);
});
app.MapGet("/api/personal-intelligence/trading-research/desk", () =>
{
    var tasks = string.IsNullOrWhiteSpace(deskRoot) ? [] : ResearchDesk.List(deskRoot);
    return Results.Json(new { executeEnabled = allowExecute, dataPlane = source, engineVersion = ResearchStrategies.EngineVersion, tasks }, json);
});
app.MapGet("/api/personal-intelligence/trading-research/tasks/{taskId}", (string taskId) =>
{
    if (string.IsNullOrWhiteSpace(deskRoot))
        return Results.NotFound();
    var task = ResearchDesk.Find(deskRoot, taskId);
    return task is null ? Results.NotFound() : Results.Json(task, json);
});
app.MapPost("/api/personal-intelligence/trading-research/batch", async (ResearchDeskRequest request, CancellationToken cancellationToken) =>
{
    if (!allowExecute)
        return Results.Json(new { error = "Research execution is disabled on this host." }, json, statusCode: StatusCodes.Status403Forbidden);
    try
    {
        var tasks = await ResearchDesk.ExecutePlanAsync(deskRoot ?? "", request, cancellationToken);
        return Results.Json(new { tasks }, json);
    }
    catch (ResearchDeskException ex)
    {
        return Results.Json(new { error = ex.Message }, json, statusCode: StatusCodes.Status400BadRequest);
    }
});
app.MapPost("/api/personal-intelligence/trading-research/tasks", async (ResearchDeskRequest request, CancellationToken cancellationToken) =>
{
    if (!allowExecute)
        return Results.Json(new { error = "Research execution is disabled on this host." }, json, statusCode: StatusCodes.Status403Forbidden);
    try
    {
        var task = await ResearchDesk.ExecuteAsync(deskRoot ?? "", request, cancellationToken: cancellationToken);
        return Results.Json(task, json);
    }
    catch (ResearchDeskException ex)
    {
        return Results.Json(new { error = ex.Message }, json, statusCode: StatusCodes.Status400BadRequest);
    }
});
app.Run();

static bool TokenMatches(string header, string token)
{
    const string prefix = "Bearer ";
    if (!header.StartsWith(prefix, StringComparison.Ordinal))
        return false;
    var given = Encoding.UTF8.GetBytes(header[prefix.Length..]);
    var expected = Encoding.UTF8.GetBytes(token);
    return given.Length == expected.Length && CryptographicOperations.FixedTimeEquals(given, expected);
}

public static class DashboardEndpoint
{
    public const string DefaultUrl = "http://127.0.0.1:5090";

    public static void Require(string? urls)
    {
        if (string.IsNullOrWhiteSpace(urls))
            return;
        var items = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (items.Length == 1 && string.Equals(items[0].TrimEnd('/'), DefaultUrl, StringComparison.OrdinalIgnoreCase))
            return;
        throw new InvalidOperationException($"The research dashboard binds only to {DefaultUrl}.");
    }
}

public partial class Program;

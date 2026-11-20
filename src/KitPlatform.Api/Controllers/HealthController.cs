using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Ping() => Ok(new { status = "ok" });

    [HttpGet("db")]
    public async Task<IActionResult> Database(
        [FromServices] IDbConnectionFactory db,
        [FromServices] IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await ReadDatabaseProbeAsync(db, cancellationToken);
            return Ok(new { status = "ok", database = true, value });
        }
        catch (Exception ex)
        {
            const string message = "Không kết nối được PostgreSQL. Kiểm tra service PostgreSQL và connection string.";
            if (environment.IsDevelopment())
            {
                return StatusCode(503, new
                {
                    status = "error",
                    message,
                    detail = ex.Message,
                });
            }

            return StatusCode(503, new
            {
                status = "error",
                message,
            });
        }
    }

    protected virtual async Task<int> ReadDatabaseProbeAsync(
        IDbConnectionFactory db,
        CancellationToken cancellationToken)
    {
        await using var conn = await db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<int>("SELECT 1");
    }
}

using Dapper;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Ping() => Ok(new { status = "ok" });

    [HttpGet("db")]
    public async Task<IActionResult> Database(
        [FromServices] IDbConnectionFactory db,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conn = await db.CreateOpenConnectionAsync(cancellationToken);
            var value = await conn.QuerySingleAsync<int>("SELECT 1");
            return Ok(new { status = "ok", database = true, value });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new
            {
                status = "error",
                message = "Không kết nối được PostgreSQL. Kiểm tra service PostgreSQL và connection string.",
                detail = ex.Message,
            });
        }
    }
}

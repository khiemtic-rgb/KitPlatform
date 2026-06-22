using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Abstractions;

namespace PharmaCore.Api.Controllers;

public sealed record UploadFileResult(string Url);

[ApiController]
[Authorize]
[Route("api/files")]
public sealed class FilesController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp",
    };

    private const long MaxFileBytes = 5 * 1024 * 1024;

    private readonly ITenantContext _tenant;
    private readonly IWebHostEnvironment _environment;

    public FilesController(ITenantContext tenant, IWebHostEnvironment environment)
    {
        _tenant = tenant;
        _environment = environment;
    }

    [HttpPost("upload")]
    [Authorize(Policy = CatalogPolicies.Write)]
    [RequestSizeLimit(MaxFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes)]
    public async Task<ActionResult<UploadFileResult>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return BadRequest(new { message = "Chọn file ảnh để tải lên." });

        if (file.Length > MaxFileBytes)
            return BadRequest(new { message = "Ảnh tối đa 5 MB." });

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            return BadRequest(new { message = "Chỉ hỗ trợ ảnh JPG, PNG hoặc WebP." });

        var tenantFolder = _tenant.TenantId.ToString("N");
        var directory = Path.Combine(_environment.ContentRootPath, "uploads", "products", tenantFolder);
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(directory, fileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var url = $"/uploads/products/{tenantFolder}/{fileName}";
        return Ok(new UploadFileResult(url));
    }
}

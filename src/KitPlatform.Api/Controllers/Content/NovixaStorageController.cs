using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>Dung lượng video đã dựng của một phim và việc dọn chúng sau khi đăng xong. Chỉ xóa file video, không đụng giọng và ảnh.</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[Route("api/content/series/builds/{id:guid}/novixa-motion/storage")]
public sealed class NovixaStorageController : ControllerBase
{
    [HttpGet]
    [ServiceFilter(typeof(ProductionTenantActionFilter))]
    public ActionResult<NovixaStorageReport> Get(Guid id) => Ok(NovixaStockMotionRunner.StorageScan(id));

    [HttpPost("clean")]
    [ServiceFilter(typeof(ProductionTenantActionFilter))]
    public ActionResult<NovixaStorageReport> Clean(Guid id) => Ok(NovixaStockMotionRunner.StorageClean(id));
}

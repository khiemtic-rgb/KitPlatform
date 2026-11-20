# SECOND BRAIN AGENT V1 — DEPLOYMENT PACKAGE REPORT

Ngày: 2026-10-30. Nguồn build: `git archive` của commit bổ sung, thư mục `E:\sb-agent-v1-5f29e1bd`. Working tree `E:\KitPlatform` không bị reset, clean, checkout đè, stage hàng loạt, amend hoặc push.

## Kết luận

`DEPLOYED_AND_RUNNING`

Đã thay riêng `second-brain-agent` lúc 2026-10-30 09:51:54–09:51:57 +07. PID cũ 43568, PID mới 62086. DLL đang chạy SHA-256 `336ca77d5d376f9661b8d51448f626965e1e3e93ab8f35c1e2683d0f5a810263`. Rollback không phải kích hoạt. Bản cũ `891e5726…` vẫn ở `/opt/second-brain-agent` và `/var/backups/second-brain-agent-891e5726`.

## 1. Commit

| Mục | Giá trị |
|---|---|
| Commit | `5f29e1bdf184364ad646e6cd4e622cdb0d0b3278` |
| Parent | `9135596a9cb8a91014d930bc7219348a06a8bf5a` |
| Thời điểm | 2026-10-30 09:31:20 +0700 |
| File duy nhất | `src/KitPlatform.Infrastructure/Core/Engines/IPermissionEngine.cs` |
| Đã push | không |
| Amend `9135596` | không |

Interface nằm trong assembly Infrastructure, namespace `KitPlatform.Application.Core.Engines`. Contract khớp code đã commit: `IsAdmin()`, `HasPermission(string)`, `GetSalesDiscountPolicy()`. `SalesDiscountPolicy` đã có trong Pharmacy Application. Host Second Brain không đăng ký implementation và không resolve `DashboardService` hay `CustomerDraftOrderService`. Đây là bề mặt biên dịch dùng chung của KIT Platform, không phải logic domain của Agent. WIP pharmacy đổi hai service sang `ICurrentUserAccessor` không được đưa vào commit.

## 2. Artifact

Archive: `E:\sb-agent-v1-5f29e1bd\source-5f29e1bd.tar` (341647360 byte), giải nén ngoài repo, không có `.git`.

Publish: `E:\sb-agent-v1-5f29e1bd\publish\host\` và `publish\ui\`. Log: `publish.log`, `test-host.log`. Manifest: `E:\sb-agent-v1-5f29e1bd\SHA256SUMS.txt`.

| File | SHA-256 | Byte |
|---|---|---|
| `KitPlatform.SecondBrainAgent.Host.dll` | `336CA77D5D376F9661B8D51448F626965E1E3E93AB8F35C1E2683D0F5A810263` | 83456 |
| `KitPlatform.Infrastructure.dll` | `846036821085C0F1806430E1198F7FD6DDF642695B244382601F8B597B5F9E60` | 2903552 |
| `KitPlatform.Application.dll` | `3E952ECC7A6521A55EE949AB2217E083C60A44335EB6A3CE20CE8F4DCDE16EF9` | 993792 |
| `KitPlatform.Domain.dll` | `FBA5FBEE86C748975D9AC21AC97BC808B1AA834B0A4249AD5CAC26BE09CC49EE` | 6656 |
| `KitPlatform.Packs.PersonalIntelligence.Api.dll` | `237D44402DB7CAE910F88F2C994AF27C85E3304E9232F80D54B6DC5DCE71C91F` | 66048 |
| `KitPlatform.Packs.PersonalIntelligence.Application.dll` | `615AC737A6A9EF231F97D306DA1075B6E07310C72AD425EEAABA1EBEBB8CC7FD` | 376832 |
| `KitPlatform.Packs.PersonalIntelligence.Infrastructure.dll` | `953A066D006D4D67EA8383FB2F9FE11658252A226A64C7010CFF3502B7A5AC1B` | 644608 |
| `KitPlatform.Packs.Pharmacy.Application.dll` | `A0CD41167F87C9E6A453DE1C3FD0587427584F3FC914CA1850D06AF2F59FB4BC` | 834560 |
| `publish/ui/index.html` | `BCF870925D0C65115C3D0160CE6AA58B1813A6F209ED69E7AF99D8EAB05EE8FB` | 409 |
| `publish/ui/assets/index-DmF68tyh.js` | `7C337D09588F1775A8C2B3289B7CFCD4F3B2CAE2BE148127ED601845102AF659` | 1305300 |
| `publish/ui/assets/index-BdOndhxL.css` | `98ADF782BD027847CFEAC99BC2F2735177375EA17BB836601CE1D7290CA699C5` | 2940 |

Ba file UI trùng hash UI đang phục vụ trên port 5095. DLL host mới khác hash candidate cũ `90265F6C…` và khác binary đang chạy `891E5726…`. Binary không nhúng full SHA của commit.

`KitPlatform.Packs.Pharmacy.Application.dll` có trong publish vì Infrastructure tham chiếu project đó. Host test không đăng ký hosted service Pharmacy. Không xóa pdb.

Cảnh báo `NU1903` (`Newtonsoft.Json` 10.0.3) có từ trước và không chặn publish.

UI bundle chứa chuỗi `https://api.novixa.vn`. Hàm base URL chỉ trả chuỗi đó khi hostname trang là `admin.novixa.vn`, `app.novixa.vn` hoặc `pos.novixa.vn`. Hostname khác, kể cả `127.0.0.1`, nhận chuỗi rỗng (same-origin). Địa chỉ `103.200.23.229` không có trong JS. Đây là cùng byte UI đang chạy, không phải host mới.

## 3. Test

Host tests, Release, log `E:\sb-agent-v1-5f29e1bd\test-host.log`: **25 passed, 0 failed**, 5.056 giây. Database test là cluster local `E:\kit-pi-validation-pg`, `127.0.0.1:5433`, database `second_brain_agent_v1`, user `pi_validation`. System identifier local `7697562943926157940`, khác identifier VPS. Cluster local đã `pg_ctl stop` sau test. Port 5432 local không được dùng.

Đã phủ:

- DatabaseGuard: từ chối port 5432, `novixa_prod`, host `103.200.23.229`, URL không loopback, `WorkerEnabled=true`, system identifier sai.
- Host/DI và integration: `/api/health`, login issuer `SecondBrainAgent`, paper replay cùng `decisionId`, đổi thesis 409, `live:true` HTTP 400, paper response chứa `PAPER`, research publication `MISSING`.
- `WorkerEnabled=false` và backfill worker không đăng ký được assert trong health/runtime của host test.
- JWT test là secret fixture của host test, không phải pharmacy JWT.

Không chạy được, lỗi biên dịch có sẵn ngoài đồ thị host, không sửa assertion và không thêm interface giả:

- Catalog `PackDefinitionsAsBuiltSyncTests`: Content thiếu `ReadVeoTake` / `ReadFalTake`; Pharmacy.Infrastructure thiếu `IInventoryEngine` và `IPricingEngine`. Log: `test-catalog.log`.
- `CoreAuthorityBoundaryTests`: cùng lỗi Pharmacy.Infrastructure. Host không reference project đó. Log: `test-boundary.log`.

## 4. Database

Không chạy migration. Không có file migration mới ngoài bộ đã đối chiếu ở commit `9135596` (000, 385, 387–393, 399), byte khớp `deploy/second-brain-agent/b-deployed.sha256`.

Cluster VPS `16/sbagent` port 5433 vẫn listen. Database `second_brain_agent_v1` và các bảng chính đã có từ audit trước. Không có migration ledger, nên lệch cột so với SQL vẫn là UNKNOWN. Binary mới không thêm schema.

Trước swap, `SecondBrain__SystemIdentifier` chưa có trong env. Cluster đã đối chiếu là `second_brain_agent_v1|5433|7702079316921501630`. `public.users` đếm được 1 dòng, nên startup không chèn operator mới. Không in connection string, JWT hay bootstrap password.

## 5. VPS B trước swap, 2026-10-30 09:39:47 +07

| Mục | Kết quả |
|---|---|
| `second-brain-agent` | active, PID 43568, không đổi sau backup |
| Listen | `127.0.0.1:5095`, `127.0.0.1:5433`. Port 5096 không listen |
| DLL đang chạy | `/opt/second-brain-agent/host/KitPlatform.SecondBrainAgent.Host.dll` |
| SHA-256 đang chạy | `891e5726d532b2923a92f38e24c902256b92cf6a326ebd748b213b71dda9acc7` |
| `postgresql@16-sbagent`, `kit-platform-api`, `nginx` | active, không đụng |
| VPS A | không truy cập |

Backup rollback, không gồm `agent.env`:

`/var/backups/second-brain-agent-891e5726/host`

`/var/backups/second-brain-agent-891e5726/ui`

`/var/backups/second-brain-agent-891e5726/second-brain-agent.service`

Hash DLL trong backup trùng `891e5726…`. PID sau copy vẫn 43568.

## 6. Guardrail của gói mới

Giữ database riêng port 5433, từ chối socket tới `103.200.23.229` và port 5432, issuer `SecondBrainAgent`, `WorkerEnabled=false`, không đăng ký backfill worker, live execution trả 400. Paper chỉ trong phạm vi test đã chứng minh. Không tạo secret mới.

Unit drop-in đã cài: `/etc/systemd/system/second-brain-agent.service.d/v1-5f29e1bd.conf`. File unit gốc không bị sửa. `agent.env` chỉ thêm `SecondBrain__SystemIdentifier`. `WebRoot` vẫn `/opt/second-brain-agent/ui` vì hash UI trùng bản mới. `WorkerEnabled` vẫn `false`. URL vẫn `http://127.0.0.1:5095`.

## 7. Kết quả swap 2026-10-30 09:51:54–09:51:57 +07

| Kiểm tra | Kết quả |
|---|---|
| Restart | bắt đầu 09:51:54, systemd trả về 09:51:55, health đạt 09:51:57 |
| PID | 43568 → 62086, state active |
| DLL | `/opt/second-brain-agent-v1-5f29e1bd/host/KitPlatform.SecondBrainAgent.Host.dll` |
| SHA-256 | `336ca77d5d376f9661b8d51448f626965e1e3e93ab8f35c1e2683d0f5a810263` |
| `/api/health` | 200, `status=ok`, `database=second_brain_agent_v1`, `workerEnabled=false`, `backfillWorkerRegistered=false` |
| Hosted services | chỉ DataProtection và GenericWebHost |
| `/api/health/db` | 200, `status=ok`, `database=second_brain_agent_v1`, `value=1`, `workerEnabled=false` |
| Socket process | một kết nối `127.0.0.1` tới port 5433. Không có socket tới 5432 hoặc `103.200.23.229` |
| System identifier | biến môi trường khớp `7702079316921501630`. Health DB chỉ trả 200 khi identifier và port khớp |
| Listen | `127.0.0.1:5095`, `5433`, `5432` vẫn listen. Port 5096 không listen |
| Postgres / API / nginx | PID giữ 43039 / 37553 / 34942 |
| Rollback | không kích hoạt. Backup `891e5726…` còn nguyên. Binary cũ tại `/opt/second-brain-agent` không bị ghi đè |
| Migration | không chạy |

Hai lần curl đầu trong khoảng startup chưa listen; lần sau đó health thành công. Không có rollback.

# Second Brain — implementation publication Research Lab / Paper Ops

Ngày: 2026-10-22. Nhiệm vụ: `SECOND-BRAIN-RESEARCH-PUBLICATION-IMPLEMENTATION-001`.

Tài liệu thiết kế `SECOND_BRAIN_RESEARCH_READ_API_DESIGN.md` giữ nguyên. Bản này chỉ ghi việc đã triển khai.

## Kết luận

Publisher tạo được publication `research-publication/v1`. Reader kiểm manifest và SHA-256 trước khi trả nội dung. Năm route GET và policy đọc đã có. Test bắt buộc của nhiệm vụ này xanh. Trading Summary không đổi contract.

**GO có điều kiện** cho một nhiệm vụ UI sau. Nhiệm vụ này không sửa UI và không deploy. Deploy vẫn không được phép từ bản ghi này.

## Mâu thuẫn đã khóa

Thiết kế mục 4 có câu: lab đọc được khi chưa có manifest có thể là `PRESENT_UNTIMED`. Nhiệm vụ implementation khóa lại: thiếu manifest là `MISSING`. `PRESENT_UNTIMED` chỉ là trạng thái timestamp của object lab bên trong một publication V1 đã hợp lệ. API không tự điền `publishedAt` và không lấy giờ sửa file.

## File đã đọc

- `src/Packs/PersonalIntelligence/SECOND_BRAIN_RESEARCH_READ_API_DESIGN.md`
- `src/KitPlatform.Api/Program.cs`
- `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Api/PersonalIntelligenceAuthorizationExtensions.cs`
- `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Api/TradingDeskController.cs`
- `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Infrastructure/PersonalIntelligencePackDependencyInjection.cs`
- `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Application/Trading/ITradingDesk.cs`
- `src/KitPlatform.Application/Configuration/ITenantPlatformSettings.cs`
- `client/admin/src/modules/second-brain/trading-routes.smoke.ts`
- `E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1\run_research_lab_v1.py`
- `research-lab-v1\paper_ops.py` (`_write_json`, hình dạng health và report)
- `research-lab-v1\run_paper_ops_v1.py` (danh sách file được bảo vệ; không chạy script này)

## File đã sửa hoặc thêm

Kit Platform:

- `Program.cs`: chỉ thêm bind `PersonalIntelligence:ResearchPublication`. Các sửa chưa commit khác trong file được giữ.
- `PersonalIntelligenceAuthorizationExtensions.cs`: policy `PersonalIntelligenceResearchRead`.
- `PersonalIntelligencePackDependencyInjection.cs`: đăng ký reader.
- `Application/Research/ResearchPublication.cs`
- `Infrastructure/Research/FileResearchPublicationReader.cs`
- `Api/ResearchPublicationController.cs`
- `tests/KitPlatform.PersonalIntelligence.Tests/ResearchPublicationTests.cs`

Research Lab, ngoài git Kit Platform:

- `research-lab-v1/publish_research_v1.py`
- `research-lab-v1/tests/test_publish_research_v1.py`
- `research-lab-v1/run_research_lab_v1.py`: `LAB_RESULT.json` và `LAB_REPORT.md` ghi qua file tạm rồi replace. Nội dung JSON không đổi. Script lab không được chạy trong nhiệm vụ này.
- `research-lab-v1/output/publication/`: một generation mới, tạo từ bốn artifact đã hoàn tất.

Không sửa `TradingDeskController`, RiskEngine, DemoPaperExecution, UI, `operator-notices.ts`, appsettings, migration, journal, snapshot `SOLUSDT.json`, research ledger, `state/paper-ops/checkpoint.json`, hay bốn file nguồn trong `output/`.

## Luồng dữ liệu

```text
LAB_RESULT.json
PAPER_OPS_STATUS.json
PAPER_RESEARCH_REPORT.json
PAPER_OPS_HEALTH.json
        │
        ▼
publish_research_v1.py
        │  staging, rồi đổi tên thư mục
        ▼
{Root}/manifest.json
{Root}/lab/LAB_RESULT.json
{Root}/paper/PAPER_OPS_STATUS.json
{Root}/paper/PAPER_RESEARCH_REPORT.json
{Root}/paper/PAPER_OPS_HEALTH.json
        │
        ▼
FileResearchPublicationReader
        │  chỉ khi manifest và cả bốn hash khớp
        ▼
GET /api/personal-intelligence/research/...
```

Request không gọi Python, không đọc `state/paper-ops`, và không nhận path từ query, route hay body.

## Manifest, hash, publication

Manifest có `schemaVersion`, `publicationId`, `publishedAt`, `publisher`, và bốn SHA-256. `publishedAt` là giờ UTC do publisher ghi lúc xuất thành công. `publicationId` là `pub-` cộng 16 ký tự hex đầu của SHA-256 bốn hash publication, nên một bộ byte cho một id.

`LAB_RESULT.json` và `PAPER_OPS_HEALTH.json` được copy nguyên byte. Bản publication của status bỏ mọi khóa có `api_key`. Bản publication của report bỏ `frozen_lab_result` rồi ghi lại JSON. Hash trong manifest là hash byte của bản publication, không phải hash file nguồn nếu bản đó đã được làm sạch. File nguồn không đổi.

Trên Windows không có một syscall đổi hai thư mục. Publisher ghi đủ staging, đổi tên bản đang phục vụ thành `.previous`, rồi đổi staging vào đúng tên. Nếu tiến trình chết giữa hai lần đổi tên, reader thấy `MISSING`, không thấy manifest mới cạnh object cũ. Đây không phải atomic publication một syscall. Khi đổi tên thứ hai ném lỗi, code đưa `.previous` trở lại.

Không có artifact nguồn thì publisher ném lỗi và không tạo publication. Không bịa số nghiên cứu.

Generation local vừa xuất:

- Id: `pub-2685c4437898bc17`
- `publishedAt`: `2026-10-22T04:17:38.851734+00:00`
- Health nguồn: `OK` (reader sẽ trả `FRESH` cho publication và `PRESENT_UNTIMED` cho object lab)
- Ba experiment: `EXP-TREND-SMA-10-30-V1`, `EXP-BREAKOUT-DONCHIAN-20-V1`, `EXP-REVERSION-RANGE-20-V1`

SHA-256 trước và sau của bốn file nguồn, snapshot, ba journal, research ledger và `checkpoint.json` giống nhau. Publication không chứa `api_key`, `frozen_lab_result`, `PAPER_OPS_STATE_DIR`, hay `BINANCE_API`.

## Endpoint và policy

Tất cả GET, policy `PersonalIntelligenceResearchRead`, kèm `[RequirePersonalIntelligenceModule]`.

| Route | Kết quả |
|---|---|
| `/api/personal-intelligence/research/publication` | Availability, id, `publishedAt`, bốn object |
| `/api/personal-intelligence/research/experiments` | Danh sách experiment khi publication hợp lệ |
| `/api/personal-intelligence/research/experiments/{experimentId}` | Một experiment, hoặc 404 `NOT_IN_PUBLICATION` |
| `/api/personal-intelligence/research/paper/status` | Status và health |
| `/api/personal-intelligence/research/paper/report` | Report; `labResult` là `SEE_LAB_RESULT_ENDPOINT` |

Policy cho phép ADMIN, `personal_intelligence.research.read`, hoặc `personal_intelligence.trading.run`. Không cho `trading.approve`, `exchange.read`, hay `exchange.connect` một mình.

Availability publication chỉ là `FRESH` khi health nguồn đúng `OK`, và `STALE` khi đúng `STALE`. Thiếu root, manifest hoặc object: `MISSING`. JSON hỏng hoặc manifest không đủ trường / hash không phải 64 hex: `UNREADABLE`, HTTP 200. Hash lệch: `INCONSISTENT`, không trả thân object nào của lần đọc đó. Schema khác `research-publication/v1`: `UNSUPPORTED_VERSION`. Id có `/`, `\`, hoặc `.` bị từ chối trước khi mở file.

## Cấu hình local

Không sửa appsettings production.

```text
PersonalIntelligence:ResearchPublication:Root = E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1\output\publication
```

Root trống hoặc thư mục không có thì API trả `MISSING`. Gán claim `personal_intelligence.research.read` cho role là cấu hình tenant, không phải migration. Nhiệm vụ này không sửa role production. ADMIN và người đã có `personal_intelligence.trading.run` đọc được mà chưa cần claim mới.

## Bằng chứng test và build

| Lệnh | Kết quả |
|---|---|
| `python -m unittest tests.test_publish_research_v1` | PASS, 5 tests, exit 0 |
| `python -m unittest tests.test_research_lab_v1 tests.test_paper_ops_v1 tests.test_publish_research_v1` | PASS, 14 tests, exit 0 |
| `dotnet test ... --filter ResearchPublicationTests\|SecondBrainV1Tests\|PersistenceContractTests\|BybitConnectorTests\|MultiExchangeConnectorTests\|BybitExecutionHistoryTests` | PASS, 59 tests, 0 failed, exit 0 |
| Trong 59 test đó, `ResearchPublicationTests` | PASS, 14 tests. Gồm 401, 403 module, `trading.run`, `research.read`, ADMIN, 403 cho approve/exchange.read/exchange.connect, thiếu manifest/object, `STALE`, `PRESENT_UNTIMED`, JSON hỏng, hash lệch, schema lạ, manifest sai, traversal, không lộ path/key/`frozen_lab_result`, và Trading Summary không đổi khi reader `MISSING`, `UNREADABLE`, hoặc ném lỗi |
| `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj` | PASS, 0 errors, 65 warnings có sẵn, 19.98s, exit 0 |
| `npx tsx src/modules/second-brain/trading-routes.smoke.ts` | PASS, `trading routes ok`, exit 0 |

## Không chạy

Các class sau dùng `PiValidationDatabase` và có thể ghi `kit_pi_validation`. Nhiệm vụ này không đụng database đó.

- `PostgresIntegrationTests`
- `RuntimeHttpTests`
- `OperationalDeliveryTests`
- `ExchangeConnectionApiTests`
- `ExchangeExecutionSyncApiTests`
- `MultiExchangeBackfillTests`

Trạng thái: **NOT_RUN**. Không phải FAIL. Không tuyên bố toàn bộ regression Personal Intelligence đã xanh.

`npm run build` của admin: **NOT_RUN**. Không có file UI nào đổi. Smoke route Second Brain đã chạy.

## Điều kiện trước khi tích hợp UI hoặc deploy

- Nhiệm vụ UI mới được đổi notice `research-not-connected`. Khi availability không phải `FRESH` hoặc `STALE`, notice chưa nối vẫn đúng.
- UI không cộng số research vào Trading Summary.
- `FRESH` chỉ nghĩa health nguồn là `OK`. Backtest, win rate, profit factor và PnL không thành kết luận đã kiểm chứng.
- API local chỉ thấy publication sau khi cấu hình root và khởi động lại process. Không đưa path máy này vào cấu hình production.
- Không deploy, không migration, không bật live trading trong nhiệm vụ UI trừ khi có nhiệm vụ vận hành riêng.

**GO có điều kiện** cho bước tích hợp UI. **NO-GO** cho deploy và cho việc coi màn hình hiện tại là đã nối Research Lab.

## Cập nhật UI

Nhiệm vụ `SECOND-BRAIN-RESEARCH-UI-INTEGRATION-001` nối Overview với năm route GET. Publisher, reader, controller, policy và DTO không đổi. Chi tiết và bằng chứng nằm ở `SECOND_BRAIN_RESEARCH_UI_INTEGRATION.md`.

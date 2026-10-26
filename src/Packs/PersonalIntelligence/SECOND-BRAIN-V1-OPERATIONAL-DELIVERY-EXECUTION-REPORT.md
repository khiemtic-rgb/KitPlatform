# SECOND-BRAIN-V1-OPERATIONAL-DELIVERY — EXECUTION REPORT

Ngày ghi: 2026-10-17. PASS chỉ gắn với kiểm thử đã chạy. Trading Module V1 chưa hoàn thành.

## 1. Mức hoạt động thực tế

FACT. Một luồng fixture đi từ HTTP test host vào PostgreSQL và đọc lại được:

`Trading Data → Context/Evidence → Decision → Recommendation → Risk → Approved Paper Action → Outcome → Reflection → Experience → Policy Proposal`

Điểm vào là `POST api/personal-intelligence/trading/loops`. Tenant lấy từ claim của danh tính đã xác thực. `RequestedTenantId` trong body bị từ chối khi khác tenant đó. Paper chỉ ghi khi Risk cho phép và có phê duyệt riêng. Live bị từ chối trước khi mở kết nối. Mọi trade của luồng này có `source = FIXTURE`.

INFERENCE. Personal Intelligence và Trading đã đủ để kiểm thử xuyên suốt bằng fixture trên cụm kiểm thử. Đây chưa phải vận hành trên API process đang phục vụ việc khác, và chưa phải kết quả giao dịch sàn.

## 2. API đến PostgreSQL

FACT. Test host dùng `WebApplication` + `UseTestServer`. Không mở cổng Kestrel mới và không đụng process `KitPlatform.Api` pid 22936. Host đó đăng ký `TenantContext`, `AddPersonalIntelligencePack()`, policy `PersonalIntelligenceTradingRun`, và controller trong assembly pack.

FACT. Một request thành công ghi và đọc lại trên `kit_pi_validation` cổng 5433: execution `mode = PAPER` theo `decision_id`, trade `source = FIXTURE`, đúng một event `DecisionMade` cho decision đó. Response có `riskAllowed = true`, `paperExecuted = true`, `executionMode = PAPER`, `policyStatus = proposed`, `autonomy = recommend_only`, `evidenceBasis = Fixture`, `reconstructedNetQuantity = 0.06`.

FACT. `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj` ra thư mục output riêng: 0 Error. `Program.cs` gọi `AddPersonalIntelligencePack()`, `AddPersonalIntelligenceAuthorization()`, và `AddApplicationPart` cho assembly chứa `TradingLoopController`. Process đang chạy không bị dừng, nên process đó chưa nạp pack này. Thư mục output đã xóa.

NOT VERIFIED. JWT của process API đang chạy, và một request HTTP vào process đó.

## 3. Các thành phần đã nối tới đâu

| Thành phần | Trạng thái đã kiểm |
|---|---|
| Ledger / import | Luồng ghi trade fixture, đối chiếu bằng `external_trade_id`, đọc lại quantity và price. Import CSV nằm trong request. |
| Decision journal | Context, evidence, decision, outcome, reflection, experience, policy và `pi_events` nằm trong cùng transaction của luồng. |
| Trading Agent | Khuyến nghị từ fixture, có unknowns ghi rõ tape không phải thị trường live và không có export sàn. Autonomy `recommend_only`. |
| Risk Engine | Notional vượt giới hạn trả `riskAllowed = false` và không tạo `trading_execution`. |
| Paper execution | Fill `PAPER` chỉ sau risk và `ApprovedBy`. Live trả HTTP 400 và không tạo decision. |
| Evaluation | `FixturePerformanceEvaluation` đọc các trade vừa lưu và trả net 0.06. Nhãn `Fixture`. |

FACT. Lặp cùng CSV giữ đúng 2 trade và price 100. Mỗi lần gọi vẫn tạo decision mới. Idempotency đã chứng minh cho dòng trade, chưa chứng minh cho cả decision.

## 4. Build, test, tenant, provenance, transaction

FACT. `dotnet test tests/KitPlatform.PersonalIntelligence.Tests` với `KIT_PI_TEST_CONNECTION` trỏ `kit_pi_validation` cổng 5433: Passed 27, Failed 0, Skipped 0. Trong đó 7 bài operational mới:

- không xác thực → 401
- xác thực nhưng không có quyền `personal_intelligence.trading.run` và không có role ADMIN → 403
- tenant B gửi `RequestedTenantId` của tenant A → 403, không có decision với thesis đó
- vòng fixture ghi và đọc lại như mục 2
- lặp fixture không nhân đôi trade
- veto không có execution; live không có decision
- event type sai trước commit làm rollback: 0 trade và 0 decision của thesis đó

Các bài PostgreSQL trước đó vẫn nằm trong 27: cô lập tenant trên journal, rollback journal, unique trade, append-only `pi_events`, paper theo `decision_id`, composite FK reflection, RLS tắt.

FACT. Cụm vẫn là `kit_pi_validation`, port 5433. `pi_events`, `trade`, `pi_policy`, `trading_execution` còn. `pg_policies` trên schema = 0. `relrowsecurity` = 0. Trigger `trg_pi_events_append_only` còn. Không áp lại 385, 387, 388. Không áp 386. Không sửa các file migration đó. Không tạo cụm mới.

Provenance của luồng: event sink là `pack_personal_intelligence.pi_events`, source `operational-loop`. Không có ghi `platform_events` trong SQL của luồng.

## 5. Capability còn thiếu

Schema có bảng nhưng không có repository và không có đường operational: `pi_knowledge`, `pi_memory`, `pi_evaluation`, `pi_agent`, `market_snapshot`, `funding_payment`.

NOT VERIFIED. Gate module `tenants.settings` / `RequirePlatformModule`. Controller cố ý không dùng gate đó vì tenant kiểm thử chưa được bật module.

Chưa có đối soát export sàn, chưa có market snapshot thật, chưa có agent runtime, chưa có production engine, chưa có strategy research.

## 6. Fixture, PostgreSQL, và dữ liệu sàn

| Việc | Cách xác minh |
|---|---|
| Số liệu giao dịch trong luồng mới | Fixture có nhãn. Cột `source = FIXTURE`. Response `evidenceBasis = Fixture`. Không phải kết quả sàn. |
| Ghi, đọc, cô lập tenant, rollback, idempotency trade, giới hạn paper | PostgreSQL thật trên `127.0.0.1:5433` / `kit_pi_validation`. |
| Export sàn, PnL thật, fill live | NOT VERIFIED. Không có file export sàn trong phạm vi này. |

## 7. Git

FACT. `E:\KitPlatform`, branch `main`, HEAD `8c59ceb4f3be537898106ec058ea547406f722e5`, ahead 6 behind 152. Không stage, commit, reset, clean, stash, worktree mới.

Hash `client/admin/src/modules/content/ContentFamixaStudioView.tsx` vẫn `bd1fbb224a421221a43b3fc11988873746754bfa`. Working tree Famixa, NOVIXA và các file modified khác vẫn còn. `Program.cs` và `KitPlatform.Api.csproj` vốn đã modified; lần này thêm reference pack API, `AddApplicationPart`, và `AddPersonalIntelligenceAuthorization`. `AddTestPack`, `AddTestPackAuthorization`, `ProductionTenantActionFilter` vẫn còn trong `Program.cs`.

Pack Personal Intelligence và test project vẫn untracked. Migration 385, 387, 388 và manifest `deploy/ubuntu/migration-files.personal-intelligence.txt` vẫn untracked. Migration 386 vẫn untracked và không thuộc luồng này.

Process `KitPlatform.Api` pid 22936 vẫn chạy sau khi báo cáo được ghi.

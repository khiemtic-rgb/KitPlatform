# SECOND-BRAIN-V1-POSTGRES-INTEGRATION-VALIDATION-001 — EXECUTION REPORT

Ngày ghi: 2026-10-17. PASS chỉ gắn với kiểm thử đã chạy trên PostgreSQL của cụm kiểm thử riêng.

## 1. Database đích

FACT. Cụm PostgreSQL 18 riêng, thư mục dữ liệu `E:\kit-pi-validation-pg`, chỉ nghe `127.0.0.1:5433`. Database `kit_pi_validation`. Trên cụm này không có database `kitplatform` hay `kitplatform_test`. `pg_policies` = 0.

Role `kitplatform` trên cụm đang phục vụ API có `rolcreatedb = false`, nên không tạo database mới ở cổng 5432. `kitplatform_test` đã có 39 bảng public và đang được test Pharmacy dùng; không ghi vào đó.

Không copy dữ liệu người dùng. Tenant, user và person trong bài test là bản ghi tổng hợp. Connection test lấy từ biến `KIT_PI_TEST_CONNECTION`. Source không chứa password. Cụm này dùng trust trên localhost, không có secret trong repo.

## 2. Schema nền và migration đã áp

Trên `kit_pi_validation`, theo thứ tự:

1. `001_extensions.sql` — pgcrypto, citext, `set_updated_at()`.
2. `002_identity.sql` — `public.tenants`, `public.users` và các bảng identity đi kèm.
3. `071_kit_schemas_foundation.sql` — `kit_uuid_v7()`, `kit_bump_row_version()`.
4. `385_pack_personal_intelligence_trading_ledger_v1.sql`.
5. `387_pack_personal_intelligence_loop_v1.sql`.
6. `388_pack_personal_intelligence_loop_integrity_v1.sql`.

Không áp migration 386. Không sửa 385, 386, 387, 388.

## 3. Kết quả trên PostgreSQL

| Kiểm tra | Kết quả |
|---|---|
| Ghi Context, Evidence, Decision và đọc lại đúng tenant, thesis, epistemic kind | PASS |
| Sự kiện `DecisionMade` nằm trong `pack_personal_intelligence.pi_events` | PASS |
| Database này không có bảng `public.platform_events` | PASS |
| Tenant B không đọc được decision của tenant A qua journal | PASS |
| Ghi chéo tenant bị từ chối; đếm SQL cho tenant B = 0 | PASS |
| Lần ghi event sai constraint làm rollback decision; cả decision và event = 0 | PASS |
| Import cùng `(account, external_trade_id)` lần hai trả cùng id, count = 1, price và source không bị ghi đè | PASS |
| Unique index `uq_trade_account_external` tồn tại | PASS |
| UPDATE và DELETE trên `pi_events` bị từ chối; source vẫn `journal` | PASS |
| Paper fill sau risk và phê duyệt tạo đúng một execution `PAPER` cho decision đó | PASS |
| Veto không thêm execution; Live ném lỗi và không thêm execution | PASS |
| Reflection của tenant B không trỏ được outcome của tenant A | PASS |
| Không bảng nào trong `pack_personal_intelligence` bật RLS | PASS |

## 4. Lệnh đã chạy

FACT. `dotnet test tests/KitPlatform.PersonalIntelligence.Tests` với `KIT_PI_TEST_CONNECTION` trỏ `kit_pi_validation` cổng 5433: Passed 20, Failed 0, Skipped 0. Trong đó 8 bài chạy trên PostgreSQL, 12 bài là unit test.

FACT. `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj` ra thư mục output riêng: 0 Error. `Program.cs` vẫn gọi `AddPersonalIntelligencePack()`. Process API đang chạy không bị dừng, nên tiến trình đó chưa nạp pack mới. Thư mục output của lần build này đã xóa.

Nếu thiếu biến môi trường, hoặc database là `kitplatform`, `kitplatform_test`, `pharmacore`, `postgres`, hoặc cổng 5432, fixture ném lỗi. Không có nhánh skip để biến thành PASS.

## 5. Sửa trong phạm vi này

- `IntelligenceJournal.ProbeSecondWriteFailureAsync`: gọi đúng transaction của journal, nhưng event type không hợp lệ để chứng minh rollback. Không nằm trên đường API.
- `tests/KitPlatform.PersonalIntelligence.Tests/PostgresIntegrationTests.cs`: bài integration. Lần chạy đầu đếm mọi execution của account nên lần chạy sau thấy hai dòng `PAPER` cũ. Sửa thành đếm theo `decision_id`.

Không sửa file Famixa, NOVIXA, migration 386, hay file shared ngoài pack.

## 6. Chưa persist hoặc chưa kiểm trên database

Chưa có repository và chưa có bài PostgreSQL cho `pi_knowledge`, `pi_memory`, `pi_evaluation`, `pi_agent`, `market_snapshot`, `funding_payment`.

Chưa có HTTP request vào process API đang chạy.

## 7. Git

FACT. `E:\KitPlatform`, branch `main`, HEAD `8c59ceb4`, ahead 6 behind 152. Không stage, commit, clean, reset, stash. Hash `ContentFamixaStudioView.tsx` vẫn `bd1fbb224a421221a43b3fc11988873746754bfa`.

## 8. Ngoài phạm vi

Không live trading. Không strategy research. Không bật RLS.

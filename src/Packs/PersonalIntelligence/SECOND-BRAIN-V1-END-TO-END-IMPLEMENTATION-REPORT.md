# SECOND-BRAIN-V1 — END-TO-END IMPLEMENTATION REPORT

Ngày ghi: 2026-10-17. Nhãn: FACT là việc đã đo được trong phiên này. INFERENCE là kết luận từ FACT đó. NOT VERIFIED là chưa có bằng chứng. PASS chỉ gắn với kiểm thử đã chạy.

## 1. Năng lực Personal Intelligence đang hoạt động

FACT. Trong pack `personal_intelligence` đã có các kiểu canonical tách khỏi Trading: Context, Decision, Action, Outcome, Reflection, Experience candidate, Policy proposal, Evidence, và sự kiện `pi_events`. Một quyết định bắt buộc có tenant. Một tuyên bố do agent tạo ra không được lưu thành FACT; nếu yêu cầu FACT thì hệ thống hạ xuống INFERENCE.

FACT. Vòng đã kiểm thử trong bộ nhớ: Context/Decision ghi sự kiện vào sink `pack_personal_intelligence.pi_events`. Không có đường ghi `platform_events`. Một outcome đơn lẻ chỉ tạo Policy ở trạng thái `proposed`.

NOT VERIFIED. Các bảng canonical chưa được ghi qua repository Dapper và chưa được áp vào database nào.

## 2. Trading Ledger, importer, journal, experience, risk, demo, evaluation, agent

FACT. Ledger V1 có sẵn được giữ nguyên: Person, Exchange, TradingAccount, Trade, Fee, Position, PortfolioSnapshot. Migration `385` không bị sửa. Không có RLS.

FACT. Importer CSV fixture dùng khóa `(trading_account_id, external_trade_id)`. Dòng trùng bị bỏ qua. Mỗi dòng nhận là `Observation`, không phải `Decision`.

FACT. Risk Engine từ chối độc lập khi notional vượt trần, đòn bẩy vượt trần, độ tin cậy dưới sàn, hoặc chế độ là Live.

FACT. Demo execution là paper. Đề xuất được risk cho phép vẫn bị từ chối nếu chưa có phê duyệt riêng. Phê duyệt không mở được lệnh Live. Kết quả paper mang nhãn `FIXTURE`.

FACT. Trading Agent V1 chỉ `recommend_only`. Khuyến nghị gồm bằng chứng, luận cứ, rủi ro, độ tin cậy và điểm chưa biết. Agent không đổi Policy và không đặt lệnh.

FACT. Evaluation trên fixture tái dựng số lượng ròng và ghi rõ đây không phải hiệu quả giao dịch thật.

NOT VERIFIED. Order, Execution, MarketSnapshot và Funding mới có trong migration `387`. Chưa có repository và chưa có dữ liệu thật. Import từ sàn thật chưa làm.

## 3. Phụ thuộc thực tế

```
ITenantContext + public.users/tenants
        │
        ▼
pack_personal_intelligence.person
        │
        ├── ledger 385: exchange, trading_account, trade, fee, position, portfolio_snapshot
        │
        ├── loop 387: pi_events, context, evidence, decision, action, outcome,
        │             reflection, experience, policy, knowledge, memory, evaluation, agent
        │
        └── trading adapter 387: trading_order, trading_execution, market_snapshot, funding_payment
                │
                ▼
        HistoricalTradeImport → ObservedTrade
                │
                ▼
        TradingAgent.Recommend → RiskEngine.Evaluate
                │
                ├── veto → DemoPaperExecution từ chối
                └── allow + ExecutionApproval riêng → paper fill
                │
                ▼
        FixturePerformanceEvaluation (basis = Fixture)
```

Trading types không nằm trong Core và không nằm trong các record canonical. Agent Runtime và Production Engine không được tạo.

## 4. File, project, migration

| Đường dẫn | Lý do |
|---|---|
| `migrations/385_pack_personal_intelligence_trading_ledger_v1.sql` | Giữ nguyên. Đã có trên `main` dạng untracked. Không sửa vì có thể đã được áp ở nơi khác. |
| `migrations/387_pack_personal_intelligence_loop_v1.sql` | Thêm vòng canonical và phần mở rộng Trading. Dùng 387 vì `386_pack_content_famixa_canon_administer.sql` đã tồn tại untracked trên `main`. |
| `deploy/ubuntu/migration-files.personal-intelligence.txt` | Thêm dòng 387. Không đưa vào manifest prod, content, family-os hay local-os. |
| `.../Intelligence/EpistemicKind.cs` | FACT, OBSERVATION, INFERENCE, HYPOTHESIS, POLICY, PREFERENCE. |
| `.../Intelligence/IntelligenceLoop.cs` | Decision, Policy proposal, sự kiện `pi_events`. |
| `.../Trading/HistoricalTradeImport.cs` | Import CSV idempotent. |
| `.../Trading/RiskEngine.cs` | Veto độc lập. |
| `.../Trading/DemoPaperExecution.cs` | Paper fill, chặn Live. |
| `.../Trading/TradingAgent.cs` | Khuyến nghị, không đặt lệnh. |
| `.../Trading/FixturePerformanceEvaluation.cs` | Đối chiếu và tái dựng trên fixture. |
| `tests/KitPlatform.PersonalIntelligence.Tests/*` | Sáu kiểm thử, chỉ tham chiếu Application pack. |
| 23 file ledger còn lại trong danh sách 25 | Sao chép nguyên trạng, không sửa. |

Project đã biên dịch: `KitPlatform.Packs.PersonalIntelligence.Application`.

Project chưa biên dịch được: `KitPlatform.Packs.PersonalIntelligence.Infrastructure`. Nó tham chiếu `KitPlatform.Infrastructure`, và project đó trên commit `8c59ceb4` lỗi CS0246 vì `IPermissionEngine` không còn định nghĩa, tại `DashboardService.cs` và `CustomerDraftOrderService.cs`. FACT: lỗi này có trước thay đổi của nhiệm vụ. Không sửa hai file shared đó.

## 5. Build, test, và các ranh giới

FACT. `dotnet test tests/KitPlatform.PersonalIntelligence.Tests` trên worktree: Passed 6, Failed 0, Skipped 0. Duration khoảng 67 ms.

Các ca đã chạy:

- Import trùng bị bỏ qua; dòng nhận là Observation.
- Risk từ chối notional, đòn bẩy, độ tin cậy thấp và Live.
- Khuyến nghị đạt risk vẫn cần phê duyệt riêng mới có paper fill.
- Veto chặn paper fill dù đã phê duyệt.
- Agent không tự nâng tuyên bố thành FACT. Policy từ một outcome vẫn `proposed`. Sự kiện trỏ vào `pi_events` và bắt buộc có tenant.
- Fixture BUY 0.10 rồi SELL 0.04 tái dựng net 0.06. Báo cáo ghi đây không phải hiệu quả live.

FACT. Migration 387 không chứa `ROW LEVEL SECURITY` hay `CREATE POLICY`. Tenant đi qua cột `tenant_id` và khóa ngoại composite tới `person`. ADR-008 giữ `RLS_DEFERRED_V1`.

NOT VERIFIED. Cách ly tenant trên Postgres, idempotency bằng unique index khi ghi DB, và provenance khi đọc lại từ DB. Các ràng buộc đó nằm trong SQL nhưng SQL chưa chạy.

## 6. Fixture, dữ liệu thật, phần chưa xác minh

| Việc | Trạng thái |
|---|---|
| Import và đánh giá bằng CSV fixture trong test | FACT, PASS |
| Tái dựng số lượng ròng từ fixture | FACT, PASS |
| Import lịch sử từ sàn, đối chiếu với export thật | NOT VERIFIED. Chưa có nguồn dữ liệu. Không tạo số liệu thật. |
| Hiệu quả chiến lược | Không đánh giá. Fixture không được dùng làm bằng chứng hiệu quả. |

## 7. Hoàn thành, chưa xong, blocker

Đã xong trong phạm vi có thể kiểm thử mà không đụng database dùng chung và không đụng host đang dirty:

- Ranh giới canonical / Trading adapter.
- Event sink `pi_events`, không dual-write.
- Import fixture idempotent.
- Journal quyết định tách khỏi trade.
- Risk veto, paper execution, evaluation fixture, agent recommend-only.

Chưa xong:

- Repository cho bảng mới trong 387.
- Đăng ký pack vào API host.
- Áp migration.
- Dữ liệu giao dịch thật.
- Strategy research.

Blocker:

- `KitPlatform.Infrastructure` trên `8c59ceb4` không biên dịch vì thiếu `IPermissionEngine`. Ledger repository vì thế chưa được build lại trong phiên này.
- `src/KitPlatform.Api/Program.cs` và `KitPlatform.Api.csproj` trên `E:\KitPlatform` đang có thay đổi Famixa/core. Không gắn `AddPersonalIntelligencePack` để tránh ghi đè.
- Không có database riêng đã được chỉ định. Không chạy 385 hoặc 387.

## 8. Tích hợp vào E:\KitPlatform

FACT. Worktree `E:\KitPlatform-wt-second-brain-v1`, branch `second-brain-v1`, tạo từ `8c59ceb4f3be537898106ec058ea547406f722e5`. `E:\KitPlatform` giữ branch `main` và HEAD đó trong lúc tạo worktree.

FACT. 25 hash Trading trước khi sao chép khớp `KIT-TRADING-ISOLATION-PLAN-001`. Không sao chép `bin`/`obj`.

Đồng bộ về `E:\KitPlatform` bằng copy từng file mới. Chỉ ghi đè `migration-files.personal-intelligence.txt` sau khi hash trên `main` vẫn là `b858532adeb2`. Không stage, không commit. Thay đổi Famixa/NOVIXA đang untracked hoặc modified không được đưa vào.

## 9. KIT_MKT và worktree khác

Trước khi đồng bộ, các HEAD sau không bị dùng làm workspace của nhiệm vụ này:

- `E:\KitPlatform` branch `main` tại `8c59ceb4`.
- `E:\KitPlatform-wt-novixa-phase5` tại `66afd2d96512eaf8c0a6f533f2a571616b2781f6`.
- `E:\KitPlatform-wt-phase5-novixa-product-proof` tại cùng commit đó.
- `E:\KitPlatform-wt-video-mvp` tại `45d21801`.
- Hash `ContentFamixaStudioView.tsx` trên `main`: `bd1fbb224a421221a43b3fc11988873746754bfa`.

FACT sau khi copy: các HEAD và hash trên không đổi. `migrations/385` trên `main` vẫn `0db91efbfea2`. File mới copy có hash trùng worktree. `dotnet test` chạy lại trên `E:\KitPlatform`: Passed 6, Failed 0.

## 10. Workspace tạm

FACT. `git worktree remove` không `--force` dừng với exit 128 vì còn file untracked. Lần `--force` gỡ worktree khỏi Git nhưng lần xóa thư mục đầu bị `Permission denied` do tiến trình còn đứng trong thư mục đó. Sau khi rời thư mục, thư mục `E:\KitPlatform-wt-second-brain-v1` đã được xóa (`exists=False`). Branch `second-brain-v1` còn, không có commit riêng. Source cần giữ nằm trên `E:\KitPlatform`, chưa stage.

## 11. Bước tiếp theo

1. Chọn database không dùng chung trước khi áp 385 rồi 387.
2. Gỡ blocker `IPermissionEngine` ở Core mà không trộn diff Famixa, rồi build lại Infrastructure của pack.
3. Thêm repository tenant-filtered cho `pi_events` và các bảng vòng lặp. Vẫn không bật RLS, không ghi `platform_events`.
4. Gắn pack vào host khi `Program.cs` không còn diff của luồng khác.
5. Khi có export thật, chạy lại import và đối chiếu. Giữ kết quả fixture tách khỏi kết quả live.
6. Chưa mở domain khác và chưa làm strategy research.

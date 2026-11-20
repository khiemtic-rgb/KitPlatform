# SECOND BRAIN — RESEARCH EVALUATION V1 IMPLEMENTATION

Trạng thái: **IMPLEMENTATION_COMPLETE_WITH_GAPS**

Lớp so sánh chỉ đọc đã có trong source, đã build và đã qua test trên publication `pub-e65b6912e02a168d`. Agent đang chạy trên VPS B chưa được thay DLL. Publish host từ working tree hiện tại sẽ kéo theo các sửa Infrastructure và Auth chưa thuộc evaluation, nên không deploy.

Commit: `9d28227b4db504ea4b36d80fae30a146ed20eead`

## Phạm vi

`ResearchEvaluationProjector` đọc một `ResearchPublicationRead` đã qua checksum. Nó không gọi `RiskEngine`, không gọi `PaperDecisionRecorder`, không ghi ledger, không tính Sharpe, CAGR, drawdown, expectancy hay equity curve. Ba experiment được so sánh là `EXP-TREND-SMA-10-30-V1`, `EXP-BREAKOUT-DONCHIAN-20-V1` và `EXP-REVERSION-RANGE-20-V1`. `sma-cross-v1` fast=5, slow=20 không nằm trong kết quả.

Cùng snapshot thì `sameSnapshot=true`. Snapshot khác nhau, publication thiếu, hoặc checksum lệch thì verdict là `INSUFFICIENT_DATA` hoặc `INCONSISTENT` và không có dòng experiment. Nguồn ghi `NOT_A_PERFORMANCE_CLAIM` thì verdict là `NOT_SELECTABLE`. Các metric chưa có trong nguồn là `NOT_IN_SOURCE`.

## Thành phần

| File | Việc |
|---|---|
| `ResearchEvaluation.cs` | Projector và contract |
| `ResearchPublicationController.cs` | Hai GET mới, cùng policy `research.read` |
| `ResearchEvaluationTests.cs` | Ba experiment của publication thật, và snapshot lệch |
| `ResearchPublicationTests.cs` | 403 với `trading.run`, publication thiếu, checksum lệch, fixture không phải performance claim |
| `research-view.ts`, `research.api.ts`, `ResearchPanel.tsx` | Bảng so sánh trên panel research |
| `SECOND_BRAIN_RESEARCH_EVALUATION_V1.md` | Cách gọi API |

Không có migration.

## Cách dùng

Cần `personal_intelligence.research.read` và module `personal_intelligence`.

- `GET /api/personal-intelligence/research/evaluations`
- `GET /api/personal-intelligence/research/evaluations/{experimentId}`

Panel Research Lab hiện bảng khi publication đọc được và các experiment cùng snapshot. PnL hiển thị là chuỗi nguồn, không phải kết luận lợi nhuận.

## Build và test

Host `KitPlatform.SecondBrainAgent.Host` build succeeded, 0 error. `NU1903` Newtonsoft.Json 10.0.3 là cảnh báo có sẵn.

`ResearchPublicationTests` và `ResearchEvaluationTests`: 18 passed, 0 failed. Publication thật trả `FRESH`, cùng snapshot `126a733bc6b709fcc1f511e4fb01aff61bd47694bb3328918f8f32dad6ccb2c3`, verdict `NOT_SELECTABLE`, đủ 3 experiment, `liveOrders=false`, `exchangeOrdersSent=0`.

`research-view.smoke.ts` passed. Snapshot lệch không hiện dòng. Checksum `INCONSISTENT` không lộ marker.

## An toàn

Route mới dùng policy research hiện có. `trading.run` nhận 403. Không đổi connection string, không bật worker, không bật model, không mở cổng 5432 và không vào VPS A.

Test checksum lệch xác nhận không có body experiment. Test này không ghi database.

## VPS B

Không deploy. Service `second-brain-agent` vẫn là DLL `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc`. Publication root và năm file JSON đã cài lúc 15:04 +07 không đổi. UI tĩnh tại `/opt/second-brain-agent/ui` chưa có bảng evaluation.

Lý do: cây làm việc còn sửa chưa commit trong `KitPlatform.Infrastructure`, `KitPlatform.Application` và `AgentAuth.cs`. Một publish host sẽ biên dịch các file đó vào process agent.

## Giới hạn còn lại

Publication không có equity curve. Drawdown, expectancy, Sharpe và CAGR không được tính. Claim nguồn vẫn là không phải bằng chứng hiệu suất. Người chỉ có `research.read` vẫn bị layout chuyển khỏi overview, nên không thấy bảng cho đến khi có thêm quyền trading sẵn có của operator. Việc đó không được nới trong lượt này.

## Việc còn lại

Publish host từ đúng commit evaluation cộng overlay session đã chạy, không kèm WIP khác. Rồi restart riêng `second-brain-agent` và gọi hai GET mới trên publication đang gắn. Xây lại UI Second Brain chỉ khi cần bảng trên VPS. Cả hai bước cần một lần deploy riêng vì chúng thay DLL hoặc file tĩnh đang phục vụ.

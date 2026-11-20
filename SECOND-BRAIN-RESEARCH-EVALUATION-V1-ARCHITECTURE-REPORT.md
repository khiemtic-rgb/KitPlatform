# SECOND BRAIN — RESEARCH EVALUATION V1 ARCHITECTURE

Kết luận: **READY_WITH_GUARDS**

Có thể thiết kế và, sau khi được duyệt riêng, triển khai một lớp đánh giá đọc publication hiện có. Chưa được dựng một engine backtest thứ hai, chưa được ghi Paper Ledger, và chưa được coi các số trong publication là kết quả giao dịch thật. Lượt này không sửa code, không đụng VPS, không bật Exchange hay LLM.

## 1. Current capability inventory

| Năng lực | Trạng thái | Bằng chứng |
|---|---|---|
| Research Publication Reader và schema `research-publication/v1` | Đã có và đang chạy | `FileResearchPublicationReader`, năm GET. Runtime VPS B 2026-10-30 15:04 +07 trả `FRESH`, id `pub-e65b6912e02a168d`. |
| So sánh ba experiment đã xuất bản | Đã có dữ liệu, chưa có màn đánh giá | `LAB_RESULT.json` có 3 experiment. API trả `development` và `out_of_sample` dạng JSON đã clone. UI chỉ có `ResearchPanel` trên overview. |
| `SmaCrossStrategy` fast=5, slow=20 | Có ở cây khác, không có trong `E:\KitPlatform` | `E:\KitPlatform-sb-research\...\SmaCrossStrategy.cs`. `Id=sma-cross-v1`, `Version=fast=5,slow=20`. Tín hiệu dùng close đến nến hiện tại, fill ở open nến sau. |
| Ba strategy trong publication | Đã xuất bản, khác SMA 5/20 | `EXP-TREND-SMA-10-30-V1`, `EXP-BREAKOUT-DONCHIAN-20-V1`, `EXP-REVERSION-RANGE-20-V1`. Parent lab là `EXP-SMA-CROSS-V1`, không phải `sma-cross-v1`. |
| Research Host và schema `research_trading` | Không nằm trong repo này | `docs/architecture/trading-paper-integrity-repair-001.md` ghi host ở worktree `E:\KitPlatform-sb-research`. Migration 395–398 của repo này là Content, không phải `research_trading`. |
| Backtest lịch sử trong agent | Chưa có | Không có class backtest trong `E:\KitPlatform`. |
| Signal generation trong agent | Chưa có | Signal SMA chỉ có trong worktree research. |
| `PaperDecisionRecorder` | Đã có, dùng cho một lệnh paper được đề xuất | Ghi decision khi intent chọn paper evaluate và mode là `paper`. Không chạy trên chuỗi nến. |
| `RiskEngine` và `DemoPaperExecution` | Đã có, một lệnh một lần | Live bị từ chối. Paper fill chỉ sau approval riêng, basis `FIXTURE`. |
| Paper Ledger | Đã có và đang giữ số liệu thật của agent | Trên VPS B sau deploy publication: `trade=3`, `pi_events=18`, `pi_task=0`, `exchange_connection=0`. |
| Orchestration intent | Đã có, model tắt | Ba capability: đọc publication, đọc sổ, đánh giá một lệnh paper. `BusinessIntent__Enabled` không có trên VPS B. |
| Equity curve, CAGR, Sharpe, walk-forward | Chưa có | Publication không có các khóa này. Report paper để drawdown, expectancy, profit factor ở dạng sentinel, không phải số dùng làm bằng chứng. |

`development` của experiment đầu có `net_pnl`, `profit_factor`, `win_rate` kiểu chuỗi, kèm `profit_factor_is_performance_evidence=false` và `performance_claim=NOT_A_PERFORMANCE_CLAIM`. **VERIFIED** từ file publication. Các chuỗi đó không được đưa vào báo cáo này như kết quả lợi nhuận.

## 2. Existing architecture

```text
Research lab (ngoài repo)                Agent host (repo này, đang chạy)
LAB_RESULT + paper files                 FileResearchPublicationReader
        │                                          │
        ▼                                          ▼
publication files trên đĩa              GET /research/*  policy research.read
snapshot SHA-256 được ghi tên           không mở file nến
nến không nằm trong publication

Paper path, tách biệt:
một lệnh đề xuất → RiskEngine → DemoPaperExecution → PaperDecisionRecorder → pi_decision / pi_task
```

Reader không ghi database. Publication root là cấu hình process, không phải dữ liệu tenant. Paper ledger thuộc tenant trong `second_brain_agent_v1`. `novixa_prod` nằm ở cụm cổng 5432 và không bị publication đụng.

**UNKNOWN.** Bộ nến ứng với `snapshot_sha256` `126a733bc6b709fcc1f511e4fb01aff61bd47694bb3328918f8f32dad6ccb2c3` đang nằm ở đâu trên VPS B. Publication chỉ giữ hash, không giữ nến.

## 3. Target architecture

Research Evaluation V1 là một phép chiếu có kiểm soát trên publication đã xác minh, không phải một hệ thống giao dịch mới.

```text
Publication (đã checksum)
        │
        ▼
EvaluationProjector
  đọc experiment đã có
  không tính lại lợi nhuận
  không gọi RiskEngine
  không gọi PaperDecisionRecorder
        │
        ▼
EvaluationReport
  verdict = NOT_A_PERFORMANCE_CLAIM khi publication nói vậy
  evidence = publication id + 4 SHA-256 + experiment id
```

Ranh giới:

| Lớp | Việc được làm | Việc không được làm |
|---|---|---|
| Research | Đọc file đã xuất | Sửa publication, tải nến lạ |
| Evaluation | So sánh các experiment trên cùng publication | Ghi sổ, gửi lệnh, biến sentinel thành số 0 |
| Paper | Một lệnh giả lập có approval | Chạy lại history |
| Orchestration | Gọi capability đọc | Bật model để bịa metric |

Phương án ít thay đổi nhất là projector trong pack Personal Intelligence, cạnh reader, trả thêm một GET. Không port `ResearchHost`. Không copy `SmaCrossStrategy` trong pha này, vì strategy đó khác ba experiment đang xuất bản.

Pha tái lập bằng cách chạy lại nến chỉ bắt đầu khi chủ dự án đưa dataset vào contract và chỉ định đúng strategy version. Khi đó mới xét đưa `SmaCrossStrategy` từ worktree research vào repo này, thay vì viết SMA thứ hai.

## 4. Evaluation contract

Đầu vào của pha chiếu, lấy từ publication chứ không phải form tự do:

| Trường | Nguồn V1 | Nếu thiếu |
|---|---|---|
| Strategy ID và version | `experiment_id`, `strategy_version` | Không tạo evaluation |
| Symbol, timeframe | `SOLUSDT`, `1h` trong lab hiện tại | Không suy ra thị trường khác |
| Dataset ID và version | `snapshot_sha256` của lab | Không đánh giá |
| Khoảng thời gian | `extension_start_utc`, `extension_end_utc` cho phần paper; lab không có cửa sổ ngày trong các khóa đã liệt kê | Ghi `UNKNOWN`, không bịa |
| Initial capital | `assumed_capital_usd` | Không giả định số khác |
| Position sizing | Không có trong publication | `NOT_IN_SOURCE` |
| Fee và slippage | Khóa có trong report; giá trị sentinel thì không dùng làm giả định số | `NOT_COMPUTED` |
| Mode | Cố định `publication_projection` | Không có mode live |

Đầu ra:

| Trường | Quy tắc |
|---|---|
| Evaluation ID | SHA-256 của publication id, bốn hash file, experiment id và mode. Cùng đầu vào thì cùng ID. |
| Strategy và dataset | Nguyên văn từ lab, kèm snapshot hash |
| Performance và risk | Chỉ các khóa publication đang có, giữ nguyên sentinel và cờ `*_is_performance_evidence` |
| Trade statistics | `trade_count`, `win_count`, `loss_count` nếu có trong lab. Không tính lại từ tín hiệu |
| Equity và drawdown | `NOT_IN_SOURCE` cho đến khi có chuỗi equity. Không điền 0 |
| Data quality | Health publication, `continuity_breaks`, cờ sample |
| Assumptions | Fill model và thời điểm tín hiệu lấy từ report: quyết định ở close, giả định khớp ở open nến sau |
| Limitations | Publication không phải lệnh thật. `live_orders=false` |
| Warnings | `NOT_A_PERFORMANCE_CLAIM`, `INSUFFICIENT_EVIDENCE`, `EXPERIMENT_NOT_APPROVED` |
| Verdict | `NOT_SELECTABLE` khi claim hoặc approval của nguồn nói vậy |
| Confidence | `LOW` khi evidence là `INSUFFICIENT_EVIDENCE` |
| Evidence | Publication id, schema, publisher, bốn SHA-256, experiment id |

So sánh nhiều strategy là một báo cáo gồm ba evaluation cùng publication id và cùng snapshot hash. Không xếp hạng theo PnL khi `selected_by_pnl=false` và claim không phải bằng chứng.

## 5. Metric definitions

MUST HAVE trong V1 chỉ là những gì publication đã mang, được gắn nhãn đủ điều kiện. Không tính thêm.

| Metric | Phân loại | Định nghĩa | Nguồn | Không được tính khi |
|---|---|---|---|---|
| Total / net return | SHOULD HAVE, chưa đủ dữ liệu để tính trong V1 | Lợi nhuận ròng chia vốn đầu | Lab `net_pnl` và `assumed_capital_usd` chỉ khi cờ evidence đúng và giá trị là số | Claim là `NOT_A_PERFORMANCE_CLAIM` hoặc giá trị sentinel |
| CAGR | FUTURE | Lợi nhuận kép theo năm | Cần ngày bắt đầu, ngày kết thúc và equity | Thiếu một trong ba |
| Profit factor | MUST HAVE ở dạng nhãn | Lời gộp chia lỗ gộp, theo lab | `development` / `out_of_sample` | Cờ `profit_factor_is_performance_evidence=false` thì hiển thị kèm cảnh báo, không dùng để chọn |
| Expectancy | SHOULD HAVE | Kỳ vọng mỗi lệnh | Report đang là sentinel | Sentinel |
| Max drawdown | SHOULD HAVE | Đáy sâu nhất của equity | Không có chuỗi equity | Không có chuỗi. Không lấy sentinel làm 0 |
| Volatility, Sharpe, Sortino | FUTURE | Độ lệch và tỷ lệ lợi nhuận trên rủi ro | Cần lợi nhuận từng kỳ cùng risk-free | Dưới một cửa sổ đã khóa, hoặc thiếu kỳ |
| Risk of ruin | FUTURE | Xác suất cạn vốn theo mô hình | Cần mô hình được duyệt | Chưa có mô hình |
| Total trades, win rate | MUST HAVE ở dạng đếm | Số lệnh và tỷ lệ thắng theo lab | `trade_count`, `win_count`, `loss_count` | Lab `blocked` hoặc thiếu mẫu |
| Average win / loss, holding period, chuỗi thắng/thua | FUTURE | Trung bình và chuỗi | Cần từng lệnh | Publication không xuất danh sách lệnh đã chuẩn hóa. `signals` là object, không phải danh sách lệnh |
| In-sample / out-of-sample | MUST HAVE | Hai khối lab tách nhau | `development` và `out_of_sample` | Không gộp hai khối thành một win rate |
| Walk-forward, sensitivity, regime | FUTURE | Kiểm tra bền | Cần nhiều cửa sổ và lưới tham số | Chưa có |
| Overfitting warning | MUST HAVE | Cảnh báo chọn tham số trên mẫu | `parameters_chosen_on_out_of_sample`, `selected_by_pnl`, `sample_warning` | Không tắt cảnh báo khi mẫu nhỏ |

Công thức return, Sharpe và drawdown chưa được triển khai. Viết công thức trước khi có chuỗi equity sẽ tạo số không tái lập được.

## 6. Data quality và bias

| Rủi ro | Trạng thái | Cách V1 xử lý |
|---|---|---|
| Look-ahead trong SMA 5/20 | **VERIFIED** ở code worktree research: SMA đến nến `t`, fill ở open nến `t+1` nếu nến đó tồn tại | Không chạy code đó trong V1. Không suy ra ba experiment lab cũng làm vậy, dù report ghi `CLOSE_OF_DECISION_BAR` và `NEXT_BAR_OPEN` |
| Look-ahead của lab Python | **UNKNOWN** trong repo này. Script lab nằm ngoài git | V1 không chạy lại lab |
| Survivorship | Một symbol `SOLUSDT` đã chọn sẵn | Ghi vào limitations. Không có universe nên không đo được |
| Data leakage | Tham số không chọn trên out-of-sample, theo cờ lab `parameters_chosen_on_out_of_sample=false` | Giữ cờ. Không kiểm chứng được nếu không có nến |
| Nến trùng hoặc thiếu | Report có `continuity_breaks` rỗng trên publication này | Chỉ báo điều publication nói. Không quét file nến |
| Timezone | Mốc paper là UTC | Không đổi sang giờ local để tính bar |
| Phí và slippage | Có khóa, giá trị report là sentinel | Không điền 10 bps từ bài audit cũ của Research Host. Đó là ví dụ test của host khác |
| Position sizing | Không có trong publication | `NOT_IN_SOURCE` |
| PnL chưa chốt | Paper report tách `open_trades` và `closed_trades` | Không cộng vị thế mở vào lợi nhuận đã chốt |
| Tương lai | Reader không nhận đường dẫn file từ query | Evaluation cũng không nhận đường dẫn |
| Backtest như kết quả thật | Publication đã khóa claim | Verdict V1 phải lặp lại khóa đó |

Tái lập pha chiếu nghĩa là cùng publication id và cùng bốn hash thì cùng evaluation id. Tái lập bằng cách chạy lại strategy cần thêm snapshot bytes đúng hash. Pha đó chưa đủ điều kiện.

## 7. API và UI

API mới, chỉ đọc, cùng policy `personal_intelligence.research.read`:

- `GET /api/personal-intelligence/research/evaluations` trả danh sách ba experiment của publication hiện tại, mỗi cái một evaluation id và verdict.
- `GET /api/personal-intelligence/research/evaluations/{experimentId}` trả một báo cáo.

Khi publication `MISSING`, `INCONSISTENT` hoặc `UNREADABLE`, trả đúng availability đó và không kèm số. Không nhận dataset từ body. Không có POST trong V1.

UI thêm một khối so sánh trong `ResearchPanel`. Khối đó nằm trên overview. Người chỉ có `research.read` vẫn bị `TradingLayout` chuyển khỏi overview. Không nới quyền để sửa việc đó trong pha evaluation.

Không thêm capability intent ở V1. Model đang tắt. Một câu thoại không được tạo evaluation mới.

## 8. Storage và ownership

Không tạo database. Không tạo bảng. Không ghi `pack_personal_intelligence.trade`, `pi_decision`, hay `pi_task`.

Bằng chứng tái lập của pha chiếu là chính publication và hash của nó. Evaluation id tính lúc đọc. KIT Platform vẫn sở hữu `novixa_prod`. Agent vẫn chỉ giữ publication như file đầu vào trên VPS B.

Nếu sau này có pha chạy lại nến, kết quả phải là artifact riêng, checksum riêng, không phải dòng ledger. Việc đó cần phê duyệt schema hoặc thư mục mới và không thuộc V1 này.

## 9. Permission và security

`research.read` đủ cho GET chiếu. Không dùng `trading.run`. Không dùng `ADMIN` làm lối tắt mới.

Evaluation không mở kết nối sàn, không đọc credential, không bật worker. Tham số experiment không được chứa đường dẫn file. `SafeToken` hiện xóa giá trị có `api_key` hoặc dấu sổ. Projector phải giữ quy tắc đó.

## 10. Test và acceptance

Test mới, khi được phép triển khai:

- Publication fixture hiện có cho ba evaluation, verdict `NOT_SELECTABLE`, không có equity.
- Hash file đổi thì evaluation id đổi và availability `INCONSISTENT`.
- Publication thiếu thì không có số.
- `trading.run` không gọi được route mới.
- Gọi route không tăng `trade`, `pi_task`, `pi_events`.
- Sentinel drawdown không thành số 0.
- Hai khối in-sample và out-of-sample không bị cộng.

Nghiệm thu sản phẩm là operator mở được bảng so sánh ba experiment của `pub-e65b6912e02a168d`, thấy claim không phải bằng chứng, và sổ paper không đổi. Chưa nghiệm thu một đường equity hay một lệnh paper mới.

## 11. Implementation phases

1. Projector chỉ đọc và test. Không migration, không restart cho đến khi có gói deploy riêng.
2. GET và một khối UI. Vẫn không ghi ledger.
3. Chỉ sau phê duyệt khác: đưa dataset đúng `snapshot_sha256` vào contract, rồi mới xét chạy `SmaCrossStrategy` sẵn có. Không viết strategy trùng.

## 12. Risks và quyết định cần duyệt

| Việc | Vì sao cần duyệt |
|---|---|
| Chỉ làm pha chiếu publication | Đây là phạm vi ít thay đổi nhất và khớp dữ liệu đang có |
| Không port Research Host trong V1 | Host đó ghi `research_trading` và không có trong repo này |
| Không gọi SMA 5/20 trên publication SOLUSDT | Hai strategy khác version. Gộp chúng là so sánh sai |
| Chưa tính Sharpe, CAGR, drawdown | Không có chuỗi equity trong nguồn đã xác minh |
| UI overview vẫn cần quyền trading để nhìn thấy | Sửa điều hướng là việc riêng, không nới `research.read` |

**INFERRED.** Operator hiện tại thấy được panel vì đã có `trading.run` và `exchange.read`. Điều này đúng với lần kiểm tra quyền trước deploy publication, không đọc lại bảng quyền trong lượt này.

Pha chiếu không đụng VPS A, không đụng `novixa_prod`, và không bật worker. Triển khai code vẫn là một phê duyệt sau, không phải hệ quả tự động của báo cáo này.

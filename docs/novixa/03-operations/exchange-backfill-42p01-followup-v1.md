# Exchange backfill 42P01 trên VPS B — việc riêng (chưa làm)

Ngày ghi: 2026-11-07. Trạng thái: **chờ duyệt**, chưa đụng.

## Triệu chứng

`ExchangeBackfillWorker` (pack PersonalIntelligence) bật mặc định (`ExchangeBackfillOptions.WorkerEnabled = true`).
DB nào chưa chạy manifest `deploy/ubuntu/migration-files.personal-intelligence.txt` thì cứ 2 giây lỗi:

`42P01 relation "pack_personal_intelligence.exchange_backfill_job" does not exist` (câu `UPDATE` đầu `PumpAsync`).

## Đã xử lý

- `kit_famixa` (`kit-famixa-api`): thêm `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false` vào `/etc/kit-famixa/api.env`
  (backup `api.env.bak-backfill-20261107195416`), restart 19:54:16. Lỗi dừng, log có "Exchange backfill worker is disabled.".

## Còn lại (log `postgresql-16-main.log` VPS B, đếm tại 2026-11-07)

| DB / user | Lỗi | Ghi chú |
|---|---|---|
| `novixa_w5_staging` / `novixa_w5_staging` | ~294k | `kit-platform-w5`. Còn thiếu riêng `pack_content.work_job`. |
| `kit_platform_staging` / `kitplatform` | ~190k | Chưa rõ process nào. |
| `novixa_prod` / `kitplatform` | ~104k | Chưa rõ process nào kết nối. Vùng Pharmacy freeze — không đụng khi chưa duyệt. |

## Cách sửa đề xuất

Như Famixa: đặt `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false` trong env của từng process
(đúng `src/Packs/PersonalIntelligence/SECOND_BRAIN_RESEARCH_DEPLOY_READINESS.md` §111, §207). Không chạy manifest Personal Intelligence lên các DB này.

Trước khi làm: xác định process/unit nối tới `novixa_prod` và `kit_platform_staging` (`ss -tnp` tới 5432 + `pg_stat_activity.application_name`/`client_port`),
restart có kiểm tra render như Famixa, smoke Pharmacy theo `pharmacy-demo-audit-freeze.mdc` nếu đụng process prod.

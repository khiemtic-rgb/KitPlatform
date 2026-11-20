# SECOND BRAIN — RESEARCH PUBLICATION ARCHITECTURE COMPLIANCE

Kết luận: **ARCHITECTURE_COMPLIANT_WITH_GUARDS**

Cấu hình `PersonalIntelligence__ResearchPublication__Root` rồi restart riêng `second-brain-agent` không nối agent vào database của KIT Platform và không biến publication thành cơ chế đồng bộ. Việc này chỉ được làm khi các điều kiện ở mục 9 được giữ. Audit này không sửa cấu hình, không ghi file lên VPS, không restart và không chạy SQL ghi.

Bằng chứng runtime lấy lúc 2026-10-30 14:51:39 +07 trên `157.10.53.182`. Không có kết nối tới `103.200.23.229` và không có câu SQL nào tới cổng 5432.

## 1. Database topology

**VERIFIED.** VPS B chạy hai process PostgreSQL 16, hai thư mục dữ liệu, hai cổng.

| Cụm | PID | Data directory | Cổng trong `postmaster.pid` | Database đã thấy |
|---|---|---|---|---|
| `sbagent` | 43039 | `/var/lib/postgresql/16/sbagent` | 5433 | `postgres`, `second_brain_agent_v1`, `template0`, `template1` |
| `main` | 34958 | `/var/lib/postgresql/16/main` | 5432 | không liệt kê, vì không được mở `novixa_prod` |

System identifier của cụm `sbagent`, đọc bằng SQL trên cổng 5433: `7702079316921501630`. Database hiện tại của câu lệnh đó là `second_brain_agent_v1`.

**UNKNOWN.** System identifier của cụm `main`. `pg_controldata` không có trên máy, và audit không kết nối cổng 5432.

**VERIFIED từ cấu hình, không từ nội dung bảng.** `kit-platform-api` dùng `ConnectionStrings__Default` tới `Host=127.0.0.1`, `Port=5432`, `Database=novixa_prod`, `Username=kitplatform`, file `/etc/kit-platform/api.env`. Agent dùng `Host=127.0.0.1`, `Port=5433`, `Database=second_brain_agent_v1`, `Username=sb_agent`, file `/etc/second-brain-agent/agent.env`. Hai chuỗi này không trỏ cùng cổng và không trỏ cùng tên database.

Hai cụm không độc lập chỉ vì khác cổng. Bằng chứng tách rời là hai `postgres -D` khác nhau, hai PID, và hai file cấu hình `/etc/postgresql/16/sbagent/postgresql.conf` với `/etc/postgresql/16/main/postgresql.conf`.

## 2. Agent đang đọc và ghi database nào

**VERIFIED.** Process agent chỉ được cấp một connection string, tới `second_brain_agent_v1` trên `127.0.0.1:5433` với role `sb_agent`. `SecondBrainDatabaseGuard.RequireConnection` từ chối cổng 5432, từ chối mọi database khác `second_brain_agent_v1`, và từ chối `novixa_prod`, `trading_research`, `kit_pi_validation`, cùng địa chỉ `103.200.23.229`. Sau khi mở kết nối, `RequireActualEndpoint` đối chiếu tên database, cổng và system identifier.

`sb_agent` không phải superuser và được login. Catalog của cụm 5433 cho thấy role này có quyền `CONNECT` trên `postgres`, `second_brain_agent_v1`, `template0` và `template1`. `template0` vẫn có `datallowconn = false`. Process đang chạy không dùng các database kia, vì guard chặn connection string trước khi mở. Quyền `CONNECT` rộng hơn contract của host là sự kiện có sẵn, không phát sinh từ publication root.

Trong `second_brain_agent_v1`, agent đọc `public.tenants`, `public.users`, `second_brain_host.operator`, `second_brain_host.auth_session`, `second_brain_host.refresh_token`, và các bảng `pack_personal_intelligence` khi một API ledger hoặc desk được gọi. Các bảng pack đó nằm trong database của agent, không nằm trong `novixa_prod`.

**VERIFIED.** `public.users` có 1 dòng. `AgentAuth.EnsureOperatorAsync` chạy `SELECT COUNT(*) FROM public.users` lúc startup. Nếu đếm lớn hơn 0, method return và không insert tenant, user, operator hay ledger. Restart với dữ liệu hiện tại chỉ đọc số đó.

Không có bằng chứng agent mở `novixa_prod`.

## 3. Luồng đồng bộ hiện hữu

**VERIFIED trên database của agent.** `pg_foreign_server` có 0 dòng. `pg_subscription` có 0 dòng. Extension chỉ có `plpgsql` và `pgcrypto`. Không có foreign data wrapper hay logical subscription trong `second_brain_agent_v1`.

**VERIFIED trong source.** Repository `E:\KitPlatform` không có client nào của KIT Platform gọi `second_brain_agent_v1`. Agent không gọi `kit-platform-api`. `CatalogGroundedIntentInterpreter` chỉ mở HTTP khi một request intent chạy và model được bật. Trên VPS, khóa `PersonalIntelligence__BusinessIntent__Enabled` không có trong `agent.env`.

**UNKNOWN.** Cụm `main` có subscription hoặc FDW trỏ sang cụm `sbagent` hay không. Kiểm tra điều đó phải mở `novixa_prod`, nên không làm trong audit này. Phía agent không có đầu nhận tương ứng.

Exchange connector và `ExchangeBackfillService` chỉ ghi ledger khi một request gọi chúng. `ExchangeBackfillWorker` không được đăng ký. Health lúc 12:41 cùng ngày đã báo `backfillWorkerRegistered=false` và hosted service chỉ có DataProtection với GenericWebHost. Audit 14:51 không gọi lại health. Service vẫn là PID 68847, `NRestarts=0`, nên process đó không bị thay giữa hai lần xem.

## 4. Publication Reader có side effect hay không

**VERIFIED.** `FileResearchPublicationReader.Read` chỉ đọc file. Root rỗng hoặc thư mục không tồn tại trả `MISSING` trước khi mở manifest. Đường đi của API research là `ResearchPublicationController` rồi `IResearchPublicationReader`. Không có `INSERT`, `UPDATE`, ghi file, cache hay queue trong thư mục `Research`.

`ResearchPublicationCapabilityHandler.ExecuteAsync` cũng chỉ gọi reader và trả `wroteData=false`, `externalCall=false`. Handler đó chỉ chạy khi một phiên intent chọn capability research. GET research không đi qua handler này.

Checksum là SHA-256 của bốn file so với manifest. Publication ID và `research-publication/v1` lấy từ manifest. `health=OK` được map thành `FRESH`. `STALE` giữ nguyên. Reader không tính lại tuổi nến tại thời điểm gọi.

Root là một option riêng, `PersonalIntelligence:ResearchPublication:Root`, bind ở `Program.cs` sau `AddPersonalIntelligenceServices`. Không có khóa publication nào điều khiển worker, model hay connection string. `PostConfigure` chỉ ép `ExchangeBackfillOptions.WorkerEnabled=false`.

## 5. Tác động của việc cấu hình root

**VERIFIED.** Giá trị đang chạy là chuỗi rỗng, cả trong `agent.env` và trong environ của PID 68847. Option được đọc lúc process khởi động. Sửa file mà không restart không đổi Reader của process hiện tại.

Khi root trỏ tới thư mục publication và process được khởi động lại, các GET research đọc năm file JSON đó. Không có dòng nào trong luồng này ghi database. Ledger, session, tenant và `novixa_prod` không đổi vì bản thân thao tác đọc file.

Nếu thư mục chưa tồn tại tại thời điểm process chạy, Reader trả `MISSING`. Host không crash vì root sai. Worker và model không bị bật bởi khóa này.

## 6. Tác động của restart `second-brain-agent`

**VERIFIED từ code và trạng thái.** Restart thay process nghe `127.0.0.1:5095`. PID hiện tại là 68847 từ 12:33:46 +07. Trong source, hosted service của host này chỉ bị từ chối nếu tên chứa ExchangeBackfill, Medication, Pharmacy, Content hoặc Famixa. Không có queue, scheduled job hay worker publication.

Startup sau restart đọc lại env, kiểm tra connection string, đối chiếu system identifier, kiểm tra `auth_session`, rồi `EnsureOperatorAsync`. Với `users=1`, bước cuối chỉ là một `SELECT`. Session và refresh token nằm trong PostgreSQL nên còn sau khi process chết. JWT cũ vẫn dùng được nếu session chưa revoke và secret không đổi.

**INFERRED.** Khoảng trống lắng nghe khoảng vài giây, dựa trên lần restart đã quan sát trước đó, không đo lại trong audit này. Request đang dở trên cổng 5095 sẽ đứt. Không có job nền để mất hoặc chạy lại.

`kit-platform-api` PID 37553, nginx, `postgresql@16-sbagent` và `postgresql@16-main` đều active. Restart unit `second-brain-agent` không nằm trong các unit đó.

**UNKNOWN.** Vị trí key ring của DataProtection. Xác thực agent dùng JWT ký bằng secret trong env, không phụ thuộc key ring đó cho các GET research.

## 7. Rủi ro

Restart làm gián đoạn ngắn riêng cổng 5095. Không có nguy cơ ghi trùng ledger từ publication, vì reader không ghi.

`EnsureOperatorAsync` sẽ ghi tenant và user mẫu nếu `public.users` về 0 trước restart. Hiện tại đếm bằng 1. Đây là điều kiện phải xem lại ngay trước restart, không phải trạng thái vĩnh viễn.

`sb_agent` có `CONNECT` tới các database khác trên cụm 5433. Đổi connection string có thể dùng quyền đó. Cấu hình publication không đụng connection string, và guard vẫn từ chối database lạ.

Chưa biết cụm `main` có kênh đồng bộ ngược tới cụm `sbagent` hay không. Không có dấu hiệu phía agent. Cấu hình root không mở kênh mới.

Publication `FRESH` chỉ có nghĩa file health ghi `OK`. Nó không làm mới dữ liệu thị trường và không ghi sang sổ giao dịch.

## 8. Năng lực tái sử dụng

Không cần database, service hay repository mới.

Dùng `FileResearchPublicationReader`, `ResearchPublicationController`, và năm GET đã có. Dùng đúng package `deploy/second-brain-agent/research-publication-v1`, publication `pub-e65b6912e02a168d`. Dùng host DLL đang chạy `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc`. Quyền `personal_intelligence.research.read` của `sb-operator` và module `personal_intelligence` trên tenant `SECOND_BRAIN` đã có từ lần kiểm tra trước. Audit này không đọc lại bảng quyền.

## 9. Phương án ít thay đổi nhất

Chưa thực hiện. Thứ tự khi được duyệt riêng:

1. Xác nhận lại `users` lớn hơn 0, DLL không đổi, worker vẫn false, và connection string vẫn là `127.0.0.1:5433` / `second_brain_agent_v1`.
2. Copy năm file JSON của package vào một thư mục mới, ví dụ `/opt/second-brain-research-publication-v1`, owner `sbagent`, rồi đối chiếu SHA-256. Không ghi vào thư mục UI hay thư mục host.
3. Sửa đúng một dòng `PersonalIntelligence__ResearchPublication__Root` trong `/etc/second-brain-agent/agent.env`. Giữ nguyên connection string, worker, URL và secret.
4. Restart chỉ `second-brain-agent`.
5. Kiểm tra health, DB health, cùng DLL, worker tắt, không có ExchangeBackfill, và GET publication trả `FRESH` với id `pub-e65b6912e02a168d`.

Không sửa `/etc/kit-platform/api.env`. Không restart `kit-platform-api`. Không mở cổng 5432.

## 10. Nghiệm thu và rollback

Nghiệm thu khi PID mới phục vụ `127.0.0.1:5095`, cả hai health trả HTTP 200, database vẫn `second_brain_agent_v1` trên cổng 5433 với cùng system identifier, DLL không đổi, worker tắt, và năm GET research trả `FRESH` mà không tăng lệnh hay giao dịch.

Rollback là trả dòng root về chuỗi rỗng từ bản sao `agent.env` đã lưu, rồi restart lại chỉ `second-brain-agent`. GET publication phải trở lại `MISSING`. Xóa thư mục publication chỉ sau khi process đã đọc root rỗng. Không đụng DLL, session, grant hay cụm `main`.

## Điều kiện bảo vệ

- Chỉ một khóa env và một unit systemd.
- Thư mục publication phải có đủ file và đúng checksum trước restart.
- `public.users` phải lớn hơn 0 ngay trước restart.
- Không đổi connection string, secret, worker, model.
- Không kết nối cụm `main`, `novixa_prod`, cổng 5432, hay VPS A.
- Không bật Exchange, backfill, LLM, và không gọi API tạo lệnh trong lúc nghiệm thu.

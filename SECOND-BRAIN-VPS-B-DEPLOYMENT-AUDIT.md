# SECOND BRAIN AGENT V1 — VPS B DEPLOYMENT AUDIT

Ngày đọc máy: 2026-10-30, khoảng 08:57 +07. Máy: `157.10.53.182`, hostname `interdata.1793262774`.

Phạm vi: chỉ đọc. Không deploy, không sửa cấu hình, không restart service, không migration, không seed, không đụng VPS A `103.200.23.229`. Không kết nối PostgreSQL cổng 5432. `kit-platform-api`, Nginx và PostgreSQL `16/main` vẫn `active` tại thời điểm đọc.

## 1. Kết luận tổng thể

`PARTIALLY_DEPLOYED`

Một host Second Brain đang chạy trên loopback `127.0.0.1:5095`, nối database riêng `second_brain_agent_v1` trên cụm `16/sbagent` cổng `5433`. Guardrail của process đang chạy cho thấy worker tắt và không có giao dịch live trong dữ liệu đã đọc.

Process đó không phải commit mục tiêu `9135596a9cb8a91014d930bc7219348a06a8bf5a`. Binary đang chạy không chứa commit này. Bản candidate mới hơn có trên đĩa, đã chạy thử rồi dừng, và cũng không chứa commit `9135596`.

## 2. Commit mục tiêu và commit thực tế

| Mục | Giá trị |
|---|---|
| Commit mục tiêu | `9135596a9cb8a91014d930bc7219348a06a8bf5a` |
| Parent đã kiểm trên repo local | `ad06c4a81b16f7aca6808828eb273eb70a1af39a` |
| Subject local | Record the Second Brain Agent V1 isolated release baseline. |
| Thời điểm local | 2026-10-29 22:24:40 +0700 |
| Có trên `origin` | Không. `origin/second-brain/paper-proposal-v1` vẫn là parent `ad06c4a`. |
| Git object trên VPS B | Không. `/opt/second-brain-agent`, `/opt/second-brain-agent-candidate`, `/opt/kit-platform`, `/root` đều không có `.git`. |

Commit đọc từ binary:

| Artifact | Chuỗi `9135596` | Chuỗi parent đầy đủ `ad06c4a81b16…` | Kết luận commit |
|---|---|---|---|
| DLL đang chạy `/opt/second-brain-agent/host/KitPlatform.SecondBrainAgent.Host.dll` | Không | Không | `UNKNOWN` |
| DLL candidate `/opt/second-brain-agent-candidate/host/KitPlatform.SecondBrainAgent.Host.dll` | Không | Có | Parent `ad06c4a` được nhúng lúc build. Commit mục tiêu `UNKNOWN` từ binary. |

Đối chiếu checksum, không phải commit id trong binary:

- DLL đang chạy trùng hash trong `b-deployed.sha256` của commit local: `891E5726D532B2923A92F38E24C902256B92CF6A326EBD748B213B71DDA9ACC7`, 78848 byte.
- DLL candidate trùng hash trong `candidate-release.sha256` của cùng commit local: `90265F6C3E87565EA38579943D437D8195AD5DDE1C74DD95B55DAACF82BE87A2`, 83456 byte.
- Năm file nguồn host trong commit `9135596`, khi tính SHA-256 theo CRLF, trùng các dòng source của `candidate-release.sha256`. File manifest đó mô tả nguồn candidate. Binary candidate được build khi HEAD còn là parent, trước khi commit `9135596` tồn tại.

Vì binary đang chạy không mang commit id, commit của process đang phục vụ là `UNKNOWN`.

## 3. Artifact, hash và đường dẫn

### Đang được process thực thi

| Mục | Giá trị |
|---|---|
| Đường dẫn | `/opt/second-brain-agent/host/KitPlatform.SecondBrainAgent.Host.dll` |
| SHA-256 | `891E5726D532B2923A92F38E24C902256B92CF6A326EBD748B213B71DDA9ACC7` |
| Kích thước | 78848 byte |
| Mtime file | 2026-10-28 23:31:31 +0700 |
| Map trong process | `/proc/43568/maps` trỏ đúng file này |
| Working directory | `/opt/second-brain-agent/host` |
| UI | `/opt/second-brain-agent/ui` |

UI đang phục vụ, SHA-256:

| File | SHA-256 | Byte |
|---|---|---|
| `ui/index.html` | `BCF870925D0C65115C3D0160CE6AA58B1813A6F209ED69E7AF99D8EAB05EE8FB` | 409 |
| `ui/assets/index-DmF68tyh.js` | `7C337D09588F1775A8C2B3289B7CFCD4F3B2CAE2BE148127ED601845102AF659` | 1305300 |
| `ui/assets/index-BdOndhxL.css` | `98ADF782BD027847CFEAC99BC2F2735177375EA17BB836601CE1D7290CA699C5` | 2940 |

Thư mục đang chạy không có `KitPlatform.Packs.Pharmacy.Application.dll`.

Mtime file là dấu thời gian trên đĩa. Binary không có metadata build nào ghi commit, nên thời điểm biên dịch theo nghĩa commit là `UNKNOWN`. Thời điểm đưa vào service là lúc unit hiện tại start: 2026-10-29 20:22:25 +07.

### Có trên đĩa, không phải process đang chạy

| Đường dẫn | SHA-256 | Byte | Mtime file |
|---|---|---|---|
| `/opt/second-brain-agent-candidate/host/KitPlatform.SecondBrainAgent.Host.dll` | `90265F6C3E87565EA38579943D437D8195AD5DDE1C74DD95B55DAACF82BE87A2` | 83456 | 2026-10-29 21:10 +07 |
| `/tmp/sb-b-upload/host/KitPlatform.SecondBrainAgent.Host.dll` | `891E5726D532B2923A92F38E24C902256B92CF6A326EBD748B213B71DDA9ACC7` | 78848 | cùng hash với bản đang chạy |

UI candidate trùng từng byte với UI đang phục vụ. Candidate có thêm `KitPlatform.Packs.Pharmacy.Application.dll`, 850432 byte. File đó không nằm trong map của process đang chạy.

`/tmp/sb-b-upload` là bản copy còn sót, không phải executable đang chạy.

## 4. Service, PID, port, process

| Unit | Active | Enabled | Ghi chú |
|---|---|---|---|
| `second-brain-agent.service` | `active` / `running` | `enabled` | Unit file `/etc/systemd/system/second-brain-agent.service`, 472 byte, mtime 2026-10-29 20:22:17 +0700 |
| `second-brain-agent-candidate.service` | `inactive` / `dead` | không còn unit file | `MainPID=0`, `FragmentPath` trống |
| `postgresql@16-sbagent.service` | `active` / `running` | `enabled-runtime` | PID 43039 |
| Timer tên second / sbagent / brain / trading | không có | | |

Process đang phục vụ:

| Mục | Giá trị |
|---|---|
| PID | 43568 |
| User | `sbagent` |
| Start | Thu 2026-10-29 20:22:25 +07 |
| Executable | `/usr/bin/dotnet` → `/usr/lib/dotnet/dotnet` |
| Command | `/usr/bin/dotnet /opt/second-brain-agent/host/KitPlatform.SecondBrainAgent.Host.dll` |
| Working directory | `/opt/second-brain-agent/host` |
| Restart policy | `on-failure`, `RestartSec=5`, `NoNewPrivileges=true` |
| `NRestarts` | 0 |
| Environment file | `/etc/second-brain-agent/agent.env` |
| Runtime | `Microsoft.AspNetCore.App 10.0.13`, `Microsoft.NETCore.App 10.0.13` |

Listen:

| Port | Bind | Process |
|---|---|---|
| 5095 | `127.0.0.1` | `dotnet` PID 43568 |
| 5096 | không listen | curl `127.0.0.1:5096` thất bại, HTTP 000 |
| 5433 | `127.0.0.1` | `postgres` PID 43039, cụm `sbagent` |
| 5432 | `127.0.0.1` | `postgres` PID 34958, cụm `main`. Không mở kết nối. |
| 5000 | `127.0.0.1` | `dotnet` PID 37553, `kit-platform-api` |
| 9091 | `127.0.0.1` | `python3` PID 37504 |

Nginx `sites-enabled` và `sites-available` không có `second-brain`, `5095`, hay `5096`.

Process 43568 không có kết nối TCP established tại thời điểm đọc. Không có socket tới `103.200.23.229` trên máy.

## 5. PostgreSQL

Cụm đọc bằng `pg_lsclusters`:

| Ver | Cluster | Port | Status | Data directory |
|---|---|---|---|---|
| 16 | `sbagent` | 5433 | online | `/var/lib/postgresql/16/sbagent` |
| 16 | `main` | 5432 | online | `/var/lib/postgresql/16/main` |

`/etc/postgresql/16/sbagent/start.conf` có chế độ `auto`. Cụm start lúc 2026-10-29 20:21:29 +07. Data directory mtime 2026-10-29 20:21:27 +0700.

Truy vấn chỉ trên cổng 5433, database `second_brain_agent_v1`:

| Mục | Kết quả |
|---|---|
| `current_database()` | `second_brain_agent_v1` |
| `current_setting('port')` | `5433` |
| `system_identifier` | `7702079316921501630` |
| Database trên cụm này | `postgres`, `second_brain_agent_v1`, `template0`, `template1` |

Schema ngoài catalog hệ thống: `pack_personal_intelligence`, `public`, `second_brain_host`.

Bảng chính đã có, gồm `person`, `trade`, `trading_account`, `trading_order`, `trading_execution`, `pi_operation`, `pi_task`, `exchange_connection`, `exchange_backfill_job`, `exchange_execution_sync`, `second_brain_host.operator`, `public.tenants`, `public.users`.

Ledger migration: không có bảng nào tên chứa `migrat`, `schema_version`, `flyway`, hoặc `ledger`. Việc schema nào được áp từ file SQL nào là `UNKNOWN`. Sự có mặt của `pi_task` và các bảng exchange cho thấy các object của dải 385–393 và 399 đang tồn tại. Byte SQL đã áp không kiểm được trên server vì cây cài không giữ các file migration.

Số dòng đọc từ `pg_stat_user_tables` và các câu đếm:

| Bảng | Số |
|---|---|
| `exchange_connection` | 0 |
| `exchange` | 1 |
| `trading_order` status `paper` | 1 |
| `trading_execution` mode `PAPER` | 1 |
| `pi_operation` | 2 |
| `trade` | 2 |
| `second_brain_host.operator` | 1 |
| `exchange_backfill_job` | 0 |

Cổng 5432 không được mở session. Database `second_brain_agent_v1` có hay không trên cụm NOVIXA là `UNKNOWN`.

Process đang chạy khai báo kết nối: host `127.0.0.1`, port `5433`, database `second_brain_agent_v1`, user `sb_agent`, có mật khẩu. Chuỗi kết nối không chứa `103.200.23.229` và không dùng cổng 5432. Giá trị mật khẩu không đưa vào báo cáo.

## 6. Guardrails

| Điều kiện | Kết quả | Trạng thái |
|---|---|---|
| `PersonalIntelligence__ExchangeBackfill__WorkerEnabled` | `false` trong environ của PID 43568 và trong `/etc/second-brain-agent/agent.env` | `PRESENT_AND_VERIFIED` |
| `backfillWorkerRegistered` | `false` trong `GET /api/health` | `PRESENT_AND_VERIFIED` |
| Hosted services | chỉ `DataProtectionHostedService` và `GenericWebHostService` | `PRESENT_AND_VERIFIED` |
| Bind | `ASPNETCORE_URLS=http://127.0.0.1:5095`. Không có `0.0.0.0`, không có địa chỉ VPS A | `PRESENT_AND_VERIFIED` |
| Database riêng | host loopback, cổng 5433, tên `second_brain_agent_v1` | `PRESENT_AND_VERIFIED` |
| Socket tới VPS A | `ss` không thấy `103.200.23.229` | `PRESENT_AND_VERIFIED` |
| JWT riêng | `SecondBrain__JwtSecret` dài 64. Khác `Jwt__Secret` trong `/etc/kit-platform/api.env` và khác `JWT_SECRET` trong `/etc/kit-platform/secrets.generated`. DLL đang chạy chứa chuỗi `SecondBrainAgent` | `PRESENT_AND_VERIFIED` |
| `SecondBrain__SystemIdentifier` trên service đang chạy | key không có trong environ và không có trong `agent.env` | `NOT_DEPLOYED` |
| System identifier của cụm | `7702079316921501630`, khớp key trong `candidate.env` | Cụm `PRESENT_AND_VERIFIED`. Service đang chạy không dùng key này |
| `ResearchPublication__Root` | chuỗi rỗng | `PRESENT_AND_VERIFIED` |
| Exchange connection | 0 dòng. Không có ciphertext, không có `permission_names` | `PRESENT_AND_VERIFIED` |
| Lệnh live trong database | 1 order `paper`, 1 execution `PAPER`. Không có mode khác | `PRESENT_AND_VERIFIED` cho dữ liệu đã đọc |
| API từ chối `live:true` | không gọi, vì không phải GET health | `UNKNOWN` |
| Live trading / quyền giao dịch ngoài phạm vi | không có credential sàn và không có dòng live. Khả năng code cũ còn nhận lệnh live khi bị gọi là `UNKNOWN` | dữ liệu `PRESENT_AND_VERIFIED`; hành vi endpoint `UNKNOWN` |
| Kết nối TCP ra ngoài của agent | không có established socket của PID 43568 | `PRESENT_AND_VERIFIED` tại thời điểm đọc |
| Worker hoặc timer nền khác của Second Brain | không có | `PRESENT_AND_VERIFIED` |
| DataProtection | journal của PID cũ 43418 ghi key có thể lưu không mã hóa. File key vẫn ở `/opt/second-brain-agent/.aspnet/DataProtection-Keys/`. PID 43568 không có dòng log lặp lại | `PRESENT_BUT_NOT_VERIFIED` cho process hiện tại |
| Candidate env | có `SecondBrain__SystemIdentifier=7702079316921501630`. Quét file không thấy VPS A, `novixa_prod`, hay cổng 5432. Candidate không chạy | cấu hình file `PRESENT_BUT_NOT_VERIFIED` |

`ASPNETCORE_ENVIRONMENT` của process đang chạy là `Production`.

## 7. Log và health

Health, GET loopback, 2026-10-30:

`GET http://127.0.0.1:5095/api/health` → HTTP 200

```json
{"status":"ok","service":"second-brain-agent","database":"second_brain_agent_v1","workerEnabled":false,"backfillWorkerRegistered":false,"hostedServices":["Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService","Microsoft.AspNetCore.Hosting.GenericWebHostService"]}
```

`GET http://127.0.0.1:5095/api/health/db` → HTTP 200

```json
{"status":"ok","database":"second_brain_agent_v1","value":1,"workerEnabled":false}
```

`GET http://127.0.0.1:5096/api/health` → không kết nối được.

Journal `second-brain-agent.service`, toàn bộ cửa sổ đang giữ là khoảng 8 dòng ngày 2026-10-29:

| Thời điểm | PID | Việc |
|---|---|---|
| 20:22:18 +07 | systemd | Start unit |
| 20:22:19 +07 | 43418 | Cảnh báo DataProtection không có XML encryptor, key `d7d422c6-cb68-4a1f-b1fe-eb613b320f6e` |
| 20:22:25 +07 | 43418 | Stopped. Đây là process cũ |
| 20:22:25 +07 | 43568 | Started. Đây là process hiện tại |

Không có dòng application log nào của PID 43568 sau câu Started. Log cảnh báo key thuộc process đã dừng.

Journal candidate:

| Thời điểm | Việc |
|---|---|
| 2026-10-29 21:14:08 +07 | Start transient unit, DLL candidate |
| 2026-10-29 21:14:16 +07 | Stopped, `Deactivated successfully` |

Candidate chạy khoảng 8 giây rồi dừng. Không còn listen.

## 8. Thành phần đã có và còn thiếu

| Thành phần | Phân loại |
|---|---|
| Unit `second-brain-agent` đang chạy, loopback 5095 | `PRESENT_AND_VERIFIED` |
| Binary đang chạy, hash `891E5726…`, 78848 byte | `PRESENT_AND_VERIFIED` là artifact cũ. Commit `UNKNOWN` |
| Commit `9135596` trên VPS hoặc trong binary đang chạy | `NOT_DEPLOYED` |
| Candidate DLL `90265F6C…` trên đĩa | `PRESENT_BUT_NOT_VERIFIED` đối với commit mục tiêu. `NOT_RUNNING` |
| Port 5096 và unit candidate | `NOT_RUNNING` |
| Cụm `16/sbagent`, database `second_brain_agent_v1`, identifier `7702079316921501630` | `PRESENT_AND_VERIFIED` |
| Bảng chính của Agent V1 | `PRESENT_AND_VERIFIED` |
| Ledger migration và byte SQL đã áp | `NOT_DEPLOYED` / phiên bản file là `UNKNOWN` |
| `WorkerEnabled=false`, backfill không đăng ký | `PRESENT_AND_VERIFIED` |
| Không nối VPS A, không dùng JWT của KitPlatform API | `PRESENT_AND_VERIFIED` |
| `SystemIdentifier` trên service đang chạy | `NOT_DEPLOYED` |
| Không có credential sàn, không có lệnh live trong DB | `PRESENT_AND_VERIFIED` |
| Từ chối `live:true` của binary đang chạy | `UNKNOWN` |
| UI hash trùng manifest local | `PRESENT_AND_VERIFIED` |
| Nginx / DNS / cổng public | `NOT_DEPLOYED` |
| DLL pharmacy trong thư mục candidate | có trên đĩa, không được process đang chạy nạp |
| .NET 10.0.13 | `PRESENT_AND_VERIFIED` |
| Database cùng tên trên cổng 5432 | `UNKNOWN` vì cố ý không kết nối |

## 9. Việc cần làm để đạt `DEPLOYED_AND_RUNNING`

`DEPLOYED_AND_RUNNING` trong nhiệm vụ này nghĩa là đúng commit mục tiêu, binary đó đang chạy, database riêng và guardrail đã xác minh. Hiện còn các khoảng trống sau:

1. Publish một artifact từ đúng cây `9135596`, rồi đối chiếu hash mới. Binary candidate hiện có nhúng parent `ad06c4a`, không nhúng `9135596`.
2. Đưa artifact đó vào một thư mục phục vụ mới. Bỏ `KitPlatform.Packs.Pharmacy.Application.dll` khỏi gói trước khi cài.
3. Gắn `SecondBrain__SystemIdentifier=7702079316921501630` vào env của unit sẽ chạy. Giữ `WorkerEnabled=false`, bind `127.0.0.1`, database `second_brain_agent_v1` cổng 5433.
4. Có ledger hoặc một đối chiếu schema chỉ đọc trước khi áp SQL mới. Chưa có ledger, nên chưa thể khẳng định database đã đúng byte của commit.
5. Health của process mới phải trả `workerEnabled=false`, `backfillWorkerRegistered=false`, và `/api/health/db` vẫn là `second_brain_agent_v1`.
6. Xác minh lại process map trỏ đúng DLL mới và DLL đó có thể được nhận diện với commit đã build.

Chưa cần Nginx để đạt trạng thái chạy loopback. Mở public là một quyết định khác.

## 10. Lộ trình tiếp theo

Nhiệm vụ này dừng ở báo cáo. Không có bước triển khai nào được thực hiện.

### Có thể làm trên máy local, không đụng VPS

- Giữ nguyên service đang chạy.
- Build publish từ commit `9135596` trên máy dev và ghi SHA-256 của DLL mới.
- Đối chiếu file SQL của commit với danh sách bảng đã đọc ở mục 5, vẫn chỉ đọc.
- Push commit lên `origin` chỉ khi có yêu cầu riêng. Hiện commit chưa có trên remote.

### Cần phê duyệt trước khi đụng VPS B

- Thay DLL đang phục vụ hoặc start lại `second-brain-agent`.
- Tạo unit mới, kể cả chạy lại candidate trên 5096.
- Sửa `agent.env`, kể cả thêm `SystemIdentifier`.
- Chạy migration, seed, hoặc backfill.
- Xóa hoặc thay thư mục `/opt/second-brain-agent`.
- Reload Nginx, đổi UFW, đổi DNS, hoặc bind khác loopback.
- Bật `WorkerEnabled`, nối sàn, hoặc gửi lệnh giao dịch.
- Mọi thao tác trên cổng 5432 và mọi thao tác trên VPS A.

Rollback đã có sẵn cho trạng thái hiện tại là dừng đúng unit `second-brain-agent`. Không dùng rollback đó trong lần audit này. Rollback không được restart `kit-platform-api`, Nginx, hay cụm `16/main`.

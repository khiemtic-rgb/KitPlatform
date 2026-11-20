# FAMIXA EP01 — IMPLEMENTATION PLAN LOCK V1

**Date:** 2026-10-17  
**Status:** Plan only. No source edit besides this file. No database write, no provider call, no media, no binding change, no credential change, no migration, no restart, no build, no deploy.  
**Candidate build:** `30ba4061-a443-4c0d-85aa-1d13ff4fdedb`  
**Tenant:** `FAMIXA_PRODUCTION` (`4028941b-49b3-4ba2-8815-778daef4a2d3`)  
**Official build left untouched:** `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
**Evidence used:** `FAMIXA_CURRENT_STATE_FORENSIC_REPORT_V1.md`; the 2026-10-17 Director Preview authority verification (same-day read of local `kitplatform`, files under `App_Data/kit-video-i2v`, and the running API); Master Architecture index; `KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md` ADR-08; `FAMIXA_PRODUCTION_STANDARD_AND_PILOT_PLAN_V1.md`; `FAMIXA_SHORT_QUALITY_PIPELINE_V1_REPORT.md`; `FAMIXA_LOCAL_LIPSYNC_TIMELINE_FIX_V1_REPORT.md`; `FAMIXA_EP01_DIRECTOR_WINDOW_CLICK_GATE_V1_REPORT.md`; `KPA-FILM-UNIVERSE-NEXT-GATE-001-REPORT.md`. Source was opened only for the functions named below.

Each conclusion is marked `PROVEN`, `PROPOSED`, or `UNKNOWN`. A catalog price is not an invoice. An OS `ACCEPTED:*` binding is not a human accept.

---

## 1. Mục tiêu và tiêu chí hoàn thành EP01

**Mục tiêu.** `PROPOSED` Một tập EP01 sáu shot trên build `30ba4061`, xem được bằng Preview của một phiên `FAMIXA_PRODUCTION` hợp lệ, đúng phần kịch bản đã khóa, và được một người nghiệm thu trên đúng artifact id và hash. Official `4ef164d6` không bị backfill, không nhận bản sao media, và không bị sửa dòng.

**Tiêu chí hoàn thành.** Chưa đạt. Mỗi dòng dưới đây là điều kiện, không phải trạng thái hiện tại.

| # | Tiêu chí | Grade của trạng thái hiện tại |
|---|---|---|
| C1 | Sáu shot `EP01-SC01-SH01` … `SH06` đều có playback trong Production OS trên `30ba4061` | `PROVEN` chưa đạt. OS hiện có chain ở SH01 và SH02. SH03 có một task Gemini `RUNNING` và không có artifact. SH04–SH06 không có task OS |
| C2 | Trên mỗi shot, video take, voice, lip-sync và mix đang current là cùng một thế hệ, và mix là file Preview phát | `PROVEN` chưa đạt ở SH01. SH02 khớp parent video/lip-sync nhưng lệch đồng hồ. Voice tenant của cả hai shot là `UNKNOWN` |
| C3 | Khoảng thoại do người gõ, nếu câu thoại ngắn hơn hình quá 0.75 giây | `PROVEN` máy chấm tồn tại. `UNKNOWN` liệu snapshot tenant đã có khoảng đó |
| C4 | Một người xem đúng file Preview phát và ghi ACCEPT hoặc REJECT theo artifact id + SHA-256, kèm hành động, liên tục, chuyển động, và miệng trên shot có thoại | `PROVEN` chưa có hàng review người. `ACCEPTED:MIX` không phải điều này |
| C5 | Master là một artifact `ASSEMBLY` mới, gồm đúng sáu shot đã ACCEPT, theo thứ tự, có hash riêng và một ACCEPT riêng | `PROVEN` chưa có. File `b9fed1d7-50c2-42bd-ba92-ecae40726d3f` dài 13.042 giây là hai shot |
| C6 | ADR-08 có thư caller cho assembly tập và hành động vẫn ngoài Core | `PROVEN` thư này chưa có. Caller policy vẫn OPEN |

Preview tenant chưa được tuyên bố là đang phát. Điều kiện để một phiên hợp lệ mở được nó nằm ở mục 4, nhóm A. Bằng chứng trình duyệt chưa có.

---

## 2. Nguyên nhân gốc đã được chứng minh

Những mục này là nguyên nhân của khoảng cách hoàn thành, không phải danh sách mọi file cũ.

1. **`PROVEN` Preview official không đi qua được hàng NULL-tenant.** `GetActiveBindingAsync` lọc `tenant_id` bằng tenant của người gọi (`ProductionWorkRepository`). Binding official có `tenant_id` NULL. Bridge không thấy `CURRENT:MIX`. Kết quả đúng đường code là preview rỗng, lý do `VISIBLE_MISSING`. GET không đăng nhập trả 401. Đây không phải lỗi thứ tự bridge.

2. **`PROVEN` Trên build tenant, Preview và nhãn VIDEO không cùng một artifact ở SH01.** Bridge phát `CURRENT:MIX` đã verify trước. Mix `33f22b13-1021-42d3-91af-a8ac3a5d800d` (1,548,906 byte, 5.042 giây, SHA-256 file `d6f7bde38ee81b380f5deb71e9c8e3a32262fdbc908755fee2893e56b967b142`) tạo ngày 26/09 từ video import `c4948374-9fef-41ec-8997-4407f66f9920` (1,529,738 byte) và Fal `b58e679d-5ae1-470a-8152-efb7161d4fa5`. Ngày 27/09 `CURRENT:VIDEO_TAKE` đổi sang Runway `81b29b5c-9e5e-492f-8e40-154c43604507` (646,555 byte). `CURRENT:LIPSYNC` và `VISIBLE` đổi sang fixture `7c20d436-cca7-4e4d-b571-345e24037b7b` (808 byte). Endpoint `authoritative-video` đọc take, nên nhãn VIDEO nêu Runway trong lúc player phát mix cũ.

3. **`PROVEN` SH02 tenant khớp parent nhưng không khớp đồng hồ.** Mix `72c6eb46-0c0a-4845-b4d3-d5e573cd67ae` (1,422,990 byte, 8.000 giây, SHA-256 file `78216013541205afea5a131d92d53e166ba9171beddc2111b05b74ffea73538a`) có parent là video `eb1072d0-aa03-4493-9dd7-db24f2123d27` và Fal `a40edfef-db48-44e6-9935-f2ee7febba7c`. Hai parent đó cũng là `CURRENT:VIDEO_TAKE` và `CURRENT:LIPSYNC`. Lip-sync dài 2.500 giây. Khớp parent không chứng minh hình, thoại, hay miệng đúng.

4. **`PROVEN` SH03–SH06 chưa là phim OS.** SH03 Gemini `RUNNING` từ 2026-09-26 22:24, không có artifact. SH04–SH06 không có task OS trên build này. Ảnh legacy không được tính là playback.

5. **`PROVEN` Câu thoại SH03 đang khóa bị cắt.** Hằng `Sh03RecordedLine` là `Điểm thấp thì lần sau gỡ...`. `PrepareVoiceDraft` từ chối shot có `DirectDialogue` và `LineComplete = false`. Phần chữ còn thiếu chưa được một người viết. Click-gate ghi rõ những chữ đó không được bịa.

6. **`PROVEN` Câu hành động SH04 trong bản ghi cũ bị cắt.** Prefix gãy là `(Người mẹ đón lấy, liếc nhanh con số đỏ chót rồi nhìn thẳng vào mắt con.` Câu đã đóng trong contract tương lai là `Người mẹ đón lấy, liếc nhanh con số đỏ chót rồi nhìn thẳng vào mắt con.` Đó là sửa dấu ngoặc đã ghi, không phải một beat mới.

7. **`PROVEN` Chấp nhận người và chấp nhận OS là hai việc.** Register invariant: `ACCEPTED != Approval`. Quality contract yêu cầu review id bằng playback id, kèm attestation hành động, liên tục, chuyển động, và miệng trên shot có thoại. Không có hàng review người nào được đọc.

8. **`PROVEN` Assembly sáu shot chưa có thẩm quyền.** ADR-08 further caller policy OPEN, không phải Core. Controller `CreateVideo` có nhánh `AssembleEpisode` gọi `AssembleEpisodeOsAsync`. File 13.042 giây đã được tạo ngày 2026-10-16 trên shot `EP01` của build tenant. Việc hành động đã chạy một lần không đóng thư caller, và file đó không phải master sáu shot.

9. **`PROVEN` Máy local này không có đường gọi đủ provider của pilot SH02.** Credential local duy nhất là `FAMIXA_PRODUCTION` / `runway` / `gen4_turbo`. Không có version Fal, ElevenLabs, Google, Gemini. `IRunwayTransport` đăng ký `DryRunRunwayTransport`. Veo, ElevenLabs và Fal không đăng ký là `I*ExecutionAdapter` ở composition root hiện tại. HTTP client legacy vẫn được đăng ký. Pilot một lần mỗi stage không tự tạo credential và không tự đổi DryRun sang live.

---

## 3. Điểm chưa biết hoặc cần quyết định

| Id | Việc | Grade |
|---|---|---|
| U1 | Trình duyệt của một phiên ADMIN `FAMIXA_PRODUCTION` có phát đúng blob đã qua SHA hay không | `UNKNOWN`. Code có cổng. Phiên thật chưa được ghi |
| U2 | `CURRENT:VOICE` của SH01 và SH02 trên `30ba4061`: artifact id, hash, duration, câu chữ | `UNKNOWN`. Hai báo cáo không đặt tên voice tenant |
| U3 | Snapshot tenant đã lưu `speechStartSec` và `speechEndSec` do người gõ hay chưa | `UNKNOWN` |
| U4 | SHA-256 của video import SH02 `eb1072d0` có trùng official Veo `b44e530b` hay không | `UNKNOWN`. Cùng kích thước 1,437,239 byte là `PROVEN`. Cùng hash thì đây là bản sao, không phải nguồn pilot |
| U5 | Hình Runway SH01 `81b29b5c` có đúng beat Minh vào phòng hay không | `UNKNOWN`. Chưa được xem |
| U6 | Mix tenant SH02 `72c6eb46` có điểm đỏ, miệng im ở đuôi, và đúng câu `Dạ… nay con làm bài không tốt.` hay không | `UNKNOWN`. Nhận xét hình đó thuộc official mix `58297402`, không chuyển sang file tenant |
| U7 | Phần còn lại của câu SH03 sau `Điểm thấp thì lần sau gỡ` | `UNKNOWN`. Không được viết trong kế hoạch này |
| U8 | Pixel SH06. Lần fetch trước trên URL official trả HTTP 401. Lượt authority không tải lại | `UNKNOWN` |
| U9 | Credential và `series_build` trên VPS sau 2026-10-15 | `UNKNOWN`. Kế hoạch này không dùng VPS |
| U10 | Số dư và hóa đơn. Runway tenant đã ghi 401 rồi “not enough credits” trước take thành công ngày 27/09 | `UNKNOWN` ngoài chuỗi lỗi đã lưu |
| U11 | Task Gemini SH03 `RUNNING` sẽ được đóng bằng fail có lý do, hay để nguyên đến khi có artifact | Cần quyết định. Không tự hủy trong kế hoạch này |
| U12 | Build pilot SH02 là build mới như click-gate đã viết, hay chính `30ba4061` mà nhiệm vụ này khóa | Cần Architect. Xem D-A |
| U13 | Thư ADR-08 cho phép gọi assembly tập sau sáu ACCEPT, vẫn ngoài Core | Cần Architect. Xem D-E. Chưa có thì không assembly |

---

## 4. Nhóm thay đổi

Năm nhóm. Không tách thành từng nút UI. Nhóm A và phần đọc của nhóm B không sửa source. Nhóm C không viết thêm câu thoại. Nhóm D không assembly. Nhóm E không gọi provider.

### Nhóm A — Preview và authority

**Việc.** Chứng minh một phiên đăng nhập hợp lệ của `FAMIXA_PRODUCTION` mở Preview của `30ba4061`, và artifact id backend resolve trùng file UI phát. Không đổi xác thực. Không thêm shortcut tenant. Không gán `tenant_id` cho official. Không sửa `series_pilot.active_build_id`.

**Điều kiện phiên được phép mở Preview tenant.** `PROVEN` từ source đang mở:

1. Request đã xác thực. Controller mang `[Authorize(Roles = "ADMIN")]`. GET không đăng nhập đã trả 401.
2. Module nền tảng `kit_content` (`RequirePlatformModule`).
3. `ProductionTenantActionFilter` gọi `RequireTenant()` trước action. `HttpProductionCallerTenant` lấy tenant từ `ITenantContext` đã xác thực. Body, build id và workspace không được dùng làm tenant.
4. Tenant claim đó phải là `4028941b-49b3-4ba2-8815-778daef4a2d3`. `GetActiveBindingAsync` chỉ trả binding có cùng `tenant_id`.
5. URL phải mang build `30ba4061` và shot `EP01-SC01-SHxx`. `graph_json.buildId` mà UI đang đọc là build này. `series_pilot.active_build_id` lại là official `4ef164d6`. Mở nhầm official thì Preview rỗng. Không sửa hàng pilot để “cho dễ mở”.

**Cách xác minh id backend trùng file UI.** `PROPOSED` một lượt có phiên, chưa làm:

1. `GET /api/content/production/shots/30ba4061-a443-4c0d-85aa-1d13ff4fdedb/EP01-SC01-SH01/preview` phải trả `empty: false`, `artifactId` `33f22b13-1021-42d3-91af-a8ac3a5d800d`, `byteSha256` khớp bản ghi, role mix.
2. `GET .../preview/media` cùng phiên. SHA-256 của body phải bằng `byteSha256` đó. Server đã rehash trong `ReadPreviewMediaAsync` qua `OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes`. Lệch hash thì API không trả byte (`FAILED_VERIFICATION` hoặc không có body).
3. UI `ContentFamixaSeriesTab` chỉ giữ blob khi `previewBytesMatch(blob, meta.byteSha256)` đúng, rồi ghi `osPreviewIdentityByShot`. Bằng chứng cần có là identity trên bàn shot bằng đúng `33f22b13` và hash đó. Ảnh chụp player không đủ.
4. Cùng phiên, `GET .../authoritative-video` của SH01 được kỳ vọng nêu `81b29b5c`. Đó là hai endpoint đúng vai trò. Khác id là lệch dữ liệu đã chứng minh, không phải lý do đổi thứ tự bridge.
5. Lặp bước 1–3 cho SH02. Id kỳ vọng của Preview là `72c6eb46-0c0a-4845-b4d3-d5e573cd67ae`.

**Không tuyên bố.** `PROVEN` lượt authority không mở trình duyệt. Cho đến khi bước 3 có bản ghi phiên, Preview tenant là đường code và file, chưa phải đường đã xem.

### Nhóm B — Đồng nhất artifact SH01 và SH02

Không đổi thứ tự trong `OfficialVideoAuthorityBridgeV1.ResolveAsync`. Thứ tự verified `CURRENT:MIX`, rồi `CURRENT:LIPSYNC`, rồi `ACCEPTED:VIDEO_TAKE` đang chọn đúng hàng nó được viết để chọn. Lệch nằm ở binding, không nằm ở thứ tự.

**SH01 — giữ, bỏ vai trò, tạo lại.**

| Artifact | Vai trò hiện tại | Quyết định | Điều kiện nghiệm thu |
|---|---|---|---|
| Runway `81b29b5c-9e5e-492f-8e40-154c43604507` | `CURRENT:VIDEO_TAKE` | `PROPOSED` giữ file và giữ vai trò cho đến khi một người xem đúng id này | ACCEPT chỉ khi người xác nhận hành động, mặt, và đạo cụ trên chính file này. REJECT thì take này không còn là nguồn lip-sync. Take mới là một permit riêng, ngoài cap SH02 |
| Import `c4948374-9fef-41ec-8997-4407f66f9920` | parent lịch sử của mix, không còn là video current | `PROPOSED` giữ file. Không bind lại làm current để Preview “khớp mix” | Không nghiệm thu làm hình hiện tại |
| Fal `b58e679d-5ae1-470a-8152-efb7161d4fa5` | không còn `CURRENT:LIPSYNC` | `PROPOSED` giữ file. Không bind lại | Không nghiệm thu. Nó khớp import, không khớp Runway hiện tại |
| Fixture `7c20d436-cca7-4e4d-b571-345e24037b7b` | `CURRENT:LIPSYNC` và `VISIBLE` | `PROPOSED` không dùng làm sản phẩm. Gỡ vai trò current chỉ khi lip-sync mới của đúng take đã verify và được bind bởi action OS | 808 byte không thể ACCEPT |
| Mix `33f22b13-1021-42d3-91af-a8ac3a5d800d` | `CURRENT:MIX` và `ACCEPTED:MIX` | `PROPOSED` giữ file. Preview tiếp tục phát nó cho đến khi mix mới, dựng từ take đã chọn và lip-sync mới, được bind `CURRENT:MIX` bởi `MIX_VIDEO` | Mix cũ không được ACCEPT hình. `ACCEPTED:MIX` hiện có không được kể là duyệt người |
| Voice SH01 | chưa đặt tên | `UNKNOWN` | Không tạo lip-sync khi chưa có voice verified và câu chữ khớp dòng đã khóa |

`PROPOSED` Lip-sync mới và mix mới của SH01 chỉ bắt đầu sau khi người đã ACCEPT hoặc REJECT `81b29b5c`, đã có voice current, và đã tự gõ hai đầu khoảng thoại nếu câu ngắn hơn hình quá 0.75 giây. Parent của lip-sync mới phải là take đó và voice đó. Parent của mix mới phải là take đó và lip-sync mới. Không lấy fixture làm đầu vào.

**SH02 — đánh giá đúng hình, thoại, miệng, rồi mới tính đến tạo lại.**

`PROVEN` những điều sau không đủ để giữ mix làm shot đạt:

- Cùng duration 8 giây giữa video và mix.
- Parent id của mix trùng current video và current lip-sync.
- Kích thước gần official mix (1,422,990 so với 1,423,715).

`PROPOSED` thứ tự đánh giá, toàn bộ là đọc và một người xem, trước mọi provider:

1. So SHA-256 `eb1072d0` với official Veo `b44e530b`. Trùng hash: video tenant là bản sao official, không phải nguồn pilot click-gate (“no official SH02 take is reused”). Dừng trước khi gọi Veo mới và ghi U4 đã đóng theo hướng trùng.
2. Đọc `CURRENT:VOICE` SH02. Không có voice verified, hoặc duration cách lip-sync quá 0.75 giây khi chưa có khoảng thoại: mix không đạt hợp đồng đồng hồ. Không suy câu chữ từ duration.
3. Một người xem mix `72c6eb46` và ghi REJECT hoặc ACCEPT tạm trên đúng id và hash, với bốn attestation. Nhận xét official `58297402` không được chép sang.
4. Chỉ khi REJECT, và chỉ trong cap mục 9, mới tạo lại. Một still, một Veo video-only 720p, một ElevenLabs, một Fal v3, một mix FFmpeg. Không retry cùng fingerprint. Không mở SH01 trong cap đó.

`PROPOSED` Giữ nguyên `eb1072d0`, `a40edfef`, `72c6eb46` trên đĩa dù người REJECT. Chúng là bằng chứng thế hệ cũ. Thế hệ mới là artifact id mới. Không sửa mix cũ cho “khớp” 2.5 giây.

### Nhóm C — Sáu shot, phần đã khóa và phần chưa được viết

Không viết lại canon. Bảng dưới tách câu đã có trong contract khỏi việc còn phải một người chốt.

| Shot | Đã khóa trong contract / báo cáo | Chưa được xác nhận | Trạng thái OS trên `30ba4061` | Việc kế hoạch cho phép |
|---|---|---|---|---|
| SH01 | Beat tương lai: Minh bước vào, đầu cúi, quai cặp kéo; Linh đã lau bàn và ngẩng lên. Ingress lịch sử vẫn ghi Linh vào | Hình Runway tenant chưa được xem (U5) | Có chain, lệch thế hệ | Xem nhóm B. Không nằm trong cap SH02 |
| SH02 | Câu nói `Dạ… nay con làm bài không tốt.` Beat: ngập ngừng, cặp lên ghế, mở khóa, tờ bài gấp, hai tay, nhìn xuống, điểm đỏ | Hình và miệng của mix tenant (U6). Khoảng thoại chưa đọc (U3) | Chain đủ parent video/lip-sync, lệch đồng hồ | Một lần mỗi stage sau REJECT và sau khi cổng credential/adapter được duyệt riêng |
| SH03 | Câu đang ghi `Điểm thấp thì lần sau gỡ...` bị máy từ chối voice. Hành động giấy điểm dùng chung câu đã đóng với SH04. Ảnh legacy: giữ pose, tờ giấy trắng. Graph QA không phải review | Phần câu bị cắt (U7). Không điền giúp | Gemini `RUNNING`, không artifact | Không `CREATE_VOICE`. Không still mới trong khi task cũ còn `RUNNING` trừ khi Architect đóng task đó |
| SH04 | Hành động đã đóng: người mẹ đón lấy, liếc điểm đỏ, nhìn thẳng mắt con. Không có câu thoại direct trong hàng contract (`DirectDialogue` false, spoken rỗng). Ảnh legacy: giữ pose, thẻ trắng | Pixel OS chưa có. Nhãn graph `finalSource = RUNWAY` trong khi host là Fal và model `wan-2.1` là nhãn cũ, không phải việc sửa trong gói này | Không task OS | Không bind URL legacy thành playback. Still và video chỉ sau permit riêng, sau SH02 ACCEPT |
| SH05 | Câu `Lát mẹ rảnh… chỉ lại chỗ này cho con nhé?` Beat tương lai: thở ra, ngồi mép sofa, liếc Linh, không được đã ngồi sẵn từ đầu | Ảnh legacy “đã ngồi sẵn” là ghi chú file cũ, không phải ACCEPT, cũng không phải lý do sửa câu | Không task OS | Cùng cổng permit riêng. Không nâng file legacy |
| SH06 | Câu `Ngồi xích lại đây, lấy bút ra mẹ xem nào.` Beat: Linh cười, vỗ đệm cạnh Minh; Minh đã ngồi từ shot trước; đệm và bút | Pixel chưa được lấy (U8) | Không task OS | Không fetch URL CloudFront trong gói này. Không bịa hình từ URL 401 |

`PROVEN` `PilotExecuted` trong `ShortProductionQualityPipelineV1` đang `false`. Bật cờ đó không hoàn thành tập.

### Nhóm D — Nghiệm thu và master

**Bằng chứng một shot.** `PROPOSED` Một ghi chú của người xem, không phải checkbox graph, gồm đủ:

- build `30ba4061`, shot code, role `MIX` trừ khi Architect chọn một role khác trước lúc xem
- artifact id
- SHA-256
- kích thước và duration đã đo trên byte vừa phát
- ACCEPT hoặc REJECT
- bốn mục: `ACTION_MATCHES_BEAT`, `CONTINUITY_WITH_NEIGHBOR`, `MOTION_PERFORMS_BEAT`, và với shot có thoại `MOUTH_ON_SPEECH_INTERVAL`
- tên người và thời điểm

Thiếu id hoặc hash thì ghi chú không nghiệm thu. ACCEPT trên id khác file Preview vừa phát không có hiệu lực. File mới không kế thừa ACCEPT cũ.

`PROPOSED` Chưa thêm bảng `ShotHumanReview`. Pilot D4 vẫn đóng trong next-gate. Ghi chú nằm ngoài OS và phải được ghi là chưa phải hàng `pack_content`. Không ghi ACCEPT vào `production_bindings`.

**Master.** `PROPOSED` Chỉ sau C1–C4 cho cả sáu shot và sau thư ADR-08 (D-E):

- Một lần `ASSEMBLE_EPISODE` trên build tenant.
- Đầu vào là sáu mix đã ACCEPT, đúng thứ tự `Ep01ShotOrder`.
- Artifact `ASSEMBLY` mới, không nối thêm vào `b9fed1d7`.
- SHA-256 và kích thước của file mới được ghi.
- Người xem file đó và ACCEPT riêng. ACCEPT các shot không ACCEPT master.
- Không ghi binding `FINAL`.

`PROVEN` Không có thư ADR-08 thì nhóm này không chạy, kể cả khi sáu shot đã ACCEPT.

### Nhóm E — Chi phí và quyền thực thi

| Lớp | Được làm sau khi kế hoạch này được duyệt | Không được làm bởi kế hoạch này |
|---|---|---|
| Đọc và kiểm thử local | Đối SHA, đọc preview JSON khi có phiên, ffprobe file đã có, test hồi quy không provider | Không được kể là đã xem hình |
| Tạo media có phí | Không có lượt nào trong lần thực thi đầu. Cap SH02 ở mục 9 chỉ có hiệu lực sau REJECT mix hiện tại và sau quyết định credential/adapter | Không nhân cap SH02 thành sáu shot. Không tự retry |
| Đổi quyền hoặc dữ liệu | Không | Không credential, không đổi `DryRunRunwayTransport`, không backfill tenant, không rebind tay, không đóng task Gemini, không migration, không deploy, không đụng official |

`PROVEN` Phê duyệt cần áp đúng phạm vi là đoạn “Blocking decision” của `FAMIXA_EP01_DIRECTOR_WINDOW_CLICK_GATE_V1_REPORT.md`: một shot `EP01-SC01-SH02`, một lần mỗi stage, thất bại thì lưu attempt và không gửi lại cùng fingerprint, không assembly, không đụng official. Báo cáo đó ghi “Approve or refuse”; nó không chứa chữ ký chấp thuận. Nhiệm vụ khóa kế hoạch này yêu cầu dùng đúng phạm vi đó, không nới thành sáu shot. `PROPOSED` Phạm vi đó không bao gồm việc tạo key, đổi DI, hay chọn build `30ba4061` thay cho “build tenant mới”. Ba việc đó là D-A, D-B, D-C.

---

## 5. File và hàm dự kiến, theo nhóm

Những file dirty hiện có được giữ. Không revert. Nhóm không có hàng “sửa” thì lần thực thi không mở diff.

| Nhóm | Sửa source lần này | Đọc hoặc gọi, không đổi |
|---|---|---|
| A | Không | `ContentProductionController.GetPreview`, `GetPreviewMedia`, `GetAuthoritativeVideo`; `ProductionCreateVideoService.GetPreviewAsync`, `ReadPreviewMediaAsync`; `ProductionExecutionService.GetPreviewAsync`; `ProductionWorkExecutionEngine.ResolveVisiblePreviewAsync`; `OfficialVideoAuthorityBridgeV1.ResolveAsync`; `ProductionWorkRepository.GetActiveBindingAsync`; `HttpProductionCallerTenant.RequireTenant`; `ProductionTenantActionFilter`; `ContentFamixaSeriesTab` khối preview khoảng dòng 2045–2086; `previewBytesMatch` trong `famixa-short-acceptance.ts` |
| B | Không, cho đến khi một lượt tạo media được duyệt riêng. Lúc đó dùng action OS đã có: `CreateLipsyncAsync`, `MixVideoAsync`, `ProductionLipsyncSpeechSource` | Cùng bridge. Không sửa thứ tự. Không sửa `ProductionMixContract` |
| C | Không. Khi một người đưa câu SH03 còn thiếu, lúc đó mới sửa `Sh03RecordedLine` và bản TS tương ứng, trong một gói riêng | `ShortProductionQualityPipelineV1.Ep01ContractRows`, `PrepareVoiceDraft` |
| D | Không, trừ khi D4 được duyệt sau. D4 là migration content mới, không apply Production, không thêm kiểu Core | `AssembleEpisodeOsAsync` chỉ được gọi sau D-E. `ShotProductionQa` không được dùng làm nơi ghi ACCEPT OS |
| E | Không | `ContentPackDependencyInjection` chỉ để xác nhận DryRun còn đó trước khi ai đó đề nghị đổi. Đổi DI là một quyết định khác, không nằm trong diff của kế hoạch này |

---

## 6. Hợp đồng dữ liệu và logic, trước và sau

**Trước, `PROVEN`.**

- Preview playback: mix verified hiện tại nếu binding cùng tenant người gọi. SH01 phát `33f22b13`. SH02 phát `72c6eb46`. Official phát rỗng.
- Video authority: take verified current. SH01 là `81b29b5c`. SH02 là `eb1072d0`.
- Lip-sync current: SH01 fixture `7c20d436`. SH02 Fal `a40edfef` dài 2.500 giây.
- `ACCEPTED:MIX` tồn tại và không phải duyệt người.
- Assembly hiện có `b9fed1d7` là 13.042 giây, hai shot.
- Cửa sổ thoại: thiếu thì lip-sync tenant EP01 trả `MISSING_TIMELINE` trước Fal. Cửa sổ hợp lệ thì FFmpeg cắt local rồi mới đưa byte sang adapter. Official film không đi đường cắt này.
- Voice SH03: `PrepareVoiceDraft` trả không cho phép, mã `SPOKEN_LINE_INCOMPLETE`.

**Sau, `PROPOSED`, chỉ khi các cổng tương ứng mở.**

- SH01: một lip-sync mới có parent là take người đã chọn và voice current; một mix mới có parent là take đó và lip-sync đó; `CURRENT:MIX` trỏ mix mới; fixture không còn current. Id cũ còn trên đĩa.
- SH02: hoặc người ACCEPT `72c6eb46` trên đúng hash, hoặc một thế hệ mới một lần mỗi stage với id mới. Không sửa byte của `72c6eb46`.
- SH03–SH06: mỗi shot có keyframe, video, voice khi shot có thoại, lip-sync, mix, tất cả trong OS của `30ba4061`. Câu SH03 chỉ đổi khi người đưa chữ.
- Review: ghi chú id + hash. Không có hàng binding mới mang nghĩa duyệt người.
- Master: một `ASSEMBLY` mới sau sáu ACCEPT và sau thư ADR-08. `b9fed1d7` giữ nguyên.

**Không đổi.** Thứ tự bridge. Vai trò `FINAL`. Tolerance 0.75 giây. Mapping duration Veo. Tenant official NULL. Credential. Transport DryRun. Cho đến khi một quyết định ở mục 10 nói khác.

---

## 7. Kiểm thử tích hợp và bằng chứng nghiệm thu

Lần thực thi đầu, nếu Architect chỉ duyệt kế hoạch này, là đọc và đối chiếu. Không provider.

| Bằng chứng | Đạt khi | Chưa đủ |
|---|---|---|
| Phiên Preview SH01 | JSON preview ghi `33f22b13` và hash; body media cùng hash; UI identity cùng id | 401, preview rỗng, hoặc chỉ một ảnh player |
| Phiên Preview SH02 | JSON và media và UI cùng `72c6eb46` và hash `78216013…` | Duration 8 giây một mình |
| Lệch có chủ đích | `authoritative-video` SH01 vẫn là `81b29b5c` trong cùng phiên | Đổi bridge để hai endpoint trùng id |
| Official không bị mở | Cùng phiên, preview `4ef164d6` SH02 vẫn rỗng `VISIBLE_MISSING` | Một thay đổi làm official phát được |
| SHA file | Hash đĩa của hai mix tenant khớp hash API | Kích thước gần official |
| Voice | `GET .../voice` đặt tên artifact, hash, duration cho SH01 và SH02, hoặc trả thiếu dependency | Suy voice từ parent của mix |
| Cửa sổ thoại | Snapshot shot ghi cả hai đầu do người gõ, hoặc ghi là chưa có | Duration 5 hoặc 10 trên picker |
| Đồng hồ SH02 | Lip-sync 2.500 giây trong mix 8 giây được ghi là chưa đạt nếu không có cửa sổ người gõ | “Parent khớp nên đạt” |
| Nghiệm thu người | Ghi chú đủ các trường mục 4 nhóm D | `ACCEPTED:MIX`, graph QA, checkbox |
| Hồi quy không phí, chỉ khi sau này có diff Famixa | `ShotProductionOutcomeV1Tests`, `ShortProductionQualityPipelineV1Tests`, `ProductionLipsyncSpeechSourceV1Tests`, smoke `famixa-short-quality-pipeline` và `famixa-director-timing-ui` | Một test xanh được gọi là tập đã xong |

`PROPOSED` Dừng và không sang shot kế khi bằng chứng phiên không trùng id, hoặc khi người REJECT mà chưa có permit mới.

---

## 8. Thứ tự, phụ thuộc, điều kiện dừng

```text
Duyệt kế hoạch (mục 10, các mục không chi tiền)
  → A. Một phiên FAMIXA_PRODUCTION, đối id Preview SH01 và SH02
  → B0. Đọc voice, cửa sổ thoại, SHA video SH02 so với Veo official
  → B1. Người xem đúng mix mà Preview phát
  → (chỉ nếu REJECT SH02, và chỉ sau D-B + D-C + D-D) một lần mỗi stage SH02
  → Người ACCEPT hoặc REJECT file mới
  → SH01: người xem take Runway; lip-sync và mix mới chỉ sau voice + cửa sổ + permit riêng
  → Người viết nốt câu SH03
  → SH03 rồi SH04, SH05, SH06, mỗi shot một chuỗi permit riêng, sau ACCEPT shot trước
  → D-E thư ADR-08
  → một assembly sáu shot và ACCEPT riêng của file đó
```

**Dừng ngay khi:**

- Phiên hợp lệ không ra đúng id mix đã nêu, hoặc hash media lệch. Không vá tenant filter.
- Có đề nghị đổi thứ tự bridge, backfill official, copy media official, hoặc sửa `4ef164d6`.
- SHA SH02 trùng Veo official mà vẫn định dùng file đó làm nguồn pilot.
- Hết một lần của một stage SH02, kể cả thất bại. Không gửi lại cùng fingerprint.
- Người REJECT vì sai mặt hoặc sai hành động lần thứ ba trên cùng shot. Trả về phương pháp pilot, không tăng cap trong task.
- Câu SH03 vẫn cụt mà một lệnh voice được chuẩn bị.
- Task Gemini SH03 còn `RUNNING` mà một still SH03 mới được chuẩn bị.
- Sáu shot chưa ACCEPT, hoặc chưa có thư ADR-08, mà `ASSEMBLE_EPISODE` được gọi.
- Việc sắp làm cần credential, đổi DryRun, migration, deploy, hoặc đụng file dirty ngoài danh sách một gói đã duyệt.
- Working tree bị dọn, stash, hoặc revert.

Nhóm A không phụ thuộc nhóm B. Nhóm B không phụ thuộc SH03–SH06. Master phụ thuộc cả sáu ACCEPT và D-E. Cap SH02 không mở khóa SH01.

---

## 9. Ước lượng stage có phí

Không gọi provider từ kế hoạch này. Số dưới đây là catalog trong pilot plan ngày 2026-10-17, không phải số dư, không phải hóa đơn.

| Stage | Khi nào được tính | Số catalog | Lần được quyền bởi cap SH02 |
|---|---|---|---|
| Đọc, đối hash, ffprobe, Preview có phiên, FFmpeg trên file đã có | Sau khi duyệt phần không chi tiền | 0 USD provider | Không giới hạn đọc. Không tạo artifact mới |
| Still Gemini SH02 | Chỉ sau REJECT mix `72c6eb46`, sau D-B, D-C, D-D, và sau khi task Gemini SH03 không còn là lý do chặn nếu still dùng cùng credential | `UNKNOWN` USD | Một |
| `google` / `veo-3.1` video-only 720p | Cùng cổng. Không dùng hàng video+audio 0.40 USD | 0.20 USD, catalog 2026-09-22, duration không nhân | Một |
| `elevenlabs` / `eleven_v3` | Cùng cổng. Câu đúng `Dạ… nay con làm bài không tốt.` | `UNKNOWN` USD | Một |
| `fal` / `sync-lipsync-v3` | Sau khi voice đo được nằm trong khoảng người gõ, lệch không quá 0.75 giây | Ước 0.67 USD vì sàn 5 giây cho câu khoảng 2.5 giây. Không phải charge | Một |
| Mix FFmpeg | Sau lip-sync mới | 0 USD provider | Một trong cap này |
| SH01 video hoặc lip-sync mới | Permit khác, sau khi người xem `81b29b5c` | Runway catalog 5 credit/giây, 25 hoặc 50 credit. Không quy USD | Không nằm trong cap SH02 |
| SH03–SH06 | Permit từng shot sau ACCEPT shot trước và sau câu SH03 đủ | Chưa được cộng thành ngân sách | Không |
| Assembly | Sau D-E | 0 USD provider, một lần | Không nằm trong cap SH02 |

`PROPOSED` Cộng dồn một vòng SH02 đúng catalog đã biết là 0.20 + 0.67 USD, cộng voice `UNKNOWN`, cộng still `UNKNOWN`. Không nhân ba lần video. Không nhân sáu shot. Ba video và hai lip-sync (1.94 USD catalog) và sáu shot (7.62 USD catalog) trong pilot plan là ví dụ số học, không phải quyền chi của kế hoạch này.

`PROVEN` Dù cap được hiểu là còn hiệu lực, composition root hiện tại không gọi được Veo, ElevenLabs, Fal, hay Gemini, và Runway đang DryRun. Ước lượng không phải lệnh gọi.

---

## 10. Quyết định Architect phải phê duyệt trước khi thực thi

Kế hoạch này xin duyệt để giữ làm khóa. Nó không xin chi tiền và không xin sửa source.

| Id | Quyết định cần chữ | Nếu chưa có chữ |
|---|---|---|
| D-0 | Khóa kế hoạch này: hoàn thành EP01 trên `30ba4061` / `FAMIXA_PRODUCTION`; official `4ef164d6` đứng yên | Không thực thi nhóm nào |
| D-A | Build của cap SH02 là `30ba4061` hiện có, thay cho “build tenant mới” trong click-gate | Không tạo build mới, cũng không gắn cap vào build này |
| D-B | Có được đăng ký credential Fal, ElevenLabs, Google, Gemini cho đúng tenant `FAMIXA_PRODUCTION` trên database local hay không. Không phải `KIT_MKT`. Không đưa secret vào kế hoạch | Không stage có phí |
| D-C | Có được đổi `DryRunRunwayTransport` và có được đăng ký adapter Veo, ElevenLabs, Fal trên composition root hay không, trong một diff riêng | Không stage có phí. DryRun không được coi là đã gọi Runway |
| D-D | Ai là người xem, và cap SH02 một lần mỗi stage có còn hiệu lực trên build D-A sau khi D-B và D-C xong hay không | Không ACCEPT có hiệu lực, không gọi provider |
| D-E | Thư ADR-08: Famixa được gọi assembly tập cho một EP01 sáu shot đã ACCEPT, hành động vẫn ngoài Core, không ghi `FINAL` | Không `ASSEMBLE_EPISODE` |
| D-F | Câu SH03 đủ chữ, do một người viết. Kế hoạch này không đề xuất chữ | Không voice SH03, không coi shot đó xong |
| D-G | Task Gemini SH03 `RUNNING` được đóng thế nào | Không still SH03 mới |
| D-H | D4 có mở hay không: hàng review trong `pack_content` qua migration content, không apply Production, không kiểu Core | Giữ ghi chú id + hash ngoài OS. Không bịa hàng review |
| D-I | Sau khi người REJECT `81b29b5c`, một permit SH01 riêng có được mở hay không | Không video SH01 mới. Lip-sync SH01 vẫn chờ voice và cửa sổ dù take được giữ |

`PROPOSED` Phần được làm ngay sau D-0, không cần D-B đến D-I: nhóm A, bước đọc B0, và một người xem hai mix hiện tại. Xem không tạo media. REJECT ở bước xem không tự gọi provider.

Dừng tại đây. Chờ Architect.

---

## 11. Evidence closure — 2026-10-17, read-only

Local `kitplatform`, `BEGIN READ ONLY`. No provider call, no binding write, no session. Playback in a browser was not observed: no authorized session was open.

| Fact | Grade |
|---|---|
| SH02 video `eb1072d0` SHA-256 `21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34` equals official Veo `b44e530b` | PROVEN. It is a byte copy, not a pilot source |
| SH02 voice `e00dbf1a` and SH01 voice `1f60574f` equal official voices `0fde86c2` and `bf8d0827`. Durations 2.534 s and 4.232 s | PROVEN as bytes. Word match to the line is UNKNOWN |
| SH01 import video `c4948374` and keyframe `5f9f78c2` equal official Runway `b22370a9` and keyframe `1f4c26e0` | PROVEN |
| SH01 current lip-sync `7c20d436` is fixture. Its parents are current Runway `81b29b5c` and current voice `1f60574f`. The current mix `33f22b13` still parents the imported video and the older Fal file | PROVEN. Mix is a stale generation. Fixture is not a deliverable |
| SH02 lip-sync `a40edfef` is Fal, 2.500 s, parents video `eb1072d0` and voice `e00dbf1a`. Mix `72c6eb46` parents that video and that lip-sync, duration 8 s | PROVEN lineage. Visual accept UNKNOWN |
| `performance_snapshot` has zero rows for `30ba4061`. Succeeded SH01/SH02 snapshots have no `speechStartSec` | PROVEN. No director speech interval is stored |
| SH03–SH06: only SH03 `CREATE_IMAGE` Gemini `RUNNING` since 2026-09-26 22:24. No artifact | PROVEN |
| Credentials: one ACTIVE `FAMIXA_PRODUCTION` / `runway` / `gen4_turbo`, two REVOKED. AI config: that Runway row enabled. No Fal, ElevenLabs, Google, or Gemini version | PROVEN |
| Composition root: `IRunwayTransport` is `DryRunRunwayTransport`. Single `IMotionExecutionAdapter` injection is Fixture after Runway. `ILipSyncExecutionAdapter` and `IVoiceExecutionAdapter` are not registered, so the service defaults are `FixtureLipSyncAdapter` and `FixtureVoiceAdapter`. FFmpeg mix and episode assembly concrete types are registered | PROVEN |
| ADR-08 caller letter | UNKNOWN. Still open. No assembly was run |
| Live Preview of a signed-in `FAMIXA_PRODUCTION` session | UNKNOWN. No session was available |

---

## 12. P1 production path — 2026-10-17 execution result

HEAD stayed `8c59ceb4f3be537898106ec058ea547406f722e5` on `main`. The working tree was already dirty. This batch did not reset, clean, stash, or checkout. No database write, no credential write, no migration, no deploy, no provider call, no assembly.

`PASS` for the authorized local production path. P2 and the paid SH02 pilot were not started.

| Change | Result |
|---|---|
| Composition root | `DryRunRunwayTransport` remains `IRunwayTransport`. `VeoMotionExecutionAdapter` is registered before `FixtureMotionAdapter`. Concrete ElevenLabs, Fal, and tenant Gemini adapters are registered. `IVoiceExecutionAdapter` and `ILipSyncExecutionAdapter` stay unregistered. `ProductionVeoHttpAuthenticator` is the Veo authenticator and stays closed. `OfficialVeoProductionAuthenticatorV1.ProductionDiRegistered` and `ProductionVeoReadinessComposition.ProductionResolverIncludesVeo` stay false |
| Tenant `CREATE_VOICE` | Tenant EP01 with `AuthorizePaidNetwork` uses the concrete ElevenLabs adapter, requires `elevenlabs` / `eleven_v3` and an issued permit, and consumes that permit once. A second fingerprint is `PaidNetworkForbidden`. The HTTP transport requires a consumed execution permit for tenant EP01 before any send. Tests used `FakeElevenLabsVoiceTransport` |
| `CURRENT:MIX` | A tenant EP01 mix with a different contract hash replaces `CURRENT:MIX`. The previous artifact bytes stay. `ACCEPTED` and `ACCEPTED:MIX` stay on the previous artifact |
| Official video SHA | Tenant SH02 lip-sync and mix reject ByteSha256 `21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34` before a physical read, including when the artifact id is not `b44e530b` |
| Fixture accept | On tenant EP01, `AcceptOs` of a `fixture` provider artifact throws `FailedIdentity` and writes no `ACCEPTED` binding |
| Official build | `GovernsAsync` is false for `4ef164d6`. Official voice and lip-sync fail before credential resolve |

Executed: `dotnet test tests/KitPlatform.Content.Tests/KitPlatform.Content.Tests.csproj` filter `ProductionPipelineV1Tests|ProductionOfficialSh02|ProductionCreateVoiceCutover|ProductionOfficialVeo|P35CredentialRotation|FalTenantEp01`. Build succeeded. 39 passed, 15 failed, 0 skipped.

P1 path passed inside that run: `Tenant_ep01_production_path_v1`, `Tenant_ep01_lipsync_v1`, `Tenant_ep01_mix_v1`, `Tenant_ep01_assemble_v1`, official SH02 offline voice/lip-sync/mix/assemble, official Veo authenticator and HTTP composition, credential rotation.

The 15 failures are outside this batch. Eleven are `INPUT_SNAPSHOT_MISMATCH` on ActingBeat (`He stays in place.` against a longer frozen continuity line) in create-video cutover checkpoints. Four live official SH02 mix/assemble probes throw `PRODUCTION_TENANT_REQUIRED` from `ProductionMissingCallerTenant` before a repository read. They were not given a tenant and were not retried.

Still required before a paid call: a human REJECT of SH02 mix `72c6eb46`, a named reviewer, both speech endpoints on `30ba4061`, the rest of the SH03 line, the four provider secrets outside the tree, a real off-machine backup attestation, and a separate approval. Setting `ProductionCredentials:OffMachineBackupConfirmed` locally is not that attestation.

## 13. P2 local readiness — 2026-10-18 execution result

HEAD stayed `8c59ceb4f3be537898106ec058ea547406f722e5` on `main`. The working tree was already dirty, including the untracked production sources this batch edited. This batch did not reset, clean, stash, checkout, commit, or push. No provider call, no credential registration, no Production DB write, no migration, no deploy, no SH01 paid take, no episode assembly, and no change to official build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`.

Decision report: `docs/novixa/03-solution/FAMIXA_EP01_P2_PILOT_SH02_GO_NO_GO_V1.md`. Conclusion: `NO-GO`.

| Local change | Result |
|---|---|
| Tenant EP01 paid video | Before a network start, an ungoverned tenant or a missing credential throws `TENANT_AI_CONFIGURATION_REQUIRED` or `TENANT_AI_CREDENTIAL_REQUIRED`. A successful replay does not need a live credential |
| Same fingerprint after success | Image replay still returns the existing artifact after the permit is consumed |
| Provider failure | Tenant paid image, voice, and lip-sync record the attempt as failed, then rethrow |
| Second attempt | Tenant EP01 paid Gemini, Google, ElevenLabs, and Fal refuse another start of the same fingerprint, including an explicit video `RetryAttempt`. Official sequential retry stays available |
| Voice to lip-sync | `LineIdFromSnapshot` reads the CREATE_VOICE freeze token when JSON has no `lineId` |
| Preview | The local chain binds a valid mix and the preview artifact role is `CURRENT:MIX`. The authoritative video take stays the video artifact |

`Runway` remains `DryRunRunwayTransport`. The regression uses fake transports. A green test is not a live provider proof. The speech window inside the regression is a fixture that covers the whole take so local FFmpeg trim is not required. It is not a director interval.

Executed: the same filter as the prior P1.1 run, `ProductionPipelineV1Tests|ProductionOfficialSh02|ProductionCreateVoiceCutover|ProductionOfficialVeo|P35CredentialRotation|FalTenantEp01`. Build succeeded. 55 passed, 0 failed, 0 skipped. The extra fact is `Pilot_sh02_local_readiness_v1`.

Read-only local `kitplatform` on 2026-10-18, `BEGIN READ ONLY`:

| Fact | Grade |
|---|---|
| Tenant `4028941b` is `FAMIXA_PRODUCTION`. Build `30ba4061` belongs to that tenant. Official `4ef164d6` still has a null tenant. `series_pilot.active_build_id` is still the official build | PROVEN |
| Credential versions: one ACTIVE `runway` / `gen4_turbo`, two REVOKED. Tenant AI config: one enabled VIDEO row, `runway` / `gen4_turbo`, with a credential. No Gemini, Google, ElevenLabs, or Fal row | PROVEN |
| SH02 `CURRENT:MIX`, `VISIBLE:MIX`, `ACCEPTED`, and `ACCEPTED:MIX` are `72c6eb46`, SHA-256 `78216013541205afea5a131d92d53e166ba9171beddc2111b05b74ffea73538a`, 1,422,990 bytes, 8 s, `VERIFIED`. No `shot_human_review` table | PROVEN as OS bindings. Human REJECT is absent |
| SH02 video `eb1072d0` and official `b44e530b` still share SHA-256 `21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34`, 1,437,239 bytes, 8 s | PROVEN. The tenant file remains a byte copy |
| SH02 voice `e00dbf1a` SHA-256 `bd348ffb84c5588335be647fc82dd37139b795d244dfdde0ea9e551ee3f6f298`, 2.534 s | PROVEN as stored bytes. Word match remains UNKNOWN |
| `performance_snapshot` for `30ba4061` is zero rows, including rows whose source mentions `speechStartSec` | PROVEN. No human speech interval is stored |
| Gemini task `e280783f` is still `CREATE_IMAGE` / `RUNNING`, model `gemini-2.5-flash-image`, created 2026-09-26 22:24 +07, one attempt, no provider job id, zero artifacts | PROVEN. D-G is not approved, so the task was not closed |
| `ProductionCredentials:OffMachineBackupConfirmed` is absent from local API appsettings | PROVEN as a local config fact. An off-machine backup attestation is absent |

Paid SH02 still requires a human REJECT of mix `72c6eb46` on that id and SHA, a named reviewer, both speech endpoints, registered Gemini, Veo, ElevenLabs, and Fal credentials, a real backup attestation, and a separate spend approval. A new still and a new Veo take are required because the current video SHA matches the official file.

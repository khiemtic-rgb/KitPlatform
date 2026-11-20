# M1 — Dựng tập từ bảng câu (Famixa · Lệch Tông)

Kịch bản CSV + giọng + ảnh tách lớp → video nháp 16:9 / 9:16, phụ đề Việt và Anh.
Dựng bằng FFmpeg trên máy chủ. Không gọi AI trả phí, không dùng `work_job`, không chạm bảng `production_*` / `video_*`.

Trang: **Content → Video Studio → nút "Dựng từ bảng câu"** (`/content/studio/linesheet`).

## Chạy thử trên máy dev (Windows)

```powershell
cd E:\KitPlatform

# 1. Migration mới (chỉ tạo 6 bảng pack_content.linesheet_*)
psql "<chuỗi kết nối dev>" -v ON_ERROR_STOP=1 -f migrations\430_pack_content_famixa_linesheet.sql

# 2. Build và test
dotnet build
dotnet test tests\KitPlatform.Content.Tests --filter "FullyQualifiedName~Linesheet"

# 3. Frontend
cd client\admin
npm run build
```

Cần `ffmpeg` và `ffprobe` trên PATH (hoặc đặt đường dẫn trong cấu hình bên dưới).

Kiểm thử gồm:
- `FamixaLinesheetV1Regression` (lõi: CSV, dòng thời gian, phụ đề, chia shot, đối số FFmpeg, cache, lọc đường dẫn zip).
- `FamixaLinesheetEp01Golden`: so với bản mẫu Python trên EP01. Kết quả phải ra 162 hàng, 64 shot, phụ đề giống từng byte và 11.665 khung ở 24 fps.
- `FamixaLinesheetSkiaComposerTests`: ghép lớp và thẻ chữ tiếng Việt bằng SkiaSharp thật, chặn tenant khác và đường dẫn `..`.

## Cấu hình (tùy chọn, `appsettings`)

```json
"Content": {
  "FamixaLinesheet": {
    "WorkerEnabled": true,
    "WorkerPollSeconds": 5,
    "Root": "App_Data/famixa-linesheet",
    "FfmpegPath": "ffmpeg",
    "FfprobePath": "ffprobe"
  }
}
```

Nếu để trống `WorkerEnabled`, giá trị lấy theo `Content:WorkerEnabled`.

## Các bước trên trang

1. **Tập mới**, ví dụ `LECH_TONG` · `EP01`.
2. **Nhập CSV** kịch bản (`ep01_script.csv`). Lỗi được báo theo hàng; chỉ cần một hàng sai thì không hàng nào được nhập.
3. **Nhập settings.json** (`ep01_settings.json`): bối cảnh, dàn nhân vật, `chat_self`.
4. **Tải zip**. Bố cục trong zip:
   - `audio/<id câu>.mp3`
   - `music/<cue>.wav`
   - `sfx/<cue>.wav`
   - `settings/<bối cảnh>/bg.png`, `settings/<bối cảnh>/mid.png`
   - `characters/<ID>.png`

   Được phép có một thư mục bọc ngoài. Giọng được cắt khoảng lặng thừa ngay khi nhận.
5. **Tính dòng thời gian**, rồi **Tạo bảng nháp** shot. Có thể tải CSV về, sửa trong Excel rồi nhập lại.
6. **Dựng**: chọn khổ, cỡ hình, khoảng thời gian, ngôn ngữ chữ màn hình, phụ đề rời hoặc đốt vào hình.
   Mỗi shot được cache, nên đổi một ảnh chỉ dựng lại các shot dùng ảnh đó.

Ảnh trong gói thử là **ảnh giữ chỗ** (khối màu), dùng để kiểm nhịp, khung và phụ đề. Thay bằng ảnh thật khi có.

## An toàn

- Tenant lấy từ claim (`IProductionCallerTenant`). Mọi bảng có `tenant_id NOT NULL` và khóa ngoại kép `(id, tenant_id)`.
- Mọi câu SQL đều có `tenant_id = @Tenant`. Worker nhận lần dựng rồi dùng tenant của chính bản ghi đó.
- File nằm ở `{Root}/{tenantN}/{productionN}/…`. Khóa của tenant khác hoặc có `..` bị từ chối.
- FFmpeg chạy bằng ArgumentList (không qua shell), có giới hạn thời gian và bị giết khi quá hạn. Chữ thoại không bao giờ nằm trên dòng lệnh.
- Worker riêng, mỗi vòng làm một bước (một shot). Lần dựng đang làm dở được nhận lại sau 10 phút không có heartbeat. Mỗi bước thử tối đa 3 lần.
- Sửa file chung chỉ thêm dòng:
  - `ContentPackDependencyInjection.cs`: 1 dòng
  - csproj Infrastructure (font): 1 dòng
  - csproj Tests (fixtures): 1 dòng
  - `router.tsx`: 2 chỗ
  - `ContentStudioPage.tsx` (nút): 1 dòng
  - manifest migration: 1 dòng

## Giọng ElevenLabs (bước 4 trên trang, thêm ngày 2026-11-04)

- **Khóa:** dùng khóa ElevenLabs đã lưu trong cài đặt AI và Video của Content (cùng chỗ luồng Video Studio cũ dùng). M1 không lưu khóa riêng.
- **Giọng:** mục `voices` trong `settings.json`, khóa theo cột `speaker` (kể cả `NARRATOR`):
  `{"voice_id": "...", "stability": 0.5, "similarity_boost": 0.75, "style": 0, "speed": 1.0}`.
  Eleven v3 chỉ nhận stability 0, 0.5 hoặc 1.
- **Thẻ cảm xúc:** tự suy từ cột `direction`, ví dụ "Thì thầm" thành `[whispers]`, "Gắt" thành `[angry]`, khớp theo nguyên từ. Ghi đè từng câu bằng cột `tts_tags` trong CSV, hoặc đổi cả bộ bằng `tts_tag_rules` trong `settings.json`.
- **Dấu chấm than** được đổi thành dấu chấm, vì v3 hay đổi cảm xúc giữa câu khi gặp dấu chấm than.
- **Bấm "Xem trước"** để thấy từng câu sẽ gửi và tổng số ký tự; bước này không tốn tiền.
- **Bấm "Tạo giọng"** để chỉ gửi các câu chưa có giọng đúng. Mỗi câu có một dấu vân tay gồm chữ, giọng và thông số; đổi một câu thì chỉ câu đó phải tạo lại. Máy chủ từ chối nếu số ký tự xác nhận khác số ký tự thật sẽ gửi.
- **Bấm "Tạo lại"** ở từng dòng để tạo lại riêng một câu.
- Tạo giọng xong phải bấm **Tính dòng thời gian** lại.
- **Bấm "Nghe"** để nghe giọng đã lưu của một câu.
- **Bấm "Bản mới"** khi một câu bị lơ lớ vài chữ hoặc sai cảm xúc. Máy tạo một bản đọc khác (seed khác) và phát luôn để nghe. Cột "Bản" cho biết đang dùng bản thứ mấy. Bản đang dùng được lưu trong `tts/takes.json`, nên các lần dựng sau vẫn giữ đúng bản đó. Bấm nhiều lần vẫn không ổn thì đổi chữ đó sang từ khác trong CSV.
- **Lỗi 409 `already_running` và 429** (thêm ngày 2026-11-04): lần đầu dùng một giọng thư viện, ElevenLabs có thể từ chối vài câu gọi cùng lúc. Máy tự chờ 3, 6 rồi 12 giây và gửi lại; lỗi này không tính ký tự.

## Cân âm lượng giữa các giọng (thêm ngày 2026-11-04)

Mỗi giọng ElevenLabs ra một độ to khác nhau. Ở EP01, giọng Mẹ nghe to hơn hẳn các nhân vật khác. Lúc ghép tiếng, máy đo độ to từng câu rồi cân về một mức chung.

- `loudness_lufs` trong `settings.json`: mức chung, mặc định `-20`. Đặt `0` để tắt.
- `gain_db` trong từng giọng ở mục `voices`: chỉnh thêm riêng giọng đó, ví dụ `-2` là nhỏ đi 2 dB.
- Mỗi câu chỉ được kéo lên hoặc hạ xuống tối đa 12 dB, nên câu thì thầm vẫn nhỏ hơn câu nói thường.
- Đổi hai giá trị này thì chỉ cần dựng lại: máy tự ghép lại tiếng, không phải tạo lại giọng.
- Tải nhạc hoặc tiếng động mới (zip) thì cũng chỉ cần dựng lại: máy tự nhận ra bộ nhạc, tiếng động đã đổi và ghép lại tiếng (sửa ngày 2026-11-04; trước đó phải bấm Tính dòng thời gian lại).

## Tiếng động (thêm ngày 2026-11-04)

- Nguồn: Pixabay (không cần ghi tên tác giả). File gốc người dùng tải để ở `_famixa_m1_test/am_thanh/sfx/`; nhiều cue có 2 bản `-1`, `-2` để chọn.
- Trước khi tải lên, Claude xử lý mỗi file: cắt khoảng lặng đầu, giữ tối đa 20 giây, cân độ to (tiếng nền -27 LUFS, tiếng việc -21, khán giả -20, đếm tiền -24, bụng sôi -22), đổi sang WAV 48 kHz mono. Bản đã xử lý: `_famixa_m1_test/am_thanh/sfx_ep01_da_xu_ly.zip`.

## Ảnh cảnh, chuyển động và ghép môi (thêm ngày 2026-11-04)

**Ảnh cảnh (keyframe).** Ảnh vẽ trọn cảnh, có đủ người ngồi đúng chỗ, tải lên trong zip ở `keyframes/<tên>.png`. Trong `settings.json`, mục `keyframes` chọn ảnh theo mã shot hoặc tên cảnh, ví dụ `{"S03 Bữa sáng": "kitchen_day_an_sang"}`. Shot có ảnh cảnh thì không ghép lớp nhân vật nữa.

Bài học: ghép lớp nhân vật cắt rời làm Runway cho nhân vật đứng dậy, đi xuyên bàn, xuyên tường, hình nhòe. Dùng ảnh cảnh thì hết.

**Chuyển động (Runway gen4_turbo).**
- Mặc định mỗi đoạn dài 5 giây (`motion_clip_seconds`), giá 25 credit. Shot dài hơn đoạn thì chạy chậm lại, dài nữa thì chạy xuôi rồi ngược.
- Câu lệnh nên ngắn và êm. Câu lệnh dài hoặc mạnh hay bị lỗi `INTERNAL.BAD_OUTPUT.CODE01`.
- Mọi câu lệnh được thêm quy tắc giữ chỗ và **ngậm miệng**. Runway không nghe được tiếng nên môi động lung tung; môi do bước ghép môi làm theo đúng lời.

**Ghép môi (fal.ai sync).**
- Mục `faces` trong `settings.json`: tên ảnh cảnh → mã nhân vật → `[x, y, rộng, cao]`, tính theo điểm ảnh của ảnh cảnh.
- Với mỗi câu của nhân vật có khuôn mặt (không tính người kể chuyện), máy làm theo thứ tự:
  1. Cắt vùng mặt từ đoạn chuyển động, đúng khung của câu.
  2. Gửi vùng mặt cùng tiếng câu đó sang fal.
  3. Đặt khuôn mặt đã khớp môi về đúng chỗ, mép mờ dần.
- Mỗi lần chỉ gửi một khuôn mặt nên fal không chọn nhầm người.
- `lipsync_model` có ba mức: `lipsync-2` (khoảng 0,05 USD/giây), `lipsync-2-pro` (khoảng 0,083) và `sync-3` (khoảng 0,133, bắt mặt nghiêng tốt hơn).
- API: `GET productions/{id}/lipsync` để xem trước giá; `POST` với `confirmUsd` đúng số tiền đã xem trước. Đoạn đã ghép lưu ở `lipsync/manifest.json`. Đổi giọng, đổi đoạn chuyển động hoặc đổi khuôn mặt thì câu đó phải ghép lại.
- Khóa fal: lấy trong kho khóa sản xuất (cùng chỗ nhánh Famixa cũ dùng), không có thì dùng khóa fal trong cài đặt Video.
- Bài học thử lần đầu (lipsync-2): câu rất ngắn ("Ăn đi.") gần như không thấy môi động; mặt quay nghiêng lúc được lúc không.

## Tin nhắn và cuộc gọi trên điện thoại (thêm ngày 2026-11-05)

**Dòng nào thành điện thoại.**
- `screen_vi` có dạng `Tên: nội dung` (dấu hai chấm có dấu cách, trong 25 ký tự đầu) là tin nhắn. Nhiều tin nối bằng ` / `.
- Chủ máy ghi ở đầu: `@Bố| …`. Không ghi thì lấy người đầu tiên trong `chat_self`.
- Kiểu màn hình theo "người gửi" đầu tiên:
  - `Đang gọi: Quân` → màn hình gọi.
  - `Cuộc gọi nhỡ: Lan · 3 cuộc` hoặc `Tin nhắn thoại: Bố · 19:42` → màn hình khóa có thông báo.
  - `Đang ghi âm: Quân · 19:42` → màn hình ghi âm.
  - Còn lại → khung trò chuyện.
- Không viết số trong ngoặc như "(3)", vì bước làm sạch chữ sẽ bỏ phần đó.

**Hai cách hiện.**
1. Kín khung: điện thoại phẳng ở giữa, nền cảnh phía sau làm mờ.
2. Tay cầm máy: khai `phone_inserts` trong `settings.json` theo dạng mã câu → tên ảnh cảnh có tay cầm điện thoại **màn hình trắng** (ví dụ `"E01_005a": "phone_bo_car"`).
   - Máy tự tìm màn hình (vùng trắng lớn nhất), phóng to quanh điện thoại (tối đa 2,2 lần) rồi bẻ chữ theo góc nghiêng của máy.
   - Không tìm được màn hình thì quay về cách kín khung.
   - Vẽ ảnh tay cầm: màn hình trắng sáng đều, không chữ, không biểu tượng; máy chiếm khoảng 10–30% khung hình.

**Bài học.**
- Chữ cỡ 22 quá nhỏ ở 720p. Bản kín khung dùng cỡ 31 và 42; bản tay cầm phóng to và vẽ chữ to hơn một bậc (320 đơn vị thay vì 376).
- Máy cầm nghiêng thì khung tìm được không ôm hết màn hình, lộ viền trắng. Máy tô viền trắng theo màu nền màn hình hai lần, trước và sau khi vẽ chữ.
- Đổi cách vẽ điện thoại thì tăng `PhoneUiVersion` trong `LinesheetRenderPlan.cs`, nếu không các cảnh đã dựng sẽ dùng lại bản cũ.
- Dựng một đoạn bắt đầu giữa cảnh: thời điểm hiện chữ được chặn không nhỏ hơn 0 (trước đây lỗi `fade st -3.156`).

## Thử chuyển động và ghép môi trên hình 3D (2026-11-05)

Thử trên ba shot: Bố cận mặt trong xe (SH040), Bố Mẹ trong bếp (SH051) và cả nhà ở phòng khách (SH053).

**Chuyển động.**
- **gen4_turbo, shot bếp:** đạt. Mặt giữ đúng, máy đứng yên, hai người chỉ quay đầu nhẹ.
- **gen4_turbo, shot xe:** hỏng. Hai người lạ hiện ra trong xe, Bố khua tay như đang nói.
- **gen4.5, shot xe**, câu lệnh ghi rõ "chỉ một mình, không ai vào khung": không có người lạ, nhưng máy tự lùi ra, ánh sáng đổi và mặt Bố trôi khỏi chỗ cũ.
- **Nguyên nhân chính:** ảnh nhìn qua kính ướt mưa, mô hình không hiểu đâu là lớp kính.
- **gen4_turbo, shot phòng khách:** Runway báo lỗi nội bộ (`INTERNAL.BAD_OUTPUT`), không ra clip.
- **Bài học:**
  - Ảnh dùng cho chuyển động không nên có lớp kính hoặc phản chiếu phủ trước mặt.
  - Câu lệnh mặc định không được gọi tên kiểu vẽ. Câu "2D flat webtoon" cũ đã bỏ khỏi `DefaultPrompt`.
  - Câu lệnh được lưu trong `motion/manifest.json`, shot nào đã tạo thì lần sau dùng lại câu cũ. Muốn đổi thì gửi `prompts`.

**Ghép môi (lipsync-2, 4 câu trong bếp, 0,40 USD).**
- Hai nhân vật đều nhìn **nghiêng hẳn**. Môi Bố hé mở nhẹ theo lời. Môi Mẹ gần như không động.
- **Bài học:** ghép môi chỉ ăn khi mặt nhìn thẳng hoặc nghiêng ba phần tư. Shot có thoại phải vẽ mặt người nói quay về phía máy. Shot hai người nhìn nhau nghiêng hẳn thì thêm shot cận qua vai cho từng câu.

**Cách đo vùng mặt (`faces`).** Lấy khung đầu của clip chuyển động (1280×720), đo hộp mặt, nhân 1,5 cho ảnh 1920×1080.

**Thử lần hai, góc qua vai trong bếp: đạt.**
- Ảnh `kitchen_ots_me` và `kitchen_ots_bo`: người nói ngồi đối diện máy, nghiêng ba phần tư; người nghe chỉ thấy gáy và vai mờ ở tiền cảnh.
- Chuyển động bằng gen4_turbo, câu lệnh ghi rõ "chỉ hai người này".
- Ghép môi bằng lipsync-2: môi mở, đóng rõ theo lời; vùng mặt ghép vào không thấy đường nối. sync-3 cho kết quả gần như nhau mà đắt gấp khoảng 2,7 lần, nên **dùng lipsync-2 làm mặc định**.
- Cách gắn: tách shot theo câu (SH051 là E01_122, SH051b là E01_123; SH051c là E01_124–125 vẫn dùng ảnh rộng), đặt `faces` cho từng ảnh.
- **Quy tắc vẽ cho mọi tập:**
  - Câu thoại có người nói trong hình thì dùng ảnh mặt người nói nhìn ba phần tư về phía máy.
  - Shot rộng chỉ dùng cho khoảng lặng, lời kể và tiếng động.
  - Chuyển động mặc định dùng gen4_turbo cho cảnh trong nhà.
  - Không dùng ảnh có kính hoặc phản chiếu che mặt.

## Cảnh 9 và cảnh 10 làm theo quy tắc mới (2026-11-05)

**Shot và ảnh.**
- Mỗi câu thoại là một shot riêng; bảng shot sửa tay bằng `PUT shots`.
- Bếp dùng hai ảnh qua vai: `kitchen_ots_me`, `kitchen_ots_bo`.
- Phòng khách dùng bốn ảnh cận: `living_cu_quan`, `living_cu_bo`, `living_cu_me_stand` (lúc Quân về), `living_cu_me_sit` (lúc thì thầm).
- Shot rộng chỉ để cho tiếng động, lời kể và khoảng lặng.
- **Nếu dựng lại shot tự động thì mất hết phần tách tay này.** Bảng shot cũ được giữ trong lịch sử.

**Chuyển động.**
- Câu lệnh chuẩn phải ghi rõ:
  - "Only the people already in the image; no other person appears at any time and nobody enters the frame."
  - "Hands stay where they are the whole time and never rise to the face; the mouth area stays fully visible."
- **Bài học:**
  - Không ghi câu về tay thì Bố đưa cốc lên uống hoặc lấy tay che mặt, che mất miệng.
  - Ảnh Quân đứng ở cửa có khoảng trống phía trước, nên turbo hay cho người lạ ló đầu vào. Phải ghi thêm câu "the edges of the frame stay empty, no heads appear in the foreground".
- Đổi cột `expressions` của shot sẽ làm khóa chuyển động đổi theo, và clip cũ không được dùng nữa. Muốn giữ clip thì giữ nguyên cột này.

**Thời gian chờ.**
- Vite cắt yêu cầu sau khoảng 3 phút (báo lỗi 500), và máy chủ hủy luôn việc đang làm.
- Vì vậy mỗi lần gửi chỉ nên có: tối đa 4 shot chuyển động, 3 ảnh hoặc 2 câu ghép môi.

## Dùng chung clip, cảnh 3, cảnh 6b và cảnh 8 (2026-11-05)

**Dùng chung clip (`motion_share` trong `settings.json`).**
- Khai mã shot → mã shot nguồn. Shot khai ở đây không tạo clip riêng mà dùng clip của shot nguồn; ghép môi vẫn làm riêng từng câu.
- Clip nguồn phải cùng ảnh cảnh, cùng câu lệnh và cùng cột `expressions`.
- Cảnh 3 có 14 câu chỉ cần 3 clip: Quân, Bố và Mẹ, mỗi người một clip.
- **Bài học:**
  - Shot còn clip cũ từ bản 2D thì clip cũ che mất clip nguồn. Đã sửa: shot có khai dùng chung thì lấy clip nguồn trước.
  - Câu có cảm xúc khác hẳn (Bố gắt "Bốn rưỡi mà còn đùa à?") phải có clip riêng, vì dùng chung clip mỉm cười thì mặt sai với lời.

**Gọi API trực tiếp.**
- Vite (localhost:5173) cắt các yêu cầu dài, nên mở thẳng trang `http://127.0.0.1:5290/api/...` trong trình duyệt và gọi `fetch` cùng gốc.
- Không còn giới hạn 3 phút, có thể gửi 4 ảnh hoặc 13 câu ghép môi trong một lần.

**Turbo hay thêm người lạ khi ảnh có khoảng trống bên cạnh** (như ghế trống ở bàn ăn).
- Ghi trong câu lệnh "the edges of the frame stay exactly as in the image" là chưa đủ.
- Thử turbo hai lần không được thì chuyển riêng shot đó sang gen4.5 (60 credit). gen4.5 giữ khung tốt hơn khi trong nhà.

**Ảnh mới.**
- Cảnh 3: `kday_wide`, `kday_cu_quan`, `kday_cu_bo`, `kday_cu_me`. Bố mặc sơ mi trắng dài tay cài kín cổ, khớp câu Mẹ hỏi "Anh mặc sơ mi trắng à?". Ảnh bữa sáng cũ có Bố mặc áo polo.
- Cảnh 8: `car_bo_inside`, chụp từ ghế phụ, không có kính chắn mặt, dùng cho "Sai nốt rồi con ạ…".
- Ảnh phòng làm mẫu: `KDAY_ref`.

## Các cảnh có Mai, Tùng, Khách, Đồng nghiệp (2026-11-05)

**Ảnh mẫu nhân vật.** `refs/{MAI,TUNG,KHACH,DONG_NGHIEP}_{ngoi,mat,goc}.png`, khai trong `character_refs` và `character_notes`.

**Cách làm từng phòng.**
1. Vẽ ảnh rộng trước: `band_wide`, `backstage_wide`, `office_wide`, `stage_band`.
2. Dựng thử một khung của ảnh rộng, lưu làm ảnh phòng mẫu: `BAND_ref`, `BACK_ref`, `OFFICE_ref`.
3. Vẽ ảnh cận từng người theo phòng mẫu: `band_cu_*`, `backstage_cu_*`, `office_cu_*`, `tailor_cu_*`, `preview_*`, `hospital_mai`.

**Chuyển động.** Mỗi ảnh cận một clip, các câu khác dùng chung qua `motion_share`. Clip của Đồng nghiệp bị turbo thêm đồng nghiệp lạ vào văn phòng phía sau, phải làm lại bằng gen4.5.

**Bài học.**
- **Không gọi hai lệnh tạo ảnh cùng lúc.** Cả hai cùng ghi `keyframes/generated.json` nên mất dấu ảnh đã tạo. Ảnh vẫn có, nhưng bị báo "chưa có".
- **Dùng chung clip phải cùng cột `focus`, không chỉ cùng ảnh và câu lệnh.** Khác `focus` thì khóa khác và shot không có clip (SH068b phải sửa `focus` thành QUAN).
- **Token đăng nhập sống khoảng 1 giờ.** Hết hạn thì mở lại trang 5173 cho trang tự làm mới, lấy token mới rồi mới gọi API trực tiếp.

## Sửa sau buổi soát EP01 bản 9 (2026-11-05)

Biên bản soát nằm ở `docs/famixa/QA_EP01_BAN9.md`. Những quy tắc dưới đây đã nằm trong phần mềm, tập sau tự có.

**Thời gian đọc chữ.**
- Hàm `LinesheetRenderPlan.ReadingMs` tính thời gian đọc: 1 giây cộng 0,3 giây mỗi từ.
  - Tin nhắn: ít nhất 2,5 giây.
  - Thẻ chữ khác: ít nhất 3 giây.
  - Tối đa 8 giây.
  - Tên phim (`cue = title_sting`): 5 giây.
- Khi tính dòng thời gian, nếu shot kết thúc trước khi chữ đọc xong thì phần mềm giữ thêm ở cuối shot, sau hàng cuối của shot (`LinesheetTimelineBuilder.ReadingHolds`). Lời thoại không bị chen ngang.
- Bước ghép tiếng lấy khoảng lặng sau mỗi hàng từ dòng thời gian, không lấy `pause_after_ms`.
- Sửa bảng shot thì tính lại dòng thời gian.
- Với EP01, tập dài thêm 27 giây (519,6 lên 546,7 giây), phần lớn ở các cảnh tin nhắn.
- Shot dài ra có câu đã ghép môi thì phải ghép môi lại, vì mã đoạn ghép môi có độ dài shot.

**Kiểu chữ.**
- Địa điểm và giờ ("Bệnh viện, 22:10", "Sáng cùng ngày"): dải tối ở góc dưới trái, chữ 34.
- Chữ khác (tên đồ vật, thông báo): giữa khung, hơi dưới tâm, chữ đậm 46, nền tối bo góc.
- Tên phim: chữ 92 trên nền tối 73 %.
- Thay đổi kiểu chữ thì đổi `CaptionVersion` để các shot có chữ được dựng lại.

**Âm thanh cả tập.**
- `master_lufs`, mặc định −14: đo độ to cả tập rồi cộng gain, chặn đỉnh −1 dB.
- `room_tone_db`, mặc định −50: một lớp nhiễu nâu lọc dưới 900 Hz chạy suốt tập, để khoảng lặng không im tuyệt đối.
- Đặt 0 để tắt từng mục.
- Bản 9 chỉ đạt −21 LUFS.

**Ảnh.**
- Viết rõ "no readable text, letters or numbers anywhere" và mô tả trơn những đồ dễ bị vẽ chữ lên: kẹp tóc, thẻ, biển số, màn hình. Kẹp tóc của Mẹ trong `kitchen_ots_bo` từng có chữ "3D".
- Câu nhắc tới một sự việc (quẹt thẻ ở cổng, xe chạy đi) phải có ảnh đúng sự việc đó, không dùng ảnh rộng của phòng.
- Đổi ảnh thì đo lại `faces` của ảnh đó: dựng thử một đoạn ngắn rồi đo trên khung 1920×1080.

**Dùng chung chuyển động cho cảnh sân khấu.** Các shot cùng ảnh `stage_band` dùng clip của SH001. Muốn vậy phải cùng cột `setting`, `characters`, `expressions` và `focus`. Đã xoá `QUAN:hat` ở SH035 và `TUNG` hay `phankhich` ở SH043.

**Chạy lại API.** Bấm đúp `_famixa_m1_test/rebuild_api.cmd`. Script đợi API tắt hẳn (tối đa 20 giây) rồi mới build. Nếu build lỗi vì file bị khoá thì tự tắt lại và build thêm một lần. Lần build lúc 17:38 ngày 05/11 hỏng chính vì lỗi khoá file này.

**Chưa làm được.**
- Chỉnh độ sáng riêng từng shot (mặt Bố quá tối ở câu "…Đèn đường").
- Cắt đuôi một clip chuyển động (SH053d).

## Font, logo trên tên phim, đoạn kết (2026-11-05, bản 12)

**Font.** Mọi chữ trong phim (thẻ chữ, điện thoại, phụ đề đốt vào hình) dùng Be Vietnam Pro, giấy phép OFL, dùng thương mại được.
- Bản chữ thường: `Fonts/BeVietnamPro-SemiBold.ttf`. Bản chữ đậm: `Fonts/BeVietnamPro-ExtraBold.ttf`.
- Tên họ font là "Be Vietnam Pro".
- Các tệp DejaVu cũ vẫn để trong thư mục nhưng không còn dùng.
- `CaptionVersion` đổi thành `card3`, `PhoneUiVersion` đổi thành `phone6`, để mọi shot có chữ được dựng lại.

**Logo trên thẻ tên phim.**
- Cài đặt `title_logo`: tên một ảnh trong `refs/`, không kèm đuôi tệp. EP01 dùng `famixa_logo_nen_toi`, là logo nền trong suốt có chữ màu kem.
- Logo hiện nhỏ phía trên tên phim, chỉ trên hàng có `cue = title_sting`.
- Bố cục thẻ: tên phim chữ lớn, dòng thứ hai màu kem, dòng thứ ba (tập) màu cam. Các phần trong chữ trên màn hình ngăn cách bằng dấu "—".

**Đoạn kết 20 giây.**
- Hiện chưa nằm trong phần mềm, đang ghép thêm vào sau bản dựng bằng ffmpeg. Ảnh hai khung kết ở `_famixa_m1_test/shorts/card1.png` và `card2.png`.
- Cấu trúc:
  - 3 giây đầu: chữ "HẾT TẬP 1", nhạc kết ngân nhỏ dần.
  - 17 giây sau: khung kết. Bên trái là logo, dòng "Theo dõi để không lỡ tập mới" và một vòng tròn chờ nút Đăng ký. Bên phải là "TẬP 2 · tên tập · Sắp ra mắt" và một khung chờ ô "Xem tiếp". Nền là bài "Đèn đường" vặn nhỏ.
- Việc tiếp theo: đưa đoạn kết thành một bước của phần mềm.

**Video ngắn.**
- `make_shorts.py` dựng bản cho YouTube.
- Đặt `VARIANT=social` để dựng bản cho TikTok và Facebook: khung kết là "Theo dõi Famixa để xem tiếp", không nhắc YouTube.

## Các bước tốn phí trên giao diện (2026-11-06)

Trang “Dựng từ bảng câu” có thêm các bước 7–12, nên không cần gọi API bằng tay nữa:

| Bước | Việc | Tốn phí | Mỗi lượt gửi |
| --- | --- | --- | --- |
| 7 | Ảnh cảnh từ `keyframe_specs` | Runway credit | 1 ảnh (không chạy hai lượt tạo ảnh cùng lúc) |
| 8 | Chuyển động; chọn mô hình gen4_turbo hoặc gen4.5 ngay trên thẻ (ghi `motion_model` vào settings); sửa câu lệnh từng shot | Runway credit | 2 shot |
| 9 | Ghép môi | fal USD | 4 câu |
| 10 | Logo trên thẻ tên phim (`title_logo`) và phần kết (`outro`) | Không | — |
| 11 | Dựng video (bước 7 cũ) | Không | — |
| 12 | Soát trước khi đăng: máy kiểm file, dòng thời gian, ảnh, chuyển động, ghép môi, phần kết, bản dựng 1920×1080 cả tập; thêm 8 mục xem bằng mắt (đánh dấu lưu trên trình duyệt) | Không | — |

- Bấm **Xem trước** (không tốn tiền) → bảng từng mục, chọn sẵn mục chưa có. **Làm lại mục đã có** gửi `force`.
- **Chạy** hiện tổng tiền rồi chia thành từng lượt nhỏ. Mỗi lượt gửi đúng số tiền của lượt đó, máy chủ từ chối nếu lệch. **Dừng** chỉ dừng sau lượt đang chạy.
- **Xem** mở kết quả: ảnh qua `GET productions/{id}/keyframes/{tên}` (endpoint mới), clip qua `GET productions/{id}/motion/{shot}`.
- Code: `client/admin/src/modules/content/LinesheetAiSteps.tsx` (thẻ `StepCard` cũng chuyển sang đây), hàm gọi API ở `famixa-linesheet.api.ts`.
- Endpoint mới cần build lại API (`_famixa_m1_test/rebuild_api.cmd`); trang thì Vite tự nạp lại.
- Sửa cùng ngày: Xem trước chuyển động coi clip là “đã có” theo đúng cách bản dựng chọn clip (khớp ảnh, với câu lệnh/độ dài/mô hình lúc tạo clip). Trước đó clip làm bằng gen4.5 hoặc shot vừa kéo dài bị tính là thiếu, EP01 hiện 42 shot “chưa có” (1.050 credit) dù phim đã đủ.


## Chặn lỗi tốn tiền (sửa 2026-11-09, theo docs/famixa/SO_LOI_TON_TIEN.md)

- Giá ảnh gpt_image_2 mức high = 20 credit (đối chiếu trang Usage Runway; trước ghi 41). Mức low/medium chưa đối chiếu.
- Xem trước chuyển động trả thêm `warnings` (motion_share) và `creditBalance`. Shot có thoại dùng chung clip lệch khóa (khác ảnh, nhân vật, nét mặt, trọng tâm) thì tạo chuyển động bị chặn với mã `LINESHEET_MOTION_SHARE`: bỏ dòng đó khỏi motion_share hoặc trỏ tới shot thoại cùng ảnh.
- Tạo ảnh và tạo chuyển động bị chặn nếu số dư Runway nhỏ hơn chi phí lô + 20% (`LINESHEET_IMAGE_BALANCE`, `LINESHEET_MOTION_BALANCE`). Không đọc được số dư thì không chặn.

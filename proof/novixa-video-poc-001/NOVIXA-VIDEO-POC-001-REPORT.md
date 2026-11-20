# NOVIXA video POC 001 — báo cáo

## A. Repository

- Repo: `E:/KitPlatform`
- Branch: `recovery/production-source-20261021`
- HEAD: `8c59ceb4f3be537898106ec058ea547406f722e5`
- Working tree trước khi làm: đã bẩn, gồm nhiều file sửa và file chưa theo dõi. Không reset, stash, clean, checkout hay commit.
- Sau khi làm: thêm `proof/novixa-video-poc-001/`, sửa renderer local vốn đã chưa theo dõi, thêm test dãy khung và test proof. `ContentVideoService.cs` vẫn ở trạng thái sửa sẵn từ trước; file này không bị sửa trong nhiệm vụ. Ảnh và logo gốc trong `docs/novixa` không đổi.

## B. Pipeline NOVIXA thực tế

Luồng đang chạy cho `/content/videos` là `ContentVideosPage` → `ContentVideoService`. Với brand `novixa`, template `tiktok_45s_hooks`, provider khác `creatomate`, `NovixaLocalRenderGate.IsGate` trả true và render đi qua `MarketingLocalVideoRenderer`. Famixa production voice/image không thuộc luồng này.

| Bước | Trạng thái | Bằng chứng trong code |
|---|---|---|
| BRIEF | EXISTS BUT NOT VERIFIED | Brief nằm trên package và `ContentQualityGate` đòi brief khi duyệt package. `CreateFromPackageAsync` không đọc brief. |
| SCRIPT | EXISTS BUT NOT VERIFIED | `PickScript` lấy `tiktok_script` rồi các kind khác. `UpdateScriptAsync` sửa script. Sinh script nằm ở `ContentGenerateService`, không nằm trong render. Chưa chạy generate trong nhiệm vụ này. |
| SCRIPT QA | MISSING trên luồng video | `ContentQualityGate.Evaluate` kiểm độ dài `tiktok_script`, claim cấm và brief, nhưng `ContentVideoService` không gọi gate này. |
| VOICE | BLOCKED với tài nguyên được phép | `RunMvpPipelineAsync` gọi `ContentElevenLabsClient.SynthesizeMp3Async` khi có key. Đường video không gửi voice settings. `StripSpokenDirection` xóa tag trong ngoặc vuông. ElevenLabs là API tính phí, không được gọi. |
| VOICE QA | EXISTS, mới kiểm mức kỹ thuật | `ProbeAudioCoversAsync` chỉ kiểm file nằm trong job, có audio stream và dài hơn storyboard. Không chấm diễn xuất. |
| SHOT PLAN | EXISTS BUT NOT VERIFIED | `BuildStoryboardObjects` chia script theo nhãn HOOK/PROBLEM/INSIGHT/SOLUTION/CTA, chia đều thời lượng, gán một câu visual hint và một prompt ảnh chung. Không có góc máy, ánh sáng hay chuyển cảnh. |
| VISUAL ASSETS | BLOCKED trên đường mặc định | `BuildPollinationsImageUrl` và `MaterializeSceneImageAsync` chỉ nhận `https://image.pollinations.ai`. Không gọi. Nếu `scene-NN-source.bin` đã giải mã được thì hàm dùng lại file đó. |
| EDIT/MIX | EXISTS AND VERIFIED cho đường ảnh đứng; đường khung mới đã chạy | Đường cũ: một PNG mỗi cảnh, ffmpeg giữ yên, trộn `voice.mp3`. Test đơn vị 6/6 pass, gồm test không đưa lời vào lệnh ffmpeg. Đường mới: `FrameSequence` tùy chọn, chỉ dùng khi có danh sách khung. |
| FINAL RENDER | EXISTS AND VERIFIED cho bản proof | `RenderAsync` xuất H.264, AAC, 1080×1920. Bản proof đi qua đúng class này. |
| FINAL QA | EXISTS ở mức giải mã | `ProbeMp4Async` kiểm H.264, đúng kích thước, đủ dài và ffmpeg giải mã được. Không có QA nội dung trong code. |

`ContentVideoService` chưa gọi đường `FrameSequence`. Bản MP4 được `MarketingLocalVideoRenderer.RenderAsync` tạo trực tiếp, không qua database, Pollinations hay ElevenLabs.

## C. Kịch bản

Lời đọc và tự kiểm nằm trong `SCRIPT.md`. Bản đã đọc dài khoảng 57 giây lời, hình giữ logo đến giây 60. «Ba trăm triệu» là tình huống. Không có câu hứa lợi nhuận. Câu sản phẩm chỉ vào Tổng quan, báo cáo tồn kho theo giá vốn lô, và báo cáo ca làm việc.

## D. Shot plan và asset

Thiết kế cảnh: `SHOT-PLAN.md`. Mốc đã xuất: `job/timeline.json`.

Asset copy, không sửa gốc:

- `docs/novixa/launch/DOC-008/assets/screenshots/01-dashboard.png`
- `docs/novixa/launch/DOC-008/assets/screenshots/08-reports-home.png`
- `docs/novixa/launch/DOC-008/assets/screenshots/04-inventory-stock.png` (copy để lưu nguồn, shot cuối không dùng)
- `docs/novixa/assets/brand/novixa-logo.png`

Không có footage người thật có quyền dùng. Không vẽ người bằng khối hình.

## E. Thay đổi code

- `MarketingLocalVideoRenderer`: thêm `FrameSequence` và `FrameDurationSec`. Khi không set, đường ảnh đứng giữ nguyên. Khi set, ghép các khung có sẵn, không vẽ lại lời lên lệnh ffmpeg, encode `veryfast` CRF 18.
- `MarketingLocalVideoRendererTests`: một test cho đường khung.
- `NovixaVideoProof001Tests`: mux bản proof khi có biến `NOVIXA_VIDEO_PROOF_DIR`. Không có biến thì test thoát ngay, để suite thường không encode 60 giây.

## F. Test

- `MarketingLocalVideoRendererTests`: 6 passed.
- `NovixaVideoProof001Tests` với `NOVIXA_VIDEO_PROOF_DIR` trỏ vào `proof/novixa-video-poc-001/job`: 1 passed. Test này gọi `ProbeMp4Async`.
- Không chạy migration, không chạy suite Famixa.

## G. Video

- File: `E:/KitPlatform/proof/novixa-video-poc-001/job/output.mp4`
- 60,0 giây, 1080×1920, H.264 High, 30 fps, AAC LC 48 kHz stereo
- 1.950.250 byte
- SHA-256: `3705F9B2BCE193A184BA3AF997D0DCDD1D55169B6B128F6C76CE3D08FCC76F68`
- Đỉnh âm −3,6 dB, trung bình −20,2 dB. Không có khung đen theo `blackframe` amount 98.

## H. QA

Đã làm được bằng công cụ:

- File mở được, đúng tỷ lệ, đúng 60 giây, có audio, ffmpeg giải mã hết.
- Không clipping ở đỉnh −3,6 dB.
- Không khung đen.
- Ảnh sản phẩm là crop từ ảnh chụp thật, có dòng «Giao diện thật · dữ liệu demo». Logo là file thương hiệu, nền trắng đã tách trong bộ nhớ.
- Đã xem các khung khóa của hook, vấn đề, insight, bốn cảnh sản phẩm và logo. Chữ kiểm tra không bị cắt ở các khung đó.

Chưa được coi là đã đạt:

- Diễn xuất giọng. Không nghe trực tiếp. Giọng Edge không có style cảm xúc. Chỉ điều tốc độ, cao độ và nghỉ.
- Cảm giác khi xem liền 60 giây. Chưa xem file như một người xem hết video.
- Hai khoảng im khoảng 1,0 giây (khoảng 3,8–4,8 giây và 34,7–35,8 giây).
- Từ khoảng 56,0 giây đến 60,0 giây là logo không có lời, để chạm mốc 60 giây.

## I. Chi phí

Không gọi ElevenLabs, Creatomate, Pollinations, Gemini, Runway hay Fal. Không mua credit, không đổi billing. Tiếng dùng `edge-tts` 7.3.1 đã cài sẵn, giọng `vi-VN-NamMinhNeural`, không khóa API.

## J. Production

Không migration, deploy, commit, push, merge. Không dùng credential production. Không ghi đè video, ảnh, audio hay báo cáo có sẵn.

## K. Giới hạn và bước tiếp

Bản này chứng minh renderer local có thể xuất một video 9:16 có lời, motion graphics và UI thật khi được đưa sẵn khung hình. Nó chưa chứng minh `RunMvpPipelineAsync` tự tạo được video này, vì đường đó vẫn cần ElevenLabs và Pollinations.

Bước tiếp, nếu được duyệt: nối asset cục bộ và file giọng có sẵn vào `ContentVideoService` để không bắt buộc Pollinations; giữ ElevenLabs sau một lần phê duyệt chi phí riêng. Chưa nên tách một video engine dùng chung.

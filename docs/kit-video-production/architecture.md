# Kiến trúc hệ thống sản xuất video

## Vì sao không nối vào pipeline video hiện có

Trong repository đã có hai đường liên quan đến video:

- Production OS / Content pack trộn ảnh đứng hoặc video từ nhà cung cấp bằng `FfmpegMixExecutionAdapter`. Đường đó gắn với episode, tenant và vận hành nền tảng. Nó không phải đồ thị cảnh 2D đọc từ kịch bản.
- Các thư mục `proof/novixa-*` là script dựng một phim, với thời lượng và danh sách cảnh viết trong code (ví dụ intro v1 khóa 53 giây và các mốc cảnh).

V1 là một công cụ riêng, `tools/kit-video-production`. Nó đọc Pillow để vẽ khung và FFmpeg 8.1.2 đã có trên máy để encode. Không thêm framework mới, không sửa DI của Content pack, không ghi database.

## Pipeline

Mỗi manifest khai đủ 12 bước, đúng thứ tự:

`brief → script_analysis → story_structure → shot_plan → asset_plan → asset_creation → voiceover → animation → audio_mix → render → qa → delivery`

Mỗi bước có `automation` (`automated`, `assisted`, `manual`) và `status`. Có bước là tự động không có nghĩa là cả phim tự động.

| Bước | V1 làm được | Vẫn cần người |
| --- | --- | --- |
| Brief | Giữ file nguồn | Duyệt thông điệp, tỷ lệ khung, CTA |
| Script analysis | Ước lượng thời lượng theo số ký tự, cảnh báo nếu dài hơn hint | Khóa lời |
| Story structure | Lưu beat do kịch bản đưa | Chia hoặc gộp cảnh |
| Shot plan | Validate schema | Sửa timing, template, lời trước khi dựng |
| Asset plan | Báo asset thiếu, từ chối UI giả | Chọn và duyệt file thật |
| Asset creation | Vẽ hình khối theo template; tổng hợp SFX placeholder | Nhân vật, nhạc, UI đã xác minh |
| Voiceover | edge-tts theo [chuẩn giọng V1](voice-direction-standard-v1.md): profile, preset và từ điển tách khỏi lời | Nghe phát âm, nhịp, tên thương hiệu trước khi khóa |
| Animation | 10 template cấu hình bằng params | Cảnh `custom`, duyệt phong cách |
| Audio mix | Ghép lời, SFX, ducking, không `atempo` | Nhạc có bản quyền, thiết kế tiếng |
| Render | Encode H.264/AAC, thư mục mới mỗi lần, dựng một hoặc nhiều cảnh | Chọn preset/CRF nếu cần bản đẹp hơn |
| QA | Giải mã, thời lượng, khung, codec, đen, clipping, lặng, chữ, nhãn demo | Xem hết hình và nghe hết tiếng |
| Delivery | Ghi đường dẫn trong báo cáo | Quyết định phát hành. V1 không phát hành |

## Thời lượng

Không có hằng số thời lượng, số cảnh, hay tỷ lệ khung trong code lõi. Tổng thời lượng là tổng `durationSeconds` của các cảnh được chọn, hoặc `voiceLead + độ dài file lời + voiceTail` khi `durationSource` là `voice_track`. Nếu lời dài hơn cảnh `fixed`, render trả `BLOCKED` và không tăng tốc giọng.

`durationPolicy.targetSeconds` chỉ là hint của dự án đó. Lệch hơn 15% thì validator cảnh báo, không sửa số cho khớp.

## Một lần render

1. Validate manifest và shot plan.
2. Chặn cảnh chưa `approved`, trừ `style_proof` khi có cờ.
3. Chặn asset `missing` / `rejected`, placeholder khi dự án không cho phép, và UI sản phẩm chưa `approved`.
4. Trộn tiếng từng cảnh, nối lại, pipe khung `rgb24` sang FFmpeg. Stderr của FFmpeg ghi ra file, không giữ trong pipe.
5. Ghi `video.mp4`, `render-log.json`, `layout.json`, khung preview. Thư mục có dấu thời gian. Không ghi đè thư mục cũ.

Render xong không phải là chất lượng nghệ thuật đã đạt. QA kỹ thuật và mục cần người nghe/xem là hai việc khác nhau.

## Lõi sản xuất và lớp hình

Từ V1.1, lõi không đổi việc của nó: manifest, shot plan, thời lượng, asset, encode, QA. Lớp hình nằm ở `kit_video/picture.py` và được gọi qua template `stock_story`. Template V1 vẫn đăng ký, nên dự án cũ dựng lại được.

`stock_story` đọc `mode`, `headline`, `boxCount`, `fastCount`, `beats` và palette. Đổi brand, số thùng, câu chữ, thời lượng hoặc tỷ lệ khung không cần sửa hàm vẽ. Màu `skin`, `wood`, `wall`, `floor`, `cloth` là tùy chọn; thiếu thì engine suy ra từ mực và giấy.

Âm thanh vẫn là lớp riêng: lời, SFX, ducking. Không có nhạc trong proof V2. Giọng không bị `atempo`.

## Sân khấu cắt lớp

`reference_stage` là lớp hình thứ hai. Nó không vẽ hộp bằng khối. Nó đọc một file sân khấu: nền, danh sách lớp, đường đi, và thẻ chữ. Ảnh nằm trong manifest. Câu chữ và nhãn lấy từ `params`. Màu thẻ lấy từ palette. Hàm chuyển động là một: nghỉ, lấy đà, cung, đáp. Đổi asset hoặc đổi câu không sửa hàm đó.

Lõi render, validate và QA không đổi. Template chỉ được thêm vào bảng đăng ký.

## Quảng cáo bằng image-to-video

`stock_story` và `reference_stage` giữ cho bản dựng cục bộ. Chúng không phải đường tạo nhân vật 3D. Quảng cáo đi qua `produce`: đọc `commercial.json`, lập job image-to-video, và chỉ vẽ thẻ chữ ở hậu kỳ.

Adapter nằm ở `kit_video/motion.py`. Runway dùng `gen4_turbo` tại `v1/image_to_video`. Veo dùng service account Vertex, không dùng khóa Gemini. Fal dùng `fal-ai/wan-i2v`. Secret chỉ đọc từ biến môi trường. File biên bản ghi tên biến, không ghi giá trị. Không có keyframe đã duyệt hoặc không có credential thì job là `BLOCKED` và không gửi mạng.

Một file provider trả về chưa phải cảnh đạt. `visualReview` giữ `VISUAL_REVIEW_REQUIRED` cho đến khi có người xem. Thẻ kết `end-card.mp4` không phải video quảng cáo.

Ba nhóm hình nằm trong cùng `commercial.json`: nhân vật và nhà thuốc, đồ họa số liệu, và UI. Bảng số vẽ từ `demoData` bằng `kit_video/demo_board.py`. Checklist hình thêm biểu cảm, độ cách điệu, chữ số ổn định, chuyển động phục vụ nội dung, và nguồn UI. Không có engine riêng cho các nhóm này.

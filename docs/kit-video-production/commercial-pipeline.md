# Pipeline quảng cáo NOVIXA

Đường này thuộc KIT Video Production. Nó không thay các template Pillow. Pillow và FFmpeg vẫn vẽ thẻ chữ và encode. Hình nhân vật 3D phải đến từ image-to-video, sau một keyframe đã duyệt.

## Chạy

Từ thư mục gốc repository:

```text
python tools/kit-video-production/kv.py produce proof/kit-video-production-v1/projects/novixa-pharmacy-commercial --repo E:\KitPlatform
```

Lệnh thoát mã 2 khi còn blocker. Thư mục mới nằm dưới `proof/kit-video-production-v1/outputs/novixa-pharmacy-commercial/`. Không ghi đè lần cũ.

## Cấu hình provider

`commercial.json` chọn `provider.id`: `runway`, `veo`, hoặc `fal`.

| id | Model mặc định | Biến môi trường | Ghi chú |
| --- | --- | --- | --- |
| `runway` | `gen4_turbo` | `RUNWAY_API_KEY` | `POST /v1/image_to_video`. Có thể kèm khung cuối. |
| `veo` | `veo-3.1` | `VEO_JOIN_FILE` | File service account Vertex. Không phải khóa Gemini. |
| `fal` | `wan-2.1` | `FAL_KEY` | Hàng đợi `fal-ai/wan-i2v`. |

Không ghi secret vào json hoặc source. Lệnh `produce` hiện không gửi mạng. Một clip chỉ được gắn sau bằng `replace_scene` khi file thật đã có, và biên bản vẫn là `VISUAL_REVIEW_REQUIRED`.

## Việc hậu kỳ làm được ngay

Cảnh `kind: post` không gọi provider. Lần chạy viết `end-card.mp4` với đúng các dòng trong `endCard.lines`. File đó là lớp chữ. `finalVideo` vẫn `null`.

## QA

Technical: codec, khung, thời lượng của thẻ chữ, và việc không có `video.mp4` khi còn blocker.

Visual: các mục trong biên bản, gồm biểu cảm, độ cách điệu, số liệu và nguồn UI, đều `VISUAL_REVIEW_REQUIRED` cho đến khi có người xem clip thật. Unit test qua không có nghĩa hình đẹp.

`demoData` khóa một bộ số. Bảng `demo-board.png` do code vẽ từ bộ đó. Cột cuối của biểu đồ phải bằng số đơn đã khai. Lệch số thì gate dừng ở B và không gửi provider. Nguồn `mockup` không được ghi là ảnh chụp sản phẩm.

Một keyframe chỉ được gửi khi `approval` là `APPROVED` và file còn trên đĩa. `AWAITING_REVIEW` không phải đã duyệt. Lệnh `produce` không tự đổi trạng thái đó.

## Gate hiện tại

Pipeline đang trả gate B vì keyframe chưa `APPROVED`. Video đầu tay chưa có: `firstVideo.status` là `BLOCKED` tại cổng asset hình. Preview v7 nằm ở `keyframes/v7/`. s02–s04 vẫn trỏ keyframe v2 đã loại và không được gửi. Chưa có clip chuyển động.

Video tham chiếu `docs/kit-video-production/references/novixa-gemini-reference.mp4` vẫn không có. Mục tiêu hình là visual bible, không phải video logo.

`RUNWAY_API_KEY`, `FAL_KEY`, `GEMINI_API_KEY`, `GOOGLE_API_KEY` và `VEO_JOIN_FILE` vẫn trống. Không có request nào được gửi. `finalVideo` vẫn null. `demo-board.png` là mockup số liệu, không phải quảng cáo.

Bước tiếp theo: duyệt hoặc từ chối preview v7. Cảnh tương tác của nhân vật chỉ làm sau khi preview được duyệt và có phép dùng provider.

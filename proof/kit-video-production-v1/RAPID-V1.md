# Rapid video V1

Hai phim 20 giây, 1280×720, 24 fps, H.264 + AAC, dựng bằng `tools/kit-video-production`. Không có nhân vật rig. Không gọi dịch vụ video trả phí.

| Phim | File | Contact sheet |
| --- | --- | --- |
| Novixa | `outputs/rapid-novixa-20/20261029T022748Z-cut/video.mp4` | cùng thư mục, `contact-sheet.png` |
| Agent Trading | `outputs/rapid-trading-20/20261029T023109Z-cut/video.mp4` | cùng thư mục, `contact-sheet.png` |

Đường đầy đủ nằm dưới `proof/kit-video-production-v1/`. Dự án nguồn:

- `projects/rapid-novixa-20`
- `projects/rapid-trading-20`

QA kỹ thuật của cả hai bản: PASS (thời lượng 20,000 giây, 480 khung, 1280×720, 24 fps, H.264, AAC, chữ trong vùng an toàn, không khung đen). Tổng thể máy vẫn là `NEEDS HUMAN REVIEW` vì máy không chấm nghĩa hình và không nghe giọng.

## Đổi nội dung rồi render lại

Từ gốc repository:

```text
python tools/kit-video-production/kv.py validate proof/kit-video-production-v1/projects/<id>
python tools/kit-video-production/kv.py render proof/kit-video-production-v1/projects/<id> --repo E:\KitPlatform --label cut
python tools/kit-video-production/kv.py qa <thư-mục-vừa-in> --project proof/kit-video-production-v1/projects/<id>
```

Mỗi lần `render` tạo thư mục mới. File cũ không bị ghi đè.

| Muốn đổi | Sửa ở đâu |
| --- | --- |
| Câu trên hình, số, nhãn | `shot-plan.json` → `params` và `onScreenText`. Chuỗi trong `onScreenText` phải là chuỗi painter thực sự vẽ. Đừng khai chữ của cột xuất hiện muộn: máy sẽ vẽ chữ đó thành dải trắng ở đáy trong những khung đầu. |
| Lời đọc | `script.md`, rồi `python tools/kit-video-production/kv.py voice --text "..." --out <project>/assets/voice/<cảnh>.wav`. Gắn `voice.tracks` trong `manifest.json` và `audio.voiceTrackId`. Tốc độ cố định `+0%`. Lời dài hơn cảnh thì render dừng; hãy rút lời hoặc nới `durationSeconds`. |
| Thời lượng | `durationSeconds` của từng cảnh. Beat trong template là tỷ lệ của cảnh, nên nới cảnh không phải sửa code. Tổng nên gần `durationPolicy.targetSeconds`. |
| Màu, font | `manifest.json` → `visualStyle`. Font phải có dấu tiếng Việt. Đang dùng Segoe UI. |
| Logo | Thêm một dòng `assets` trỏ file có sẵn, `approval: approved`, quyền dùng và `sha256` đúng file. Gắn `params.logoAssetId`. Không sửa file logo. |
| Nhãn trượt | `params.lanes`. Mỗi lane một nhãn ngắn. Nhãn dài hơn ô trái sẽ tràn. Đừng đặt mốc chữ ở mép phải của đường ray. |
| Biểu đồ | `params.chart.bars`. Độ dài khác nhau cần `confirmed: true` và một câu trên hình nói rõ đây là mô phỏng. |
| Xem vài khung trước | thêm `--preview-only`. |

`brand` trong manifest là tên phim. Trading không tham chiếu logo hay chữ nhà thuốc.

## Đã tái sử dụng

- Engine `kit_video`: manifest, shot plan, validate, render, QA, contact sheet.
- Template có sẵn: `designed_board` (`brand_center`, `lanes`, `dashboard`, `triptych`) và `brand_reveal`.
- Easing, font, FFmpeg và edge-tts đã có trong công cụ.
- Logo Novixa gốc, không sửa byte.
- Cùng bốn bước cho cả hai phim: mở, một nhịp ba việc, biểu đồ mô phỏng, ba cột kết. Khác brand, palette, chữ và số.

## Còn thủ công

- Viết `shot-plan.json` cho từng phim. Máy không tự chọn bố cục đẹp.
- Cắt lời cho vừa 20 giây, rồi khai khoảng lặng sau khi đo file wav.
- Xem khung hình. Lần này đã bỏ mốc `Chiều` vì chữ chạm mép phải, và rút nhãn `Khối lượng` trên ray thành `Khối` vì tràn ô.
- Nghe giọng. Track đang `proposed`. QA không nghe thay người.

## Giới hạn

- Không có người diễn, không có camera pan/zoom trên các board này. Chuyển động là chữ và logo vào khung, nhãn trượt trên ray, cột biểu đồ mọc, ba cột kết lần lượt hiện. Cảnh mở Novixa gần như giữ logo; scale rất nhỏ.
- Cắt cảnh thẳng, không có chồng mờ.
- Không có nhạc. Giọng là edge-tts `vi-VN-NamMinhNeural`, chưa có người nghe duyệt phát âm.
- Số trên hình là mô phỏng. Video Trading không phải khuyến nghị giao dịch.
- Chưa phải bản phát hành thương mại. `finalVideo` vẫn null.
- Đổi một chữ trên hình là sửa JSON rồi render lại, không cần gọi AI video.

# Tạo một dự án video mới

1. Tạo thư mục `proof/kit-video-production-v1/projects/<id>/`. Không viết vào `proof/novixa-*` hay `docs/novixa/assets`.
2. Viết `script.md` theo các khối `## segment <id>` mô tả trong [shot plan](shot-plan-schema.md). Giữ nguyên lời đã được giao. Chưa đặt 15 giây cho mỗi cảnh.
3. Chạy `analyze`. Mở `shot-plan.proposed.json`. Sửa template, lời trên hình, ẩn dụ và chuyển cảnh. Giữ `approvalStatus` của cảnh là `proposed` cho đến khi người phụ trách khóa.
4. Nếu một cảnh báo `voice_exceeds_hint`, nới cảnh hoặc sửa lời. Không tăng tốc giọng.
5. Viết `manifest.json` trỏ `shotPlan` tới file đề xuất hoặc tới bản đã sửa. Khai brand, palette, tỷ lệ, frame rate, font, và asset thật. `render.versioned` phải là `true`. `outputRoot` nên là `../../outputs` để file nằm dưới `proof/kit-video-production-v1/outputs`.
6. Chạy `validate`. Hết FAIL mới dựng. Cảnh báo lệch hint là thông tin, không phải lệnh phải ép cho khớp.
7. Với lời đọc: `voice --text "..."` ra wav và `boundaries.json`. Đặt track `approval: proposed` cho đến khi có người nghe. Gắn `durationSource: voice_track` nếu muốn cảnh ôm theo file, kèm `voiceLeadSeconds` và `voiceTailSeconds`. Chọn beat hình theo `start` trong boundaries. Không dùng tốc độ khác `+0%` nếu chưa được duyệt.
8. `--preview-only` để xem vài khung. Sửa params. Sau đó render. Rồi `qa`.
9. Muốn dựng lại một cảnh: `--scenes <id> --label <tên mới>`. Thư mục cũ giữ nguyên.
10. Cảnh chưa có template phù hợp để `templateId` lạ. Render sẽ chặn. Đó là chỗ thiết kế riêng, không phải lỗ hổng để bịa một cảnh bằng ảnh tĩnh.

Fixture `reuse-short` và `reuse-structured` là mẫu cấu trúc, có nhãn kiểm thử trên hình. Đừng dùng chúng làm phim thương hiệu.

Đổi thời lượng, số cảnh hoặc 9:16 so với 16:9 bằng manifest và shot plan. Không sửa `kit_video` cho từng phim.

Template `stock_story` nhận `mode` (`acting`, `metaphor`, `hybrid`), `headline`, `boxCount`, `fastCount` và `beats`. Ba hướng của proof V2 được vẽ lại bằng:

```text
python tools/kit-video-production/kv.py storyboard --out <thư-mục>
```

Lệnh đó chỉ xuất khung so sánh cho đoạn NOVIXA đã chốt. Phim khác truyền palette và params qua manifest, không sửa lệnh này.

Template `reference_stage` đọc `params.stageFile`. File đó khai nền, lớp, và đường đi. Nhãn và câu chữ để trong ngoặc nhọn, ví dụ `{productLabel}`, rồi shot plan điền giá trị. Đổi câu hoặc màu không sửa code chuyển động. Ảnh phải có trong manifest và có file thật.

# Render và QA

## Render

```text
python tools/kit-video-production/kv.py render <project-dir> --repo E:\KitPlatform --label <tên>
python tools/kit-video-production/kv.py render <project-dir> --repo E:\KitPlatform --scenes id1,id2 --label <tên>
python tools/kit-video-production/kv.py render <project-dir> --repo E:\KitPlatform --scenes <id> --allow-style-proof --preview-only --label preview
```

Thư mục xuất: `<outputRoot>/<projectId>/<utc>-<label>/`.

Bên trong có `video.mp4` (trừ preview), `scene-*.wav`, `render-log.json`, `layout.json`, `frames/`, `ffmpeg-encode.log`.

`render-log.json` ghi cảnh đã dựng, số khung, thời lượng đã snap theo frame rate, hash SHA-256 của manifest và shot plan tại lúc encode, và danh sách placeholder. Sửa manifest sau lần encode làm hash hiện tại khác hash trong log.

Encode: libx264, yuv420p, AAC 192 kb/s, 48 kHz, stereo, `-movflags +faststart`. CRF và preset lấy từ manifest.

Thiếu FFmpeg, font, file lời, file SFX hoặc template: BLOCKED, kèm id. Không tạo hình thay thế.

## QA

```text
python tools/kit-video-production/kv.py qa <render-dir> --project <project-dir>
```

Báo cáo ghi đè `qa-report.json` trong chính thư mục render đó và, nếu có khung preview, ghi `contact-sheet.png`.

| id | Việc kiểm |
| --- | --- |
| decode | `ffmpeg -f null` exit 0 |
| duration | Khớp tổng khung / frame rate |
| resolution, frame_rate, codec | Khớp manifest. Kỳ vọng H.264 và AAC |
| black_frames | `blackdetect` không thấy đoạn đen |
| audio_levels | `max_volume` không quá −0,4 dB. Nếu cảnh có lời mà mean dưới −45 dB thì FAIL |
| silence | Khoảng lặng dài hơn `silenceMinSeconds` phải nằm trong các `intentionalSilences` đã nối lại |
| text_safe_area | `layout.json` không có vi phạm |
| required_text | Mỗi chuỗi `onScreenText` của cảnh đã dựng có trong layout |
| demo_label | UI demo đã dựng phải có nhãn đúng |
| placeholder_disclosure | Placeholder trong log được ghi ra, không bị gọi là asset hoàn thiện |
| visual_storytelling, audio_listening | Luôn `NEEDS HUMAN REVIEW` |

`technicalStatus` bỏ hai mục người xem. `overallStatus` gồm cả hai, nên bản kỹ thuật sạch vẫn là `NEEDS HUMAN REVIEW` cho đến khi có người xem và nghe.

Thứ tự gom: FAIL, rồi BLOCKED, rồi NEEDS HUMAN REVIEW, rồi PASS.

Contact sheet không thay cho việc xem cả file và nghe tiếng.

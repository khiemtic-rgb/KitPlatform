# NOVIXA video series (bản thử, chưa duyệt)

Nguồn: tạo trong phiên Park Marketing ngày 2026-10-29. Chưa duyệt, chưa đưa vào danh mục (catalog), không gắn với video_job hay Video Studio.

- `video/`: 5 clip khổ dọc 1080x1920 (38–44 giây) + bản đầy đủ 2 phút 54 giây, kèm `KICH-BAN.md`.
- `voice/`: danh sách 38 câu lời đọc (`voice_lines.json`), script tạo giọng ElevenLabs (đọc khoá từ file cục bộ, không chứa khoá), `voice_el.zip` (38 file mp3 đã tạo).
- `src/`: mã dựng (`series.py`, `scenes.py`, `illus.py`) và ảnh dùng trong cảnh (`shots/`: 4 ảnh chụp giao diện localhost dữ liệu demo, logo Novixa).

Dựng lại (Linux/ffmpeg/Python+Pillow, Piper tuỳ chọn):
`VOICE_DIR=<thư mục chứa mp3 đã giải nén> python3 series.py <thư mục ra> [01_tien-di-dau,...,long]`

Kiểm tra đã làm: H.264/AAC, 1080x1920, 30 fps, âm thanh và hình cùng độ dài, độ ồn khoảng -14 LUFS, đỉnh âm không quá -1 dBFS, xem các khung cuối mỗi cảnh (chữ không tràn khung).
Chưa làm: nghe thử bằng tai, canh phụ đề theo thời điểm từng từ, bản ngang 16:9.

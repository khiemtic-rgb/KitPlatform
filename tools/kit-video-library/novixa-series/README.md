# Novixa series — video ngắn → dài (bản thử, chưa duyệt)

Mã nguồn dựng series 5 clip + bản dài cho Reels / TikTok / YouTube Shorts, góc nhìn chủ nhà thuốc.
Kịch bản: `KICH-BAN.md`. Giọng: ElevenLabs qua `voice/make_voice_elevenlabs.py` (khóa lấy từ biến môi trường, không có trong repo).

Trạng thái: **bản thử, chưa duyệt**. Chưa nằm trong catalog, chưa gắn với `video_job` hay Video Studio.
Video đã dựng nằm ở `proof/kit-video-library/20261029T153000Z-novixa-series/video/` (không chép vào đây).

## Dựng lại
```
python src/series.py <thư-mục-ra> [clip,...,long]
VOICE_DIR=<thư-mục-mp3> python src/series.py <thư-mục-ra> long
```
Cần Python 3, Pillow, ffmpeg. Không ghi database, không đổi DI của Content.
Không đặt khóa ElevenLabs hay file khóa vào thư mục này.

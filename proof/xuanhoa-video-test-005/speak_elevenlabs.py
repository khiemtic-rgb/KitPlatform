"""Xuân Hòa video test 003 — tạo giọng ElevenLabs cho từng câu. CHẠY TRÊN MÁY ANH.

Khóa API chỉ đọc từ biến môi trường ELEVENLABS_API_KEY, không ghi ra file hay log.
Chỉ dùng thư viện chuẩn của Python, không cần cài thêm gì.

PowerShell, đứng trong thư mục này:
    $env:ELEVENLABS_API_KEY = "khóa của anh"
    python speak_elevenlabs.py

- Câu nào đã có file mp3 thì bỏ qua, không tính tiền lần nữa. Muốn đọc lại một câu: xóa file đó rồi chạy lại.
- Model mặc định eleven_v3 (repo đang dùng cho Famixa). Đổi bằng: $env:ELEVENLABS_MODEL = "..."
- Trước khi gửi, script in tổng số ký tự sẽ đọc và hỏi xác nhận.
"""

from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE / "job" / "voice-parts"
API = "https://api.elevenlabs.io/v1/text-to-speech/{voice}?output_format=mp3_44100_128"


def main() -> None:
    key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not key:
        sys.exit("Thiếu ELEVENLABS_API_KEY. Đặt biến môi trường rồi chạy lại.")
    model = os.environ.get("ELEVENLABS_MODEL", "eleven_v3").strip()
    spec = json.loads((HERE / "lines.json").read_text(encoding="utf-8"))
    voices = {who: v["elevenlabsVoiceId"] for who, v in spec["voices"].items()}
    OUT.mkdir(parents=True, exist_ok=True)

    todo = [l for l in spec["lines"] if not (OUT / f"{l['id']}.mp3").exists()]
    chars = sum(len(l.get("tts", l["text"])) for l in todo)
    print(f"Model {model}. Cần đọc {len(todo)}/{len(spec['lines'])} câu, khoảng {chars} ký tự.")
    if not todo:
        print("Đã đủ file. Không gửi gì.")
        return
    if input("Gửi lên ElevenLabs? Gõ 'co' để tiếp tục: ").strip().lower() != "co":
        sys.exit("Đã dừng, không gửi.")

    for line in todo:
        body = json.dumps({
            "text": line.get("tts", line["text"]),
            "model_id": model,
            "voice_settings": {"stability": 0.5, "similarity_boost": 0.75, "style": 0.0, "use_speaker_boost": True},
        }).encode("utf-8")
        req = urllib.request.Request(API.format(voice=voices[line["who"]]), data=body, method="POST", headers={
            "xi-api-key": key, "Content-Type": "application/json", "Accept": "audio/mpeg"})
        try:
            with urllib.request.urlopen(req, timeout=120) as res:
                audio = res.read()
        except urllib.error.HTTPError as err:
            detail = err.read().decode("utf-8", "replace")[:300]
            sys.exit(f"{line['id']}: ElevenLabs {err.code}: {detail}")
        if len(audio) < 1000:
            sys.exit(f"{line['id']}: file trả về quá nhỏ, dừng.")
        (OUT / f"{line['id']}.mp3").write_bytes(audio)
        print(f"  {line['id']} ({line['who']}) OK  {len(audio):,} byte")
    print("Xong. Báo Claude để ghép video.")


if __name__ == "__main__":
    main()

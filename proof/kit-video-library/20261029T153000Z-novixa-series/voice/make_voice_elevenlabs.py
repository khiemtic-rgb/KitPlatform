#!/usr/bin/env python3
"""Tạo giọng ElevenLabs cho bộ video NOVIXA. Chạy trên máy của bạn.

Cách dùng (PowerShell):
  $env:ELEVENLABS_API_KEY = "khoá của bạn"      # chỉ gõ trong terminal, không gửi vào chat
  $env:ELEVENLABS_VOICE_ID = "mã giọng bạn chọn"  # giọng hỗ trợ tiếng Việt trong thư viện của bạn
  python make_voice_elevenlabs.py voice_lines.json voice_el
Sau đó nén thư mục voice_el thành zip và gửi lại vào chat.
"""
import json, os, sys, time, urllib.request, urllib.error

lines_path, out_dir = sys.argv[1], sys.argv[2]
import re
key = os.environ.get("ELEVENLABS_API_KEY"); voice = os.environ.get("ELEVENLABS_VOICE_ID")
cfg = os.environ.get("ELEVENLABS_FILE", r"E:\KitPlatform\api\Elevenlab.txt")
if (not key or not voice) and os.path.exists(cfg):
    # đọc khoá và mã giọng từ file cấu hình trên máy bạn; không in giá trị ra màn hình
    txt = open(cfg, encoding="utf-8-sig", errors="ignore").read()
    kv = {}
    for ln in txt.splitlines():
        m = re.match(r"\s*([A-Za-z_ ]+?)\s*[:=]\s*(\S+)", ln)
        if m: kv[m.group(1).strip().lower().replace(" ", "_")] = m.group(2).strip("\"'")
    key = key or next((v for k, v in kv.items() if "key" in k), None) or next(iter(re.findall(r"\b(?:sk_|xi-)\w+", txt)), None)
    voice = voice or next((v for k, v in kv.items() if "voice" in k), None)
    if not voice:
        cand = [w for w in re.findall(r"\b[A-Za-z0-9]{20}\b", txt) if w != key]
        voice = cand[0] if cand else None
    print("Đã đọc cấu hình từ", cfg, "| có khoá:", bool(key), "| có mã giọng:", bool(voice))
voice = voice or "nbv4fVbfyLxvuHzyIeDo"  # giọng đã kiểm thử hiển thị trong Video Studio (mã giọng, không phải khoá)
MODEL = os.environ.get("ELEVENLABS_MODEL", "eleven_v3")  # cùng model với Video Studio
if not key: sys.exit("Thiếu khoá API (biến môi trường ELEVENLABS_API_KEY, hoặc file cấu hình).")
os.makedirs(out_dir, exist_ok=True)
items = json.load(open(lines_path, encoding="utf-8"))
url = f"https://api.elevenlabs.io/v1/text-to-speech/{voice}?output_format=mp3_44100_128"
for n, it in enumerate(items, 1):
    path = os.path.join(out_dir, it["id"] + ".mp3")
    if os.path.exists(path) and os.path.getsize(path) > 1000: print(n, "bỏ qua (đã có)"); continue
    body = json.dumps({"text": it["text"], "model_id": MODEL,
                       "voice_settings": {"stability": 0.5}}).encode()
    req = urllib.request.Request(url, data=body, headers={"xi-api-key": key, "Content-Type": "application/json", "Accept": "audio/mpeg"})
    for attempt in range(3):
        try:
            data = urllib.request.urlopen(req, timeout=90).read(); open(path, "wb").write(data); print(n, "ok", len(data)); break
        except urllib.error.HTTPError as e:
            print(n, "lỗi", e.code, e.read()[:200]);
            if e.code in (401, 403, 404, 422): sys.exit("Dừng: kiểm tra khoá hoặc mã giọng.")
            time.sleep(3)
print("Xong:", out_dir)

#!/usr/bin/env python3
"""NOVIXA series: 5 short clips + 1 long compilation. Piper voice, synthetic music, brand end card."""
import os, sys, json, hashlib, subprocess, wave
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import scenes as SC
from scenes import *

OUT = sys.argv[1] if len(sys.argv) > 1 else "/tmp/s"
SEG = f"{OUT}/seg"; WAV = f"{OUT}/wav"
for d in (OUT, SEG, WAV): os.makedirs(d, exist_ok=True)
MODEL = "/tmp/tts/vits-piper-vi_VN-vais1000-medium/vi_VN-vais1000-medium.onnx"
LENGTH = 0.9
SR = 22050

# ---------------- kịch bản ----------------
# (khóa cảnh, lời đọc). Khóa cảnh = số cảnh có sẵn trong scenes.plan, hoặc "brand".
BRAND_LINE = "Novixa. Nền tảng vận hành nhà thuốc thông minh. Tìm hiểu thêm tại novixa.vn."
CLIPS = {
 "01_tien-di-dau": ("Bán đều mà tiền đi đâu?", [
    (5,  "Hôm nào cũng đông khách, mà cuối tháng nhìn lại, tiền đi đâu hết?"),
    (3,  "Cửa hàng vẫn mở mỗi sáng, khách vẫn ra vào, hàng vẫn bán đều."),
    (6,  "Nhưng mỗi lần nhập hàng, tiền của bạn biến thành những hộp thuốc trên kệ."),
    (9,  "Hộp nào bán nhanh, tiền quay về nhanh."),
    (10, "Hộp nào nằm im trên kệ, tiền của bạn cũng nằm im theo."),
    (11, "Và mỗi ngày trôi qua, hạn dùng lại gần thêm một chút."),
    (13, "Doanh thu vẫn đẹp, nhưng tiền thật đang bị giữ lại trong hàng tồn."),
    (62, "Món nào giúp tiền quay vòng, và món nào đang giữ tiền của bạn lại?"),
 ]),
 "02_ba-loai-hang": ("Ba loại hàng trên kệ", [
    (8,  "Trên kệ nhà thuốc của bạn, có ba loại hàng."),
    (9,  "Loại thứ nhất: bán nhanh. Nhập về là hết, vốn quay vòng liên tục."),
    (10, "Loại thứ hai: bán chậm. Nằm trên kệ nhiều tuần, giữ lại một khoản vốn."),
    (11, "Loại thứ ba: sắp hết hạn. Âm thầm tiến gần ngày phải bỏ đi."),
    (12, "Nhanh thì vốn quay, chậm thì vốn bị giữ, cận hạn thì vốn có nguy cơ mất."),
    (13, "Điều đáng nói: hộp thuốc vẫn còn nguyên, nhưng tiền bên trong thì không còn tự do."),
    (17, "Nhìn kệ của mình lúc này, bạn có biết ngay hộp nào thuộc loại nào không?"),
 ]),
 "03_nhap-them": ("Nhập thêm vì giá tốt?", [
    (23, "Nhà phân phối báo giá tốt. Bạn có nên nhập thêm không?"),
    (24, "Có ưu đãi, nhập nhiều thì rẻ hơn, mặt hàng này trước đây bán chạy."),
    (25, "Nhập thêm một chút cho có lợi. Chủ nhà thuốc nào cũng từng nghĩ vậy."),
    (26, "Nhưng giá nhập rẻ hơn chưa chắc là kinh doanh hiệu quả hơn."),
    (27, "Điều quyết định là hàng đó có bán ra kịp hay không."),
    (28, "Nhập tốt là mua được giá tốt và phù hợp nhu cầu bán thực tế."),
    (22, "Nếu không, một hộp bán chậm sẽ giữ lại cả một khoản tiền."),
    (54, "Lần tới trước khi nhập, bạn có biết món nào giúp vốn quay nhanh không?"),
 ]),
 "04_cham-chi": ("Chăm chỉ mà vẫn chưa thấy", [
    (35, "Bạn có mặt mỗi ngày, theo sát nhân viên, kiểm tra hàng, giải quyết đủ việc phát sinh."),
    (34, "Bạn rất chăm chỉ. Nhưng chăm chỉ chưa chắc nhìn thấy hết vấn đề."),
    (36, "Số liệu nằm rải rác: sổ ghi chép, vài báo cáo, trí nhớ của người này người kia."),
    (40, "Đến cuối tháng kiểm kê mới thấy hàng cận hạn, thì nhiều việc đã khó xử lý hơn."),
    (37, "Làm việc nhiều hơn không có nghĩa là thấy vấn đề sớm hơn."),
    (38, "Bạn cần một cách nhìn rõ hơn, chứ không phải thêm một người bận rộn."),
    (43, "Phát hiện sớm thì còn nhiều cách xử lý. Phát hiện muộn thì lựa chọn ít đi."),
 ]),
 "05_novixa": ("Nhìn thấy sớm cùng Novixa", [
    (44, "Đây là hướng mà Novixa theo đuổi."),
    (45, "Novixa là nền tảng vận hành nhà thuốc thông minh, giúp bạn quản lý mọi hoạt động trên một hệ thống thống nhất."),
    (32, "Số liệu không còn rải rác, mà gom về thành một bức tranh chung."),
    (39, "Bạn nhìn thấy sớm mặt hàng nào sắp hết hạn, ngay trên màn hình."),
    (46, "Doanh số từng ngày được cập nhật, để bạn theo dõi tình hình kinh doanh."),
    (48, "Và biết nhóm hàng nào đang mang lại doanh thu."),
    (50, "Mục tiêu không phải cắt tồn kho bằng mọi giá, mà vẫn đủ hàng cho khách."),
 ]),
}
LONG_ORDER = ["01_tien-di-dau", "02_ba-loai-hang", "03_nhap-them", "04_cham-chi", "05_novixa"]
LONG_TITLE = "Tiền nhà thuốc đang nằm ở đâu? (bản đầy đủ)"
BRAND = ("brand", BRAND_LINE)


# ---------------- cảnh thương hiệu ----------------
def logo_card(width=760):
    from PIL import ImageDraw as _D
    lg = Image.open(os.path.join(SC.SHOTS, "novixa_logo.png")).convert("RGB").crop((165, 243, 1101, 997))
    k = width / lg.width; lg = lg.resize((width, int(lg.height * k)), Image.LANCZOS)
    pad = 36; W, H = lg.width + pad * 2, lg.height + pad * 2
    im = Image.new("RGBA", (W + 40, H + 40), (0, 0, 0, 0)); d = _D.Draw(im)
    d.rounded_rectangle((20, 26, 20 + W, 26 + H), 44, fill=(0, 0, 0, 120))
    from PIL import ImageFilter
    im = im.filter(ImageFilter.GaussianBlur(10))
    d = _D.Draw(im); d.rounded_rectangle((20, 20, 20 + W, 20 + H), 44, fill=(254, 254, 254, 255))
    im.paste(lg, (20 + pad, 20 + pad)); return im


def sc_brand(text):
    els = []
    lc = logo_card(720)
    els.append(El(lc, 540 - lc.width / 2, 130, 0, "pop", .7))
    y = 130 + lc.height + 20
    els.append(tx("Nền tảng vận hành nhà thuốc thông minh", 48, WHITE, 540, y, 860, .9))
    els.append(tx("Tìm hiểu thêm tại", 38, GREY, 540, y + 130, 860, 1.4, bold=False))
    u = tx("novixa.vn", 96, CYAN, 540, y + 185, 900, 1.6, stroke=3); u.kind = "pop"; els.append(u)
    return els


_plan_orig = SC.plan
def plan2(i, t):
    if i == "brand": return sc_brand(t)
    return _plan_orig(i, t)
SC.plan = plan2


# ---------------- giọng đọc ----------------
_voice = None
def spoken(text):
    return (text.replace("novixa.vn", "nô vi xa chấm vê en").replace("Novixa", "Nô vi xa").replace("NOVIXA", "Nô vi xa"))


def vhash(text):
    return hashlib.md5(spoken(text).encode()).hexdigest()[:12]


VOICE_DIR = os.environ.get("VOICE_DIR")  # thư mục chứa <hash>.mp3 (ví dụ từ ElevenLabs)


def synth(text):
    global _voice
    sp = spoken(text)
    if VOICE_DIR:
        f = os.path.join(VOICE_DIR, vhash(text) + ".mp3")
        if not os.path.exists(f): raise SystemExit(f"thiếu file giọng: {f}  ({sp})")
        raw = subprocess.run(["ffmpeg", "-loglevel", "error", "-i", f, "-f", "f32le", "-ac", "1", "-ar", str(SR), "-"], capture_output=True, check=True).stdout
        return np.frombuffer(raw, dtype=np.float32).copy()
    h = hashlib.md5((sp + str(LENGTH)).encode()).hexdigest()[:12]; p = f"{WAV}/{h}.npy"
    if os.path.exists(p): return np.load(p)
    if _voice is None:
        from piper import PiperVoice
        _voice = PiperVoice.load(MODEL)
    from piper import SynthesisConfig
    a = np.concatenate([c.audio_float_array for c in _voice.synthesize(sp, syn_config=SynthesisConfig(length_scale=LENGTH))])
    np.save(p, a); return a


def line_audio(text, last=False):
    a = synth(text)
    tail = 1.8 if last else .32
    return np.concatenate([a, np.zeros(int(SR * tail), dtype=np.float32)])


# ---------------- nhạc nền ----------------
def music(dur, sr=44100):
    n = int(dur * sr); t = np.arange(n) / sr; out = np.zeros(n)
    bpm = 84; beat = 60 / bpm; bar = beat * 4
    chords = [(57, 60, 64), (53, 57, 60), (48, 52, 55), (55, 59, 62)]  # Am F C G
    f = lambda m: 440 * 2 ** ((m - 69) / 12)
    for k in range(int(dur / bar) + 1):
        t0 = k * bar; ch = chords[k % 4]; i0 = int(t0 * sr); i1 = min(n, int((t0 + bar + .6) * sr))
        if i0 >= n: break
        tt = np.arange(i1 - i0) / sr
        env = np.minimum(1, tt / .5) * np.exp(-np.maximum(0, tt - bar) * 4)
        for m in ch:
            out[i0:i1] += .16 * env * (np.sin(2 * np.pi * f(m) * tt) + .35 * np.sin(2 * np.pi * f(m) * 2 * tt))
        out[i0:i1] += .22 * env * np.sin(2 * np.pi * f(ch[0] - 12) * tt)
        for j in range(8):  # arpeggio nhẹ
            s0 = t0 + j * beat / 2; a0 = int(s0 * sr)
            if a0 >= n: break
            a1 = min(n, a0 + int(.55 * sr)); ta = np.arange(a1 - a0) / sr
            m = ch[j % 3] + 12
            out[a0:a1] += .11 * np.exp(-ta * 5) * np.sin(2 * np.pi * f(m) * ta) * (1 if j % 2 == 0 else .7)
    out /= max(1e-6, np.abs(out).max()); fi = int(1.5 * sr); fo = int(2.5 * sr)
    out[:fi] *= np.linspace(0, 1, fi); out[-fo:] *= np.linspace(1, 0, fo)
    return (out * .9).astype(np.float32)


def write_wav(path, a, sr):
    a16 = (np.clip(a, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as w: w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr); w.writeframes(a16.tobytes())


# ---------------- dựng ----------------
def seg_path(key, text, dur):
    h = hashlib.md5(f"{key}|{text}|{dur:.3f}".encode()).hexdigest()[:12]; return f"{SEG}/{h}.mp4"


def build(name, title, items):
    """items: list of (key, text). returns mp4 path"""
    base = board_base(); audios = []; durs = []
    for idx, (key, text) in enumerate(items):
        a = line_audio(text, last=(key == "brand")); audios.append(a); durs.append(len(a) / SR)
    # tỉ lệ khung hình: làm tròn theo FPS để audio và video khớp
    durs = [round(d * FPS) / FPS for d in durs]
    paths = []
    for (key, text), d in zip(items, durs):
        p = seg_path(key, text, d)
        if not os.path.exists(p): SC.render_scene(key, text, d, p, base); print("seg", key, f"{d:.1f}s", flush=True)
        paths.append(p)
    lst = f"{OUT}/{name}.txt"; open(lst, "w").write("".join(f"file '{p}'\n" for p in paths))
    vid = f"{OUT}/{name}_v.mp4"
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", lst, "-c", "copy", vid], check=True)
    # tiếng: giọng + xử lý, nhạc nền
    voice = np.concatenate([np.concatenate([a, np.zeros(0, np.float32)])[:int(round(d * SR))] if len(a) >= int(round(d * SR)) else np.concatenate([a, np.zeros(int(round(d * SR)) - len(a), np.float32)]) for a, d in zip(audios, durs)])
    vw = f"{OUT}/{name}_voice.wav"; write_wav(vw, voice, SR)
    total = len(voice) / SR; mw = f"{OUT}/{name}_music.wav"; write_wav(mw, music(total + 1), 44100)
    final = f"{OUT}/{name}.mp4"
    fc = ("[0:a]highpass=f=70,equalizer=f=3200:t=q:w=1:g=2.5,acompressor=threshold=-20dB:ratio=3:attack=10:release=120,aresample=44100,dynaudnorm=f=250:g=7:p=0.9:m=8,alimiter=limit=0.89,apad=pad_dur=3,asplit=2[v][vs];"
          "[1:a]volume=0.6[m];[m][vs]sidechaincompress=threshold=0.04:ratio=8:attack=20:release=400[d];"
          "[v][d]amix=inputs=2:normalize=0:duration=first,alimiter=limit=0.80:level=disabled[a]")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", vw, "-i", mw, "-i", vid, "-filter_complex", fc, "-map", "2:v", "-map", "[a]", "-c:v", "copy", "-c:a", "aac", "-b:a", "160k", "-t", f"{total:.3f}", final], check=True)
    print("DONE", name, f"{total:.1f}s", flush=True)
    return final, total


def export_lines(path):
    seen = {}
    for name, (title, body) in CLIPS.items():
        for _, txt in body + [BRAND]: seen[vhash(txt)] = spoken(txt)
    json.dump([{"id": k, "text": v} for k, v in seen.items()], open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("lines", len(seen))


if __name__ == "__main__":
    if len(sys.argv) > 2 and sys.argv[2] == "--export-lines": export_lines(sys.argv[3]); sys.exit(0)
    which = sys.argv[2].split(",") if len(sys.argv) > 2 else list(CLIPS) + ["long"]
    manifest = {}
    for name in which:
        if name == "long":
            items = []
            for cn in LONG_ORDER: items += CLIPS[cn][1]
            items.append(BRAND); title = LONG_TITLE
        else:
            title, body = CLIPS[name]; items = list(body) + [BRAND]
        path, total = build(name, title, items)
        manifest[name] = {"title": title, "duration_s": round(total, 1), "lines": [t for _, t in items]}
    json.dump(manifest, open(f"{OUT}/manifest_{'_'.join(which)[:40]}.json", "w"), ensure_ascii=False, indent=1)

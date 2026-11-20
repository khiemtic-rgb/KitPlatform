"""Dựng Shorts tập 02 (1080x1920) từ dòng thời gian video dài.
    python render_short.py <so> <ra.mp4>      (so = 1, 2, 3)
Cả 3 Short: khung ngang của video dài đặt giữa nền mờ, tiêu đề trên, phụ đề lớn dưới.
Cuối mỗi Short: thẻ 4 giây mời xem video đầy đủ (liên kết "Video liên quan" của Shorts)."""
import sys, json, subprocess, re
N = int(sys.argv[1]); OUT = sys.argv[2]
sys.path.insert(0, '/home/claude/v02')
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance
import numpy as np
W, H = 1080, 1920; FPS = 30
TL = json.load(open('/home/claude/v02/timeline.json')); L = TL['lines']
FONTS = '/home/claude/kp/tools/kit-video-library/bta-series/fonts/'
INK = (28, 36, 64); CREAM = (244, 236, 220); VERM = (214, 72, 48)
DISP = lambda s: ImageFont.truetype(FONTS + 'BTA-Display.ttf', s)
BOLD = lambda s: ImageFont.truetype(FONTS + 'BTA-Sans-Bold.ttf', s)
END = 4.0
CFG = {
    1: dict(segs=[(0.0, L[6]['out_e'] + 0.8)], title=('SAME MOON,', 'DIFFERENT CLOCK'),
            end=('SAME MOON.', 'OWN CLOCK.', 'Why Vietnam kept Tet')),
    2: dict(segs=[(L[39]['out_s'] - 0.3, L[40]['out_e'] + 0.4), (L[45]['out_s'] - 0.3, L[49]['out_e'] + 0.8)], title=('THE DEAD', 'COME HOME FOR TET'),
            end=('3 HOLIDAYS', 'IN 1 WEEK', 'Inside a Vietnamese Tet')),
    3: dict(segs=[(L[63]['out_s'] - 0.3, L[65]['out_e'] + 0.8)], title=('THIS FRUIT TRAY', 'IS A PUN'),
            end=('CẦU SUNG', 'VỪA ĐỦ XÀI', 'Pray for just enough to spend')),
}[N]
LOGO = Image.open('/mnt/user-data/uploads/KitPlatform/docs/marketing/bta/nhan-dien/bta-logo-ngang.png').convert('RGBA')
LOGO = LOGO.resize((int(LOGO.width * 130 / LOGO.height), 130), Image.LANCZOS)

import render02 as R
# Bỏ người dẫn của bản ngang (phần lớn bị cắt mép khi đặt vào khung dọc); Shorts dùng người dẫn lớn riêng ở dưới.
for _r in R.ROWS:
    if _r: _r['params']['items'] = [i for i in _r['params']['items'] if i.get('art') != 'host']
sys.path.insert(0, '/home/claude/kp/tools/kit-video-library/bta-series/src')
import bta_art as BA
# Quy tắc từ 14/11/2026: người dẫn to, khung trống có người dẫn. Shorts dọc chừa trống phía dưới, nên đặt người dẫn lớn ở đó.
HS = 580
MOOD = {1: ('curious', 'point'), 2: ('smile', 'point'), 3: ('wink', 'point')}[N]
def put_host(img, T, mood, pose, talk):
    h = BA.host(HS, HS, {'mood': mood, 'pose': pose, 'talk': talk, '_pos': 0 if talk else None, '_t': T})
    h = h if isinstance(h, Image.Image) else Image.fromarray(h)
    img.paste(h.convert('RGBA'), (W // 2 - HS // 2 + 40, 1425), h.convert('RGBA'))

def wrap(d, text, font, maxw):
    rows, cur = [], ''
    for w in text.split():
        t = (cur + ' ' + w).strip()
        if d.textlength(t, font=font) <= maxw or not cur: cur = t
        else: rows.append(cur); cur = w
    rows.append(cur); return rows

CAP = BOLD(58)
def vcaption(img, T):
    hit = [c for c in R.CHUNKS if c[0] <= T < c[2]]
    if not hit: return
    d = ImageDraw.Draw(img, 'RGBA')
    for i, r in enumerate(wrap(d, hit[-1][1], CAP, W * 0.86)):
        tw = d.textlength(r, font=CAP); y = 1250 + i * CAP.size * 1.32
        d.rounded_rectangle((W / 2 - tw / 2 - 22, y - 10, W / 2 + tw / 2 + 22, y + CAP.size * 1.22), 16, fill=INK + (225,))
        d.text((W / 2, y), r, font=CAP, fill=(255, 252, 246), anchor='ma')

_bgcache = {}
def vframe(T, t_local):
    wide = R.frame_nocap(T).convert('RGB')
    key = int(T * 2)
    if key not in _bgcache:
        _bgcache.clear()
        sm = wide.resize((W * 9 // 16 // 4, H // 8)).filter(ImageFilter.GaussianBlur(6))
        _bgcache[key] = ImageEnhance.Brightness(sm.resize((W, H), Image.BICUBIC)).enhance(0.30)
    img = _bgcache[key].copy()
    band = wide.crop((288, 110, 1632, 866)).resize((W, W * 9 // 16), Image.LANCZOS)  # phóng phần giữa khung ngang 1,43 lần cho dễ đọc
    img.paste(band, (0, 600))
    d = ImageDraw.Draw(img, 'RGBA')
    t1, t2 = CFG['title']
    a = min(1, t_local / 0.4)
    f = DISP(78)
    for dx, dy in ((3, 3),):
        d.text((W / 2 + dx, 300 + dy), t1, font=f, fill=(0, 0, 0, int(140 * a)), anchor='ma')
        d.text((W / 2 + dx, 400 + dy), t2, font=f, fill=(0, 0, 0, int(140 * a)), anchor='ma')
    d.text((W / 2, 300), t1, font=f, fill=(255, 250, 240, int(255 * a)), anchor='ma')
    d.text((W / 2, 400), t2, font=f, fill=(236, 96, 70) + (int(255 * a),), anchor='ma')
    talk = any(c[0] <= T < c[2] for c in R.CHUNKS)
    put_host(img, T, MOOD[0], MOOD[1], talk)
    vcaption(img, T)
    return img

def end_card(t):
    img = Image.new('RGB', (W, H), CREAM); d = ImageDraw.Draw(img, 'RGBA')
    a = min(1, t / 0.35)
    img.paste(LOGO, (W // 2 - LOGO.width // 2, 300), LOGO)
    b1, b2, sub = CFG['end']
    f = DISP(130)
    d.text((W / 2, 560), b1, font=f, fill=INK + (int(255 * a),), anchor='ma')
    d.text((W / 2, 720), b2, font=f, fill=VERM + (int(255 * a),), anchor='ma')
    d.text((W / 2, 930), sub, font=BOLD(54), fill=INK + (int(220 * a),), anchor='ma')
    k = min(1, max(0, (t - 0.5) / 0.4))
    pill = 'FULL STORY: TAP THE VIDEO BELOW'
    pf = BOLD(44); tw = d.textlength(pill, font=pf)
    y = 1180 + (1 - k) * 30
    d.rounded_rectangle((W / 2 - tw / 2 - 30, y, W / 2 + tw / 2 + 30, y + 86), 43, fill=VERM + (int(255 * k),))
    d.text((W / 2, y + 20), pill, font=pf, fill=(255, 250, 244, int(255 * k)), anchor='ma')
    # mũi tên chỉ xuống chỗ liên kết video liên quan của Shorts
    ay = 1330 + 18 * abs(np.sin(t * 3.0))
    d.polygon([(W / 2 - 40, ay), (W / 2 + 40, ay), (W / 2, ay + 50)], fill=VERM + (int(255 * k),))
    put_host(img, 100 + t, 'smile', 'wave', False)
    return img

# ---------- danh sách khung ----------
plan = []   # (T_long or None, t_local)
tl = 0.0
for a, b in CFG['segs']:
    for i in range(int(round((b - a) * FPS))):
        plan.append((a + i / FPS, tl)); tl += 1 / FPS
for i in range(int(END * FPS)):
    plan.append((None, i / FPS))
dur = len(plan) / FPS

# ---------- âm thanh: cắt từ bản trộn video dài, hoà 0,25 s chỗ nối; thẻ kết lấy nhạc ở thẻ kết video dài ----------
SR = 48000
def cut(a, b):
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-ss', f'{a}', '-t', f'{b - a}', '-i', '/home/claude/v02/mix02.wav', '-f', 'f32le', '-ac', '2', '-ar', str(SR), '-'], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).reshape(-1, 2).copy()
xf = int(0.25 * SR)
pieces = [cut(a, b) for a, b in CFG['segs']] + [cut(TL['end_card'][0] + 1.0, TL['end_card'][0] + 1.0 + END)]
aud = pieces[0]
for x in pieces[1:]:
    r = np.linspace(1, 0, xf)[:, None]
    aud = np.concatenate([aud[:-xf], aud[-xf:] * r + x[:xf] * (1 - r), x[xf:]])
n = int(dur * SR); aud = np.pad(aud[:n], ((0, max(0, n - len(aud))), (0, 0)))
f = int(0.6 * SR); aud[-f:] *= np.linspace(1, 0, f)[:, None]
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '2', '-i', '-', f'/tmp/s{N}.wav'], input=aud.astype(np.float32).tobytes(), check=True)

p = subprocess.Popen(['ffmpeg', '-y', '-loglevel', 'warning', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{W}x{H}', '-r', str(FPS), '-i', '-',
                      '-i', f'/tmp/s{N}.wav', '-af', 'loudnorm=I=-14:TP=-1.5:LRA=11', '-ar', '48000',
                      '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '20', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-b:a', '192k', '-shortest', OUT], stdin=subprocess.PIPE)
for k, (T, t) in enumerate(plan):
    if T is None:
        im = end_card(t)
        if t < 0.3:
            prev = vframe(plan[k - 1 - int(t * FPS)][0], 9)
            im = Image.blend(prev.convert('RGB'), im, t / 0.3)
    else:
        im = vframe(T, t)
    p.stdin.write(im.convert('RGB').tobytes())
p.stdin.close(); p.wait()
print(OUT, round(dur, 2))

"""Dựng video dài tập 03 (ngang 1920x1080).  python render02.py <tu_giay> <den_giay> <ra.mp4> [fps]
Mọi cảnh dùng bộ dựng nv_board (edu_studio) theo 03-latin-alphabet.hinh.json. Cuối: thẻ kết 12 giây."""
import sys, json, re, subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT = Path('/home/claude/kp')
sys.path.insert(0, str(ROOT / 'tools/kit-video-library/ngv-edu'))
import edu_studio as es
HERE = Path(__file__).resolve().parent
W, H = 1920, 1080
TL = json.load(open(HERE / 'timeline.json'))
L = TL['lines']; SC = TL['scenes']; EC0, TOTAL = TL['end_card']
lines = [o['shown'] for o in L]
ROWS = es.bind(lines, 'wide')
assert ROWS[0] and ROWS[0]['sidecar'].startswith('03-'), 'không khớp file hình tập 03'
BYSC = {}
for i, o in enumerate(L): BYSC.setdefault(o['scene'], []).append(i)
LEAD_IN = 0.25
XF = 0.35

def scene_frame(k, T):
    s = SC[k]; idx = BYSC[s['scene']]
    j = idx[0]
    for i in idx:
        if L[i]['out_s'] - LEAD_IN <= T: j = i
    b = dict(ROWS[j])
    return es.frame(b, T - s['start'], s['end'] - s['start'], max(0.0, T - (L[j]['out_s'] - LEAD_IN)))

FONTS = ROOT / 'tools/kit-video-library/bta-series/fonts'
CAP = ImageFont.truetype(str(FONTS / 'BTA-Sans-Bold.ttf'), 46)
INK = (28, 36, 64); CREAM = (244, 236, 220); VERM = (214, 72, 48)
PUNCT = r'[,.?!:;"”]$|\.\.\.$|…$'
VI = [('quoc ngu', 'quốc ngữ'), ('Quoc ngu', 'Quốc ngữ'), ('chu Nom', 'chữ Nôm'), ('Nom', 'Nôm'), ('Hoi An', 'Hội An'), ('Dac Lo', 'Đắc Lộ'),
      ('Gia Dinh Bao', 'Gia Định báo'), ('Dong Kinh Nghia Thuc', 'Đông Kinh Nghĩa Thục'), ('Hue', 'Huế'),
      ('Kieu', 'Kiều'), ('Nguyen', 'Nguyễn'), ('cam on', 'cảm ơn'), ('Viet', 'Việt'), ('Tet', 'Tết')]
def viet(s):
    import re as _re
    for a, b in VI:
        s = _re.sub(r'(?<![\w\u00C0-\u1EF9])' + _re.escape(a) + r'(?![\w\u00C0-\u1EF9])', b, s)
    return s
CHUNKS = []
for o in L:
    disp = viet(o["shown"]).split()
    nv = len(o['wout']); nd = len(disp)
    groups, cur = [], []
    for wi, w in enumerate(disp):
        cur.append(w)
        nxt = disp[wi + 1] if wi + 1 < len(disp) else ''
        if nxt and re.search(PUNCT, nxt) and len(cur) < 9 and not re.search(PUNCT, w):
            continue
        small = w.lower().strip('"') in ('a', 'an', 'the', 'for', 'to', 'of', 'in', 'on', 'at', 'and', 'or', 'but', 'with', 'from', 'that', 'is', 'its', 'his', 'her', 'their', 'your', 'my', 'this', 'by', 'as')
        if re.search(PUNCT, w) or ((len(cur) >= 7 or len(' '.join(cur)) > 40) and not small):
            groups.append(cur); cur = []
    if cur: groups.append(cur)
    k = 0
    for g in groups:
        vi = min(nv - 1, round(k * nv / nd))
        CHUNKS.append([o['wout'][vi][0] - 0.08, ' '.join(g), o['out_e'] + 0.35]); k += len(g)
for i in range(len(CHUNKS) - 1):
    CHUNKS[i][2] = min(CHUNKS[i][2], CHUNKS[i + 1][0] - 0.02)

def caption(img, T):
    hit = [c for c in CHUNKS if c[0] <= T < c[2]]
    if not hit: return img
    text = hit[-1][1]
    d = ImageDraw.Draw(img, 'RGBA'); rows, cur = [], ''
    for w in text.split():
        t = (cur + ' ' + w).strip()
        if d.textlength(t, font=CAP) <= W * 0.8 or not cur: cur = t
        else: rows.append(cur); cur = w
    rows.append(cur)
    for i, r in enumerate(rows):
        tw = d.textlength(r, font=CAP); y = H * 0.80 + i * CAP.size * 1.35
        d.rounded_rectangle((W / 2 - tw / 2 - 18, y - 8, W / 2 + tw / 2 + 18, y + CAP.size * 1.2), 12, fill=INK + (215,))
        d.text((W / 2, y), r, font=CAP, fill=(255, 252, 246), anchor='ma')
    return img

DISP = ImageFont.truetype(str(FONTS / 'BTA-Display.ttf'), 64)
SUB = ImageFont.truetype(str(FONTS / 'BTA-Sans-Bold.ttf'), 34)
LOGO = Image.open('/mnt/user-data/uploads/KitPlatform/docs/marketing/bta/nhan-dien/bta-logo-ngang.png').convert('RGBA')
LOGO = LOGO.resize((int(LOGO.width * 150 / LOGO.height), 150), Image.LANCZOS)
def end_card(T):
    t = T - EC0
    img = Image.new('RGB', (W, H), CREAM); d = ImageDraw.Draw(img, 'RGBA')
    a = min(1, t / 0.6)
    img.paste(LOGO, (W // 2 - LOGO.width // 2, 70), LOGO)
    d.text((W / 2, 250), 'NEXT EPISODE', font=SUB, fill=VERM + (int(255 * a),), anchor='ma')
    d.text((W / 2, 298), "Why Two in Five Are Named Nguyễn", font=DISP, fill=INK + (int(255 * a),), anchor='ma')
    bx, by, bw, bh = 250, 420, 800, 450
    d.rounded_rectangle((bx, by, bx + bw, by + bh), 18, outline=INK + (90,), width=4)
    d.text((bx + bw / 2, by + bh + 28), 'WATCH NEXT', font=SUB, fill=INK + (160,), anchor='ma')
    cx, cy, r = 1440, by + bh / 2, 190
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=VERM + (140,), width=6)
    d.text((cx, by + bh + 28), 'SUBSCRIBE', font=SUB, fill=INK + (160,), anchor='ma')
    if T > TOTAL - 0.8:
        img = Image.blend(img, Image.new('RGB', (W, H), CREAM), min(1, (T - (TOTAL - 0.8)) / 0.8))
    return img

def frame_nocap(T):
    k = max(i for i, s in enumerate(SC) if s['start'] <= T)
    im = scene_frame(k, T)
    if 0 < k and T - SC[k]['start'] < XF:
        im = Image.blend(scene_frame(k - 1, T), im, (T - SC[k]['start']) / XF)
    return im

def frame(T, f=0):
    if T >= EC0:
        im = end_card(T)
        if T < EC0 + XF:
            im = Image.blend(caption(scene_frame(len(SC) - 1, T), T), im, (T - EC0) / XF)
        return im
    return caption(frame_nocap(T), T)

if __name__ == '__main__':
    a, b, out = float(sys.argv[1]), float(sys.argv[2]), sys.argv[3]
    FPS = int(sys.argv[4]) if len(sys.argv) > 4 else 30
    if out.endswith('.jpg'):
        frame(a).convert('RGB').save(out, quality=85); sys.exit()
    f0, f1 = int(round(a * FPS)), int(round(min(b, TOTAL) * FPS))
    p = subprocess.Popen(['ffmpeg', '-y', '-loglevel', 'warning', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{W}x{H}', '-r', str(FPS), '-i', '-',
                          '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '19', '-pix_fmt', 'yuv420p', '-g', str(FPS * 2), out], stdin=subprocess.PIPE)
    for f in range(f0, f1):
        p.stdin.write(frame(f / FPS, f).convert('RGB').tobytes())
        if (f - f0) % 600 == 0: print(out, f'{(f - f0) / max(1, f1 - f0) * 100:.0f}%', flush=True)
    p.stdin.close(); p.wait()

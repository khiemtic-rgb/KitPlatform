# Shorts khuôn mới (19/11/2026): hình tràn màn hình dọc, giọng đọc từ giây 0,
# phụ đề to theo cụm từ, một ý, 20–30 giây. Dùng lại giọng và ảnh có sẵn, nhạc Suno.
import json, subprocess, sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

W, H, FPS = 1080, 1920, 30
F = '/home/claude/kp/tools/kit-video-library/bta-series/fonts/'
DISP = lambda s: ImageFont.truetype(F + 'BTA-Display.ttf', s)
BOLD = lambda s: ImageFont.truetype(F + 'BTA-Sans-Bold.ttf', s)
INK = (28, 36, 64); CREAM = (244, 236, 220); VERM = (226, 82, 58); GOLD = (240, 196, 92)

_cache = {}
def load(path):
    if path not in _cache:
        _cache[path] = Image.open(path).convert('RGB')
    return _cache[path]

def cover(img, fx=0.5, fy=0.5, zoom=1.0):
    """Cắt ảnh phủ kín 1080x1920 quanh điểm (fx, fy), phóng thêm zoom."""
    iw, ih = img.size
    s = max(W / iw, H / ih) * zoom
    cw, ch = W / s, H / s
    cx = min(max(fx * iw, cw / 2), iw - cw / 2); cy = min(max(fy * ih, ch / 2), ih - ch / 2)
    box = (cx - cw / 2, cy - ch / 2, cx + cw / 2, cy + ch / 2)
    return img.resize((W, H), Image.LANCZOS, box=box)

def ease(x): return 0.5 - 0.5 * math.cos(math.pi * max(0, min(1, x)))

def text_outline(d, xy, txt, font, fill, anchor='mm', stroke=6, sfill=(15, 18, 30)):
    d.text(xy, txt, font=font, fill=fill, anchor=anchor, stroke_width=stroke, stroke_fill=sfill)

def fit_font(mk, txt, maxw, start):
    s = start
    while mk(s).getlength(txt) > maxw and s > 30: s -= 4
    return mk(s)

def chunks_from_line(text, t0, t1, n=3):
    words = text.split(); out = []
    groups = [words[i:i + n] for i in range(0, len(words), n)]
    total = sum(len(' '.join(g)) for g in groups) or 1; t = t0
    for g in groups:
        dur = (t1 - t0) * len(' '.join(g)) / total
        out.append((t, t + dur, ' '.join(g))); t += dur
    return out

def render(spec, out):
    dur = spec['dur']
    caps = []
    for (t0, t1, txt) in spec['lines']:
        caps += chunks_from_line(txt, t0, t1, spec.get('words_per_cap', 3))
    p = subprocess.Popen(['ffmpeg', '-v', 'error', '-y', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{W}x{H}', '-r', str(FPS),
                          '-i', '-', '-i', spec['audio'], '-map', '0:v', '-map', '1:a', '-c:v', 'libx264', '-preset', 'veryfast',
                          '-crf', '18', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-b:a', '192k', '-shortest', '-movflags', '+faststart', out],
                         stdin=subprocess.PIPE)
    n = int(dur * FPS)
    for i in range(n):
        t = i / FPS
        frame = None
        for sc in spec['scenes']:
            if sc['t0'] <= t < sc['t1']:
                frame = sc['draw'](t, (t - sc['t0']) / (sc['t1'] - sc['t0']))
        if frame is None: frame = Image.new('RGB', (W, H), INK)
        d = ImageDraw.Draw(frame)
        for (a, b, txt) in caps:
            if a <= t < b:
                f = fit_font(DISP, txt.upper(), W * 0.88, 92)
                text_outline(d, (W / 2, spec.get('cap_y', 1380)), txt.upper(), f, (255, 255, 255))
        # tên kênh nhỏ, góc trên
        d.text((W / 2, 120), 'BETWEEN TWO ASIAS', font=BOLD(38), fill=(255, 255, 255), anchor='mm', stroke_width=4, stroke_fill=(15, 18, 30))
        p.stdin.write(frame.tobytes())
    p.stdin.close(); p.wait()

def mix_audio(voice, segs, music, out, music_db=-23, fade_out=1.2, music_vol=None):
    """Ghép các đoạn giọng [(start, end, gap_after)], đặt nhạc Suno dưới, chuẩn −14 LUFS."""
    parts = []
    for k, (a, b, gap) in enumerate(segs):
        subprocess.run(['ffmpeg', '-v', 'error', '-y', '-ss', str(a), '-to', str(b), '-i', voice, '-ar', '48000', '-ac', '2',
                        '-af', f'afade=t=in:d=0.02,afade=t=out:st={b - a - 0.04}:d=0.04,apad=pad_dur={gap}', f'/tmp/seg{k}.wav'], check=True)
        parts.append(f'/tmp/seg{k}.wav')
    with open('/tmp/segs.txt', 'w') as fh:
        for p_ in parts: fh.write(f"file '{p_}'\n")
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'concat', '-safe', '0', '-i', '/tmp/segs.txt', '-c', 'pcm_s16le', '/tmp/voice.wav'], check=True)
    vd = float(subprocess.run(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', '/tmp/voice.wav'],
                              capture_output=True, text=True).stdout)
    tot = vd + 0.6
    mv = music_vol or f"volume={music_db}dB"
    fc = (f"[1:a]atrim=0:{tot},{mv},afade=t=out:st={tot - fade_out}:d={fade_out}[m];"
          f"[0:a]apad=pad_dur=0.6[v];[v][m]amix=inputs=2:duration=first:normalize=0,loudnorm=I=-14:TP=-1.5:LRA=11[o]")
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', '/tmp/voice.wav', '-stream_loop', '-1', '-i', music, '-filter_complex', fc,
                    '-map', '[o]', '-ar', '48000', out], check=True)
    return vd, tot

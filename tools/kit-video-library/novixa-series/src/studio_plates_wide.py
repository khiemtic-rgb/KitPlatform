"""Bản ngang 16:9 (1920x1080) của bộ vẽ ảnh Studio. Pure Pillow, không gọi mạng.
Vùng nội dung y 70..820; y>=840 chừa cho phụ đề của Studio."""
import re, random, zlib
from PIL import Image, ImageDraw, ImageFilter
import studio_plates as SP
SC = SP.SC
from scenes import El, at, tx, Cv, card_img
from illus import (person_img, bubble_img, arrow_img, calendar_img, box_img, shelf_img, lock_img, coins_img, mark_img,
                   carton_img, clock_img, mag_img, report_img, textsize, SS)

WW, HH = 1920, 1080
BOARD_W = (60, 60, 1860, 830)
CYAN, GREEN, ORANGE, RED, YEL, PURPLE, GREY, WHITE, INK = SP.CYAN, SP.GREEN, SP.ORANGE, SP.RED, SP.YEL, SP.PURPLE, SP.GREY, SP.WHITE, SP.INK
ACCENTS = SP.ACCENTS


def _fit(t, size, maxw):
    return textsize(t, size, True) <= maxw


def head(text, n=60, size=60, maxw=1300):
    t = re.split(r"[,.;:!?—–]", text.strip())[0].strip() or text.strip()
    if len(t) > n:
        t = t[:n].rsplit(" ", 1)[0] + "…"
    while " " in t and not _fit(t.rstrip("…"), size, maxw):
        t = t.rstrip("…").rsplit(" ", 1)[0] + "…"
    return t[:1].upper() + t[1:]


def sentence(text, n=110):
    t = text.strip().rstrip(".!?")
    if len(t) > n:
        t = t[:n].rsplit(" ", 1)[0] + "…"
    return t[:1].upper() + t[1:]


def title(text, col, size=58):
    for sz in (size, 52, 46, 42):  # chữ dài thì thu nhỏ dần trước khi phải cắt "…"
        full = head(text, 90, sz, 1100)
        if not full.endswith("…") or sz == 42:
            return tx(full, sz, col, 820, 78, 1160)


# ---------------- nền ----------------
_BASES = {}


def _lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def _base(idx):
    th = SP.THEMES[idx % len(SP.THEMES)]
    im = Image.new("RGB", (WW, HH), th["top"]); d = ImageDraw.Draw(im)
    for yy in range(0, HH, 4):
        d.rectangle((0, yy, WW, yy + 4), fill=_lerp(th["top"], th["bot"], min(1.0, yy / 900)))
    x0, y0, x1, y1 = BOARD_W
    pat = Image.new("RGBA", (WW, HH), (0, 0, 0, 0)); pd = ImageDraw.Draw(pat); lc = th["line"] + (34,)
    if th["pat"] == "grid":
        for gx in range(x0 + 60, x1, 90): pd.line((gx, y0 + 14, gx, y1 - 14), fill=lc, width=1)
        for gy in range(y0 + 60, y1, 90): pd.line((x0 + 14, gy, x1 - 14, gy), fill=lc, width=1)
    elif th["pat"] == "dots":
        for gx in range(x0 + 50, x1, 72):
            for gy in range(y0 + 50, y1, 72): pd.ellipse((gx - 3, gy - 3, gx + 3, gy + 3), fill=th["line"] + (46,))
    elif th["pat"] == "diag":
        for k in range(-900, 1900, 100): pd.line((x0 + k, y1, x0 + k + 700, y0), fill=lc, width=2)
    fr = th["frame"]
    if fr == "board":
        sh = Image.new("RGBA", (WW, HH), (0, 0, 0, 0)); ImageDraw.Draw(sh).rounded_rectangle((x0 + 6, y0 + 18, x1 + 6, y1 + 18), radius=34, fill=(0, 0, 0, 150))
        sh = sh.filter(ImageFilter.GaussianBlur(20)); im.paste(sh, (0, 0), sh)
        d = ImageDraw.Draw(im); d.rounded_rectangle(BOARD_W, radius=34, fill=th["board"], outline=th["line"], width=3)
    elif fr == "glass":
        gl = Image.new("RGBA", (WW, HH), (0, 0, 0, 0)); gd = ImageDraw.Draw(gl)
        gd.rounded_rectangle(BOARD_W, radius=44, fill=th["board"] + (150,), outline=th["line"] + (200,), width=2)
        gd.rounded_rectangle((x0 + 60, y1 - 14, x1 - 60, y1 - 6), radius=4, fill=th["line"] + (230,))
        im = Image.alpha_composite(im.convert("RGBA"), gl).convert("RGB")
    else:
        d = ImageDraw.Draw(im); d.rectangle((0, 28, WW, 34), fill=th["line"])
    im = Image.alpha_composite(im.convert("RGBA"), pat).convert("RGB")
    d = ImageDraw.Draw(im); _tf = SC.F(38); d.text((WW - 120 - d.textlength(SP.SB.TAG[0], font=_tf), 74 if fr != "bleed" else 44), SP.SB.TAG[0], font=_tf, fill=th["line"] if th["n"] != "navy" else CYAN)
    d.rectangle((0, 850, WW, HH), fill=th["bot"] if th["n"] == "navy" else _lerp(th["bot"], (0, 0, 0), .55))
    return im


def base_plate_wide(theme=0):
    k = int(theme) % len(SP.THEMES)
    if k not in _BASES:
        _BASES[k] = _base(k)
    return _BASES[k].copy()


# ---------------- tiện ích ----------------
def _p3(rng, k, text):
    return SP._pick3(rng, k, text)


def _ic(name, size, col):
    return SP._icon(name, size, col)


def _card_row(items, y, h, w=None, gap=60, delay0=0.0):
    n = len(items); w = w or int((1700 - gap * (n - 1)) / n); total = n * w + (n - 1) * gap; x0 = (WW - total) // 2
    els = []
    for i, (lab, ic, col) in enumerate(items):
        x = x0 + i * (w + gap)
        els.append(El(card_img(w, h, col), x, y, delay0 + i * .4, "rise"))
        im = _ic(ic, int(min(h * .45, 240)), col)
        els.append(El(im, x + (w - im.width) / 2, y + h * .12, delay0 + .15 + i * .4, "pop", .5, 4))
        els.append(tx(lab, 46, col, x + w / 2, y + h * .70, w - 50, delay0 + .3 + i * .4, gap=1.08))
    return els


# ---------------- các cảnh ngang ----------------
def w_types3(text, rng):
    return [title(text, rng.choice(ACCENTS))] + _card_row([(l, i, c) for l, i, c in _p3(rng, 3, text)], 190, 560)


def w_grid4(text, rng):
    return [title(text, SP.CYAN)] + _card_row([(l, i, c) for l, i, c in _p3(rng, 4, text)], 210, 520, gap=40)


def w_flow3(text, rng):
    t = _p3(rng, 3, text); els = [title(text, CYAN)]
    w, h, gap = 480, 520, 130; x0 = (WW - (3 * w + 2 * gap)) // 2
    for k, (lab, ic, col) in enumerate(t):
        x = x0 + k * (w + gap)
        els.append(El(card_img(w, h, col), x, 200, k * .5, "rise"))
        n = Cv(70, 70); n.ell((2, 2, 68, 68), fill=col); n.text(35, 8, str(k + 1), 44, INK, anchor="m"); els.append(El(n.out(), x + 20, 220, k * .5 + .2, "pop", .4))
        im = _ic(ic, 220, col); els.append(El(im, x + (w - im.width) / 2, 270, k * .5 + .25, "pop", .5, 4))
        els.append(tx(lab, 48, col, x + w / 2, 540, w - 40, k * .5 + .4, gap=1.08))
        if k < 2: els.append(at(arrow_img(100, 24, col), x + w + gap / 2, 430, k * .5 + .6, "wipe_l"))
    return els


def w_steps(text, rng):
    t = _p3(rng, 4, text); els = [tx(rng.choice(["Từng bước", "Quy trình", "Cách làm"]), 58, CYAN, 880, 78, 1360)]
    w, h, gap = 400, 520, 50; x0 = (WW - (4 * w + 3 * gap)) // 2
    for k, (lab, ic, col) in enumerate(t):
        x = x0 + k * (w + gap)
        els.append(El(card_img(w, h, col), x, 200, k * .4, "rise"))
        n = Cv(100, 100); n.ell((3, 3, 97, 97), fill=col); n.text(50, 14, str(k + 1), 62, INK, anchor="m"); els.append(El(n.out(), x + w / 2 - 50, 220, k * .4 + .15, "pop", .4))
        im = _ic(ic, 190, col); els.append(El(im, x + (w - im.width) / 2, 350, k * .4 + .3, "pop", .5, 4))
        els.append(tx(lab, 44, col, x + w / 2, 590, w - 40, k * .4 + .45, gap=1.08))
    return els


def w_compare(text, rng):
    a, b = _p3(rng, 2, text); op = rng.choice(["≠", "→", "vs"]); els = [title(text, CYAN, 52)]
    for k, ((lab, ic, col), x) in enumerate(((a, 110), (b, 1010))):
        els.append(El(card_img(800, 560, col), x, 200, k * .6, "rise"))
        im = _ic(ic, 300, col); els.append(El(im, x + 400 - im.width / 2, 250, .25 + k * .6, "pop", .5, 4))
        els.append(tx(lab, 58, col, x + 400, 610, 700, .4 + k * .6, gap=1.1))
    bd = Cv(140, 140); bd.shadow((10, 110, 130, 140), 8, 110); bd.ell((6, 4, 134, 132), fill=(8, 18, 44), outline=YEL, width=6)
    bd.text(70, 20 if op != "vs" else 36, op, 84 if op != "vs" else 60, YEL, anchor="m"); els.append(El(bd.out(), 890, 400, 1.0, "pop", .45))
    return els


def w_split(text, rng):
    return w_compare(text, rng)


def w_checklist(text, rng):
    c = [SP._short(x, 40) for x in SP._clauses(text)][:4]
    while len(c) < 3: c.append(rng.choice(["Kiểm tra kế hoạch" if SP.SB.TRADING[0] else "Kiểm tra hàng", "Xem báo cáo" if not SP.SB.TRADING[0] else "Xem lại nhật ký lệnh", "Chọn xử lý" if not SP.SB.TRADING[0] else "Chọn mức rủi ro", "Theo dõi mỗi ngày", "Giữ vốn quay" if not SP.SB.TRADING[0] else "Giữ vốn an toàn"]))
    els = [tx(rng.choice(["Việc cần làm", "Ghi nhớ", "Cần chú ý"]), 58, CYAN, 800, 78, 1300)]
    n = len(c); h = int(min(150, (620 - 26 * (n - 1)) / n))
    for i, t in enumerate(c):
        y = 190 + i * (h + 26)
        els.append(El(card_img(1260, h, GREEN), 100, y, .3 + i * .45, "rise")); els.append(El(mark_img("check", 80, GREEN), 130, y + (h - 80) / 2, .5 + i * .45, "pop", .4))
        els.append(tx(t, 44, WHITE, 760, y + h / 2 - 28, 1000, .55 + i * .45, gap=1.1))
    els.append(at(SP.person_v(380, 560, rng.choice(["ok", "think"]), rng), 1600, 200, .5, "rise", .6, 4))
    return els


def w_bars(text, rng):
    n = rng.choice([2, 3, 3]); labs = rng.sample(SP.CHART_LABELS, n); data, real = SP._bar_data(text, rng, labs); n = len(data)
    els = [title(text, rng.choice(ACCENTS), 52), El(card_img(1700, 580, SC.LINEC), 110, 190, 0, "rise")]
    step = 1560 / n; bw = 280
    for i, (l, v, col, vt) in enumerate(data):
        hh = int(340 * min(v, .95)); x = 150 + i * step + (step - bw) / 2
        b = Cv(bw, hh); b.grad((0, 0, bw, hh), SP.shade(col, .15), SP.shade(col, -.2), r=16)
        els.append(El(b.out(), x, 610 - hh, .4 + i * .5, "wipe_b", .9)); els.append(tx(vt, 52, WHITE, x + bw / 2, 610 - hh - 64, 360, .9 + i * .5))
        els.append(tx(l, 44, WHITE, x + bw / 2, 650, 320, .5 + i * .5))
    if not real: els.append(tx("Số liệu minh họa", 28, GREY, 1560, 712, 400, 1.6, bold=False))
    return els


def w_asset(text, rng):
    ic, lab = SP._asset_of(text) or ("btc", "Thị trường")
    if ic == "btc" and re.search(r"ethereum|eth\b", text, re.I): ic = "eth"; lab = "Ethereum"
    elif ic == "btc" and re.search(r"bitcoin|btc", text, re.I): lab = "Bitcoin"
    col = {"btc": SC.ORANGE, "eth": SC.CYAN, "gold": SC.YEL, "fx": SC.CYAN, "stock": SC.GREEN}.get(ic, SC.CYAN)
    im = _ic(ic, 380, col)
    els = [title(text, col, 52), El(card_img(1700, 580, col), 110, 190, 0, "rise"), at(im, 640, 190 + (580 - im.height) / 2 - 20, .3, "pop", .6, 4)]
    els.append(tx(lab, 96, col, 1360, 360, 700, 1.0))
    num = next(iter(SP._NUM.findall(text)), None)
    if num: els.append(tx(num.strip(), 110, WHITE, 1360, 500, 700, 1.4))
    return els


def w_plan(text, rng):
    up = not (re.search(r"giảm|xuống|short|bán", text.lower()) and not re.search(r"tăng|lên|mua", text.lower()))
    els = [title(text, SC.CYAN, 52), El(card_img(1700, 580, SC.CYAN), 110, 190, 0, "rise"), at(SC.chart_big_img(1000, 500, up, True, rng.randint(0, 99999), SC.load_closes()), 660, 232, .3, "wipe_l", 2.2)]
    els.append(tx("Vào · Cắt lỗ · Chốt lời", 46, SC.CYAN, 1480, 330, 440, 1.0))
    num = next(iter(SP._NUM.findall(text)), None)
    if num: els.append(tx(num.strip(), 100, SC.GREEN if up else SC.RED, 1480, 470, 440, 1.4))
    return els


def w_equity(text, rng):
    up = not re.search(r"sụt|drawdown|mất|thua|(?<!\w)lỗ(?!\w)|cháy|giảm", text.lower()); col = SC.GREEN if up else SC.RED
    els = [title(text, col, 52), El(card_img(1700, 580, col), 110, 190, 0, "rise"), at(SC.equity_img(1000, 500, up, "", rng.randint(0, 99999)), 660, 232, .3, "pop", .6)]
    els.append(tx("Đường vốn tăng" if up else "Tài khoản sụt giảm", 46, col, 1480, 330, 440, 1.0))
    num = next(iter(SP._NUM.findall(text)), None)
    if num: els.append(tx(num.strip(), 100, col, 1480, 470, 440, 1.4))
    return els


def _w_panel(name):
    def make(text, rng):
        rx, draw, lab = SP._PANELS[name]; seed = rng.randint(0, 99999); lay = rng.choice(["left", "center", "right"]); col = rng.choice([SC.CYAN, SC.GREEN, SC.ORANGE, SC.YEL])
        if lay == "left":
            els = [title(text, col, 52), El(card_img(1700, 580, col), 110, 190, 0, "rise"), at(draw(1000, 500, text, seed), 660, 232, .3, "pop", .6), tx(lab, 46, col, 1480, 380, 440, 1.0)]
        elif lay == "right":
            els = [title(text, col, 52), El(card_img(1700, 580, col), 110, 190, 0, "rise"), tx(lab, 46, col, 430, 380, 540, 1.0), at(draw(1000, 500, text, seed), 1240, 232, .3, "pop", .6)]
        else:  # bảng rộng chiếm cả khung
            els = [title(text, col, 52), at(draw(1500, 520, text, seed), 880, 175, .3, "pop", .6), tx(lab, 42, col, 880, 715, 900, 1.2)]
        return els
    return make


def w_candle(text, rng):
    low = text.lower(); up = not (re.search(r"giảm|xuống|(?<!\w)lỗ(?!\w)|mất|thua|sập|cháy", low) and not re.search(r"tăng|lên", low)); col = GREEN if up else RED
    els = [title(text, col, 52), El(card_img(1700, 580, col), 110, 190, 0, "rise"), at(SC.candle_img(900, 500, up, rng.randint(0, 99999)), 590, 230, .3, "pop", .6)]
    num = next(iter(SP._NUM.findall(text)), None)
    els.append(tx("Xu hướng tăng" if up else "Xu hướng giảm", 56, col, 1400, 330, 560, 1.0))
    if num: els.append(tx(num.strip(), 120, col, 1400, 450, 560, 1.4))
    return els


def w_stat(text, rng):
    big = next(iter(SP._NUM.findall(text)), None) or next(iter(re.findall(r"\d+[%\w ]{0,6}", text)), None) or "—"; big = big.strip()[:8]
    col = rng.choice(ACCENTS); lab, ic, icol = _p3(rng, 1, text)[0]
    els = [El(card_img(1040, 600, col), 90, 190, 0, "rise", .6), tx(big, 220, col, 610, 230, 960, .3), tx(head(text, 44, 48, 900), 48, WHITE, 610, 520, 900, .7)]
    els.append(El(card_img(640, 600, icol), 1190, 190, .5, "rise")); im = _ic(ic, 280, icol)
    els.append(El(im, 1190 + (640 - im.width) / 2, 250, .8, "pop", .5, 4)); els.append(tx(lab, 52, icol, 1510, 610, 560, 1.0))
    return els


def w_hub(text, rng):
    t = _p3(rng, 4, text); els = [title(text, CYAN, 52)]
    hub = Cv(300, 300); hub.shadow((20, 250, 280, 300), 10, 120); hub.ell((10, 6, 290, 286), fill=(8, 22, 52), outline=CYAN, width=8)
    c = SP._clauses(text); hub.text(150, 100, SP._short(c[0], 12) if c else SP.SB.HUB[0], 40, CYAN, anchor="m", maxw=250, gap=1.1)
    els.append(El(hub.out(), 810, 300, .3, "pop", .6))
    for k, ((lab, ic, col), (x, y)) in enumerate(zip(t, [(110, 190), (110, 500), (1290, 190), (1290, 500)])):
        els.append(El(card_img(520, 270, col), x, y, .6 + k * .3, "rise")); im = _ic(ic, 130, col)
        els.append(El(im, x + 40, y + (270 - im.height) / 2 - 8, .8 + k * .3, "pop")); els.append(tx(lab, 42, col, x + 360, y + 90, 250, .9 + k * .3, gap=1.08))
    return els


def w_donut(text, rng):
    m = re.search(r"(\d{1,3})\s*%", text); p = min(1.0, int(m.group(1)) / 100) if m else rng.choice([.25, .5, .75])
    col = rng.choice(ACCENTS); other = rng.choice([c for c in ACCENTS if c != col])
    cv = Cv(560, 560); cv.arc((30, 30, 530, 530), 0, 360, (40, 74, 120), 70); cv.arc((30, 30, 530, 530), -90, -90 + 360 * p, col, 70); cv.text(280, 205, "%d%%" % int(p * 100), 130, WHITE, anchor="m")
    els = [title(text, col, 52), El(cv.out(), 160, 220, .2, "pop", .7)]
    a, b = _p3(rng, 2, text)
    for k, ((lab, ic, c2), cc) in enumerate(((a, col), (b, other))):
        y = 230 + k * 290; els.append(El(card_img(900, 260, cc), 860, y, .8 + k * .4, "rise")); im = _ic(ic, 150, c2)
        els.append(El(im, 900, y + (260 - im.height) / 2 - 6, 1.0 + k * .4, "pop")); els.append(tx(lab, 54, cc, 1330, y + 90, 520, 1.1 + k * .4))
    return els


def w_hero(text, rng):
    lab, ic, col = _p3(rng, 1, text)[0]; c = SP._clauses(text)
    disc = Cv(560, 560); disc.ell((0, 0, 560, 560), fill=(14, 40, 84), outline=col, width=6)
    els = [El(disc.out(), 170, 200, 0, "pop", .6), at(_ic(ic, 360, col), 450, 300, .3, "pop", .6, 5)]
    els.append(tx(head(text, 46, 70, 900), 70, WHITE, 1230, 300, 900, .6, gap=1.18))
    if len(c) > 1: els.append(tx(SP._short(c[1], 60), 44, GREY, 1230, 560, 860, 1.0, bold=False))
    return els


def w_quote(text, rng):
    els = [El(bubble_img(1220, 520, "bl"), 90, 170, 0, "pop", .6), tx("“" + sentence(text, 100) + "”", 56, INK, 700, 230, 1060, .4, gap=1.2)]
    els.append(at(SP.person_v(400, 600, rng.choice(["worry", "think"]), rng), 1600, 200, .6, "rise", .6, 4))
    els.append(El(mark_img("q", 100, YEL), 1400, 220, 1.0, "pop", .5, 6))
    return els


def w_typo(text, rng):
    col = rng.choice(ACCENTS); lab, ic, icol = _p3(rng, 1, text)[0]
    els = [El(card_img(1700, 600, col), 110, 190, 0, "rise", .6)]
    q = Cv(160, 160); q.text(80, -10, "“", 220, col, anchor="m"); els.append(El(q.out(), 150, 205, .2, "pop", .5))
    els.append(tx(sentence(text, 100), 72, WHITE, 960, 320, 1450, .5, gap=1.2))
    els.append(El(_ic(ic, 130, icol), 1620, 560, 1.0, "pop", .5, 4)); els.append(tx(lab, 44, icol, 1380, 590, 420, 1.1))
    return els


def w_think(text, rng):
    lab, ic, icol = _p3(rng, 1, text)[0]
    els = [at(SP.person_v(420, 620, rng.choice(["worry", "think", "ok"]), rng), 380, 190, .3, "rise", .6, 4), El(bubble_img(1000, 420, "bl"), 740, 190, 0, "pop", .6)]
    els.append(at(_ic(ic, 200, icol), 1240, 215, .5, "pop", .5, 4)); els.append(tx(lab, 46, INK, 1240, 440, 800, .8))
    els.append(tx(head(text, 40, 48, 1000), 48, WHITE, 1240, 650, 1000, 1.0))
    return els


def w_iconrow(text, rng):
    t = _p3(rng, 3, text); els = [title(text, CYAN, 52)]; xs = (360, 960, 1560)
    for k, ((lab, ic, col), x) in enumerate(zip(t, xs)):
        im = _ic(ic, 250, col); els.append(at(im, x, 200 + (250 - im.height) / 2, .3 + k * .5, "pop", .5, 4)); els.append(tx(lab, 46, col, x, 480, 420, .5 + k * .5, gap=1.08))
        if k < 2: els.append(at(arrow_img(120, 22, GREY), (xs[k] + xs[k + 1]) / 2, 300, .6 + k * .5, "wipe_l"))
    els.append(El(card_img(1500, 190, rng.choice(ACCENTS)), 210, 600, 1.8, "rise")); els.append(tx(sentence(text, 100), 46, WHITE, 960, 660, 1350, 2.0, gap=1.15))
    return els


def w_shelf(text, rng):
    col = rng.choice(ACCENTS); els = [title(text, col)]
    els.append(El(shelf_img(1560, 620, 2), 180, 180, .1, "rise"))
    mode = rng.choice(["dim", "check", "q", "lock", "hsd", "plain"]); idx = list(range(12)); rng.shuffle(idx); chosen = set(idx[:rng.randint(2, 4)])
    cols = [CYAN, ORANGE, GREEN, PURPLE]
    for i in range(12):
        r, cc = divmod(i, 6); line_y = 180 + 620 * (.02 + (r + 1) * .96 / 2) - 16; bw, bh = 188, 224
        x = 180 + 40 + cc * 250 + (250 - bw) / 2 - 8; y = line_y - bh + 6
        dim = mode == "dim" and i not in chosen; red = mode == "hsd" and i in chosen
        els.append(El(box_img(bw, bh, cols[i % 4], "Thuốc " + "ABCDEFGHIJKL"[i], "HSD 11/2026" if red else "HSD 12/2027", red, dim), x, y, .3 + i * .07, "pop", .45))
        if i in chosen and mode in ("check", "q", "lock"):
            m = {"q": mark_img("q", 80, YEL), "lock": lock_img(80), "check": mark_img("check", 80, GREEN)}[mode]
            els.append(El(m, x + bw - 50, y - 18, .3 + i * .07 + .35, "pop", .4))
    return els


def w_expiry(text, rng):
    els = [title(text, RED), El(calendar_img(380, 410, "30", "HSD", "Tháng 11"), 330, 190, .2, "pop", .55, 4), El(box_img(330, 400, RED, "Thuốc A", "HSD 11/2026", True), 1000, 200, .5, "pop", .5, 5)]
    els.append(El(card_img(1400, 110, ORANGE), 260, 640, .9, "rise"))
    bar = Cv(1300, 46); bar.rect((0, 0, 1300, 46), fill=(8, 18, 44), r=23); bar.grad((0, 0, 1000, 46), ORANGE, RED, r=23, horiz=True); els.append(El(bar.out(), 310, 672, 1.0, "wipe_l", 1.0))
    els.append(tx("Ngày nhập", 36, GREY, 400, 740, 300, 1.1, bold=False)); els.append(tx("Hết hạn", 36, RED, 1520, 740, 300, 1.1))
    return els


def w_locked(text, rng):
    c = SP._clauses(text)
    els = [El(box_img(380, 450, CYAN, "Thuốc A", "HSD 12/2027"), 250, 170, .1, "pop", .6, 5), El(lock_img(240), 740, 200, 1.0, "pop"), El(coins_img(5, 62), 1080, 400, 1.3, "pop", .5)]
    els.append(tx(head(text, 60, 64, 1650), 64, WHITE, 960, 660, 1650, .9, gap=1.15))
    if len(c) > 1: els.append(tx(SP._short(c[1], 70), 44, GREY, 960, 770, 1500, 1.3, bold=False))
    return els


GEN_W = {
    "shelf": w_shelf, "types3": w_types3, "grid4": w_grid4, "flow3": w_flow3, "steps": w_steps, "compare": w_compare, "split": w_split,
    "checklist": w_checklist, "bars": w_bars, "stat": w_stat, "hub": w_hub, "donut": w_donut, "hero": w_hero, "quote": w_quote,
    "typo": w_typo, "think": w_think, "iconrow": w_iconrow, "expiry": w_expiry, "locked": w_locked, "candle": w_candle, "asset": w_asset, "plan": w_plan, "equity": w_equity,
}
def w_hook(text, rng):
    import illus
    col = rng.choice([SC.CYAN, SC.ORANGE, SC.YEL])
    chip = Cv(360, 80); chip.rect((0, 0, 360, 80), fill=SC.shade(col, -.55), outline=col, width=4, r=40); chip.text(180, 14, "SỰ THẬT" if "?" not in text else "BẠN CÓ BIẾT", 40, col, anchor="m")
    els = [El(chip.out(), 120, 120, 0, "pop", .5), tx(text.strip(), 78, SC.WHITE, 780, 250, 1300, .3, gap=1.2)]
    els.append(at(illus.trader_img(380, 540, "think" if "?" in text else "ok", rng.randint(0, 1), "point"), 1640, 250, .6, "rise", .6, 4))
    return els

GEN_W["hook"] = w_hook

for _n in SP._PANELS:
    GEN_W["p_" + _n] = _w_panel(_n)
EXTRA = {"pharmacy": ("expiry", "locked"), "goods": ("locked",), "data": (), "chart": (), "contrast": ("expiry",), "process": (), "interface": ()}


def generics_w(layout, text):
    fam = layout.split("_")[0]
    names = tuple(SP.FAMILY_GENERIC.get(fam, ("hero", "types3", "quote"))) + EXTRA.get(fam, ())
    if SP.SB.TRADING[0]:
        front = []
        if SP._HOOK_RX.search(text) and len(text) < 140: front.append("hook")
        if SP._asset_of(text): front.append("asset")
        if SP._PLAN_RX.search(text): front.append("plan")
        if SP._EQ_RX.search(text): front.append("equity")
        front += ["p_" + n for n in SP._panels_of(text)]
        if re.search(r"nến|xu hướng|biểu đồ|giá |đường giá|đỉnh|đáy", text.lower()): front.append("candle")
        names = tuple(front) + tuple(n for n in names if n not in ("shelf", "expiry", "locked"))
    has_num = bool(re.search(r"\d", text)); names = tuple(n for n in names if has_num or n not in (("stat", "donut", "bars") if SP.SB.TRADING[0] else ("stat", "donut")))
    if "expiry" in names and not re.search(r"hạn|hsd", text.lower()): names = tuple(n for n in names if n != "expiry")
    if "locked" in names and not re.search(r"tiền|vốn|giữ|nhập|hộp|hàng", text.lower()): names = tuple(n for n in names if n != "locked")
    out = []
    for r in range(4):
        for nm in names:
            seed = (r + 1) * 31 + len(out)
            out.append(("g-%s-%s-%d" % (layout, nm, r), (lambda nm=nm, seed=seed: GEN_W[nm](text, SP._rng(layout + nm, text, seed)))))
    return out

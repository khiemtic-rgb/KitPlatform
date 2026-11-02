"""64 animated scenes. python scenes.py still OUT_DIR    -> final-state PNG per scene
                      python scenes.py video JOB TL WAV OUT.mp4"""
import json, math, os, sys, subprocess, re
from illus import *
from illus import _dummy
from PIL import Image, ImageDraw, ImageFilter

FPS = 30
BOARD = (40, 100, 1040, 1330)
CAPBOX = (40, 1360, 1040, 1880)
LINEC = (40, 120, 190)


class El:
    def __init__(s, img, x, y, delay=0.0, kind="rise", dur=0.5, bob=0.0):
        s.img, s.x, s.y, s.delay, s.kind, s.dur, s.bob = img, x, y, delay, kind, dur, bob


def at(img, cx, top, delay=0.0, kind="rise", dur=.5, bob=0.0):
    return El(img, cx - img.width / 2, top, delay, kind, dur, bob)


def tx(text, size, color, cx, top, maxw=900, delay=0.0, bold=True, stroke=0, gap=1.16, kind="rise"):
    f = F(size * SS, bold); lines = wrap(text, f, maxw * SS); hh = int(len(lines) * size * gap + size * .35); w = int(maxw + 24)
    cv = Cv(w, hh); cv.text(w / 2, 0, text, size, color, bold, "m", maxw, gap, stroke, (4, 10, 28) if stroke else None)
    e = El(cv.out(), cx - w / 2, top, delay, kind); e.h = hh; return e


_CNT = {}
def count_el(target, size, color, cx, top, maxw=820, delay=0.0, dur=1.4, prefix="", suffix="", dec=0):
    """Số lớn đếm từ 0 lên target (dùng cho cảnh mở đầu / thống kê)."""
    fin = tx(f"{prefix}{target:,.{dec}f}{suffix}".replace(",", "."), size, color, cx, top, maxw, delay)
    e = El(fin.img, fin.x, fin.y, delay, "count", dur); e.h = fin.h
    e.spec = (target, size, color, cx, top, maxw, prefix, suffix, dec); return e


def _count_img(e, v):
    target, size, color, cx, top, maxw, prefix, suffix, dec = e.spec
    key = (round(v, dec), e.spec)
    if key not in _CNT:
        if len(_CNT) > 400: _CNT.clear()
        _CNT[key] = tx(f"{prefix}{v:,.{dec}f}{suffix}".replace(",", "."), size, color, cx, top, maxw).img
    return _CNT[key]


def height_of(e): return e.img.height


def board_base():
    im = Image.new("RGB", (W, H), NAVY); d = ImageDraw.Draw(im)
    for yy in range(0, H, 4):
        t = yy / H; d.rectangle((0, yy, W, yy + 4), fill=(int(6 + 10 * t), int(18 + 24 * (1 - abs(t - .3))), int(44 + 34 * (1 - t))))
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0)); ImageDraw.Draw(sh).rounded_rectangle((BOARD[0] + 6, BOARD[1] + 18, BOARD[2] + 6, BOARD[3] + 18), radius=34, fill=(0, 0, 0, 160))
    sh = sh.filter(ImageFilter.GaussianBlur(20)); im.paste(sh, (0, 0), sh)
    d = ImageDraw.Draw(im); d.rounded_rectangle(BOARD, radius=34, fill=(15, 36, 74), outline=LINEC, width=3)
    for gx in range(BOARD[0] + 50, BOARD[2], 80): d.line((gx, BOARD[1] + 14, gx, BOARD[3] - 14), fill=(20, 46, 90), width=1)
    for gy in range(BOARD[1] + 50, BOARD[3], 80): d.line((BOARD[0] + 14, gy, BOARD[2] - 14, gy), fill=(20, 46, 90), width=1)
    d.text((W - 270, 40), "NOVIXA", font=F(40), fill=CYAN)
    d.rounded_rectangle(CAPBOX, radius=30, fill=(8, 20, 46))
    return im


# ---------- building blocks ----------
def title_lab(text, color, top=130, size=62, delay=0.0, maxw=900):
    return tx(text, size, color, 540, top, maxw, delay)


def sublab(text, color=WHITE, top=1180, size=52, delay=.8, maxw=900):
    return tx(text, size, color, 540, top, maxw, delay, bold=False)


def flow_cards(items, top=135, h=None, gap=44, delay0=0.0, arrows=True, colors=None):
    n = len(items); h = h or int((1190 - gap * (n - 1)) / n); els = []
    for i, (label, sub, icon, color) in enumerate(items):
        y = top + i * (h + gap); d0 = delay0 + i * .45
        els.append(El(card_img(900, h, color), 90, y, d0, "rise"))
        ih = min(icon.height, h - 50); sc = ih / icon.height
        ic = icon if sc >= .999 else icon.resize((max(1, int(icon.width * sc)), int(ih)), Image.LANCZOS)
        els.append(El(ic, 90 + 40 + (230 - ic.width) / 2, y + (h - ic.height) / 2, d0 + .15, "pop", .5))
        ty = y + h / 2 - (36 if sub else 28)
        t1 = tx(label, 54, color, 640, ty - (34 if sub else 0), 560, d0 + .25, gap=1.1); t1.x = 380; els.append(t1)
        if sub:
            t2 = tx(sub, 40, WHITE, 640, ty + 54, 560, d0 + .4, bold=False); t2.x = 380; els.append(t2)
        if arrows and i < n - 1:
            els.append(at(arrow_img(gap + 4, 18, color, True).resize((int(40), int(gap + 4)), Image.LANCZOS), 540, y + h - 2, d0 + .5, "fade", .3))
    return els


def shelf_scene(title, color, n=9, dim=(), marks=None, strip=None, delay=0.0, boxcols=None):
    els = [title_lab(title, color, 124, 56, 0)]
    sh = shelf_img(900, 820, 3); els.append(El(sh, 90, 200, .1, "rise"))
    cols = [CYAN, ORANGE, GREEN, PURPLE]
    for i in range(n):
        r, c = divmod(i, 3); line_y = 200 + 820 * (.02 + (r + 1) * .96 / 3) - 16
        bw, bh = 188, 224; x = 90 + 28 + c * 292 + (264 - bw) / 2; y = line_y - bh + 6
        d = i in dim; red = i in (marks or {}) and marks[i] == "hsd"
        img = box_img(bw, bh, cols[i % 4], "Thuốc " + "ABCDEFGHI"[i % 9], "HSD 11/2026" if red else "HSD 12/2027", red, d)
        els.append(El(img, x, y, .3 + i * .1, "pop", .45))
        if marks and i in marks and marks[i] in ("q", "lock", "check"):
            m = {"q": mark_img("q", 84, YEL), "lock": lock_img(84), "check": mark_img("check", 84, GREEN)}[marks[i]]
            els.append(El(m, x + bw - 50, y - 18, .3 + i * .1 + .35, "pop", .4))
    if strip:
        kind, txt, col = strip
        els.append(El(card_img(940, 190, col), 70, 1128, 1.3, "rise"))
        if kind == "coins":
            els.append(El(coins_img(5, 36), 100, 1160, 1.45, "pop")); els.append(El(bill_img(190, 95), 300, 1176, 1.55, "pop"))
        else:
            els.append(El(wallet_img(210, 150, True), 110, 1148, 1.45, "pop"))
        t = tx(txt, 42, col, 770, 1172, 400, 1.5, gap=1.12); els.append(t)
    return els


def sc_title(t, sub=None):
    els = [El(store_img(900, 700), 90, 300, 0, "rise", .7), at(tx("NOVIXA", 120, CYAN, 540, 130, 900, .2).img, 540, 130, .2, "pop", .6)]
    els.append(tx(t, 62, WHITE, 540, 1050, 880, .6));
    if sub: els.append(tx(sub, 46, GREY, 540, 1220, 880, .9, bold=False))
    return els


def sc_day():
    els = [at(store_img(640, 500), 540, 130, 0, "rise", .6)]
    els.append(El(clock_img(120, 6, 30, YEL), 130, 700, .5, "pop")); els.append(El(clock_img(120, 10, 0, PURPLE), 690, 700, .8, "pop"))
    els.append(at(arrow_img(220, 22, GREY), 540, 810, 1.0, "wipe_l"))
    els.append(tx("Mở cửa từ rất sớm", 48, YEL, 255, 980, 360, .9)); els.append(tx("Đóng cửa khá muộn", 48, PURPLE, 825, 980, 360, 1.1))
    els.append(El(person_img(200, 290, "think"), 440, 1040, 1.2, "rise", .5, 4))
    return els


def sc_flow3(items, color=CYAN):
    return flow_cards(items)


def sc_question(text, mood="worry"):
    b = bubble_img(900, 460); els = [El(b, 90, 150, 0, "pop", .6)]
    t = tx(text, 60, INK, 540, 220, 780, .3, gap=1.2); els.append(t)
    els.append(at(person_img(380, 560, mood), 540, 720, .7, "rise", .6, 4))
    els.append(El(mark_img("q", 120, YEL), 140, 800, 1.0, "pop", .5, 6)); els.append(El(mark_img("q", 90, CYAN), 840, 880, 1.2, "pop", .5, 6))
    return els


def sc_places():
    els = [title_lab("Tiền nằm trong hộp thuốc", YEL, 124, 58)]
    for k, (name, col) in enumerate((("Kệ", CYAN), ("Tủ", ORANGE), ("Kho", GREEN))):
        x = 60 + k * 322
        els.append(El(card_img(300, 800, col), x, 230, .2 + k * .4, "rise"))
        els.append(tx(name, 64, col, x + 150, 250, 280, .3 + k * .4))
        if k == 0:
            els.append(El(shelf_img(250, 520, 3), x + 25, 360, .5, "rise"))
            for j in range(3): els.append(El(box_img(96, 116, [CYAN, ORANGE, GREEN][j], "A", "", False, False), x + 50 + (j % 2) * 100, 395 + j * 160, .7 + j * .15, "pop"))
        elif k == 1:
            els.append(El(cabinet_img(230, 560), x + 35, 360, .9, "rise"))
            for j in range(2): els.append(El(box_img(90, 110, [PURPLE, CYAN][j], "B", "", False), x + 110, 410 + j * 190, 1.2 + j * .15, "pop"))
        else:
            for j in range(3): els.append(El(carton_img(250, 180), x + 25, 380 + j * 190, 1.3 + j * .15, "pop"))
    els.append(El(coins_img(6, 44), 120, 1120, 1.8, "pop")); els.append(El(bill_img(300, 150), 420, 1130, 1.9, "pop")); els.append(El(coins_img(4, 44), 800, 1130, 2.0, "pop"))
    return els


def sc_types(items):
    return flow_cards(items, gap=40)


def sc_speed(mode):
    if mode == "fast":
        els = [title_lab("Bán nhanh", GREEN, 128, 88)]
        els.append(El(person_img(300, 430, "ok"), 60, 330, .2, "rise")); els.append(El(box_img(260, 310, GREEN, "Thuốc A", "HSD 12/2027"), 400, 440, .5, "pop", .5, 6))
        els.append(El(arrow_img(180, 26, GREEN), 680, 560, .9, "wipe_l")); els.append(El(coins_img(6, 40), 780, 570, 1.2, "pop")); els.append(El(bill_img(250, 125), 760, 460, 1.3, "pop"))
        els.append(tx("Nhập về là bán hết", 56, WHITE, 540, 900, 880, 1.0)); els.append(tx("Tiền quay lại ngay để nhập lô tiếp theo", 44, GREY, 540, 1040, 880, 1.2, bold=False))
        els.append(El(carton_img(220, 160), 90, 1120, 1.4, "pop")); els.append(El(arrow_img(120, 20, CYAN), 340, 1160, 1.6, "wipe_l")); els.append(El(box_img(130, 160, GREEN, "A", ""), 480, 1100, 1.8, "pop")); els.append(El(coins_img(4, 36), 680, 1160, 2.0, "pop"))
    else:
        els = [title_lab("Bán chậm", ORANGE, 128, 88)]
        els.append(El(box_img(290, 340, ORANGE, "Thuốc A", "HSD 12/2027", False, True), 90, 430, .3, "pop")); els.append(El(clock_img(150, 3, 0, ORANGE), 560, 410, .6, "pop", .5, 5))
        els.append(El(lock_img(150), 330, 360, 1.0, "pop"))
        els.append(tx("Nằm trên kệ hàng tháng", 56, WHITE, 540, 900 - 40, 880, 1.0)); els.append(tx("Tiền bị giữ lại, chưa quay vòng được", 44, GREY, 540, 1040, 880, 1.2, bold=False))
        els.append(El(coins_img(5, 40), 120, 1130, 1.5, "pop")); els.append(El(lock_img(80), 330, 1130, 1.7, "pop"))
    return els


def sc_expiry(txt, prog, day="30", month="Tháng 11", stamp="HSD", title="Hạn sử dụng đến gần"):
    els = [title_lab(title, RED, 124, 60)]
    els.append(El(calendar_img(400, 430, day, stamp, month), 80, 250, .2, "pop", .55, 4)); els.append(El(box_img(330, 390, RED, "Thuốc A", "HSD " + month[-2:] + "/2026", True), 590, 290, .5, "pop", .5, 5))
    els.append(El(card_img(900, 120, ORANGE if prog < .9 else RED), 90, 790, .9, "rise"))
    bar = Cv(820, 50); bar.rect((0, 0, 820, 50), fill=(8, 18, 44), r=25); bar.grad((0, 0, max(60, 820 * prog), 50), ORANGE if prog < .9 else (255, 120, 90), RED, r=25, horiz=True)
    els.append(El(bar.out(), 130, 826, 1.0, "wipe_l", 1.0))
    els.append(tx("Ngày nhập", 40, GREY, 250, 925, 300, 1.1, bold=False)); els.append(tx("Hết hạn", 40, RED, 840, 925, 300, 1.1))
    els.append(tx(txt, 72, YEL, 540, 1010, 900, 1.3)); els.append(El(clock_img(70, 4, 0, ORANGE), 150, 1160, 1.5, "pop")); els.append(El(clock_img(70, 8, 0, RED), 810, 1160, 1.6, "pop"))
    return els


def sc_locked(text, sub=None):
    els = [El(box_img(440, 520, CYAN, "Thuốc A", "HSD 12/2027"), 100, 190, .1, "pop", .6, 5), at(coin_img(120), 330, 460, .8, "pop", .5, 6)]
    els.append(El(lock_img(240), 590, 230, 1.1, "pop")); els.append(El(coins_img(5, 60), 640, 520, 1.3, "pop", .5))
    els.append(tx(text, 58, WHITE, 540, 860, 900, .9, gap=1.18))
    if sub: els.append(tx(sub, 46, GREY, 540, 1110, 900, 1.3, bold=False))
    return els


def sc_cycle(broken=False, labels=("Nhập hàng", "Bán nhanh", "Tiền về")):
    cx, cy, R = 540, 700, 330
    nodes = [(cx, cy - R + 20), (cx + R * .95, cy + R * .6), (cx - R * .95, cy + R * .6)]
    arrows = Cv(1080, 1400)
    for a, b in ((0, 1), (1, 2), (2, 0)):
        (x1, y1), (x2, y2) = nodes[a], nodes[b]; dx, dy = x2 - x1, y2 - y1; L = math.hypot(dx, dy); ux, uy = dx / L, dy / L
        s_, e_ = (x1 + ux * 150, y1 + uy * 150), (x2 - ux * 160, y2 - uy * 160)
        col = RED if (broken and a == 1) else GREEN
        arrows.line([s_, e_], col, 14); px, py = -uy, ux
        arrows.poly([(e_[0] + ux * 36, e_[1] + uy * 36), (e_[0] + px * 24, e_[1] + py * 24), (e_[0] - px * 24, e_[1] - py * 24)], fill=col)
    els = [El(arrows.out(), 0, 0, .9, "fade", .6)]
    icons = [carton_img(180, 130), box_img(120, 150, GREEN, "A", ""), coins_img(4, 36)]
    cols = [CYAN, GREEN if not broken else ORANGE, YEL]
    for k, (nx, ny) in enumerate(nodes):
        nd = Cv(300, 300); nd.shadow((20, 250, 280, 300), 10, 120); nd.ell((10, 6, 290, 286), fill=(10, 24, 52), outline=cols[k], width=6)
        els.append(El(nd.out(), nx - 150, ny - 150, .2 + k * .35, "pop", .5))
        ic = icons[k]; els.append(El(ic, nx - ic.width / 2, ny - ic.height / 2 - 58, .4 + k * .35, "pop", .5))
        els.append(tx(labels[k] if not (broken and k == 1) else "Nằm trên kệ", 40, cols[k], nx, ny + 24, 250, .5 + k * .35, gap=1.05))
    if broken: els.append(El(lock_img(120), nodes[1][0] + 40, nodes[1][1] - 150, 1.4, "pop"))
    els.append(tx("Vốn quay vòng" if not broken else "Tiền bị giữ lại, vòng quay chậm lại", 54, GREEN if not broken else RED, 540, 1190, 900, 1.5))
    return els


def sc_options():
    return flow_cards([("Đẩy bán", "Dồn lực bán trước khi hết hạn", megaphone_img(200), CYAN), ("Giảm giá", "Chấp nhận lợi nhuận thấp hơn", tag_img(220, "-30%"), ORANGE), ("Chấp nhận tổn thất", "Nếu không kịp bán", cracked_coin_img(70), RED)], gap=40)


def sc_compare(l, r, op="≠", lic=None, ric=None, lcol=ORANGE, rcol=GREEN, lsub=None, rsub=None):
    els = []
    for k, (title, sub, ic, col) in enumerate(((l, lsub, lic, lcol), (r, rsub, ric, rcol))):
        y = 140 + k * 600; h = 520
        els.append(El(card_img(900, h, col), 90, y, k * .8, "rise"))
        if ic is not None: els.append(El(ic, 90 + 40 + (330 - ic.width) / 2 if ic.width < 330 else 120, y + (h - ic.height) / 2 - 10, .25 + k * .8, "pop", .5, 4))
        tx_x = 650 if ic is not None else 540
        els.append(tx(title, 56, col, tx_x, y + h / 2 - (70 if not sub else 100), 480 if ic is not None else 820, .35 + k * .8, gap=1.12))
        if sub: els.append(tx(sub, 42, WHITE, tx_x, y + h / 2 + 50, 480, .5 + k * .8, bold=False))
    badge = Cv(150, 150); badge.shadow((10, 120, 140, 150), 8, 110); badge.ell((6, 4, 144, 142), fill=(8, 18, 44), outline=YEL, width=6)
    badge.text(75, 22 if op != "vs" else 38, op, 90 if op != "vs" else 64, YEL, anchor="m")
    els.append(El(badge.out(), 465, 535, 1.0, "pop", .45))
    return els


def sc_scale():
    img, L, R = scale_img(900, 560, 12)
    els = [El(img, 90, 150, 0, "rise", .6)]
    els.append(El(coins_img(3, 40), 90 + L[0] - 90, 150 + L[1] + 140 - 90, .8, "pop")); els.append(El(tag_img(160, "-30%"), 90 + L[0] - 70, 150 + L[1] + 70, .9, "pop"))
    els.append(El(box_img(150, 180, RED, "A", "HSD 11/26", True), 90 + R[0] - 130, 150 + R[1] + 110, 1.0, "pop")); els.append(El(calendar_img(120, 130, "30", "", "T11", RED), 90 + R[0] + 10, 150 + R[1] + 130, 1.1, "pop"))
    els.append(tx("Lợi ích từ giá nhập rẻ", 50, GREEN, 290, 760, 400, 1.2)); els.append(tx("Chi phí giữ hàng + nguy cơ hết hạn", 50, RED, 790, 760, 420, 1.4))
    els.append(El(card_img(900, 280, YEL), 90, 960, 1.6, "rise")); els.append(tx("Hàng nằm quá lâu: phần chi phí có thể nặng hơn phần tiết kiệm được", 52, WHITE, 540, 1010, 820, 1.8, gap=1.2))
    return els


def sc_notebook(merged):
    els = []; srcs = [("Sổ ghi chép", book_img(200, 250)), ("Nhiều báo cáo riêng lẻ", report_img(200, 250)), ("Trí nhớ của vài người", brain_img(230, 230))]
    for i, (t, ic) in enumerate(srcs):
        y = 150 + i * 330
        els.append(El(card_img(560, 290, LINEC), 70, y, i * .5, "rise")); els.append(El(ic, 90 + (200 - ic.width) / 2, y + (290 - ic.height) / 2 - 8, .2 + i * .5, "pop"))
        els.append(tx(t, 44, WHITE, 450, y + 80, 300, .3 + i * .5, gap=1.1))
        if merged: els.append(El(arrow_img(120, 20, CYAN), 640, y + 120, 1.6 + i * .15, "wipe_l"))
    if merged:
        els.append(El(card_img(300, 960, GREEN), 740, 150, 1.8, "rise")); els.append(El(phone_img(220, 400), 780, 260, 2.0, "pop"))
        els.append(tx("Một bức tranh chung", 46, GREEN, 890, 700, 260, 2.2, gap=1.1))
        for j, c in enumerate((GREEN, ORANGE, CYAN)): els.append(El(card_img(180, 90, c, (16, 40, 80)), 800, 330 + j * 110, 2.3 + j * .15, "rise"))
    else:
        els.append(El(mark_img("cross", 150, RED), 780, 330, 1.8, "pop")); els.append(tx("Rời rạc, khó nhìn thấy vấn đề sớm", 52, ORANGE, 860, 520, 360, 2.0, gap=1.15))
    return els


def sc_checklist(items, title, mood="ok"):
    els = [title_lab(title, CYAN, 124, 56, 0, 900)]; n = len(items); h = int(min(210, (1000 - 30 * (n - 1)) / n))
    for i, t in enumerate(items):
        y = 230 + i * (h + 30); els.append(El(card_img(640, h, GREEN), 60, y, .3 + i * .45, "rise")); els.append(El(mark_img("check", 86, GREEN), 85, y + (h - 86) / 2, .5 + i * .45, "pop", .4))
        els.append(tx(t, 46, WHITE, 410, y + h / 2 - 30, 470, .55 + i * .45, gap=1.1))
    els.append(El(person_img(300, 440, mood), 730, 400, .5, "rise", .6, 4))
    return els


def dash_img(w, h, tiles, title="Novixa  ·  Tồn kho", zk=1.0):
    cv = Cv(w, h); cv.shadow((w * .05, h * .94, w * .95, h * 1.05), 14, 130)
    cv.grad((0, 0, w, h * .97), (36, 56, 96), (20, 34, 66), r=26); cv.rect((0, 0, w, 66), fill=(12, 26, 54), r=26)
    for i, c in enumerate((RED, YEL, GREEN)): cv.ell((28 + i * 36, 24, 48 + i * 36, 44), fill=c)
    cv.text(w / 2, 14, title, 36, CYAN, anchor="m")
    th = (h - 120 - 30 * (len(tiles) - 1)) / len(tiles)
    for i, (label, val, col) in enumerate(tiles):
        y = 96 + i * (th + 30); cv.grad((28, y, w - 28, y + th), (22, 46, 88), (14, 32, 64), r=20); cv.rect((28, y, w - 28, y + th), outline=col, width=4, r=20)
        cv.text(60 * zk, y + th * .2, label, 40 * zk, WHITE, maxw=w * .55, gap=1.1); cv.text(w - 50 * zk, y + th * .12, val, 92 * zk, col, anchor="r")
        for k, v in enumerate((.3, .5, .4, .7, .55, .85)): cv.rect((60 + k * 38, y + th - 22 - v * th * .3, 60 + k * 38 + 24, y + th - 22), fill=shade(col, -.15), r=4)
    return cv.out()


def sc_dash(tiles, tagtxt=""):
    els = [El(dash_img(920, 1000, tiles), 80, 150, 0, "rise", .6)]
    for i in range(len(tiles)): pass
    if tagtxt: els.append(tx(tagtxt, 34, GREY, 540, 1170, 900, 1.6, bold=False))
    return els


def sc_modules():
    els = [at(tx("Một hệ thống thống nhất", 60, CYAN, 540, 124, 900).img, 540, 124, 0)]
    hub = Cv(300, 300); hub.shadow((20, 250, 280, 300), 10, 120); hub.ell((10, 6, 290, 286), fill=(8, 22, 52), outline=CYAN, width=8); hub.text(150, 110, "NOVIXA", 56, CYAN, anchor="m"); hub.text(150, 180, "Nhà thuốc", 34, WHITE, False, "m")
    els.append(El(hub.out(), 390, 540, .3, "pop", .6))
    items = [("Bán hàng", GREEN, coins_img(3, 30), (80, 250)), ("Tồn kho", CYAN, box_img(110, 130, CYAN, "A", ""), (610, 250)), ("Nhập hàng", ORANGE, carton_img(150, 110), (80, 860)), ("Báo cáo", PURPLE, report_img(120, 150), (610, 860))]
    for k, (name, col, ic, (x, y)) in enumerate(items):
        els.append(El(card_img(390, 300, col), x, y, .6 + k * .35, "rise")); els.append(El(ic, x + (390 - ic.width) / 2, y + 40, .8 + k * .35, "pop")); els.append(tx(name, 50, col, x + 195, y + 205, 340, .9 + k * .35))
    els.append(tx("Dữ liệu cùng một nơi", 50, YEL, 540, 1200, 900, 2.0, bold=False))
    return els


def sc_receipt(insight):
    els = [El(receipt_img(420, 660), 70, 220, 0, "rise", .6)]
    if not insight:
        els.append(El(person_img(300, 430, "think"), 640, 400, .6, "rise")); els.append(tx("Mới chỉ ghi nhận giao dịch đã xong", 54, WHITE, 540, 980, 900, 1.0)); els.append(El(mark_img("check", 110, GREEN), 560, 280, .8, "pop"))
        return els
    els.append(El(arrow_img(120, 22, CYAN), 500, 520, .8, "wipe_l"))
    for i, (t, ic, c) in enumerate((("Bán nhanh hay chậm?", clock_img(60, 2, 0, ORANGE), ORANGE), ("Còn bao nhiêu?", carton_img(120, 90), CYAN), ("Hạn dùng ra sao?", calendar_img(100, 110, "30", "", "T11"), RED))):
        y = 210 + i * 250; els.append(El(card_img(430, 220, c), 620, y, 1.0 + i * .4, "rise")); els.append(El(ic, 640 + (130 - ic.width) / 2 if ic.width < 130 else 640, y + 55, 1.2 + i * .4, "pop")); els.append(tx(t, 36, c, 880, y + 70, 260, 1.3 + i * .4, gap=1.1))
    els.append(tx("Hiểu điều gì xảy ra sau mỗi giao dịch", 54, YEL, 540, 1000, 900, 2.2))
    return els


def sc_timeline():
    els = []
    for k, (title, col, marks, ic) in enumerate((("Cuối tháng mới kiểm kê", RED, [.96], mag_img(150)), ("Theo dõi trong quá trình vận hành", GREEN, [.1, .3, .5, .7, .9], None))):
        y = 150 + k * 560
        els.append(El(card_img(900, 500, col), 90, y, k * 1.0, "rise")); els.append(tx(title, 52, col, 540, y + 36, 820, .2 + k * 1.0))
        tl = Cv(820, 120); tl.rect((0, 50, 820, 70), fill=(100, 130, 170), r=10)
        for m in marks: x = 820 * m; tl.ell((x - 26, 34, x + 26, 86), fill=col, outline=WHITE, width=4)
        els.append(El(tl.out(), 130, y + 170, .5 + k * 1.0, "wipe_l", .9))
        els.append(tx("Ngày 1", 38, GREY, 210, y + 300, 200, .8 + k * 1.0, bold=False)); els.append(tx("Ngày 30", 38, GREY, 910, y + 300, 200, .8 + k * 1.0, bold=False))
        els.append(tx("Phát hiện muộn" if k == 0 else "Phát hiện sớm, kịp xử lý", 56, col, 540, y + 390, 820, 1.2 + k * 1.0))
    return els


def sc_note(text, col=CYAN, mood="think"):
    els = [El(card_img(900, 560, col), 90, 160, 0, "rise"), tx(text, 64, WHITE, 540, 300, 780, .4, gap=1.22)]
    els.append(at(person_img(320, 460, mood), 540, 780, .8, "rise", .6, 4)); els.append(El(mark_img("q", 110, YEL), 780, 860, 1.2, "pop", .5, 6))
    return els


def sc_cta():
    els = [at(tx("NOVIXA", 120, CYAN, 540, 140, 900).img, 540, 140, 0, "pop", .6), tx("Nền tảng vận hành nhà thuốc thông minh", 54, WHITE, 540, 330, 880, .4)]
    els.append(El(phone_img(380, 640), 350, 520, .8, "rise", .7)); els.append(El(dash_img(330, 560, [("Bán chậm", "12", ORANGE), ("Sắp hết hạn", "5", RED), ("Cần chú ý", "8", CYAN)], "Novixa", .62), 375, 548, 1.1, "fade", .6)); btn = Cv(300, 90); btn.grad((0, 0, 300, 90), YEL, ORANGE, r=45); btn.text(150, 20, "Tìm hiểu thêm", 40, INK, anchor="m")
    els.append(El(btn.out(), 390, 1080, 1.5, "pop", .5, 5)); els.append(at(arrow_img(120, 24, YEL, True), 540, 1170, 1.8, "wipe_l")) if False else None
    els.append(tx("Đường dẫn bên dưới", 50, YEL, 540, 1190, 800, 1.8))
    return [e for e in els if e]


def sc_decision(text, sub=None):
    els = [at(person_img(360, 520, "think"), 330, 300, 0, "rise", .6, 4), El(box_img(300, 350, CYAN, "Thuốc A", "HSD 12/2027"), 600, 520, .5, "pop", .5, 5)]
    els.append(El(mark_img("q", 140, YEL), 760, 330, .9, "pop", .5, 6)); els.append(tx(text, 66, WHITE, 540, 900, 900, .8));
    if sub: els.append(tx(sub, 46, GREY, 540, 1080, 900, 1.2, bold=False))
    els.append(El(coins_img(4, 40), 160, 1170, 1.5, "pop")); els.append(El(carton_img(220, 160), 760, 1130, 1.7, "pop"))
    return els


def sc_bars(items, hidden=(), single=False, demo=None):
    els = [El(card_img(920, 1090, LINEC), 80, 140, 0, "rise")]; gl = Cv(840, 760)
    for i in range(5): gl.line([(0, 150 + i * 150), (840, 150 + i * 150)], (40, 74, 120), 3)
    els.append(El(gl.out(), 120, 250, .2, "fade"))
    n = len(items); bw = 200 if n > 1 else 360; step = 840 / n
    for i, item in enumerate(items):
        label, val, col = item[:3]; vtxt = item[3] if len(item) > 3 else "%d%%" % round(val * 100)
        x = 120 + i * step + (step - bw) / 2; hgt = int(680 * val) if i not in hidden else 520
        if i in hidden:
            b = Cv(bw, hgt); b.rect((0, 0, bw, hgt), outline=GREY, width=5, r=14); b.text(bw / 2, hgt / 2 - 80, "?", 160, GREY, anchor="m")
            els.append(El(b.out(), x, 930 - hgt, .6 + i * .5, "fade", .6))
            els.append(tx("chưa nhìn thấy", 34, RED, x + bw / 2, 940 - hgt - 50, 240, 1.0 + i * .5, bold=False))
        else:
            b = Cv(bw, hgt); b.grad((0, 0, bw, hgt), shade(col, .15), shade(col, -.2), r=16)
            els.append(El(b.out(), x, 930 - hgt, .4 + i * .5, "wipe_b", .9))
            els.append(tx(vtxt, 56, WHITE, x + bw / 2, 930 - hgt - (122 if single else 70), 300, .9 + i * .5))
            if single: els.append(tx("Doanh thu", 34, GREY, x + bw / 2, 930 - hgt - 52, 240, 1.2 + i * .5, bold=False))
        els.append(tx(label, 46, WHITE, x + bw / 2, 970, 270, .5 + i * .5, gap=1.08))
    if demo is True or (demo is None and any(len(it) < 4 for it in items)):
        els.append(tx("Số liệu minh họa", 38, (170,190,215), 540, 1140, 600, 1.6, bold=False))
    if single: els.append(El(coins_img(6, 44), 760, 760, 1.4, "pop")); els.append(El(person_img(220, 320, "ok"), 780, 480, 1.0, "rise"))
    return els


# ---------- real screenshots ----------
import os as _os
SHOTS = _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), "shots")


def real_img(name, crop, width, radius=26):
    from PIL import ImageFilter
    im = Image.open(_os.path.join(SHOTS, name + ".jpg")).convert("RGB").crop(crop)
    k = width / im.width; im = im.resize((width, int(im.height * k)), Image.LANCZOS)
    pad = 36; W, H = im.width + pad * 2, im.height + pad * 2
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0)); ImageDraw.Draw(sh).rounded_rectangle((pad, pad + 10, pad + im.width, pad + im.height + 10), radius, fill=(0, 0, 0, 150))
    out.alpha_composite(sh.filter(ImageFilter.GaussianBlur(14)))
    m = Image.new("L", im.size, 0); ImageDraw.Draw(m).rounded_rectangle((0, 0, im.width - 1, im.height - 1), radius, fill=255)
    card = im.convert("RGBA"); card.putalpha(m); out.alpha_composite(card, (pad, pad))
    ImageDraw.Draw(out).rounded_rectangle((pad, pad, pad + im.width - 1, pad + im.height - 1), radius, outline=(120, 190, 255, 200), width=3)
    return out, k, pad


def mark_box(w, h, col=(255, 196, 40)):
    im = Image.new("RGBA", (w + 16, h + 16), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rounded_rectangle((4, 4, w + 12, h + 12), 18, outline=col + (255,), width=8)
    return im


def sc_real(name, crop, width, title, marks=(), tag="Màn hình Novixa thật, dữ liệu demo"):
    img, k, pad = real_img(name, crop, width)
    x0 = 540 - img.width / 2
    total = 70 + 20 + img.height + 50
    top = max(125, 100 + (1230 - total) // 2 - 10)
    els = [tx(title, 54, WHITE, 540, top, 940, 0.0)]
    y0 = top + 70 + 20
    els.append(El(img, x0, y0, .15, "pop", .6))
    for j, (mx, my, mw, mh) in enumerate(marks):
        b = mark_box(int(mw * k), int(mh * k))
        els.append(El(b, x0 + pad + (mx - crop[0]) * k - 8, y0 + pad + (my - crop[1]) * k - 8, 1.0 + .5 * j, "pop", .4))
    els.append(tx(tag, 32, GREY, 540, y0 + img.height + 4, 900, 1.2, bold=False))
    return els


# ---------- scene plan ----------
def plan(i, t):
    G, C, O, R, Y, P = GREEN, CYAN, ORANGE, RED, YEL, PURPLE
    person = person_img(150, 220, "ok")
    S = {
        0: lambda: sc_title(t),
        1: sc_day,
        2: lambda: sc_flow3([("Khách vẫn đến", "Nhà thuốc vẫn đông", person_img(190, 270, "ok"), G), ("Thuốc vẫn bán", "Hàng ra đều mỗi ngày", box_img(180, 210, C, "Thuốc A", "HSD 12/2027"), G), ("Tiền vẫn thu về", "Doanh thu vẫn có", coins_img(6, 40), G)]),
        3: lambda: shelf_scene("Bên ngoài: mọi thứ bình thường", G, 9, marks={0: "check", 4: "check", 8: "check"}, strip=("coins", "Tiền thu về mỗi ngày", G)),
        4: lambda: sc_bars([("Doanh thu", .85, G), ("Tiền nhập hàng", .62, C), ("Chi phí khác", .4, O)]),
        5: lambda: sc_question("“Bán đâu có ít, sao tiền không thấy đâu?”"),
        6: lambda: sc_flow3([("Tiền", "Vốn của nhà thuốc", coins_img(6, 40), Y), ("Nhập hàng", "Đổi tiền lấy hàng", carton_img(220, 160), C), ("Hộp thuốc trên kệ", "Tiền nằm trong hàng", box_img(170, 200, C, "Thuốc A", "HSD 12/2027"), C)]),
        7: sc_places,
        8: lambda: sc_types([("Bán nhanh", "Nhập về là hết", box_img(150, 180, G, "A", ""), G), ("Bán chậm", "Nằm trên kệ lâu", clock_img(80, 3, 0, O), O), ("Sắp hết hạn", "Âm thầm tiến gần hạn", calendar_img(150, 160, "30", "HSD", "T11"), R)]),
        9: lambda: sc_speed("fast"),
        10: lambda: sc_speed("slow"),
        11: lambda: sc_expiry("Sắp hết hạn", .8, "30", "Tháng 11"),
        12: lambda: sc_types([("Bán nhanh: vốn quay nhanh", None, coins_img(5, 36), G), ("Bán chậm: vốn bị giữ", None, lock_img(110, O), O), ("Cận hạn: vốn có nguy cơ mất", None, cracked_coin_img(56), R)]),
        13: lambda: sc_locked("Hộp thuốc còn nguyên, nhưng tiền bên trong bị giữ lại"),
        14: lambda: sc_cycle(False),
        15: lambda: sc_cycle(True),
        16: lambda: shelf_scene("Kệ đầy hàng bán chậm", O, 9, dim=range(9), strip=("empty", "Ví tiền mặt: gần như trống", R)),
        17: lambda: shelf_scene("Hàng nào đang có vấn đề?", Y, 9, marks={1: "q", 4: "q", 7: "q"}, strip=("coins", "Tiền đã chi để nhập hàng", Y)),
        18: lambda: sc_speed("slow"),
        19: lambda: sc_expiry("Càng để lâu, càng gần hạn", .65, "15", "Tháng 10", "HSD", "Thời gian trôi qua"),
        20: lambda: sc_expiry("Chỉ còn vài tháng", .92, "05", "Tháng 12", "HSD", "Đến khi phát hiện ra"),
        21: sc_options,
        22: lambda: sc_locked("Vốn quay nhiều lần, cuối cùng mắc kẹt trong hộp thuốc khó bán", "Một hộp thuốc bán chậm giữ một khoản tiền"),
        23: lambda: sc_decision("Nhập hàng", "Một quyết định rất quen thuộc"),
        24: lambda: sc_types([("Nhà phân phối có ưu đãi", None, tag_img(200, "ƯU ĐÃI", O), O), ("Nhập nhiều hơn, giá tốt hơn", None, carton_img(220, 160), G), ("Mặt hàng trước đây bán chạy", None, box_img(150, 180, C, "Thuốc A", ""), C)]),
        25: lambda: sc_decision("Nhập thêm một chút sẽ có lợi?", "Suy nghĩ rất dễ gặp"),
        26: lambda: sc_compare("Giá nhập rẻ hơn", "Kinh doanh hiệu quả hơn", "≠", tag_img(260, "RẺ HƠN"), report_img(220, 280, C), G, C),
        27: sc_scale,
        28: lambda: sc_types([("Mua được giá tốt", None, tag_img(200, "GIÁ TỐT", G), G), ("Phù hợp nhu cầu bán thực tế", None, report_img(150, 190, C), C), ("= Quyết định nhập tốt", None, mark_img("check", 130, G), Y)]),
        29: lambda: sc_bars([("Doanh thu", .85, G)], single=True),
        30: lambda: sc_bars([("Doanh thu", .85, G), ("Tiền trong tồn kho", .5, C), ("Chi phí vận hành", .5, O)], hidden=(1, 2)),
        31: lambda: sc_checklist(["Tiền nằm trong hàng tồn", "Hàng phải giảm giá", "Hàng không bán được", "Thuê mặt bằng, nhân sự, chi phí khác"], "Những phần còn lại", "think"),
        32: lambda: sc_notebook(True),
        33: lambda: sc_question("Hoạt động kinh doanh đang tạo ra bao nhiêu giá trị thực sự?", "think"),
        34: lambda: sc_checklist(["Có mặt ở cửa hàng mỗi ngày", "Theo sát nhân viên", "Kiểm tra hàng hóa"], "Chủ nhà thuốc rất chăm chỉ", "ok"),
        35: lambda: sc_checklist(["Có mặt mỗi ngày", "Theo sát nhân viên", "Kiểm tra hàng hóa", "Giải quyết việc phát sinh"], "Một ngày của chủ nhà thuốc", "ok"),
        36: lambda: sc_notebook(False),
        37: lambda: sc_compare("Làm việc nhiều hơn", "Thấy vấn đề sớm hơn", "≠", person_img(220, 320, "worry"), mag_img(220), O, C),
        38: lambda: sc_compare("Thêm một người bận rộn", "Cách nhìn rõ hơn", "→", person_img(220, 320, "worry"), mag_img(240), GREY, G),
        39: lambda: sc_real("0svtai", (88, 50, 476, 245), 940, "Nhận ra sớm hàng sắp hết hạn", marks=[(104, 62, 180, 66), (104, 165, 190, 66)]),
        40: sc_timeline,
        41: lambda: sc_compare("Thói quen, cảm giác", "Tình hình bán thực tế", "→", brain_img(240, 240), report_img(200, 250, C), GREY, G),
        42: lambda: sc_note("Không thể tránh được mọi tổn thất", O, "think"),
        43: lambda: sc_compare("Phát hiện sớm: nhiều cách xử lý", "Phát hiện muộn: ít lựa chọn", "vs", mark_img("check", 160, G), mark_img("cross", 160, R), G, R),
        44: lambda: sc_title("Hướng Novixa theo đuổi"),
        45: sc_modules,
        46: lambda: sc_real("39lplt", (88, 14, 476, 322), 940, "Theo dõi doanh số từng ngày", marks=[(412, 168, 40, 122)]),
        47: lambda: sc_receipt(False),
        48: lambda: sc_real("39lplt", (88, 348, 476, 830), 760, "Hiểu điều gì đang diễn ra", marks=[(104, 598, 352, 36)]),
        49: lambda: shelf_scene("Cắt tồn kho bằng mọi giá?", R, 3, marks={}, strip=("coins", "Không phải giải pháp", R)) + [El(mark_img("cross", 240, RED), 420, 640, 1.2, "pop", .5), tx("Kệ trống, khách không có hàng", 50, WHITE, 540, 1030, 800, 1.4)],
        50: lambda: shelf_scene("Vẫn cần đủ hàng cho khách", G, 9, marks={0: "check", 4: "check", 8: "check"}, strip=("coins", "Đủ hàng để phục vụ", G)),
        51: lambda: sc_types([("Duy trì", "mặt hàng cần có", mark_img("check", 120, G), G), ("Bổ sung", "mặt hàng sắp thiếu", carton_img(200, 150), C), ("Chú ý", "trước khi vốn bị giữ lâu", mag_img(150), O)]),
        52: lambda: sc_compare("Ít hàng hơn", "Quản lý tốt", "≠", box_img(140, 170, GREY, "A", "", False, True), mark_img("check", 160, G), GREY, G),
        53: lambda: sc_types([("Đồng vốn dùng có chủ đích", None, coins_img(5, 36), Y), ("Hàng hóa được theo dõi tốt", None, box_img(150, 180, C, "A", ""), C), ("Quyết định có cơ sở rõ ràng", None, report_img(130, 170, G), G)]),
        54: lambda: shelf_scene("Mặt hàng nào giúp vốn quay nhanh?", G, 9, marks={1: "check", 3: "check", 8: "check"}, strip=("coins", "Vốn quay vòng nhanh", G)),
        55: lambda: shelf_scene("Mặt hàng nào đang giữ tiền lại?", R, 9, dim=(2, 5, 8), marks={2: "lock", 5: "lock", 8: "lock"}, strip=("coins", "Tiền bị giữ lại", R)),
        56: lambda: sc_note("Vấn đề lớn không bắt đầu bằng khoản thất thoát rõ ràng", R, "worry"),
        57: lambda: sc_speed("slow"),
        58: lambda: sc_cycle(False, ("Nhập hàng", "Bán hàng", "Tiền về")),
        59: lambda: sc_title("Nền tảng vận hành nhà thuốc thông minh"),
        60: sc_cta,
        61: lambda: sc_question("Hãy thử tự hỏi:", "think"),
        62: lambda: sc_question("Món nào giúp tiền quay vòng — và món nào giữ tiền của bạn lại?", "think"),
        63: lambda: sc_title("Nền tảng vận hành nhà thuốc thông minh"),
    }
    return S[i]()


# ---------- caption (karaoke) ----------
def caption_states(text, dur):
    words = text.split(); box = CAPBOX; maxw = box[2] - box[0] - 100
    for size in range(68, 44, -2):
        f = F(size * SS); lines = wrap(text, F(size, True), maxw)
        if len(lines) * size * 1.22 <= (box[3] - box[1]) - 50: break
    f = F(size, True); lh = int(size * 1.22); block = len(lines) * lh; y0 = (box[3] - box[1] - block) // 2 - 4
    pos = []; k = 0
    for li, ln in enumerate(lines):
        ws = ln.split(); wd = _dummy.textlength(ln, font=f); x = (box[2] - box[0] - wd) / 2; sp = _dummy.textlength(" ", font=f)
        for w in ws:
            pos.append((w, x, y0 + li * lh)); x += _dummy.textlength(w, font=f) + sp
    n = len(pos); chars = [len(p[0]) + 1 for p in pos]; tot = sum(chars); starts = []; acc = 0
    for c in chars: starts.append(.15 + acc / tot * dur * .8); acc += c
    states = []
    for k in range(n + 1):
        im = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
        for j, (w, x, y) in enumerate(pos):
            col = YEL if j < k else ((255, 255, 255) if j == k else (120, 142, 178))
            d.text((x, y), w, font=f, fill=col, stroke_width=2, stroke_fill=(4, 10, 24))
        states.append(im)
    return states, starts


# ---------- compose ----------
def ease(p): return 1 - (1 - p) ** 3
def back(p): c1 = 1.70158; c3 = c1 + 1; return 1 + c3 * (p - 1) ** 3 + c1 * (p - 1) ** 2


def paste(dst, img, x, y, alpha=1.0):
    if alpha <= 0.01: return
    if alpha < .99:
        a = img.getchannel("A").point(lambda v: int(v * alpha)); img = img.copy(); img.putalpha(a)
    dst.paste(img, (int(round(x)), int(round(y))), img)


def draw_el(fr, e, t):
    p = (t - e.delay) / e.dur
    if p <= 0: return
    p = min(1.0, p); im = e.img; x, y = e.x, e.y
    bob = e.bob * math.sin(2 * math.pi * (t * .6 + e.delay)) if p >= 1 and e.bob else 0
    if e.kind == "rise": paste(fr, im, x, y + (1 - ease(p)) * 70 + bob, min(1, p * 1.5))
    elif e.kind == "fade": paste(fr, im, x, y + bob, p)
    elif e.kind == "pop":
        s = .55 + .45 * back(p)
        if abs(s - 1) > .01: im = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.BILINEAR)
        paste(fr, im, x + (e.img.width - im.width) / 2, y + (e.img.height - im.height) / 2 + bob, min(1, p * 2))
    elif e.kind == "count":
        v = e.spec[0] * ease(p); paste(fr, _count_img(e, v), x, y, min(1, p * 4))
    elif e.kind == "wipe_l":
        w = max(1, int(im.width * ease(p))); paste(fr, im.crop((0, 0, w, im.height)), x, y)
    elif e.kind == "wipe_b":
        hh = max(1, int(im.height * ease(p))); paste(fr, im.crop((0, im.height - hh, im.width, im.height)), x, y + im.height - hh)


def render_scene(i, text, dur, outpath, base):
    els = plan(i, text); states, starts = caption_states(text, dur); n = max(1, int(round(dur * FPS)))
    cmd = ["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-", "-c:v", "libx264", "-preset", "veryfast", "-crf", "24", "-pix_fmt", "yuv420p", outpath]
    pr = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    last = None; end_anim = max(e.delay + e.dur for e in els); anyb = any(e.bob for e in els)
    for f in range(n):
        t = f / FPS; k = sum(1 for s in starts if t >= s)
        key = (k, round(t, 2) if (t < end_anim + .05 or anyb) else None)
        if last is not None and last[0] == key: pr.stdin.write(last[1]); continue
        fr = base.copy()
        for e in els: draw_el(fr, e, t)
        fr.paste(states[k], (CAPBOX[0], CAPBOX[1]), states[k]); raw = fr.convert("RGB").tobytes()
        last = (key, raw); pr.stdin.write(raw)
    pr.stdin.close(); pr.wait()


def still(i, text, out):
    els = plan(i, text); base = board_base(); fr = base.copy(); states, _ = caption_states(text, 4)
    for e in els: draw_el(fr, e, 99)
    fr.paste(states[-1], (CAPBOX[0], CAPBOX[1]), states[-1]); fr.convert("RGB").save(out)


if __name__ == "__main__":
    mode = sys.argv[1]
    job = json.load(open(sys.argv[2], encoding="utf-8")); lines = [x.strip() for x in job["lines"] if x.strip()]
    if mode == "still":
        out = sys.argv[3]; os.makedirs(out, exist_ok=True)
        ids = [int(x) for x in sys.argv[4].split(",")] if len(sys.argv) > 4 else range(64)
        for i in ids: still(i, lines[i], f"{out}/{i:02d}.png")
        print("ok")
    elif mode == "seg":
        tl = json.load(open(sys.argv[3])); outdir = sys.argv[4]; os.makedirs(outdir, exist_ok=True)
        ids = [int(x) for x in sys.argv[5].split(",")]; base = board_base(); starts = [round(x["start"] * FPS) for x in tl] + [round((tl[-1]["start"] + tl[-1]["dur"]) * FPS)]
        for i in ids:
            d = (starts[i + 1] - starts[i]) / FPS; render_scene(i, lines[i], d, f"{outdir}/{i:02d}.mp4", base); print("seg", i, flush=True)

# -*- coding: utf-8 -*-
"""NGV – cảnh số liệu / thẻ chữ cùng nét vẽ với ngv_scenes (nền xanh đêm, vàng giữ vốn).

Mỗi hàm: f(t_local, cfg) -> PIL.Image RGB 1920x1080.
"""
import math
import random

from PIL import Image

import ngv_scenes as N
from ngv_scenes import (DN, GOLD, H, SS, UP, W, Pen, clamp, down, ease, lerp, light, mix, seg, vgrad,
                        vignette)

INK = (236, 240, 248)
MUT = (140, 152, 178)
PAN = (22, 30, 48)
LINE = (40, 50, 72)
NAVY0, NAVY1 = (16, 22, 40), (10, 14, 26)


def base(dim=1.0):
    img = N.canvas()
    p = Pen(img)
    vgrad(p, 0, 0, W, H, NAVY0, NAVY1)
    for x in range(0, W, 120):
        p.line([(x, 0), (x, H)], (255, 255, 255, 6), 1)
    for y in range(0, H, 120):
        p.line([(0, y), (W, y)], (255, 255, 255, 6), 1)
    return img, p


def finish(img, glow=None, vig=0.5):
    out = down(img)
    if glow:
        out = light(out, *glow)
    return vignette(out, vig)


def wrap(p, s, size, maxw, w="Bold", disp=True):
    out, cur = [], ""
    for wd in s.split():
        tr = (cur + " " + wd).strip()
        if p.tw(tr, size, w, disp) > maxw and cur:
            out.append(cur)
            cur = wd
        else:
            cur = tr
    out.append(cur)
    return out


def A(a):
    return int(255 * clamp(a))


def person(p, cx, cy, s, col):
    p.ell(cx, cy - 16 * s, 9 * s, 9 * s, col)
    p.rect(cx - 12 * s, cy - 4 * s, cx + 12 * s, cy + 22 * s, col, 9 * s)


# ------------------------------------------------------------ thẻ mở chương
def section(t, c):
    img, p = base()
    a = seg(t, 0.1, 0.6)
    k = c.get("kicker", "")
    if k:
        p.text(W / 2, 400 - 20 * (1 - a), k, 34, GOLD + (A(a),), "SemiBold", "mm")
    rows = wrap(p, c["title"], 76, 1500)
    y0 = 520 - (len(rows) - 1) * 46
    for i, r in enumerate(rows):
        ai = seg(t, 0.25 + i * 0.12, 0.8 + i * 0.12)
        p.text(W / 2, y0 + i * 92 + 20 * (1 - ai), r, 76, INK + (A(ai),), "Bold", "mm", True)
    ul = seg(t, 0.7, 1.4)
    yy = y0 + len(rows) * 92 - 20
    p.rect(W / 2 - 160 * ul, yy, W / 2 + 160 * ul, yy + 6, GOLD, 3)
    if c.get("sub"):
        p.text(W / 2, yy + 60, c["sub"], 30, MUT + (A(seg(t, 0.9, 1.4)),), "Medium", "mm")
    return finish(img, (960, 540, 900, (60, 70, 120), 0.25))


# ------------------------------------------------------------ trích dẫn lớn
def quote(t, c):
    img, p = base()
    rows = wrap(p, c["text"], c.get("size", 64), 1400)
    lh = c.get("size", 64) * 1.32
    y0 = 540 - (len(rows) - 1) * lh / 2
    p.text(250, y0 - 70, "“", 220, GOLD + (A(seg(t, 0, 0.4)),), "Bold", "mm", True)
    hl = c.get("hl", [])
    for i, r in enumerate(rows):
        ai = seg(t, 0.15 + i * 0.18, 0.6 + i * 0.18)
        col = GOLD if any(h in r for h in hl) else INK
        p.text(W / 2, y0 + i * lh + 16 * (1 - ai), r, c.get("size", 64), col + (A(ai),), "Bold", "mm", True)
    if c.get("by"):
        p.text(W / 2, y0 + len(rows) * lh + 30, c["by"], 28, MUT + (A(seg(t, 1, 1.5)),), "Medium", "mm")
    return finish(img, (960, 540, 900, (80, 70, 50), 0.2))


# ------------------------------------------------------------ thẻ nguồn nghiên cứu
def source(t, c):
    img, p = base()
    a = seg(t, 0, 0.5)
    x0, y0, x1, y1 = 360, 230 + 30 * (1 - a), 1560, 850 + 30 * (1 - a)
    p.rect(x0 + 14, y0 + 18, x1 + 14, y1 + 18, (0, 0, 0, 90), 30)
    p.rect(x0, y0, x1, y1, PAN + (A(a),), 30)
    p.rect(x0, y0, x0 + 14, y1, GOLD + (A(a),), 7)
    p.text(x0 + 70, y0 + 60, c["place"], 34, GOLD + (A(a),), "SemiBold")
    p.text(x1 - 60, y0 + 64, c.get("when", ""), 30, MUT + (A(a),), "Medium", "ra")
    rows = wrap(p, c["who"], 56, 1060)
    for i, r in enumerate(rows):
        p.text(x0 + 70, y0 + 130 + i * 70, r, 56, INK + (A(seg(t, 0.2, 0.7)),), "Bold", "la", True)
    yy = y0 + 150 + len(rows) * 70
    for i, d in enumerate(c.get("lines", [])):
        ai = seg(t, 0.6 + i * 0.35, 1.0 + i * 0.35)
        p.ell(x0 + 84, yy + i * 64 + 18, 7, 7, GOLD + (A(ai),))
        p.text(x0 + 110, yy + i * 64, d, 32, (210, 216, 230, A(ai)), "Medium")
    return finish(img)


# ------------------------------------------------------------ 100 người
def dots(t, c):
    img, p = base()
    red, gold = c.get("red", 0), c.get("gold", 0)
    rng = c.get("red_hi")
    prog = seg(t, 0.3, c.get("fill_t", 2.0))
    x0, y0, s = 220, 190, 70
    for i in range(100):
        r, k = divmod(i, 10)
        cx, cy = x0 + k * s, y0 + r * s
        col = (64, 74, 98)
        if i < red * prog:
            col = DN
        elif rng and i < rng * prog:
            col = mix(DN, (64, 74, 98), 0.45)
        if gold and i >= 100 - gold and prog >= 1:
            col = GOLD
        person(p, cx, cy, 1.25, col)
    big = c.get("big", "")
    ab = seg(t, 0.5, 1.0)
    p.text(1500, 420, big, 150, (c.get("big_c") or DN) + (A(ab),), "Bold", "mm", True)
    rows = wrap(p, c.get("cap", ""), 36, 640, "SemiBold", False)
    for i, r in enumerate(rows):
        p.text(1500, 540 + i * 50, r, 36, INK + (A(seg(t, 0.8, 1.3)),), "SemiBold", "mm")
    if c.get("note"):
        p.text(1500, 560 + len(rows) * 50 + 20, c["note"], 26, MUT + (A(seg(t, 1.2, 1.7)),), "Medium", "mm")
    return finish(img)


# ------------------------------------------------------------ cột so sánh
def bars(t, c):
    img, p = base()
    items = c["items"]
    vmax = max(v for _, v, _ in items) * 1.15
    base_y, top_y = 860, 260
    n = len(items)
    for i, (lab, v, col) in enumerate(items):
        cx = 960 + (i - (n - 1) / 2) * 420
        g = seg(t, 0.3 + i * 0.4, 1.4 + i * 0.4)
        hh = (base_y - top_y) * v / vmax * g
        p.rect(cx - 120, base_y - hh, cx + 120, base_y, col, 16)
        p.text(cx, base_y - hh - 60, (c.get("fmt") or "{:.1f}%").format(v * g).replace(".", ","), 60, col, "Bold", "mm", True)
        p.text(cx, base_y + 50, lab, 32, INK, "SemiBold", "mm")
    p.line([(300, base_y), (1620, base_y)], LINE, 3)
    if c.get("title"):
        p.text(W / 2, 140, c["title"], 40, INK, "Bold", "mm", True)
    if c.get("gap") and t > 2.2:
        a = seg(t, 2.2, 2.7)
        p.text(W / 2, 220, c["gap"], 34, GOLD + (A(a),), "SemiBold", "mm")
    return finish(img)


# ------------------------------------------------------------ khối đòn bẩy
def blocks(t, c):
    """c: own0, loan, total(lerp), stage: 'build'|'drop'|'net'|'compare'"""
    img, p = base()
    st = c.get("stage", "build")
    sc = 1.55  # px mỗi triệu
    bx, base_y, bw = 620, 900, 300
    own0, loan = c.get("own0", 200), c.get("loan", 200)
    a1 = seg(t, 0.2, 0.8)
    a2 = seg(t, 0.9, 1.6) if st == "build" else 1
    total = own0 + loan
    if st in ("drop", "net", "compare"):
        total = lerp(400, 270, seg(t, 0.2, 1.6)) if st == "drop" else 270
    own = max(0, total - loan)
    # vay (dưới) – vốn mình (trên)
    lh = loan * sc * a2
    p.rect(bx, base_y - lh, bx + bw, base_y, (96, 108, 136), 10)
    if a2 > 0.5:
        p.text(bx + bw / 2, base_y - lh / 2, f"Vay {loan} triệu", 34, (20, 24, 34), "Bold", "mm")
    oh = own * sc * (a1 if st == "build" else 1)
    p.rect(bx, base_y - lh - oh, bx + bw, base_y - lh - 4, GOLD, 10)
    if oh > 50:
        p.text(bx + bw / 2, base_y - lh - oh / 2, f"Của bạn {own:.0f} triệu", 34, (40, 30, 10), "Bold", "mm")
    p.text(bx + bw / 2, base_y - lh - oh - 50, f"Tổng {total:.0f} triệu", 40, INK, "Bold", "mm", True)
    p.line([(bx - 80, base_y), (bx + bw + 80, base_y)], LINE, 3)
    if st in ("drop", "net", "compare"):
        p.rect(bx, base_y - 400 * sc, bx + bw, base_y - total * sc, (255, 255, 255, 18), 10)
        p.text(bx - 30, base_y - 335 * sc, "mất 1/3", 32, DN, "Bold", "rm")
    # bảng bên phải
    rx = 1120
    if st == "build":
        rows = [("Vốn của bạn", f"{own0} triệu", GOLD), ("Vay thêm", f"{loan} triệu", (170, 180, 200)),
                ("Mua cổ phiếu", f"{own0 + loan} triệu", INK)]
    elif st == "drop":
        rows = [("Danh mục giảm", "khoảng 1/3", DN), ("400 triệu còn", f"{total:.0f} triệu", INK)]
    else:
        rows = [("Danh mục còn", "270 triệu", INK), ("Trả khoản vay", "− 200 triệu", (170, 180, 200)),
                ("Phần của bạn", "khoảng 70 triệu", GOLD)]
    for i, (l, v, col) in enumerate(rows):
        ai = seg(t, 0.3 + i * 0.5, 0.8 + i * 0.5)
        y = 330 + i * 130
        p.text(rx, y, l, 32, MUT + (A(ai),), "Medium")
        p.text(rx, y + 44, v, 56, col + (A(ai),), "Bold", "la", True)
    if st == "compare":
        a = seg(t, 0.3, 0.8)
        p.rect(rx - 30, 740, 1820, 900, PAN + (A(a),), 20)
        p.text(rx, 770, "Thị trường: mất 1/3", 38, (210, 216, 230, A(a)), "SemiBold")
        p.text(rx, 830, "Bạn: mất khoảng 2/3", 46, DN + (A(seg(t, 0.8, 1.3)),), "Bold", "la", True)
    return finish(img)


# ------------------------------------------------------------ phí theo tháng
def fees(t, c):
    img, p = base()
    st = c.get("stage", "month")
    p.text(W / 2, 150, c.get("title", "Phí ăn vào vốn"), 44, INK, "Bold", "mm", True)
    if st == "receipt":
        a = seg(t, 0, 0.5)
        x0, y0 = 660, 240
        p.rect(x0, y0, x0 + 600, y0 + 640, (240, 236, 226, A(a)), 16)
        p.text(x0 + 300, y0 + 60, "Mỗi lượt mua rồi bán", 30, (60, 54, 48, A(a)), "Bold", "mm")
        items = c.get("items", [("Phí giao dịch", ""), ("Thuế", ""), ("Chênh lệch giá mua – bán", "")])
        for i, (l, v) in enumerate(items):
            ai = seg(t, 0.4 + i * 0.5, 0.8 + i * 0.5)
            y = y0 + 150 + i * 90
            p.text(x0 + 50, y, l, 30, (60, 54, 48, A(ai)), "Medium")
            p.line([(x0 + 50, y + 56), (x0 + 550, y + 56)], (200, 194, 180, A(ai)), 2)
        if c.get("total"):
            ai = seg(t, 1.8, 2.3)
            p.text(x0 + 50, y0 + 470, "Tổng mỗi lượt", 32, (60, 54, 48, A(ai)), "Bold")
            p.text(x0 + 550, y0 + 520, c["total"], 80, (200, 60, 70, A(ai)), "Bold", "ra", True)
            p.text(x0 + 300, y0 + 610, "giả sử, tùy nơi giao dịch", 22, (120, 112, 100, A(ai)), "Medium", "mm")
        return finish(img)
    if st == "turns":
        n = int(10 * seg(t, 0.3, 3.0) + 1e-6)
        for i in range(10):
            x = 330 + i * 130
            on = i < n
            p.rect(x, 430, x + 100, 560, (UP if on else (52, 62, 86)), 14)
            p.text(x + 50, 470, "Mua", 22, (10, 30, 20), "Bold", "mm")
            p.text(x + 50, 520, "Bán", 22, (10, 30, 20), "Bold", "mm")
            if on:
                p.text(x + 50, 610, "−0,4%", 26, DN, "SemiBold", "mm")
        p.text(W / 2, 760, f"{n} lượt trong 1 tháng", 48, INK, "Bold", "mm", True)
        return finish(img)
    # month: 12 khối, tích luỹ 4%/tháng
    m = int(12 * seg(t, 0.5, c.get("dur", 4.0)) + 1e-6) if st == "year" else 1
    for i in range(12):
        x = 240 + i * 122
        on = i < m
        hh = 300
        p.rect(x, 320, x + 100, 320 + hh, (40, 50, 72), 12)
        if on:
            p.rect(x, 320 + hh * 0.96, x + 100, 320 + hh, DN, 6)
            p.text(x + 50, 300, "−4%", 22, DN, "SemiBold", "mm")
        p.text(x + 50, 660, f"T{i + 1}", 24, MUT, "Medium", "mm")
    tot = 4 * m
    p.text(W / 2, 760, f"Đã trả phí: khoảng {tot}% số vốn", 54, DN if m > 1 else INK, "Bold", "mm", True)
    p.text(W / 2, 830, "nếu số vốn giữ nguyên, trước khi thị trường lên hay xuống", 26, MUT, "Medium", "mm")
    return finish(img)


# ------------------------------------------------------------ biểu đồ đường
def linechart(t, c):
    img, p = base()
    pts = c["pts"]  # [(label, value)]
    n = len(pts)
    x0, x1, y0, y1 = 200, 1720, 220, 840
    vmin, vmax = min(v for _, v in pts), max(v for _, v in pts)
    pad = (vmax - vmin) * 0.12
    vmin, vmax = vmin - pad, vmax + pad
    X = lambda i: x0 + (x1 - x0) * i / (n - 1)
    Y = lambda v: y1 - (v - vmin) / (vmax - vmin) * (y1 - y0)
    for k in range(5):
        v = vmin + (vmax - vmin) * k / 4
        p.line([(x0, Y(v)), (x1, Y(v))], LINE, 1.5)
        p.text(x0 - 20, Y(v), f"{v:,.0f}".replace(",", "."), 22, MUT, "Medium", "rm")
    for i, (lab, _) in enumerate(pts):
        if lab:
            p.text(X(i), y1 + 36, lab, 24, MUT, "Medium", "mm")
    pr = seg(t, 0.2, c.get("draw_t", 2.5))
    m = 1 + (n - 1) * pr
    path = []
    for i in range(int(m) + 1):
        if i >= n:
            break
        f = min(1, m - i)
        if i == 0:
            path.append((X(0), Y(pts[0][1])))
        else:
            xa, ya = X(i - 1), Y(pts[i - 1][1])
            path.append((lerp(xa, X(i), f), lerp(ya, Y(pts[i][1]), f)))
    if len(path) > 1:
        p.line(path, c.get("col", DN), 7)
        p.ell(path[-1][0], path[-1][1], 12, 12, c.get("col", DN))
    if c.get("title"):
        p.text(x0, 140, c["title"], 40, INK, "Bold", "la", True)
    lab = c.get("label")
    if lab and pr >= 1:
        a = seg(t, c.get("draw_t", 2.5), c.get("draw_t", 2.5) + 0.5)
        p.rect(x1 - 420, 120, x1, 210, DN + (A(a),), 18)
        p.text(x1 - 210, 165, lab, 50, (255, 255, 255, A(a)), "Bold", "mm", True)
    for mk in c.get("marks", []):
        i, txt, col, at = mk[:4]
        above = len(mk) > 4 and mk[4]
        a = seg(t, at, at + 0.4)
        if a <= 0:
            continue
        x, y = X(i), Y(pts[i][1])
        p.ell(x, y, 18, 18, col + (A(a),))
        bw = p.tw(txt, 28, "Bold") / 2 + 24
        bx = min(max(x, x0 + bw), x1 - bw)
        by = y - 130 if above else y + 36
        p.rect(bx - bw, by, bx + bw, by + 60, col + (A(a * 0.9),), 14)
        p.text(bx, by + 30, txt, 28, (255, 255, 255, A(a)), "Bold", "mm")
    p.text(x1, y1 + 80, c.get("note", "Hình dạng minh hoạ"), 20, MUT, "Medium", "ra")
    return finish(img)


# ------------------------------------------------------------ danh sách
def checklist(t, c):
    img, p = base()
    if c.get("title"):
        p.text(W / 2, 200, c["title"], 52, INK, "Bold", "mm", True)
    items = c["items"]
    y0 = 540 - (len(items) - 1) * 70
    for i, it in enumerate(items):
        a = seg(t, c.get("t0", 0.3) + i * c.get("step", 0.6), c.get("t0", 0.3) + 0.4 + i * c.get("step", 0.6))
        y = y0 + i * 140
        kind = c.get("kind", "check")
        p.rect(420, y - 52, 1500, y + 52, PAN + (A(a),), 22)
        col = UP if kind == "check" else DN if kind == "cross" else GOLD
        p.ell(500, y, 30, 30, col + (A(a),))
        if kind == "check":
            p.line([(486, y), (497, y + 12), (516, y - 12)], (10, 30, 20, A(a)), 6)
        elif kind == "cross":
            p.line([(488, y - 12), (512, y + 12)], (255, 255, 255, A(a)), 6)
            p.line([(488, y + 12), (512, y - 12)], (255, 255, 255, A(a)), 6)
        else:
            p.text(500, y, str(i + 1), 30, (40, 30, 10, A(a)), "Bold", "mm")
        p.text(560, y, it, 40, INK + (A(a),), "SemiBold", "lm")
    return finish(img)


# ------------------------------------------------------------ 3 nhóm người xem
def audience(t, c):
    img, p = base()
    cards = [("Đang giao dịch", "biết mình thua ở đâu"), ("Sắp bắt đầu", "thấy tỷ lệ thật trước"),
             ("Có người thân giao dịch", "biết nên hỏi câu gì")]
    hi = c.get("hi", -1)
    for i, (h, s) in enumerate(cards):
        a = seg(t, 0.1 + i * 0.25, 0.6 + i * 0.25) if hi < 0 else 1
        x = 260 + i * 480
        on = (i == hi)
        y = 300 - (30 if on else 0)
        p.rect(x, y, x + 420, y + 480, (PAN if not on else (34, 44, 70)) + (A(a),), 28)
        if on:
            p.rect(x, y, x + 420, y + 480, None, 28, outline=GOLD, width=4)
        cx = x + 210
        if i == 2:
            person(p, cx - 50, y + 170, 3.2, (GOLD if on else (120, 132, 160)) + (A(a),))
            person(p, cx + 60, y + 180, 2.6, ((200, 160, 90) if on else (96, 106, 130)) + (A(a),))
        else:
            person(p, cx, y + 170, 3.4, (GOLD if on else (120, 132, 160)) + (A(a),))
        p.text(cx, y + 330, h, 34, INK + (A(a),), "Bold", "mm")
        p.text(cx, y + 385, s, 26, MUT + (A(a),), "Medium", "mm")
        if hi >= 0 and not on:
            p.rect(x, y, x + 420, y + 480, (8, 12, 22, 120), 28)
    return finish(img)


# ------------------------------------------------------------ sổ tay ba con số
def notebook(t, c):
    img = N.bg_home_night().convert("RGB").resize((W * SS, H * SS))
    p = Pen(img)
    p.rect(0, 0, W, H, (0, 0, 0, 120))
    a = seg(t, 0, 0.4)
    x0, y0, x1, y1 = 520, 120 + 30 * (1 - a), 1400, 1000
    p.rect(x0 + 16, y0 + 16, x1 + 16, y1 + 16, (0, 0, 0, 80), 14)
    p.rect(x0, y0, x1, y1, (246, 242, 230), 14)
    for y in range(int(y0) + 120, int(y1) - 20, 64):
        p.line([(x0 + 40, y), (x1 - 40, y)], (200, 214, 230), 2)
    p.line([(x0 + 110, y0 + 20), (x0 + 110, y1 - 20)], (230, 150, 150), 2)
    ink = (36, 48, 92)
    p.text(x0 + 150, y0 + 60, c.get("title", "Phép tính trung thực"), 44, ink, "Bold")
    rows = c.get("rows", [("Tổng tiền đã nạp", ""), ("− Tổng tiền đã rút", ""), ("So với: số tiền đang có", "")])
    for i, (l, v) in enumerate(rows):
        if "show_n" in c:
            ai = 1.0 if i < c["show_n"] else (seg(t, .4, .9) if i == c["show_n"] else 0.0)
        else:
            ai = seg(t, c.get("t0", 0.5) + i * c.get("step", 0.9), c.get("t0", 0.5) + 0.4 + i * c.get("step", 0.9))
        y = y0 + 170 + i * 128
        p.text(x0 + 150, y, l, 40, ink + (A(ai),), "Medium")
        p.line([(x0 + 860, y + 50), (x1 - 80, y + 50)], ink + (A(ai * 0.5),), 2)
    if c.get("total"):
        ai = seg(t, c.get("total_t", 3.2), c.get("total_t", 3.2) + 0.5)
        y = y0 + 170 + len(rows) * 128 + 10
        p.line([(x0 + 150, y - 14), (x1 - 80, y - 14)], ink + (A(ai),), 4)
        p.text(x0 + 150, y + 10, c["total"], 46, (190, 50, 60, A(ai)), "Bold")
        if c.get("circle"):
            p.ell(x0 + 450, y + 36, 330, 62, None, outline=(190, 50, 60, A(ai)), width=4)
    return finish(img, None, 0.6)


# ------------------------------------------------------------ bảng tính (mô phỏng file Excel của kênh)
def sheet(t, c):
    img, p = base()
    x0, y0, x1, y1 = 200, 150, 1720, 900
    p.rect(x0, y0, x1, y1, (250, 250, 250), 12)
    p.rect(x0, y0, x1, y0 + 70, (32, 110, 70), 12)
    p.rect(x0, y0 + 50, x1, y0 + 70, (32, 110, 70))
    p.text(x0 + 30, y0 + 35, "NGV-Bang-tinh-giu-von.xlsx", 26, (255, 255, 255), "SemiBold", "lm")
    tabs = ["Hướng dẫn", "Phép tính trung thực", "Chi phí giao dịch", "Lỗ bao nhiêu lãi bao nhiêu"]
    tx = x0 + 20
    for i, tb in enumerate(tabs):
        w_ = p.tw(tb, 22, "Medium") + 40
        on = i == c.get("tab", 1)
        p.rect(tx, y1 - 56, tx + w_, y1 - 10, (255, 255, 255) if on else (226, 230, 234), 6)
        p.text(tx + 20, y1 - 33, tb, 22, (30, 110, 70) if on else (90, 96, 104), "SemiBold" if on else "Medium", "lm")
        tx += w_ + 8
    rows = c.get("rows", [("Tổng tiền đã nạp", "", True), ("Tổng tiền đã rút", "", True), ("Số tiền đang có", "", True),
                          ("Lãi / lỗ thật", "= tự tính", False)])
    for i, (l, v, inp) in enumerate(rows):
        a = seg(t, 0.3 + i * 0.4, 0.7 + i * 0.4)
        y = y0 + 130 + i * 110
        p.text(x0 + 60, y + 30, l, 34, (40, 44, 52, A(a)), "SemiBold", "lm")
        p.rect(x0 + 760, y, x0 + 1300, y + 64, ((255, 244, 180) if inp else (232, 240, 236)) + (A(a),), 6,
               outline=(200, 204, 210, A(a)), width=2)
        p.text(x0 + 790, y + 32, v or ("nhập số của bạn" if inp else ""), 28,
               ((40, 70, 200) if inp else (40, 44, 52)) + (A(a),), "Medium", "lm")
    if c.get("link"):
        a = seg(t, 1.6, 2.1)
        p.rect(1180, 60, 1740, 130, GOLD + (A(a),), 20)
        p.text(1460, 95, "Link ở phần mô tả  ↓", 32, (40, 30, 10, A(a)), "Bold", "mm")
    return finish(img, None, 0.3)


# ------------------------------------------------------------ hai nguyên nhân đã nói / hai còn lại
def causes(t, c):
    img, p = base()
    hi = c.get("hi", [])
    items = [("1", "Phí ăn dần vào vốn", True), ("2", "Đòn bẩy", True), ("3", "?", False), ("4", "?", False)]
    if c.get("all"):
        items = [("1", "Phí ăn dần vào vốn", True), ("2", "Đòn bẩy", True), ("3", "Chỉ thấy người thắng", True), ("4", "Tin mình khác", True)]
    for i, (n, s, done) in enumerate(items):
        a = seg(t, 0.2 + i * 0.3, 0.6 + i * 0.3)
        x = 240 + i * 370
        p.rect(x, 340, x + 330, 740, (PAN if done else (30, 22, 30)) + (A(a),), 26)
        if (i + 1) in hi:
            p.rect(x, 340, x + 330, 740, None, 26, outline=GOLD + (A(a),), width=5)
        p.text(x + 165, 440, n, 110, ((GOLD if done else DN)) + (A(a),), "Bold", "mm", True)
        p.text(x + 165, 600, s if done else c.get("locked", "Tập sau"), 30, (INK if done else (220, 160, 170)) + (A(a),), "Bold", "mm")
        if not done:
            p.rect(x + 135, 520, x + 195, 560, (220, 160, 170, A(a)), 8)
            p.ell(x + 165, 518, 22, 26, None, outline=(220, 160, 170, A(a)), width=6)
    p.text(W / 2, 200, c.get("title", "4 nguyên nhân"), 52, INK, "Bold", "mm", True)
    return finish(img)


# ------------------------------------------------------------ thẻ kết
def endcard(t, c):
    img, p = base()
    a = seg(t, 0, 0.6)
    p.ell(W / 2, 380, 90, 90, GOLD + (A(a),))
    p.rect(W / 2 - 46, 350, W / 2 + 46, 420, (40, 30, 10, A(a)), 10)
    p.rect(W / 2 - 30, 330, W / 2 + 30, 360, None, 12, outline=(40, 30, 10, A(a)), width=8)
    p.text(W / 2, 560, "Người Giữ Vốn", 96, INK + (A(a),), "Bold", "mm", True)
    p.text(W / 2, 660, "Giữ vốn trước, tính lãi sau.", 44, GOLD + (A(seg(t, 0.6, 1.2)),), "SemiBold", "mm")
    b = seg(t, c.get("disc_t", 99), c.get("disc_t", 99) + 0.5)
    p.text(W / 2, 880, "Nội dung chỉ chia sẻ kiến thức, không phải lời khuyên đầu tư.", 30, MUT + (A(b),), "Medium", "mm")
    p.text(W / 2, 930, "Giao dịch có rủi ro mất vốn.", 30, MUT + (A(b),), "Medium", "mm")
    return finish(img, (960, 420, 700, (90, 70, 30), 0.3))


# ------------------------------------------------------------ tảng băng người sống sót
def survivor(t, c):
    img, p = base()
    wl = 470
    a = seg(t, 0.2, 0.8)
    vgrad(p, 0, wl, W, H, (14, 34, 60), (8, 16, 30))
    p.line([(0, wl), (W, wl)], (120, 170, 220, 160), 3)
    p.text(120, wl - 40, "Bạn nhìn thấy", 30, GOLD, "SemiBold")
    p.text(120, wl + 30, "Bạn không thấy", 30, (140, 170, 210), "SemiBold")
    for i in range(10):
        x = 560 + i * 90
        person(p, x, wl - 70, 2.0, GOLD + (A(a),))
        if c.get("bubbles", True) and i % 3 == 0:
            ab = seg(t, 0.6 + i * 0.1, 1.0 + i * 0.1)
            p.rect(x - 40, wl - 200, x + 70, wl - 150, (255, 255, 255, A(ab)), 14)
            p.text(x + 15, wl - 175, "+%d%%" % (80 + i * 23), 24, (20, 120, 70, A(ab)), "Bold", "mm")
    fade = seg(t, c.get("fade_t", 2.0), c.get("fade_t", 2.0) + 2.0)
    for i in range(90):
        r, k = divmod(i, 15)
        x, y = 330 + k * 92, wl + 110 + r * 88
        col = mix((120, 132, 160), (30, 44, 70), fade)
        person(p, x, y, 1.6, col + (A(seg(t, 0.8, 1.4)),))
    big = seg(t, 1.2, 1.7)
    p.text(1780, wl - 60, "10", 90, GOLD + (A(big),), "Bold", "rm", True)
    p.text(1780, wl + 300, "90", 90, (140, 170, 210, A(big)), "Bold", "rm", True)
    return finish(img, None, 0.5)


# ------------------------------------------------------------ một con số lớn
def bignum(t, c):
    img, p = base()
    v = c["value"] * seg(t, 0.2, 1.6)
    txt = c.get("fmt", "{:.1f}").format(v).replace(".", ",") + c.get("unit", "")
    p.text(W / 2, 470, txt, 200, GOLD, "Bold", "mm", True)
    p.text(W / 2, 640, c.get("cap", ""), 40, INK + (A(seg(t, 0.8, 1.3)),), "SemiBold", "mm")
    if c.get("note"):
        p.text(W / 2, 710, c["note"], 26, MUT + (A(seg(t, 1.2, 1.7)),), "Medium", "mm")
    if c.get("q"):
        a = seg(t, c.get("q_t", 2.0), c.get("q_t", 2.0) + 0.5)
        p.text(W / 2, 220, c["q"], 56, DN + (A(a),), "Bold", "mm", True)
    return finish(img, (960, 470, 800, (90, 70, 30), 0.25))


# ------------------------------------------------------------ hai cột đối lập
def split(t, c):
    img, p = base()
    L, Rr = c["left"], c["right"]
    for k, (head, body, col) in enumerate((L, Rr)):
        a = seg(t, 0.2 + k * 0.6, 0.7 + k * 0.6)
        x0 = 200 + k * 800
        p.rect(x0, 300, x0 + 720, 760, PAN + (A(a),), 30)
        p.rect(x0, 300, x0 + 720, 316, col + (A(a),), 8)
        p.text(x0 + 360, 430, head, 70, col + (A(a),), "Bold", "mm", True)
        rows = wrap(p, body, 44, 620, "SemiBold", False)
        for i, r in enumerate(rows):
            p.text(x0 + 360, 560 + i * 60, r, 44, INK + (A(a),), "SemiBold", "mm")
    if c.get("title"):
        p.text(W / 2, 190, c["title"], 48, INK, "Bold", "mm", True)
    return finish(img)


# ------------------------------------------------------------ vòng lặp
def loop(t, c):
    img, p = base()
    nodes = c.get("nodes", ["Tự tin", "Mua bán nhiều", "Dùng đòn bẩy", "Thua", "Mua bán nhiều hơn để gỡ"])
    n = len(nodes)
    cx, cy, R_ = 960, 560, 330
    hi = c.get("hi", -1)
    if c.get("spin"):
        hi = int((t * 1.2) % n)
    pts = [(cx + R_ * math.cos(-math.pi / 2 + 2 * math.pi * i / n), cy + R_ * math.sin(-math.pi / 2 + 2 * math.pi * i / n)) for i in range(n)]
    for i in range(n):
        a = seg(t, 0.2 + i * c.get("step", 0.5), 0.6 + i * c.get("step", 0.5))
        x0, y0 = pts[i]; x1, y1 = pts[(i + 1) % n]
        if a > 0:
            m0 = (lerp(x0, x1, 0.22), lerp(y0, y1, 0.22)); m1 = (lerp(x0, x1, 0.22 + 0.56 * a), lerp(y0, y1, 0.22 + 0.56 * a))
            p.line([m0, m1], (232, 184, 92, 200), 6)
            if a >= 1:
                ang = math.atan2(y1 - y0, x1 - x0); tx, ty = m1
                p.poly([(tx + 18 * math.cos(ang), ty + 18 * math.sin(ang)), (tx + 14 * math.cos(ang + 2.5), ty + 14 * math.sin(ang + 2.5)),
                        (tx + 14 * math.cos(ang - 2.5), ty + 14 * math.sin(ang - 2.5))], GOLD)
    for i, (x, y) in enumerate(pts):
        a = seg(t, 0.1 + i * c.get("step", 0.5), 0.5 + i * c.get("step", 0.5))
        on = i == hi
        w_ = p.tw(nodes[i], 34, "Bold") / 2 + 40
        p.rect(x - w_, y - 46, x + w_, y + 46, ((60, 30, 36) if on else PAN) + (A(a),), 46)
        p.rect(x - w_, y - 46, x + w_, y + 46, None, 46, outline=(DN if on else (70, 80, 110)) + (A(a),), width=4)
        p.text(x, y, nodes[i], 34, (INK if not on else (255, 220, 225)) + (A(a),), "Bold", "mm")
    if c.get("center"):
        p.text(cx, cy, c["center"], 40, DN + (A(seg(t, 1.5, 2.0)),), "Bold", "mm", True)
    return finish(img)

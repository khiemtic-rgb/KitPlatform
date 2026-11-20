"""Nhân vật khớp nối 2D (hoạt hình cắt giấy) cho kênh Người Giữ Vốn. Ẩn danh: chỉ nhìn từ sau lưng chếch trái, không có mặt.

Cảnh "bàn làm việc": nhân vật ngồi ghế quay lưng về người xem, laptop bên phải trên bàn.
Tay gần (trái) tính bằng IK hai đoạn từ vai tới điểm đích của bàn tay; tay xa chỉ thấy bàn tay trên phím.
Mỗi động tác là một chuỗi khoá tư thế; nội suy có giữ nhịp (giật nhẹ 12 hình/giây, kiểu hoạt hình cắt giấy).

render(width, height, t, actions, opts) -> ảnh RGBA. t là giây tính từ đầu cảnh.
actions: [{"do": "power_on"|"type"|"phone_look"|"phone_down"|"head_hands"|"idle"|"slump", "at": giây, "len": giây}]
opts: {"screen": "off"|"on"|"chart_up"|"chart_down"|"red"|"green", "night": bool, "phone": "desk"|"down"|"none"}
Thuần Pillow, không mạng.
"""

from __future__ import annotations

import math
from PIL import Image, ImageDraw, ImageFilter

VW, VH = 1600, 900  # khung ảo; vẽ x2 rồi thu nhỏ
SS = 2
HAIR = (34, 30, 32)
SKIN = (222, 176, 140)
SKIN_D = (190, 142, 110)
POLO = (238, 241, 246)
POLO_D = (196, 204, 216)
PANTS = (150, 156, 168)
LEATHER = (120, 72, 40)
STEEL = (210, 216, 224)
WOOD = (104, 72, 50)
WOOD_D = (78, 54, 38)
CHAIR = (44, 48, 60)
RED = (240, 84, 100)
GREEN = (64, 196, 136)
STEP = 1 / 12  # nhịp hoạt hình cắt giấy

SHOULDER = (640, 430)  # vai gần, toạ độ khi ngồi thẳng
HIP = (690, 640)
L1, L2 = 140, 132  # cánh tay trên, cẳng tay


def _ease(p):
    p = max(0.0, min(1.0, p))
    return p * p * (3 - 2 * p)


def _lerp(a, b, p):
    return a + (b - a) * p


def _lerp2(a, b, p):
    return (_lerp(a[0], b[0], p), _lerp(a[1], b[1], p))


def _rot(pt, origin, deg):
    a = math.radians(deg)
    x, y = pt[0] - origin[0], pt[1] - origin[1]
    return (origin[0] + x * math.cos(a) - y * math.sin(a), origin[1] + x * math.sin(a) + y * math.cos(a))


def _ik(shoulder, target, elbow_down=True):
    dx, dy = target[0] - shoulder[0], target[1] - shoulder[1]
    d = max(1e-3, min(L1 + L2 - 1, math.hypot(dx, dy)))
    a = math.atan2(dy, dx)
    cos_b = (L1 * L1 + d * d - L2 * L2) / (2 * L1 * d)
    b = math.acos(max(-1, min(1, cos_b)))
    ang = a + (b if elbow_down else -b)
    elbow = (shoulder[0] + L1 * math.cos(ang), shoulder[1] + L1 * math.sin(ang))
    hand = (shoulder[0] + d * math.cos(a), shoulder[1] + d * math.sin(a))
    return elbow, hand


# Tư thế: lean (độ, + là chồm về trước/phải), head (độ nghiêng đầu), near (đích tay gần), far (đích tay xa), phone_in_hand
POSES = {
    "idle": {"lean": 0, "head": 0, "near": (800, 560), "far": (905, 552), "phone": False},
    "reach": {"lean": 9, "head": 4, "near": (1000, 540), "far": (905, 552), "phone": False},
    "type": {"lean": 4, "head": 3, "near": (880, 548), "far": (960, 548), "phone": False},
    "phone_up": {"lean": -2, "head": 10, "near": (735, 330), "far": (905, 552), "phone": True},
    "phone_desk": {"lean": 6, "head": 4, "near": (1150, 540), "far": (905, 552), "phone": True},
    "head_hands": {"lean": -10, "head": 16, "near": (610, 300), "far": (705, 300), "phone": False},
    "slump": {"lean": -4, "head": 22, "near": (760, 600), "far": (870, 590), "phone": False},
}

# Động tác = các khoá (thời điểm 0..1 trong động tác, tư thế)
ACTIONS = {
    "idle": [(0, "idle"), (1, "idle")],
    "power_on": [(0, "idle"), (.45, "reach"), (.6, "reach"), (1, "idle")],
    "type": [(0, "type"), (1, "type")],
    "phone_look": [(0, "idle"), (.25, "phone_desk"), (.5, "phone_up"), (1, "phone_up")],
    "phone_down": [(0, "phone_up"), (.5, "phone_desk"), (1, "idle")],
    "head_hands": [(0, "idle"), (.35, "head_hands"), (1, "head_hands")],
    "slump": [(0, "idle"), (.4, "slump"), (1, "slump")],
}


def _pose_at(t, actions):
    """Tư thế tại thời điểm t, theo động tác đang chạy hoặc đã chạy xong gần nhất."""
    cur = POSES["idle"]
    events = []
    for act in sorted(actions, key=lambda a: float(a.get("at", 0))):
        at, ln = float(act.get("at", 0)), max(.2, float(act.get("len", 1.5)))
        keys = ACTIONS.get(str(act.get("do")), ACTIONS["idle"])
        if t < at:
            break
        p = min(1.0, (t - at) / ln)
        for (k0, n0), (k1, n1) in zip(keys, keys[1:]):
            if k0 <= p <= k1:
                q = _ease((p - k0) / max(1e-6, k1 - k0))
                a, b = POSES[n0], POSES[n1]
                cur = {
                    "lean": _lerp(a["lean"], b["lean"], q), "head": _lerp(a["head"], b["head"], q),
                    "near": _lerp2(a["near"], b["near"], q), "far": _lerp2(a["far"], b["far"], q),
                    "phone": (b["phone"] if q > .5 else a["phone"]),
                }
                break
        events.append((str(act.get("do")), p))
        typing = act.get("do") == "type" and p < 1
        if typing:
            jig = math.sin(t * 34) * 7, math.cos(t * 29) * 6
            cur = {**cur, "near": (cur["near"][0], cur["near"][1] + jig[0]), "far": (cur["far"][0], cur["far"][1] + jig[1])}
    return cur, events


def _capsule(d, a, b, r, fill):
    d.line([(a[0] * SS, a[1] * SS), (b[0] * SS, b[1] * SS)], fill=fill, width=int(r * 2 * SS))
    for p in (a, b):
        d.ellipse([(p[0] - r) * SS, (p[1] - r) * SS, (p[0] + r) * SS, (p[1] + r) * SS], fill=fill)


def _poly(d, pts, fill):
    d.polygon([(x * SS, y * SS) for x, y in pts], fill=fill)


def _ell(d, box, fill):
    d.ellipse([v * SS for v in box], fill=fill)


def _rect(d, box, fill, r=0):
    d.rounded_rectangle([v * SS for v in box], radius=r * SS, fill=fill)


def _screen_color(state, t, power_t):
    if state == "off":
        return (24, 28, 38)
    if power_t is not None and t < power_t:
        return (24, 28, 38)
    return {"red": RED, "chart_down": RED, "green": GREEN, "chart_up": GREEN}.get(state, (70, 110, 170))


def render(width, height, t, actions=None, opts=None):
    actions = list(actions or [])
    opts = dict(opts or {})
    t = math.floor(t / STEP) * STEP  # giật nhẹ kiểu cắt giấy
    pose, events = _pose_at(t, actions)
    power = next((float(a.get("at", 0)) + .55 * float(a.get("len", 1.5)) for a in actions if a.get("do") == "power_on"), None)
    state = str(opts.get("screen", "on"))
    if state != "off" and power is not None and t < power:
        state_now = "off"
    else:
        state_now = state
    glow = _screen_color(state_now, t, None)
    night = bool(opts.get("night", True))

    img = Image.new("RGBA", (VW * SS, VH * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # phòng
    wall_top, wall_bot = ((14, 22, 42), (22, 34, 60)) if night else ((52, 70, 104), (70, 92, 130))
    for y in range(0, VH, 6):
        c = tuple(int(_lerp(wall_top[i], wall_bot[i], y / VH)) for i in range(3))
        d.rectangle([0, y * SS, VW * SS, (y + 6) * SS], fill=c + (255,))
    # cửa sổ đêm bên trái
    _rect(d, (90, 120, 420, 470), (10, 16, 30) if night else (150, 190, 230), 10)
    if night:
        for i, (x, y) in enumerate(((140, 380), (200, 360), (260, 400), (320, 370), (370, 410), (170, 420), (300, 430))):
            _rect(d, (x, y, x + 18, y + 26), (250, 210, 120) if i % 2 else (200, 220, 250), 3)
    _rect(d, (84, 114, 426, 124), (60, 70, 90), 4)
    _rect(d, (250, 114, 260, 476), (60, 70, 90), 2)
    # ánh màn hình hắt lên tường
    if state_now != "off":
        halo = Image.new("RGBA", img.size, (0, 0, 0, 0))
        hd = ImageDraw.Draw(halo)
        hd.ellipse([760 * SS, 150 * SS, 1420 * SS, 700 * SS], fill=glow + (70 if night else 30,))
        img.alpha_composite(halo.filter(ImageFilter.GaussianBlur(60 * SS)))
    # bàn
    _rect(d, (720, 530, 1560, 560), WOOD, 8)
    _rect(d, (720, 556, 1560, 900), WOOD_D, 0)
    for x in (900, 1140, 1380):
        _rect(d, (x, 580, x + 4, 900), (66, 46, 32), 0)
    # laptop: đế + màn hình
    _poly(d, [(880, 545), (1200, 545), (1225, 532), (905, 532)], (60, 64, 74))
    _poly(d, [(915, 532), (1205, 532), (1185, 300), (935, 300)], (40, 44, 54))
    _poly(d, [(926, 524), (1194, 524), (1176, 310), (944, 310)], glow)
    if state_now in ("chart_up", "chart_down", "red", "green"):
        up = state_now in ("chart_up", "green")
        pts = []
        for i in range(9):
            u = i / 8
            base = u if up else 1 - u
            y = 500 - 170 * (.15 + .7 * base) + math.sin(i * 1.9) * 12
            pts.append(((950 + 220 * u) * SS, y * SS))
        d.line(pts, fill=(250, 250, 250), width=6 * SS)
    elif state_now == "on" and power is not None and t < power + 1.0:
        _ell(d, (1040, 400, 1080, 440), (230, 240, 255))  # biểu tượng khởi động
    # điện thoại trên bàn (khi không cầm)
    phone_mode = str(opts.get("phone", "desk"))
    phone_down = phone_mode == "down" or any(e == "phone_down" and p >= .5 for e, p in events)
    if not pose["phone"] and phone_mode != "none":
        _poly(d, [(1250, 538), (1330, 538), (1340, 528), (1260, 528)], (30, 32, 40) if phone_down else (20, 24, 32))
        if not phone_down:
            _poly(d, [(1256, 536), (1326, 536), (1334, 530), (1264, 530)], (240, 84, 100) if opts.get("phone_red", True) else glow)
    # khung người theo tư thế
    lean = pose["lean"]
    sh = _rot(SHOULDER, HIP, lean)
    sh_far = _rot((SHOULDER[0] + 120, SHOULDER[1] - 6), HIP, lean)
    neck = _rot((SHOULDER[0] + 58, SHOULDER[1] - 40), HIP, lean)
    # tay xa: chỉ bàn tay và một đoạn cẳng tay trên bàn, vẽ trước thân
    fe, fh = _ik(sh_far, pose["far"], True)
    _capsule(d, sh_far, fe, 21, POLO_D if True else SKIN)
    _capsule(d, fe, fh, 17, SKIN_D)
    _ell(d, (fh[0] - 19, fh[1] - 15, fh[0] + 19, fh[1] + 15), SKIN_D)
    # cổ (vẽ trước thân để cổ áo che)
    _capsule(d, neck, _rot((neck[0], neck[1] - 20), neck, pose["head"]), 24, SKIN_D)
    # thân (lưng áo polo)
    torso = [_rot(p, HIP, lean) for p in ((560, 452), (780, 442), (760, 660), (585, 670))]
    for c, r in (((585, 478), 32), ((757, 470), 30)):
        cc = _rot(c, HIP, lean)
        _ell(d, (cc[0] - r, cc[1] - r, cc[0] + r, cc[1] + r), POLO if c[0] < 700 else POLO_D)
    _poly(d, torso, POLO)
    _poly(d, [torso[1], (torso[1][0] - 70, torso[1][1] + 6), (torso[2][0] - 60, torso[2][1]), torso[2]], POLO_D)
    # đầu: nhìn chếch sau, tóc phủ, tai trái, không mặt
    hc = _rot((neck[0] - 8, neck[1] - 58), neck, pose["head"])
    ear = _rot((hc[0] - 76, hc[1] + 10), hc, pose["head"])
    _ell(d, (ear[0] - 15, ear[1] - 22, ear[0] + 15, ear[1] + 22), SKIN_D)
    _ell(d, (hc[0] - 80, hc[1] - 92, hc[0] + 80, hc[1] + 96), HAIR)
    # cổ áo
    _poly(d, [_rot(p, HIP, lean) for p in ((600, 436), (700, 428), (690, 452), (612, 458))], (226, 230, 238))
    # ghế: lưng ghế che phần dưới lưng
    _rect(d, (530, 600, 820, 760), CHAIR, 26)
    _rect(d, (650, 760, 690, 900), CHAIR, 6)
    _rect(d, (520, 860, 820, 885), CHAIR, 10)
    # tay gần: tay áo, cẳng tay, bàn tay, đồng hồ dây da
    ne, nh = _ik(sh, pose["near"], True)
    _capsule(d, sh, ne, 26, POLO)
    _capsule(d, _lerp2(sh, ne, .55), ne, 21, SKIN)
    _capsule(d, ne, nh, 19, SKIN)
    _ell(d, (nh[0] - 22, nh[1] - 18, nh[0] + 22, nh[1] + 18), SKIN)
    wr = _lerp2(ne, nh, .78)
    ang = math.degrees(math.atan2(nh[1] - ne[1], nh[0] - ne[0])) + 90
    band = [_rot(p, wr, ang) for p in ((wr[0] - 22, wr[1] - 9), (wr[0] + 22, wr[1] - 9), (wr[0] + 22, wr[1] + 9), (wr[0] - 22, wr[1] + 9))]
    _poly(d, band, LEATHER)
    _ell(d, (wr[0] - 13, wr[1] - 13, wr[0] + 13, wr[1] + 13), STEEL)
    _ell(d, (wr[0] - 9, wr[1] - 9, wr[0] + 9, wr[1] + 9), (250, 250, 246))
    if pose["phone"]:
        ph = [_rot(p, nh, -12) for p in ((nh[0] - 26, nh[1] - 58), (nh[0] + 26, nh[1] - 58), (nh[0] + 26, nh[1] + 30), (nh[0] - 26, nh[1] + 30))]
        _poly(d, ph, (24, 26, 34))
        scr = [_rot(p, nh, -12) for p in ((nh[0] - 20, nh[1] - 50), (nh[0] + 20, nh[1] - 50), (nh[0] + 20, nh[1] + 22), (nh[0] - 20, nh[1] + 22))]
        _poly(d, scr, RED if opts.get("phone_red", True) else glow)
        _ell(d, (nh[0] - 22, nh[1] - 4, nh[0] + 18, nh[1] + 26), SKIN)
    z = float(opts.get("zoom", 1.32)) + .03 * min(1.0, t / 12)  # máy quay áp sát, đẩy chậm
    cx, cy = opts.get("focus", (860, 520))
    cw, ch = VW / z, VH / z
    x0 = max(0, min(VW - cw, cx - cw / 2)); y0 = max(0, min(VH - ch, cy - ch / 2))
    img = img.crop((int(x0 * SS), int(y0 * SS), int((x0 + cw) * SS), int((y0 + ch) * SS)))
    out = img.resize((VW, VH), Image.LANCZOS)
    if (width, height) != (VW, VH):
        scale = min(width / VW, height / VH)
        out = out.resize((int(VW * scale), int(VH * scale)), Image.LANCZOS)
        canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        canvas.alpha_composite(out, ((width - out.width) // 2, (height - out.height) // 2))
        out = canvas
    return out

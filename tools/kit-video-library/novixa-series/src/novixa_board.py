"""Mẫu cảnh `nv_board`: ghép hình minh hoạ vẽ bằng code theo từng câu đọc, ít chữ.

Sidecar `.hinh.json` ghi cảnh như sau (toạ độ tính theo % vùng an toàn của cảnh):

    {"first": "Câu mở cảnh", "template": "nv_board", "params": {
        "title": "Tiêu đề ngắn (tuỳ chọn)",
        "items": [
            {"art": "shelf", "x": 50, "y": 55, "s": 90, "w": 70},
            {"art": "box", "x": 30, "y": 60, "s": 26, "args": {"color": "cyan", "hsd": "HSD 03/2026", "red": true},
             "line": 1, "from": "pop", "glow": "red", "label": "Đã qua hạn", "tone": "red",
             "move": [{"line": 3, "x": 60, "y": 60}], "hide": {"line": 4}}
        ]}}

- `x`, `y`: tâm hình; `s`: chiều cao (% chiều cao vùng); `w`: chiều rộng (% chiều rộng vùng), bỏ trống thì theo tỉ lệ hình.
- `line`: hình hiện ra khi câu thứ `line` của cảnh bắt đầu (0 = câu đầu). Không ghi thì hiện theo thời gian `d`.
- `from`: pop | rise | drop | left | right | fade | flip | none (có sẵn từ khung đầu). `move`, `hide`: đổi chỗ, ẩn đi ở câu sau.
- `wide`/`tall`: ghi đè x/y/s/w riêng cho khung ngang hoặc dọc.

Không gọi mạng, không tốn phí. Chữ trên hình chỉ là nhãn ngắn do người duyệt ghi trong sidecar.
"""

from __future__ import annotations

import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import novixa_illus as il  # noqa: E402

COLORS = {
    "cyan": il.CYAN, "green": il.GREEN, "orange": il.ORANGE, "red": il.RED, "purple": il.PURPLE,
    "yellow": il.YEL, "grey": il.GREY, "white": il.WHITE, "navy": il.NAVY, "ink": il.INK,
}
MIN_LABEL = {"tall": 34, "wide": 32}
PEOPLE_SCALE = 1.22
BOARD_AR = (1 - 0.12) / (1 - 0.125 - 0.30) * 1920 / 1080  # tỉ lệ vùng cảnh của khung ngang


def _col(value, fallback=il.CYAN):
    if isinstance(value, (list, tuple)) and len(value) >= 3:
        return tuple(int(v) for v in value[:3])
    return COLORS.get(str(value or "").lower(), fallback)


def _ease(p: float) -> float:
    p = max(0.0, min(1.0, p))
    return 1 - (1 - p) ** 3


def _back(p: float) -> float:
    p = max(0.0, min(1.0, p))
    c1 = 1.70158
    return 1 + (c1 + 1) * (p - 1) ** 3 + c1 * (p - 1) ** 2


# ---------------- hình bổ sung (bản 29/10 chưa có) ----------------
def _customer_img(w, h, mood="ok", shirt=(236, 120, 96)):
    cv = il.Cv(w, h)
    cx = w / 2
    cv.shadow((w * .1, h * .93, w * .9, h * 1.06), 10, 120)
    cv.poly([(w * .12, h), (w * .2, h * .58), (w * .8, h * .58), (w * .88, h)], fill=shirt)
    cv.poly([(cx - w * .1, h * .58), (cx, h * .68), (cx + w * .1, h * .58)], fill=il.SKIN)
    cv.rect((cx - w * .06, h * .5, cx + w * .06, h * .6), fill=il.SKIN)
    cv.ell((cx - w * .22, h * .12, cx + w * .22, h * .54), fill=il.SKIN)
    cv.d.pieslice([(cx - w * .24) * il.SS, (h * .06) * il.SS, (cx + w * .24) * il.SS, (h * .40) * il.SS], 180, 360, fill=(90, 70, 60))
    ey = h * .32
    for sx in (-1, 1):
        ex = cx + sx * w * .09
        cv.ell((ex - w * .022, ey - w * .026, ex + w * .022, ey + w * .026), fill=(30, 30, 40))
        if mood == "angry":
            cv.line([(ex - sx * w * .06, ey - h * .07), (ex + sx * w * .03, ey - h * .045)], (60, 44, 44), 4)
    my = h * .43
    if mood == "angry":
        cv.arc((cx - w * .07, my, cx + w * .07, my + h * .05), 200, 340, (150, 60, 60), 4)
    elif mood == "worry":
        cv.line([(cx - w * .05, my + h * .01), (cx + w * .05, my)], (150, 60, 60), 4)
    else:
        cv.arc((cx - w * .08, my - h * .03, cx + w * .08, my + h * .04), 20, 160, (150, 60, 60), 4)
    return cv.out()


def _counter_img(w, h):
    cv = il.Cv(w, h)
    cv.shadow((w * .02, h * .9, w * .98, h * 1.08), 12, 120)
    cv.grad((0, 0, w, h * .22), (214, 226, 242), (176, 192, 216), r=10)
    cv.grad((w * .02, h * .2, w * .98, h), (64, 98, 150), (40, 64, 106), r=8)
    cv.rect((w * .06, h * .34, w * .94, h * .36), fill=(96, 132, 186))
    return cv.out()


def _bag_img(w, h):
    cv = il.Cv(w, h)
    cv.shadow((w * .08, h * .92, w * .92, h * 1.06), 10, 120)
    cv.grad((w * .06, h * .2, w * .94, h), (70, 78, 96), (40, 46, 60), r=w * .14)
    cv.poly([(w * .3, h * .22), (w * .42, h * .02), (w * .5, h * .2), (w * .58, h * .02), (w * .7, h * .22)], fill=(84, 92, 112))
    cv.rect((w * .32, h * .18, w * .68, h * .26), fill=(30, 34, 46), r=6)
    return cv.out()


def _note_img(w, h, text="", color=il.CYAN, ink=il.INK, size=None):
    cv = il.Cv(w, h)
    cv.rect((0, 0, w, h), fill=color, r=h * .5)
    fs = size or h * .5
    cv.text(w / 2, h / 2 - fs * .62, text, fs, ink, anchor="m")
    return cv.out()


def _tray_img(w, h, color=il.CYAN):
    """Ô danh sách lô trên màn hình bán hàng (hình vẽ, không phải giao diện thật)."""
    cv = il.Cv(w, h)
    cv.rect((0, 0, w, h), fill=(24, 44, 82), outline=color, width=max(3, h * .05), r=h * .22)
    cv.rect((w * .05, h * .3, w * .12, h * .7), fill=color, r=h * .1)
    cv.rect((w * .18, h * .3, w * .62, h * .44), fill=(200, 214, 236), r=h * .06)
    cv.rect((w * .18, h * .56, w * .46, h * .68), fill=(140, 160, 194), r=h * .06)
    return cv.out()


def _slot_img(w, h, color=il.RED):
    """Chỗ trống trên kệ: khung nét đứt, nền mờ (món vừa bán hết)."""
    cv = il.Cv(w, h)
    cv.rect((0, 0, w, h), fill=(40, 54, 86), r=min(w, h) * .08)
    step, dash = h * .12, h * .07
    lw = max(3, min(w, h) * .035)
    y = 0.0
    while y < h:
        cv.line([(lw / 2, y), (lw / 2, min(h, y + dash))], color, lw)
        cv.line([(w - lw / 2, y), (w - lw / 2, min(h, y + dash))], color, lw)
        y += step
    x = 0.0
    while x < w:
        cv.line([(x, lw / 2), (min(w, x + dash), lw / 2)], color, lw)
        cv.line([(x, h - lw / 2), (min(w, x + dash), h - lw / 2)], color, lw)
        x += step
    return cv.out()


ASPECT = {
    "box": .78, "bottle": .6, "blister": 1.6, "coin": .92, "coins": 1.45, "bill": 2.0, "wallet": 1.36,
    "person": 1.09, "customer": 1.09, "store": 1.29, "shelf": 1.0, "cabinet": .48, "carton": 1.4, "calendar": .94,
    "clock": .93, "lock": .83, "mark": 1.0, "arrow": 3.0, "card": 1.4, "phone": .58, "receipt": .66, "mag": 1.0,
    "bubble": 2.4, "report": .79, "book": .79, "brain": 1.0, "megaphone": 1.0, "tag": 1.43, "cracked_coin": .9,
    "counter": 3.2, "screen": 1.6, "bag": .82, "note": 4.0, "tray": 3.6, "slot": .78, "scale": 1.6,
}


# Hình riêng kênh Người Giữ Vốn (nhân vật ẩn mặt, đồng hồ, hũ tích cóp...): tools/kit-video-library/ngv-edu/ngv_art.py
_NGV_ART: dict = {}
try:
    import importlib.util as _ilu

    _ngv_path = Path(__file__).resolve().parents[2] / "ngv-edu" / "ngv_art.py"
    if _ngv_path.is_file():
        _ngv_spec = _ilu.spec_from_file_location("ngv_art", _ngv_path)
        _ngv_mod = _ilu.module_from_spec(_ngv_spec)
        _ngv_spec.loader.exec_module(_ngv_mod)
        _NGV_ART = dict(getattr(_ngv_mod, "ART", {}))
        for _k, (_fn, _asp) in _NGV_ART.items():
            ASPECT.setdefault(_k, _asp)
except Exception:  # hình phụ không được làm hỏng cảnh Novixa
    _NGV_ART = {}


# Hình riêng kênh Between Two Asias (BTA): tools/kit-video-library/bta-series/src/bta_art.py
# ART: tên -> (hàm(w, h, args), tỉ lệ) hoặc (hàm, tỉ lệ, True) với hình "sống" (nhận thêm _t, _lt, _pos, _a, _dur
# để tự chuyển động: bánh xe quay, chữ hiện dần, ảnh có máy quay). BACKGROUNDS: phông riêng (ví dụ "bta_paper").
_BTA_ART: dict = {}
_BTA_LIVE: set = set()
_BTA_BG: dict = {}
try:
    import importlib.util as _ilu2

    _bta_path = Path(__file__).resolve().parents[2] / "bta-series" / "src" / "bta_art.py"
    if _bta_path.is_file():
        _bta_spec = _ilu2.spec_from_file_location("bta_art", _bta_path)
        _bta_mod = _ilu2.module_from_spec(_bta_spec)
        _bta_spec.loader.exec_module(_bta_mod)
        for _k, _v in dict(getattr(_bta_mod, "ART", {})).items():
            if _k in ASPECT or _k in _NGV_ART:
                continue  # không đè hình của Novixa / Người Giữ Vốn
            _BTA_ART[_k] = _v[0]
            ASPECT[_k] = float(_v[1])
            if len(_v) > 2 and _v[2]:
                _BTA_LIVE.add(_k)
        _BTA_BG = dict(getattr(_bta_mod, "BACKGROUNDS", {}))
except Exception:  # hình BTA lỗi không được làm hỏng cảnh Novixa, Người Giữ Vốn
    _BTA_ART, _BTA_LIVE, _BTA_BG = {}, set(), {}


_PEOPLE: list = []


def _people():
    """Nhân vật mới có tay và cử chỉ (novixa_people.py). Lỗi nạp thì quay về nhân vật cũ, không làm hỏng cảnh."""
    if not _PEOPLE:
        try:
            import novixa_people  # noqa: E402

            _PEOPLE.append(novixa_people)
        except Exception:
            _PEOPLE.append(None)
    return _PEOPLE[0]


_SCREENS: list = []


def _screens():
    """Màn Novixa vẽ lại (novixa_screens.py): quầy bán chọn lô, tồn kho, chốt ca, công nợ, giao ca."""
    if not _SCREENS:
        try:
            import novixa_screens  # noqa: E402

            _SCREENS.append(novixa_screens)
        except Exception:
            _SCREENS.append(None)
    return _SCREENS[0]


def _art(name: str, w: int, h: int, a: dict) -> Image.Image | None:
    w, h = max(8, int(w)), max(8, int(h))
    c = _col(a.get("color"), il.CYAN)
    if name == "box":
        return il.box_img(w, h, c, str(a.get("name", "Thuốc A")), str(a.get("hsd", "")), bool(a.get("red")), bool(a.get("dim")), bool(a.get("qm")))
    if name == "bottle":
        return il.bottle_img(w, h, _col(a.get("color"), il.ORANGE), bool(a.get("dim")))
    if name == "blister":
        return il.blister_img(w, h, _col(a.get("color"), il.GREEN))
    if name == "coin":
        return il.coin_img(max(6, int(min(w, h) * .45)))
    if name == "coins":
        n = int(a.get("n", 5))
        img = il.coins_img(n, 36)
        return img.resize((w, h), Image.LANCZOS)
    if name == "bill":
        return il.bill_img(w, h, _col(a.get("color"), il.GREEN))
    if name == "wallet":
        return il.wallet_img(w, h, bool(a.get("empty")))
    if name == "screen":
        screens = _screens()
        if screens is not None:
            return screens.screen(str(a.get("kind", "fefo")), w, h)
        return _tray_img(w, h, c)
    if name in ("person", "customer"):
        people = _people()
        if people is not None:
            args = {**a, "color": list(_col(a.get("color"), (236, 120, 96)))} if name == "customer" else dict(a)
            return people.sprite(name, w, h, args)
        if name == "person":
            return il.person_img(w, h, str(a.get("mood", "ok")))
        return _customer_img(w, h, str(a.get("mood", "ok")), _col(a.get("color"), (236, 120, 96)))
    if name == "store":
        return il.store_img(w, h)
    if name == "shelf":
        return il.shelf_img(w, h, int(a.get("rows", 3)))
    if name == "cabinet":
        return il.cabinet_img(w, h)
    if name == "carton":
        return il.carton_img(w, h)
    if name == "calendar":
        return il.calendar_img(w, h, str(a.get("day", "30")), str(a.get("stamp", "HSD")), str(a.get("month", "Tháng 11")), _col(a.get("color"), il.RED))
    if name == "clock":
        img = il.clock_img(120, int(a.get("hh", 9)), int(a.get("mm", 0)), c)
        return img.resize((w, h), Image.LANCZOS)
    if name == "lock":
        return il.lock_img(w, _col(a.get("color"), il.RED)).resize((w, h), Image.LANCZOS)
    if name == "mark":
        return il.mark_img(str(a.get("kind", "check")), min(w, h), _col(a.get("color"), il.GREEN if a.get("kind", "check") == "check" else il.RED))
    if name == "arrow":
        vertical = bool(a.get("vertical"))
        length = h if vertical else w
        thick = max(6, int((w if vertical else h) / 2.2 * .9))
        return il.arrow_img(length, thick, c, vertical)
    if name == "card":
        return il.card_img(w, h, c)
    if name == "phone":
        return il.phone_img(w, h)
    if name == "receipt":
        return il.receipt_img(w, h)
    if name == "mag":
        return il.mag_img(min(w, h))
    if name == "bubble":
        return il.bubble_img(w, h)
    if name == "report":
        return il.report_img(w, h, c)
    if name == "book":
        return il.book_img(w, h)
    if name == "brain":
        return il.brain_img(w, h)
    if name == "megaphone":
        return il.megaphone_img(min(w, h))
    if name == "tag":
        return il.tag_img(w, str(a.get("text", "-30%")), _col(a.get("color"), il.ORANGE)).resize((w, h), Image.LANCZOS)
    if name == "cracked_coin":
        return il.cracked_coin_img(max(10, int(min(w, h) * .45))).resize((w, h), Image.LANCZOS)
    if name == "counter":
        return _counter_img(w, h)
    if name == "bag":
        return _bag_img(w, h)
    if name == "slot":
        return _slot_img(w, h, _col(a.get("color"), il.RED))
    if name == "scale":
        return il.scale_img(w, h, float(a.get("tilt", 0.0)))[0]
    if name == "tray":
        return _tray_img(w, h, c)
    if name in _NGV_ART:
        return _NGV_ART[name][0](w, h, a)
    if name in _BTA_ART:
        try:
            return _BTA_ART[name](w, h, a)
        except Exception:  # một hình BTA lỗi: bỏ hình đó, cảnh vẫn dựng
            return None
    return None


_SPRITES: dict = {}
_GLOWS: dict = {}


def _sprite(name, w, h, args):
    key = (name, int(w), int(h), repr(sorted(args.items())))
    if key not in _SPRITES:
        if len(_SPRITES) > 400:
            _SPRITES.clear()
        _SPRITES[key] = _art(name, w, h, args)
    return _SPRITES[key]


def _glow(w, h, color):
    key = (int(w), int(h), color)
    if key not in _GLOWS:
        pad = int(max(w, h) * .35)
        img = Image.new("RGBA", (int(w) + pad * 2, int(h) + pad * 2), (0, 0, 0, 0))
        ImageDraw.Draw(img).ellipse((pad * .6, pad * .6, img.width - pad * .6, img.height - pad * .6), fill=(*color, 150))
        _GLOWS[key] = (img.filter(ImageFilter.GaussianBlur(pad * .45)), pad)
    return _GLOWS[key]


def _reveal(ctx, line, delay, length=0.45) -> float:
    """0..1: hình đã hiện được bao nhiêu. Theo câu đọc khi có `line`, theo thời gian khi không."""
    pos = getattr(ctx, "edu_pos", None)
    if line is None:
        start = 0.1 if delay is None else float(delay)
        return _ease((ctx.t - start) / length)
    if pos is None:
        return 1.0  # ảnh duyệt: cảnh đã đọc xong
    line = int(line)
    if pos > line:
        return 1.0
    if pos < line:
        return 0.0
    lt = float(getattr(ctx, "edu_lt", 1.0))
    return _ease((lt - float(delay or 0.0)) / length)


def _place(item: dict, orient: str) -> dict:
    over = item.get(orient)
    return {**item, **over} if isinstance(over, dict) else item


def nv_board(ctx, assets, repo_root, project_dir):
    del assets, repo_root, project_dir
    import kit_video.edu_scenes as scenes

    l, t, r, b, tall, base = scenes._frame(ctx)
    orient = "tall" if tall else "wide"
    aw, ah = r - l, b - t
    title = str(ctx.params.get("title") or "").strip()
    accent = ctx.color("accent")
    if title:
        size = max(MIN_LABEL[orient] + 6, int(ah * (0.050 if tall else 0.075)))
        a = _ease((ctx.t - 0.05) / 0.5)
        ctx.text(title, (l, t + int((1 - a) * size * .4)), size, accent, bold=True, opacity=a)
        t += int(size * 1.7)
        ah = b - t
    if tall and not ctx.params.get("tallLayout"):
        # Bố cục ghi cho khung ngang: khung dọc đặt nguyên bảng hình vào giữa, đủ chiều ngang.
        l, aw = int(ctx.width * 0.02), int(ctx.width * 0.96)
        bh = min(ah, aw / BOARD_AR * float(ctx.params.get("tallZoom", 1.0)))
        t += (ah - bh) / 2
        ah = bh
    # Đẩy máy chậm cả khung: hình không đứng im giữa các câu.
    push = 1.0 + 0.035 * _ease(ctx.t / max(ctx.duration, 1.0))
    cx0, cy0 = l + aw / 2, t + ah / 2
    for index, raw in enumerate(ctx.params.get("items") or []):
        item = _place(raw, orient)
        name = str(item.get("art") or "")
        args = item.get("args") or {}
        if name == "note":
            args = {**args, "text": str(item.get("text", ""))}
        line = item.get("line")
        # "from": "none": có sẵn từ khung đầu (cảnh câu móc: giây 0 phải đọc được ngay).
        a_in = 1.0 if str(item.get("from") or "") == "none" else _reveal(ctx, line, item.get("d"))
        if a_in <= 0.01:
            continue
        hide = item.get("hide")
        opacity = 1.0
        if isinstance(hide, dict):
            opacity *= 1 - _reveal(ctx, hide.get("line"), hide.get("d"), 0.35)
            if opacity <= 0.02:
                continue
        x, y = float(item.get("x", 50)), float(item.get("y", 50))
        for step in item.get("move") or []:
            k = _reveal(ctx, step.get("line"), step.get("d"), float(step.get("len", 0.8)))
            x += (float(step.get("x", x)) - x) * k
            y += (float(step.get("y", y)) - y) * k
        hh = ah * float(item.get("s", 30)) / 100 * push
        if name in ("person", "customer") and _people() is not None:
            hh *= PEOPLE_SCALE  # nhân vật mới vẽ cả vai và tay: phóng lên để mặt to như nhân vật cũ
        ww = aw * float(item["w"]) / 100 * push if item.get("w") else hh * ASPECT.get(name, 1.0)
        # "bleed": true (chỉ khung ngang): ảnh phủ kín cả khung, không bị vùng an toàn thu nhỏ thành dải
        bleed = bool(item.get("bleed")) and not tall
        if bleed:
            ww, hh = float(ctx.width), float(ctx.height)
        if name == "note":
            text = str(item.get("text", ""))
            fs = max(MIN_LABEL[orient], int(hh * .5))
            probe = ImageDraw.Draw(ctx.image)
            ww = probe.textlength(text, font=ctx.font(fs, True)) + fs * 1.6
            hh = fs * 1.9
            sprite = _pill(text, fs, _col(item.get("tone") or "yellow", il.YEL), ctx.font(fs, True), int(ww), int(hh))
            if hasattr(ctx, "sizes"):
                ctx.sizes.append(fs)
        elif name in _BTA_LIVE:
            # Hình sống của BTA: biết thời gian cảnh và tiến độ câu để tự chuyển động.
            live = {**args, "_t": round(float(ctx.t), 3), "_dur": float(ctx.duration), "_a": round(float(a_in), 3),
                    "_pos": getattr(ctx, "edu_pos", None), "_lt": round(float(getattr(ctx, "edu_lt", 1.0)), 3),
                    "_line": line, "_orient": orient}
            sprite = _art(name, ww, hh, live)
        else:
            sprite = _sprite(name, ww, hh, args)
        if sprite is None:
            continue
        px = cx0 + (l + aw * x / 100 - cx0) * push
        py = cy0 + (t + ah * y / 100 - cy0) * push
        if bleed:
            px, py = ctx.width / 2, ctx.height / 2
        if item.get("bob"):
            py += math.sin(ctx.t * 2.2 + index) * ah * 0.006
        effect = "fade" if bleed else str(item.get("from") or "pop")
        scale_x = scale_y = 1.0
        if effect == "pop":
            scale_x = scale_y = 0.55 + 0.45 * _back(a_in)
        elif effect == "rise":
            py += (1 - a_in) * ah * 0.08
        elif effect == "drop":
            py -= (1 - a_in) * ah * 0.12
        elif effect == "left":
            px -= (1 - a_in) * aw * 0.18
        elif effect == "right":
            px += (1 - a_in) * aw * 0.18
        elif effect == "flip":
            scale_x = max(0.04, a_in)
        if item.get("shake") is not None:
            k = _reveal(ctx, item.get("shake"), 0.0, 0.6)
            if 0 < k < 1:
                px += math.sin(k * math.pi * 8) * aw * 0.008 * (1 - k)
        if effect != "fade":
            opacity *= min(1.0, a_in * 1.6)
        else:
            opacity *= a_in
        rot = float(item.get("rot", 0) or 0)
        img = sprite
        if scale_x != 1.0 or scale_y != 1.0:
            img = img.resize((max(2, int(img.width * scale_x)), max(2, int(img.height * scale_y))), Image.BILINEAR)
        if rot:
            img = img.rotate(rot, expand=True, resample=Image.BICUBIC)
        glow = item.get("glow")
        if glow:
            g, pad = _glow(sprite.width, sprite.height, _col(glow, il.RED))
            pulse = 0.65 + 0.35 * math.sin(ctx.t * 3.4)
            ctx.blit(g, (px - g.width / 2, py - g.height / 2), opacity * pulse)
        ctx.blit(img, (px - img.width / 2, py - img.height / 2), opacity)
        label = str(item.get("label") or "").strip()
        if label:
            fs = max(MIN_LABEL[orient], int(ah * (0.036 if tall else 0.058)))
            tone = _col(item.get("tone") or "yellow", il.YEL)
            probe = ImageDraw.Draw(ctx.image)
            lw = probe.textlength(label, font=ctx.font(fs, True)) + fs * 1.2
            lh = fs * 1.6
            ly = py + img.height / 2 + fs * 0.35 if item.get("labelAt") != "top" else py - img.height / 2 - lh - fs * 0.3
            pill = _pill(label, fs, tone, ctx.font(fs, True), int(lw), int(lh))
            la = opacity * _reveal(ctx, item.get("labelLine", line), (float(item.get("d") or 0) + 0.25) if line is not None else (float(item.get("d") or 0.1) + 0.25))
            ctx.sizes.append(fs) if hasattr(ctx, "sizes") else None
            ctx.blit(pill, (px - lw / 2, ly), la)


_PILLS: dict = {}


def _pill(text, size, tone, font, w, h):
    """Nhãn bo tròn, chữ căn giữa theo khung chữ thật (chữ có dấu tiếng Việt không bị lệch)."""
    key = (text, size, tone, w, h)
    if key not in _PILLS:
        img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        d.rounded_rectangle((0, 0, w - 1, h - 1), radius=h // 2, fill=(*tone, 255))
        box = d.textbbox((0, 0), text, font=font)
        d.text(((w - (box[2] - box[0])) / 2 - box[0], (h - (box[3] - box[1])) / 2 - box[1]), text, font=font, fill=(*il.INK, 255))
        _PILLS[key] = img
    return _PILLS[key]



# ---------------- phông quầy thuốc (cập nhật 09/11/2026) ----------------
# Thay nền xanh trơn bằng không gian nhà thuốc mờ phía sau: tường kệ thuốc, đèn trần, mặt quầy.
# Bật bằng `"background": "pharmacy"` trong file hình, hoặc `series.background` trong kenh.json.
# `"time": "evening"` trong params của cảnh đổi sang ánh đèn tối (cảnh tối, cuối ca).
_ROOMS: dict = {}


def _room(w: int, h: int, evening: bool = False) -> Image.Image:
    key = (w, h, evening)
    if key in _ROOMS:
        return _ROOMS[key]
    import random

    rnd = random.Random(29)
    tall = h > w
    top, bot = ((26, 52, 92), (9, 20, 44)) if not evening else ((20, 30, 58), (6, 12, 28))
    img = Image.new("RGBA", (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        k = y / max(1, h - 1)
        d.line([(0, y), (w, y)], fill=(*il.mix(top, bot, k), 255))
    # Tường kệ thuốc phía sau (vẽ rồi làm mờ như máy quay lấy nét ở phía trước).
    wall = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dw = ImageDraw.Draw(wall)
    y0, y1 = (0.10, 0.62) if not tall else (0.12, 0.70)
    rows = 4 if not tall else 6
    bay = int(w * (0.19 if not tall else 0.30))
    gap = int(bay * 0.06)
    palette = [il.CYAN, il.GREEN, il.ORANGE, il.PURPLE, (236, 120, 96), il.WHITE]
    x = -int(bay * 0.35)
    while x < w:
        dw.rectangle((x, h * y0, x + bay - gap, h * y1), fill=(34, 60, 102, 150))
        for r_ in range(rows):
            sy = h * (y0 + (r_ + 1) * (y1 - y0) / rows)
            dw.rectangle((x, sy - h * 0.008, x + bay - gap, sy), fill=(120, 150, 196, 150))
            bx = x + bay * 0.04
            while bx < x + bay - gap - bay * 0.06:
                bw = bay * rnd.uniform(0.07, 0.12)
                bh = (y1 - y0) * h / rows * rnd.uniform(0.45, 0.72)
                c = palette[rnd.randrange(len(palette))]
                c = il.mix(c, (60, 80, 120), 0.45)
                dw.rectangle((bx, sy - h * 0.008 - bh, bx + bw, sy - h * 0.008), fill=(*c, 96))
                dw.rectangle((bx, sy - h * 0.008 - bh, bx + bw, sy - h * 0.008 - bh * 0.28), fill=(*il.mix(c, (255, 255, 255), 0.25), 96))
                bx += bw + bay * rnd.uniform(0.008, 0.02)
        x += bay
    # Bảng hiệu chữ thập xanh trên tường.
    cx, cy, cr = w * (0.86 if not tall else 0.80), h * (0.07 if not tall else 0.075), h * (0.045 if not tall else 0.026)
    dw.ellipse((cx - cr, cy - cr, cx + cr, cy + cr), fill=(64, 196, 136, 170))
    dw.rectangle((cx - cr * .62, cy - cr * .2, cx + cr * .62, cy + cr * .2), fill=(240, 255, 248, 200))
    dw.rectangle((cx - cr * .2, cy - cr * .62, cx + cr * .2, cy + cr * .62), fill=(240, 255, 248, 200))
    wall = wall.filter(ImageFilter.GaussianBlur(max(4, w * 0.0062)))
    img.alpha_composite(wall)
    # Đèn trần: quầng sáng ấm trên đầu kệ.
    light = Image.new("L", (w, h), 0)
    dl = ImageDraw.Draw(light)
    spots = 3 if not tall else 2
    for i in range(spots):
        lx = w * (i + 0.5) / spots
        dl.ellipse((lx - w * 0.16, -h * 0.12, lx + w * 0.16, h * (0.30 if not tall else 0.22)), fill=70 if not evening else 40)
    light = light.filter(ImageFilter.GaussianBlur(w * 0.06))
    warm = Image.new("RGBA", (w, h), (255, 236, 196, 255) if not evening else (255, 196, 140, 255))
    warm.putalpha(light)
    img.alpha_composite(warm)
    # Mặt quầy ngang khung. Khung ngang: ngay trên vùng phụ đề. Khung dọc: ngay dưới bảng hình (bảng đặt giữa khung),
    # phần dưới là mặt trước quầy có ngăn kéo, để khung không trống.
    qy = h * (0.665 if not tall else 0.505)
    dq = ImageDraw.Draw(img)
    front_top, front_bot = ((12, 26, 54), (8, 18, 40)) if not evening else ((8, 16, 36), (5, 10, 24))
    for yy in range(int(qy), h):
        k = (yy - qy) / max(1, h - qy)
        dq.line([(0, yy), (w, yy)], fill=(*il.mix(front_top, front_bot, k), 255))
    if tall:
        # Mặt trước quầy: các ô ngăn kéo, tay nắm, dải xanh và chữ thập, dừng trước vùng phụ đề.
        py0, py1 = qy + h * 0.03, h * 0.745
        cols = 3
        cw = w / cols
        for i in range(cols):
            x0, x1 = i * cw + w * 0.02, (i + 1) * cw - w * 0.02
            dq.rounded_rectangle((x0, py0, x1, py1), radius=int(w * 0.012), fill=(22, 42, 80, 255), outline=(46, 74, 120, 255), width=3)
            for j in range(2):
                hy = py0 + (py1 - py0) * (0.28 + j * 0.44)
                dq.rounded_rectangle(((x0 + x1) / 2 - w * 0.05, hy - h * 0.004, (x0 + x1) / 2 + w * 0.05, hy + h * 0.004), radius=4, fill=(120, 150, 196, 255))
        dq.rectangle((0, py0 - h * 0.012, w, py0 - h * 0.004), fill=(64, 196, 136, 255))
    dq.rectangle((0, qy - h * 0.012, w, qy + h * 0.004), fill=(150, 176, 214, 255) if not evening else (100, 120, 156, 255))
    dq.rectangle((0, qy + h * 0.004, w, qy + h * 0.012), fill=(70, 98, 146, 255))
    # Tối dần về mép để mắt tập trung vào giữa.
    vig = Image.new("L", (w, h), 0)
    dv = ImageDraw.Draw(vig)
    dv.ellipse((-w * 0.25, -h * 0.2, w * 1.25, h * 1.05), fill=255)
    vig = vig.filter(ImageFilter.GaussianBlur(w * 0.08))
    dark = Image.new("RGBA", (w, h), (4, 10, 24, 255))
    dark.putalpha(Image.eval(vig, lambda v: int((255 - v) * 0.55)))
    img.alpha_composite(dark)
    _ROOMS[key] = img
    return img


def pharmacy_background(ctx) -> None:
    evening = str(ctx.params.get("time") or "").lower() in ("evening", "night", "toi")
    ctx.image.alpha_composite(_room(ctx.width, ctx.height, evening))


BACKGROUNDS = {"pharmacy": pharmacy_background}
BACKGROUNDS.update({k: v for k, v in _BTA_BG.items() if k not in BACKGROUNDS})


TEMPLATES = {"nv_board": nv_board}

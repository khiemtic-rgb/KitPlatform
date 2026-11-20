"""Màn Novixa vẽ lại cho video (cập nhật 09/11/2026).

Vẽ lại đơn giản theo bố cục và màu của phần mềm thật (bản demo): thanh bên xanh đậm, nền xám nhạt, thẻ trắng,
nút xanh. Không menu chi tiết. Số liệu là số minh hoạ của một nhà thuốc bán khoảng 5–10 triệu đồng mỗi ngày.
Mỗi màn có đúng một chỗ được tô sáng (dòng cam / đỏ) để người xem nhìn vào.

Dùng trong file hình: {"art": "screen", "args": {"kind": "fefo"}} với kind:
- fefo: quầy bán, chọn lô, lô sắp hết hạn được gợi ý lấy trước.
- stock: danh sách hàng, món sắp hết và món nằm im.
- shift: chốt ca, tiền trong két so với số đã ghi, chỗ lệch.
- debt: công nợ khách quen.
- handover: ghi chú giao ca, ca sau thấy việc ca trước.
"""

from __future__ import annotations

import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

SS = 2
BG = (243, 245, 249)
SIDE = (18, 34, 66)
SIDE_ON = (37, 99, 235)
CARD = (255, 255, 255)
LINE = (226, 231, 240)
INK = (24, 32, 48)
MUTED = (118, 128, 146)
BLUE = (37, 99, 235)
GREEN = (22, 163, 74)
ORANGE = (234, 120, 20)
ORANGE_BG = (255, 241, 225)
RED = (220, 38, 38)
RED_BG = (254, 228, 228)
GREY_BG = (236, 239, 244)
PURPLE = (124, 58, 237)
PURPLE_BG = (240, 233, 255)

_FB = ["C:/Windows/Fonts/segoeuib.ttf", "/usr/share/fonts/opentype/inter/Inter-Bold.otf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"]
_FR = ["C:/Windows/Fonts/segoeui.ttf", "/usr/share/fonts/opentype/inter/Inter-Medium.otf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"]
_FC: dict = {}

LOGICAL = (1280, 800)
ASPECT = LOGICAL[0] / LOGICAL[1]


def _f(size, bold=False):
    key = (int(size * SS), bold)
    if key not in _FC:
        for p in (_FB if bold else _FR):
            if os.path.exists(p):
                _FC[key] = ImageFont.truetype(p, int(size * SS))
                break
    return _FC[key]


class _P:
    def __init__(self, img):
        self.d = ImageDraw.Draw(img, "RGBA")

    def rect(self, x0, y0, x1, y1, fill=None, r=0, outline=None, width=1):
        self.d.rounded_rectangle([x0 * SS, y0 * SS, x1 * SS, y1 * SS], int(r * SS), fill=fill, outline=outline, width=int(width * SS))

    def ell(self, cx, cy, r, fill):
        self.d.ellipse([(cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS], fill=fill)

    def text(self, x, y, s, size, fill=INK, bold=False, anchor="la"):
        self.d.text((x * SS, y * SS), s, font=_f(size, bold), fill=fill, anchor=anchor)

    def pill(self, x, y, s, size, fg, bg, anchor="l"):
        w = self.d.textlength(s, font=_f(size, True)) / SS + size * 1.2
        h = size * 1.7
        x0 = x if anchor == "l" else x - w if anchor == "r" else x - w / 2
        self.rect(x0, y - h / 2, x0 + w, y + h / 2, bg, h / 2)
        self.text(x0 + w / 2, y, s, size, fg, True, "mm")
        return w


def _frame(p, title, active):
    w, h = LOGICAL
    p.rect(0, 0, w, h, BG, 18)
    p.rect(0, 0, 92, h, SIDE, 18)
    p.rect(60, 0, 92, h, SIDE)
    p.rect(22, 22, 70, 70, (255, 255, 255), 12)
    p.text(46, 46, "N", 30, SIDE, True, "mm")
    for i in range(7):
        y = 120 + i * 62
        on = i == active
        if on:
            p.rect(14, y - 22, 78, y + 22, SIDE_ON, 12)
        p.rect(36, y - 9, 56, y + 9, (255, 255, 255, 235 if on else 110), 4)
    p.rect(92, 0, w, 64, CARD)
    p.rect(92, 63, w, 65, LINE)
    p.text(122, 32, title, 24, INK, True, "lm")
    p.pill(w - 40, 32, "Nhà thuốc Minh An", 15, BLUE, (232, 240, 255), "r")


def _card(p, x0, y0, x1, y1):
    p.rect(x0, y0, x1, y1, CARD, 14, LINE, 1.5)


def _money(v):
    return f"{v:,.0f}".replace(",", ".") + " đ"


def _fefo(p):
    _frame(p, "Quầy bán", 1)
    _card(p, 116, 88, 900, 776)
    p.rect(140, 110, 876, 156, BG, 10)
    p.text(160, 133, "Tìm thuốc theo tên hoặc mã…", 18, MUTED, False, "lm")
    cols = [(150, "Sản phẩm"), (450, "Lô"), (735, "SL"), (860, "Thành tiền")]
    for x, s in cols:
        p.text(x, 190, s, 16, MUTED, True, "rm" if s == "Thành tiền" else "lm")
    p.rect(140, 208, 876, 210, LINE)
    rows = [("Paracetamol 500mg", "Hộp", "LÔ B · HSD 08/2027", 2, 48000),
            ("Vitamin C 500mg", "Lọ", None, 1, 65000),
            ("Loratadin 10mg", "Vỉ", "LÔ A · HSD 05/2027", 3, 36000)]
    y = 250
    for name, unit, lot, qty, total in rows:
        p.text(150, y - 10, name, 20, INK, True, "lm")
        p.text(150, y + 16, unit, 15, MUTED, False, "lm")
        if lot:
            p.rect(450, y - 20, 700, y + 20, CARD, 8, LINE, 1.5)
            p.text(464, y, lot, 15, INK, False, "lm")
        else:
            p.rect(446, y - 24, 704, y + 24, CARD, 8, ORANGE, 3)
            p.text(464, y, "LÔ C · HSD 12/2026", 15, INK, True, "lm")
        p.text(735, y, str(qty), 20, INK, True, "lm")
        p.text(860, y, _money(total), 20, INK, True, "rm")
        y += 92
    # Danh sách lô mở ra dưới dòng Vitamin C: lô sắp hết hạn được gợi ý lấy trước.
    by = 250 + 92 + 30
    p.rect(440, by, 712, by + 190, CARD, 12, LINE, 1.5)
    p.rect(450, by + 10, 702, by + 66, ORANGE_BG, 10)
    p.text(462, by + 38, "LÔ C · 12/2026", 17, INK, True, "lm")
    p.pill(694, by + 38, "Lấy trước", 14, (255, 255, 255), ORANGE, "r")
    p.text(462, by + 100, "LÔ D · 09/2027", 17, MUTED, False, "lm")
    p.text(462, by + 156, "LÔ E · 02/2028", 17, MUTED, False, "lm")
    p.rect(450, by + 128, 702, by + 129, LINE)
    _card(p, 920, 88, 1256, 776)
    p.text(944, 128, "Tổng tiền hàng", 17, MUTED, False, "lm")
    p.text(1232, 128, _money(149000), 19, INK, True, "rm")
    p.text(944, 172, "Giảm giá", 17, MUTED, False, "lm")
    p.text(1232, 172, "0 đ", 19, INK, True, "rm")
    p.rect(944, 206, 1232, 208, LINE)
    p.text(944, 248, "Khách phải trả", 19, INK, True, "lm")
    p.rect(944, 276, 1232, 350, (232, 240, 255), 12)
    p.text(1216, 313, _money(149000), 32, BLUE, True, "rm")
    p.rect(944, 680, 1232, 744, BLUE, 12)
    p.text(1088, 712, "Thanh toán", 22, (255, 255, 255), True, "mm")


def _stock(p):
    _frame(p, "Tồn kho", 3)
    _card(p, 116, 88, 1256, 776)
    for i, (s, fg, bg) in enumerate([("Tất cả", INK, GREY_BG), ("Sắp hết · 6", ORANGE, ORANGE_BG), ("Nằm im · 9", PURPLE, PURPLE_BG)]):
        p.pill(140 + i * 190, 130, s, 16, fg, bg)
    head = [(150, "Sản phẩm"), (640, "Tồn"), (800, "Bán 30 ngày"), (1010, "Trạng thái")]
    for x, s in head:
        p.text(x, 190, s, 16, MUTED, True, "lm")
    p.rect(140, 208, 1232, 210, LINE)
    rows = [("Amlodipin 5mg", "Hộp", "4", "62", "Sắp hết", ORANGE, ORANGE_BG),
            ("Vitamin C 500mg", "Lọ", "38", "92", "Ổn", GREEN, (225, 246, 233)),
            ("Siro ho thảo dược", "Chai", "27", "0", "Nằm im 60 ngày", PURPLE, PURPLE_BG),
            ("Men vi sinh", "Hộp", "3", "58", "Sắp hết", ORANGE, ORANGE_BG),
            ("Dầu gió xanh", "Chai", "45", "31", "Ổn", GREEN, (225, 246, 233)),
            ("Kem chống nắng", "Tuýp", "18", "0", "Nằm im 90 ngày", PURPLE, PURPLE_BG)]
    y = 252
    for i, (name, unit, qty, sold, st, fg, bg) in enumerate(rows):
        if i == 0:
            p.rect(140, y - 38, 1232, y + 38, ORANGE_BG, 10)
        p.text(150, y - 9, name, 20, INK, True, "lm")
        p.text(150, y + 16, unit, 14, MUTED, False, "lm")
        p.text(640, y, qty, 20, ORANGE if st == "Sắp hết" else INK, True, "lm")
        p.text(800, y, sold, 20, INK, False, "lm")
        p.pill(1010, y, st, 15, fg, bg)
        p.rect(140, y + 44, 1232, y + 45, LINE)
        y += 86


def _shift(p):
    _frame(p, "Chốt ca", 1)
    _card(p, 116, 88, 640, 776)
    p.text(140, 128, "Ca chiều · 14:00 – 21:00", 20, INK, True, "lm")
    p.pill(620, 128, "Đang chốt", 14, BLUE, (232, 240, 255), "r")
    items = [("Tổng thu bán", 7850000), ("Tiền mặt", 5120000), ("Chuyển khoản", 2730000), ("Quỹ đầu ca", 1000000)]
    y = 196
    for s, v in items:
        p.text(140, y, s, 18, MUTED, False, "lm")
        p.text(616, y, _money(v), 20, INK, True, "rm")
        p.rect(140, y + 30, 616, y + 31, LINE)
        y += 66
    p.text(140, y + 6, "Tiền mặt phải có trong két", 18, INK, True, "lm")
    p.text(616, y + 6, _money(6120000), 22, INK, True, "rm")
    _card(p, 660, 88, 1256, 776)
    p.text(684, 128, "Đếm két cuối ca", 20, INK, True, "lm")
    p.rect(684, 180, 1232, 270, GREY_BG, 12)
    p.text(708, 225, "Đã đếm", 18, MUTED, False, "lm")
    p.text(1208, 225, _money(6070000), 26, INK, True, "rm")
    p.rect(684, 296, 1232, 410, RED_BG, 14)
    p.text(708, 336, "Chênh lệch", 18, RED, True, "lm")
    p.text(1208, 352, "−" + _money(50000), 34, RED, True, "rm")
    p.text(708, 380, "Cần ghi lý do trước khi chốt", 15, RED, False, "lm")
    p.rect(684, 440, 1232, 540, CARD, 12, LINE, 1.5)
    p.text(708, 474, "Lý do", 15, MUTED, False, "lm")
    p.text(708, 508, "Lấy tạm trả tiền giao hàng lúc 17:40", 18, INK, False, "lm")
    p.rect(684, 680, 1232, 744, BLUE, 12)
    p.text(958, 712, "Chốt ca", 22, (255, 255, 255), True, "mm")


def _debt(p):
    _frame(p, "Công nợ khách", 4)
    _card(p, 116, 88, 1256, 776)
    p.text(140, 128, "Khách quen còn nợ", 20, INK, True, "lm")
    p.pill(1232, 128, "Tổng " + _money(745000), 16, BLUE, (232, 240, 255), "r")
    head = [(150, "Khách hàng"), (620, "Còn phải thu"), (860, "Lần mua gần nhất"), (1100, "Tuổi nợ")]
    for x, s in head:
        p.text(x, 190, s, 16, MUTED, True, "lm")
    p.rect(140, 208, 1232, 210, LINE)
    rows = [("Cô Lan", "Phường 4", 350000, "Hôm qua", "35 ngày", True),
            ("Chú Minh", "Gần chợ", 215000, "3 ngày trước", "12 ngày", False),
            ("Anh Tuấn", "Tổ 7", 120000, "Tuần trước", "8 ngày", False),
            ("Bà Sáu", "Hẻm 12", 60000, "Hôm nay", "Mới", False)]
    y = 256
    for name, sub, v, last, age, hot in rows:
        if hot:
            p.rect(140, y - 40, 1232, y + 40, ORANGE_BG, 10)
        p.ell(170, y, 22, (219, 228, 245))
        p.text(170, y, name.split()[-1][0], 18, BLUE, True, "mm")
        p.text(206, y - 10, name, 20, INK, True, "lm")
        p.text(206, y + 16, sub, 14, MUTED, False, "lm")
        p.text(620, y, _money(v), 20, ORANGE if hot else INK, True, "lm")
        p.text(860, y, last, 18, INK, False, "lm")
        p.pill(1100, y, age, 15, ORANGE if hot else MUTED, (255, 226, 200) if hot else GREY_BG)
        p.rect(140, y + 46, 1232, y + 47, LINE)
        y += 96


def _handover(p):
    _frame(p, "Giao ca", 5)
    _card(p, 116, 88, 1256, 776)
    p.text(140, 128, "Ca sáng → Ca chiều", 22, INK, True, "lm")
    p.pill(1232, 128, "3 việc cần làm", 16, ORANGE, ORANGE_BG, "r")
    notes = [("Cô Lan hẹn 16:00 lấy thuốc đã đặt — để sẵn ở ngăn 2", True, False),
             ("Đã nhập 2 thùng Vitamin C, chưa xếp lên kệ", False, False),
             ("Két lệch 50.000 đ, đã ghi lý do", False, True),
             ("Paracetamol còn 4 hộp — báo nhà cung cấp", False, False)]
    y = 210
    for s, hot, done in notes:
        if hot:
            p.rect(140, y - 38, 1232, y + 38, ORANGE_BG, 10)
        p.rect(160, y - 16, 192, y + 16, GREEN if done else CARD, 8, GREEN if done else (190, 198, 212), 2)
        if done:
            p.d.line([(168 * SS, y * SS), (175 * SS, (y + 8) * SS), (186 * SS, (y - 8) * SS)], fill=(255, 255, 255), width=4 * SS)
        p.text(214, y, s, 20, MUTED if done else INK, hot, "lm")
        y += 92
    p.text(140, 640, "Ghi bởi Thu ngân ca sáng · 14:02", 15, MUTED, False, "lm")


KINDS = {"fefo": _fefo, "stock": _stock, "shift": _shift, "debt": _debt, "handover": _handover}
_CACHE: dict = {}


def screen(kind: str, w: int, h: int) -> Image.Image:
    key = (kind, int(w), int(h))
    if key in _CACHE:
        return _CACHE[key]
    lw, lh = LOGICAL
    img = Image.new("RGBA", (lw * SS, lh * SS), (0, 0, 0, 0))
    KINDS.get(kind, _fefo)(_P(img))
    k = min(w / lw, h / lh)
    small = img.resize((max(2, int(lw * k)), max(2, int(lh * k))), Image.LANCZOS)
    # Bóng đổ nhẹ để màn nổi trên phông quầy.
    pad = int(max(6, small.width * 0.03))
    out = Image.new("RGBA", (int(w), int(h)), (0, 0, 0, 0))
    sh = Image.new("RGBA", (small.width + pad * 2, small.height + pad * 2), (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle((pad, pad + pad // 2, pad + small.width, pad + small.height + pad // 2), radius=int(18 * k), fill=(0, 0, 0, 120))
    sh = sh.filter(ImageFilter.GaussianBlur(pad * 0.6))
    ox, oy = (int(w) - small.width) // 2, (int(h) - small.height) // 2
    out.alpha_composite(sh, (max(0, ox - pad), max(0, oy - pad)))
    out.alpha_composite(small, (ox, oy))
    if len(_CACHE) > 60:
        _CACHE.clear()
    _CACHE[key] = out
    return out

"""Phong cách theo brand cho ảnh Studio (studio_plates).

Brand = {id, name, colors, style, imageNotes, products, avoid} do Studio truyền vào job.
Không có brand, hoặc chuỗi màu không nhận ra màu nào: giữ nguyên diện mạo mặc định.
Có brand: bảng màu nền/khung/nhấn theo màu brand, tên brand ở góc, nhãn lấy từ sản phẩm của brand,
bỏ nhãn chứa từ brand cần tránh, thứ tự khung/hoa văn khác nhau giữa các brand.
"""
import colorsys
import json
import re
import sys
import zlib

TAG = ["NOVIXA"]
HUB = ["Nhà thuốc"]
_PHARMA_RX = re.compile(r"thuốc|dược|pharm|y tế|healthcare|phòng khám|clinic", re.I)
_TRADING_RX = re.compile(r"trading|trader|trade|chứng khoán|forex|crypto|tiền số|tiền điện tử|bitcoin|cổ phiếu|đầu tư|giao dịch|nến|giữ vốn|quản lý vốn|cắt lỗ|chốt lời|nhật ký lệnh|sổ lệnh|vào lệnh|điểm vào|thị trường tài chính|tài chính cá nhân|ngoại hối|hợp đồng tương lai|stop ?loss", re.I)

_TRADING_STRICT = re.compile(r"trading|trader|trade|chứng khoán|forex|crypto|tiền số|bitcoin|cổ phiếu|giao dịch|giữ vốn|cắt lỗ|nhật ký lệnh|sổ lệnh", re.I)


def _all_text(v, depth=0):
    """Gom mọi chữ trong hồ sơ brand (mọi trường, kể cả danh sách lồng) để nhận diện lĩnh vực không phụ thuộc tên brand."""
    if depth > 3: return ""
    if isinstance(v, str): return v[:600]
    if isinstance(v, dict): return " ".join(_all_text(x, depth + 1) for x in list(v.values())[:40])
    if isinstance(v, (list, tuple)): return " ".join(_all_text(x, depth + 1) for x in list(v)[:40])
    return ""
STATE = {"key": None, "snap": None}
TRADING = [False]
BAR_DEMO = {}

# (cụm từ, màu sáng đại diện). Cụm dài đặt trước để "xanh lá" không bị nhận thành "xanh".
_WORDS = [
    ("xanh lá", (46, 184, 114)), ("xanh lục", (46, 184, 114)), ("xanh ngọc", (30, 184, 176)), ("xanh dương", (52, 120, 230)),
    ("xanh biển", (52, 120, 230)), ("xanh da trời", (64, 170, 240)), ("xanh rêu", (96, 150, 70)), ("xanh navy", (40, 80, 170)),
    ("green", (46, 184, 114)), ("teal", (30, 184, 176)), ("cyan", (40, 190, 220)), ("blue", (52, 120, 230)),
    ("xanh", (52, 120, 230)), ("đỏ", (226, 72, 84)), ("red", (226, 72, 84)), ("cam", (255, 150, 50)), ("orange", (255, 150, 50)),
    ("vàng", (250, 200, 50)), ("yellow", (250, 200, 50)), ("gold", (250, 200, 50)), ("tím", (150, 100, 230)), ("purple", (150, 100, 230)),
    ("hồng", (236, 100, 160)), ("pink", (236, 100, 160)), ("nâu", (176, 116, 72)), ("brown", (176, 116, 72)),
    ("xám", (130, 146, 170)), ("grey", (130, 146, 170)), ("gray", (130, 146, 170)),
]
_HEX = re.compile(r"#([0-9a-fA-F]{6})")
_ICON_BY_WORD = [
    ("pos", "bill"), ("bán", "bill"), ("doanh", "bill"), ("tồn", "box"), ("kho", "box"), ("hạn", "calendar"), ("lô", "carton"),
    ("quỹ", "wallet"), ("tiền", "coins"), ("chi phí", "wallet"), ("báo cáo", "report"), ("dữ liệu", "report"), ("khách", "person"),
    ("nhân viên", "person"), ("đơn", "carton"), ("nhập", "carton"), ("kiểm", "mag"), ("theo dõi", "mag"), ("cảnh báo", "mega"),
]


def _hsv(rgb):
    return colorsys.rgb_to_hsv(*(c / 255 for c in rgb))


def _rgb(h, s, v):
    return tuple(int(round(c * 255)) for c in colorsys.hsv_to_rgb(h % 1.0, max(0, min(1, s)), max(0, min(1, v))))


def parse_colors(text):
    low = (text or "").lower()
    found = [(m.start(), tuple(int(m.group(1)[i:i + 2], 16) for i in (0, 2, 4))) for m in _HEX.finditer(low)]
    taken = [False] * len(low)
    for word, rgb in _WORDS:
        for m in re.finditer(re.escape(word), low):
            if any(taken[m.start():m.end()]):
                continue
            for i in range(m.start(), m.end()):
                taken[i] = True
            found.append((m.start(), rgb))
    found.sort(key=lambda item: item[0])
    out = []
    for _, rgb in found:
        if rgb not in out:
            out.append(rgb)
    return out[:3]


def _short(label):
    label = re.split(r"\s*/\s*", str(label).strip())[0]
    words = label.split()
    while len(" ".join(words)) > 16 and len(words) > 1:
        words.pop()
    return " ".join(words)[:16]


def _icon_for(label):
    low = label.lower()
    for word, icon in _ICON_BY_WORD:
        if word in low:
            return icon
    return "check"


def profile(brand):
    if not isinstance(brand, dict):
        return None
    if brand.get("cols"):  # đã là hồ sơ đã phân tích (đọc lại từ metadata PNG)
        return brand
    cols = parse_colors(str(brand.get("colors") or ""))
    name = str(brand.get("name") or "").strip()
    info = " ".join([name, str(brand.get("style") or ""), str(brand.get("imageNotes") or ""), " ".join(str(p) for p in (brand.get("products") or []))]).strip()
    full = _all_text({k: v for k, v in brand.items() if k not in ("cols",)})
    head = info + " " + str(brand.get("id") or "")
    trading = bool(_TRADING_STRICT.search(head)) or (not _PHARMA_RX.search(head) and bool(_TRADING_RX.search(head + " " + full)) and not _PHARMA_RX.search(full))
    if not cols and trading:
        cols = [(30, 184, 176)]  # brand trading chưa ghi màu: dùng xanh ngọc thay vì hình nhà thuốc
    if not cols:
        return None
    return {
        "trading": trading,
        "id": str(brand.get("id") or name),
        "name": name,
        "pharma": (not trading) and ((not info) or bool(_PHARMA_RX.search(info))),  # không có thông tin gì thì giữ hình nhà thuốc như cũ
        "cols": cols,
        "products": [str(p) for p in (brand.get("products") or []) if str(p).strip()][:8],
        "avoid": [str(a).strip().lower() for a in (brand.get("avoid") or []) if str(a).strip()],
    }


def compact(prof):
    return json.dumps({"i": prof["id"], "n": prof["name"], "g": 1 if prof.get("pharma", True) else 0, "t": 1 if prof.get("trading") else 0, "c": ["%02x%02x%02x" % c for c in prof["cols"]], "p": prof["products"], "a": prof["avoid"]}, ensure_ascii=False, separators=(",", ":"))


def expand(text):
    try:
        d = json.loads(text)
        return {"id": d["i"], "name": d["n"], "pharma": bool(d.get("g", 1)), "trading": bool(d.get("t", 0)), "cols": [tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) for h in d["c"]], "products": d.get("p", []), "avoid": d.get("a", [])}
    except Exception:
        return None


def key_of(prof):
    return compact(prof) if prof else ""


def _themes(prof):
    p = prof["cols"][0]
    hp, sp_, _ = _hsv(p)
    s2 = prof["cols"][1] if len(prof["cols"]) > 1 else _rgb(hp + 0.07, sp_, 1)
    s3 = prof["cols"][2] if len(prof["cols"]) > 2 else _rgb(hp - 0.07, sp_, 1)
    sat = max(0.55, min(0.95, sp_))

    def mk(name, rgb, vtop, vbot, vboard, pat, frame, mix=0.0):
        h, s, _ = _hsv(rgb)
        top = _rgb(h, sat * 0.9, vtop); bot = _rgb(h, sat * 0.85, vbot); board = _rgb(h, sat * 0.85, vboard)
        if mix:
            grey = (14, 16, 22)
            top, bot, board = [tuple(int(g + (c - g) * (1 - mix)) for g, c in zip(grey, col)) for col in (top, bot, board)]
        return dict(n=name, top=top, bot=bot, board=board, line=_rgb(h, min(0.8, s * 0.75), 0.95), pat=pat, frame=frame)

    seed = zlib.crc32(prof["id"].encode("utf-8"))
    pats = ["grid", "dots", "diag", "none", "grid", "dots", "diag", "none"]
    frames = ["board", "glass", "bleed", "board", "glass", "bleed", "board", "glass"]
    r = seed % 8
    pats = pats[r:] + pats[:r]
    frames = frames[(seed // 7) % 8:] + frames[:(seed // 7) % 8]
    # (tên, màu gốc, v nền trên, v nền dưới, v bảng, độ bão hoà nhân, trộn xám)
    spec = [("b0", p, .17, .36, .26, 1.0, 0), ("b1", s2, .16, .34, .24, 1.0, 0), ("b2", _rgb(hp + .05, sp_, 1), .30, .50, .38, 1.0, 0),
            ("b4", s3, .16, .34, .24, 1.0, 0), ("b5", p, .11, .26, .18, 1.0, 0), ("b6", p, .38, .58, .46, 0.95, 0)]
    out = [mk(n, rgb, a, b, c, pats[i], frames[i], mix) for i, (n, rgb, a, b, c, _, mix) in enumerate(spec)]
    line = _rgb(hp, min(0.55, sp_ * 0.6), 0.9)
    # nền xám trung tính xen kẽ, chỉ viền/hoa văn mang màu brand
    out.append(dict(n="b3", top=(18, 22, 30), bot=(44, 52, 66), board=(30, 36, 48), line=line, pat=pats[6], frame=frames[6]))
    out.append(dict(n="b7", top=(12, 14, 18), bot=(30, 33, 40), board=(22, 25, 31), line=line, pat=pats[7], frame=frames[7]))
    return out


def _light(rgb):
    h, s, v = _hsv(rgb)
    return _rgb(h, min(s, 0.62), max(v, 0.97))


def _snapshot(sp):
    import illus
    import scenes
    return {"box_img": illus.box_img, "title_lab": scenes.title_lab, "themes": [dict(t) for t in sp.THEMES], "triples": list(sp.TRIPLES), "accents": list(sp.ACCENTS), "tags": dict(sp.LABEL_TAGS),
            "colors": {name: getattr(illus, name) for name in ("CYAN", "GREEN", "PURPLE")}, "linec": scenes.LINEC,
            "chart": list(sp.CHART_LABELS), "big": list(sp.BIG), "person": illus.person_img}


def _set_colors(sp, mapping):
    """Đổi hằng số màu dùng chung (CYAN/GREEN/PURPLE) ở mọi module vẽ."""
    import illus
    import scenes
    mods = [illus, scenes, sp]
    wide = sys.modules.get("studio_plates_wide")
    if wide is not None:
        mods.append(wide)
    for mod in mods:
        for name, value in mapping.items():
            if hasattr(mod, name):
                setattr(mod, name, value)


def _set_box(sp, fn):
    import illus
    import scenes
    mods = [illus, scenes, sp, sys.modules.get("studio_plates_wide")]
    for mod in mods:
        if mod is not None and hasattr(mod, "box_img"):
            mod.box_img = fn


def _set_person(sp, fn):
    import illus
    import scenes
    for mod in (illus, scenes, sp, sys.modules.get("studio_plates_wide")):
        if mod is not None and hasattr(mod, "person_img"):
            mod.person_img = fn


def _trading_person(mood_fn):
    def person(w=300, h=420, mood="ok"):
        return mood_fn(w, h, mood)
    return person


_TRADING_CHART = ["Lợi nhuận", "Rủi ro", "Vốn", "Số lệnh thắng", "Số lệnh thua", "Khối lượng", "Biến động", "Tỉ lệ R:R", "Drawdown", "Tăng trưởng"]
_TRADING_BAR_DEMO = {"Lợi nhuận": ("+12%", .6), "Rủi ro": ("2%", .2), "Vốn": ("1.000$", .85), "Số lệnh thắng": ("6", .6), "Số lệnh thua": ("4", .4),
                     "Khối lượng": ("0,5 lot", .5), "Biến động": ("3,2%", .45), "Tỉ lệ R:R": ("1:2", .55), "Drawdown": ("-8%", .35), "Tăng trưởng": ("+5%", .5)}
_TRADING_BIG = ["2%", "1:2", "5 phút", "3 bước", "24/7", "10 lệnh", "7 ngày", "30 ngày", "1 quy tắc", "-5%"]


def _trading_triples(lp, ls, lt):
    import illus
    R, O, Y, G = illus.RED, illus.ORANGE, illus.YEL, illus.GREY
    return [
        ("Biểu đồ nến", "candle", lp), ("Xu hướng lên", "candle", ls), ("Xu hướng xuống", "candle", R), ("Rủi ro", "cross", R),
        ("Cắt lỗ", "lock", R), ("Vốn", "coins", Y), ("Lợi nhuận", "bill", ls), ("Kỷ luật", "check", ls), ("Cảm xúc", "tworry", O),
        ("Nhật ký lệnh", "report", lp), ("Soi thị trường", "mag", lp), ("Cảnh báo", "mega", O), ("Trader", "trader", lp),
        ("FOMO", "tworry", O), ("Chờ tín hiệu", "clock", O), ("Câu hỏi", "q", Y), ("Tài khoản", "wallet", lp), ("Thua lỗ", "cracked", R),
        ("Kế hoạch", "report", ls), ("Quản lý vốn", "coins", ls), ("Điểm vào", "check", ls), ("Điểm thoát", "cross", R),
        ("Đòn bẩy", "mega", R), ("Thanh khoản", "coins", lp), ("Tín hiệu", "mag", lt), ("Lệnh thắng", "check", ls), ("Lệnh thua", "cross", R),
        ("Quy tắc", "report", Y), ("Bitcoin", "btc", O), ("Ethereum", "eth", lt), ("Vàng", "gold", Y), ("Forex", "fx", lp), ("Cổ phiếu", "stock", lp),
        ("Kế hoạch lệnh", "chart", lp), ("Đường vốn", "equity", ls), ("Sụt giảm", "dd", R), ("Thói quen", "report", G), ("Chi phí", "wallet", O),
        ("Nhà đầu tư", "trader2", ls), ("Phân tích", "tpoint", lp), ("Theo dõi lệnh", "tphone", lt), ("Suy nghĩ", "tchin", Y),
    ]


_GENERIC_RENAME = {"Chủ nhà thuốc": "Chủ cửa hàng", "Nhà thuốc": "Cửa hàng"}
_GENERIC_DROP = {"Vỉ thuốc", "Chai thuốc"}


def _generic_title(orig):
    def title_lab(text, *args, **kwargs):
        return orig(str(text).replace("hộp thuốc", "hộp hàng").replace("Nhà thuốc", "Cửa hàng"), *args, **kwargs)
    return title_lab


def _generic_box(orig):
    def box(*args, **kwargs):
        args = list(args)
        if len(args) >= 4 and isinstance(args[3], str):
            args[3] = args[3].replace("Thuốc", "Hàng")
        elif "name" in kwargs and isinstance(kwargs["name"], str):
            kwargs["name"] = kwargs["name"].replace("Thuốc", "Hàng")
        elif len(args) < 4:
            kwargs.setdefault("name", "Hàng A")
        return orig(*args, **kwargs)
    return box


def apply(sp, brand):
    """Áp (hoặc gỡ) phong cách brand lên studio_plates `sp`. Gọi lại với cùng brand thì không làm gì."""
    prof = profile(brand)
    key = key_of(prof)
    if STATE["key"] == key:
        return prof
    if STATE["snap"] is None:
        STATE["snap"] = _snapshot(sp)
    snap = STATE["snap"]
    # về mặc định
    sp.THEMES[:] = [dict(t) for t in snap["themes"]]
    sp.TRIPLES[:] = list(snap["triples"])
    sp.ACCENTS[:] = list(snap["accents"])
    sp.LABEL_TAGS.clear(); sp.LABEL_TAGS.update(snap["tags"])
    _set_colors(sp, snap["colors"])
    TAG[0] = "NOVIXA"
    HUB[0] = "Nhà thuốc"
    _set_box(sp, snap["box_img"])
    _set_person(sp, snap["person"])
    TRADING[0] = False
    BAR_DEMO.clear()
    sp.CHART_LABELS[:] = snap["chart"]
    sp.BIG[:] = snap["big"]
    import scenes as _sc
    _sc.title_lab = snap["title_lab"]
    if prof:
        lp = _light(prof["cols"][0])
        ls = _light(prof["cols"][1]) if len(prof["cols"]) > 1 else _light(_rgb(_hsv(prof["cols"][0])[0] + 0.07, 0.6, 1))
        lt = _light(prof["cols"][2]) if len(prof["cols"]) > 2 else _light(_rgb(_hsv(prof["cols"][0])[0] - 0.07, 0.6, 1))
        old = snap["colors"]
        remap = {old["CYAN"]: lp, old["GREEN"]: ls, old["PURPLE"]: lt}
        _set_colors(sp, {"CYAN": lp, "GREEN": ls, "PURPLE": lt})
        sp.THEMES[:] = _themes(prof)
        sp.ACCENTS[:] = [remap.get(c, c) for c in snap["accents"]]
        triples = [(lab, ic, remap.get(col, col)) for lab, ic, col in snap["triples"]]
        avoid = prof["avoid"]
        if not prof.get("pharma", True):  # brand ngoài ngành thuốc: đổi chữ và hình đặc thù nhà thuốc sang từ chung
            triples = [(_GENERIC_RENAME.get(lab, lab), ic, col) for lab, ic, col in triples if lab not in _GENERIC_DROP]
            for old, new in _GENERIC_RENAME.items():
                if old in sp.LABEL_TAGS:
                    sp.LABEL_TAGS[new] = sp.LABEL_TAGS.pop(old)
            HUB[0] = "Cửa hàng"
            _set_box(sp, _generic_box(snap["box_img"]))
            _sc.title_lab = _generic_title(snap["title_lab"])
        if prof.get("trading"):
            import illus as _il
            triples = _trading_triples(lp, ls, lt)
            for lab, _, _ in triples:
                sp.LABEL_TAGS[lab] = {"brand"}
            sp.CHART_LABELS[:] = _TRADING_CHART
            sp.BIG[:] = _TRADING_BIG
            _set_person(sp, _trading_person(_il.trader_img))
            _set_box(sp, lambda w=240, h=300, color=None, name="", hsd="", red=False, dim=False, qm=False: _il.candle_img(max(120, int(w * 1.15)), max(120, int(h * .9)), not (red or dim)))
            HUB[0] = "Thị trường"
            TRADING[0] = True
            BAR_DEMO.update(_TRADING_BAR_DEMO)
        triples = [t for t in triples if not any(a in t[0].lower() for a in avoid)]
        own = []
        for product in prof["products"]:
            lab = _short(product)
            if lab and not any(a in lab.lower() for a in avoid) and lab not in [t[0] for t in own]:
                own.append((lab, _icon_for(lab), lp))
        for lab, _, _ in own:
            sp.LABEL_TAGS[lab] = {"brand"}
        sp.TRIPLES[:] = triples + own * 2
        name = prof["name"].upper().strip()
        TAG[0] = (name[:16] if name else "NOVIXA")
    STATE["key"] = key
    for cache_owner in (sp, sys.modules.get("studio_plates_wide")):
        if cache_owner is not None and hasattr(cache_owner, "_BASES"):
            cache_owner._BASES.clear()
    return prof

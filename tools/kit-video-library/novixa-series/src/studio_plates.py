"""Studio plate adapter (draft): Studio layout + spoken line -> 1080x1920 picture, no caption.
Pure Pillow, no network. Caption zone (y>=1500) left clear for Studio's karaoke."""
import os, re, sys
sys.path.insert(0, os.environ.get("NOVIXA_SERIES_SRC", os.path.dirname(os.path.abspath(__file__))))
import scenes as SC
import studio_brand as SB
from scenes import *
from PIL import Image, ImageDraw

_orig_tx = SC.tx
def _tagged_tx(text, *a, **k):
    e = _orig_tx(text, *a, **k); e._lab = text; return e
SC.tx = _tagged_tx  # đánh dấu chữ để lọc nhãn không cần


def _fits(t, size=56, maxw=860):
    from illus import textsize
    return textsize(t, size, True) <= maxw

def headline(text, n=44):
    t = re.split(r"[,.;:!?—–]", text.strip())[0].strip() or text.strip()
    if len(t) > n:
        t = t[:n].rsplit(" ", 1)[0] + "…"
    while " " in t and not _fits(t.rstrip("…")):  # vừa một dòng ở cỡ chữ tiêu đề, không tràn khung
        t = t.rstrip("…").rsplit(" ", 1)[0] + "…"
    return t[:1].upper() + t[1:]

def tail(text, n=34):
    parts = [p.strip() for p in re.split(r"[,.;:!?—–]", text) if p.strip()]
    t = parts[1] if len(parts) > 1 else ""
    return (t[:n].rsplit(" ", 1)[0] + "…") if len(t) > n else t

_USED = set()
_COUNT = {}

def _shelf(h, col, dim=(), marks=None, strip=None, rows=9):
    return lambda: shelf_scene(h, col, rows, dim=dim, marks=marks or {}, strip=strip)

def variants(layout, text):
    """Danh sách (khoá, hàm vẽ) cho mỗi kiểu cảnh. Khoá là duy nhất để không lặp trong một phim."""
    h, s = headline(text), tail(text)
    h2 = headline(text, 26)
    coin = lambda: coins_img(5, 36)
    V = {
     "pharmacy_sleep": [
        ("shelf-dim-a", _shelf(h, CYAN, dim=(1, 4, 7), strip=("coins", s or "Hàng nằm trên kệ", CYAN))),
        ("shelf-dim-b", _shelf(h, ORANGE, dim=(0, 2, 3, 5, 6, 8), strip=("empty", s or "Tiền chưa quay lại", ORANGE))),
        ("speed-slow", lambda: sc_speed("slow")),
        ("note-sleep", lambda: sc_note(h, ORANGE, "think")),
        ("day", sc_day)],
     "pharmacy_quiet": [
        ("shelf-ok", _shelf(h, GREEN, marks={0: "check", 4: "check", 8: "check"})),
        ("flow-normal", lambda: sc_flow3([("Khách vẫn đến", None, person_img(190, 270, "ok"), GREEN), ("Thuốc vẫn bán", None, box_img(180, 210, CYAN, "Thuốc A", "HSD 12/2027"), GREEN), ("Tiền vẫn thu về", None, coins_img(6, 40), GREEN)])),
        ("bars-ok", lambda: sc_bars([("Doanh thu", .85, GREEN), ("Nhập hàng", .62, CYAN), ("Chi phí", .4, ORANGE)])),
        ("check-quiet", lambda: sc_checklist([h2, s or "Quầy vẫn đông", "Mọi thứ vẫn ổn"], "Bên ngoài", "ok"))],
     "pharmacy_cabinet": [
        ("shelf-hsd", _shelf(h, RED, marks={2: "hsd", 5: "hsd"}, strip=("empty", s or "Hạn dùng đang đến gần", RED))),
        ("expiry-30", lambda: sc_expiry(headline(text, 28), .92, "05", "Tháng 12", "HSD", "Hết hạn rồi")),
        ("cracked", lambda: sc_types([(h2, None, cracked_coin_img(56), RED), ("Hàng hết hạn", None, calendar_img(150, 160, "30", "HSD", "T11"), RED), ("Tiền mất theo", None, coins_img(4, 34), ORANGE)])),
        ("note-expired", lambda: sc_note(h, RED, "worry"))],
     "pharmacy_group": [
        ("types-3", lambda: sc_types([(h2, None, box_img(150, 180, CYAN, "A", ""), CYAN), ("Bán chậm", None, clock_img(80, 3, 0, ORANGE), ORANGE), ("Cận hạn", None, calendar_img(150, 160, "30", "HSD", "T11"), RED)])),
        ("types-speed", lambda: sc_types([("Bán nhanh", "Nhập về là hết", box_img(150, 180, GREEN, "A", ""), GREEN), ("Bán chậm", "Nằm trên kệ lâu", clock_img(80, 3, 0, ORANGE), ORANGE), ("Sắp hết hạn", "Âm thầm tiến gần hạn", calendar_img(150, 160, "30", "HSD", "T11"), RED)])),
        ("places", sc_places),
        ("options", sc_options)],
     "goods_macro": [
        ("shelf-q", _shelf(h, YEL, marks={1: "q"})),
        ("box-big", lambda: sc_decision(h, s or None)),
        ("speed-fast", lambda: sc_speed("fast")),
        ("types-box", lambda: sc_types([(h2, None, box_img(150, 180, CYAN, "Thuốc A", "HSD 12/2027"), CYAN), ("Vỉ thuốc", None, blister_img(150, 100), GREEN), ("Chai thuốc", None, bottle_img(90, 160, ORANGE), ORANGE)]))],
     "goods_pile": [
        ("locked", lambda: sc_locked(h, s or None)),
        ("carton-types", lambda: sc_types([("Nhập nhiều hơn", None, carton_img(220, 160), CYAN), ("Giá tốt hơn", None, tag_img(200, "ƯU ĐÃI", ORANGE), ORANGE), ("Hàng nằm lại", None, lock_img(110, RED), RED)])),
        ("scale", sc_scale),
        ("compare-pile", lambda: sc_compare("Giá nhập rẻ hơn", "Kinh doanh hiệu quả hơn", "≠", tag_img(260, "RẺ HƠN"), report_img(220, 280, CYAN), GREEN, CYAN)),
        ("shelf-full", _shelf(h, ORANGE, dim=range(9), strip=("empty", s or "Ví tiền mặt gần như trống", RED)))],
     "goods_lone": [
        ("shelf-lone", _shelf(h, ORANGE, dim=(0, 1, 2, 3, 5, 6, 7, 8), marks={4: "q"})),
        ("question-lone", lambda: sc_question("“" + h + "?”", "think")),
        ("shelf-lock", _shelf(h, RED, dim=(2, 5, 8), marks={2: "lock", 5: "lock", 8: "lock"}, strip=("coins", "Tiền bị giữ lại", RED))),
        ("mag", lambda: sc_compare("Ai cũng bỏ qua", h2, "→", person_img(220, 320, "worry"), mag_img(220), GREY, CYAN))],
     "data_cash": [
        ("receipt", lambda: sc_receipt(False)),
        ("cash-types", lambda: sc_types([("Vốn", None, coins_img(6, 40), YEL), ("Hàng tồn", None, box_img(150, 180, CYAN, "A", ""), CYAN), ("Tiền mặt", None, wallet_img(200, 150, True), RED)])),
        ("notebook", lambda: sc_notebook(True)),
        ("bars-single", lambda: sc_bars([("Doanh thu", .85, GREEN)], single=True)),
        ("bars-hidden", lambda: sc_bars([("Doanh thu", .85, GREEN), ("Tiền trong tồn kho", .5, CYAN), ("Chi phí vận hành", .5, ORANGE)], hidden=(1, 2)))],
     "data_where": [
        ("places", sc_places),
        ("question-where", lambda: sc_question("“" + h + "?”", "worry")),
        ("cycle", lambda: sc_cycle(False)),
        ("timeline", sc_timeline)],
     "chart_fall": [
        ("bars-fall", lambda: sc_bars([("Tháng 1", .85, GREEN), ("Tháng 2", .6, CYAN), ("Tháng 3", .35, ORANGE)])),
        ("cycle-broken", lambda: sc_cycle(True)),
        ("bars-ok", lambda: sc_bars([("Doanh thu", .85, GREEN), ("Tồn kho", .7, CYAN), ("Tiền mặt", .3, RED)])),
        ("speed-slow", lambda: sc_speed("slow"))],
     "chart_rank": [
        ("bars-rank", lambda: sc_bars([("Bán chạy", .9, GREEN), ("Trung bình", .55, CYAN), ("Bán chậm", .25, ORANGE)])),
        ("dash", lambda: sc_dash([("Bán chậm", "12", ORANGE), ("Sắp hết hạn", "5", RED), ("Bán chạy", "20", GREEN)])),
        ("types-rank", lambda: sc_types([("Bán nhanh", None, box_img(150, 180, GREEN, "A", ""), GREEN), ("Bán chậm", None, clock_img(80, 3, 0, ORANGE), ORANGE), ("Đứng yên", None, lock_img(110, RED), RED)]))],
     "contrast_stop": [
        ("cmp-stop", lambda: sc_compare("Bình thường", h2, "≠", box_img(140, 170, GREY, "A", "", False, True), mark_img("cross", 160, RED), GREY, RED)),
        ("note-stop", lambda: sc_note(h, RED, "worry")),
        ("cmp-less", lambda: sc_compare("Ít hàng hơn", "Quản lý tốt", "≠", box_img(140, 170, GREY, "A", "", False, True), mark_img("check", 160, GREEN), GREY, GREEN)),
        ("shelf-cut", lambda: shelf_scene("Cắt tồn kho bằng mọi giá?", RED, 3, marks={}, strip=("coins", "Không phải giải pháp", RED)))],
     "contrast_split": [
        ("cmp-split", lambda: sc_compare("Nhìn bề ngoài", h2, "≠", person_img(220, 320, "ok"), mag_img(220), GREEN, CYAN)),
        ("cmp-busy", lambda: sc_compare("Làm việc nhiều hơn", "Thấy vấn đề sớm hơn", "≠", person_img(220, 320, "worry"), mag_img(220), ORANGE, CYAN)),
        ("cmp-habit", lambda: sc_compare("Thói quen, cảm giác", "Tình hình bán thực tế", "→", brain_img(240, 240), report_img(200, 250, CYAN), GREY, GREEN)),
        ("check-busy", lambda: sc_checklist(["Có mặt mỗi ngày", "Theo sát nhân viên", "Kiểm tra hàng hóa"], "Chủ nhà thuốc chăm chỉ", "ok"))],
     "contrast_dates": [
        ("cmp-dates", lambda: sc_compare("Ngày nhập", "Ngày hết hạn", "→", calendar_img(200, 220, "02", "NHẬP", "Tháng 3", GREEN), calendar_img(200, 220, "30", "HSD", "Tháng 11", RED), GREEN, RED)),
        ("expiry-65", lambda: sc_expiry(headline(text, 28), .65, "15", "Tháng 10", "HSD", "Thời gian trôi qua")),
        ("timeline", sc_timeline)],
     "contrast_aside": [
        ("cmp-aside", lambda: sc_compare("Ít người nói", h2, "vs", person_img(220, 320, "worry"), megaphone_img(220), GREY, ORANGE)),
        ("note-aside", lambda: sc_note(h, CYAN, "think")),
        ("cmp-early", lambda: sc_compare("Phát hiện sớm: nhiều cách xử lý", "Phát hiện muộn: ít lựa chọn", "vs", mark_img("check", 160, GREEN), mark_img("cross", 160, RED), GREEN, RED))],
     "process_queue": [
        ("check-queue", lambda: sc_checklist([headline(text, 30), s or "Kiểm tra", "Xác nhận"], "Các bước", "ok")),
        ("flow-3", lambda: sc_flow3([("Bước 1", "Xem cảnh báo", mag_img(120), CYAN), ("Bước 2", "Chọn xử lý", report_img(110, 140, CYAN), GREEN), ("Bước 3", "Giữ vốn quay", coins_img(5, 36), YEL)])),
        ("types-queue", lambda: sc_types([("Duy trì", "mặt hàng cần có", mark_img("check", 120, GREEN), GREEN), ("Bổ sung", "mặt hàng sắp thiếu", carton_img(200, 150), CYAN), ("Chú ý", "trước khi vốn bị giữ lâu", mag_img(150), ORANGE)]))],
     "process_late": [
        ("cycle-broken", lambda: sc_cycle(True)),
        ("timeline", sc_timeline),
        ("options", sc_options),
        ("expiry-30", lambda: sc_expiry(headline(text, 28), .92, "05", "Tháng 12", "HSD", "Đến khi phát hiện ra"))],
     "process_act": [
        ("decision", lambda: sc_decision(h, s or None)),
        ("options", sc_options),
        ("checklist-act", lambda: sc_checklist([h2, "Đẩy bán", "Giảm giá"], "Hành động", "ok")),
        ("cta", sc_cta)],
     "process_early": [
        ("cmp-early", lambda: sc_compare("Biết sớm: nhiều lựa chọn", "Biết muộn: ít lựa chọn", "vs", mark_img("check", 160, GREEN), mark_img("cross", 160, RED), GREEN, RED)),
        ("timeline", sc_timeline),
        ("note-early", lambda: sc_note(h, GREEN, "ok")),
        ("flow-early", lambda: sc_flow3([("Theo dõi", "Mỗi ngày", mag_img(120), CYAN), ("Phát hiện sớm", "Còn thời gian", calendar_img(120, 130, "05", "", "T10", GREEN), GREEN), ("Xử lý kịp", "Giữ được vốn", coins_img(5, 36), YEL)]))],
     "process_next": [
        ("flow-next", lambda: sc_flow3([("Bước 1", "Xem cảnh báo", mag_img(120), CYAN), ("Bước 2", "Chọn xử lý", report_img(110, 140, CYAN), GREEN), ("Bước 3", "Giữ vốn quay", coins_img(5, 36), YEL)])),
        ("modules", sc_modules),
        ("cta", sc_cta),
        ("cycle", lambda: sc_cycle(False, ("Nhập hàng", "Bán hàng", "Tiền về")))],
     "interface_hero": [
        ("modules", sc_modules),
        ("cta", sc_cta),
        ("dash-hero", lambda: sc_dash([("Bán chậm", "12", ORANGE), ("Sắp hết hạn", "5", RED), ("Cần chú ý", "8", CYAN)])),
        ("title-hero", lambda: sc_title(h))],
     "interface_expiry": [
        ("expiry-80", lambda: sc_expiry(headline(text, 28), .8, "30", "Tháng 11")),
        ("dash-exp", lambda: sc_dash([("Sắp hết hạn", "5", RED), ("Bán chậm", "12", ORANGE), ("Đã xử lý", "9", GREEN)])),
        ("modules", sc_modules)],
    }
    return V.get(layout) or [("note", lambda: sc_note(h, CYAN, "think"))]


# ===================== bộ tạo cảnh theo tham số (thư viện lớn) =====================
import random, zlib

def _icon(name, size, col=None):
    s = int(size)
    return {
        "box": lambda: box_img(int(s * .8), s, col or CYAN, "Thuốc", ""),
        "boxhsd": lambda: box_img(int(s * .8), s, RED, "Thuốc", "HSD 11/2026", True),
        "dim": lambda: box_img(int(s * .8), s, GREY, "Thuốc", "", False, True),
        "clock": lambda: clock_img(int(s * .45), 3, 0, col or ORANGE),
        "calendar": lambda: calendar_img(int(s * .9), s, "30", "HSD", "T11"),
        "coins": lambda: coins_img(5, max(24, int(s * .22))),
        "lock": lambda: lock_img(int(s * .7), col or RED),
        "tag": lambda: tag_img(int(s * .9), "ƯU ĐÃI", col or ORANGE),
        "mag": lambda: mag_img(int(s * .9)),
        "report": lambda: report_img(int(s * .75), s, col or CYAN),
        "carton": lambda: carton_img(int(s * 1.1), int(s * .8)),
        "blister": lambda: blister_img(int(s * 1.1), int(s * .7)),
        "bottle": lambda: bottle_img(int(s * .55), s, col or ORANGE),
        "wallet": lambda: wallet_img(int(s * 1.2), int(s * .9), True),
        "brain": lambda: brain_img(s, s),
        "mega": lambda: megaphone_img(int(s * .9)),
        "check": lambda: mark_img("check", int(s * .8), GREEN),
        "cross": lambda: mark_img("cross", int(s * .8), RED),
        "q": lambda: mark_img("q", int(s * .8), YEL),
        "person": lambda: person_img(int(s * .7), s, "ok"),
        "worry": lambda: person_img(int(s * .7), s, "worry"),
        "cracked": lambda: cracked_coin_img(int(s * .35)),
        "bill": lambda: bill_img(int(s * 1.2), int(s * .6)),
        "btc": lambda: btc_img(max(30, int(s * .5))),
        "eth": lambda: eth_img(max(30, int(s * .5))),
        "gold": lambda: gold_img(int(s * 1.3), int(s * .95)),
        "fx": lambda: fx_img(int(s * 1.5), int(s * .86)),
        "stock": lambda: stock_img(int(s * 1.1), int(s * .8)),
        "chart": lambda: chart_big_img(int(s * 1.5), int(s * .95), (col or GREEN) != RED, True, hash((col, s)) % 997),
        "equity": lambda: equity_img(int(s * 1.4), int(s * .85), True, ""),
        "dd": lambda: equity_img(int(s * 1.4), int(s * .85), False, ""),
        "candle": lambda: candle_img(int(s * 1.25), s, (col or GREEN) != RED, hash((col, s)) % 997),
        "trader": lambda: trader_img(int(s * .7), s, "ok"),
        "tworry": lambda: trader_img(int(s * .7), s, "worry"),
        "trader2": lambda: trader_img(int(s * .7), s, "ok", 1),
        "tpoint": lambda: trader_img(int(s * .8), s, "ok", 0, "point"),
        "tphone": lambda: trader_img(int(s * .7), s, "ok", 1, "phone"),
        "tchin": lambda: trader_img(int(s * .7), s, "think", 0, "chin"),
        "store": lambda: store_img(int(s * 1.4), int(s * 1.1)),
    }[name]()

# (nhãn, icon, màu) mạch lạc theo chủ đề nhà thuốc
TRIPLES = [
    ("Hàng tồn", "box", CYAN), ("Bán chậm", "clock", ORANGE), ("Cận hạn", "calendar", RED), ("Tiền vốn", "coins", YEL),
    ("Vốn bị giữ", "lock", RED), ("Ưu đãi nhập", "tag", ORANGE), ("Soi dữ liệu", "mag", CYAN), ("Báo cáo", "report", CYAN),
    ("Nhập hàng", "carton", ORANGE), ("Vỉ thuốc", "blister", GREEN), ("Chai thuốc", "bottle", ORANGE), ("Tiền mặt", "wallet", RED),
    ("Thói quen", "brain", GREY), ("Cảnh báo sớm", "mega", ORANGE), ("Đã kiểm tra", "check", GREEN), ("Có rủi ro", "cross", RED),
    ("Chủ nhà thuốc", "person", GREEN), ("Hàng sắp hết hạn", "boxhsd", RED), ("Hàng nằm im", "dim", GREY), ("Doanh thu", "bill", GREEN),
    ("Nhà thuốc", "store", CYAN), ("Tiền mất theo", "cracked", RED), ("Câu hỏi", "q", YEL),
]
TRIPLES += [
    ("Giá nhập", "tag", ORANGE), ("Dòng tiền", "bill", GREEN), ("Kệ hàng", "box", CYAN), ("Lô mới", "carton", GREEN), ("Cần xử lý", "mega", ORANGE),
    ("Hạn dùng", "calendar", RED), ("Số liệu", "report", CYAN), ("Kiểm kê", "mag", YEL), ("Tồn đọng", "lock", ORANGE), ("Lỗ vì hết hạn", "cracked", RED),
    ("Quyết định", "q", YEL), ("Bận rộn", "worry", ORANGE), ("Người quản lý", "person", CYAN), ("Hàng bán chạy", "box", GREEN), ("Hàng dễ thiếu", "bottle", ORANGE),
    ("Doanh số", "bill", GREEN), ("Lợi nhuận", "coins", GREEN), ("Cảnh báo", "mega", RED), ("Theo dõi", "mag", CYAN), ("Nguy cơ", "cross", RED),
    ("Giải pháp", "check", GREEN), ("Cửa hàng", "store", CYAN), ("Đơn nhập", "carton", CYAN), ("Chi phí", "wallet", ORANGE), ("Ưu tiên", "check", YEL),
]
CHART_LABELS = ["Doanh thu", "Nhập hàng", "Tồn kho", "Tiền mặt", "Bán chạy", "Bán chậm", "Cận hạn", "Chi phí", "Lợi nhuận", "Hàng mới"]
BIG = ["30%", "5 phút", "12", "3 bước", "24/7", "100%", "7 ngày", "90 ngày", "1 lô", "x2"]
ACCENTS = [CYAN, GREEN, ORANGE, YEL, PURPLE, RED]

def _rng(layout, text, pick):
    return random.Random(zlib.crc32((layout + "|" + text).encode("utf-8")) + int(pick) * 7919)

def _clauses(text):
    return [p.strip() for p in re.split(r"[,.;:!?—–]", text) if p.strip()]

def _short(t, n=22):
    t = t.strip()
    if len(t) <= n: return t[:1].upper() + t[1:]
    t = t[:n].rsplit(" ", 1)[0] + "…"
    return t[:1].upper() + t[1:]


# ===================== ý của câu lời =====================
INTENT_RE = {
    "expiry": r"hạn|hsd|hết hạn|cận hạn",
    "money": r"tiền|vốn|doanh thu|lợi nhuận|chi phí|giá |doanh số",
    "slow": r"chậm|nằm|lâu|tồn|đọng|giữ|im ",
    "buy": r"nhập|mua|đặt hàng|nhà phân phối",
    "price": r"giá|rẻ|ưu đãi|giảm|khuyến mãi",
    "data": r"dữ liệu|báo cáo|số liệu|theo dõi|ghi chép|sổ|thông tin|nền tảng|novixa|bảng",
    "people": r"chủ|nhân viên|bận|chăm|khách|làm việc|kinh nghiệm",
    "check": r"kiểm|duyệt|xem|nhìn|phát hiện|sớm|muộn|cảnh báo|để ý",
    "step": r"bước|cách|quy trình|làm sao|ưu tiên|xử lý",
}
INTENT_PREF = {
    "expiry": ("expiry", "shelf-hsd", "cracked", "cmp-dates", "types-speed", "shelf", "types3", "compare"),
    "money": ("locked", "cash-types", "carton-types", "scale", "bars", "stat", "donut", "types3", "compare"),
    "slow": ("speed-slow", "shelf-dim", "shelf-full", "shelf-lock", "locked", "note-sleep", "shelf", "types3"),
    "buy": ("carton-types", "scale", "compare-pile", "cycle", "flow", "types3", "compare"),
    "price": ("scale", "compare-pile", "carton-types", "compare", "types3"),
    "data": ("dash", "modules", "bars", "donut", "hub", "receipt", "notebook", "grid4"),
    "people": ("cmp-busy", "check-busy", "cmp-split", "question", "note", "quote", "hero"),
    "check": ("timeline", "cmp-early", "flow-early", "check", "steps", "checklist", "flow3"),
    "step": ("steps", "flow", "checklist", "check-queue", "types-queue"),
}
LABEL_TAGS = {
    "Hàng tồn": {"slow", "money"}, "Bán chậm": {"slow"}, "Cận hạn": {"expiry"}, "Tiền vốn": {"money"}, "Vốn bị giữ": {"money", "slow"},
    "Ưu đãi nhập": {"price", "buy"}, "Soi dữ liệu": {"data", "check"}, "Báo cáo": {"data"}, "Nhập hàng": {"buy"}, "Vỉ thuốc": {"slow"},
    "Chai thuốc": {"slow"}, "Tiền mặt": {"money"}, "Thói quen": {"people"}, "Cảnh báo sớm": {"check", "expiry"}, "Đã kiểm tra": {"check"},
    "Có rủi ro": {"expiry", "check"}, "Chủ nhà thuốc": {"people"}, "Hàng sắp hết hạn": {"expiry"}, "Hàng nằm im": {"slow"},
    "Doanh thu": {"money", "data"}, "Nhà thuốc": {"people"}, "Tiền mất theo": {"money", "expiry"}, "Câu hỏi": {"check", "people"},
}

LABEL_TAGS.update({
    "Giá nhập": {"price", "buy", "money"}, "Dòng tiền": {"money"}, "Kệ hàng": {"slow"}, "Lô mới": {"buy"}, "Cần xử lý": {"step", "expiry", "check"},
    "Hạn dùng": {"expiry"}, "Số liệu": {"data"}, "Kiểm kê": {"check", "data"}, "Tồn đọng": {"slow", "money"}, "Lỗ vì hết hạn": {"expiry", "money"},
    "Quyết định": {"step", "people"}, "Bận rộn": {"people"}, "Người quản lý": {"people"}, "Hàng bán chạy": {"slow", "data"}, "Hàng dễ thiếu": {"buy"},
    "Doanh số": {"money", "data"}, "Lợi nhuận": {"money"}, "Cảnh báo": {"check", "expiry"}, "Theo dõi": {"check", "data"}, "Nguy cơ": {"expiry", "check"},
    "Giải pháp": {"step"}, "Cửa hàng": {"people"}, "Đơn nhập": {"buy"}, "Chi phí": {"money"}, "Ưu tiên": {"step", "check"},
})

def intents_of(text):
    low = text.lower() + " "
    return [k for k, rx in INTENT_RE.items() if re.search(rx, low)]

_LABEL_USED = set()

_TLAB = {"Biểu đồ nến": r"nến|biểu đồ|đồ thị|chart", "Xu hướng lên": r"xu hướng (tăng|lên)|tăng giá|uptrend|bull", "Xu hướng xuống": r"xu hướng (giảm|xuống)|giảm giá|downtrend|bear",
 "Rủi ro": r"rủi ro|nguy hiểm|mạo hiểm", "Cắt lỗ": r"cắt lỗ|stop ?loss", "Vốn": r"vốn|tiền", "Lợi nhuận": r"lợi nhuận|lãi|chốt lời|profit", "Kỷ luật": r"kỷ luật|tuân thủ|quy tắc|nguyên tắc",
 "Cảm xúc": r"cảm xúc|tâm lý|sợ|tham|hoảng", "Nhật ký lệnh": r"nhật ký|ghi chép|ghi lại", "Soi thị trường": r"thị trường|phân tích|quan sát", "Cảnh báo": r"cảnh báo|lưu ý|chú ý",
 "Trader": r"trader|nhà đầu tư|người chơi", "Nhà đầu tư": r"cô ấy|chị|bạn nữ", "Phân tích": r"phân tích|chỉ ra|quan sát biểu đồ", "Theo dõi lệnh": r"điện thoại|theo dõi|app|ứng dụng", "Suy nghĩ": r"suy nghĩ|cân nhắc|tự hỏi|đặt câu hỏi", "FOMO": r"fomo|đu theo|đuổi giá", "Chờ tín hiệu": r"chờ|kiên nhẫn|tín hiệu", "Tài khoản": r"tài khoản", "Thua lỗ": r"thua|lỗ(?!\w)|mất",
 "Kế hoạch": r"kế hoạch|chiến lược", "Quản lý vốn": r"quản lý vốn|phân bổ", "Điểm vào": r"điểm vào|vào lệnh|mua", "Điểm thoát": r"điểm thoát|thoát lệnh|bán", "Đòn bẩy": r"đòn bẩy|margin",
 "Lệnh thắng": r"thắng", "Lệnh thua": r"lệnh thua|thua lệnh", "Bitcoin": r"bitcoin|btc", "Ethereum": r"ethereum|eth\b", "Vàng": r"vàng|gold", "Forex": r"forex|ngoại hối", "Cổ phiếu": r"cổ phiếu|chứng khoán"}
_TNEUTRAL = ("Biểu đồ nến", "Trader", "Nhà đầu tư", "Phân tích", "Theo dõi lệnh", "Suy nghĩ", "Vốn", "Rủi ro", "Kỷ luật", "Soi thị trường", "Kế hoạch")

def _pick3_trading(rng, k, text):
    low = text.lower()
    hit = [t for t in TRIPLES if re.search(_TLAB.get(t[0], r"(?!x)x"), low)]
    neutral = [t for t in TRIPLES if t[0] in _TNEUTRAL]
    rng.shuffle(hit); rng.shuffle(neutral)
    seen = []; out = []
    for t in hit + [t for t in neutral if t not in hit]:
        if t[0] in seen: continue
        seen.append(t[0])
        if t[0] not in _LABEL_USED or len(out) < k and len(seen) > len(TRIPLES): out.append(t)
        if len(out) >= k: break
    for t in neutral + TRIPLES:
        if len(out) >= k: break
        if t not in out: out.append(t)
    for t in out: _LABEL_USED.add(t[0])
    return out[:k]

def _pick3(rng, k=3, text=""):
    if SB.TRADING[0]: return _pick3_trading(rng, k, text)
    want = set(intents_of(text))
    fresh = lambda t: t[0] not in _LABEL_USED
    good = [t for t in TRIPLES if LABEL_TAGS.get(t[0], set()) & want]
    rest = [t for t in TRIPLES if t not in good]
    rng.shuffle(good); rng.shuffle(rest)
    order = [t for t in good if fresh(t)] + [t for t in rest if fresh(t)] + [t for t in good if not fresh(t)] + [t for t in rest if not fresh(t)]
    out = order[:k]
    for t in out: _LABEL_USED.add(t[0])
    return out

def g_shelf(text, rng):
    h = headline(text)
    n = 9
    idx = list(range(9)); rng.shuffle(idx)
    mode = rng.choice(["dim", "check", "q", "lock", "hsd", "plain"])
    k = rng.randint(2, 4)
    chosen = idx[:k]
    dim = tuple(i for i in range(9) if i not in chosen) if mode == "dim" else ()
    marks = {i: {"check": "check", "q": "q", "lock": "lock", "hsd": "hsd"}.get(mode, "check") for i in chosen} if mode != "plain" and mode != "dim" else ({chosen[0]: "q"} if mode == "dim" else {})
    col = rng.choice(ACCENTS)
    strip = rng.choice([None, ("coins", rng.choice(["Tiền thu về", "Vốn quay vòng", "Tiền đã chi nhập hàng"]), col), ("empty", rng.choice(["Ví gần như trống", "Tiền mặt chưa về"]), RED)])
    return shelf_scene(h, col, 9 if n == 9 else n, dim=dim, marks=marks, strip=strip)

def g_types3(text, rng):
    t = _pick3(rng, 3, text); c = _clauses(text)
    items = []
    for i, (lab, ic, col) in enumerate(t):
        items.append((lab, None, _icon(ic, 160, col), col))
    return sc_types(items)

def g_compare(text, rng):
    a, b = _pick3(rng, 2, text); c = _clauses(text)
    op = rng.choice(["≠", "→", "vs"])
    l, r = a[0], b[0]
    return sc_compare(l, r, op, _icon(a[1], 230, a[2]), _icon(b[1], 230, b[2]), a[2], b[2])

def g_flow3(text, rng):
    t = _pick3(rng, 3, text)
    return sc_flow3([("Bước %d" % (i + 1), lab, _icon(ic, 130, col), col) for i, (lab, ic, col) in enumerate(t)])

def g_checklist(text, rng):
    c = [_short(x, 20) for x in _clauses(text)][:4]
    while len(c) < 3: c.append(rng.choice(["Kiểm tra kế hoạch" if SB.TRADING[0] else "Kiểm tra hàng", "Xem báo cáo" if not SB.TRADING[0] else "Xem lại nhật ký lệnh", "Chọn xử lý" if not SB.TRADING[0] else "Chọn mức rủi ro", "Theo dõi mỗi ngày", "Giữ vốn quay" if not SB.TRADING[0] else "Giữ vốn an toàn"]))
    return sc_checklist(c[:4], rng.choice(["Việc cần làm", "Các bước", "Ghi nhớ", "Cần chú ý"]), rng.choice(["ok", "think"]))

_NUM = re.compile(r"\d+(?:[.,]\d+)?\s?(?:%|\$|đô|lệnh|lot)?", re.I)

def _num_labels(text, k):
    """Tên cột lấy từ 1-2 từ đứng ngay trước mỗi con số trong câu (ví dụ 'lỗ 3 lệnh' -> 'Lỗ')."""
    out = []
    for m in list(_NUM.finditer(text))[:k]:
        words = re.findall(r"[^\W\d_]+", text[:m.start()].split(",")[-1].split(".")[-1], re.U)[-3:]
        stop = {"là", "và", "với", "có", "được", "chỉ", "còn", "đang", "sẽ", "mỗi", "của", "khi", "thì", "mà"}
        while words and words[0].lower() in stop: words = words[1:]
        while words and words[-1].lower() in stop: words = words[:-1]
        while words and len(" ".join(words)) > 14: words = words[1:]
        out.append(" ".join(words).capitalize() if words else "")
    return out

def _bar_data(text, rng, labs):
    """Số trên cột: ưu tiên số có trong câu lời; không có thì số minh họa (cột hiện chữ 'Số liệu minh họa')."""
    nums = [m.group(0).strip() for m in _NUM.finditer(text)]
    if len(nums) >= 2:
        k = min(len(labs), len(nums)); vs = [float(re.match(r"\d+(?:[.,]\d+)?", x).group(0).replace(",", ".")) for x in nums[:k]]
        m = max(vs) or 1
        names = _num_labels(text, k)
        return [(names[i] or labs[i], max(.15, vs[i] / m * .9), ACCENTS[i % len(ACCENTS)], nums[i]) for i in range(k)], True
    out = []
    for i, l in enumerate(labs):
        if l in SB.BAR_DEMO:
            t, v = SB.BAR_DEMO[l]
        else:
            v = round(rng.uniform(.4, .9), 2); t = "%d%%" % round(v * 100)
        out.append((l, v, ACCENTS[i % len(ACCENTS)], t))
    return out, False

def g_bars(text, rng):
    n = rng.choice([2, 3, 3]); labs = rng.sample(CHART_LABELS, n)
    items, real = _bar_data(text, rng, labs)
    return sc_bars(items, demo=not real)

_ASSETS = [
    (re.compile(r"bitcoin|btc|ethereum|eth\b|crypto|tiền số|tiền điện tử|altcoin|coin", re.I), "btc", "Crypto"),
    (re.compile(r"vàng|gold|xau", re.I), "gold", "Vàng"),
    (re.compile(r"forex|ngoại hối|usd|eur|cặp tiền|tỷ giá|tỉ giá", re.I), "fx", "Forex"),
    (re.compile(r"cổ phiếu|chứng khoán|vn-?index|mã cp|sàn", re.I), "stock", "Chứng khoán"),
]
_PLAN_RX = re.compile(r"cắt lỗ|chốt lời|điểm vào|điểm thoát|stoploss|stop loss|take profit|r:r", re.I)
_EQ_RX = re.compile(r"sụt|drawdown|lợi nhuận|tăng trưởng|thua lỗ|mất tiền|cháy|tài khoản (?:tăng|giảm|mất)", re.I)

def _asset_of(text):
    for rx, ic, lab in _ASSETS:
        if rx.search(text): return ic, lab
    return None

def g_asset(text, rng):
    ic, lab = _asset_of(text) or ("btc", "Thị trường")
    if ic == "btc" and re.search(r"ethereum|eth\b", text, re.I): ic = "eth"; lab = "Ethereum"
    elif ic == "btc" and re.search(r"bitcoin|btc", text, re.I): lab = "Bitcoin"
    col = {"btc": ORANGE, "eth": CYAN, "gold": YEL, "fx": CYAN, "stock": GREEN}.get(ic, CYAN)
    els = [El(card_img(900, 980, col), 90, 160, 0, "rise", .6)]
    els.append(at(_icon(ic, 400, col), 540, 230, .3, "pop", .6, 4))
    els.append(tx(lab, 76, col, 540, 690, 800, .9))
    els.append(tx(headline(text, 44), 50, WHITE, 540, 810, 800, 1.2))
    return els

def g_plan(text, rng):
    up = not (re.search(r"giảm|xuống|short|bán", text.lower()) and not re.search(r"tăng|lên|mua", text.lower()))
    els = [El(card_img(900, 980, CYAN), 90, 160, 0, "rise", .6), at(chart_big_img(800, 520, up, True, rng.randint(0, 99999), load_closes()), 540, 205, .3, "wipe_l", 2.2)]
    els.append(tx(headline(text, 40), 50, WHITE, 540, 760, 800, 1.0))
    num = next(iter(_NUM.findall(text)), None)
    els.append(tx("Vào · Cắt lỗ · Chốt lời", 44, CYAN, 540, 880, 800, 1.3))
    if num: els.append(tx(num.strip(), 70, GREEN if up else RED, 540, 950, 800, 1.6))
    return els

def g_equity(text, rng):
    low = text.lower(); up = not re.search(r"sụt|drawdown|mất|thua|(?<!\w)lỗ(?!\w)|cháy|giảm", low)
    col = GREEN if up else RED; num = next(iter(_NUM.findall(text)), None)
    els = [El(card_img(900, 980, col), 90, 160, 0, "rise", .6), at(equity_img(800, 480, up, "", rng.randint(0, 99999)), 540, 210, .3, "wipe_l", 1.8)]
    els.append(tx(headline(text, 40), 52, WHITE, 540, 730, 800, 1.0))
    els.append(tx("Đường vốn tăng" if up else "Tài khoản sụt giảm", 46, col, 540, 870, 800, 1.3))
    if num: els.append(tx(num.strip(), 84, col, 540, 940, 800, 1.6))
    return els

def _vary(name, text, seed):
    """Dữ liệu minh họa khác nhau theo câu lời: cùng một loại bảng nhưng không còn giống hệt nhau."""
    r = random.Random(zlib.crc32((name + text).encode("utf-8")) + seed)
    nums = [m.group(0).strip() for m in _NUM.finditer(text)]
    if name == "gauge":
        lv = r.choice([.12, .2, .3, .5, .7, .85]); return (lv, (nums[0] if nums else "%d%%" % r.choice([1, 2, 3, 5, 10])))
    if name == "ticket":
        e = r.choice([100, 250, 1850, 64500, 1.08]); risk = r.choice([.01, .02, .03]); rr = r.choice([1.5, 2, 3]); up = r.random() < .6
        sl = e * (1 - risk if up else 1 + risk); tp = e * (1 + risk * rr if up else 1 - risk * rr)
        f = lambda v: ("%.2f" % v).replace(".", ",") if e < 10 else ("%.1f" % v).replace(".", ",")
        return (up, (f(e), f(sl), f(tp), ("1 : %s" % ("%g" % rr)).replace(".", ",")))
    if name == "calc":
        cap = r.choice([500, 1000, 2000, 5000, 10000]); rk = r.choice([1, 2, 3])
        return ("%s$" % format(cap, ",").replace(",", "."), "%d%%" % rk, "%s$" % format(int(cap * rk / 100), ",").replace(",", "."))
    if name == "pie":
        sets = [(.4, .3, .2, .1), (.3, .25, .25, .2), (.5, .2, .2, .1), (.25, .25, .25, .25), (.35, .35, .15, .15)]
        s = r.choice(sets); names = r.choice([("Crypto", "Vàng", "Ngoại tệ", "Cổ phiếu"), ("Cổ phiếu", "Vàng", "Tiền mặt", "Crypto"), ("Ngoại tệ", "Crypto", "Vàng", "Cổ phiếu")]); return (s, names)
    if name == "streak":
        n = 10; w_ = r.randint(3, 7); pat = ["W"] * w_ + ["L"] * (n - w_); r.shuffle(pat); return ("".join(pat),)
    if name == "heat":
        return (tuple(round(r.uniform(-3, 3), 1) for _ in range(6)),)
    if name == "journal":
        good = ["Vào theo kế hoạch", "Chờ đủ tín hiệu", "Cắt lỗ đúng điểm", "Chốt lời theo kế hoạch", "Giữ đúng khối lượng"]
        bad = ["Vào vì FOMO", "Dời điểm cắt lỗ", "Tăng khối lượng gỡ lỗ", "Vào lệnh không kế hoạch", "Bỏ qua cắt lỗ"]
        rows = [(r.choice(good), r.choice(["+1R", "+2R", "+3R"])) if r.random() < .5 else (r.choice(bad), r.choice(["-1R", "-2R"])) for _ in range(4)]; return (tuple(rows),)
    return ()

_PANELS = {  # tên -> (từ khoá, hàm vẽ(w,h,text,seed), nhãn)
    "gauge": (r"rủi ro|nguy hiểm|an toàn", lambda w, h, t, s: gauge_img(w, h, *_vary("gauge", t, s)), "Mức rủi ro"),
    "ticket": (r"vào lệnh|đặt lệnh|mở lệnh|lệnh mua|lệnh bán", lambda w, h, t, s: ticket_img(w, h, *_vary("ticket", t, s)), "Lệnh giao dịch"),
    "fear": (r"cảm xúc|fomo|sợ|tham lam|hoảng|tâm lý", lambda w, h, t, s: fear_img(w, h, s), "Cảm xúc và giá"),
    "calc": (r"phần trăm vốn|% vốn|mỗi lệnh chỉ|rủi ro một đến|khối lượng lệnh|tính", lambda w, h, t, s: calc_img(w, h, *_vary("calc", t, s)), "Tính rủi ro mỗi lệnh"),
    "book": (r"thanh khoản|sổ lệnh|người mua|người bán|cung cầu", lambda w, h, t, s: book_img(w, h, s), "Sổ lệnh mua bán"),
    "pie": (r"danh mục|phân bổ|đa dạng|chia vốn", lambda w, h, t, s: pie_img(w, h, *_vary("pie", t, s)), "Phân bổ danh mục"),
    "streak": (r"liên tiếp|chuỗi|thắng|thua mười|mười lệnh", lambda w, h, t, s: streak_img(w, h, *_vary("streak", t, s)), "Chuỗi lệnh"),
    "heat": (r"thị trường|chỉ số|toàn cảnh|biến động", lambda w, h, t, s: heat_img(w, h, *_vary("heat", t, s)), "Bản đồ thị trường"),
    "journal": (r"nhật ký|ghi lại|ghi chép|ghi nhớ", lambda w, h, t, s: journal_img(w, h, *_vary("journal", t, s)), "Nhật ký lệnh"),
}

def _panels_of(text):
    low = text.lower()
    return [n for n, (rx, _, _) in _PANELS.items() if re.search(rx, low)]

def _g_panel(name):
    def make(text, rng):
        rx, draw, lab = _PANELS[name]; seed = rng.randint(0, 99999); lay = rng.choice(["card", "big", "top"]); col = rng.choice([CYAN, GREEN, ORANGE, YEL])
        if lay == "card":
            els = [El(card_img(900, 980, col), 90, 160, 0, "rise", .6), at(draw(800, 520, text, seed), 540, 205, .3, "pop", .6)]
            els.append(tx(headline(text, 40), 52, WHITE, 540, 760, 800, 1.0)); els.append(tx(lab, 44, col, 540, 900, 800, 1.3))
        elif lay == "big":  # bảng phóng to gần hết khung, không thẻ ngoài
            els = [tx(headline(text, 34), 56, WHITE, 540, 150, 940, .2), at(draw(960, 700, text, seed), 540, 330, .4, "pop", .6), tx(lab, 46, col, 540, 1060, 900, 1.2)]
        else:  # chữ lớn phía trên, bảng nhỏ bên dưới
            els = [tx(_sentence(text, 70), 60, WHITE, 540, 170, 900, .2, gap=1.15), at(draw(860, 560, text, seed), 540, 560, .5, "pop", .6), tx(lab, 44, col, 540, 1130, 900, 1.3)]
        return els
    return make

def g_candle(text, rng):
    low = text.lower(); up = not (re.search(r"giảm|xuống|(?<!\w)lỗ(?!\w)|mất|thua|sập|cháy", low) and not re.search(r"tăng|lên", low))
    col = GREEN if up else RED
    els = [El(card_img(900, 980, col), 90, 160, 0, "rise", .6), at(candle_img(780, 440, up, rng.randint(0, 99999)), 540, 215, .3, "wipe_l", 2.0)]
    els.append(tx(headline(text, 40), 52, WHITE, 540, 685, 800, .9))
    num = next(iter(_NUM.findall(text)), None)
    els.append(tx("Xu hướng tăng" if up else "Xu hướng giảm", 46, col, 540, 835, 800, 1.2))
    if num: els.append(tx(num.strip(), 80, col, 540, 910, 800, 1.5))
    return els

def SB_TRADING(): return bool(SB.TRADING[0])

def g_stat(text, rng):
    big = next(iter(_NUM.findall(text)), None) or next(iter(re.findall(r"\d+[%\w ]{0,6}", text)), None) or "—"
    big = big.strip()[:8]
    col = rng.choice(ACCENTS); lab, ic, icol = _pick3(rng, 1, text)[0]; h = headline(text, 40)
    els = [El(card_img(900, 980, col), 90, 160, 0, "rise", .6)]
    m = re.fullmatch(r"(\d{1,3}(?:[.,]\d{3})*|\d+)(\s*(?:%|\$|k|tr|x|lần|đồng)?)", big)
    if m and SB_TRADING():
        v = float(m.group(1).replace(".", "").replace(",", "")); suf = m.group(2)
        els.append(SC.count_el(v, 200, col, 540, 230, 820, .3, 1.5, "", suf))
    else:
        els.append(tx(big, 190, col, 540, 230, 820, .3))
    els.append(tx(h, 58, WHITE, 540, 520, 800, .7))
    els.append(at(_icon(ic, 240, icol), 540, 700, .9, "pop", .5, 4))
    els.append(tx(lab, 52, icol, 540, 1000, 800, 1.2))
    return els

def g_hub(text, rng):
    t = _pick3(rng, 4, text); c = _clauses(text)
    hubt = _short(c[0], 12) if c else SB.HUB[0]
    els = [tx(headline(text, 30), 50, CYAN, 540, 124, 900)]
    hub = Cv(300, 300); hub.shadow((20, 250, 280, 300), 10, 120); hub.ell((10, 6, 290, 286), fill=(8, 22, 52), outline=CYAN, width=8)
    hub.text(150, 100, hubt, 40, CYAN, anchor="m", maxw=250, gap=1.1)
    els.append(El(hub.out(), 390, 560, .3, "pop", .6))
    pos = [(80, 250), (610, 250), (80, 880), (610, 880)]
    for k, ((lab, ic, col), (x, y)) in enumerate(zip(t, pos)):
        els.append(El(card_img(390, 300, col), x, y, .6 + k * .3, "rise")); im = _icon(ic, 130, col)
        els.append(El(im, x + (390 - im.width) / 2, y + 30, .8 + k * .3, "pop")); els.append(tx(lab, 44, col, x + 195, y + 205, 340, .9 + k * .3))
    return els

def g_donut(text, rng):
    m = re.search(r"(\d{1,3})\s*%", text)
    p = min(1.0, int(m.group(1)) / 100) if m else rng.choice([.25, .5, .75]); col = rng.choice(ACCENTS); other = rng.choice([c for c in ACCENTS if c != col])
    cv = Cv(560, 560); cv.arc((30, 30, 530, 530), 0, 360, (40, 74, 120), 70); cv.arc((30, 30, 530, 530), -90, -90 + 360 * p, col, 70)
    cv.text(280, 205, "%d%%" % int(p * 100), 130, WHITE, anchor="m")
    els = [tx(headline(text, 30), 50, col, 540, 124, 900), El(cv.out(), 260, 250, .2, "pop", .7)]
    a, b = _pick3(rng, 2, text)
    for k, ((lab, ic, c2), x, cc) in enumerate(((a, 90, col), (b, 560, other))):
        els.append(El(card_img(430, 290, cc), x, 880, .8 + k * .4, "rise")); im = _icon(ic, 120, c2)
        els.append(El(im, x + (430 - im.width) / 2, 905, 1.0 + k * .4, "pop")); els.append(tx(lab, 42, cc, x + 215, 1035, 380, 1.1 + k * .4))
    return els

def g_steps(text, rng):
    t = _pick3(rng, 4, text); els = [tx(rng.choice(["Từng bước", "Quy trình", "Cách làm"]), 56, CYAN, 540, 124, 900)]
    for i, (lab, ic, col) in enumerate(t):
        y = 220 + i * 275
        els.append(El(card_img(760, 230, col), 230, y, .3 + i * .4, "rise"))
        n = Cv(110, 110); n.ell((4, 4, 106, 106), fill=col); n.text(55, 18, str(i + 1), 68, INK, anchor="m"); els.append(El(n.out(), 90, y + 50, .4 + i * .4, "pop", .4))
        im = _icon(ic, 120, col); els.append(El(im, 250, y + (230 - im.height) / 2 - 8, .5 + i * .4, "pop"))
        els.append(tx(lab, 46, col, 700, y + 70, 420, .6 + i * .4, gap=1.1))
    return els

def g_hero(text, rng):
    lab, ic, col = _pick3(rng, 1, text)[0]; c = _clauses(text)
    disc = Cv(700, 700); disc.ell((0, 0, 700, 700), fill=shade(col, -.7) if False else (14, 40, 84), outline=col, width=6)
    els = [El(disc.out(), 190, 170, 0, "pop", .6), at(_icon(ic, 420, col), 540, 330, .3, "pop", .6, 5)]
    els.append(tx(headline(text, 40), 62, WHITE, 540, 930, 900, .7))
    if len(c) > 1: els.append(tx(_short(c[1], 50), 44, GREY, 540, 1120, 900, 1.0, bold=False))
    return els

def g_grid4(text, rng):
    t = _pick3(rng, 4, text); els = [tx(headline(text, 30), 50, CYAN, 540, 124, 900)]
    pos = [(80, 230), (560, 220), (80, 720), (560, 720)]
    for k, ((lab, ic, col), (x, y)) in enumerate(zip(t, pos)):
        els.append(El(card_img(400, 440, col), x, y, .3 + k * .3, "rise")); im = _icon(ic, 200, col)
        els.append(El(im, x + (400 - im.width) / 2, y + 40, .5 + k * .3, "pop")); els.append(tx(lab, 44, col, x + 200, y + 310, 360, .6 + k * .3))
    return els

def _sentence(text, n=84):
    t = text.strip().rstrip(".!?")
    if len(t) > n:
        t = t[:n].rsplit(" ", 1)[0] + "…"
    return t[:1].upper() + t[1:]

def g_quote(text, rng):
    col = rng.choice(ACCENTS)
    if rng.random() < .5: return sc_question("“" + _sentence(text, 84) + "”", rng.choice(["worry", "think"]))
    return sc_note(_sentence(text, 70), col, rng.choice(["think", "worry", "ok"]))


def g_typo(text, rng):
    col = rng.choice(ACCENTS); lab, ic, icol = _pick3(rng, 1, text)[0]
    els = [El(card_img(900, 860, col), 90, 170, 0, "rise", .6)]
    q = Cv(160, 160); q.text(80, -10, "“", 220, col, anchor="m"); els.append(El(q.out(), 120, 190, .2, "pop", .5))
    els.append(tx(_sentence(text, 80), 66, WHITE, 540, 380, 780, .5, gap=1.22))
    els.append(El(_icon(ic, 120, icol), 780, 790, 1.0, "pop", .5, 4))
    els.append(tx(lab, 44, icol, 400, 830, 420, 1.1))
    return els

POSES = ("", "point", "phone", "chin")
def person_v(w, h, mood, rng):
    if SB.TRADING[0]:
        import illus
        return illus.trader_img(w, h, mood, rng.randint(0, 1), rng.choice(POSES))
    return person_img(w, h, mood)

def g_think(text, rng):
    lab, ic, icol = _pick3(rng, 1, text)[0]; mood = rng.choice(["worry", "think", "ok"])
    els = [El(bubble_img(860, 420, "bl"), 110, 150, 0, "pop", .6)]
    els.append(at(_icon(ic, 190, icol), 540, 200, .5, "pop", .5, 4))
    els.append(tx(lab, 46, INK, 540, 400, 700, .8))
    els.append(at(person_v(380, 560, mood, rng), 540, 640, .3, "rise", .6, 4))
    els.append(tx(headline(text, 34), 52, WHITE, 540, 1220, 900, 1.0))
    return els

def g_split(text, rng):
    a, b = _pick3(rng, 2, text); els = []
    for k, ((lab, ic, col), x) in enumerate(((a, 70), (b, 560))):
        els.append(El(card_img(450, 960, col), x, 190, k * .5, "rise"))
        im = _icon(ic, 260, col); els.append(El(im, x + (450 - im.width) / 2, 330, .3 + k * .5, "pop", .5, 4))
        els.append(tx(lab, 54, col, x + 225, 760, 400, .5 + k * .5, gap=1.1))
    els.append(tx(headline(text, 30), 50, CYAN, 540, 124, 900))
    return els

def g_iconrow(text, rng):
    t = _pick3(rng, 3, text); els = [tx(headline(text, 30), 52, CYAN, 540, 124, 900)]
    xs = (190, 540, 890)
    for k, ((lab, ic, col), x) in enumerate(zip(t, xs)):
        im = _icon(ic, 220, col); els.append(at(im, x, 430 + (220 - im.height) / 2, .3 + k * .5, "pop", .5, 4))
        els.append(tx(lab, 40, col, x, 720, 300, .5 + k * .5, gap=1.08))
        if k < 2: els.append(at(arrow_img(80, 20, GREY), (xs[k] + xs[k + 1]) / 2, 520, .6 + k * .5, "wipe_l"))
    els.append(El(card_img(900, 220, rng.choice(ACCENTS)), 90, 900, 1.8, "rise"))
    els.append(tx(_sentence(text, 56), 46, WHITE, 540, 960, 820, 2.0, gap=1.15))
    return els

GENERIC = {
    "shelf": g_shelf, "types3": g_types3, "compare": g_compare, "flow3": g_flow3, "checklist": g_checklist, "bars": g_bars,
    "stat": g_stat, "typo": g_typo, "think": g_think, "split": g_split, "iconrow": g_iconrow, "hub": g_hub, "donut": g_donut, "steps": g_steps, "hero": g_hero, "grid4": g_grid4, "quote": g_quote, "candle": g_candle, "asset": g_asset, "plan": g_plan, "equity": g_equity,
}
for _n in _PANELS:
    GENERIC["p_" + _n] = _g_panel(_n)
FAMILY_GENERIC = {
    "pharmacy": ("shelf", "think", "typo", "hero", "iconrow", "split", "quote", "grid4", "types3", "stat"),
    "goods": ("shelf", "hero", "iconrow", "think", "typo", "split", "stat", "grid4", "compare", "types3", "quote"),
    "data": ("donut", "bars", "typo", "stat", "iconrow", "hero", "hub", "types3", "split", "compare"),
    "chart": ("bars", "donut", "split", "iconrow", "stat", "typo", "compare", "types3", "grid4"),
    "contrast": ("split", "compare", "think", "typo", "quote", "iconrow", "types3", "stat", "hero"),
    "process": ("iconrow", "steps", "think", "typo", "flow3", "checklist", "split", "hub", "grid4", "types3"),
    "interface": ("hub", "typo", "iconrow", "grid4", "hero", "stat", "steps", "bars", "think"),
}

_HOOK_RX = re.compile(r"\?|bạn có biết|vì sao|tại sao", re.I)

def g_hook(text, rng):
    """Cảnh mở đầu cho trading: câu hỏi/khẳng định lớn + nhân vật chỉ tay."""
    import illus
    col = rng.choice([CYAN, ORANGE, YEL])
    chip = Cv(360, 80); chip.rect((0, 0, 360, 80), fill=shade(col, -.55), outline=col, width=4, r=40); chip.text(180, 14, "SỰ THẬT" if "?" not in text else "BẠN CÓ BIẾT", 40, col, anchor="m")
    els = [at(chip.out(), 540, 150, 0, "pop", .5), tx(text.strip(), 76, WHITE, 540, 300, 900, .3, gap=1.2)]
    tr = illus.trader_img(380, 540, "think" if "?" in text else "ok", rng.randint(0, 1), "point")
    els.append(at(tr, 540, 760, .6, "rise", .6, 4))
    return els

GENERIC["hook"] = g_hook

def generics(layout, text):
    fam = layout.split("_")[0]
    names = FAMILY_GENERIC.get(fam, ("hero", "types3", "quote"))
    if SB.TRADING[0]:
        front = []
        if _HOOK_RX.search(text) and len(text) < 140: front.append("hook")
        if _asset_of(text): front.append("asset")
        if _PLAN_RX.search(text): front.append("plan")
        if _EQ_RX.search(text): front.append("equity")
        front += ["p_" + n for n in _panels_of(text)]
        if re.search(r"nến|xu hướng|biểu đồ|giá |đường giá|đỉnh|đáy", text.lower()): front.append("candle")
        names = tuple(front) + tuple(n for n in names if n not in ("shelf", "expiry", "locked"))  # cảnh hộp thuốc/kệ/hạn dùng chỉ dành cho nhà thuốc
    out = []
    has_num = bool(re.search(r"\d", text))
    names = tuple(n for n in names if has_num or n not in (("stat", "donut", "bars") if SB.TRADING[0] else ("stat", "donut")))  # không bịa số liệu
    for r in range(4):  # mỗi kiểu sinh nhiều bản theo hạt giống khác nhau
        for nm in names:
            seed = (r + 1) * 31 + len(out)
            out.append(("g-%s-%s-%d" % (layout, nm, r), (lambda nm=nm, seed=seed: GENERIC[nm](text, _rng(layout + nm, text, seed)))))
    return out

# --- chọn cảnh: không lặp bản vẽ, hạn chế lặp "kiểu", cảnh có chữ cố định chỉ dùng khi lời có liên quan ---
KIND_CAP = {"chart": 1, "p_gauge": 1, "p_ticket": 1, "p_fear": 1, "p_calc": 1, "p_book": 1, "p_pie": 1, "p_streak": 1, "p_heat": 1, "p_journal": 1, "asset": 4, "shelf": 2, "bars": 2, "quote": 2, "brand": 1, "hub": 1, "dash": 1, "locked": 1, "donut": 2, "steps": 2, "compare": 2, "types": 2, "flow": 2, "check": 2, "stat": 2, "hero": 2, "grid": 2, "typo": 2, "think": 2, "split": 2, "iconrow": 2}
_KINDS = {}
_LAST = [""]
CARDISH = {"types", "flow", "steps", "compare", "hub", "grid", "check", "cash-types", "carton-types"}
_TOTAL = [0, 0]  # [số cảnh, số cảnh dạng thẻ]

def kind_of(key):
    k = key.lower()
    if k.startswith("g-"):
        k = k.split("-")[2]
    for pref, kind in (("hook", "hook"), ("candle", "chart"), ("plan", "chart"), ("equity", "chart"), ("shelf", "shelf"), ("bars", "bars"), ("cmp", "compare"), ("compare", "compare"), ("types", "types"), ("check", "check"),
                       ("flow", "flow"), ("note", "quote"), ("question", "quote"), ("quote", "quote"), ("modules", "hub"), ("hub", "hub"),
                       ("dash", "dash"), ("cta", "brand"), ("title", "brand"), ("locked", "locked"), ("donut", "donut"), ("steps", "steps"),
                       ("stat", "stat"), ("hero", "hero"), ("grid", "grid"), ("expiry", "expiry"), ("speed", "speed"), ("cycle", "cycle")):
        if k.startswith(pref):
            return kind
    return k

# chữ cố định trong cảnh -> chỉ dùng khi câu lời có từ khoá liên quan
NEEDS = {
    "modules": r"nền tảng|novixa|hệ thống|dữ liệu|một nơi",
    "cta": r"nền tảng|novixa|tìm hiểu|đường dẫn|thử",
    "title-hero": r"nền tảng|novixa",
    "dash-hero": r"nền tảng|novixa|bảng|giao diện|cảnh báo|theo dõi",
    "dash-exp": r"hết hạn|cận hạn|cảnh báo|theo dõi",
    "dash": r"nền tảng|novixa|bảng|giao diện|cảnh báo|theo dõi",
    "receipt": r"ghi nhận|giao dịch|doanh số|hóa đơn|bán được",
    "notebook": r"ghi chép|sổ|giấy|nhớ|trí nhớ",
    "places": r"kệ|tủ|kho|ở đâu|nằm",
    "options": r"đẩy bán|giảm giá|tổn thất|xử lý|lựa chọn",
    "timeline": r"kiểm kê|sớm|muộn|cuối tháng|theo dõi",
    "cycle": r"vòng|quay|nhập hàng|bán hàng|tiền về",
    "cycle-broken": r"vòng|quay|chậm|giữ",
    "scale": r"giá|rẻ|lợi|chi phí|cân|so sánh",
    "compare-pile": r"giá|rẻ|hiệu quả",
    "speed-slow": r"chậm|nằm|kệ|giữ",
    "speed-fast": r"nhanh|hết|bán",
    "day": r"mở cửa|đóng cửa|ngày|sáng|tối",
    "flow-normal": r"vẫn|bình thường|khách|đông",
    "types-speed": r"nhanh|chậm|hạn",
    "types-3": r"nhóm|loại|hàng",
    "bars-single": r"doanh thu",
    "bars-hidden": r"không thấy|chưa nhìn|ẩn|doanh thu",
    "bars-ok": r"doanh thu|bán|tiền",
    "bars-fall": r"giảm|chậm|âm thầm|xu hướng|doanh thu",
    "bars-rank": r"bán chạy|bán chậm|xếp|nhóm",
    "cmp-dates": r"ngày|hạn|nhập",
    "expiry-30": r"hạn|hsd|hết",
    "expiry-80": r"hạn|hsd|hết",
    "expiry-65": r"hạn|hsd|hết|thời gian",
    "cmp-early": r"sớm|muộn|phát hiện",
    "cmp-busy": r"làm việc|chăm|bận|sớm",
    "cmp-habit": r"thói quen|cảm giác|thực tế",
    "check-busy": r"chăm|mỗi ngày|nhân viên|kiểm tra",
    "check-ok": r"vẫn|ổn|bình thường",
    "shelf-cut": r"cắt|tồn kho|mọi giá",
    "cmp-less": r"ít hàng|quản lý|tốt",
    "locked": r"tiền|vốn|giữ|nhập|hộp",
    "carton-types": r"nhập|giá|ưu đãi|nhiều",
    "cash-types": r"tiền|vốn|mặt",
    "cracked": r"hết hạn|mất|tổn thất",
    "mag": r"nhìn|để ý|phát hiện|soi",
}

def _ok_for(key, low):
    need = NEEDS.get(key)
    return True if need is None else bool(re.search(need, low))

_LAST_KEY = [""]
ORIENT = ["tall"]  # "tall" 1080x1920 | "wide" 1920x1080

def _wide():
    import studio_plates_wide as W
    return W

def _pool(layout, text):
    if ORIENT[0] == "wide":
        return _wide().generics_w(layout, text)
    low = text.lower()
    if SB.TRADING[0]:  # brand trading: bỏ cảnh dựng sẵn mang dáng nhà thuốc, chỉ dùng bộ cảnh chung + cảnh tài chính
        return generics(layout, text)
    return [v for v in variants(layout, text) if _ok_for(v[0], low)] + generics(layout, text)

def build(layout, text, pick=0):
    pool = _pool(layout, text)
    rng = random.Random(zlib.crc32((layout + text).encode("utf-8")) + int(pick) * 104729)
    rng.shuffle(pool)
    prefs = []
    for it in intents_of(text):
        prefs += list(INTENT_PREF.get(it, ()))
    def tier(item):  # ưu tiên cảnh hợp ý câu lời (khớp càng sớm trong danh sách càng tốt)
        key = item[0]
        if SB.TRADING[0] and any("-%s-" % n in key for n in ("asset", "plan", "equity", "hook")) or "-p_" in key: return -1  # câu nhắc tài sản/lệnh/vốn thì ưu tiên hình tài chính
        for i, pf in enumerate(prefs):
            if pf in key: return i
        return 99
    pool.sort(key=tier)  # sort ổn định: trong cùng bậc vẫn giữ thứ tự ngẫu nhiên
    def take(relax_kind, relax_key):
        for key, fn in pool:
            kind = kind_of(key)
            if not relax_key and key in _USED: continue
            if not relax_kind and (_KINDS.get(kind, 0) >= KIND_CAP.get(kind, 2) or kind == _LAST[0]): continue
            if not relax_kind and kind in CARDISH and _TOTAL[1] >= 0.3 * _TOTAL[0] + 1: continue
            return key, fn, kind
        return None
    got = take(False, False) or take(False, True) or take(True, False) or take(True, True)
    key, fn, kind = got
    _USED.add(key); _KINDS[kind] = _KINDS.get(kind, 0) + 1; _LAST[0] = kind; _LAST_KEY[0] = key
    _TOTAL[0] += 1
    if kind in CARDISH: _TOTAL[1] += 1
    return fn()

def build_by_key(layout, text, key):
    for k, fn in _pool(layout, text):
        if k == key:
            return fn()
    if ORIENT[0] == "wide":
        return None
    for k, fn in variants(layout, text) + generics(layout, text):
        if k == key:
            return fn()
    return None

# ===================== nền/khung: nhiều phong cách xen kẽ =====================
THEMES = [
    dict(n="navy", top=(6, 18, 44), bot=(16, 40, 78), board=(15, 36, 74), line=(40, 120, 190), pat="grid", frame="board"),
    dict(n="teal", top=(4, 30, 40), bot=(8, 66, 76), board=(8, 46, 58), line=(40, 190, 180), pat="dots", frame="board"),
    dict(n="plum", top=(26, 10, 44), bot=(64, 26, 86), board=(40, 16, 64), line=(170, 100, 230), pat="diag", frame="bleed"),
    dict(n="forest", top=(6, 28, 22), bot=(14, 66, 46), board=(10, 46, 34), line=(70, 200, 130), pat="grid", frame="bleed"),
    dict(n="charcoal", top=(14, 16, 22), bot=(40, 44, 58), board=(26, 30, 40), line=(150, 170, 200), pat="none", frame="glass"),
    dict(n="indigo", top=(10, 12, 56), bot=(32, 38, 124), board=(20, 24, 92), line=(120, 140, 255), pat="dots", frame="glass"),
]
_BASES = {}

def _lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))

def _themed_base(idx):
    th = THEMES[idx % len(THEMES)]
    W_, H_ = SC.W, SC.H
    im = Image.new("RGB", (W_, H_), th["top"]); d = ImageDraw.Draw(im)
    for yy in range(0, H_, 4):
        t = min(1.0, yy / 1500)
        d.rectangle((0, yy, W_, yy + 4), fill=_lerp(th["top"], th["bot"], t))
    x0, y0, x1, y1 = SC.BOARD
    pat = Image.new("RGBA", (W_, H_), (0, 0, 0, 0)); pd = ImageDraw.Draw(pat); lc = th["line"] + (34,)
    if th["pat"] == "grid":
        for gx in range(x0 + 50, x1, 80): pd.line((gx, y0 + 14, gx, y1 - 14), fill=lc, width=1)
        for gy in range(y0 + 50, y1, 80): pd.line((x0 + 14, gy, x1 - 14, gy), fill=lc, width=1)
    elif th["pat"] == "dots":
        for gx in range(x0 + 40, x1, 64):
            for gy in range(y0 + 40, y1, 64): pd.ellipse((gx - 3, gy - 3, gx + 3, gy + 3), fill=th["line"] + (46,))
    elif th["pat"] == "diag":
        for k in range(-1400, 1400, 90): pd.line((x0 + k, y1, x0 + k + 900, y0), fill=lc, width=2)
    elif th["frame"] == "glass":
        pass
    frame = th["frame"]
    if frame == "board":
        sh = Image.new("RGBA", (W_, H_), (0, 0, 0, 0)); ImageDraw.Draw(sh).rounded_rectangle((x0 + 6, y0 + 18, x1 + 6, y1 + 18), radius=34, fill=(0, 0, 0, 150))
        from PIL import ImageFilter
        sh = sh.filter(ImageFilter.GaussianBlur(20)); im.paste(sh, (0, 0), sh)
        d = ImageDraw.Draw(im); d.rounded_rectangle(SC.BOARD, radius=34, fill=th["board"], outline=th["line"], width=3)
    elif frame == "glass":
        gl = Image.new("RGBA", (W_, H_), (0, 0, 0, 0)); gd = ImageDraw.Draw(gl)
        gd.rounded_rectangle(SC.BOARD, radius=44, fill=th["board"] + (150,), outline=th["line"] + (200,), width=2)
        gd.rounded_rectangle((x0 + 40, y1 - 14, x1 - 40, y1 - 6), radius=4, fill=th["line"] + (230,))
        im = Image.alpha_composite(im.convert("RGBA"), gl).convert("RGB")
    else:  # bleed: không khung, hoa văn trải rộng + vạch nhấn phía trên
        d = ImageDraw.Draw(im); d.rectangle((0, 88, W_, 94), fill=th["line"])
    im = Image.alpha_composite(im.convert("RGBA"), pat).convert("RGB")
    d = ImageDraw.Draw(im); _tf = SC.F(40); d.text((W_ - 100 - d.textlength(SB.TAG[0], font=_tf), 40), SB.TAG[0], font=_tf, fill=th["line"] if th["n"] != "navy" else SC.CYAN)
    d.rectangle((0, 1340, W_, H_), fill=th["bot"] if th["n"] == "navy" else _lerp(th["bot"], (0, 0, 0), .55))
    return im

def base_plate(theme=0):
    if ORIENT[0] == "wide":
        return _wide().base_plate_wide(theme)
    key = int(theme) % len(THEMES)
    if key not in _BASES:
        _BASES[key] = _themed_base(key)
    return _BASES[key].copy()

_THEME_USE = {}
_THEME_LAST = [-1]

def _choose_theme(text):
    order = sorted(range(len(THEMES)), key=lambda i: (_THEME_USE.get(i, 0), zlib.crc32((text + str(i)).encode("utf-8"))))
    pick = next((i for i in order if i != _THEME_LAST[0]), order[0])
    _THEME_USE[pick] = _THEME_USE.get(pick, 0) + 1; _THEME_LAST[0] = pick
    return pick


def prime(paths):
    """Nạp 'lịch sử' từ các ảnh thư viện đã có trong phim (khi vẽ lại một ảnh lẻ), để cảnh mới không trùng các cảnh còn lại."""
    for path in paths:
        meta = library_key(path)
        if not meta:
            continue
        key = meta[2]
        kind = kind_of(key)
        if key in _USED:
            continue
        _USED.add(key)
        _KINDS[kind] = _KINDS.get(kind, 0) + 1
        _TOTAL[0] += 1
        if kind in CARDISH:
            _TOTAL[1] += 1
        th = meta[3]
        _THEME_USE[th] = _THEME_USE.get(th, 0) + 1

def plate(layout, text, out, pick=0, orient="tall", brand=None):
    """pick: số tuỳ ý (ví dụ arrangement + chỉ số câu) để mỗi lần vẽ chọn biến thể khác.
    brand: dict brand của Studio. Không có/không nhận ra màu thì giữ diện mạo mặc định."""
    ORIENT[0] = "wide" if orient == "wide" else "tall"
    prof = SB.apply(sys.modules[__name__], brand)
    n = _COUNT.get(layout, 0); _COUNT[layout] = n + 1
    theme = _choose_theme(text)
    fr = base_plate(theme)
    for e in build(layout, text, int(pick) + n):
        SC.draw_el(fr, e, 99)
    from PIL import PngImagePlugin
    meta = PngImagePlugin.PngInfo()
    meta.add_text("novixa_layout", layout); meta.add_text("novixa_text", text); meta.add_text("novixa_key", _LAST_KEY[0]); meta.add_text("novixa_theme", str(theme)); meta.add_text("novixa_orient", ORIENT[0])
    if prof:
        meta.add_text("novixa_brand", SB.compact(prof))
    fr.convert("RGB").save(out, pnginfo=meta)
    return out

# ---------- chuyển động: dựng lại đúng cảnh từ khoá lưu trong PNG ----------
_ANIM = {}
_BASE = []

def library_key(path):
    try:
        with Image.open(path) as im:
            info = getattr(im, "text", None) or im.info
            if info.get("novixa_key"):
                return info.get("novixa_layout", ""), info.get("novixa_text", ""), info["novixa_key"], int(info.get("novixa_theme", 0) or 0), info.get("novixa_orient", "tall")
    except Exception:
        pass
    return None

def library_brand(path):
    try:
        with Image.open(path) as im:
            info = getattr(im, "text", None) or im.info
            return info.get("novixa_brand")
    except Exception:
        return None

def animated(path, t):
    """Khung hình tại giây t (tính từ lúc câu bắt đầu). Trả None nếu PNG không phải ảnh thư viện."""
    ent = _ANIM.get(path)
    if ent is None:
        meta = library_key(path)
        brand_txt = library_brand(path)
        if meta:
            ORIENT[0] = meta[4]
            SB.apply(sys.modules[__name__], SB.expand(brand_txt) if brand_txt else None)
        els = build_by_key(*meta[:3]) if meta else None
        ent = _ANIM[path] = ((els, meta[3], meta[4], brand_txt) if els else False)
    if not ent:
        return None
    els, theme, orient, brand_txt = ent
    ORIENT[0] = orient
    SB.apply(sys.modules[__name__], SB.expand(brand_txt) if brand_txt else None)
    fr = base_plate(theme)
    for e in els:
        SC.draw_el(fr, e, max(0.0, t))
    return fr.convert("RGB")

if __name__ == "__main__":
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    samples = {
     "pharmacy_sleep": "Hàng nằm im trên kệ nhà thuốc, từng ngày trôi qua",
     "pharmacy_quiet": "Quầy vẫn ồn ào, nhưng mọi thứ vẫn ổn",
     "pharmacy_cabinet": "Mở tủ ra, hàng đã hết hạn rồi, tiền nằm trong đó",
     "pharmacy_group": "Có ba nhóm hàng cần phân biệt",
     "goods_macro": "Từng hộp thuốc đang chờ được bán",
     "goods_pile": "Nhập hơi nhiều, tiền bị giữ trong hàng",
     "goods_lone": "Một mặt hàng không ai để ý",
     "data_cash": "Tiền thật nằm ở đâu, bỏ tiền vào đâu",
     "data_where": "Vốn nằm ở đâu?",
     "chart_fall": "Doanh thu bán chậm âm thầm đi xuống",
     "chart_rank": "Mặt hàng đang bán chậm nhất",
     "contrast_stop": "Vấn đề không quay về từ hôm nay",
     "contrast_split": "Không chỉ là chuyện làm việc chăm chỉ",
     "contrast_dates": "Vấn đề không bắt đầu từ ngày hết hạn",
     "contrast_aside": "Điều ít người nói tới",
     "process_queue": "Ưu tiên xử lý theo phiếu, chữ ký, kiểm tra",
     "process_late": "Kiểm kê khi đã quá muộn",
     "process_act": "Ra tay xử lý trước khi quá muộn",
     "process_early": "Biết sớm điều cần biết",
     "process_next": "Tập sau, chỉ cần 5 phút mỗi ngày",
     "interface_hero": "Nền tảng Novixa giúp nhìn thấy tất cả",
     "interface_expiry": "Hàng sắp hết hạn được báo sớm",
    }
    for k, v in samples.items():
        plate(k, v, f"{out}/{k}.png"); print(k, flush=True)

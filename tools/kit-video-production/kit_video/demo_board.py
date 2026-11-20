"""Controlled demo graphics. Numbers are drawn from one spec. This is a mockup, not a product screenshot."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

DEMO_LABEL = "Dữ liệu minh họa"
DEMO_CAPTION = "Dữ liệu mô phỏng"
SHELF_LABELS = (
    "THUỐC KÊ ĐƠN",
    "THUỐC KHÔNG KÊ ĐƠN",
    "THỰC PHẨM BẢO VỆ SỨC KHỎE",
    "THIẾT BỊ Y TẾ",
    "DƯỢC MỸ PHẨM",
)
MOCKUP_BADGES = ("MOCKUP", "DEMO")
FORBIDDEN_PRODUCT_FIELDS = ("dosage", "dose", "strength", "indication", "usage")


def format_vnd_per_day(amount: int) -> str:
    grouped = f"{int(amount):,}".replace(",", ".")
    return f"{grouped} VNĐ/ngày"


def sunday_share_percent(data: dict) -> int:
    series = data.get("orderSeries") or []
    total = sum(int(item["orders"]) for item in series)
    if total <= 0 or not series:
        return 0
    return int(series[-1]["orders"]) * 100 // total


def demo_issues(spec: dict) -> list[dict]:
    data = spec.get("demoData")
    if not data:
        return []
    rows = []
    if data.get("label") != DEMO_LABEL:
        rows.append({
            "code": "DEMO_LABEL",
            "message": f"Nhãn dữ liệu minh họa phải đúng chuỗi {DEMO_LABEL}.",
        })
    if data.get("caption") not in (None, DEMO_CAPTION):
        rows.append({
            "code": "DEMO_LABEL",
            "message": f"Chú thích nhỏ phải đúng chuỗi {DEMO_CAPTION}.",
        })
    if data.get("source") not in ("mockup", "product_screenshot"):
        rows.append({
            "code": "DEMO_SOURCE",
            "message": "demoData.source phải là mockup hoặc product_screenshot.",
        })
    if data.get("source") == "mockup" and data.get("presentedAs") == "product_screenshot":
        rows.append({
            "code": "DEMO_SOURCE",
            "message": "Mockup không được ghi là ảnh chụp sản phẩm.",
        })
    series = data.get("orderSeries") or []
    if not series or int(series[-1].get("orders") or -1) != int(data.get("orders") or -2):
        rows.append({
            "code": "DEMO_DATA_CONFLICT",
            "message": "Cột cuối của biểu đồ đơn phải bằng đúng số đơn đã khai.",
        })
    for key in ("open", "close", "revenueVndPerDay", "orders", "inventoryOnShelf"):
        if data.get(key) in (None, ""):
            rows.append({
                "code": "DEMO_DATA_CONFLICT",
                "message": f"demoData thiếu {key}.",
            })
    rows.extend(_product_issues(data))
    for item in spec.get("shelfCategories") or []:
        if item.get("label") not in SHELF_LABELS:
            rows.append({
                "code": "DEMO_LABEL",
                "message": f"Nhãn kệ không nằm trong bộ danh mục: {item.get('label')}.",
            })
    return rows


def _product_issues(data: dict) -> list[dict]:
    products = data.get("products")
    if not products:
        return []
    rows = []
    names = []
    total = 0
    for product in products:
        names.append(product.get("name"))
        total += int(product.get("stock") or 0)
        if product.get("status") == "mockup" and product.get("badge") not in MOCKUP_BADGES:
            rows.append({
                "code": "DEMO_SOURCE",
                "message": f"{product.get('assetId')} là mockup nhưng thiếu nhãn MOCKUP hoặc DEMO.",
            })
        if any(product.get(field) not in (None, "") for field in FORBIDDEN_PRODUCT_FIELDS):
            rows.append({
                "code": "DEMO_DATA_CONFLICT",
                "message": f"{product.get('assetId')} không được chứa liều dùng hoặc chỉ định chưa xác minh.",
            })
    if len(names) != len(set(names)):
        rows.append({
            "code": "DEMO_DATA_CONFLICT",
            "message": "Tên sản phẩm minh họa bị lặp.",
        })
    if total != int(data.get("inventoryOnShelf") or -1):
        rows.append({
            "code": "DEMO_DATA_CONFLICT",
            "message": "Tổng tồn từng sản phẩm phải bằng tồn trên kệ đã khai.",
        })
    return rows


def render_demo_board(
    dest: Path,
    data: dict,
    font_regular: str,
    font_bold: str,
    size: tuple[int, int] = (1280, 720),
    ink: tuple[int, int, int] = (0, 32, 96),
    accent: tuple[int, int, int] = (0, 144, 224),
    paper: tuple[int, int, int] = (255, 250, 244),
    mint: tuple[int, int, int] = (168, 196, 176),
) -> dict:
    """Draw the clock, KPI cards, and the order chart. Returns the exact strings painted."""
    lines = _lines(data)
    image = Image.new("RGB", size, paper)
    draw = ImageDraw.Draw(image)
    width, height = size
    draw.rectangle((0, 0, int(width * 0.012), height), fill=accent)
    title = ImageFont.truetype(font_bold, int(height * 0.055))
    body = ImageFont.truetype(font_regular, int(height * 0.028))
    small = ImageFont.truetype(font_regular, int(height * 0.024))
    value = ImageFont.truetype(font_bold, int(height * 0.046))
    draw.text((int(width * 0.04), int(height * 0.04)), lines["label"], font=title, fill=ink)
    draw.text((int(width * 0.04), int(height * 0.12)), lines["sourceNote"], font=body, fill=ink)

    amount, unit = lines["revenue"].rsplit(" ", 1)
    cards = [
        ("Mở cửa", lines["open"], "", True),
        ("Đóng cửa", lines["close"], "", False),
        ("Doanh thu", amount, unit, False),
        ("Đơn hàng", lines["orders"], "", False),
        ("Tồn trên kệ", lines["inventory"], "", False),
    ]
    left = int(width * 0.04)
    gap = int(width * 0.012)
    top = int(height * 0.20)
    card_h = int(height * 0.20)
    card_w = (width - left - int(width * 0.03) - gap * (len(cards) - 1)) // len(cards)
    for index, (label, text, extra, clock) in enumerate(cards):
        x = left + index * (card_w + gap)
        draw.rounded_rectangle((x, top, x + card_w, top + card_h), radius=16, fill=(255, 255, 255), outline=mint, width=2)
        draw.text((x + 16, top + 12), label, font=small, fill=ink)
        draw.text((x + 16, top + int(card_h * 0.38)), text, font=value, fill=ink)
        if extra:
            draw.text((x + 16, top + int(card_h * 0.72)), extra, font=small, fill=ink)
        if clock:
            _clock_0700(draw, x + card_w - 58, top + 12, 34, accent, ink)
    if lines["products"]:
        draw.text((left, int(height * 0.415)), "Tồn " + " · ".join(lines["products"]), font=small, fill=ink)
    _chart(draw, data, lines, left, int(height * 0.46), width - int(width * 0.03), height - int(height * 0.06), ink, accent, mint, small, body)
    dest.parent.mkdir(parents=True, exist_ok=True)
    image.save(dest)
    return lines


def _lines(data: dict) -> dict:
    share = sunday_share_percent(data)
    note = (
        "Mockup giao diện, không phải ảnh chụp sản phẩm."
        if data.get("source") == "mockup"
        else "Ảnh giao diện sản phẩm."
    )
    return {
        "label": str(data["label"]),
        "sourceNote": note,
        "open": str(data["open"]),
        "close": str(data["close"]),
        "revenue": format_vnd_per_day(int(data["revenueVndPerDay"])),
        "orders": str(int(data["orders"])),
        "inventory": str(int(data["inventoryOnShelf"])),
        "todayShare": f"{share}%",
        "series": [str(int(item["orders"])) for item in data["orderSeries"]],
        "products": [f"{item['name']} {int(item['stock'])}" for item in data.get("products") or []],
    }


def _clock_0700(draw: ImageDraw.ImageDraw, x: int, y: int, radius: int, accent: tuple[int, int, int], ink: tuple[int, int, int]) -> None:
    """Hands sit at 07:00. The card text is the number that counts."""
    draw.ellipse((x, y, x + radius, y + radius), outline=accent, width=2)
    cx = x + radius / 2
    cy = y + radius / 2
    draw.line((cx, cy, cx, y + 5), fill=ink, width=2)
    draw.line((cx, cy, cx - radius * 0.16, cy + radius * 0.28), fill=accent, width=2)


def _chart(
    draw: ImageDraw.ImageDraw,
    data: dict,
    lines: dict,
    left: int,
    top: int,
    right: int,
    bottom: int,
    ink: tuple[int, int, int],
    accent: tuple[int, int, int],
    mint: tuple[int, int, int],
    small: ImageFont.FreeTypeFont,
    body: ImageFont.FreeTypeFont,
) -> None:
    series = data["orderSeries"]
    draw.text((left, top), "Đơn theo ngày", font=body, fill=ink)
    plot_top = top + 36
    plot_bottom = bottom - 52
    span = max(max(int(item["orders"]) for item in series), 1)
    slot = (right - left) / len(series)
    points = []
    for index, item in enumerate(series):
        orders = int(item["orders"])
        bar_h = int((plot_bottom - plot_top - 28) * orders / span)
        x = left + index * slot + slot * 0.28
        w = slot * 0.44
        y = plot_bottom - bar_h
        last = index == len(series) - 1
        draw.rounded_rectangle((x, y, x + w, plot_bottom), radius=6, fill=accent if last else mint)
        number = str(orders)
        box = draw.textbbox((0, 0), number, font=small)
        draw.text((x + (w - (box[2] - box[0])) / 2, y - 24), number, font=small, fill=ink)
        day = str(item["day"])
        day_box = draw.textbbox((0, 0), day, font=small)
        draw.text((x + (w - (day_box[2] - day_box[0])) / 2, plot_bottom + 6), day, font=small, fill=ink)
        points.append((x + w / 2, y))
    if len(points) > 1:
        draw.line(points, fill=ink, width=3)
        for px, py in points:
            draw.ellipse((px - 4, py - 4, px + 4, py + 4), fill=ink)
    caption = f"CN = {lines['todayShare']} số đơn trong tuần minh họa. Cột CN bằng số đơn đã khai."
    draw.text((left, bottom - 4), caption, font=small, fill=ink)

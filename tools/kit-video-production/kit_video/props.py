"""Controlled shelf labels, package faces, and a POS panel. Text is drawn here, then warped onto the plate."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from kit_video.demo_board import DEMO_CAPTION, format_vnd_per_day


def sample_strings(spec: dict) -> dict:
    data = spec["demoData"]
    return {
        "label": data["label"],
        "caption": data.get("caption") or DEMO_CAPTION,
        "open": data["open"],
        "close": data["close"],
        "revenue": format_vnd_per_day(int(data["revenueVndPerDay"])),
        "orders": str(int(data["orders"])),
        "inventory": str(int(data["inventoryOnShelf"])),
        "shelves": [item["label"] for item in spec.get("shelfCategories") or []],
        "products": [
            {
                "name": item["name"],
                "badge": item["badge"],
                "stock": str(int(item["stock"])),
                "expiry": item["expiry"],
            }
            for item in data.get("products") or []
        ],
    }


def render_label_library(dest_dir: Path, spec: dict, font_regular: str, font_bold: str) -> dict:
    dest_dir.mkdir(parents=True, exist_ok=True)
    written = []
    categories = {item["id"]: item for item in spec.get("shelfCategories") or []}
    for item in spec.get("shelfCategories") or []:
        path = dest_dir / f"shelf-{item['id']}.png"
        shelf_sign(item["label"], font_regular, (1100, 64)).save(path)
        written.append(str(path))
    for product in spec["demoData"].get("products") or []:
        path = dest_dir / f"{product['assetId']}.png"
        package_face(product, categories, font_regular, (480, 320)).save(path)
        written.append(str(path))
    kpi = dest_dir / "kpi-card.png"
    kpi_card(spec, font_regular, font_bold, (640, 280)).save(kpi)
    written.append(str(kpi))
    inventory = dest_dir / "inventory-list.png"
    inventory_sheet(spec, font_regular, font_bold).save(inventory)
    written.append(str(inventory))
    return {"files": written, "strings": sample_strings(spec)}


def render_sample(
    plate: Path,
    dest: Path,
    spec: dict,
    font_regular: str,
    font_bold: str,
    phone: Path | None = None,
    phone_width: int = 390,
) -> dict:
    base = Image.open(plate).convert("RGBA")
    categories = {item["id"]: item for item in spec.get("shelfCategories") or []}
    products = {item["assetId"]: item for item in spec["demoData"].get("products") or []}
    placements = spec.get("sampleScene", {}).get("placements") or []
    order = {"shelf": 0, "package": 1, "kpi": 2}
    for place in sorted(placements, key=lambda item: order.get(item["kind"], 9)):
        kind = place["kind"]
        if kind == "shelf":
            _shadow(base, place["quad"], 4)
            sprite = shelf_sign(categories[place["categoryId"]]["label"], font_regular, _quad_size(place["quad"], 3))
            _paste(base, sprite, place["quad"])
        elif kind == "package":
            _paste_package(base, products[place["assetId"]], categories, place, font_regular)
        elif kind == "kpi":
            left, top, right, bottom = place["box"]
            _paste_flat(base, kpi_card(spec, font_regular, font_bold, (right - left, bottom - top)), place["box"])
    dest.parent.mkdir(parents=True, exist_ok=True)
    rgb = base.convert("RGB")
    rgb.save(dest, quality=95)
    if phone is not None:
        height = round(phone_width * rgb.height / rgb.width)
        rgb.resize((phone_width, height), Image.Resampling.LANCZOS).save(phone, quality=95)
    return sample_strings(spec)


def shelf_sign(label: str, font_path: str, size: tuple[int, int]) -> Image.Image:
    """A thin fascia. One weight, one line, padding on both sides. No stretch."""
    width, height = size
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rectangle((0, 0, width - 1, height - 1), fill=(22, 140, 136, 255))
    draw.rectangle((0, 0, width - 1, max(1, height // 10)), fill=(191, 231, 216, 255))
    pad = max(8, width // 24)
    font = _fit_font(draw, label, font_path, width - pad * 2, int(height * 0.62))
    box = draw.textbbox((0, 0), label, font=font)
    text_w = box[2] - box[0]
    text_h = box[3] - box[1]
    draw.text(((width - text_w) / 2, (height - text_h) / 2 - box[1]), label, font=font, fill=(247, 249, 247, 255))
    return image


def package_face(product: dict, categories: dict, font_path: str, size: tuple[int, int]) -> Image.Image:
    """Name, category line, color band, and a small mockup mark. Two sizes, one weight."""
    width, height = size
    color = tuple(product["color"])
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((0, 0, width - 1, height - 1), radius=max(8, width // 28), fill=(247, 249, 247, 255))
    band = max(10, width // 18)
    draw.rectangle((0, 0, band, height - 1), fill=color + (255,))
    pad = max(10, width // 16)
    left = band + pad
    category = categories.get(product["group"], {}).get("short") or ""
    category_font = _fit_font(draw, category, font_path, width - left - pad, int(height * 0.16))
    name_font = _fit_font(draw, product["name"], font_path, width - left - pad, int(height * 0.30))
    draw.text((left, int(height * 0.22)), category, font=category_font, fill=color + (255,))
    name_box = draw.textbbox((0, 0), product["name"], font=name_font)
    draw.text((left, int(height * 0.46) - name_box[1]), product["name"], font=name_font, fill=(0, 32, 96, 255))
    return image


def kpi_card(spec: dict, font_regular: str, font_bold: str, size: tuple[int, int]) -> Image.Image:
    """Four facts only: the demo label, revenue, hours, and orders."""
    strings = sample_strings(spec)
    width, height = size
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((0, 0, width - 1, height - 1), radius=18, fill=(247, 249, 247, 245))
    draw.rectangle((0, 0, 10, height - 1), fill=(22, 140, 136, 255))
    pad = max(16, width // 22)
    label = ImageFont.truetype(font_regular, max(14, int(height * 0.16)))
    number = ImageFont.truetype(font_bold, max(22, int(height * 0.34)))
    unit = ImageFont.truetype(font_regular, max(12, int(height * 0.13)))
    draw.text((pad, int(height * 0.08)), strings["label"], font=label, fill=(22, 140, 136, 255))
    amount, money = strings["revenue"].rsplit(" ", 1)
    draw.text((pad, int(height * 0.28)), amount, font=number, fill=(0, 32, 96, 255))
    draw.text((pad, int(height * 0.64)), money, font=unit, fill=(0, 32, 96, 255))
    draw.text((pad, int(height * 0.80)), f"{strings['open']}–{strings['close']}", font=label, fill=(0, 32, 96, 255))
    orders = f"{strings['orders']} đơn"
    box = draw.textbbox((0, 0), orders, font=label)
    draw.text((width - pad - (box[2] - box[0]), int(height * 0.80)), orders, font=label, fill=(0, 32, 96, 255))
    return image


def inventory_sheet(spec: dict, font_regular: str, font_bold: str) -> Image.Image:
    return render_inventory_board(spec, font_regular, font_bold, (1280, 720))


def operations_cards(spec: dict) -> list[dict]:
    strings = sample_strings(spec)
    amount, unit = strings["revenue"].rsplit(" ", 1)
    return [
        {"label": "Giờ mở cửa", "value": strings["open"], "unit": "giờ"},
        {"label": "Giờ đóng cửa", "value": strings["close"], "unit": "giờ"},
        {"label": "Doanh thu", "value": amount, "unit": unit},
        {"label": "Đơn hàng", "value": strings["orders"], "unit": "đơn"},
        {"label": "Tồn kho", "value": strings["inventory"], "unit": "mặt hàng"},
    ]


def render_operations_board(spec: dict, font_regular: str, font_bold: str, size: tuple[int, int] = (1280, 720)) -> Image.Image:
    """Five matching cards. The disclosure line is a footnote, not the title."""
    width, height = size
    image = Image.new("RGB", size, (247, 249, 247))
    draw = ImageDraw.Draw(image)
    title = ImageFont.truetype(font_bold, 36)
    draw.text((56, 44), "Vận hành", font=title, fill=(0, 32, 96))
    cards = operations_cards(spec)
    gap = 16
    left = 56
    top = 128
    card_h = 360
    widths = [196, 196, 320, 196, 196]
    x = left
    for card, card_w in zip(cards, widths):
        _metric_card(draw, (x, top, x + card_w, top + card_h), card, font_regular, font_bold)
        x += card_w + gap
    _footnote(draw, spec, font_regular, width, height)
    return image


def render_inventory_board(spec: dict, font_regular: str, font_bold: str, size: tuple[int, int] = (1280, 720)) -> Image.Image:
    """A column table. Stock lines are not run together into one sentence."""
    strings = sample_strings(spec)
    width, height = size
    image = Image.new("RGB", size, (247, 249, 247))
    draw = ImageDraw.Draw(image)
    title = ImageFont.truetype(font_bold, 36)
    sub = ImageFont.truetype(font_regular, 22)
    draw.text((80, 72), "Tồn kho", font=title, fill=(0, 32, 96))
    draw.text((80, 120), "Hàng tồn kho cũng là vốn.", font=sub, fill=(22, 140, 136))
    columns = (560, 240, 320)
    headers = ("Sản phẩm", "Số lượng", "Hạn sử dụng")
    rows = [(item["name"], item["stock"], item["expiry"]) for item in strings["products"]]
    header_h = 72
    row_h = 88
    table_top = 188
    table = (80, table_top, 80 + sum(columns), table_top + header_h + row_h * len(rows))
    _table(draw, table, columns, headers, rows, font_regular, font_bold, header_h, row_h)
    total = ImageFont.truetype(font_regular, 22)
    draw.text((64, table[3] + 24), f"Tổng {strings['inventory']} mặt hàng", font=total, fill=(0, 32, 96))
    _footnote(draw, spec, font_regular, width, height)
    return image


def render_transition_board(frames: list[tuple[str, Path]], dest: Path, font_regular: str, font_bold: str) -> None:
    """Still storyboard of the edit order. This is not a moving video."""
    image = Image.new("RGB", (1280, 720), (247, 249, 247))
    draw = ImageDraw.Draw(image)
    title = ImageFont.truetype(font_bold, 28)
    body = ImageFont.truetype(font_regular, 18)
    draw.text((48, 28), "Thứ tự kể chuyện", font=title, fill=(0, 32, 96))
    draw.text((48, 68), "Dàn cảnh tĩnh. Chưa phải video chuyển động.", font=body, fill=(22, 140, 136))
    cell_w = 360
    gap = 28
    origin = (1280 - (cell_w * len(frames) + gap * (len(frames) - 1))) // 2
    for index, (label, path) in enumerate(frames):
        x = origin + index * (cell_w + gap)
        frame = Image.open(path).convert("RGB")
        frame.thumbnail((cell_w, 202), Image.Resampling.LANCZOS)
        image.paste(frame, (x, 150))
        draw.text((x, 150 + frame.height + 16), f"{index + 1}. {label}", font=title, fill=(0, 32, 96))
        if index < len(frames) - 1:
            draw.text((x + frame.width + 6, 230), "→", font=title, fill=(22, 140, 136))
    dest.parent.mkdir(parents=True, exist_ok=True)
    image.save(dest, quality=95)


def render_contact_sheet(frames: list[tuple[str, Path]], dest: Path, font_path: str) -> None:
    columns = 2 if len(frames) > 3 else len(frames)
    rows = (len(frames) + columns - 1) // columns
    sheet = Image.new("RGB", (1280, 720 if rows > 1 else 520), (247, 249, 247))
    draw = ImageDraw.Draw(sheet)
    label_font = ImageFont.truetype(font_path, 18)
    cell_w = 560 if columns == 2 else 380
    gap = 24
    for index, (label, path) in enumerate(frames):
        column = index % columns
        row = index // columns
        row_width = cell_w * columns + gap * (columns - 1)
        x = (1280 - row_width) // 2 + column * (cell_w + gap)
        y = 36 + row * 340
        frame = Image.open(path).convert("RGB")
        frame.thumbnail((cell_w, 300), Image.Resampling.LANCZOS)
        sheet.paste(frame, (x, y))
        draw.text((x, y + frame.height + 8), label, font=label_font, fill=(0, 32, 96))
    dest.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(dest, quality=95)


def _metric_card(draw, box, card, font_regular: str, font_bold: str) -> None:
    left, top, right, bottom = box
    draw.rounded_rectangle(box, radius=16, fill=(255, 255, 255), outline=(220, 235, 250), width=2)
    pad = 20
    label = ImageFont.truetype(font_regular, 18)
    value = _fit_font(draw, card["value"], font_bold, (right - left) - pad * 2, 44)
    unit = ImageFont.truetype(font_regular, 18)
    draw.text((left + pad, top + 36), card["label"], font=label, fill=(22, 140, 136))
    draw.text((left + pad, top + 108), card["value"], font=value, fill=(0, 32, 96))
    draw.text((left + pad, top + 196), card["unit"], font=unit, fill=(0, 32, 96))


def _table(draw, box, columns, headers, rows, font_regular: str, font_bold: str, header_h: int = 64, row_h: int = 72) -> None:
    left, top, right, bottom = box
    ink = (0, 32, 96)
    line = (214, 224, 222)
    header_font = ImageFont.truetype(font_bold, 20)
    body = ImageFont.truetype(font_regular, 24)
    draw.rectangle((left, top, right, top + header_h), fill=(220, 235, 250))
    x = left
    for index, header in enumerate(headers):
        draw.text((x + 24, top + 24), header, font=header_font, fill=ink)
        x += columns[index]
    y = top + header_h
    for row_index, row in enumerate(rows):
        if row_index % 2 == 1:
            draw.rectangle((left + 1, y + 1, right - 1, y + row_h - 1), fill=(244, 248, 247))
        draw.line((left, y, right, y), fill=line, width=1)
        x = left
        for index, cell in enumerate(row):
            if index == 1:
                cell_box = draw.textbbox((0, 0), cell, font=body)
                draw.text((x + columns[index] - 28 - (cell_box[2] - cell_box[0]), y + 28), cell, font=body, fill=ink)
            else:
                draw.text((x + 24, y + 28), cell, font=body, fill=ink)
            if index:
                draw.line((x, y, x, y + row_h), fill=line, width=1)
            x += columns[index]
        y += row_h
    draw.rectangle((left, top, right, bottom), outline=line, width=1)


def _footnote(draw, spec: dict, font_regular: str, width: int, height: int) -> None:
    caption = sample_strings(spec)["caption"]
    font = ImageFont.truetype(font_regular, 16)
    box = draw.textbbox((0, 0), caption, font=font)
    draw.text((width - 64 - (box[2] - box[0]), height - 40), caption, font=font, fill=(22, 140, 136))


def _paste_package(base: Image.Image, product: dict, categories: dict, place: dict, font_path: str) -> None:
    color = tuple(product["color"])
    template = place.get("template") or product["template"]
    if template == "bottle":
        _shadow(base, place["quad"], 8)
        _paste(base, _bottle(product, categories, font_path, _quad_size(place["quad"], 3)), place["quad"])
        return
    _shadow(base, place["front"], 8)
    _paste(base, _panel(color, _quad_size(place["side"], 2), _shade(color, -36)), place["side"])
    _paste(base, _panel(_shade(color, 36), _quad_size(place["top"], 2)), place["top"])
    _paste(base, package_face(product, categories, font_path, _quad_size(place["front"], 3)), place["front"])


def _bottle(product: dict, categories: dict, font_path: str, size: tuple[int, int]) -> Image.Image:
    color = tuple(product["color"])
    width, height = size
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    cap_w = int(width * 0.34)
    cap_x = (width - cap_w) // 2
    draw.rounded_rectangle((cap_x, int(height * 0.02), cap_x + cap_w, int(height * 0.16)), radius=8, fill=_shade(color, -24) + (255,))
    draw.rounded_rectangle((int(width * 0.16), int(height * 0.14), int(width * 0.84), int(height * 0.96)), radius=max(8, width // 8), fill=color + (255,))
    label_box = (int(width * 0.22), int(height * 0.30), int(width * 0.78), int(height * 0.78))
    draw.rounded_rectangle(label_box, radius=6, fill=(247, 249, 247, 255))
    inner_w = label_box[2] - label_box[0] - 8
    name_font = _fit_font(draw, product["name"], font_path, inner_w, int(height * 0.11))
    lines = _wrap(draw, product["name"], name_font, inner_w)
    y = label_box[1] + 6
    for line in lines:
        box = draw.textbbox((0, 0), line, font=name_font)
        draw.text((label_box[0] + 4, y), line, font=name_font, fill=(0, 32, 96, 255))
        y += (box[3] - box[1]) + 2
    return image


def _quad_size(quad: list, scale: int) -> tuple[int, int]:
    xs = [point[0] for point in quad]
    ys = [point[1] for point in quad]
    return max(8, int(max(xs) - min(xs)) * scale), max(8, int(max(ys) - min(ys)) * scale)


def _fit_font(draw: ImageDraw.ImageDraw, text: str, font_path: str, max_width: int, max_size: int) -> ImageFont.FreeTypeFont:
    size = max(8, max_size)
    font = ImageFont.truetype(font_path, size)
    while size > 8 and draw.textbbox((0, 0), text, font=font)[2] > max_width:
        size -= 1
        font = ImageFont.truetype(font_path, size)
    return font


def _paste_flat(base: Image.Image, sprite: Image.Image, box: list) -> None:
    left, top, right, bottom = box
    fitted = sprite.resize((max(1, right - left), max(1, bottom - top)), Image.Resampling.LANCZOS)
    shadow = Image.new("RGBA", base.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((left + 4, top + 8, right + 4, bottom + 8), radius=12, fill=(22, 60, 58, 60))
    base.alpha_composite(shadow)
    base.alpha_composite(fitted, (left, top))


def _panel(color: tuple[int, int, int], size: tuple[int, int], fill: tuple[int, int, int] | None = None) -> Image.Image:
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rectangle((0, 0, size[0] - 1, size[1] - 1), fill=(fill or color) + (255,))
    return image


def _shadow(base: Image.Image, quad: list, depth: int) -> None:
    xs = [point[0] for point in quad]
    ys = [point[1] for point in quad]
    overlay = Image.new("RGBA", base.size, (0, 0, 0, 0))
    ImageDraw.Draw(overlay).rectangle((min(xs), max(ys), max(xs), max(ys) + depth), fill=(22, 60, 58, 70))
    base.alpha_composite(overlay)


def _paste(base: Image.Image, sprite: Image.Image, quad: list) -> None:
    source = [(0, 0), (sprite.width, 0), (sprite.width, sprite.height), (0, sprite.height)]
    coeffs = _perspective(source, [tuple(point) for point in quad])
    warped = sprite.transform(base.size, Image.PERSPECTIVE, coeffs, Image.Resampling.BICUBIC, fillcolor=(0, 0, 0, 0))
    base.alpha_composite(warped)


def _perspective(src: list[tuple[float, float]], dst: list[tuple[float, float]]) -> list[float]:
    matrix = []
    target = []
    for (x, y), (u, v) in zip(dst, src):
        matrix.append([x, y, 1, 0, 0, 0, -u * x, -u * y])
        matrix.append([0, 0, 0, x, y, 1, -v * x, -v * y])
        target.extend((u, v))
    return _solve(matrix, target)


def _solve(matrix: list[list[float]], target: list[float]) -> list[float]:
    rows = [row[:] + [target[index]] for index, row in enumerate(matrix)]
    size = len(target)
    for col in range(size):
        pivot = max(range(col, size), key=lambda row: abs(rows[row][col]))
        rows[col], rows[pivot] = rows[pivot], rows[col]
        scale = rows[col][col]
        for index in range(col, size + 1):
            rows[col][index] /= scale
        for row in range(size):
            if row == col:
                continue
            factor = rows[row][col]
            for index in range(col, size + 1):
                rows[row][index] -= factor * rows[col][index]
    return [rows[index][size] for index in range(size)]


def _shade(color: tuple[int, int, int], delta: int) -> tuple[int, int, int]:
    return tuple(max(0, min(255, channel + delta)) for channel in color)


def _wrap(draw: ImageDraw.ImageDraw, text: str, font: ImageFont.FreeTypeFont, max_width: int) -> list[str]:
    if draw.textbbox((0, 0), text, font=font)[2] <= max_width:
        return [text]
    words = text.split()
    best = [text]
    best_gap = 10**9
    for cut in range(1, len(words)):
        lines = [" ".join(words[:cut]), " ".join(words[cut:])]
        widths = [draw.textbbox((0, 0), line, font=font)[2] for line in lines]
        if max(widths) <= max_width and abs(widths[0] - widths[1]) < best_gap:
            best = lines
            best_gap = abs(widths[0] - widths[1])
    return best


def _center_lines(draw, lines, font, size, fill, top: int | None = None) -> None:
    heights = []
    for line in lines:
        box = draw.textbbox((0, 0), line, font=font)
        heights.append(box[3] - box[1])
    gap = 8
    block = sum(heights) + gap * (len(lines) - 1)
    y = (size[1] - block) / 2 if top is None else top
    for line, height in zip(lines, heights):
        box = draw.textbbox((0, 0), line, font=font)
        draw.text(((size[0] - (box[2] - box[0])) / 2, y), line, font=font, fill=fill)
        y += height + gap

"""Checks that the NOVIXA visual standard points at real files and one data source. It does not approve art or send video jobs."""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from kit_video.demo_board import demo_issues
from kit_video.props import inventory_sheet, operations_cards, render_operations_board, shelf_sign


def may_submit(approval: str, credential: bool) -> bool:
    return approval == "APPROVED" and bool(credential)


def write_character_contact(project_dir: Path, dest: Path, font_path: str) -> None:
    """Sheet of angles that exist. Missing turns stay as text, not invented pictures."""
    sheet = Image.new("RGB", (1280, 720), (247, 249, 247))
    draw = ImageDraw.Draw(sheet)
    title = ImageFont.truetype(font_path, 28)
    label = ImageFont.truetype(font_path, 20)
    draw.text((40, 28), "Nhân vật NOVIXA — chỉ các góc đang có file", font=title, fill=(0, 32, 96))
    cells = [
        ("Góc đã có: gần chính diện", project_dir / "assets/pharmacist-nova-cartoon-v3.jpg"),
        ("Cùng góc, đứng trong quầy", project_dir / "keyframes/v3/s01-pharmacy.jpg"),
        ("3/4 trái, 3/4 phải và nghiêng: chưa có ảnh", None),
    ]
    for index, (caption, path) in enumerate(cells):
        left = 40 + index * 410
        box = (left, 88, left + 380, 560)
        draw.rounded_rectangle(box, radius=16, fill=(255, 255, 255), outline=(191, 231, 216), width=2)
        if path is not None and path.is_file():
            picture = Image.open(path).convert("RGB")
            scale = max(348 / picture.width, 430 / picture.height)
            resized = picture.resize((round(picture.width * scale), round(picture.height * scale)))
            crop_x = (resized.width - 348) // 2
            crop_y = (resized.height - 430) // 2
            sheet.paste(resized.crop((crop_x, crop_y, crop_x + 348, crop_y + 430)), (left + 16, 104))
        else:
            draw.text((left + 24, 280), "Chưa kiểm chứng", font=label, fill=(22, 140, 136))
        draw.text((left, 590), caption, font=label, fill=(0, 32, 96))
    dest.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(dest, quality=95)


def write_consistency_probe_sheet(probe_dir: Path, font_path: str) -> None:
    """Contact sheet for the character probe. Blocked slots stay text. No mirror, rotate, or stretch."""
    probe = json.loads((probe_dir / "probe.json").read_text(encoding="utf-8"))
    sheet = Image.new("RGB", (1280, 720), (247, 249, 247))
    draw = ImageDraw.Draw(sheet)
    title = ImageFont.truetype(font_path, 26)
    label = ImageFont.truetype(font_path, 18)
    small = ImageFont.truetype(font_path, 16)
    draw.text((16, 14), "Probe nhận diện — một ảnh có file, bảy mục bị chặn", font=title, fill=(0, 32, 96))
    items = probe["items"]
    cell_w, cell_h, gap = 304, 292, 10
    for index, item in enumerate(items):
        column, row = index % 4, index // 4
        left = 16 + column * (cell_w + gap)
        top = 56 + row * (cell_h + gap)
        draw.rounded_rectangle((left, top, left + cell_w, top + cell_h), radius=12, fill=(255, 255, 255), outline=(191, 231, 216), width=2)
        draw.text((left + 12, top + 10), item["title"], font=label, fill=(0, 32, 96))
        if item["status"] == "ASSET_EXISTS" and item.get("path"):
            picture = Image.open(probe_dir / item["path"]).convert("RGB")
            fitted = picture.copy()
            fitted.thumbnail((cell_w - 24, cell_h - 78))
            sheet.paste(fitted, (left + (cell_w - fitted.width) // 2, top + 44))
            draw.text((left + 12, top + cell_h - 28), "ASSET_EXISTS · chờ xem", font=small, fill=(22, 140, 136))
        else:
            draw.text((left + 12, top + 120), "BLOCKED", font=title, fill=(22, 140, 136))
            draw.text((left + 12, top + cell_h - 28), "Không tạo bằng lật hoặc kéo ảnh", font=small, fill=(0, 32, 96))
    dest = probe_dir / "contact-sheet.jpg"
    sheet.save(dest, quality=95)


def check_character_probe(project_dir: Path) -> list[dict]:
    probe_dir = project_dir / "probes" / "character-consistency-v1"
    probe_path = probe_dir / "probe.json"
    if not probe_path.is_file():
        return [{"code": "PROBE_MISSING", "message": "Thiếu probe nhận diện."}]
    probe = json.loads(probe_path.read_text(encoding="utf-8"))
    rows = []
    if probe.get("approval") == "APPROVED":
        rows.append({"code": "APPROVAL_CHANGED", "message": "Probe không được tự đánh dấu APPROVED."})
    items = probe.get("items") or []
    existing = [item for item in items if item.get("status") == "ASSET_EXISTS"]
    if len(existing) != 1:
        rows.append({"code": "PROBE_COUNT", "message": "Probe chỉ được có một ảnh thật."})
    for item in items:
        if item.get("approval") == "APPROVED":
            rows.append({"code": "APPROVAL_CHANGED", "message": f"{item.get('id')} bị đánh dấu APPROVED."})
        if item.get("method") in {"flip", "mirror", "rotate", "stretch"}:
            rows.append({"code": "PROBE_FAKE", "message": f"{item.get('id')} dùng phép biến hình ảnh cũ."})
        path = item.get("path")
        if item.get("status") == "BLOCKED" and path:
            rows.append({"code": "PROBE_FAKE", "message": f"{item.get('id')} đang bị chặn nhưng có file."})
        if item.get("status") == "ASSET_EXISTS":
            copy = probe_dir / str(path)
            source = project_dir / str(item.get("source"))
            if not copy.is_file() or not source.is_file() or copy.read_bytes() != source.read_bytes():
                rows.append({"code": "PROBE_SOURCE", "message": "Ảnh đối chiếu không còn trùng file nguồn."})
            if item.get("review") != "VISUAL_REVIEW_REQUIRED":
                rows.append({"code": "PROBE_REVIEW", "message": "Ảnh có file vẫn phải chờ người xem."})
    if not (probe_dir / "contact-sheet.jpg").is_file():
        rows.append({"code": "PROBE_SHEET", "message": "Thiếu contact sheet của probe."})
    return rows


def check_visual_standard(project_dir: Path, repo_root: Path | None = None) -> list[dict]:
    standard_path = project_dir / "visual-standard-v1.json"
    commercial_path = project_dir / "commercial.json"
    rows = []
    if not standard_path.is_file() or not commercial_path.is_file():
        return [{"code": "STANDARD_MISSING", "message": "Thiếu visual-standard-v1.json hoặc commercial.json."}]
    standard = json.loads(standard_path.read_text(encoding="utf-8"))
    commercial = json.loads(commercial_path.read_text(encoding="utf-8"))
    if standard.get("approval") == "APPROVED":
        rows.append({"code": "APPROVAL_CHANGED", "message": "Bộ chuẩn không được tự đánh dấu APPROVED."})
    rows.extend(demo_issues(commercial))
    rows.extend(_assets(project_dir, repo_root, standard))
    rows.extend(_products(standard, commercial))
    rows.extend(_cards(standard, commercial))
    rows.extend(_preserved(project_dir, standard))
    rows.extend(_probe(project_dir, standard))
    rows.extend(_scenes(commercial))
    return rows


def graphics_overflow(project_dir: Path) -> list[str]:
    """Return labels whose drawn box leaves the canvas. Empty means the controlled layer did not clip."""
    commercial = json.loads((project_dir / "commercial.json").read_text(encoding="utf-8"))
    fonts = commercial["endCard"]
    regular = fonts["fontRegular"]
    problems = []
    board = render_operations_board(commercial, regular, fonts["fontBold"])
    problems.extend(_outside(board, "Vận hành", regular, 36, (56, 44)))
    problems.extend(_outside(board, commercial["demoData"]["caption"], regular, 16, None))
    table = inventory_sheet(commercial, regular, fonts["fontBold"])
    body = ImageFont.truetype(regular, 24)
    draw = ImageDraw.Draw(table)
    y = 188 + 72 + 28
    for product in commercial["demoData"]["products"]:
        box = draw.textbbox((104, y), product["name"], font=body)
        if box[0] < 80 or box[2] > 80 + 560 or box[3] > table.height:
            problems.append(product["name"])
        y += 88
    sign = shelf_sign("THỰC PHẨM BẢO VỆ SỨC KHỎE", regular, (420, 40))
    content = sign.getbbox()
    if content is None or content[2] > sign.width or content[3] > sign.height:
        problems.append("THỰC PHẨM BẢO VỆ SỨC KHỎE")
    return problems


def _resolve(project_dir: Path, repo_root: Path | None, raw: str) -> Path:
    local = project_dir / raw
    if raw and local.is_file():
        return local
    if raw and repo_root is not None and (repo_root / raw).is_file():
        return repo_root / raw
    return local


def _assets(project_dir: Path, repo_root: Path | None, standard: dict) -> list[dict]:
    rows = []
    seen = set()
    for asset in standard.get("assets") or []:
        asset_id = asset.get("id")
        if asset_id in seen:
            rows.append({"code": "ASSET_ID", "message": f"Mã {asset_id} bị lặp."})
        seen.add(asset_id)
        state = asset.get("state")
        path = _resolve(project_dir, repo_root, str(asset.get("path") or ""))
        if state == "file" and not path.is_file():
            rows.append({"code": "ASSET_MISSING", "message": f"Không thấy file của {asset_id}."})
        if state == "spec-only" and path.is_file() and asset.get("path"):
            rows.append({"code": "ASSET_STATE", "message": f"{asset_id} đang ghi là spec-only nhưng có file."})
        if asset.get("approval") == "APPROVED" and asset.get("role") != "logo":
            rows.append({"code": "APPROVAL_CHANGED", "message": f"{asset_id} bị đánh dấu APPROVED."})
        if asset.get("version") in (None, ""):
            rows.append({"code": "ASSET_VERSION", "message": f"{asset_id} thiếu phiên bản."})
    return rows


def _products(standard: dict, commercial: dict) -> list[dict]:
    demo = commercial["demoData"]["products"]
    names = [item["name"] for item in demo]
    declared = [item.get("name") for item in standard.get("products") or []]
    if declared != names:
        return [{"code": "PRODUCT_SOURCE", "message": "Tên sản phẩm trong bộ chuẩn không khớp demoData."}]
    return []


def _cards(standard: dict, commercial: dict) -> list[dict]:
    expected = [card["label"] for card in operations_cards(commercial)]
    if [card.get("label") for card in standard.get("kpiCards") or []] != expected:
        return [{"code": "KPI_SOURCE", "message": "Nhãn KPI trong bộ chuẩn không khớp nguồn dữ liệu."}]
    return []


def _preserved(project_dir: Path, standard: dict) -> list[dict]:
    rows = []
    for raw in standard.get("preserved") or []:
        if not (project_dir / raw).is_file():
            rows.append({"code": "HISTORY_MISSING", "message": f"Thiếu bản cũ {raw}."})
    return rows


def _probe(project_dir: Path, standard: dict) -> list[dict]:
    probe = standard.get("sceneProbe") or {}
    if probe.get("status") != "SPEC_ONLY":
        return [{"code": "PROBE_STATE", "message": "Cảnh kiểm chứng phải ở SPEC_ONLY khi chưa có video."}]
    video = probe.get("video")
    if video and (project_dir / video).is_file():
        return [{"code": "PROBE_STATE", "message": "Cảnh kiểm chứng đang trỏ tới một file video."}]
    return []


def _scenes(commercial: dict) -> list[dict]:
    rows = []
    for scene in commercial.get("scenes") or []:
        keyframe = scene.get("keyframe")
        if isinstance(keyframe, dict) and keyframe.get("approval") == "APPROVED":
            rows.append({"code": "APPROVAL_CHANGED", "message": f"{scene.get('sceneId')} bị đánh dấu APPROVED."})
    return rows


def _outside(image: Image.Image, text: str, font_path: str, size: int, anchor) -> list[str]:
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype(font_path, size)
    if anchor is None:
        box = draw.textbbox((0, 0), text, font=font)
        width = box[2] - box[0]
        anchor = (image.width - 64 - width, image.height - 40)
    box = draw.textbbox(anchor, text, font=font)
    if box[0] < 0 or box[1] < 0 or box[2] > image.width or box[3] > image.height:
        return [text]
    return []


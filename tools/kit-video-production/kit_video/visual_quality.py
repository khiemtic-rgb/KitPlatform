"""Visual quality contract shared by planning, rendering, and QA.

Technical correctness is not this module. A scene can have the right size and
still be refused when the plan asks for a schematic stand-in or an unlabeled
product mockup. The module does not name a brand or a film.
"""

from __future__ import annotations

import struct
from functools import lru_cache
from pathlib import Path

from kit_video.composition import art_profile, family_for, scene_intent
from kit_video.contract import DesignContract


MOCKUP_KINDS = frozenset({"workspace", "catalog", "dashboard", "lanes", "app_screen"})
SCHEMATIC = frozenset({"lines", "schematic", "placeholder", "box", "wireframe"})


def art_direction(style: dict) -> dict:
    return art_profile(style)


def assess_intent(profile: dict, intent: dict) -> dict:
    """A profile can require one picture idea. Other profiles stay on the older path."""
    if "one-idea" not in (profile.get("principles") or []):
        return {"status": "ok", "code": "", "reason": "Hồ sơ không bắt mỗi cảnh phải có một ý tưởng hình."}
    if not intent.get("idea") or not intent.get("subject") or not intent.get("focal"):
        return {
            "status": "blocked",
            "code": "intent_missing",
            "reason": "Hồ sơ yêu cầu mỗi cảnh một ý tưởng, một chủ thể và một điểm tập trung.",
        }
    if intent["subject"] not in {
        "still", "portrait", "schedule", "goods", "record", "transaction",
        "stock", "relation", "mark", "statement", "lockup",
    }:
        return {
            "status": "blocked",
            "code": "intent_subject",
            "reason": "Chủ thể hình chưa có họ bố cục trong engine.",
        }
    return {"status": "ok", "code": "", "reason": "Cảnh có ý tưởng hình để chọn bố cục."}


def assess_scene(contract: DesignContract, kind: str, params: dict) -> dict:
    """Refuse a plan that cannot be a finished picture. Do not shrink type to pass."""
    defects: list[tuple[str, str]] = []
    imagery = str(params.get("imagery") or params.get("treatment") or "").strip().lower()
    if imagery in SCHEMATIC:
        defects.append((
            "provisional_imagery",
            "Bố cục khai báo hình sơ đồ. Chọn cách diễn đạt khác hoặc từ chối, không vá bằng hình khối chung.",
        ))
    if kind == "catalog":
        for tile in params.get("tiles") or []:
            if isinstance(tile, dict) and str(tile.get("mark") or "").strip().lower() in SCHEMATIC:
                defects.append((
                    "provisional_imagery",
                    "Thẻ hàng dùng ký hiệu sơ đồ làm chủ thể.",
                ))
                break
    if kind in MOCKUP_KINDS:
        disclosure = str(params.get("disclosure") or "").strip()
        data_label = str(params.get("dataLabel") or "").strip()
        if len(disclosure) < 8 and len(data_label) < 8:
            defects.append((
                "mockup_unlabeled",
                "Màn hình minh họa phải mang nhãn. Không để người xem tưởng đó là giao diện chính thức.",
            ))
    if not contract.readable_pair("ink", "paper"):
        defects.append((
            "contrast",
            "Mực trên giấy không đủ tương phản để đọc.",
        ))
    if defects:
        code, reason = defects[0]
        return {
            "status": "blocked",
            "code": code,
            "kind": kind,
            "reason": reason,
            "defects": [item[0] for item in defects],
        }
    return {
        "status": "ok",
        "code": "",
        "kind": kind,
        "reason": "Bố cục đủ điều kiện chất lượng để vẽ.",
        "defects": [],
    }


def assess_film(scenes: list) -> list[dict]:
    """Flag a run of the same board and title. Variety is a film requirement."""
    findings = []
    previous = None
    run = 0
    started = ""
    for scene in scenes:
        if not isinstance(scene, dict) or scene.get("templateId") != "designed_board":
            previous = None
            run = 0
            continue
        params = scene.get("params") if isinstance(scene.get("params"), dict) else {}
        key = (str(params.get("kind") or ""), str(params.get("title") or ""))
        if key == previous and key[0]:
            run += 1
        else:
            previous = key
            run = 1
            started = str(scene.get("sceneId") or "")
        if run == 3:
            findings.append({
                "status": "blocked",
                "code": "visual_repeat",
                "sceneId": str(scene.get("sceneId") or ""),
                "reason": f"Ba cảnh liên tiếp cùng bố cục và cùng tiêu đề, bắt đầu từ {started}.",
            })
    return findings


def assess_composition(scenes: list) -> list[dict]:
    """Refuse three scenes in a row that resolve to the same picture family."""
    findings = []
    previous = None
    run = 0
    started = ""
    for scene in scenes:
        if not isinstance(scene, dict) or scene.get("templateId") != "designed_board":
            previous = None
            run = 0
            continue
        params = scene.get("params") if isinstance(scene.get("params"), dict) else {}
        if not scene_intent(params)["subject"]:
            previous = None
            run = 0
            continue
        family = family_for(params, str(params.get("kind") or ""))
        if family == previous:
            run += 1
        else:
            previous = family
            run = 1
            started = str(scene.get("sceneId") or "")
        if run == 3:
            findings.append({
                "status": "blocked",
                "code": "composition_repeat",
                "sceneId": str(scene.get("sceneId") or ""),
                "reason": f"Ba cảnh liên tiếp cùng họ hình {family}, bắt đầu từ {started}.",
            })
    return findings


def assess_breathing(samples: dict, width: int, height: int, safe: dict, pad: int) -> list[dict]:
    """Flag type that leaves the safe margin, and type that covers other type.

    Sitting inside the safe margin is the required air from the frame edge.
    A few pixels of sprite padding are ignored. This does not score art direction.
    """
    slack = max(0, int(pad))
    left = int(width * float(safe.get("left", 0))) - slack
    top = int(height * float(safe.get("top", 0))) - slack
    right = int(width * (1 - float(safe.get("right", 0)))) + slack
    bottom = int(height * (1 - float(safe.get("bottom", 0)))) + slack
    findings = []
    boxes: list[tuple[str, tuple[int, int, int, int]]] = []
    for text, raw in samples.items():
        if not isinstance(raw, (list, tuple)) or len(raw) != 4:
            continue
        box = tuple(int(value) for value in raw)
        boxes.append((str(text), box))
        x0, y0, x1, y1 = box
        if x0 < left or y0 < top or x1 > right or y1 > bottom:
            findings.append({
                "class": "automatic",
                "code": "spacing_flush",
                "text": str(text),
                "reason": "Chữ đi ra khỏi vùng an toàn của khung. Còn trong khung chưa phải là đủ khoảng thở.",
            })
    for index, (text, box) in enumerate(boxes):
        for other_text, other in boxes[index + 1:]:
            overlap_w = min(box[2], other[2]) - max(box[0], other[0])
            overlap_h = min(box[3], other[3]) - max(box[1], other[1])
            if overlap_w <= 8 or overlap_h <= 8:
                continue
            small = min(
                max(1, (box[2] - box[0]) * (box[3] - box[1])),
                max(1, (other[2] - other[0]) * (other[3] - other[1])),
            )
            if overlap_w * overlap_h > small * 0.35:
                findings.append({
                    "class": "automatic",
                    "code": "type_collision",
                    "text": text,
                    "reason": f"Chữ đè lên '{other_text}'.",
                })
    return findings


def picture_share(boxes: list, width: int, height: int) -> float:
    """Share of the frame given to picture wells. This is size, not taste."""
    area = max(1, int(width) * int(height))
    covered = 0
    for box in boxes:
        if not isinstance(box, (list, tuple)) or len(box) != 4:
            continue
        covered += max(0, int(box[2]) - int(box[0])) * max(0, int(box[3]) - int(box[1]))
    return covered / area


@lru_cache(maxsize=8)
def _unicode_cmap(font_path: str) -> frozenset[int]:
    """Unicode code points with a real glyph. An unreadable font reports none."""
    try:
        data = Path(font_path).read_bytes()
        num = struct.unpack_from(">H", data, 4)[0]
        cmap_off = None
        for index in range(num):
            tag, _checksum, offset, _length = struct.unpack_from(">4sIII", data, 12 + index * 16)
            if tag == b"cmap":
                cmap_off = offset
                break
        if cmap_off is None:
            return frozenset()
        _version, count = struct.unpack_from(">HH", data, cmap_off)
        chosen = None
        score = -1
        for index in range(count):
            platform, encoding, offset = struct.unpack_from(">HHI", data, cmap_off + 4 + index * 8)
            rank = 4 if platform == 3 and encoding == 10 else 3 if platform == 0 else 2 if platform == 3 and encoding == 1 else 0
            if rank > score:
                score = rank
                chosen = cmap_off + offset
        if chosen is None:
            return frozenset()
        fmt = struct.unpack_from(">H", data, chosen)[0]
        found: set[int] = set()
        if fmt == 4:
            _fmt, _length, _language, seg_x2 = struct.unpack_from(">HHHH", data, chosen)
            segments = seg_x2 // 2
            end_at = chosen + 14
            ends = struct.unpack_from(f">{segments}H", data, end_at)
            start_at = end_at + segments * 2 + 2
            starts = struct.unpack_from(f">{segments}H", data, start_at)
            delta_at = start_at + segments * 2
            deltas = struct.unpack_from(f">{segments}h", data, delta_at)
            range_at = delta_at + segments * 2
            offsets = struct.unpack_from(f">{segments}H", data, range_at)
            for index in range(segments):
                start, end = starts[index], ends[index]
                if start == 0xFFFF:
                    continue
                for code in range(start, end + 1):
                    if offsets[index] == 0:
                        glyph = (code + deltas[index]) & 0xFFFF
                    else:
                        pos = range_at + index * 2 + offsets[index] + (code - start) * 2
                        glyph = struct.unpack_from(">H", data, pos)[0]
                        if glyph:
                            glyph = (glyph + deltas[index]) & 0xFFFF
                    if glyph:
                        found.add(code)
        elif fmt == 12:
            _fmt, _reserved, _length, _language, groups = struct.unpack_from(">HHIII", data, chosen)
            pos = chosen + 16
            for _group in range(groups):
                start, end, glyph = struct.unpack_from(">III", data, pos)
                pos += 12
                for code in range(start, end + 1):
                    if glyph + (code - start):
                        found.add(code)
        return frozenset(found)
    except (OSError, struct.error):
        return frozenset()


def missing_glyphs(font_path: str, text: str) -> list[str]:
    """Characters the font cannot draw. No Unicode cmap means none of them are covered."""
    seen: set[str] = set()
    chars: list[str] = []
    for char in text:
        if char in seen or char.isspace() or ord(char) < 32:
            continue
        seen.add(char)
        chars.append(char)
    covered = _unicode_cmap(font_path)
    if not covered:
        return chars
    return [char for char in chars if ord(char) not in covered]


def content_coverage(path: Path, background: tuple[int, int, int], safe: tuple[float, float, float, float]) -> float:
    """Share of the safe frame that is not the page background."""
    from PIL import Image

    image = Image.open(path).convert("RGB")
    width, height = image.size
    left = int(width * safe[3])
    top = int(height * safe[0])
    right = int(width * (1 - safe[1]))
    bottom = int(height * (1 - safe[2]))
    if right <= left or bottom <= top:
        return 0.0
    total = 0
    filled = 0
    step = 4
    for y in range(top, bottom, step):
        for x in range(left, right, step):
            total += 1
            pixel = image.getpixel((x, y))
            if sum(abs(int(pixel[index]) - background[index]) for index in range(3)) > 18:
                filled += 1
    if total == 0:
        return 0.0
    return filled / total

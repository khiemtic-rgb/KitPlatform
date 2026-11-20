"""Check a .hinh.json against spoken lines before it is saved.

Usage:
  python hinh_check.py --script kich-ban.txt --plan draft.json --channel ngv
  python hinh_check.py --smoke

Prints one JSON object: {"ok": true|false, "errors": [...], "scenes": n, "lines": n}
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import tempfile
import unicodedata
from pathlib import Path

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import edu_studio as edu  # noqa: E402

TEMPLATES = {"nv_board", "edu_title", "edu_bignumber", "edu_steps", "edu_contrast", "edu_rules"}
NGV_ART = {"keeper", "keeper_desk", "wrist", "watch", "jar", "cup", "keys"}
NGV_OBJECTS = {
    "note", "coin", "coins", "bill", "wallet", "clock", "lock", "mark", "arrow", "card", "phone",
    "receipt", "book", "scale", "tag", "calendar", "bag", "mag", "bubble", "report",
}
NGV_BANNED = {"person", "customer", "box", "bottle", "blister", "store", "shelf", "screen"}
BANNED_TEXT = ("$", "usd", "btc", "eth", "usdt", "bitcoin", "binance")
NUMBER_KEYS = {"number", "text", "tag", "note"}
SCENE_HEADING = re.compile(r"^(?:cảnh|canh|sc)\s*\d+", re.I)
DIRECTION = re.compile(r"^(chủ đề|định dạng|phong cách|thời lượng|hình ảnh|lời dẫn|ghi chú|nhạc|âm thanh|chú thích|camera|bối cảnh|mục tiêu|tone|style)\b", re.I)
SPOKEN_LABEL = re.compile(r"^(một|hai|ba|bốn|năm|sáu|bảy|tám|chín|mười|thứ\s+\S+|bước\s*\d+|\d+|lưu ý|ví dụ|nhớ|quy tắc(\s*\d+)?|điều\s+\S+|nguồn|kết luận|tóm lại|chú ý|cảnh báo|câu hỏi|hỏi|đáp)$", re.I)
SHORT_MARK = re.compile(r"^(?:[-*]\s*)?\[\s*(?:chỉ\s*)?ngắn\s*\]\s*", re.I)
DIGIT = re.compile(r"\d+(?:[.,]\d+)*")
WORD_NUMBER = re.compile(
    r"(?:không|một|hai|ba|bốn|tư|năm|lăm|sáu|bảy|tám|chín|mười)(?:\s+(?:mươi|mốt|lăm|một|hai|ba|bốn|tư|năm|sáu|bảy|tám|chín|trăm|nghìn|ngàn|triệu|tỷ))*",
    re.I,
)
_ONES = ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"]


def spoken_lines(script: str) -> list[dict]:
    """Same spoken sentences the studio sends, plus whether the source line was [ngắn]."""
    rows = []
    for raw in script.splitlines():
        short = bool(SHORT_MARK.search(raw.strip()))
        text = _spoken(raw)
        if text:
            rows.append({"text": text, "short": short})
    return rows


def _spoken(raw: str) -> str:
    line = SHORT_MARK.sub("", raw.strip()).strip()
    if not line or re.match(r"^#{1,6}\s", line) or re.match(r"^([-*_])\1{2,}$", line.replace(" ", "")) or SCENE_HEADING.match(line) or re.match(r"^\[[^\]]*\]$", line):
        return ""
    plain = line.replace("**", "").strip()
    if not plain or SCENE_HEADING.match(plain) or re.match(r"^([-*_])\1{2,}$", plain):
        return ""
    colon = plain.find(":")
    label = plain[:colon].strip() if colon > 0 else ""
    if label and DIRECTION.match(label):
        return ""
    speaker = colon > 0 and colon <= 40 and label and len(label.split()) <= 3 and not re.search(r"[,.!?…]", label) and not SPOKEN_LABEL.match(label)
    return plain[colon + 1:].strip() if speaker else plain


def _fold(text: str) -> str:
    return unicodedata.normalize("NFC", str(text or "")).casefold()


def _vi(n: int) -> str:
    if n < 0 or n >= 1_000_000_000:
        return str(n)
    if n < 10:
        return _ONES[n]
    if n < 20:
        return "mười" if n == 10 else "mười " + ("lăm" if n == 15 else _ONES[n % 10])
    if n < 100:
        ten, one = divmod(n, 10)
        tail = "" if one == 0 else " " + ("mốt" if one == 1 else "lăm" if one == 5 else _ONES[one])
        return _ONES[ten] + " mươi" + tail
    if n < 1000:
        hundred, rest = divmod(n, 100)
        return _ONES[hundred] + " trăm" + ("" if rest == 0 else " " + _vi(rest))
    if n < 1_000_000:
        thousand, rest = divmod(n, 1000)
        return _vi(thousand) + " nghìn" + ("" if rest == 0 else " " + _vi(rest))
    million, rest = divmod(n, 1_000_000)
    return _vi(million) + " triệu" + ("" if rest == 0 else " " + _vi(rest))


def _present(token: str, speech: str) -> bool:
    folded = _fold(token)
    if folded and folded in speech:
        return True
    digits = folded.replace(".", "").replace(",", "")
    if digits.isdigit():
        if digits in speech.replace(".", "").replace(",", ""):
            return True
        words = _fold(_vi(int(digits)))
        return words in speech
    return False


def _numbers(text: str) -> list[str]:
    found = [m.group(0) for m in DIGIT.finditer(text)]
    for match in WORD_NUMBER.finditer(text):
        word = match.group(0).strip()
        if word and not word.casefold().startswith("không "):
            found.append(word)
    return found


def _strings(node, bag: list[tuple[str, str]] | None = None) -> list[tuple[str, str]]:
    bag = [] if bag is None else bag
    if isinstance(node, dict):
        for key, value in node.items():
            if key in NUMBER_KEYS and isinstance(value, str):
                bag.append((key, value))
            else:
                _strings(value, bag)
    elif isinstance(node, list):
        for item in node:
            _strings(item, bag)
    return bag


def _arts(node, bag: list[str] | None = None) -> list[str]:
    bag = [] if bag is None else bag
    if isinstance(node, dict):
        art = node.get("art")
        if isinstance(art, str) and art.strip():
            bag.append(art.strip())
        for value in node.values():
            _arts(value, bag)
    elif isinstance(node, list):
        for item in node:
            _arts(item, bag)
    return bag


def _icons(node, bag: list[str] | None = None) -> list[str]:
    bag = [] if bag is None else bag
    if isinstance(node, dict):
        for key, value in node.items():
            if key in ("icon", "icons"):
                values = value if isinstance(value, list) else [value]
                bag.extend(str(item) for item in values if str(item or "").strip())
            else:
                _icons(value, bag)
    elif isinstance(node, list):
        for item in node:
            _icons(item, bag)
    return bag


def _props() -> set[str]:
    src = HERE.parents[1] / "kit-video-production" / "kit_video" / "edu_props.py"
    text = src.read_text(encoding="utf-8")
    block = text.split("PROPS = {", 1)[1].split("}", 1)[0]
    return set(re.findall(r'"([a-z0-9_]+)"\s*:', block))


def check(lines: list[dict], plan: dict, channel: str = "") -> list[str]:
    errors: list[str] = []
    scenes = plan.get("scenes") if isinstance(plan, dict) else None
    if not isinstance(scenes, list) or not scenes:
        return ["Kế hoạch không có cảnh."]
    texts = [str(row.get("text") or "") for row in lines]
    shorts = [bool(row.get("short")) for row in lines]
    normal = [edu._norm(text) for text in texts]
    starts: list[int] = []
    cursor = 0
    for number, scene in enumerate(scenes, start=1):
        if not isinstance(scene, dict):
            errors.append(f"Cảnh {number} không phải một cảnh.")
            continue
        key = edu._norm(scene.get("first", ""))
        hit = next((i for i in range(cursor, len(texts)) if key and normal[i].startswith(key)), -1)
        if hit < 0:
            errors.append(f"Cảnh {number} không khớp câu: {str(scene.get('first') or '')[:80]}")
            continue
        if shorts[hit] and hit != 0:
            errors.append(f"Cảnh {number} mở bằng câu [ngắn]. Câu ngắn dùng chung cảnh đứng trước.")
        template = str(scene.get("template") or "").strip()
        if template not in TEMPLATES:
            errors.append(f"Cảnh {number} dùng mẫu không có trong danh mục: {template or '(trống)'}")
        starts.append(hit)
        cursor = hit + 1
    if scenes and (not starts or starts[0] != 0):
        errors.append("Cảnh đầu phải bắt đầu ở câu 1.")
    if len(starts) != len(scenes):
        return errors[:40]
    starts.append(len(texts))
    props = _props()
    for number, scene in enumerate(scenes, start=1):
        first, end = starts[number - 1], starts[number]
        speech = _fold(" ".join(texts[first:end]))
        template = str(scene.get("template") or "")
        if channel == "ngv":
            for art in _arts(scene.get("params")):
                if art in NGV_BANNED or art not in NGV_ART | NGV_OBJECTS:
                    errors.append(f"Cảnh {number} dùng hình không được phép: {art}")
            for icon in _icons(scene.get("params")):
                if icon not in props:
                    errors.append(f"Cảnh {number} dùng icon không có: {icon}")
            folded_scene = json.dumps(scene, ensure_ascii=False).casefold()
            for word in BANNED_TEXT:
                if word in folded_scene:
                    errors.append(f"Cảnh {number} có nội dung bị cấm: {word}")
                    break
        for key, value in _strings(scene.get("params")):
            for token in _numbers(value):
                if not _present(token, speech):
                    errors.append(f"Cảnh {number} ghi số {token!r} trong {key} nhưng câu đọc của cảnh không có số đó.")
    if errors:
        return errors[:40]
    for number, scene in enumerate(scenes, start=1):
        first, end = starts[number - 1], starts[number]
        template = str(scene.get("template") or "")
        if template:
            binding = {
                "sidecar": "draft", "scene": number, "first": first, "count": end - first, "pos": 0,
                "template": template, "params": scene.get("params") or {},
                "palette": plan.get("palette") or edu.DEFAULT_PALETTE, "brand": "", "logo": "", "tagline": "", "footer": "", "background": "",
            }
            for orient in ("wide", "tall"):
                binding["orient"] = orient
                dest = Path(tempfile.gettempdir()) / f"hinh-check-{number}-{orient}.png"
                try:
                    problems = edu.still(binding, str(dest))
                except Exception as error:  # noqa: BLE001
                    errors.append(f"Cảnh {number} ({orient}) không vẽ được: {error}")
                    continue
                finally:
                    dest.unlink(missing_ok=True)
                for problem in problems:
                    reason = str(problem.get("reason") or "")
                    if reason.startswith("Chữ nhỏ nhất") or "Nội dung dài hơn khung" in reason or problem.get("bbox") != [0, 0, 0, 0]:
                        errors.append(f"Cảnh {number} ({orient}): {reason}")
    return errors[:40]


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--script")
    parser.add_argument("--plan")
    parser.add_argument("--channel", default="")
    parser.add_argument("--lines", action="store_true", help="print spoken lines as JSON and exit")
    parser.add_argument("--smoke", action="store_true")
    args = parser.parse_args(argv)
    if args.smoke:
        return _smoke()
    if args.lines:
        json.dump(spoken_lines(Path(args.script).read_text(encoding="utf-8")), sys.stdout, ensure_ascii=False)
        return 0
    lines = spoken_lines(Path(args.script).read_text(encoding="utf-8"))
    plan = json.loads(Path(args.plan).read_text(encoding="utf-8"))
    errors = check(lines, plan, args.channel)
    json.dump({"ok": not errors, "errors": errors, "scenes": len(plan.get("scenes") or []), "lines": len(lines)}, sys.stdout, ensure_ascii=False)
    return 0 if not errors else 1


def _smoke() -> int:
    good_lines = [
        {"text": "Hai trăm triệu để dành, không mang đi.", "short": False},
        {"text": "Giao dịch có rủi ro mất vốn.", "short": True},
        {"text": "Bước sau là giữ nguyên khoản đó.", "short": False},
    ]
    good = {"scenes": [
        {"first": "Hai trăm triệu để dành", "template": "edu_bignumber", "params": {"number": "200 triệu", "label": "Để dành", "icon": "wallet"}},
        {"first": "Bước sau là giữ nguyên", "template": "edu_rules", "params": {"rules": ["Giữ nguyên khoản đó"]}},
    ]}
    bad_first = {"scenes": [{"first": "Câu không có trong kịch bản", "template": "edu_title", "params": {"title": "Sai"}}]}
    bad_art = {"scenes": [{"first": "Hai trăm triệu để dành", "template": "nv_board", "params": {"items": [{"art": "person", "x": 50, "y": 50, "s": 40}]}}]}
    bad_number = {"scenes": [{"first": "Hai trăm triệu để dành", "template": "edu_bignumber", "params": {"number": "999 tỷ", "label": "Không có trong câu"}}]}
    cases = {
        "đúng": check(good_lines, good, "ngv"),
        "sai first": check(good_lines, bad_first, "ngv"),
        "hình cấm": check(good_lines, bad_art, "ngv"),
        "số lạ": check(good_lines, bad_number, "ngv"),
    }
    problems = []
    if cases["đúng"]:
        problems.append("file đúng bị chặn: " + " | ".join(cases["đúng"][:4]))
    if not any("không khớp" in item for item in cases["sai first"]):
        problems.append("file sai first không bị chặn")
    if not any("không được phép" in item for item in cases["hình cấm"]):
        problems.append("hình cấm không bị chặn")
    if not any("999" in item for item in cases["số lạ"]):
        problems.append("số lạ không bị chặn")
    lead = [
        {"text": "Bạn thua cả tháng.", "short": True},
        {"text": "Cuối tháng chị mở sổ.", "short": False},
        {"text": "Ai vẫn có lãi.", "short": True},
        {"text": "Phí nằm ở cuối trang.", "short": False},
    ]
    lead_ok = {"scenes": [
        {"first": "Bạn thua cả tháng", "template": "edu_title", "params": {"title": "Ai có lãi"}},
        {"first": "Phí nằm ở cuối trang", "template": "edu_rules", "params": {"rules": ["Xem phí"]}},
    ]}
    lead_bad = {"scenes": [
        {"first": "Bạn thua cả tháng", "template": "edu_title", "params": {"title": "Ai có lãi"}},
        {"first": "Ai vẫn có lãi", "template": "edu_title", "params": {"title": "Hook"}},
    ]}
    if check(lead, lead_ok, "ngv"):
        problems.append("câu ngắn đầu phim bị chặn: " + " | ".join(check(lead, lead_ok, "ngv")[:3]))
    if not any("ngắn" in item for item in check(lead, lead_bad, "ngv")):
        problems.append("câu ngắn giữa phim vẫn được mở cảnh")
    if problems:
        json.dump({"ok": False, "errors": problems}, sys.stdout, ensure_ascii=False)
        return 1
    json.dump({"ok": True, "errors": []}, sys.stdout, ensure_ascii=False)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

"""Choose a whole film from scene briefs.

A brief is the meaning of one narration span. Retrieval applies hard filters
first, then ranks whoever is left. Sequence search looks across the timeline.
Image-text embeddings are not used: this environment has no local model, and a
paid API is out of scope. Token overlap is measured only as a comparison.
"""

from __future__ import annotations

import importlib
import re
from kit_video.chart import load_candles, ohlcv_errors, sma, structure_notes
from kit_video.library import hamming
from kit_video.still import BLOCKED_RIGHTS

# Trial weights. The evaluation set is what says whether they are good enough.
WEIGHTS = {
    "semantic_relevance": 0.40,
    "scene_role_fit": 0.20,
    "asset_quality": 0.15,
    "composition_fit": 0.10,
    "underuse_bonus": 0.15,
}

# Distances measured on the approved catalog. Same-image pairs in review sit at
# 0 and 1. The closest two approved originals, the two POS screens, sit at 6.
PERCEPTUAL_SAME = 3
PERCEPTUAL_NEAR = 6
SEMANTIC_FLOOR = 0.50
BEAM_WIDTH = 16
CANDIDATE_CAP = 8

DISCRIMINATORS = (
    "liên thông",
    "nhân viên",
    "doanh thu",
    "xuất nhập tồn",
    "đăng nhập",
    "hóa đơn",
    "nháp",
    "ứng dụng",
    "cấu hình",
    "đồng bộ",
    "phòng chờ",
    "quầy",
    "chai",
    "lọ",
    "vỉ",
    "thuốc",
    "sản phẩm",
)

_WORD = re.compile(r"\w+", re.UNICODE)


def interpret(narration: str, brand: str, start: float, end: float) -> dict:
    """Turn one spoken span into a brief. The topic is a role, not the raw line."""
    text = _norm(narration)
    duration = max(0.0, float(end) - float(start))
    intent = _intent(text)
    brief = {
        "narration_segment": narration.strip(),
        "time_range": [round(float(start), 3), round(float(end), 3)],
        "brand_scope": brand,
        "topic": intent["topic"],
        "entities": _hits(text, ("tồn kho", "hạn dùng", "vỉ", "chai", "lọ", "hộp", "nhà thuốc", "dược sĩ", "khách", "bitcoin", "ethereum", "báo cáo", "logo")),
        "actions": _hits(text, ("xem", "bán", "nhập", "kiểm", "tư vấn", "đối chiếu", "đăng nhập", "tăng", "giảm")),
        "key_message": narration.strip(),
        "visual_intent": intent["visual_intent"],
        "required_asset_types": list(intent["types"]),
        "required_categories": list(intent["categories"]),
        "preferred_tags": list(intent["tags"]),
        "forbidden_tags": list(intent["forbidden_tags"]),
        "forbidden_categories": list(intent["forbidden_categories"]),
        "visual_role": intent["role"],
        "chart_requirements": intent["chart"],
        "desired_duration_range": [round(max(3.0, duration * 0.75), 3), round(max(duration, duration * 1.15), 3)],
        "continuity_constraints": list(intent["continuity"]),
        "selected_assets": [],
        "selection_reasons": [],
    }
    return brief


def retrieve(brief: dict, rows: list[dict]) -> dict:
    """Hard-filter the catalog, then keep a scored pool. The first row is not the answer."""
    if brief["visual_intent"] in {"chart", "unspecified"}:
        return {"accepted": [], "rejected": [{"assetId": None, "reason": "Cảnh này không lấy ảnh tĩnh."}]}
    accepted = []
    rejected = []
    requested = [item for item in DISCRIMINATORS if item in _for_terms(brief["narration_segment"])]
    for row in rows:
        ok, reason = hard_filter(brief, row)
        if not ok:
            rejected.append({"assetId": row.get("assetId"), "reason": reason})
            continue
        parts = _parts(brief, row, requested, usage_count=_usage_count(row))
        if parts["semantic_relevance"] < SEMANTIC_FLOOR:
            rejected.append({
                "assetId": row.get("assetId"),
                "reason": "Đúng bộ lọc cứng nhưng độ nghĩa dưới ngưỡng.",
                "semantic_relevance": parts["semantic_relevance"],
            })
            continue
        accepted.append({"asset": row, "parts": parts, "score": _combine(parts), "reason": _fit_reason(brief, row, parts)})
    accepted.sort(key=lambda item: (-item["score"], str(item["asset"].get("assetId"))))
    return {"accepted": accepted, "rejected": rejected}


def optimize_sequence(briefs: list[dict], rows: list[dict], series_by_symbol: dict | None = None) -> list[dict]:
    """Beam search over a short candidate list. The score is the whole timeline."""
    series_by_symbol = series_by_symbol or {}
    pools = [_pool(brief, rows, series_by_symbol) for brief in briefs]
    beam = [{"score": 0.0, "picks": [], "used": [], "last_hashes": []}]
    for index, pool in enumerate(pools):
        brief = briefs[index]
        nxt = []
        for path in beam:
            for candidate in pool:
                step, notes = _transition(brief, candidate, path["used"], path["last_hashes"])
                hashes = [int(asset.get("averageHash") or 0) for asset in candidate["assets"] if asset.get("averageHash")]
                nxt.append({
                    "score": path["score"] + step,
                    "picks": path["picks"] + [{"candidate": candidate, "notes": notes}],
                    "used": path["used"] + list(candidate["ids"]),
                    "last_hashes": hashes or path["last_hashes"],
                })
        nxt.sort(key=lambda item: -item["score"])
        beam = nxt[:BEAM_WIDTH] or beam
    best = beam[0]
    timeline = []
    for brief, pick in zip(briefs, best["picks"]):
        timeline.append(_outcome(brief, pick["candidate"], pick["notes"], series_by_symbol))
    return timeline


def plan_beats(brief: dict, selection: dict, chart: dict | None) -> list[dict]:
    """Split a span where the picture or the sentence actually changes."""
    start, end = brief["time_range"]
    duration = max(0.0, end - start)
    if chart and chart.get("status") == "chosen":
        return _chart_beats(brief, chart, start, duration)
    events = []
    if brief["visual_intent"] == "composite":
        events.append({"at": round(start + duration * 0.25, 3), "kind": "inset", "text": "Ảnh thứ hai xuất hiện cạnh hình nền."})
    elif selection.get("assets"):
        events.append({"at": round(start + min(0.6, duration * 0.2), 3), "kind": "caption", "text": brief["key_message"]})
    hold = ""
    if duration >= 16 and brief["visual_intent"] != "composite":
        hold = "Một ý, một ảnh. Giữ khung vì lời không chuyển sang ý khác."
    return [{
        "beatId": brief.get("briefId") or "beat-1",
        "start": round(start, 3),
        "end": round(end, 3),
        "emphasis": brief["key_message"],
        "layout": _layout(brief),
        "events": events,
        "transition": "Cắt khi câu thoại kế tiếp đổi chủ đề.",
        "new_information": brief["topic"],
        "hold_reason": hold,
    }]


def select_chart(series: dict, requirements: dict) -> dict:
    """Find a real window that meets the brief. Refuse when the tape does not."""
    situation = str(requirements.get("situation") or "")
    if situation in {"strategy_pnl", "unsupported"}:
        return {"status": "refused", "reason": "Không vẽ lợi nhuận chiến lược và không bịa nến cho một tình huống không có trong dữ liệu.", "predicate_ok": False}
    symbol = str(requirements.get("symbol") or "")
    if symbol and symbol != str(series.get("symbol") or ""):
        return {"status": "gap", "reason": "File nến không đúng symbol.", "predicate_ok": False}
    candles = series["candles"]
    errors = ohlcv_errors(candles)
    if errors:
        return {"status": "gap", "reason": "File nến không qua kiểm tra OHLCV.", "predicate_ok": False, "errors": errors[:5]}
    count = int(requirements.get("count") or 48)
    best = None
    for start in range(0, len(candles) - count + 1):
        window = candles[start:start + count]
        if not window_meets(window, requirements):
            continue
        notes = structure_notes(window)
        rank = _chart_rank(situation, notes)
        if best is None or rank > best["rank"]:
            best = {"start": start, "notes": notes, "rank": rank}
    if best is None:
        return {"status": "gap", "reason": "Không có đoạn nào trong file thỏa điều kiện đã khai.", "predicate_ok": False}
    window = candles[best["start"]:best["start"] + count]
    indicators = _indicator_trace(candles, best["start"], count, requirements)
    return {
        "status": "chosen",
        "reason": "Đoạn này thỏa điều kiện trên chính các nến được vẽ.",
        "predicate_ok": True,
        "symbol": series.get("symbol"),
        "timeframe": series.get("timeframe"),
        "source": series.get("source"),
        "mode": series.get("mode"),
        "start": best["start"],
        "count": count,
        "notes": best["notes"],
        "indicators": indicators,
        "highlight": _largest_up(window),
    }


def window_meets(window: list[dict], requirements: dict) -> bool:
    """Independent check used by the search and again by the evaluation."""
    if ohlcv_errors(window):
        return False
    situation = str(requirements.get("situation") or "")
    notes = structure_notes(window)
    change = float(notes.get("change") or 0)
    closes = [row["c"] for row in window]
    third = max(1, len(closes) // 3)
    early = sum(closes[:third]) / third
    late = sum(closes[-third:]) / third
    if situation == "rise":
        high = max(row["h"] for row in window)
        return change >= float(requirements.get("min_change") or 0.03) and closes[-1] >= high * 0.97 and late > early
    if situation == "drop":
        return change <= -float(requirements.get("min_change") or 0.03) and late < early
    if situation == "narrow":
        first = closes[0] or 1
        span = (max(row["h"] for row in window) - min(row["l"] for row in window)) / first
        return abs(change) <= float(requirements.get("max_change") or 0.012) and span >= 0.012
    return False


def hard_filter(brief: dict, row: dict) -> tuple[bool, str]:
    brand = str(brief.get("brand_scope") or "")
    if brand and row.get("brand") not in {brand, "Shared"}:
        return False, "Sai thương hiệu."
    if row.get("approval") != "approved" or row.get("derived") or row.get("parentId"):
        return False, "Chưa duyệt, hoặc là bản gần giống đã bị loại."
    rights = str(row.get("usageRights") or "").strip().lower()
    if rights in BLOCKED_RIGHTS:
        return False, "Thiếu quyền sử dụng."
    kind = str(row.get("kind") or "")
    types = brief.get("required_asset_types") or []
    if types and kind not in types:
        return False, f"Sai loại tài nguyên: cần {', '.join(types)}."
    categories = brief.get("required_categories") or []
    category = str(row.get("category") or "")
    if categories and not any(category == item or category.startswith(item + ".") for item in categories):
        return False, "Sai nhóm nội dung."
    blob = _asset_text(row)
    for tag in brief.get("forbidden_tags") or []:
        if tag and tag in blob:
            return False, f"Dính thẻ bị cấm: {tag}."
    for item in brief.get("forbidden_categories") or []:
        if category == item or category.startswith(item + "."):
            return False, "Thuộc nhóm bị cấm."
    if int(row.get("width") or 0) < 320 or int(row.get("height") or 0) < 320:
        return False, "Ảnh nhỏ hơn ngưỡng kỹ thuật."
    return True, ""


def hash_profile(rows: list[dict]) -> dict:
    """Report the distances the thresholds were checked against."""
    approved = [row for row in rows if row.get("approval") == "approved" and not row.get("derived") and row.get("averageHash")]
    nearest = None
    for index, left in enumerate(approved):
        for right in approved[index + 1:]:
            distance = hamming(int(left["averageHash"]), int(right["averageHash"]))
            if nearest is None or distance < nearest["distance"]:
                nearest = {"distance": distance, "left": left["assetId"], "right": right["assetId"]}
    derived = []
    for row in rows:
        parent = str(row.get("parentId") or "")
        if not parent or not row.get("averageHash"):
            continue
        match = next((item for item in rows if item.get("assetId") == parent and item.get("averageHash")), None)
        if match is None:
            continue
        derived.append({
            "assetId": row.get("assetId"),
            "parentId": parent,
            "distance": hamming(int(row["averageHash"]), int(match["averageHash"])),
        })
    return {
        "perceptualSame": PERCEPTUAL_SAME,
        "perceptualNear": PERCEPTUAL_NEAR,
        "nearestApproved": nearest,
        "derived": derived,
    }


def probe_embedding() -> dict:
    """A local image-text model is optional. Absence is a result, not a silent fallback claim."""
    for name in ("sentence_transformers", "open_clip", "clip", "torch"):
        try:
            importlib.import_module(name)
        except ImportError:
            continue
        return {"available": True, "module": name, "used": False, "reason": "Có thư viện nhưng chưa có checkpoint đã kiểm tra. Không tự tải model."}
    return {
        "available": False,
        "used": False,
        "reason": "Máy không có torch, CLIP hay sentence-transformers. Không gọi API embedding trả phí.",
    }


def evaluate(groups: list[dict], rows: list[dict], series_by_symbol: dict | None = None) -> dict:
    """Score the selector against reviewer labels. The labels are an input, not an output."""
    series_by_symbol = series_by_symbol or {}
    scenes = []
    hard = []
    exact_pairs = []
    near_pairs = []
    layout_runs = []
    chart_met = 0
    chart_total = 0
    for group in groups:
        briefs = []
        for scene in group["scenes"]:
            brief = interpret(scene["narration"], scene["brand"], scene["start"], scene["end"])
            brief["briefId"] = scene["id"]
            briefs.append(brief)
        outcomes = optimize_sequence(briefs, rows, series_by_symbol)
        previous = None
        layout_run = []
        for scene, outcome in zip(group["scenes"], outcomes):
            chart_flag = _chart_flag(scene, outcome, series_by_symbol)
            if scene.get("chart"):
                chart_total += 1
                chart_met += 1 if chart_flag["ok"] else 0
            appropriate = _matches(scene, outcome, chart_flag)
            violation = _selected_violation(outcome)
            if violation:
                hard.append({"sceneId": scene["id"], "reason": violation})
            ids = [item["assetId"] for item in outcome["selected"]["assets"]]
            if previous and ids and previous["ids"]:
                if set(ids) & set(previous["ids"]) and not outcome["brief"]["continuity_constraints"]:
                    exact_pairs.append({"sceneId": scene["id"], "assets": ids})
                if _near(previous["hashes"], outcome):
                    near_pairs.append({"sceneId": scene["id"], "assets": ids})
            layout = outcome["selected"].get("layout")
            category = outcome["selected"].get("category") or ""
            if previous and layout and layout == previous["layout"] and category and category == previous["category"] and not outcome["brief"]["continuity_constraints"]:
                layout_run.append(scene["id"])
            else:
                if len(layout_run) >= 3:
                    layout_runs.append(layout_run)
                layout_run = [scene["id"]] if layout else []
            scenes.append({
                "sceneId": scene["id"],
                "appropriate": appropriate and not violation,
                "gap": outcome["selected"]["status"] == "gap",
                "status": outcome["selected"]["status"],
                "assets": ids,
                "chartOk": chart_flag["ok"] if scene.get("chart") else None,
                "note": scene.get("note") or "",
            })
            previous = {
                "ids": ids,
                "hashes": [int(item.get("averageHash") or 0) for item in outcome["selected"]["assets"]],
                "layout": layout,
                "category": category,
            }
        if len(layout_run) >= 3:
            layout_runs.append(layout_run)
    total = len(scenes)
    appropriate_n = sum(1 for item in scenes if item["appropriate"])
    gaps = sum(1 for item in scenes if item["gap"])
    still = [item for item in scenes if item["chartOk"] is None]
    return {
        "scenes": total,
        "appropriate": appropriate_n,
        "appropriateRate": appropriate_n / total if total else 0,
        "gaps": gaps,
        "gapRate": gaps / total if total else 0,
        "exactRepeatPairs": exact_pairs,
        "nearRepeatPairs": near_pairs,
        "unintendedLayoutRuns": layout_runs,
        "hardViolations": hard,
        "chartsMet": chart_met,
        "charts": chart_total,
        "baseline": _baseline(groups, rows),
        "jaccard": _jaccard_trial(groups, rows),
        "embedding": probe_embedding(),
        "weights": WEIGHTS,
        "stillScenes": len(still),
        "sceneResults": scenes,
        "hashProfile": hash_profile(rows),
    }


def _intent(text: str) -> dict:
    chart = _chart_intent(text)
    if chart:
        return chart
    if "không dùng lại" in text and "tồn" in text:
        return _still("inventory.stock", "show_software", ["screenshot"], ["inventory.stock"], ["tồn", "liên thông"], forbid=True)
    if "vỉ" in text and ("màn" in text or "đối chiếu" in text):
        made = _composite()
        if "giữ" in text or "đối chiếu" in text:
            made["continuity"] = [{
                "reuse_categories": ["medicine.packaging", "inventory.stock"],
                "reason": "Lời nói giữ vỉ và đặt cạnh màn tồn để đối chiếu.",
            }]
        return made
    if "dược sĩ" in text and ("tư vấn" in text or "khách" in text):
        return _still("people.consult", "show_person", ["photo"], ["people.consult"], ["dược sĩ", "tư vấn"], forbidden_categories=["medicine.packaging", "software.pos"])
    if "bàn giao dịch" in text or "nhiều màn hình" in text:
        return _still("environment.desk", "show_place", ["photo"], ["environment.desk"], ["bàn giao dịch", "màn hình"])
    if "hộp" in text and ("các mặt" in text or "bán lẻ" in text):
        return _still("medicine.box", "show_product", ["photo"], ["medicine.box"], ["hộp thuốc"], forbidden_categories=["medicine.packaging"])
    if "kho thật" in text or "trong kho" in text or "kiểm hàng" in text:
        return _still("environment.warehouse", "show_place", ["photo"], ["environment.warehouse"], ["kho"], forbidden_categories=["inventory.receiving"])
    if "logo" in text:
        return _still("brand.mark", "show_brand", ["logo"], ["brand.mark"], ["logo"])
    if "đăng nhập" in text:
        return _still("software.login", "show_software", ["screenshot"], ["software.login"], ["đăng nhập"])
    if "xuất nhập tồn" in text:
        return _still("software.reports", "show_software", ["screenshot"], ["software.reports"], ["xuất nhập tồn"])
    if "doanh thu" in text:
        return _still("software.reports", "show_software", ["screenshot"], ["software.reports"], ["doanh thu"])
    if "hóa đơn" in text:
        return _still("software.sale", "show_software", ["screenshot"], ["software.sale"], ["hóa đơn"])
    if "nháp" in text:
        return _still("software.draft", "show_software", ["screenshot"], ["software.draft"], ["nháp"])
    if "ứng dụng" in text:
        return _still("customer.app", "show_software", ["screenshot"], ["customer.app"], ["ứng dụng"])
    if "cấu hình" in text:
        return _still("software.compliance", "show_software", ["screenshot"], ["software.compliance"], ["cấu hình"])
    if "liên thông" in text and "danh mục" in text:
        return _still("software.catalog", "show_software", ["screenshot"], ["software.catalog"], ["danh mục", "thuốc", "liên thông"])
    if "liên thông" in text and ("kết nối" in text or "theo dõi" in text):
        return _still("software.compliance", "show_software", ["screenshot"], ["software.compliance"], ["liên thông", "kết nối"])
    if "nhân viên" in text and "bán" in text:
        return _still("software.pos", "show_software", ["screenshot"], ["software.pos"], ["nhân viên", "bán"])
    if "quầy" in text and "bán" in text:
        return _still("software.pos", "show_software", ["screenshot"], ["software.pos"], ["quầy", "bán"])
    if "hồ sơ" in text:
        return _still("customer.crm", "show_software", ["screenshot"], ["customer.crm"], ["hồ sơ", "khách"])
    if "nhập hàng" in text:
        return _still("inventory.receiving", "show_software", ["screenshot"], ["inventory.receiving"], ["nhập hàng"])
    if ("tồn" in text or "hạn dùng" in text) and ("màn" in text or "phần mềm" in text):
        made = _still("inventory.stock", "show_software", ["screenshot"], ["inventory.stock"], ["tồn", "hạn"], forbidden_categories=["medicine.packaging"])
        return made
    if "danh mục" in text and "phần mềm" in text:
        tags = ["danh mục"]
        if "sản phẩm" in text:
            tags.append("sản phẩm")
        if "thuốc" in _for_terms(text):
            tags.append("thuốc")
        return _still("software.catalog", "show_software", ["screenshot"], ["software.catalog"], tags)
    if "báo cáo" in text:
        return _still("software.reports", "show_software", ["screenshot"], ["software.reports"], ["báo cáo"])
    if "tổng quan" in text or "dashboard" in text:
        return _still("software.dashboard", "show_software", ["screenshot"], ["software.dashboard"], ["tổng quan"])
    if "phòng chờ" in text or ("đợi" in text and "quầy" in text):
        return _still("environment.pharmacy.wait", "show_place", ["photo"], ["environment.pharmacy.wait"], ["phòng chờ"])
    if "chai" in text or "lọ" in text:
        return _still("medicine.packaging", "show_product", ["photo"], ["medicine.packaging"], ["chai", "lọ"])
    if "kiểm" in text and "vỉ" in text:
        return _still("people.handling", "show_person", ["photo"], ["people.handling"], ["kiểm", "vỉ"])
    if "vỉ" in text:
        return _still("medicine.packaging", "show_product", ["photo"], ["medicine.packaging"], ["vỉ"], forbidden_categories=["software.dashboard", "software.pos", "inventory.stock"])
    if "tủ thuốc" in text or "nhà thuốc" in text:
        return _still("environment.pharmacy", "show_place", ["photo"], ["environment.pharmacy"], ["nhà thuốc", "tủ"])
    return _still("unspecified", "unspecified", [], [], [])


def _chart_intent(text: str) -> dict | None:
    if "lợi nhuận" in text or "pnl" in text:
        return _chart_shell({"situation": "strategy_pnl", "symbol": "BTCUSDT", "timeframe": "1h"})
    if not any(token in text for token in ("bitcoin", "ethereum")):
        return None
    symbol = "ETHUSDT" if "ethereum" in text else "BTCUSDT"
    if "80" in text or "tám mươi" in text:
        situation, minimum = "rise", 0.8
    elif "giảm" in text:
        situation, minimum = "drop", 0.03
    elif "biên" in text or "hẹp" in text:
        situation, minimum = "narrow", 0.0
    elif "tăng" in text:
        situation, minimum = "rise", 0.03
    else:
        return _chart_shell({"situation": "unsupported", "symbol": symbol, "timeframe": "1h"})
    requirements = {"situation": situation, "symbol": symbol, "timeframe": "1h", "source": "binance-public-klines", "mode": "historical", "count": 48}
    if situation == "rise":
        requirements["min_change"] = minimum
    if situation == "drop":
        requirements["min_change"] = minimum
    if situation == "narrow":
        requirements["max_change"] = 0.012
    if "sma" in text:
        requirements["indicators"] = ["sma20"]
    if "nến tăng" in text:
        requirements["annotate"] = "largest_up"
    return _chart_shell(requirements)


def _still(topic: str, intent: str, types: list[str], categories: list[str], tags: list[str], forbid: bool = False, forbidden_categories: list[str] | None = None) -> dict:
    continuity = []
    if forbid:
        continuity.append({"forbid_reuse": True, "reason": "Lời nói yêu cầu một hình khác, không dùng lại hình vừa rồi."})
    return {
        "topic": topic,
        "visual_intent": intent,
        "types": types,
        "categories": categories,
        "tags": tags,
        "forbidden_tags": [],
        "forbidden_categories": forbidden_categories or [],
        "role": "hero",
        "chart": None,
        "continuity": continuity,
    }


def _composite() -> dict:
    return {
        "topic": "inventory.stock+medicine.packaging",
        "visual_intent": "composite",
        "types": ["screenshot", "photo"],
        "categories": ["inventory.stock", "medicine.packaging"],
        "tags": ["tồn", "vỉ"],
        "forbidden_tags": [],
        "forbidden_categories": [],
        "role": "hero",
        "chart": None,
        "continuity": [],
    }


def _chart_shell(requirements: dict) -> dict:
    return {
        "topic": "market." + str(requirements.get("situation") or "chart"),
        "visual_intent": "chart",
        "types": [],
        "categories": [],
        "tags": [],
        "forbidden_tags": [],
        "forbidden_categories": [],
        "role": "chart",
        "chart": requirements,
        "continuity": [],
    }


def _pool(brief: dict, rows: list[dict], series_by_symbol: dict) -> list[dict]:
    if brief["visual_intent"] == "chart":
        series = series_by_symbol.get(str((brief["chart_requirements"] or {}).get("symbol") or ""))
        if series is None:
            return [_gap("Không có file nến cho symbol này.")]
        found = select_chart(series, brief["chart_requirements"] or {})
        if found["status"] == "refused":
            refused = _gap(found["reason"])
            refused["kind"] = "refused"
            refused["chart"] = found
            return [refused]
        if found["status"] != "chosen":
            return [_gap(found["reason"])]
        return [{
            "ids": [],
            "assets": [],
            "score": 0.92,
            "layout": "chart-plate",
            "category": brief["topic"],
            "kind": "chart",
            "chart": found,
            "reason": found["reason"],
            "parts": {},
        }]
    if brief["visual_intent"] == "composite":
        return _composite_pool(brief, rows) or [_gap("Thiếu một trong hai ảnh để ghép.")]
    found = retrieve(brief, rows)
    pool = []
    for item in found["accepted"][:CANDIDATE_CAP]:
        row = item["asset"]
        pool.append({
            "ids": [str(row.get("assetId"))],
            "assets": [row],
            "score": item["score"],
            "layout": _asset_layout(row),
            "category": str(row.get("category") or ""),
            "kind": "still",
            "chart": None,
            "reason": item["reason"],
            "parts": item["parts"],
            "rejected_sample": found["rejected"][:8],
        })
    if not pool:
        gap = _gap("Không có ảnh đã duyệt, đúng loại và đủ nghĩa.")
        gap["rejected_sample"] = found["rejected"][:8]
        return [gap]
    return pool


def _composite_pool(brief: dict, rows: list[dict]) -> list[dict]:
    back_brief = dict(brief)
    back_brief["required_asset_types"] = ["screenshot"]
    back_brief["required_categories"] = ["inventory.stock"]
    back_brief["preferred_tags"] = ["tồn"]
    inset_brief = dict(brief)
    inset_brief["required_asset_types"] = ["photo"]
    inset_brief["required_categories"] = ["medicine.packaging"]
    inset_brief["preferred_tags"] = ["vỉ"]
    back_brief["narration_segment"] = "tồn kho trên màn hình"
    inset_brief["narration_segment"] = "vỉ thuốc"
    backs = retrieve(back_brief, rows)["accepted"][:3]
    insets = retrieve(inset_brief, rows)["accepted"][:3]
    pool = []
    for back in backs:
        for inset in insets:
            if back["asset"]["assetId"] == inset["asset"]["assetId"]:
                continue
            score = (back["score"] + inset["score"]) / 2
            pool.append({
                "ids": [str(back["asset"]["assetId"]), str(inset["asset"]["assetId"])],
                "assets": [back["asset"], inset["asset"]],
                "score": score,
                "layout": "composite-plate",
                "category": "inventory.stock+medicine.packaging",
                "kind": "composite",
                "chart": None,
                "reason": "Màn tồn là hình nền. Ảnh vỉ là ảnh tham chiếu, không thay cho giao diện.",
                "parts": {},
            })
    pool.sort(key=lambda item: -item["score"])
    return pool[:CANDIDATE_CAP]


def _gap(reason: str) -> dict:
    return {
        "ids": [],
        "assets": [],
        "score": 0.0,
        "layout": "",
        "category": "",
        "kind": "gap",
        "chart": None,
        "reason": reason,
        "parts": {},
    }


def _transition(brief: dict, candidate: dict, used: list[str], last_hashes: list[int]) -> tuple[float, list[str]]:
    notes = []
    if candidate["kind"] == "gap":
        return 0.0, [candidate["reason"]]
    penalty = 0.0
    allowed = []
    forbid = False
    for rule in brief.get("continuity_constraints") or []:
        if rule.get("forbid_reuse"):
            forbid = True
            notes.append(rule.get("reason") or "Không dùng lại.")
        allowed.extend(rule.get("reuse_categories") or [])
        if rule.get("reason") and not rule.get("forbid_reuse"):
            notes.append(rule["reason"])
    repeated = [asset_id for asset_id in candidate["ids"] if asset_id in used]
    if repeated and forbid:
        return -1.0, notes + ["Bị loại vì lời nói cấm dùng lại ảnh vừa chọn."]
    if repeated:
        categories = [str(asset.get("category") or "") for asset in candidate["assets"]]
        waived = allowed and all(any(category == item or category.startswith(item + ".") for item in allowed) for category in categories)
        if waived:
            notes.append("Giữ ảnh vì ràng buộc liên tục, không phạt lặp.")
        else:
            penalty += 0.28 * len(repeated)
            notes.append("Phạt vì ảnh đã dùng trong video.")
    current_hashes = [int(asset.get("averageHash") or 0) for asset in candidate["assets"]]
    near = False
    for left in last_hashes:
        for right in current_hashes:
            if left and right and 0 < hamming(left, right) <= PERCEPTUAL_NEAR:
                near = True
                break
        if near:
            break
    if near:
        penalty += 0.18
        notes.append("Phạt vì hash nhìn gần ảnh vừa dùng.")
    notes.append(candidate["reason"])
    return candidate["score"] - penalty, notes


def _outcome(brief: dict, candidate: dict, notes: list[str], series_by_symbol: dict) -> dict:
    brief = dict(brief)
    brief["selected_assets"] = [asset.get("assetId") for asset in candidate["assets"]]
    brief["selection_reasons"] = notes
    chart = candidate.get("chart")
    status = "chosen"
    if candidate["kind"] == "gap":
        status = "gap"
    elif candidate["kind"] == "refused":
        status = "refused"
    selection = {
        "status": status,
        "assets": candidate["assets"],
        "score": candidate["score"],
        "layout": candidate["layout"],
        "category": candidate["category"],
        "reasons": notes,
    }
    if chart and chart.get("status") == "refused":
        selection["status"] = "refused"
    beats = plan_beats(brief, selection, chart)
    return {"brief": brief, "selected": selection, "chart": chart, "beats": beats, "candidates_note": candidate.get("reason")}


def _parts(brief: dict, row: dict, requested: list[str], usage_count: int) -> dict:
    blob = _asset_text(row)
    tags = brief.get("preferred_tags") or []
    hits = sum(1 for tag in tags if tag and tag in blob)
    tag_score = hits / len(tags) if tags else 1.0
    category = str(row.get("category") or "")
    categories = brief.get("required_categories") or []
    category_score = 1.0 if any(category == item or category.startswith(item + ".") for item in categories) else 0.0
    semantic = 0.6 * category_score + 0.4 * tag_score
    terms = _for_terms(blob)
    asked = _for_terms(brief["narration_segment"])
    if requested and not any(item in terms for item in requested):
        semantic = min(semantic, 0.35)
    extra = [item for item in DISCRIMINATORS if item in terms and item not in asked]
    if extra:
        semantic = max(0.0, semantic - 0.35 * len(extra))
    kind = str(row.get("kind") or "")
    types = brief.get("required_asset_types") or []
    role = 1.0 if not types or kind in types else 0.2
    short_side = min(int(row.get("width") or 0), int(row.get("height") or 0))
    quality = 1.0 if short_side >= 720 else (0.7 if short_side >= 480 else 0.35)
    composition = _composition(brief, row)
    # A full drop from 1 to 0.1 let an unused near-miss beat a used, better screen.
    underuse = 1.0 if usage_count <= 0 else (0.85 if usage_count == 1 else 0.7)
    return {
        "semantic_relevance": round(semantic, 4),
        "scene_role_fit": role,
        "asset_quality": quality,
        "composition_fit": composition,
        "underuse_bonus": underuse,
    }


def _composition(brief: dict, row: dict) -> float:
    intent = brief.get("visual_intent")
    kind = str(row.get("kind") or "")
    if intent == "show_software" and kind == "screenshot":
        return 1.0
    if intent in {"show_product", "show_place", "show_person"} and kind == "photo":
        return 1.0
    if intent == "show_brand" and kind == "logo":
        return 1.0
    if intent == "composite":
        return 0.8
    return 0.3


def _combine(parts: dict) -> float:
    total = 0.0
    for key, weight in WEIGHTS.items():
        total += weight * float(parts.get(key) or 0)
    return round(total, 4)


def _fit_reason(brief: dict, row: dict, parts: dict) -> str:
    return (
        f"{row.get('assetId')} khớp {brief.get('topic')} "
        f"(nghĩa {parts['semantic_relevance']}, vai {parts['scene_role_fit']})."
    )


def _chart_beats(brief: dict, chart: dict, start: float, duration: float) -> list[dict]:
    text = _norm(brief["narration_segment"])
    specs = [("wide", 1.0, "Cả đoạn giá", "Người xem thấy hướng của cả cửa sổ.")]
    if chart.get("highlight") and "nến" in text:
        specs.append(("highlight", 1.35, "Nến tăng mạnh nhất", "Người xem thấy nến tạo đà, không phải một vùng tô ngẫu nhiên."))
    if "sma20" in ((brief.get("chart_requirements") or {}).get("indicators") or []) or "sma" in text:
        specs.append(("sma", 1.2, "SMA 20", "Đường trung bình được tính từ giá đóng cửa của chuỗi."))
    specs.append(("conclusion", 0.8, "Con số của đoạn", "Phần trăm là change của đúng cửa sổ được vẽ."))
    weights = sum(item[1] for item in specs)
    beats = []
    cursor = start
    for index, (name, weight, emphasis, info) in enumerate(specs):
        span = duration * weight / weights
        end = start + duration if index == len(specs) - 1 else cursor + span
        beats.append({
            "beatId": name,
            "start": round(cursor, 3),
            "end": round(end, 3),
            "emphasis": emphasis,
            "layout": "chart-plate",
            "events": [{"at": round(cursor, 3), "kind": name, "text": emphasis}],
            "transition": "Chuyển khi ý này đã hiện và câu thoại sang ý sau.",
            "new_information": info,
            "hold_reason": "",
        })
        cursor = end
    return beats


def _indicator_trace(candles: list[dict], start: int, count: int, requirements: dict) -> dict:
    closes = [row["c"] for row in candles]
    trace = {"requested": list(requirements.get("indicators") or [])}
    if start >= 19:
        value = sma(closes, 20)[start]
        manual = sum(closes[start - 19:start + 1]) / 20
        trace["sma20_at_start"] = value
        trace["sma20_manual"] = manual
        trace["sma20_matches"] = value is not None and abs(float(value) - manual) < 1e-4
    return trace


def _largest_up(window: list[dict]) -> dict | None:
    best = None
    for index, row in enumerate(window):
        if row["o"] <= 0 or row["c"] <= row["o"]:
            continue
        body = (row["c"] - row["o"]) / row["o"]
        if best is None or body > best["body"]:
            best = {"index": index, "body": body, "open": row["o"], "close": row["c"]}
    return best


def _chart_rank(situation: str, notes: dict) -> float:
    change = float(notes.get("change") or 0)
    if situation == "drop":
        return -change
    if situation == "narrow":
        return -abs(change)
    return change


def _layout(brief: dict) -> str:
    return {
        "show_software": "ui-plate",
        "show_product": "product-plate",
        "show_place": "place-plate",
        "show_person": "person-plate",
        "show_brand": "brand-plate",
        "composite": "composite-plate",
        "chart": "chart-plate",
    }.get(brief.get("visual_intent"), "type-plate")


def _asset_layout(row: dict) -> str:
    kind = str(row.get("kind") or "")
    if kind == "screenshot":
        return "ui-plate"
    if kind == "logo":
        return "brand-plate"
    category = str(row.get("category") or "")
    if category.startswith("people"):
        return "person-plate"
    if category.startswith("environment"):
        return "place-plate"
    if category.startswith("medicine"):
        return "product-plate"
    return "photo-plate"


def _asset_text(row: dict) -> str:
    path = str(row.get("path") or "")
    folder = " liên thông" if "lien-thong" in path else ""
    return _norm(" ".join([
        str(row.get("assetId") or ""),
        str(row.get("label") or ""),
        str(row.get("category") or ""),
        " ".join(str(item) for item in (row.get("keywords") or [])),
    ]) + folder)


def _usage_count(row: dict) -> int:
    usage = row.get("usage")
    return len(usage) if isinstance(usage, list) else 0


def _near_assets(assets: list[dict], used_ids: list[str]) -> bool:
    del used_ids
    hashes = [int(asset.get("averageHash") or 0) for asset in assets if asset.get("averageHash")]
    return False if len(hashes) < 2 else any(hamming(hashes[index], other) <= PERCEPTUAL_NEAR for index in range(len(hashes)) for other in hashes[index + 1:])


def _near(previous: list[int], outcome: dict) -> bool:
    current = [int(item.get("averageHash") or 0) for item in outcome["selected"]["assets"]]
    for left in previous:
        for right in current:
            if left and right and 0 < hamming(left, right) <= PERCEPTUAL_NEAR:
                return True
    return False


def _matches(scene: dict, outcome: dict, chart_flag: dict) -> bool:
    status = outcome["selected"]["status"]
    ids = [item["assetId"] for item in outcome["selected"]["assets"]]
    if scene.get("chart"):
        if scene.get("gap"):
            return status in {"gap", "refused"}
        return bool(chart_flag["ok"])
    if scene.get("gap"):
        return status == "gap" and not ids and not any(asset_id in ids for asset_id in scene.get("reject") or [])
    if scene.get("accept_all"):
        return set(scene["accept_all"]).issubset(set(ids))
    return any(asset_id in (scene.get("accept") or []) for asset_id in ids)


def _chart_flag(scene: dict, outcome: dict, series_by_symbol: dict) -> dict:
    if not scene.get("chart"):
        return {"ok": False}
    chart = outcome.get("chart") or {}
    if scene.get("gap"):
        return {"ok": outcome["selected"]["status"] in {"gap", "refused"}}
    if chart.get("status") != "chosen":
        return {"ok": False}
    series = series_by_symbol.get(chart.get("symbol") or "")
    if series is None:
        return {"ok": False}
    window = series["candles"][int(chart["start"]):int(chart["start"]) + int(chart["count"])]
    requirements = dict(scene.get("chart") or {})
    return {"ok": window_meets(window, requirements)}


def _selected_violation(outcome: dict) -> str:
    brief = outcome["brief"]
    if brief["visual_intent"] == "chart":
        return ""
    for asset in outcome["selected"]["assets"]:
        ok, reason = hard_filter(brief if brief["visual_intent"] != "composite" else _composite_side(brief, asset), asset)
        if not ok:
            return f"{asset.get('assetId')}: {reason}"
    return ""


def _composite_side(brief: dict, asset: dict) -> dict:
    side = dict(brief)
    if str(asset.get("kind")) == "screenshot":
        side["required_asset_types"] = ["screenshot"]
        side["required_categories"] = ["inventory.stock"]
    else:
        side["required_asset_types"] = ["photo"]
        side["required_categories"] = ["medicine.packaging"]
    return side


def _baseline(groups: list[dict], rows: list[dict]) -> dict:
    """Bag-of-words over the narration. No type filter and no look across the film."""
    from kit_video.library import choose_asset

    judged = []
    for group in groups:
        for scene in group["scenes"]:
            if scene.get("chart"):
                continue
            tokens = [token for token in _WORD.findall(_norm(scene["narration"])) if len(token) >= 4]
            choice = choose_asset(rows, {"brand": scene["brand"], "category": "", "keywords": tokens}, [], [], [])
            asset_id = (choice.get("asset") or {}).get("assetId")
            if scene.get("gap"):
                ok = choice["status"] != "chosen"
            elif scene.get("accept_all"):
                ok = asset_id in scene["accept_all"]
            else:
                ok = asset_id in (scene.get("accept") or [])
            judged.append(ok)
    total = len(judged)
    hit = sum(1 for item in judged if item)
    return {"scenes": total, "appropriate": hit, "appropriateRate": hit / total if total else 0}


def _jaccard_trial(groups: list[dict], rows: list[dict]) -> dict:
    """Local text overlap against labels. Compared, then not adopted if it loses."""
    judged = []
    eligible = [row for row in rows if row.get("approval") == "approved" and not row.get("derived") and not row.get("parentId")]
    for group in groups:
        for scene in group["scenes"]:
            if scene.get("chart"):
                continue
            left = set(_WORD.findall(_norm(scene["narration"])))
            best_id = None
            best = -1.0
            for row in eligible:
                if scene["brand"] and row.get("brand") not in {scene["brand"], "Shared"}:
                    continue
                right = set(_WORD.findall(_asset_text(row)))
                union = left | right
                score = len(left & right) / len(union) if union else 0
                if score > best:
                    best = score
                    best_id = row.get("assetId")
            if best <= 0:
                best_id = None
            if scene.get("gap"):
                ok = best_id is None
            elif scene.get("accept_all"):
                ok = best_id in scene["accept_all"]
            else:
                ok = best_id in (scene.get("accept") or [])
            judged.append(ok)
    total = len(judged)
    hit = sum(1 for item in judged if item)
    return {"scenes": total, "appropriate": hit, "appropriateRate": hit / total if total else 0, "adopted": False}


def _hits(text: str, words: tuple[str, ...]) -> list[str]:
    return [word for word in words if word in text]


def _for_terms(value: str) -> str:
    """Keep 'nhà thuốc' from being read as the drug-name discriminator."""
    return _norm(value).replace("nhà thuốc", "nha thuoc")


def _norm(value: str) -> str:
    return value.lower()


# load_candles is re-exported for callers that resolve a path beside the selector.
__all__ = [
    "WEIGHTS",
    "PERCEPTUAL_SAME",
    "PERCEPTUAL_NEAR",
    "interpret",
    "retrieve",
    "optimize_sequence",
    "plan_beats",
    "select_chart",
    "window_meets",
    "hard_filter",
    "hash_profile",
    "probe_embedding",
    "evaluate",
    "load_candles",
]

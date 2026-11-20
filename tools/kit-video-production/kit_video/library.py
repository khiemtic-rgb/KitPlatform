"""Shared still library. Selection follows the line being said, then refuses a repeat.

Code-drawn charts are not rows in this catalog. A crop of a file already listed
is a child, not a second original.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image


def load_catalog(path: Path) -> list[dict]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    rows = payload.get("assets") if isinstance(payload, dict) else payload
    if not isinstance(rows, list):
        raise ValueError("Catalog cần mảng assets.")
    return [row for row in rows if isinstance(row, dict)]


def file_sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def average_hash(path: Path, size: int = 8) -> int:
    """Difference hash. An average hash collapses these light UI screens into one value."""
    image = Image.open(path).convert("L").resize((size + 1, size))
    pixels = list(image.get_flattened_data()) if hasattr(image, "get_flattened_data") else list(image.getdata())
    bits = 0
    row = size + 1
    for y in range(size):
        for x in range(size):
            left = pixels[y * row + x]
            right = pixels[y * row + x + 1]
            bits = (bits << 1) | (1 if left > right else 0)
    return bits


def hamming(left: int, right: int) -> int:
    return (left ^ right).bit_count()


def count_independent(rows: list[dict]) -> dict:
    """Originals only. A child row, a rejected row, or a code chart is not an original."""
    totals = {"NOVIXA": 0, "AgentTrading": 0, "Shared": 0, "other": 0}
    for row in rows:
        if row.get("parentId") or row.get("derived"):
            continue
        if row.get("approval") not in {"approved", "review"}:
            continue
        if str(row.get("kind") or "") in {"chart", "code"}:
            continue
        brand = str(row.get("brand") or "other")
        if brand not in totals:
            brand = "other"
        totals[brand] += 1
    totals["total"] = totals["NOVIXA"] + totals["AgentTrading"] + totals["Shared"]
    return totals


def choose_asset(rows: list[dict], need: dict, used: list[str], recent_groups: list[str], recent_hashes: list[int] | None = None) -> dict:
    """Pick one original that matches the line. A miss is a gap, not a random picture."""
    brand = str(need.get("brand") or "")
    keywords = [str(item).lower() for item in (need.get("keywords") or []) if str(item).strip()]
    category = str(need.get("category") or "")
    ranked = []
    for row in rows:
        if row.get("parentId") or row.get("derived"):
            continue
        if row.get("approval") != "approved":
            continue
        if brand and row.get("brand") not in {brand, "Shared"}:
            continue
        if category and not str(row.get("category") or "").startswith(category):
            continue
        hay = " ".join([
            str(row.get("assetId") or ""),
            str(row.get("category") or ""),
            " ".join(str(item) for item in (row.get("keywords") or [])),
            str(row.get("label") or ""),
        ]).lower()
        score = sum(1 for word in keywords if word in hay)
        if keywords and score == 0:
            continue
        ranked.append((score, row))
    ranked.sort(key=lambda item: (-item[0], str(item[1].get("assetId"))))
    hashes = recent_hashes or []
    for _score, row in ranked:
        asset_id = str(row.get("assetId"))
        group = str(row.get("similarityGroup") or asset_id)
        if asset_id in used:
            continue
        if group in recent_groups:
            continue
        digest = int(row.get("averageHash") or 0)
        if digest and any(hamming(digest, previous) <= 4 for previous in hashes[-4:] if previous):
            continue
        return {"status": "chosen", "asset": row, "score": _score}
    return {
        "status": "gap",
        "asset": None,
        "reason": "Không có ảnh đã duyệt, đúng ý, và chưa dùng trong video này.",
        "need": need,
    }


def plan_usage(rows: list[dict], scenes: list[dict]) -> dict:
    """Walk the whole film once. Later scenes see what earlier scenes already took."""
    used: list[str] = []
    recent: list[str] = []
    hashes: list[int] = []
    choices = []
    gaps = []
    for scene in scenes:
        need = scene.get("need") if isinstance(scene.get("need"), dict) else {}
        if not need:
            continue
        choice = choose_asset(rows, need, used, recent[-2:], hashes)
        choices.append({"sceneId": scene.get("sceneId"), **{k: v for k, v in choice.items() if k != "asset"}, "assetId": (choice.get("asset") or {}).get("assetId")})
        if choice["status"] != "chosen":
            gaps.append({"sceneId": scene.get("sceneId"), "reason": choice["reason"], "need": need})
            continue
        asset = choice["asset"]
        used.append(str(asset.get("assetId")))
        recent.append(str(asset.get("similarityGroup") or asset.get("assetId")))
        digest = int(asset.get("averageHash") or 0)
        if digest:
            hashes.append(digest)
    near = []
    hashes = [(str(row.get("assetId")), int(row.get("averageHash") or 0), str(row.get("similarityGroup") or "")) for row in rows if row.get("averageHash")]
    for index, (left_id, left_hash, left_group) in enumerate(hashes):
        for right_id, right_hash, right_group in hashes[index + 1:]:
            if left_group and left_group == right_group:
                continue
            distance = hamming(left_hash, right_hash)
            if distance <= 8:
                near.append({"left": left_id, "right": right_id, "distance": distance})
    return {"choices": choices, "gaps": gaps, "nearDuplicates": near[:40], "used": used}


def assess_timeline(scenes: list[dict], rows: list[dict] | None = None) -> dict:
    """Repetition across a film. A finding is a report, not an order to swap the picture."""
    catalog = {str(row.get("assetId")): row for row in (rows or []) if row.get("assetId")}
    findings = []
    intentional = []
    heroes: list[str] = []
    previous = None
    frame_run: list[str] = []
    frame_states: list[tuple] = []
    frame_name = ""
    for scene in scenes:
        params = scene.get("params") if isinstance(scene.get("params"), dict) else {}
        scene_id = str(scene.get("sceneId") or scene.get("id") or "")
        template = str(scene.get("templateId") or scene.get("template") or "")
        kind = str(params.get("kind") or "")
        duration = float(scene.get("durationSeconds") or scene.get("duration") or 0)
        asset_id = str(params.get("assetId") or "")
        shown = [item for item in (asset_id, str(params.get("insetId") or "")) if item]
        continuity = bool(scene.get("continuity") or params.get("continuity"))
        beats = params.get("beats") if isinstance(params.get("beats"), list) else []
        if duration >= 16 and template in {"market_chart", "annotated_plate", "layered_plate", "brand_reveal", "designed_board", "kinetic_type"} and len(beats) < 2 and not params.get("holdIntent"):
            findings.append({
                "sceneId": scene_id,
                "code": "static_hold",
                "reason": "Cảnh dài mà không chia thành các nhịp nhìn khác nhau.",
            })
        for shown_id in shown:
            if shown_id in heroes:
                record = {"sceneId": scene_id, "code": "hero_reused", "assetId": shown_id, "reason": "Ảnh chủ đạo đã xuất hiện trước đó."}
                if continuity:
                    intentional.append({**record, "reason": "Dùng lại có chủ đích để câu chuyện còn nối."})
                else:
                    findings.append(record)
            heroes.append(shown_id)
        if previous and previous["asset"] and asset_id and previous["asset"] != asset_id:
            left = catalog.get(previous["asset"]) or {}
            right = catalog.get(asset_id) or {}
            page_left = str(left.get("sourcePage") or "")
            page_right = str(right.get("sourcePage") or "")
            if page_left and page_left == page_right:
                findings.append({"sceneId": scene_id, "code": "same_source", "reason": "Hai ảnh liên tiếp cùng một nguồn tệp."})
            digest_left = int(left.get("averageHash") or 0)
            digest_right = int(right.get("averageHash") or 0)
            if digest_left and digest_right and hamming(digest_left, digest_right) <= 8:
                findings.append({"sceneId": scene_id, "code": "near_duplicate", "reason": "Ảnh liên tiếp gần giống nhau."})
        state = (
            template,
            kind,
            asset_id,
            str(params.get("dataFile") or ""),
            params.get("start"),
            params.get("count"),
            tuple(params.get("indicators") or []),
            str((params.get("focus") or {}).get("label") if isinstance(params.get("focus"), dict) else ""),
        )
        if previous and state == previous["state"] and not continuity:
            findings.append({"sceneId": scene_id, "code": "same_state", "reason": "Cảnh sau giữ nguyên bố cục và trạng thái của cảnh trước."})
        if template == frame_name and template:
            frame_run.append(scene_id)
            frame_states.append(state)
        else:
            _close_frame_run(findings, frame_run, frame_states, frame_name, catalog, scenes)
            frame_run = [scene_id]
            frame_states = [state]
            frame_name = template
        previous = {"asset": asset_id, "state": state}
    _close_frame_run(findings, frame_run, frame_states, frame_name, catalog, scenes)
    return {"findings": findings, "intentional": intentional}


def _close_frame_run(findings: list[dict], run: list[str], states: list[tuple], template: str, catalog: dict, scenes: list[dict]) -> None:
    if len(run) < 3 or not template:
        return
    categories = []
    by_id = {str(scene.get("sceneId") or scene.get("id") or ""): scene for scene in scenes}
    for scene_id in run:
        params = (by_id.get(scene_id) or {}).get("params") if isinstance((by_id.get(scene_id) or {}).get("params"), dict) else {}
        asset_id = str((params or {}).get("assetId") or "")
        categories.append(str((catalog.get(asset_id) or {}).get("category") or asset_id or template))
    findings.append({
        "sceneId": run[0],
        "code": "same_frame",
        "template": template,
        "sceneIds": run,
        "subjectsDiffer": len(set(categories)) > 1,
        "statesDiffer": len(set(states)) > 1,
        "reason": "Cùng một kiểu khung xuất hiện liên tiếp.",
    })

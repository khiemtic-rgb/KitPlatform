"""NOVIXA commercial slice. Plans image-to-video jobs and a post title card. Does not invent a finished ad."""

from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

from kit_video.demo_board import demo_issues, render_demo_board
from kit_video.errors import ProductionError, issue
from kit_video.audio import media_duration
from kit_video.finish import write_brand_hold, write_still_hold, write_title_card
from kit_video.motion import PROVIDERS, build_request, credential_present
from kit_video.props import render_inventory_board, render_operations_board

VISUAL_CRITERIA = (
    "character_consistency",
    "expression_readability",
    "stylization_held",
    "environment_consistency",
    "product_integrity",
    "prop_data_ui_parity",
    "action_coherence",
    "motion_quality",
    "motion_serves_story",
    "camera_quality",
    "lighting_composition",
    "commercial_layout",
    "brand_correctness",
    "text_and_numbers_stable",
    "demo_data_declared",
    "ui_source_declared",
    "shelf_category_legible",
    "foreground_product_identity",
    "product_name_consistent",
    "pos_data_consistent",
    "demo_not_presented_as_real",
    "audio_quality",
    "technical_quality",
)


def produce_commercial(project_dir: Path, repo_root: Path) -> dict:
    spec_path = project_dir / "commercial.json"
    if not spec_path.is_file():
        raise ProductionError("FAIL", [issue("commercial", str(spec_path), "Không thấy commercial.json.")])
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    _require_spec(spec)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    dest = _output_dir(project_dir, spec, stamp)
    dest.mkdir(parents=True)
    blockers = _blockers(spec, project_dir, repo_root)
    demo_ok = not any(item["code"].startswith("DEMO_") for item in blockers)
    scenes = [_scene_row(spec, scene, demo_ok) for scene in spec["scenes"]]
    end_card = _end_card(dest, spec)
    demo = _demo(dest, spec) if demo_ok else None
    record = {
        "schemaVersion": "kit-video-production-record/1",
        "projectId": spec["projectId"],
        "createdAt": stamp,
        "status": "BLOCKED",
        "gate": _gate(blockers),
        "scriptVersion": spec["brief"]["version"],
        "reference": {
            "video": spec["reference"]["video"],
            "present": (repo_root / spec["reference"]["video"]).is_file(),
        },
        "provider": {
            "id": spec["provider"]["id"],
            "model": spec["provider"].get("model") or PROVIDERS[spec["provider"]["id"]]["model"],
            "mode": spec["provider"].get("mode") or "image_to_video",
            "secretEnv": PROVIDERS[spec["provider"]["id"]]["secretEnv"],
            "credentialPresent": credential_present(PROVIDERS[spec["provider"]["id"]]["secretEnv"]),
            "requestSent": False,
        },
        "visualBible": spec.get("visualBible"),
        "assetGroups": spec.get("assetGroups") or [],
        "assets": _assets(spec, project_dir, repo_root),
        "demo": demo,
        "scenes": scenes,
        "timeline": [{"sceneId": row["sceneId"], "order": row["order"], "clip": row.get("clip")} for row in scenes],
        "post": {
            "endCard": str(end_card),
            "role": "post_graphic",
            "lines": spec["endCard"]["lines"],
            "isFinalCommercial": False,
        },
        "finalVideo": None,
        "blockers": blockers,
        "visualReview": "VISUAL_REVIEW_REQUIRED",
        "visualChecklist": [{"id": name, "status": "VISUAL_REVIEW_REQUIRED"} for name in VISUAL_CRITERIA],
        "revision": spec.get("revision") or [],
        "next": "Duyệt visual bible và keyframe trước. Có APPROVED và credential thì mới gửi một cảnh. Không ghép thẻ chữ hoặc mockup thành quảng cáo.",
    }
    (dest / "production-record.json").write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")
    return {"status": record["status"], "dir": str(dest), "blockers": record["blockers"], "record": record}


def export_graphics_holds(project_dir: Path, repo_root: Path, dest: Path | None = None) -> dict:
    """Encode the data and logo scenes only. Character action is not included."""
    spec = json.loads((project_dir / "commercial.json").read_text(encoding="utf-8"))
    plan = json.loads((project_dir / spec["shotPlan"]).read_text(encoding="utf-8"))
    assets = {row["assetId"]: row for row in json.loads((project_dir / "manifest.json").read_text(encoding="utf-8"))["assets"]}
    if dest is None:
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        root = Path(spec["output"].get("outputRoot") or "../../outputs")
        if not root.is_absolute():
            root = (project_dir / root).resolve()
        dest = root / spec["projectId"] / f"{stamp}-graphics-holds"
    else:
        stamp = dest.name
    dest.mkdir(parents=True, exist_ok=True)
    rows = []
    omitted = []
    for scene in plan["scenes"]:
        method = (scene.get("params") or {}).get("productionMethod")
        if method != "post_graphics":
            omitted.append(scene["sceneId"])
            continue
        if scene["sceneId"] == "s06-logo":
            logo = _resolve(project_dir, repo_root, assets["logo"]["path"])
            clip = write_brand_hold(
                logo,
                "Hàng tồn kho cũng là vốn.",
                dest / "s06-logo.mp4",
                spec["endCard"]["fontRegular"],
                seconds=float(scene["durationSeconds"]),
                frame_rate=int(spec["output"].get("frameRate") or 30),
            )
            source = assets["logo"]["path"]
        else:
            source = "demoData"
            board = _data_board(spec, scene["sceneId"])
            png = dest / f"{scene['sceneId']}.png"
            board.save(png)
            clip = write_still_hold(
                png,
                dest / f"{scene['sceneId']}.mp4",
                float(scene["durationSeconds"]),
                int(spec["output"].get("frameRate") or 30),
            )
        rows.append({
            "sceneId": scene["sceneId"],
            "file": str(clip),
            "source": source,
            "plannedSeconds": scene["durationSeconds"],
            "measuredSeconds": round(media_duration(clip), 3),
            "method": "still-hold",
            "motionClaim": False,
        })
    record = {
        "schemaVersion": "kit-video-production-record/1",
        "projectId": spec["projectId"],
        "createdAt": stamp,
        "kind": "graphics-holds",
        "status": "VISUAL_REVIEW_REQUIRED",
        "isFinalCommercial": False,
        "finalVideo": None,
        "requestSent": False,
        "scenes": rows,
        "omitted": omitted,
        "reason": "Chỉ các cảnh số liệu và logo. Không giả lập cảnh lấy hộp.",
    }
    (dest / "production-record.json").write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")
    return record


def _data_board(spec: dict, scene_id: str):
    card = spec["endCard"]
    regular = str(card["fontRegular"])
    bold = str(card["fontBold"])
    if scene_id == "s05-inventory":
        return render_inventory_board(spec, regular, bold)
    return render_operations_board(spec, regular, bold)


def assemble_preview(clips: dict) -> dict:
    """Do not concat a film while the character scenes are absent."""
    needed = ("s01-pharmacy", "s02-take-box", "s03-label", "s04-operations", "s05-inventory", "s06-logo")
    missing = [scene_id for scene_id in needed if not Path(str(clips.get(scene_id) or "")).is_file()]
    return {
        "status": "BLOCKED" if missing else "READY_TO_ASSEMBLE",
        "finalVideo": None,
        "requestSent": False,
        "missing": missing,
        "reason": "Chưa ghép. Thiếu cảnh nhân vật thì ba cảnh đồ họa không thành phim.",
    }


def replace_scene(record_path: Path, scene_id: str, clip_path: Path) -> dict:
    """Attach one real clip. The film stays unapproved until every generate scene has a clip and a person reviews it."""
    record = json.loads(record_path.read_text(encoding="utf-8"))
    if not clip_path.is_file():
        raise ProductionError("FAIL", [issue("clip_missing", scene_id, f"Không thấy clip {clip_path}.")])
    found = False
    for row in record["scenes"]:
        if row["sceneId"] != scene_id:
            continue
        if row["kind"] != "generate":
            raise ProductionError("FAIL", [issue("scene_kind", scene_id, "Chỉ thay cảnh generate.")])
        row["status"] = "CLIP_ATTACHED"
        row["clip"] = str(clip_path)
        row["visualReview"] = "VISUAL_REVIEW_REQUIRED"
        found = True
    if not found:
        raise ProductionError("FAIL", [issue("scene", scene_id, "Không có cảnh này trong biên bản.")])
    generate = [row for row in record["scenes"] if row["kind"] == "generate"]
    record["finalVideo"] = None
    record["visualReview"] = "VISUAL_REVIEW_REQUIRED"
    record["status"] = "BLOCKED" if any(row.get("status") != "CLIP_ATTACHED" for row in generate) else "VISUAL_REVIEW_REQUIRED"
    record["post"]["isFinalCommercial"] = False
    record_path.write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")
    return record


def _require_spec(spec: dict) -> None:
    for key in ("projectId", "brief", "reference", "provider", "scenes", "endCard", "output"):
        if key not in spec:
            raise ProductionError("FAIL", [issue("commercial", key, f"commercial.json thiếu {key}.")])
    if spec["provider"].get("id") not in PROVIDERS:
        raise ProductionError("BLOCKED", [issue("provider", "provider.id", "Provider chưa có adapter.")])
    if not spec["scenes"]:
        raise ProductionError("FAIL", [issue("scenes", "scenes", "Cần ít nhất một cảnh.")])


def _output_dir(project_dir: Path, spec: dict, stamp: str) -> Path:
    root = Path(spec["output"].get("outputRoot") or "../../outputs")
    if not root.is_absolute():
        root = (project_dir / root).resolve()
    return root / spec["projectId"] / f"{stamp}-produce"


def _blockers(spec: dict, project_dir: Path, repo_root: Path) -> list[dict]:
    rows = []
    reference = repo_root / spec["reference"]["video"]
    if not reference.is_file() and spec["reference"].get("required", True):
        rows.append({
            "code": "REFERENCE_MISSING",
            "message": f"Không thấy video tham chiếu {spec['reference']['video']}. Tiêu chuẩn chữ trong brief vẫn là mục tiêu. Không suy chất lượng từ logo.",
        })
    provider = PROVIDERS[spec["provider"]["id"]]
    if not credential_present(provider["secretEnv"]):
        rows.append({
            "code": "CREDENTIAL_MISSING",
            "message": f"Biến {provider['secretEnv']} trống. Không gọi {spec['provider']['id']}.",
        })
    rows.extend(demo_issues(spec))
    missing = []
    waiting = []
    for scene in spec["scenes"]:
        if scene.get("kind") != "generate":
            continue
        path = _keyframe_path(scene)
        if not path or not _resolve(project_dir, repo_root, path).is_file():
            missing.append(scene["sceneId"])
        elif _keyframe_approval(scene) != "APPROVED":
            waiting.append(scene["sceneId"])
    if missing:
        rows.append({
            "code": "KEYFRAME_MISSING",
            "message": "Chưa có file keyframe cho: " + ", ".join(missing) + ".",
        })
    if waiting:
        rows.append({
            "code": "KEYFRAME_NOT_APPROVED",
            "message": "Keyframe đang chờ người duyệt, chưa được gửi: " + ", ".join(waiting) + ".",
        })
    return rows


def _gate(blockers: list[dict]) -> str:
    codes = {item["code"] for item in blockers}
    if "KEYFRAME_MISSING" in codes:
        return "A"
    if "KEYFRAME_NOT_APPROVED" in codes:
        return "B"
    if any(code.startswith("DEMO_") for code in codes):
        return "B"
    if "CREDENTIAL_MISSING" in codes or "REFERENCE_MISSING" in codes:
        return "C"
    return "C"


def _resolve(project_dir: Path, repo_root: Path, raw: str) -> Path:
    path = Path(raw)
    if path.is_absolute():
        return path
    local = project_dir / path
    if local.is_file():
        return local
    return repo_root / path


def _keyframe_path(scene: dict) -> str:
    value = scene.get("keyframe")
    if isinstance(value, str):
        return value
    if isinstance(value, dict):
        return str(value.get("path") or "")
    return ""


def _keyframe_approval(scene: dict) -> str:
    value = scene.get("keyframe")
    if isinstance(value, dict):
        return str(value.get("approval") or "AWAITING_REVIEW")
    return "AWAITING_REVIEW" if isinstance(value, str) and value else ""


def _scene_row(spec: dict, scene: dict, demo_ok: bool = True) -> dict:
    kind = scene.get("kind")
    row = {
        "sceneId": scene["sceneId"],
        "order": scene["order"],
        "kind": kind,
        "action": scene.get("action") or "",
        "prompt": scene.get("prompt") or "",
        "keyframe": scene.get("keyframe"),
        "clip": None,
        "visualReview": "VISUAL_REVIEW_REQUIRED",
    }
    if kind == "post":
        row["status"] = "POST_GRAPHIC"
        return row
    provider = PROVIDERS[spec["provider"]["id"]]
    path = _keyframe_path(scene)
    approved = _keyframe_approval(scene) == "APPROVED"
    row["keyframe"] = path or None
    row["keyframeApproval"] = _keyframe_approval(scene) or None
    row["request"] = None
    if path and approved and demo_ok and credential_present(provider["secretEnv"]):
        row["request"] = build_request(spec["provider"], scene, path)
        row["status"] = "READY_TO_SUBMIT"
    else:
        row["status"] = "BLOCKED"
    row["requestSent"] = False
    return row


def _assets(spec: dict, project_dir: Path, repo_root: Path) -> list[dict]:
    rows = []
    for asset in spec.get("assets") or []:
        raw = str(asset.get("path") or "")
        path = _resolve(project_dir, repo_root, raw) if raw else project_dir
        rows.append({
            "assetId": asset.get("assetId"),
            "role": asset.get("role"),
            "source": asset.get("source"),
            "approval": asset.get("approval"),
            "path": str(path),
            "present": path.is_file(),
        })
    return rows


def _demo(dest: Path, spec: dict) -> dict | None:
    data = spec.get("demoData")
    if not data:
        return None
    palette = spec.get("palette") or {}
    card = spec["endCard"]
    output = spec["output"]
    board = render_demo_board(
        dest / "demo-board.png",
        data,
        str(card["fontRegular"]),
        str(card["fontBold"]),
        (int(output["width"]), int(output["height"])),
        tuple(palette.get("ink") or [0, 32, 96]),
        tuple(palette.get("accent") or [0, 144, 224]),
        tuple(palette.get("paper") or [255, 250, 244]),
        tuple(palette.get("mint") or [168, 196, 176]),
    )
    return {
        "label": data.get("label"),
        "source": data.get("source"),
        "isProductScreenshot": data.get("source") == "product_screenshot",
        "board": str(dest / "demo-board.png"),
        "lines": board,
        "role": "controlled_mockup" if data.get("source") == "mockup" else "product_ui",
    }


def _end_card(dest: Path, spec: dict) -> Path:
    card = spec["endCard"]
    output = spec["output"]
    palette = spec.get("palette") or {}
    return write_title_card(
        dest / "end-card.mp4",
        list(card["lines"]),
        str(card["fontRegular"]),
        str(card["fontBold"]),
        (int(output["width"]), int(output["height"])),
        float(card.get("seconds") or 3),
        int(output.get("frameRate") or 30),
        tuple(palette.get("ink") or [0, 32, 96]),
        tuple(palette.get("accent") or [0, 144, 224]),
        tuple(palette.get("paper") or [255, 250, 244]),
    )

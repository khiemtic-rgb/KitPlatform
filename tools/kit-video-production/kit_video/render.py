"""Render selected scenes from a manifest into a new version folder."""

from __future__ import annotations

import hashlib
import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path

import kit_video
from kit_video.audio import concat_audio, mix_scene, scene_duration, snap_frames
from kit_video.canvas import FrameContext
from kit_video.errors import ProductionError, issue
from kit_video.planning import select_scene_asset
from kit_video.templates import TEMPLATES, draw_scene
from kit_video.still import confirm_file, inspect_asset, is_picture
from kit_video.validate import load_project, resolve_path, validate_project


def _style_preview_blocks(errors: list[dict], scenes: list, assets: list, scene_ids: list[str] | None, allow_style_proof: bool, preview_only: bool):
    """Separate a style-proof preview from the project gate.

    Returns None when the call is a project render or includes an approved scene.
    semantic_unreviewed is noted and does not block the preview. Every other error
    on a scene being previewed still blocks. Another scene's error blocks only
    when that scene is part of this preview.
    """
    if not preview_only or not allow_style_proof:
        return None
    known = {scene.get("sceneId") for scene in scenes if isinstance(scene, dict)}
    catalog = {row.get("assetId") for row in assets if isinstance(row, dict)}
    if scene_ids:
        wanted = set(scene_ids)
        selected = [scene for scene in scenes if isinstance(scene, dict) and scene.get("sceneId") in wanted]
        if not selected or any(scene.get("approvalStatus") == "approved" for scene in selected):
            return None
        ids = {scene.get("sceneId") for scene in selected}
        referenced = {asset_id for scene in selected for asset_id in (scene.get("assets") or [])}
        blocking = []
        noted = []
        for err in errors:
            path = err.get("path")
            if path in ids:
                if err.get("code") == "semantic_unreviewed":
                    noted.append(err)
                else:
                    blocking.append(err)
            elif path in known or path in catalog:
                if path in referenced:
                    blocking.append(err)
            else:
                blocking.append(err)
        return blocking, noted
    rendered = [scene for scene in scenes if isinstance(scene, dict) and scene.get("approvalStatus") in {"approved", "style_proof"}]
    if any(scene.get("approvalStatus") == "approved" for scene in rendered):
        return None
    noted = [err for err in errors if err.get("code") == "semantic_unreviewed"]
    blocking = [err for err in errors if err.get("code") != "semantic_unreviewed"]
    return blocking, noted


def _approved_integrity(scenes: list, library: dict, project_dir: Path, repo_root: Path) -> list[dict]:
    """Hash and file checks for approved scenes, reported with the rest of validation."""
    found = []
    for scene in scenes:
        if not isinstance(scene, dict) or scene.get("approvalStatus") != "approved":
            continue
        choice = select_scene_asset(scene, library, project_dir, repo_root)
        if choice["status"] == "blocked" and choice.get("code") in {"hash_unrecorded", "asset_hash", "asset_missing"}:
            found.append(issue(choice["code"], scene.get("sceneId") or "?", choice.get("reason") or "Asset chưa đủ toàn vẹn để render."))
    return found


def render_project(
    project_dir: Path,
    repo_root: Path,
    scene_ids: list[str] | None = None,
    label: str = "cut",
    allow_style_proof: bool = False,
    preview_only: bool = False,
) -> Path:
    project, load_errors = load_project(project_dir)
    if project is None or load_errors:
        raise ProductionError("FAIL", load_errors or [issue("project", str(project_dir), "Không mở được dự án.")])
    errors, warnings = validate_project(project)
    integrity = _approved_integrity(project["shotPlan"]["scenes"], {row["assetId"]: row for row in project["manifest"].get("assets") or [] if isinstance(row, dict)}, project["dir"], repo_root)
    errors = list(errors) + integrity
    scoped = _style_preview_blocks(errors, project["shotPlan"]["scenes"], project["manifest"].get("assets") or [], scene_ids, allow_style_proof, preview_only)
    if scoped is None:
        if errors:
            raise ProductionError("FAIL", errors)
    else:
        blocking, noted = scoped
        warnings = list(warnings) + noted
        if blocking:
            raise ProductionError("FAIL", blocking)
    manifest = project["manifest"]
    scenes = _select_scenes(project["shotPlan"]["scenes"], scene_ids, allow_style_proof)
    _check_dependencies(manifest, scenes, project["dir"], repo_root)
    library = {row["assetId"]: row for row in manifest.get("assets") or [] if isinstance(row, dict)}
    for scene in scenes:
        if scene.get("approvalStatus") != "approved":
            continue
        choice = select_scene_asset(scene, library, project["dir"], repo_root)
        if choice["status"] == "blocked" and choice.get("code") in {"hash_unrecorded", "asset_hash", "asset_missing"}:
            raise ProductionError("BLOCKED", [issue(choice["code"], scene["sceneId"], choice.get("reason") or "Asset chưa đủ toàn vẹn để render.")])
    fmt = manifest["format"]
    frame_rate = float(fmt["frameRate"])
    width = int(fmt["width"])
    height = int(fmt["height"])
    prepared = []
    for scene in scenes:
        voice = _voice_file(manifest, scene, project["dir"], repo_root)
        duration = scene_duration(scene, voice)
        frames, snapped = snap_frames(duration, frame_rate)
        prepared.append({"scene": scene, "voice": voice, "frames": frames, "duration": snapped})

    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    output_root = _output_root(manifest, project["dir"])
    dest = output_root / manifest["projectId"] / f"{stamp}-{label}"
    if dest.exists():
        dest = output_root / manifest["projectId"] / f"{stamp}-{label}-b"
    dest.mkdir(parents=True)
    cache: dict = {}
    previews = []
    if preview_only:
        for item in prepared:
            previews.extend(_preview_scene(item, manifest, width, height, dest, cache, repo_root, project["dir"]))
        _write_log(dest, manifest, project, prepared, None, warnings, preview=True, previews=previews, repo_root=repo_root)
        return dest

    audio_parts = []
    for index, item in enumerate(prepared):
        part = dest / f"scene-{index}.wav"
        _mix_item(item, manifest, project["dir"], repo_root, part)
        audio_parts.append(part)
    master = dest / "audio-mix.wav"
    if len(audio_parts) == 1:
        master = audio_parts[0]
    else:
        concat_audio(audio_parts, master)

    video = dest / "video.mp4"
    total_frames = sum(item["frames"] for item in prepared)
    total_duration = total_frames / frame_rate
    _encode(video, master, prepared, manifest, width, height, frame_rate, total_duration, cache, repo_root, project["dir"], dest)
    for item in prepared:
        previews.extend(_saved_names(item, dest))
    _write_layout(dest, cache)
    _write_log(dest, manifest, project, prepared, video, warnings, preview=False, previews=previews, repo_root=repo_root)
    return dest


def _select_scenes(scenes: list[dict], scene_ids: list[str] | None, allow_style_proof: bool) -> list[dict]:
    known = {scene["sceneId"] for scene in scenes}
    wanted = list(scene_ids or [])
    missing = [scene_id for scene_id in wanted if scene_id not in known]
    if missing:
        raise ProductionError("FAIL", [issue("scene", ",".join(missing), "Không có sceneId này trong shot plan.")])
    chosen = []
    blocked = []
    for scene in scenes:
        if wanted and scene["sceneId"] not in wanted:
            continue
        status = scene["approvalStatus"]
        if status == "approved" or (status == "style_proof" and allow_style_proof):
            chosen.append(scene)
            continue
        blocked.append(issue(
            "not_approved",
            scene["sceneId"],
            "Cảnh chưa được duyệt để dựng. Bản đề xuất không tự động thành kịch bản đã khóa.",
        ))
    if blocked:
        raise ProductionError("BLOCKED", blocked)
    if not chosen:
        raise ProductionError("BLOCKED", [issue("scene", "scenes", "Không có cảnh nào được phép dựng.")])
    return chosen


def _check_dependencies(manifest: dict, scenes: list[dict], project_dir: Path, repo_root: Path) -> None:
    errors = []
    assets = {row["assetId"]: row for row in manifest["assets"]}
    allow_placeholders = manifest["render"]["allowPlaceholders"]
    for scene in scenes:
        for asset_id in scene["assets"]:
            row = assets[asset_id]
            _asset_renderable(errors, row, allow_placeholders, scene["approvalStatus"], project_dir, repo_root)
        for cue in scene["audio"]["sfx"]:
            row = assets[cue["assetId"]]
            _asset_renderable(errors, row, allow_placeholders, scene["approvalStatus"], project_dir, repo_root)
            path = resolve_path(row, project_dir, repo_root)
            if path is None or not path.is_file():
                errors.append(issue("asset_missing", row["assetId"], f"Không thấy file SFX {row['assetId']}."))
        voice_id = scene["audio"].get("voiceTrackId")
        if voice_id:
            track = next(item for item in manifest["voice"]["tracks"] if item["trackId"] == voice_id)
            path = resolve_path(track, project_dir, repo_root)
            if path is None or not path.is_file():
                errors.append(issue("voice_missing", voice_id, f"Không thấy file lời đọc {voice_id}."))
            if track.get("approval") == "placeholder" and not allow_placeholders:
                errors.append(issue("placeholder", voice_id, "Placeholder bị chặn vì allowPlaceholders=false."))
    for key in ("fontRegular", "fontBold"):
        path = Path(manifest["visualStyle"]["typography"][key])
        if not path.is_file():
            errors.append(issue("font_missing", str(path), f"Không thấy font {path.name}."))
    if manifest["audioProfile"]["musicPolicy"] == "required":
        errors.append(issue("music_missing", "audioProfile.musicPolicy", "Dự án bắt buộc nhạc nhưng V1 chưa gắn file nhạc đã cấp quyền."))
    for scene in scenes:
        if scene["templateId"] not in TEMPLATES:
            errors.append(issue("template_missing", scene["sceneId"], f"Không có template {scene['templateId']}."))
    if errors:
        raise ProductionError("BLOCKED", errors)


def _asset_renderable(errors: list[dict], row: dict, allow_placeholders: bool, scene_status: str, project_dir: Path, repo_root: Path) -> None:
    approval = row["approval"]
    if approval in {"missing", "rejected"}:
        errors.append(issue("asset_missing", row["assetId"], f"Asset {row['assetId']} không dùng được ({approval})."))
        return
    if approval == "placeholder" and not allow_placeholders:
        errors.append(issue("placeholder", row["assetId"], f"Asset {row['assetId']} là placeholder và dự án không cho phép."))
    if approval == "placeholder" and row.get("role") == "product_ui":
        errors.append(issue("product_ui", row["assetId"], "Không dùng placeholder làm UI sản phẩm."))
    if row.get("role") == "product_ui" and approval != "approved":
        errors.append(issue("product_ui", row["assetId"], "UI sản phẩm chưa được duyệt."))
    path = resolve_path(row, project_dir, repo_root) if row.get("path") else None
    if row.get("path") and row.get("kind") != "vector_generated":
        if path is None or not path.is_file():
            errors.append(issue("asset_missing", row["assetId"], f"Không thấy file của asset {row['assetId']}."))
    if is_picture(row):
        decision = inspect_asset(row)
        if not decision["allowed"]:
            errors.append(issue("asset_not_cleared", row["assetId"], decision["reason"]))
        elif path is not None and path.is_file():
            check = confirm_file(row, path)
            if check["match"] is False:
                errors.append(issue("asset_hash", row["assetId"], "File không khớp sha256 đã ghi. Không dùng bản khác."))
    if scene_status == "approved" and approval == "proposed" and row.get("role") == "product_ui":
        errors.append(issue("product_ui", row["assetId"], "UI đề xuất không được dựng trong cảnh đã duyệt."))


def _voice_file(manifest: dict, scene: dict, project_dir: Path, repo_root: Path) -> Path | None:
    voice_id = scene["audio"].get("voiceTrackId")
    if not voice_id:
        return None
    track = next(item for item in manifest["voice"]["tracks"] if item["trackId"] == voice_id)
    return resolve_path(track, project_dir, repo_root)


def _mix_item(item: dict, manifest: dict, project_dir: Path, repo_root: Path, dest: Path) -> None:
    scene = item["scene"]
    assets = {row["assetId"]: row for row in manifest["assets"]}
    profile = manifest["audioProfile"]
    cues = []
    for cue in scene["audio"]["sfx"]:
        path = resolve_path(assets[cue["assetId"]], project_dir, repo_root)
        cues.append((path, float(cue["atSeconds"]), float(cue.get("gain", 1))))
    mix_scene(
        dest,
        item["duration"],
        item["voice"],
        float(scene.get("voiceLeadSeconds") or 0),
        cues,
        float(profile["voiceGain"]),
        float(profile["sfxGain"]),
        bool(profile["ducking"]),
    )


def _encode(video, master, prepared, manifest, width, height, frame_rate, total_duration, cache, repo_root, project_dir, dest) -> None:
    render = manifest["render"]
    log_path = dest / "ffmpeg-encode.log"
    command = [
        "ffmpeg", "-y",
        "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{width}x{height}", "-r", str(frame_rate), "-i", "-",
        "-i", str(master),
        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-profile:v", "high", "-crf", str(render["crf"]),
        "-preset", str(render.get("preset", "veryfast")),
        "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2",
        "-t", f"{total_duration:.6f}", "-movflags", "+faststart", str(video),
    ]
    with log_path.open("w", encoding="utf-8") as log_file:
        process = subprocess.Popen(command, stdin=subprocess.PIPE, stderr=log_file)
        assert process.stdin is not None
        try:
            done = 0
            for item in prepared:
                scene = item["scene"]
                for index in range(item["frames"]):
                    now = index / frame_rate
                    frame = _frame(scene, manifest, width, height, now, item["duration"], cache, repo_root, project_dir)
                    process.stdin.write(frame.convert("RGB").tobytes())
                    _maybe_save_preview(scene, now, item["duration"], frame_rate, frame, dest)
                    done += 1
                    if done % 48 == 0:
                        print(f"frame {done}", flush=True)
        finally:
            process.stdin.close()
            code = process.wait()
    if code != 0:
        detail = log_path.read_text(encoding="utf-8", errors="replace")[-2000:]
        raise ProductionError("FAIL", [issue("ffmpeg", str(video), detail or "ffmpeg encode failed")])


def _frame(scene, manifest, width, height, now, duration, cache, repo_root, project_dir) -> object:
    ctx = FrameContext(scene, manifest["visualStyle"], width, height, now, duration, cache)
    assets = {row["assetId"]: row for row in manifest["assets"]}
    return draw_scene(ctx, assets, repo_root, project_dir)


def _maybe_save_preview(scene: dict, now: float, duration: float, frame_rate: float, frame, dest: Path) -> None:
    fractions = scene.get("previewFractions") or []
    folder = dest / "frames"
    for fraction in fractions:
        target = float(fraction) * duration
        if abs(now - target) <= (0.51 / frame_rate):
            folder.mkdir(parents=True, exist_ok=True)
            name = f"{scene['sceneId']}-f{float(fraction):.2f}.png"
            path = folder / name
            if not path.exists():
                frame.convert("RGB").save(path)


def _preview_scene(item, manifest, width, height, dest, cache, repo_root, project_dir) -> list[str]:
    scene = item["scene"]
    fractions = scene.get("previewFractions") or [0.15, 0.4, 0.7, 0.9]
    folder = dest / "frames"
    folder.mkdir(parents=True, exist_ok=True)
    names = []
    for fraction in fractions:
        now = min(item["duration"] - (1 / float(manifest["format"]["frameRate"])), float(fraction) * item["duration"])
        frame = _frame(scene, manifest, width, height, max(0, now), item["duration"], cache, repo_root, project_dir)
        name = f"{scene['sceneId']}-f{float(fraction):.2f}.png"
        frame.convert("RGB").save(folder / name)
        names.append(name)
        print(name, flush=True)
    _write_layout(dest, cache)
    _export_ad_plate(dest, cache, manifest)
    return names


def _export_ad_plate(dest: Path, cache: dict, manifest: dict) -> None:
    """Write a 16:9 plate where a native-size screenshot is pasted, plus the untouched source layer."""
    rows = [row for row in (cache.get("pictureSources") or []) if row.get("path")]
    if not rows:
        return
    row = rows[-1]
    source_path = Path(str(row["path"]))
    if not source_path.is_file():
        return
    from PIL import Image, ImageDraw, ImageFont

    from kit_video.design import paint_software_ad

    source = Image.open(source_path).convert("RGBA")
    source.load()
    palette = manifest["visualStyle"]["palette"]
    ink = tuple(int(channel) for channel in palette["ink"])
    accent = tuple(int(channel) for channel in palette["accent"])
    paper = tuple(int(channel) for channel in palette["paper"])
    plate = Image.new("RGBA", (1920, 1080), (*ink, 255))
    layout = paint_software_ad(plate, source, ink, accent)
    box = layout["box"]
    label = str(row.get("demoLabel") or "")
    font_path = (manifest.get("visualStyle") or {}).get("typography", {}).get("fontBold")
    label_box = None
    if label and isinstance(font_path, str) and Path(font_path).is_file():
        font = ImageFont.truetype(font_path, 28)
        measure = font.getbbox(label)
        text_w = measure[2] - measure[0]
        text_h = measure[3] - measure[1]
        origin = (box[2] - text_w, max(48, box[1] - text_h - 20))
        ImageDraw.Draw(plate).text(origin, label, font=font, fill=(*paper, 255))
        label_box = [origin[0], origin[1], origin[0] + text_w, origin[1] + text_h]
    layers = dest / "layers"
    layers.mkdir(exist_ok=True)
    (layers / "screenshot.png").write_bytes(source_path.read_bytes())
    ground = Image.new("RGBA", plate.size, (*ink, 255))
    paint_software_ad(ground, source, ink, accent, paste=False)
    ground.convert("RGB").save(layers / "background.png")
    plate.convert("RGB").save(layers / "plate-1920x1080.png")
    headline_right = (label_box[0] - 28) if label_box else box[2]
    placement = {
        "source": str(source_path),
        "sha256": row.get("sha256"),
        "visualSource": row.get("visualSource"),
        "sourceSize": [source.width, source.height],
        "plateSize": [1920, 1080],
        "screenshotBox": [int(box[0]), int(box[1]), int(box[2]), int(box[3])],
        "placement": "native" if layout["native"] else "scaled",
        "headline": [box[0], 48, max(box[0] + 8, headline_right), max(49, box[1] - 16)],
        "demoLabel": label,
        "labelBox": label_box,
        "screenshotLayer": "layers/screenshot.png",
    }
    (layers / "placement.json").write_text(json.dumps(placement, ensure_ascii=False, indent=2), encoding="utf-8")


def _saved_names(item: dict, dest: Path) -> list[str]:
    folder = dest / "frames"
    if not folder.exists():
        return []
    return sorted(path.name for path in folder.glob(f"{item['scene']['sceneId']}-*.png"))


def _write_layout(dest: Path, cache: dict) -> None:
    payload = {
        "texts": sorted(cache.get("texts") or []),
        "notices": cache.get("notices") or [],
        "samples": cache.get("samples") or {},
        "violations": cache.get("violations") or [],
        "drawnAssets": cache.get("drawn_assets") or [],
        "contract": cache.get("contract") or {},
        "layoutDecisions": cache.get("layout_decisions") or [],
        "visualDecisions": cache.get("visual_decisions") or [],
        "compositionDecisions": cache.get("composition_decisions") or [],
        "visualPlans": cache.get("visual_plans") or [],
        "methodDecisions": cache.get("method_decisions") or [],
        "assetBindings": cache.get("assetBindings") or [],
        "pictureSources": cache.get("pictureSources") or [],
        "deferredOnScreenText": cache.get("deferredOnScreenText") or [],
        "artDirection": cache.get("artDirection") or {},
    }
    (dest / "layout.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def _write_log(dest, manifest, project, prepared, video, warnings, preview: bool, previews: list[str], repo_root: Path) -> None:
    digest = hashlib.sha256()
    digest.update((project["dir"] / "manifest.json").read_bytes())
    digest.update((project["dir"] / manifest["shotPlan"]).read_bytes())
    payload = {
        "projectId": manifest["projectId"],
        "engineVersion": kit_video.__version__,
        "brandId": (manifest.get("brand") or {}).get("id"),
        "styleProfileId": (manifest.get("visualStyle") or {}).get("profileId"),
        "scriptVersion": (manifest.get("script") or {}).get("version"),
        "shotPlan": manifest.get("shotPlan"),
        "preview": preview,
        "video": str(video) if video else None,
        "sha256ManifestAndShotPlan": digest.hexdigest(),
        "warnings": warnings,
        "scenes": [
            {"sceneId": item["scene"]["sceneId"], "frames": item["frames"], "durationSeconds": item["duration"]}
            for item in prepared
        ],
        "format": manifest["format"],
        "render": {key: manifest["render"][key] for key in ("videoCodec", "audioCodec", "crf", "preset") if key in manifest["render"]},
        "placeholders": [
            row["assetId"] for row in manifest["assets"]
            if row["approval"] == "placeholder" and row["assetId"] in _used_ids(prepared)
        ],
        "inputs": _input_records(manifest, project["dir"], repo_root, prepared),
        "previews": previews,
    }
    (dest / "render-log.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def _file_sha256(path: Path | None) -> str | None:
    if path is None or not path.is_file():
        return None
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _input_records(manifest: dict, project_dir: Path, repo_root: Path, prepared: list[dict]) -> list[dict]:
    from kit_video.validate import resolve_path

    used = _used_ids(prepared)
    records = []
    for row in manifest.get("assets") or []:
        if row.get("assetId") not in used:
            continue
        path = resolve_path(row, project_dir, repo_root)
        clearance = inspect_asset(row) if is_picture(row) else None
        record = {
            "kind": "asset",
            "id": row.get("assetId"),
            "role": row.get("role"),
            "source": row.get("source"),
            "usageRights": row.get("usageRights"),
            "approval": row.get("approval"),
            "clearance": "allowed" if clearance and clearance["allowed"] else ("refused" if clearance else "not_picture"),
            "path": str(path) if path else None,
            "sha256": _file_sha256(path),
        }
        if row.get("visualSource"):
            record["visualSource"] = row.get("visualSource")
        records.append(record)
    seen: set[str] = set()
    for item in prepared:
        voice_id = item["scene"]["audio"].get("voiceTrackId")
        if not voice_id or voice_id in seen:
            continue
        seen.add(voice_id)
        track = next(row for row in manifest["voice"]["tracks"] if row["trackId"] == voice_id)
        path = resolve_path(track, project_dir, repo_root)
        records.append({
            "kind": "voice",
            "id": voice_id,
            "role": "voice",
            "approval": track.get("approval"),
            "path": str(path) if path else None,
            "sha256": _file_sha256(path),
        })
    fonts = (manifest.get("visualStyle") or {}).get("typography") or {}
    for key in ("fontRegular", "fontBold"):
        raw = fonts.get(key)
        path = Path(raw) if isinstance(raw, str) and raw else None
        records.append({
            "kind": "font",
            "id": key,
            "role": "typography",
            "approval": None,
            "path": str(path) if path else None,
            "sha256": _file_sha256(path),
        })
    return records


def _used_ids(prepared: list[dict]) -> set[str]:
    used = set()
    for item in prepared:
        used.update(item["scene"]["assets"])
        for cue in item["scene"]["audio"]["sfx"]:
            used.add(cue["assetId"])
    return used


def _output_root(manifest: dict, project_dir: Path) -> Path:
    raw = Path(manifest["render"]["outputRoot"])
    if not raw.is_absolute():
        raw = (project_dir / raw).resolve()
    return raw

"""Validate a project manifest and shot plan.

The JSON files under schemas/ document the same contract. This module is the
check that actually runs. Nothing here assumes a brand, a scene count, or a
runtime.
"""

from __future__ import annotations

import json
from pathlib import Path

from kit_video.contract import DesignContract
from kit_video.composition import art_profile, scene_intent
from kit_video.planning import ASSET_ERROR_CODES, QUALITY_ERROR_CODES, production_gate, scene_findings, visual_plan
from kit_video.still import goods_source, inspect_asset, is_picture, referenced_ids
from kit_video.visual_quality import assess_composition, assess_film, assess_intent, assess_scene
from kit_video.errors import issue

SCHEMA_PROJECT = "kit-video-project/1"
SCHEMA_SHOT = "kit-video-shot-plan/1"
STAGES = (
    "brief",
    "script_analysis",
    "story_structure",
    "shot_plan",
    "asset_plan",
    "asset_creation",
    "voiceover",
    "animation",
    "audio_mix",
    "render",
    "qa",
    "delivery",
)
AUTOMATION = {"automated", "assisted", "manual"}
STAGE_STATUS = {"not_started", "in_progress", "proposed", "approved", "blocked", "done", "needs_human"}
SCRIPT_STATUS = {"draft", "proposed", "approved"}
SCENE_STATUS = {"proposed", "approved", "style_proof"}
ASSET_SOURCE = {"user_supplied", "library", "generated", "placeholder"}
ASSET_APPROVAL = {"missing", "draft", "placeholder", "proposed", "approved", "rejected"}
DURATION_MODE = {"target", "voice_driven", "explicit_scenes"}
PATH_BASE = {"project", "repo", "absolute"}


def load_json(path: Path) -> tuple[dict | None, list[dict]]:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        return None, [issue("missing_file", str(path), f"Không thấy file {path.name}.")]
    except json.JSONDecodeError as exc:
        return None, [issue("invalid_json", str(path), f"JSON không đọc được: {exc.msg} (dòng {exc.lineno}).")]
    if not isinstance(data, dict):
        return None, [issue("invalid_json", str(path), "Gốc file phải là một object.")]
    return data, []


def load_project(project_dir: Path) -> tuple[dict | None, list[dict]]:
    project_dir = project_dir.resolve()
    manifest, errors = load_json(project_dir / "manifest.json")
    if manifest is None:
        return None, errors
    shot_name = manifest.get("shotPlan")
    if not isinstance(shot_name, str) or not shot_name:
        errors.append(issue("shot_plan", "shotPlan", "manifest.shotPlan phải là tên file trong thư mục dự án."))
        return {"manifest": manifest, "shotPlan": None, "dir": project_dir}, errors
    shot_path = project_dir / shot_name
    if not shot_path.resolve().is_relative_to(project_dir):
        errors.append(issue("shot_plan", "shotPlan", "shot plan phải nằm trong thư mục dự án."))
        return {"manifest": manifest, "shotPlan": None, "dir": project_dir}, errors
    shot, shot_errors = load_json(shot_path)
    errors.extend(shot_errors)
    return {"manifest": manifest, "shotPlan": shot, "dir": project_dir}, errors


def validate_project(project: dict) -> tuple[list[dict], list[dict]]:
    errors: list[dict] = []
    warnings: list[dict] = []
    manifest = project.get("manifest") or {}
    shot = project.get("shotPlan") or {}
    _require_str(errors, manifest, "schemaVersion", "schemaVersion")
    if manifest.get("schemaVersion") not in (None, SCHEMA_PROJECT):
        if manifest.get("schemaVersion") != SCHEMA_PROJECT:
            errors.append(issue("schema", "schemaVersion", f"schemaVersion phải là {SCHEMA_PROJECT}."))
    _require_str(errors, manifest, "projectId", "projectId")
    _require_str(errors, manifest, "projectName", "projectName")
    brand = manifest.get("brand")
    if not isinstance(brand, dict) or not brand.get("id") or not brand.get("name"):
        errors.append(issue("brand", "brand", "brand.id và brand.name là bắt buộc. Không có thương hiệu mặc định."))
    script = manifest.get("script")
    if not isinstance(script, dict):
        errors.append(issue("script", "script", "script là bắt buộc."))
    else:
        if script.get("approvalStatus") not in SCRIPT_STATUS:
            errors.append(issue("script", "script.approvalStatus", "approvalStatus phải là draft, proposed hoặc approved."))
        _require_str(errors, script, "version", "script.version")
    _duration_policy(errors, warnings, manifest)
    _story(errors, manifest)
    _voice(errors, manifest)
    assets = _assets(errors, manifest)
    _style(errors, manifest)
    _audio_profile(errors, manifest)
    _format(errors, manifest)
    _render(errors, manifest)
    _pipeline(errors, manifest)
    scenes = _shot(errors, manifest, shot)
    if isinstance(scenes, list) and isinstance(assets, dict):
        _bind_scenes(errors, warnings, manifest, scenes, assets)
        _layout_fit(errors, warnings, manifest, scenes)
    return errors, warnings


def _require_str(errors: list[dict], obj: dict, key: str, path: str) -> None:
    value = obj.get(key)
    if not isinstance(value, str) or not value.strip():
        errors.append(issue("required", path, f"{path} phải là chuỗi không rỗng."))


def _duration_policy(errors: list[dict], warnings: list[dict], manifest: dict) -> None:
    policy = manifest.get("durationPolicy")
    if not isinstance(policy, dict):
        errors.append(issue("duration", "durationPolicy", "durationPolicy là bắt buộc."))
        return
    mode = policy.get("mode")
    if mode not in DURATION_MODE:
        errors.append(issue("duration", "durationPolicy.mode", "mode phải là target, voice_driven hoặc explicit_scenes."))
        return
    target = policy.get("targetSeconds")
    if mode == "target":
        if not isinstance(target, (int, float)) or target <= 0:
            errors.append(issue("duration", "durationPolicy.targetSeconds", "Chế độ target cần targetSeconds > 0."))
    elif mode == "explicit_scenes" and target is not None:
        errors.append(issue("duration", "durationPolicy.targetSeconds", "Chế độ explicit_scenes không đặt targetSeconds."))
    elif target is not None and (not isinstance(target, (int, float)) or target <= 0):
        errors.append(issue("duration", "durationPolicy.targetSeconds", "targetSeconds nếu có thì phải > 0."))
    if "notes" in policy and not isinstance(policy.get("notes"), str):
        errors.append(issue("duration", "durationPolicy.notes", "notes phải là chuỗi."))
    # A numeric hint is project data. It is never compared with a built-in runtime.
    _ = warnings


def _story(errors: list[dict], manifest: dict) -> None:
    beats = manifest.get("storyStructure")
    if not isinstance(beats, list) or not beats:
        errors.append(issue("story", "storyStructure", "Cần ít nhất một beat. Số beat không cố định."))
        return
    seen = set()
    for index, beat in enumerate(beats):
        path = f"storyStructure[{index}]"
        if not isinstance(beat, dict) or not isinstance(beat.get("beatId"), str) or not beat.get("summary"):
            errors.append(issue("story", path, "Mỗi beat cần beatId và summary."))
            continue
        if beat["beatId"] in seen:
            errors.append(issue("story", path, "beatId bị trùng."))
        seen.add(beat["beatId"])


def _voice(errors: list[dict], manifest: dict) -> None:
    voice = manifest.get("voice")
    if not isinstance(voice, dict):
        errors.append(issue("voice", "voice", "voice là bắt buộc, kể cả khi chưa có lời đọc."))
        return
    _require_str(errors, voice, "profileId", "voice.profileId")
    _require_str(errors, voice, "language", "voice.language")
    if not isinstance(voice.get("pronunciationNotes", ""), str):
        errors.append(issue("voice", "voice.pronunciationNotes", "pronunciationNotes phải là chuỗi."))
    tracks = voice.get("tracks")
    if not isinstance(tracks, list):
        errors.append(issue("voice", "voice.tracks", "voice.tracks phải là mảng."))
        return
    seen = set()
    for index, track in enumerate(tracks):
        path = f"voice.tracks[{index}]"
        if not isinstance(track, dict):
            errors.append(issue("voice", path, "Track không hợp lệ."))
            continue
        track_id = track.get("trackId")
        if not isinstance(track_id, str) or track_id in seen:
            errors.append(issue("voice", path, "trackId phải là chuỗi duy nhất."))
        seen.add(track_id)
        if track.get("approval") not in ASSET_APPROVAL:
            errors.append(issue("voice", path + ".approval", "approval của track không hợp lệ."))
        if not isinstance(track.get("text", ""), str):
            errors.append(issue("voice", path + ".text", "text của track phải là chuỗi."))


def _assets(errors: list[dict], manifest: dict) -> dict | None:
    rows = manifest.get("assets")
    if not isinstance(rows, list):
        errors.append(issue("asset", "assets", "assets phải là mảng."))
        return None
    found = {}
    for index, row in enumerate(rows):
        path = f"assets[{index}]"
        if not isinstance(row, dict):
            errors.append(issue("asset", path, "Asset không hợp lệ."))
            continue
        asset_id = row.get("assetId")
        if not isinstance(asset_id, str) or not asset_id or asset_id in found:
            errors.append(issue("asset", path, "assetId phải là chuỗi duy nhất."))
            continue
        if row.get("source") not in ASSET_SOURCE:
            errors.append(issue("asset", path + ".source", "source phải là user_supplied, library, generated hoặc placeholder."))
        if row.get("approval") not in ASSET_APPROVAL:
            errors.append(issue("asset", path + ".approval", "approval của asset không hợp lệ."))
        if row.get("pathBase", "project") not in PATH_BASE:
            errors.append(issue("asset", path + ".pathBase", "pathBase không hợp lệ."))
        if row.get("role") == "product_ui":
            if row.get("approval") != "approved" or row.get("source") not in {"user_supplied", "library"}:
                errors.append(issue(
                    "product_ui",
                    path,
                    "UI sản phẩm chỉ được là tài sản người dùng hoặc thư viện đã duyệt. Không thay bằng placeholder.",
                ))
            if row.get("demoData") is True and row.get("demoLabel") != "Dữ liệu minh họa":
                errors.append(issue(
                    "demo_label",
                    path,
                    "Dữ liệu demo trên UI phải khai báo demoLabel đúng chuỗi 'Dữ liệu minh họa'.",
                ))
        if row.get("approval") == "approved" and row.get("source") == "placeholder":
            errors.append(issue("asset", path, "Placeholder không thể ở trạng thái approved."))
        found[asset_id] = row
    return found


def _style(errors: list[dict], manifest: dict) -> None:
    style = manifest.get("visualStyle")
    if not isinstance(style, dict):
        errors.append(issue("style", "visualStyle", "visualStyle là bắt buộc."))
        return
    _require_str(errors, style, "profileId", "visualStyle.profileId")
    palette = style.get("palette")
    required = ("background", "ink", "accent", "paper")
    if not isinstance(palette, dict) or any(name not in palette for name in required):
        errors.append(issue("style", "visualStyle.palette", "palette cần background, ink, accent và paper."))
    else:
        for name in palette:
            if not _color(palette[name]):
                errors.append(issue("style", f"visualStyle.palette.{name}", "Màu phải là 3 số 0–255."))
    typography = style.get("typography")
    if not isinstance(typography, dict):
        errors.append(issue("style", "visualStyle.typography", "typography là bắt buộc."))
    else:
        for key in ("fontRegular", "fontBold"):
            if not isinstance(typography.get(key), str):
                errors.append(issue("style", f"visualStyle.typography.{key}", "Cần đường dẫn font."))
    safe = style.get("safeArea")
    if not isinstance(safe, dict):
        errors.append(issue("style", "visualStyle.safeArea", "safeArea là bắt buộc."))
    else:
        for edge in ("top", "right", "bottom", "left"):
            value = safe.get(edge)
            if not isinstance(value, (int, float)) or not 0 <= value < 0.4:
                errors.append(issue("style", f"visualStyle.safeArea.{edge}", "Tỷ lệ safe area phải trong khoảng 0 đến dưới 0.4."))


def _color(value: object) -> bool:
    return (
        isinstance(value, list)
        and len(value) == 3
        and all(isinstance(channel, int) and 0 <= channel <= 255 for channel in value)
    )


def _audio_profile(errors: list[dict], manifest: dict) -> None:
    profile = manifest.get("audioProfile")
    if not isinstance(profile, dict):
        errors.append(issue("audio", "audioProfile", "audioProfile là bắt buộc."))
        return
    if not isinstance(profile.get("ducking"), bool):
        errors.append(issue("audio", "audioProfile.ducking", "ducking phải là true hoặc false."))
    for key in ("voiceGain", "sfxGain"):
        value = profile.get(key)
        if not isinstance(value, (int, float)) or not 0 <= value <= 2:
            errors.append(issue("audio", f"audioProfile.{key}", f"{key} phải từ 0 đến 2."))
    if profile.get("musicPolicy") not in {"none", "required", "optional"}:
        errors.append(issue("audio", "audioProfile.musicPolicy", "musicPolicy phải là none, required hoặc optional."))


def _format(errors: list[dict], manifest: dict) -> None:
    fmt = manifest.get("format")
    if not isinstance(fmt, dict):
        errors.append(issue("format", "format", "format là bắt buộc."))
        return
    ratio = fmt.get("aspectRatio")
    width = fmt.get("width")
    height = fmt.get("height")
    fps = fmt.get("frameRate")
    if not isinstance(width, int) or not isinstance(height, int) or width < 16 or height < 16:
        errors.append(issue("format", "format", "width và height phải là số nguyên tối thiểu 16."))
        return
    if not isinstance(fps, (int, float)) or not 1 <= fps <= 60:
        errors.append(issue("format", "format.frameRate", "frameRate phải từ 1 đến 60."))
    if not isinstance(ratio, str) or ":" not in ratio:
        errors.append(issue("format", "format.aspectRatio", "aspectRatio có dạng '16:9'."))
        return
    left, _, right = ratio.partition(":")
    try:
        expected = float(left) / float(right)
    except ValueError:
        errors.append(issue("format", "format.aspectRatio", "aspectRatio không đọc được."))
        return
    actual = width / height
    if abs(actual - expected) / expected > 0.02:
        errors.append(issue("format", "format", "width/height không khớp aspectRatio đã khai."))


def _render(errors: list[dict], manifest: dict) -> None:
    render = manifest.get("render")
    if not isinstance(render, dict):
        errors.append(issue("render", "render", "render là bắt buộc."))
        return
    if render.get("videoCodec") != "libx264" or render.get("audioCodec") != "aac":
        errors.append(issue("render", "render", "V1 xuất H.264 (libx264) và AAC."))
    crf = render.get("crf")
    if not isinstance(crf, int) or not 0 <= crf <= 51:
        errors.append(issue("render", "render.crf", "crf phải từ 0 đến 51."))
    if not isinstance(render.get("allowPlaceholders"), bool):
        errors.append(issue("render", "render.allowPlaceholders", "allowPlaceholders phải là true hoặc false."))
    if render.get("versioned") is not True:
        errors.append(issue("render", "render.versioned", "Mọi lần xuất phải versioned=true để không ghi đè."))
    if not isinstance(render.get("outputRoot"), str) or not render.get("outputRoot"):
        errors.append(issue("render", "render.outputRoot", "outputRoot là bắt buộc."))


def _pipeline(errors: list[dict], manifest: dict) -> None:
    rows = manifest.get("pipeline")
    if not isinstance(rows, list):
        errors.append(issue("pipeline", "pipeline", "pipeline là bắt buộc."))
        return
    seen = []
    for index, row in enumerate(rows):
        path = f"pipeline[{index}]"
        if not isinstance(row, dict):
            errors.append(issue("pipeline", path, "Bước pipeline không hợp lệ."))
            continue
        seen.append(row.get("stage"))
        if row.get("automation") not in AUTOMATION:
            errors.append(issue("pipeline", path, "automation phải là automated, assisted hoặc manual."))
        if row.get("status") not in STAGE_STATUS:
            errors.append(issue("pipeline", path, "status của bước không hợp lệ."))
    if seen != list(STAGES):
        errors.append(issue("pipeline", "pipeline", "pipeline phải đủ 12 bước, đúng thứ tự brief → delivery."))


def _shot(errors: list[dict], manifest: dict, shot: dict) -> list | None:
    if not shot:
        errors.append(issue("shot_plan", "shotPlan", "Chưa có shot plan."))
        return None
    if shot.get("schemaVersion") != SCHEMA_SHOT:
        errors.append(issue("shot_plan", "schemaVersion", f"Shot plan phải có schemaVersion {SCHEMA_SHOT}."))
    if shot.get("projectId") != manifest.get("projectId"):
        errors.append(issue("shot_plan", "projectId", "projectId của shot plan phải trùng manifest."))
    if shot.get("approvalStatus") not in SCRIPT_STATUS:
        errors.append(issue("shot_plan", "approvalStatus", "approvalStatus của shot plan không hợp lệ."))
    scenes = shot.get("scenes")
    if not isinstance(scenes, list) or not scenes:
        errors.append(issue("shot_plan", "scenes", "Cần ít nhất một cảnh. Số cảnh không cố định."))
        return None
    seen = set()
    for index, scene in enumerate(scenes):
        path = f"scenes[{index}]"
        if not isinstance(scene, dict):
            errors.append(issue("shot_plan", path, "Cảnh không hợp lệ."))
            continue
        scene_id = scene.get("sceneId")
        if not isinstance(scene_id, str) or not scene_id or scene_id in seen:
            errors.append(issue("shot_plan", path + ".sceneId", "sceneId phải là chuỗi duy nhất."))
        seen.add(scene_id)
        if scene.get("approvalStatus") not in SCENE_STATUS:
            errors.append(issue("shot_plan", path + ".approvalStatus", "approvalStatus của cảnh không hợp lệ."))
        for key in ("storyGoal", "dialogue", "action"):
            if not isinstance(scene.get(key), str):
                errors.append(issue("shot_plan", f"{path}.{key}", f"{key} phải là chuỗi."))
        duration = scene.get("durationSeconds")
        if not isinstance(duration, (int, float)) or duration <= 0:
            errors.append(issue("shot_plan", path + ".durationSeconds", "durationSeconds phải > 0."))
        if scene.get("durationSource", "fixed") not in {"fixed", "voice_track"}:
            errors.append(issue("shot_plan", path + ".durationSource", "durationSource phải là fixed hoặc voice_track."))
        if not isinstance(scene.get("templateId"), str) or not scene.get("templateId"):
            errors.append(issue("shot_plan", path + ".templateId", "templateId là bắt buộc."))
        if not isinstance(scene.get("params"), dict):
            errors.append(issue("shot_plan", path + ".params", "params phải là object."))
        if not isinstance(scene.get("onScreenText"), list):
            errors.append(issue("shot_plan", path + ".onScreenText", "onScreenText phải là mảng."))
        if not isinstance(scene.get("assets"), list):
            errors.append(issue("shot_plan", path + ".assets", "assets của cảnh phải là mảng id."))
        if "visualMetaphor" not in scene:
            errors.append(issue("shot_plan", path + ".visualMetaphor", "visualMetaphor phải có, dùng null nếu cảnh không có ẩn dụ."))
        audio = scene.get("audio")
        if not isinstance(audio, dict):
            errors.append(issue("shot_plan", path + ".audio", "audio của cảnh là bắt buộc."))
        else:
            if not isinstance(audio.get("sfx"), list) or not isinstance(audio.get("intentionalSilences"), list):
                errors.append(issue("shot_plan", path + ".audio", "sfx và intentionalSilences phải là mảng."))
            else:
                for silence in audio["intentionalSilences"]:
                    if not isinstance(silence, dict) or not silence.get("reason"):
                        errors.append(issue("shot_plan", path + ".audio.intentionalSilences", "Mỗi khoảng lặng cần reason."))
        if scene.get("transitionOut") is not None and not isinstance(scene.get("transitionOut"), dict):
            errors.append(issue("shot_plan", path + ".transitionOut", "transitionOut là object hoặc null."))
        if not isinstance(scene.get("acceptance"), list) or not scene.get("acceptance"):
            errors.append(issue("shot_plan", path + ".acceptance", "Mỗi cảnh cần ít nhất một tiêu chí nghiệm thu."))
    return scenes


def _layout_fit(errors: list[dict], warnings: list[dict], manifest: dict, scenes: list) -> None:
    style = manifest.get("visualStyle")
    fmt = manifest.get("format")
    if not isinstance(style, dict) or not isinstance(fmt, dict):
        return
    if not isinstance(style.get("palette"), dict) or not isinstance(style.get("safeArea"), dict):
        return
    width = fmt.get("width")
    height = fmt.get("height")
    if not isinstance(width, int) or not isinstance(height, int) or width <= 0 or height <= 0:
        return
    contract = DesignContract.from_frame(style, width, height)
    profile = art_profile(style)
    for scene in scenes:
        if not isinstance(scene, dict) or scene.get("templateId") != "designed_board":
            continue
        params = scene.get("params") if isinstance(scene.get("params"), dict) else {}
        decision = contract.fit_board(str(params.get("kind") or ""), params)
        scene_id = str(scene.get("sceneId") or "?")
        if decision["status"] == "blocked":
            errors.append(issue(decision["code"] or "layout_density", scene_id, decision["reason"]))
            continue
        intent = assess_intent(profile, scene_intent(params))
        if intent["status"] == "blocked":
            errors.append(issue(intent["code"] or "intent_missing", scene_id, intent["reason"]))
            continue
        visual = assess_scene(contract, decision["kind"], params)
        if visual["status"] == "blocked":
            errors.append(issue(visual["code"] or "visual_quality", scene_id, visual["reason"]))
        elif decision["omitted"]:
            warnings.append(issue("layout_omit", scene_id, decision["reason"] + " " + ", ".join(decision["omitted"])))
    for finding in assess_film(scenes):
        errors.append(issue(finding["code"], finding["sceneId"], finding["reason"]))
    for finding in assess_composition(scenes):
        errors.append(issue(finding["code"], finding["sceneId"], finding["reason"]))


def _bind_scenes(errors: list[dict], warnings: list[dict], manifest: dict, scenes: list, assets: dict) -> None:
    tracks = {row.get("trackId"): row for row in manifest.get("voice", {}).get("tracks", []) if isinstance(row, dict)}
    total = 0.0
    for scene in scenes:
        if not isinstance(scene, dict) or not isinstance(scene.get("durationSeconds"), (int, float)):
            continue
        total += float(scene["durationSeconds"])
        for asset_id in referenced_ids(scene):
            if asset_id not in assets:
                errors.append(issue("asset", scene.get("sceneId", "?"), f"Cảnh tham chiếu asset chưa khai báo: {asset_id}."))
                continue
            row = assets[asset_id]
            if row.get("approval") == "missing":
                errors.append(issue("asset_missing", asset_id, f"Asset bắt buộc {asset_id} đang missing."))
            if row.get("approval") == "rejected":
                errors.append(issue("asset_rejected", asset_id, f"Asset {asset_id} đã bị từ chối."))
            if is_picture(row):
                decision = inspect_asset(row)
                if not decision["allowed"]:
                    finding = issue("asset_not_cleared", asset_id, decision["reason"])
                    if scene.get("approvalStatus") in {"approved", "style_proof"}:
                        errors.append(finding)
                    else:
                        warnings.append(finding)
        picture = goods_source(scene, assets)
        if picture["status"] == "blocked":
            finding = issue(picture["code"], str(scene.get("sceneId") or "?"), picture["reason"])
            if scene.get("approvalStatus") in {"approved", "style_proof"}:
                errors.append(finding)
            else:
                warnings.append(finding)
        gate = production_gate(visual_plan(scene, assets))
        hard = scene.get("approvalStatus") in {"approved", "style_proof"}
        scene_id = str(scene.get("sceneId") or "?")
        for item in gate["blockers"]:
            code = str(item.get("code") or "")
            if code not in ASSET_ERROR_CODES or code == "product_source_missing":
                continue
            finding = issue(code, scene_id, str(item.get("reason") or ""))
            if hard:
                errors.append(finding)
            else:
                warnings.append(finding)
        for item in gate["failures"]:
            code = str(item.get("code") or "")
            if code not in QUALITY_ERROR_CODES:
                continue
            finding = issue(code, scene_id, str(item.get("reason") or ""))
            if hard:
                errors.append(finding)
            else:
                warnings.append(finding)
        diagnosed = {item["code"]: item for item in scene_findings(scene, assets)}
        seen = {(item["code"], item["path"]) for item in errors}
        for finding in diagnosed.values():
            if finding["code"] not in {"semantic_unreviewed", "semantic_fail", "ui_unpriced", "ui_empty", "ui_inconsistent"}:
                continue
            key = (finding["code"], finding["path"])
            if key in seen:
                continue
            extra = issue(finding["code"], finding["path"], finding["reason"])
            if finding.get("remedy"):
                extra["remedy"] = finding["remedy"]
            if hard:
                errors.append(extra)
            else:
                warnings.append(extra)
            seen.add(key)
        for bucket in (errors, warnings):
            for item in bucket:
                known = diagnosed.get(item["code"]) if item.get("path") == scene_id else None
                if known and known.get("remedy"):
                    item["remedy"] = known["remedy"]
        voice_id = (scene.get("audio") or {}).get("voiceTrackId")
        if voice_id:
            if voice_id not in tracks:
                errors.append(issue("voice", scene.get("sceneId", "?"), f"Không có voice track {voice_id}."))
            elif tracks[voice_id].get("approval") == "missing":
                errors.append(issue("voice_missing", voice_id, f"Voice track {voice_id} đang missing."))
        for cue in (scene.get("audio") or {}).get("sfx") or []:
            if not isinstance(cue, dict) or cue.get("assetId") not in assets:
                errors.append(issue("sfx", scene.get("sceneId", "?"), "SFX trỏ tới asset không có trong manifest."))
    policy = manifest.get("durationPolicy") or {}
    target = policy.get("targetSeconds")
    if isinstance(target, (int, float)) and total > 0 and abs(total - float(target)) / float(target) > 0.15:
        warnings.append(issue(
            "duration_differs_from_target",
            "durationPolicy.targetSeconds",
            f"Tổng durationSeconds của các cảnh là {total:.2f}s, khác hint {float(target):.2f}s hơn 15%. Hint không bị ép cho khớp.",
        ))


def resolve_path(asset: dict, project_dir: Path, repo_root: Path) -> Path | None:
    raw = asset.get("path")
    if not isinstance(raw, str) or not raw:
        return None
    base_name = asset.get("pathBase", "project")
    candidate = Path(raw)
    if base_name == "absolute" or candidate.is_absolute():
        return candidate
    base = repo_root if base_name == "repo" else project_dir
    return (base / candidate).resolve()

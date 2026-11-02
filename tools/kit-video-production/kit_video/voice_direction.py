"""Shared voice direction. Performance notes never enter the TTS string."""

from __future__ import annotations

import json
import re
from pathlib import Path

from kit_video.errors import ProductionError, issue
from kit_video.motion import credential_present

PIPELINE = (
    "script",
    "voice_direction",
    "tts_generation",
    "audio_measurement",
    "listening_review",
    "timing_approval",
)
MARKER = re.compile(r"\[[^\]\n]+\]")


def standard_path(repo_root: Path) -> Path:
    return repo_root / "docs/kit-video-production/voice-direction-standard-v1.json"


def load_standard(repo_root: Path) -> dict:
    import json

    path = standard_path(repo_root)
    if not path.is_file():
        raise ProductionError("FAIL", [issue("voice_standard", str(path), "Chưa có Voice Direction Standard.")])
    return json.loads(path.read_text(encoding="utf-8"))


def script_dialogues(script_path: Path) -> list[dict]:
    text = script_path.read_text(encoding="utf-8")
    found = []
    for part in re.split(r"^## segment ", text, flags=re.M)[1:]:
        scene_id = part.splitlines()[0].strip()
        match = re.search(r"^dialogue: \|\r?\n((?:  .*(?:\r?\n|$))*)", part, re.M)
        if not match:
            raise ProductionError("FAIL", [issue("script", scene_id, "Thiếu dialogue.")])
        lines = []
        for line in match.group(1).splitlines():
            if not line.startswith("  "):
                raise ProductionError("FAIL", [issue("script", scene_id, "Lệch thụt dòng lời thoại.")])
            lines.append(line[2:])
        spoken = "\n".join(lines).strip()
        found.append({"sceneId": scene_id, "dialogue": spoken})
    return found


def prepare_tts_text(spoken: str, entries: list[dict], apply_statuses: tuple[str, ...] = ("VERIFIED",)) -> str:
    """Return the string a TTS adapter may receive. Does not write the script."""
    result = spoken
    for entry in entries:
        if entry.get("verification") not in apply_statuses:
            continue
        replacement = entry.get("tts")
        display = entry.get("display") or ""
        if not replacement or not display or replacement == display:
            continue
        pattern = re.compile(rf"(?<!\w){re.escape(display)}(?!\w)")
        result = pattern.sub(replacement, result)
    if MARKER.search(result):
        raise ProductionError("FAIL", [issue("tts_text", "direction", "Chỉ dẫn trong ngoặc vuông không được gửi cho TTS.")])
    return result


def payload(line: dict, entries: list[dict]) -> str:
    """TTS receives the spoken line only. Emphasis and pause notes stay on the line."""
    return prepare_tts_text(line["dialogue"], entries)


def mix_plan(lines: list[dict], profiles: list[dict]) -> list[dict]:
    """Order a cast. One voice is a cast of one, not a pipeline default."""
    known = {item["profileId"] for item in profiles}
    orders = [line["order"] for line in lines]
    if len(orders) != len(set(orders)):
        raise ProductionError("FAIL", [issue("cast", "order", "Thứ tự thoại bị trùng.")])
    plan = []
    for line in sorted(lines, key=lambda item: item["order"]):
        if line["profileId"] not in known:
            raise ProductionError("FAIL", [issue("cast", line["profileId"], "Chưa có voice profile.")])
        plan.append({
            "order": line["order"],
            "speakerId": line["speakerId"],
            "profileId": line["profileId"],
            "presetId": line["presetId"],
            "text": line["dialogue"],
            "gapAfterSeconds": line.get("gapAfterSeconds", 0),
            "gain": line.get("gain", 1),
        })
    return plan


def check_sheet(sheet: dict, spoken: list[dict], standard: dict) -> list[dict]:
    errors = []
    if sheet.get("approval") == "APPROVED":
        errors.append(issue("approval", "sheet", "Sheet chưa được người duyệt."))
    by_script = {item["sceneId"]: item["dialogue"] for item in spoken}
    profiles = {item["profileId"] for item in standard["profiles"]}
    presets = {item["presetId"] for item in standard["presets"]}
    cast = {item["speakerId"] for item in sheet.get("cast", [])}
    seen = []
    for scene in sheet.get("scenes", []):
        scene_id = scene.get("sceneId", "?")
        seen.append(scene_id)
        if scene.get("dialogue") != by_script.get(scene_id):
            errors.append(issue("script", scene_id, "Lời trên sheet khác script.md."))
        if MARKER.search(scene.get("dialogue") or ""):
            errors.append(issue("script", scene_id, "Lời kịch bản đang chứa chỉ dẫn."))
        if scene.get("speakerId") not in cast:
            errors.append(issue("cast", scene_id, "Thiếu vai."))
        if scene.get("profileId") not in profiles:
            errors.append(issue("profile", scene_id, "Thiếu voice profile."))
        if scene.get("presetId") not in presets:
            errors.append(issue("preset", scene_id, "Thiếu delivery preset."))
        if scene.get("approval") == "APPROVED":
            errors.append(issue("approval", scene_id, "Cảnh giọng chưa được duyệt."))
        for word in scene.get("emphasis", []):
            if word not in scene.get("dialogue", ""):
                errors.append(issue("emphasis", scene_id, "Từ nhấn không có trong lời."))
        payload_text = payload(scene, standard["dictionary"])
        if payload_text != scene["dialogue"] and scene.get("ttsTextStatus") == "UNSUBSTITUTED":
            errors.append(issue("tts_text", scene_id, "Đang thay chữ khi sheet còn UNSUBSTITUTED."))
        for note in scene.get("emphasis", []) + scene.get("phrases", []):
            if note and note not in scene["dialogue"] and note not in payload_text:
                continue
        if any(token in payload_text for token in ("[nghỉ]", "[nhấn mạnh]")):
            errors.append(issue("tts_text", scene_id, "Payload còn chỉ dẫn."))
    if seen != list(by_script):
        errors.append(issue("script", "scenes", "Sheet không khớp thứ tự 12 đoạn."))
    return errors


def check_standard(standard: dict) -> list[dict]:
    errors = []
    if tuple(standard.get("pipeline", [])) != PIPELINE:
        errors.append(issue("pipeline", "pipeline", "Sai thứ tự Script → Direction → TTS → Đo → Nghe → Khóa."))
    if standard.get("approval") == "APPROVED":
        errors.append(issue("approval", "standard", "Chuẩn giọng chưa được người duyệt."))
    controls = set()
    unsupported = set()
    for adapter in standard.get("adapters", []):
        controls.update(adapter.get("controls", []))
        unsupported.update(adapter.get("unsupported", []))
    if "emotion" in controls or "emphasis" in controls:
        errors.append(issue("adapter", "edge-tts", "Adapter không có nút cảm xúc hoặc nhấn từ."))
    if not {"emotion", "emphasis", "phoneme"} <= unsupported:
        errors.append(issue("adapter", "edge-tts", "Thiếu khai báo phần adapter không làm được."))
    for entry in standard.get("dictionary", []):
        if entry.get("verification") == "VERIFIED" and not entry.get("listeningApproved"):
            errors.append(issue("dictionary", entry.get("entryId", "?"), "Chưa có người nghe duyệt mà đã VERIFIED."))
    genders = {item.get("gender") for item in standard.get("profiles", [])}
    if "male" not in genders or "female" not in genders:
        errors.append(issue("profile", "gender", "Danh tính giọng nam và nữ phải tách khỏi preset."))
    for preset in standard.get("presets", []):
        if "gender" in preset:
            errors.append(issue("preset", preset["presetId"], "Preset không được chứa giới tính."))
    return errors


def intro_edit_gate(manifest: dict) -> dict:
    """Refuse an intro assemble. File tails are not scene holds, and nothing is approved."""
    reasons = ["Chưa có video cảnh đã duyệt để ghép."]
    if manifest.get("durationLocked"):
        reasons.append("durationLocked đang bật khi chưa có phê duyệt thời lượng.")
    if manifest.get("approval") == "APPROVED" or manifest.get("proposedPictureStatus") == "APPROVED":
        reasons.append("Manifest đang tự nhận đã duyệt.")
    return {
        "status": "BLOCKED",
        "finalVideo": None,
        "requestSent": False,
        "aiCost": 0,
        "reasons": reasons,
    }


def intro_picture_blockers(repo_root: Path) -> list[dict]:
    """Why the intro film cannot take a picture from the shared engine yet.

    Reads approval records and files. Does not approve art, does not render,
    and does not treat a generated still as a production asset.
    """
    proof = repo_root / "proof" / "kit-video-production-v1"
    intro = proof / "projects" / "novixa-intro-film"
    commercial = proof / "projects" / "novixa-pharmacy-commercial"
    rows: list[dict] = []
    manifest = json.loads((intro / "production-manifest.json").read_text(encoding="utf-8"))
    auth = manifest.get("executionAuthorizationV1") or {}
    # The execution gate names these two files. characterApprovedForIntro covers only them.
    intro_character = {
        "pharmacist-nova-cartoon-v3": "assets/pharmacist-nova-cartoon-v3.jpg",
        "pharmacy-plate-v3": "keyframes/v3/s01-pharmacy.jpg",
    }
    intro_character_approved = (
        auth.get("characterApprovedForIntro") is True
        and auth.get("characterApproval") == "APPROVED"
    )
    if not intro_character_approved:
        rows.append(issue(
            "character_approval",
            "executionAuthorizationV1.characterApprovedForIntro",
            "Nhân vật chưa được duyệt cho intro. Cho phép triển khai không phải duyệt tạo hình.",
        ))
    else:
        for asset_id, relative in intro_character.items():
            if not (commercial / relative).is_file():
                rows.append(issue("asset_missing", relative, f"{asset_id} được duyệt cho intro nhưng không thấy file."))
    if auth.get("paidCallAuthorized") is not True or auth.get("expectedCostConfirmed") is not True:
        rows.append(issue(
            "paid_call",
            "executionAuthorizationV1.paidCallAuthorized",
            "Chưa có phép gọi dịch vụ trả phí và chưa xác nhận chi phí.",
        ))
    elif not credential_present("RUNWAY_API_KEY"):
        rows.append(issue(
            "credential",
            "RUNWAY_API_KEY",
            "Biến RUNWAY_API_KEY trống. Không gửi request.",
        ))
    standard = json.loads((commercial / "visual-standard-v1.json").read_text(encoding="utf-8"))
    if standard.get("approval") == "APPROVED":
        rows.append(issue(
            "visual_standard",
            "projects/novixa-pharmacy-commercial/visual-standard-v1.json",
            "Bộ chuẩn toàn cục không được tự đánh dấu APPROVED. Duyệt intro nằm ở executionAuthorizationV1.",
        ))
    for asset in standard.get("assets") or []:
        asset_id = str(asset.get("id") or "?")
        role = asset.get("role")
        if asset.get("state") == "spec-only" or not asset.get("path"):
            if role == "character" or asset_id.startswith("pose-"):
                rows.append(issue("asset_missing", asset_id, f"Chưa có file: {asset.get('use') or asset_id}."))
            continue
        if asset_id in intro_character or role == "character":
            if intro_character_approved and asset_id in intro_character:
                continue
            path = commercial / str(asset["path"])
            present = "có file" if path.is_file() else "không thấy file"
            rows.append(issue(
                "asset_unapproved",
                str(asset["path"]),
                f"{asset_id}: {present}, approval={asset.get('approval')}. Không đưa vào phim.",
            ))
    reference = json.loads((commercial / "probes" / "character-reference-v2" / "reference.json").read_text(encoding="utf-8"))
    if reference.get("integratedIntoProduction") is not False or reference.get("approval") == "APPROVED":
        rows.append(issue(
            "pose_missing",
            "probes/character-reference-v2/reference.json",
            "Bộ tư thế không được tự nhận đã vào sản xuất.",
        ))
    for item in reference.get("items") or []:
        if item.get("path") is None or item.get("status") == "BLOCKED":
            rows.append(issue(
                "pose_missing",
                str(item.get("id") or "?"),
                str(item.get("reason") or "Chưa có khung tư thế đã duyệt."),
            ))
    engine = json.loads((intro / "manifest.json").read_text(encoding="utf-8"))
    for asset in engine.get("assets") or []:
        if str(asset.get("assetId") or "").startswith("ui-") and asset.get("approval") != "approved":
            rows.append(issue(
                "product_ui",
                str(asset["assetId"]),
                f"Ảnh giao diện role={asset.get('role')} approval={asset.get('approval')}. Không dùng làm UI phim.",
            ))
    shot = json.loads((intro / "shot-plan.proposed.json").read_text(encoding="utf-8"))
    proposed = [scene["sceneId"] for scene in shot.get("scenes") or [] if scene.get("approvalStatus") != "approved"]
    if proposed:
        rows.append(issue(
            "not_approved",
            "shot-plan.proposed.json",
            "Engine từ chối dựng cảnh chưa duyệt: " + ", ".join(proposed),
        ))
    for scene in manifest.get("scenes") or []:
        rows.append(issue(
            "scene_asset",
            str(scene.get("sceneId")),
            "approval={approval}; readiness={readiness}; have={have}; missing={missing}".format(
                approval=scene.get("approval"),
                readiness=scene.get("pictureReadiness"),
                have=scene.get("have"),
                missing=scene.get("missing"),
            ),
        ))
    logo = next((row for row in engine.get("assets") or [] if row.get("assetId") == "logo-novixa"), None)
    if logo is None or logo.get("approval") != "approved":
        rows.append(issue("logo", "logo-novixa", "Logo gốc không còn ở trạng thái approved."))
    if manifest.get("finalVideo") is not None or manifest.get("durationLocked") is not False:
        rows.append(issue("lock", "production-manifest.json", "finalVideo phải null và durationLocked phải false."))
    return rows

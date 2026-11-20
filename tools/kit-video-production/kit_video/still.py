"""Still-image capability.

The provider interface is separate from any vendor. Library files and precise
code graphics are implemented. A remote still generator is not: no price, terms,
or access have been verified, and this module never sends a request.

A missing credential does not retarget the picture to code graphics.
"""

from __future__ import annotations

import hashlib
import os
from pathlib import Path

CODE_ROLES = frozenset({"type", "chart", "ui", "label", "typography", "logo"})
PICTURE_ROLES = frozenset({"product", "setting", "illustration", "portrait", "still", "character"})
PICTURE_KINDS = frozenset({"image", "photo", "still", "screenshot"})
BLOCKED_RIGHTS = frozenset({"", "unverified", "unknown", "none", "mockup", "proposed", "placeholder"})
ILLUSTRATION_LABEL = "Dữ liệu minh họa"
ILLUSTRATION_ROLES = PICTURE_ROLES | {"reference", "product_ui"}
VISUAL_SOURCES = frozenset({
    "SOFTWARE_SCREENSHOT",
    "AI_RECONSTRUCTED_UI",
    "AI_VISUAL",
    "CODE_EXACT",
})
STILL_PROVIDERS = {
    "library": {"implemented": True, "network": False, "costUsd": 0},
    "code": {"implemented": True, "network": False, "costUsd": 0},
    "generated": {
        "implemented": False,
        "network": False,
        "costUsd": None,
        "reason": "Chưa xác minh công cụ, quyền truy cập, điều khoản và đơn giá.",
    },
}
_CREDENTIAL_NAMES = (
    "STILL_IMAGE_API_KEY",
    "RUNWAY_API_KEY",
    "RUNWAYML_API_SECRET",
    "OPENAI_API_KEY",
    "FAL_KEY",
    "VEO_JOIN_FILE",
)


def visual_source(row: dict) -> dict:
    """Classify a picture. An omitted field stays unspecified. A generated file is not a software screenshot."""
    name = row.get("visualSource")
    if name is None or name == "":
        return {"status": "unspecified", "source": "", "reason": ""}
    source = str(name)
    if source not in VISUAL_SOURCES:
        return {
            "status": "invalid",
            "source": source,
            "reason": "visualSource chỉ được là SOFTWARE_SCREENSHOT, AI_RECONSTRUCTED_UI, AI_VISUAL hoặc CODE_EXACT.",
        }
    kind = str(row.get("kind") or "")
    origin = str(row.get("source") or "")
    if source == "SOFTWARE_SCREENSHOT":
        if kind != "screenshot" or origin == "generated":
            return {
                "status": "invalid",
                "source": source,
                "reason": "SOFTWARE_SCREENSHOT phải là kind screenshot và không được là file generated.",
            }
    if source in {"AI_RECONSTRUCTED_UI", "AI_VISUAL"} and kind == "screenshot":
        return {
            "status": "invalid",
            "source": source,
            "reason": "Hình AI không được khai kind screenshot. Giao diện tái dựng phải được ghi là minh họa.",
        }
    return {"status": "ok", "source": source, "reason": ""}


def is_picture(row: dict) -> bool:
    """A raster still. Code-drawn vectors stay on the graphics path."""
    kind = str(row.get("kind") or "")
    if kind == "vector_generated":
        return False
    if kind in PICTURE_KINDS or kind == "character":
        return True
    return str(row.get("role") or "") in PICTURE_ROLES


def referenced_ids(scene: dict) -> list[str]:
    found: list[str] = []
    for asset_id in scene.get("assets") or []:
        if isinstance(asset_id, str) and asset_id and asset_id not in found:
            found.append(asset_id)
    params = scene.get("params") if isinstance(scene.get("params"), dict) else {}
    for key in ("assetId", "logoAssetId", "insetId"):
        asset_id = params.get(key)
        if isinstance(asset_id, str) and asset_id and asset_id not in found:
            found.append(asset_id)
    return found


def _illustration_gaps(row: dict) -> list[str]:
    """What keeps a demo row from being an allowed illustration. An empty list does not make it production data."""
    gaps = []
    if row.get("demoLabel") != ILLUSTRATION_LABEL:
        gaps.append("demo_label")
    kind = str(row.get("kind") or "")
    if kind not in PICTURE_KINDS and kind != "character":
        gaps.append("kind")
    role = str(row.get("role") or row.get("visualRole") or "")
    if role not in ILLUSTRATION_ROLES:
        gaps.append("role")
    return gaps


def inspect_asset(row: dict, path: Path | None = None) -> dict:
    """Clearance for film use. An approved demo illustration is not production data."""
    reasons: list[str] = []
    approval = str(row.get("approval") or "")
    if approval != "approved":
        reasons.append(f"approval={approval or 'missing'}")
    rights = str(row.get("usageRights") or "").strip()
    if rights.lower() in BLOCKED_RIGHTS:
        reasons.append("usageRights")
    if str(row.get("source") or "") == "placeholder":
        reasons.append("placeholder")
    if str(row.get("source") or "") == "generated" and row.get("licenseVerified") is not True:
        reasons.append("license_unverified")
    blob = f"{row.get('label') or ''} {row.get('content') or ''}".lower()
    if "mockup" in blob:
        reasons.append("mockup")
    classified = visual_source(row)
    if classified["status"] == "invalid":
        reasons.append("visual_source")
    demo = row.get("demoData") is True
    if demo:
        for gap in _illustration_gaps(row):
            if gap not in reasons:
                reasons.append(gap)
        if path is not None and not reasons:
            check = confirm_file(row, path)
            if not check.get("exists"):
                reasons.append("asset_missing")
            elif check.get("match") is not True:
                reasons.append("asset_hash" if check.get("recorded") else "hash_unrecorded")
        if reasons:
            reasons.append("demo")
    reason = ""
    if reasons:
        reason = "Asset chưa được phép dùng trong phim: " + ", ".join(reasons) + "."
        if classified["status"] == "invalid" and classified.get("reason"):
            reason = classified["reason"] + " " + reason
    allowed = not reasons
    return {
        "assetId": row.get("assetId"),
        "allowed": allowed,
        "productionData": allowed and not demo,
        "use": "illustration" if allowed and demo else ("production" if allowed else "refused"),
        "reasons": reasons,
        "reason": reason,
        "source": row.get("source"),
        "path": row.get("path"),
        "role": row.get("role") or row.get("visualRole"),
        "content": row.get("content") or row.get("label"),
        "usageRights": rights,
        "approval": approval,
    }


def confirm_file(row: dict, path: Path | None) -> dict:
    """Hash the file when it exists. A missing recorded hash is not a match."""
    if path is None or not path.is_file():
        return {"match": None, "sha256": None, "recorded": False, "exists": False}
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    recorded = str(row.get("sha256") or "").strip().lower()
    if not recorded:
        return {"match": None, "sha256": digest, "recorded": False, "exists": True}
    return {"match": digest == recorded, "sha256": digest, "recorded": True, "exists": True}


def find_reuse(need: dict, assets: list[dict]) -> dict | None:
    key = str(need.get("contentKey") or "").strip()
    if not key:
        return None
    for row in assets:
        if str(row.get("contentKey") or "") != key:
            continue
        if inspect_asset(row)["allowed"]:
            return row
    return None


def _decision(provider: str, code: str, cost: float, reason: str, asset_id: str | None = None) -> dict:
    return {
        "provider": provider,
        "code": code,
        "costUsd": cost,
        "sent": False,
        "fallback": None,
        "assetId": asset_id,
        "reason": reason,
    }


# The only still model the shared park catalog actually runs. The retired id is not a fallback.
PARK_IMAGE_MODEL = "gemini-3.1-flash-image"
_RETIRED_IMAGE_MODELS = {"gemini-2.5-flash-image"}


def park_image(config: dict | None) -> dict:
    """Read the one shared Model AI row for the whole park.

    The only image path is the tenant card: capability IMAGE, providerId GEMINI,
    modelId gemini-3.1-flash-image, enabled, and a credential id. Org settings,
    an image model field, a secret ref, and an API key are not a second path.
    """
    blocked = {
        "authorized": False,
        "provider": "generated",
        "code": "NOT_INTEGRATED",
        "model": None,
        "separateConfig": False,
        "reason": "Park chưa bật Hình ảnh trên Model AI, hoặc chưa có credential Gemini 3.1.",
    }
    if not isinstance(config, dict) or not config:
        return blocked
    capability = str(config.get("capability") or "IMAGE").strip().upper()
    provider = str(config.get("providerId") or "").strip().upper()
    model = str(config.get("modelId") or "").strip()
    enabled = config.get("enabled") is True
    credential = str(config.get("credentialId") or "").strip()
    if model.lower() in _RETIRED_IMAGE_MODELS:
        blocked["reason"] = "Model ảnh này đã nghỉ. Không dùng làm fallback."
        return blocked
    if capability != "IMAGE" or provider != "GEMINI" or model != PARK_IMAGE_MODEL or not enabled or not credential:
        return blocked
    return {
        "authorized": True,
        "provider": "gemini",
        "code": "SHARED",
        "model": PARK_IMAGE_MODEL,
        "separateConfig": False,
        "reason": "Cả park dùng chung một dòng Hình ảnh trên Model AI. Không có đường cấu hình khác.",
    }


def _bound_assets(scene: dict, assets: list | None) -> list[dict]:
    wanted = {str(item) for item in (scene.get("assets") or [])}
    return [row for row in (assets or []) if str(row.get("assetId") or "") in wanted]


def _is_screenshot(row: dict) -> bool:
    return str(row.get("visualSource") or "") == "SOFTWARE_SCREENSHOT" or (
        str(row.get("kind") or "") == "screenshot" and str(row.get("source") or "") != "generated"
    )


def compile_route(scene: dict, assets: list | None = None) -> dict:
    """Route one scene. The provider receives this prompt, not a free-form sample."""
    params = scene.get("params") if isinstance(scene.get("params"), dict) else {}
    intent = params.get("intent") if isinstance(params.get("intent"), dict) else {}
    rows = _bound_assets(scene, assets)
    screenshot = next((row for row in rows if _is_screenshot(row)), None)
    asked = str(params.get("route") or scene.get("route") or "").upper()
    if asked == "IMAGE_TO_VIDEO" or scene.get("kind") == "image_to_video":
        return _motion_route(scene)
    if screenshot is not None or asked == "SOFTWARE_SCREENSHOT":
        return _screenshot_route(scene, screenshot)
    if params.get("edit") is True or asked == "IMAGE_EDIT":
        return _edit_route(scene, rows)
    return _visual_route(scene, intent)


def _screenshot_route(scene: dict, row: dict | None) -> dict:
    lines = []
    evidence = ((scene.get("params") or {}).get("evidence") or {}) if isinstance(scene.get("params"), dict) else {}
    for line in evidence.get("lines") or []:
        if isinstance(line, dict):
            lines.append(f"{line.get('name')} {line.get('qty')} {line.get('price')} {line.get('amount')}")
    if evidence.get("total") is not None:
        lines.append(f"total {evidence.get('total')}")
    return {
        "route": "SOFTWARE_SCREENSHOT",
        "send": False,
        "provider": "composite",
        "prompt": "",
        "visualSource": "SOFTWARE_SCREENSHOT",
        "assetId": None if row is None else row.get("assetId"),
        "demoData": bool(row and row.get("demoData") is True),
        "immutable": lines,
        "forbidden": ["Không gửi ảnh chụp cho Gemini.", "Không vẽ lại giao diện."],
        "contentKey": scene.get("sceneId"),
        "reason": "Đặt ảnh chụp gốc vào frame. Không gửi Gemini.",
    }


def _edit_route(scene: dict, rows: list[dict]) -> dict:
    if any(_is_screenshot(row) for row in rows):
        return _screenshot_route(scene, next(row for row in rows if _is_screenshot(row)))
    params = scene.get("params") or {}
    regions = [str(item) for item in (params.get("editRegions") or []) if str(item).strip()]
    immutable = [str(item) for item in (params.get("immutable") or []) if str(item).strip()]
    blocked = {
        "route": "IMAGE_EDIT",
        "send": False,
        "provider": "blocked",
        "prompt": "",
        "visualSource": "AI_RECONSTRUCTED_UI",
        "immutable": immutable,
        "forbidden": ["Không sửa khi chưa có vùng được phép và danh sách phải giữ."],
        "contentKey": scene.get("sceneId"),
        "reason": "Chỉnh ảnh cần vùng được phép và danh sách chi tiết phải giữ nguyên.",
    }
    if not regions or not immutable:
        return blocked
    prompt = "\n".join([
        "EDIT",
        "Only change: " + ", ".join(regions) + ".",
        "IMMUTABLE",
        "Keep unchanged: " + ", ".join(immutable) + ".",
        "FORBIDDEN",
        "Do not redraw required data, logos, or interface outside the allowed region.",
    ])
    return {
        **blocked,
        "send": True,
        "provider": "gemini",
        "prompt": prompt,
        "reason": "Chỉ sửa vùng được chỉ định. Các mục bất biến phải được kiểm tra sau ảnh.",
    }


def _visual_route(scene: dict, intent: dict) -> dict:
    dialogue = str(scene.get("dialogue") or "").strip()
    metaphor = str(scene.get("visualMetaphor") or "").strip()
    action = str(scene.get("action") or "").strip()
    idea = str(intent.get("idea") or "").strip()
    meaning = " ".join(part for part in (metaphor, idea, action) if part)
    on_screen = [str(item) for item in (scene.get("onScreenText") or []) if str(item).strip()]
    goods = intent.get("subject") == "goods" or "hàng" in dialogue.lower()
    visual = meaning
    if goods:
        visual = (
            "Chỉ hai nhóm hàng, không người, không màn hình, không chữ. "
            "Nhóm bán nhanh là phần nhỏ, gọn, tách riêng. "
            "Nhóm nằm lâu là khối lớn, nặng, chiếm gần hết hình, trông đã nằm yên. "
            "Khác biệt hai nhóm phải đọc được ngay. Hộp và lọ để trắng, không nhãn."
        )
    forbidden = [
        "Không vẽ số liệu, giá, phần trăm, tên thương hiệu, mã lô hay hạn dùng.",
        "Không vẽ chữ lên ảnh.",
    ]
    if on_screen:
        forbidden.append("Không vẽ các dòng chữ màn hình: " + "; ".join(on_screen) + ".")
    prompt = "\n".join([
        "SCENE",
        dialogue,
        "MEANING",
        meaning,
        "VISUAL",
        visual,
        "IMMUTABLE",
        "Không thêm số, giá, tên hãng hoặc dữ liệu không có trong cảnh.",
        "FORBIDDEN",
        " ".join(forbidden),
    ])
    return {
        "route": "AI_VISUAL",
        "send": True,
        "provider": "gemini",
        "prompt": prompt,
        "visualSource": "AI_VISUAL",
        "role": "product" if goods else "still",
        "immutable": ["no numbers", "no brands", "no prices"],
        "forbidden": forbidden,
        "contentKey": scene.get("sceneId"),
        "reason": "Ảnh mới phải truyền đúng ý cảnh.",
    }


def _motion_route(scene: dict) -> dict:
    from kit_video.motion import motion_instruction

    prompt = motion_instruction(scene, {"durationSeconds": scene.get("durationSeconds") or 5})
    return {
        "route": "IMAGE_TO_VIDEO",
        "send": False,
        "provider": "motion",
        "prompt": prompt,
        "visualSource": "",
        "immutable": ["source image pixels"],
        "forbidden": ["Không thiết kế lại ảnh gốc."],
        "contentKey": scene.get("sceneId"),
        "reason": "Chuyển động chỉ nhận hướng dẫn máy quay, hành động và thời lượng.",
    }


def select_still(need: dict, assets: list | None = None, *, park: dict | None = None) -> dict:
    """Choose a still source. Picture roles never fall back to code."""
    if isinstance(need.get("visualDemand"), dict):
        supplied = need.get("resolution") if isinstance(need.get("resolution"), dict) else None
        code = str((supplied or {}).get("code") or "")
        if code in {"REUSE", "BOUND", "GENERATE_LOCAL", "NO_FIT"}:
            provider = {"REUSE": "library", "BOUND": "library", "GENERATE_LOCAL": "local"}.get(code, "blocked")
            asset_id = supplied.get("assetId") if isinstance(supplied.get("assetId"), str) else None
            decision = _decision(provider, code, 0.0, str(supplied.get("reason") or "Resolver hình dùng chung."), asset_id)
            decision["paidProviderCalls"] = 0
            return decision
        return _decision(
            "visual-intelligence",
            "NEED_RESOLVER",
            0.0,
            "Cảnh có nhu cầu hình. Không lấy asset khác nội dung và không gọi provider từ đây.",
        )
    role = str(need.get("role") or "")
    found = find_reuse(need, list(assets or []))
    if found is not None:
        return _decision("library", "REUSE", 0.0, "Dùng asset đã có, không tạo lại.", str(found.get("assetId") or ""))
    if role in CODE_ROLES:
        return _decision("code", "CODE", 0.0, "Thành phần cần độ chính xác được vẽ bằng code.")
    if role not in PICTURE_ROLES:
        return _decision("blocked", "UNKNOWN_ROLE", 0.0, "Chưa có vai trò ảnh tương ứng.")
    shared = park_image(park)
    if shared["authorized"]:
        decision = _decision("gemini", "SHARED", 0.0, shared["reason"])
        decision["model"] = shared["model"]
        decision["separateConfig"] = False
        decision["configSource"] = "tenant-ai"
        return decision
    decision = _decision(
        "generated",
        "NOT_INTEGRATED",
        0.0,
        shared["reason"] if park else (
            "Chưa có provider ảnh tĩnh nào được xác minh đơn giá, điều khoản và quyền truy cập. Không chuyển sang vẽ bằng code."
        ),
    )
    decision["separateConfig"] = False
    return decision


def submit_still(request: dict, transport=None) -> dict:
    """Local decisions stay local. A shared Gemini decision is not sent from here."""
    if request.get("code") == "SHARED":
        if transport is not None:
            raise RuntimeError("Ảnh Gemini chỉ gửi từ execute_still khi park đã cấp quyền.")
        return {
            "sent": False,
            "status": "READY",
            "code": "SHARED",
            "providerExecuted": False,
            "reason": request.get("reason") or "",
        }
    if request.get("provider") == "generated" or request.get("code") == "NOT_INTEGRATED":
        return {
            "sent": False,
            "status": "BLOCKED",
            "code": "NOT_INTEGRATED",
            "providerExecuted": False,
            "reason": STILL_PROVIDERS["generated"]["reason"],
        }
    if transport is not None:
        raise RuntimeError("Still provider không có adapter mạng.")
    return {"sent": False, "status": "LOCAL", "code": request.get("code"), "providerExecuted": False}


def _png(payload: object) -> bool:
    return isinstance(payload, (bytes, bytearray)) and bytes(payload).startswith(b"\x89PNG\r\n\x1a\n")


def _image_file(path: Path) -> bool:
    try:
        from PIL import Image

        with Image.open(path) as image:
            image.load()
        return True
    except (OSError, ValueError):
        return False


def _record(need: dict, decision: dict, submission: dict) -> dict:
    return {
        "role": need.get("role"),
        "contentKey": need.get("contentKey"),
        "provider": decision.get("provider"),
        "code": decision.get("code"),
        "status": "blocked",
        "providerExecuted": False,
        "success": False,
        "sent": bool(submission.get("sent")),
        "costUsd": 0,
        "candidate": None,
        "sha256": None,
        "approval": "proposed",
        "usageRights": "unverified",
        "commercial": False,
        "origin": None,
        "reason": submission.get("reason") or decision.get("reason") or "",
        "technical": "BLOCKED",
        "semantic": "unreviewed",
    }


def _store_fixture(need: dict, source: Path, dest: Path) -> dict:
    """Copy a local fixture into a new candidate file. This is not a provider result."""
    decision = select_still({"role": "type"}, [])
    record = _record(need, decision, {"sent": False, "reason": ""})
    record["origin"] = "fixture"
    record["provider"] = "fixture"
    record["code"] = "FIXTURE"
    if dest.exists():
        record["status"] = "blocked"
        record["reason"] = "Candidate đã có. Không ghi đè."
        return record
    if not source.is_file() or not _image_file(source):
        record["status"] = "invalid_output"
        record["reason"] = "Fixture không phải tệp ảnh đọc được."
        return record
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(source.read_bytes())
    digest = hashlib.sha256(dest.read_bytes()).hexdigest()
    if digest != hashlib.sha256(source.read_bytes()).hexdigest() or not _image_file(dest):
        dest.unlink(missing_ok=True)
        record["status"] = "failed"
        record["reason"] = "Tệp candidate không khớp nguồn."
        return record
    record.update({
        "status": "stored",
        "success": True,
        "candidate": str(dest),
        "sha256": digest,
        "approval": "proposed",
        "usageRights": "unverified",
        "commercial": False,
        "technical": "PASS",
        "reason": "Đã lưu fixture cục bộ. Chưa duyệt và không phải ảnh của provider.",
    })
    return record


def _shared_request(need: dict, decision: dict) -> dict:
    """What a host transport may see. Never a secret and never a second config key."""
    return {
        "provider": decision.get("provider"),
        "model": decision.get("model"),
        "role": need.get("role"),
        "contentKey": need.get("contentKey"),
        "prompt": need.get("prompt") or "",
        "route": need.get("route") or "",
        "references": [str(item) for item in (need.get("references") or []) if str(item).strip()],
        "aspectRatio": need.get("aspectRatio") or "16:9",
    }


def _store_shared(need: dict, decision: dict, dest: Path, payload: bytes) -> dict:
    record = _record(need, decision, {"sent": True, "reason": decision.get("reason") or ""})
    record["provider"] = "gemini"
    record["code"] = "SHARED"
    record["model"] = decision.get("model")
    record["separateConfig"] = False
    record["configSource"] = "tenant-ai"
    record["origin"] = "park"
    source = str(need.get("visualSource") or "AI_VISUAL")
    record["visualSource"] = source if source in {"AI_VISUAL", "AI_RECONSTRUCTED_UI"} else "AI_VISUAL"
    record["sent"] = True
    record["providerExecuted"] = True
    record["costUsd"] = 0
    if dest.exists():
        record["status"] = "blocked"
        record["sent"] = False
        record["providerExecuted"] = False
        record["reason"] = "Candidate đã có. Không ghi đè."
        return record
    if not _png(payload):
        record["status"] = "invalid_output"
        record["success"] = False
        record["reason"] = "Ảnh từ cấu hình chung không phải PNG hợp lệ."
        return record
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(payload)
    digest = hashlib.sha256(dest.read_bytes()).hexdigest()
    if digest != hashlib.sha256(payload).hexdigest() or not _image_file(dest):
        dest.unlink(missing_ok=True)
        record["status"] = "failed"
        record["reason"] = "Tệp candidate không khớp ảnh vừa nhận."
        return record
    record.update({
        "status": "stored",
        "success": True,
        "candidate": str(dest),
        "sha256": digest,
        "approval": "proposed",
        "usageRights": "unverified",
        "commercial": False,
        "technical": "PASS",
        "reason": "Đã lưu ảnh từ cấu hình AI dùng chung. Chưa duyệt và chưa đưa vào phim.",
    })
    return record


def execute_still(
    need: dict,
    assets: list | None = None,
    *,
    source: Path | None = None,
    dest: Path | None = None,
    adapter=None,
    park: dict | None = None,
    transport=None,
) -> dict:
    """Run the still contract. A mock or a missing provider never counts as a generated image.

    Shared Gemini runs only when the park document authorizes it and the host passes the
    existing transport. This function does not read an API key and does not open a socket.
    """
    if source is not None or dest is not None:
        if park is None and transport is None:
            if source is None or dest is None:
                record = _record(need, {"provider": "fixture", "code": "FIXTURE", "reason": ""}, {"sent": False})
                record["status"] = "failed"
                record["reason"] = "Lưu fixture cần cả tệp nguồn và đường candidate mới."
                return record
            return _store_fixture(need, source, dest)
    if str(need.get("visualSource") or "") == "SOFTWARE_SCREENSHOT" or need.get("route") == "SOFTWARE_SCREENSHOT":
        record = _record(need, {"provider": "composite", "code": "SCREENSHOT", "reason": ""}, {"sent": False})
        record["status"] = "composite"
        record["visualSource"] = "SOFTWARE_SCREENSHOT"
        record["sent"] = False
        record["providerExecuted"] = False
        record["reason"] = "Ảnh chụp gốc được đặt vào frame. Không gửi Gemini."
        return record
    decision = select_still(need, list(assets or []), park=park)
    if decision.get("code") == "SHARED":
        record = _record(need, decision, {"sent": False, "reason": decision.get("reason") or ""})
        record["model"] = decision.get("model")
        record["separateConfig"] = False
        record["configSource"] = "tenant-ai"
        if transport is None or dest is None:
            record["status"] = "ready"
            record["reason"] = "Cấu hình AI dùng chung đã sẵn. Lần gọi này không gửi yêu cầu."
            return record
        if dest.exists():
            record["status"] = "blocked"
            record["reason"] = "Candidate đã có. Không gửi yêu cầu mới."
            return record
        try:
            result = transport(_shared_request(need, decision))
        except Exception as exc:
            record["status"] = "failed"
            record["reason"] = str(exc)
            return record
        if not isinstance(result, dict) or result.get("ok") is not True:
            record["status"] = "failed"
            record["sent"] = bool(isinstance(result, dict) and result.get("sent"))
            record["providerExecuted"] = record["sent"]
            record["reason"] = str((result or {}).get("error") or "Cấu hình chung không trả ảnh.") if isinstance(result, dict) else "Cấu hình chung không trả ảnh."
            return record
        return _store_shared(need, decision, dest, bytes(result.get("bytes") or b""))
    submission = submit_still(decision)
    if decision.get("code") == "NOT_INTEGRATED" or submission.get("code") == "NOT_INTEGRATED":
        record = _record(need, decision, submission)
        record["status"] = "not_integrated"
        if adapter is None:
            return record
        try:
            result = adapter(decision)
        except Exception as exc:
            record["status"] = "failed"
            record["origin"] = "mock"
            record["reason"] = str(exc)
            return record
        record["origin"] = "mock"
        if not isinstance(result, dict) or result.get("ok") is not True or not _png(result.get("bytes")):
            record["status"] = "invalid_output" if isinstance(result, dict) and result.get("ok") is True else "failed"
            record["reason"] = str((result or {}).get("error") or "Đầu ra mock không phải PNG hợp lệ.") if isinstance(result, dict) else "Mock không trả kết quả."
            return record
        record["status"] = "mock_only"
        record["reason"] = "Mock chỉ dùng trong test. Không ghi success và không gọi provider."
        return record
    if source is not None and dest is not None:
        return _store_fixture(need, source, dest)
    record = _record(need, decision, submission)
    record["status"] = "local" if decision.get("code") in {"CODE", "REUSE"} else "blocked"
    record["technical"] = "PASS" if record["status"] == "local" else "BLOCKED"
    return record


def candidate_row(record: dict, asset_id: str, role: str) -> dict:
    """A stored candidate enters the existing registry shape as proposed, never approved."""
    return {
        "assetId": asset_id,
        "kind": "image",
        "source": "user_supplied",
        "path": record.get("candidate"),
        "pathBase": "absolute",
        "version": "candidate",
        "usageRights": record.get("usageRights") or "unverified",
        "approval": "proposed",
        "role": role,
        "sha256": record.get("sha256"),
        "origin": record.get("origin"),
        "providerExecuted": False,
    }


def registry_view(assets: list[dict], scenes: list[dict]) -> list[dict]:
    rows = []
    for row in assets:
        if not isinstance(row, dict):
            continue
        asset_id = row.get("assetId")
        used = []
        for scene in scenes:
            if not isinstance(scene, dict):
                continue
            if asset_id in referenced_ids(scene):
                used.append(scene.get("sceneId"))
        decision = inspect_asset(row)
        decision["scenes"] = used
        rows.append(decision)
    return rows


def goods_source(scene: dict, assets: dict) -> dict:
    """A goods scene needs a cleared product picture. Code cards do not satisfy it."""
    intent = (scene.get("params") or {}).get("intent") if isinstance(scene.get("params"), dict) else None
    subject = str((intent or {}).get("subject") or "")
    if subject != "goods":
        return {"status": "not_required", "code": "", "assets": []}
    cleared = []
    for asset_id in referenced_ids(scene):
        row = assets.get(asset_id)
        if not isinstance(row, dict) or str(row.get("role") or "") != "product":
            continue
        if inspect_asset(row)["allowed"]:
            cleared.append(asset_id)
    if not cleared:
        return {
            "status": "blocked",
            "code": "product_source_missing",
            "assets": [],
            "reason": "Cảnh hàng hóa chưa có ảnh sản phẩm đã duyệt, có đường dẫn và quyền sử dụng.",
        }
    return {"status": "ready", "code": "", "assets": cleared, "reason": ""}


def access_report() -> dict:
    """Presence only. Values of secrets are never returned."""
    return {
        "credentialsPresent": {name: bool(os.environ.get(name, "").strip()) for name in _CREDENTIAL_NAMES},
        "stillProviderIntegrated": False,
        "providers": STILL_PROVIDERS,
        "spentUsd": 0,
        "network": False,
    }

"""Shared picture planning.

A scene intent becomes one plan: what must be shown, which asset can show it,
and which gate can close. The module does not paint, does not approve an asset,
and does not name a brand or a film.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from kit_video.composition import family_for, scene_intent
from kit_video.still import confirm_file, goods_source, inspect_asset, referenced_ids, select_still, submit_still


MAX_ATTEMPTS = 3
ASSET_ERROR_CODES = frozenset({
    "product_source_missing",
    "official_ui_missing",
    "setting_missing",
    "portrait_missing",
    "logo_missing",
    "pose_unsupported",
})
QUALITY_ERROR_CODES = frozenset({"false_comparison"})
_ACTION_POSES = frozenset({"observe", "decide", "mark", "choose", "reach", "grasp", "point"})
_SPECS = {
    "still": {"kind": "setting", "roles": ("setting",), "method": "photographic", "semantic": True, "missing": "setting_missing"},
    "portrait": {"kind": "person", "roles": ("character", "portrait"), "method": "portrait", "semantic": True, "missing": "portrait_missing"},
    "goods": {"kind": "product", "roles": ("product",), "method": "photographic", "semantic": True, "missing": "product_source_missing"},
    "transaction": {"kind": "ui", "roles": ("product_ui", "reference"), "method": "screenshot", "semantic": True, "missing": "official_ui_missing"},
    "relation": {"kind": "diagram", "roles": (), "method": "code", "semantic": True, "missing": ""},
    "lockup": {"kind": "logo", "roles": ("brand_logo", "logo"), "method": "code", "semantic": True, "missing": "logo_missing"},
    "schedule": {"kind": "diagram", "roles": (), "method": "code", "semantic": False, "missing": ""},
    "record": {"kind": "type", "roles": (), "method": "code", "semantic": False, "missing": ""},
    "stock": {"kind": "diagram", "roles": (), "method": "code", "semantic": False, "missing": ""},
    "statement": {"kind": "type", "roles": (), "method": "code", "semantic": False, "missing": ""},
    "mark": {"kind": "mark", "roles": (), "method": "code", "semantic": False, "missing": ""},
}
_REQUESTS = {
    "setting_missing": "Cần một ảnh bối cảnh, role setting, approval approved, quyền sử dụng là một câu cụ thể.",
    "portrait_missing": "Cần một ảnh người, role character hoặc portrait, approval approved. Ảnh tĩnh không được gán thêm hành động.",
    "product_source_missing": "Cần ảnh sản phẩm, role product, approval approved, quyền sử dụng là một câu cụ thể.",
    "official_ui_missing": "Cần ảnh giao diện đã duyệt, kind screenshot, không phải demo, không phải mockup, và phải được gắn vào cảnh.",
    "logo_missing": "Cần logo đã duyệt, role brand_logo, dùng nguyên tệp.",
    "pose_unsupported": "Cần một tài sản khai báo đúng hành động. Ảnh chân dung tĩnh không được vẽ thành người đang thao tác.",
}


def _params(scene: dict) -> dict:
    value = scene.get("params")
    return value if isinstance(value, dict) else {}


def _actions(params: dict) -> list[str]:
    picture = params.get("picture") if isinstance(params.get("picture"), dict) else {}
    raw = picture.get("actions")
    if isinstance(raw, list):
        return [str(item) for item in raw if isinstance(item, str) and item]
    one = picture.get("action")
    if isinstance(one, str) and one.strip():
        return [one.strip()]
    return []


def _chart(params: dict) -> dict:
    chart = params.get("chart") if isinstance(params.get("chart"), dict) else None
    if chart is None:
        return {"status": "not_required", "code": "", "reason": ""}
    amounts = []
    for item in chart.get("bars") or []:
        if isinstance(item, dict) and isinstance(item.get("amount"), (int, float)):
            amounts.append(float(item["amount"]))
    if len(set(amounts)) > 1 and chart.get("confirmed") is not True:
        return {
            "status": "fail",
            "code": "false_comparison",
            "reason": "Các cột có độ dài khác nhau nhưng chưa được xác nhận là một so sánh. Không vẽ chúng thành kết quả.",
        }
    return {"status": "illustrative", "code": "", "reason": "Biểu đồ chỉ là minh họa khi không có so sánh đã xác nhận."}


def _bound(scene: dict, assets: dict, roles: tuple[str, ...]) -> tuple[list[str], list[dict]]:
    found = []
    refused = []
    for asset_id in referenced_ids(scene):
        row = assets.get(asset_id)
        if not isinstance(row, dict) or str(row.get("role") or "") not in roles:
            continue
        decision = inspect_asset(row)
        if decision["allowed"]:
            found.append(asset_id)
        else:
            refused.append(decision)
    return found, refused


def _clearance(scene: dict, assets: dict, spec: dict) -> dict:
    if spec["missing"] == "product_source_missing":
        goods = goods_source(scene, assets)
        if goods["status"] == "blocked":
            return {"status": "blocked", "code": goods["code"], "assets": [], "request": _REQUESTS[goods["code"]]}
        return {"status": "ready", "code": "", "assets": goods.get("assets") or [], "request": ""}
    if not spec["roles"]:
        return {"status": "not_required", "code": "", "assets": [], "request": ""}
    found, refused = _bound(scene, assets, spec["roles"])
    if found:
        return {"status": "ready", "code": "", "assets": found, "request": ""}
    if refused:
        return {
            "status": "blocked",
            "code": "asset_not_cleared",
            "assets": [],
            "request": refused[0]["reason"],
        }
    code = spec["missing"]
    return {"status": "blocked", "code": code, "assets": [], "request": _REQUESTS.get(code, "")}


def _pose(actions: list[str], clearance: dict, assets: dict) -> dict:
    needed = [item for item in actions if item in _ACTION_POSES]
    if not needed:
        return {"status": "not_required", "code": "", "actions": [], "reason": ""}
    supported: set[str] = set()
    for asset_id in clearance.get("assets") or []:
        row = assets.get(asset_id) if isinstance(assets.get(asset_id), dict) else {}
        for item in row.get("actions") or []:
            if isinstance(item, str):
                supported.add(item)
    missing = [item for item in needed if item not in supported]
    if missing:
        return {
            "status": "blocked",
            "code": "pose_unsupported",
            "actions": missing,
            "reason": _REQUESTS["pose_unsupported"],
        }
    return {"status": "ready", "code": "", "actions": needed, "reason": ""}


def visual_plan(scene: dict, assets: dict | None = None) -> dict:
    """One structured picture brief. Missing fields stay empty; they are not invented."""
    params = _params(scene)
    intent = scene_intent(params)
    subject = intent["subject"]
    spec = _SPECS.get(subject, {"kind": "unspecified", "roles": (), "method": "unspecified", "semantic": True, "missing": ""})
    library = assets or {}
    clearance = _clearance(scene, library, spec)
    actions = _actions(params)
    pose = _pose(actions, clearance, library)
    exact = [str(item) for item in (scene.get("onScreenText") or []) if isinstance(item, str)]
    illustrative = []
    if isinstance(params.get("metrics"), list):
        illustrative.append("metrics")
    if isinstance(params.get("chart"), dict):
        illustrative.append("chart")
    if isinstance(params.get("disclosure"), str) and params.get("disclosure"):
        illustrative.append("mockup")
    criteria = ["safe_area", "type_hierarchy", "exact_copy"]
    if spec["semantic"]:
        criteria.append("meaning")
    if spec["kind"] == "product":
        criteria.append("product_picture")
    if spec["kind"] == "ui":
        criteria.append("official_interface")
    return {
        "sceneId": str(scene.get("sceneId") or ""),
        "purpose": str(scene.get("storyGoal") or intent["idea"]),
        "idea": intent["idea"],
        "subject": subject,
        "action": str(scene.get("action") or ""),
        "relation": str(scene.get("visualMetaphor") or ""),
        "family": family_for(params, str(params.get("kind") or "")),
        "pictureKind": spec["kind"],
        "method": spec["method"],
        "exact": exact,
        "illustrative": illustrative,
        "semantic": bool(spec["semantic"]),
        "criteria": criteria,
        "clearance": clearance,
        "pose": pose,
        "chart": _chart(params),
        "requiredAsset": clearance.get("request") or pose.get("reason") or "",
    }


_FINDING_LAYER = {
    "product_source_missing": "shot",
    "official_ui_missing": "shot",
    "ui_unpriced": "shot",
    "ui_empty": "shot",
    "ui_inconsistent": "shot",
    "false_comparison": "shot",
    "semantic_unreviewed": "semantic",
}
_FINDING_REMEDY = {
    "product_source_missing": "Gắn asset role product đã duyệt, có quyền dùng và hash khớp.",
    "official_ui_missing": "Gắn ảnh giao diện role product_ui hoặc reference đã được inspect_asset cho phép.",
    "ui_unpriced": "Khai params.evidence với số lượng, đơn giá, thành tiền và tổng, hoặc thay bảng mẫu.",
    "ui_empty": "Thêm dòng hàng có giá trị thanh toán trước khi dùng ảnh này làm bằng chứng.",
    "ui_inconsistent": "Sửa số lượng, đơn giá, thành tiền hoặc tổng cho khớp.",
    "false_comparison": "Bỏ các cột chưa được xác nhận, hoặc chỉ vẽ khi chart.confirmed là true.",
    "semantic_unreviewed": "Người xem hình và ghi kết quả. Hash khớp không thay cho duyệt mặt hình.",
    "semantic_fail": "Người xem lại frame đã ghi trong visualReview. Máy không đổi kết quả này thành đạt.",
}


def candidate_id(scene: dict) -> str:
    """Identity of the picture a person reviewed. A later edit of that picture does not keep the old id."""
    payload = {
        "sceneId": scene.get("sceneId") or "",
        "templateId": scene.get("templateId") or "",
        "action": scene.get("action") or "",
        "onScreenText": scene.get("onScreenText") or [],
        "assets": scene.get("assets") or [],
        "params": scene.get("params") or {},
        "previewFractions": scene.get("previewFractions") or [],
    }
    raw = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()


def accepted_review(scene: dict) -> dict | None:
    """The scene's own visualReview, and only while it still names this candidate."""
    review = scene.get("visualReview")
    if not isinstance(review, dict):
        return None
    if str(review.get("sceneId") or "") != str(scene.get("sceneId") or ""):
        return None
    frame = review.get("frame")
    if not isinstance(frame, str) or not frame.strip():
        return None
    if str(review.get("candidate") or "") != candidate_id(scene):
        return None
    if review.get("status") not in {"PASS", "FAIL", "REVISE"}:
        return None
    return review


def scene_findings(scene: dict, assets=None) -> list[dict]:
    """Every shot condition visible now. A later pass is not required to reveal the next one."""
    library = assets or {}
    plan = visual_plan(scene, library if isinstance(library, dict) else {})
    gate = production_gate(plan, review=accepted_review(scene))
    scene_id = str(scene.get("sceneId") or "")
    found: list[dict] = []

    def add(code: str, reason: str) -> None:
        if any(item["code"] == code for item in found):
            return
        found.append({
            "code": code,
            "path": scene_id,
            "layer": _FINDING_LAYER.get(code, "shot"),
            "reason": reason,
            "remedy": _FINDING_REMEDY.get(code, ""),
        })

    for item in list(gate.get("blockers") or []) + list(gate.get("failures") or []):
        code = str(item.get("code") or "")
        if not code:
            continue
        if code == "semantic_unreviewed" and plan.get("subject") not in {"still", "portrait", "goods", "transaction", "relation", "lockup"}:
            continue
        add(code, str(item.get("reason") or ""))
    if gate.get("semantic") == "unreviewed" and plan.get("subject") in {"still", "portrait", "goods", "transaction", "relation", "lockup"}:
        add("semantic_unreviewed", "Hình có chủ thể cần người xem xét. Máy không tự cho đạt.")
    evidence = ui_evidence(scene)
    if evidence.get("code"):
        add(str(evidence["code"]), str(evidence.get("reason") or ""))
    return found


def production_gate(plan: dict, findings: list | None = None, review: dict | None = None) -> dict:
    """PASS only when every required layer is closed. A review cannot drop a criterion."""
    blockers = []
    failures = []
    clearance = plan.get("clearance") if isinstance(plan.get("clearance"), dict) else {}
    if clearance.get("status") == "blocked":
        blockers.append({"code": clearance.get("code") or "asset_missing", "class": "blocked", "reason": clearance.get("request") or ""})
    pose = plan.get("pose") if isinstance(plan.get("pose"), dict) else {}
    if pose.get("status") == "blocked":
        blockers.append({"code": "pose_unsupported", "class": "blocked", "reason": pose.get("reason") or ""})
    chart = plan.get("chart") if isinstance(plan.get("chart"), dict) else {}
    if chart.get("status") == "fail":
        failures.append({"code": chart.get("code") or "false_comparison", "class": "automatic", "reason": chart.get("reason") or ""})
    for item in findings or []:
        if isinstance(item, dict) and item.get("code"):
            failures.append({"code": item["code"], "class": item.get("class") or "automatic", "reason": item.get("reason") or ""})
    criteria = list(plan.get("criteria") or [])
    semantic = "not_required"
    if plan.get("semantic"):
        semantic = "unreviewed"
        if isinstance(review, dict) and review.get("status") in {"PASS", "FAIL"}:
            dropped = [item for item in criteria if item not in (review.get("criteria") or [])]
            if dropped:
                blockers.append({
                    "code": "standard_lowered",
                    "class": "blocked",
                    "reason": "Không bỏ tiêu chí sau khi xem kết quả: " + ", ".join(dropped) + ".",
                })
            elif review.get("status") == "FAIL":
                failures.append({"code": "semantic_fail", "class": "semantic", "reason": str(review.get("reason") or "Đánh giá thị giác không đạt.")})
                semantic = "fail"
            else:
                semantic = "pass"
    if blockers:
        status = "BLOCKED"
    elif failures:
        status = "FAIL"
    elif semantic == "unreviewed":
        status = "BLOCKED"
        blockers.append({
            "code": "semantic_unreviewed",
            "class": "semantic",
            "reason": "Hình có chủ thể cần người xem xét. Máy không tự cho đạt.",
        })
    else:
        status = "PASS"
    return {
        "status": status,
        "semantic": semantic,
        "blockers": blockers,
        "failures": failures,
        "criteria": criteria,
    }


def _resolve(row: dict, project_dir: Path, repo_root: Path) -> Path | None:
    raw = row.get("path")
    if not isinstance(raw, str) or not raw:
        return None
    candidate = Path(raw)
    base_name = row.get("pathBase", "project")
    if base_name == "absolute" or candidate.is_absolute():
        return candidate
    base = repo_root if base_name == "repo" else project_dir
    return (base / candidate).resolve()


def select_scene_asset(scene: dict, assets=None, project_dir: Path | None = None, repo_root: Path | None = None) -> dict:
    """The bound asset for this picture. A different role is never substituted."""
    library, _rows = _library(assets)
    plan = visual_plan(scene, library)
    clearance = plan["clearance"]
    result = {
        "sceneId": plan["sceneId"],
        "pictureKind": plan["pictureKind"],
        "method": plan["method"],
        "assetId": None,
        "role": None,
        "approval": None,
        "usageRights": None,
        "path": None,
        "substituted": False,
        "integrity": None,
        "reason": "",
    }
    if clearance.get("status") == "blocked":
        result.update(status="blocked", code=clearance.get("code") or "asset_missing", reason=clearance.get("request") or "")
        return result
    chosen = list(clearance.get("assets") or [])
    if not chosen:
        result.update(status="not_required", code="", reason="")
        return result
    asset_id = chosen[0]
    row = library.get(asset_id) or {}
    result.update(
        assetId=asset_id,
        role=row.get("role"),
        approval=row.get("approval"),
        usageRights=row.get("usageRights"),
    )
    if project_dir is None:
        result.update(status="unchecked", code="hash_unrecorded", reason="Chưa kiểm file. Hash chưa được xác minh.")
        return result
    path = _resolve(row, project_dir, repo_root or project_dir)
    check = confirm_file(row, path)
    result["integrity"] = {
        "exists": bool(check.get("exists")),
        "sha256": check.get("sha256"),
        "match": check.get("match"),
        "recorded": bool(check.get("recorded")),
    }
    result["path"] = str(path) if path is not None else None
    if not check.get("exists"):
        result["integrity"]["verified"] = False
        result.update(status="blocked", code="asset_missing", reason="Không thấy file của asset đã chọn.")
        return result
    if check.get("match") is False:
        result["integrity"]["verified"] = False
        result.update(status="blocked", code="asset_hash", reason="File không khớp sha256 đã ghi. Không cập nhật hash mới.")
        return result
    verified = bool(check.get("recorded") and check.get("match") is True)
    result["integrity"]["verified"] = verified
    if not verified:
        commercial_use = str(scene.get("approvalStatus") or "") == "approved"
        result.update(
            status="blocked" if commercial_use else "unverified",
            code="hash_unrecorded",
            reason="Registry chưa có sha256. Cần ghi nhận hash và xác minh lại trước commercial render. Không tự cập nhật hash và không đổi approval.",
        )
        return result
    result.update(status="ready", code="REUSE", reason="")
    return result


def ui_evidence(scene: dict) -> dict:
    """Number consistency for a selling screen. A match is not an approval."""
    params = _params(scene)
    subject = scene_intent(params)["subject"]
    base = {"commercial": False, "consistent": None}
    if subject != "transaction":
        return {**base, "status": "not_required", "code": "", "reason": ""}
    evidence = params.get("evidence") if isinstance(params.get("evidence"), dict) else None
    lines = evidence.get("lines") if isinstance(evidence, dict) else None
    if isinstance(lines, list) and lines:
        total = 0.0
        broken = False
        for item in lines:
            if not isinstance(item, dict):
                broken = True
                continue
            qty, price, amount = item.get("qty"), item.get("price"), item.get("amount")
            if not all(isinstance(value, (int, float)) and not isinstance(value, bool) for value in (qty, price, amount)):
                broken = True
                continue
            if abs(float(qty) * float(price) - float(amount)) > 0.01:
                broken = True
            total += float(amount)
        stated = evidence.get("total")
        if not isinstance(stated, (int, float)) or isinstance(stated, bool) or abs(float(stated) - total) > 0.01:
            broken = True
        if total == 0:
            return {**base, "status": "blocked", "code": "ui_empty", "consistent": False, "reason": "Giỏ không có giá trị thanh toán."}
        if broken:
            return {**base, "status": "blocked", "code": "ui_inconsistent", "consistent": False, "reason": "Số lượng, đơn giá, thành tiền hoặc tổng không khớp."}
        return {**base, "status": "consistent", "code": "", "consistent": True, "reason": "Phép tính khớp. Đây chưa phải approval của ảnh giao diện."}
    table = params.get("table") if isinstance(params.get("table"), dict) else {}
    rows = [row for row in (table.get("rows") or []) if isinstance(row, list) and row]
    if not rows:
        return {**base, "status": "blocked", "code": "ui_empty", "consistent": False, "reason": "Chưa có dòng hàng để đối chiếu."}
    return {**base, "status": "blocked", "code": "ui_unpriced", "consistent": False, "reason": "Bảng chưa có đơn giá, thành tiền và tổng bằng số để kiểm."}


_PICTURE_ROLE = {"setting": "setting", "product": "product", "person": "character"}


def _library(assets) -> tuple[dict, list]:
    if isinstance(assets, dict):
        return assets, list(assets.values())
    rows = [row for row in (assets or []) if isinstance(row, dict)]
    return {str(row.get("assetId") or ""): row for row in rows}, rows


def select_method(scene: dict, assets=None, *, park: dict | None = None) -> dict:
    """Choose how to make the picture. A missing photograph is not replaced by code."""
    library, rows = _library(assets)
    plan = visual_plan(scene, library)
    gate = production_gate(plan)
    decision = {
        "sceneId": plan["sceneId"],
        "planMethod": plan["method"],
        "pictureKind": plan["pictureKind"],
        "method": "blocked",
        "provider": "none",
        "code": "",
        "stillCode": "",
        "paint": False,
        "commercial": False,
        "sent": False,
        "costUsd": 0,
        "assetIds": list(plan["clearance"].get("assets") or []),
        "criteria": list(plan.get("criteria") or []),
        "semantic": gate["semantic"],
        "gate": gate["status"],
        "reason": "",
    }
    if plan["pose"].get("status") == "blocked":
        decision.update(code="pose_unsupported", reason=plan["pose"].get("reason") or "")
        return decision
    if plan["clearance"].get("status") == "blocked" and plan["method"] in {"photographic", "portrait", "screenshot"}:
        decision.update(code=plan["clearance"].get("code") or "", reason=plan["clearance"].get("request") or "")
        role = _PICTURE_ROLE.get(plan["pictureKind"])
        if role:
            still = select_still({"role": role}, rows, park=park)
            submitted = submit_still(still)
            decision.update(provider=still["provider"], stillCode=still["code"], sent=bool(submitted.get("sent")), costUsd=0)
        return decision
    if plan["chart"].get("status") == "fail":
        decision.update(
            method="code",
            provider="code",
            code="false_comparison",
            paint=False,
            reason=plan["chart"].get("reason") or "",
        )
        return decision
    if plan["clearance"].get("status") == "ready" and plan["method"] in {"photographic", "portrait", "screenshot"}:
        decision.update(
            method="composite" if plan.get("exact") else plan["method"],
            provider="library",
            code="REUSE",
            paint=True,
            commercial=gate["status"] == "PASS",
            reason="Dùng tài sản đã duyệt. Phần nghĩa của hình vẫn cần người xem." if gate["semantic"] == "unreviewed" else "",
        )
        return decision
    if plan["method"] == "code" and plan["clearance"].get("status") != "blocked":
        decision.update(
            method="composite" if plan["clearance"].get("status") == "ready" else "code",
            provider="library+code" if plan["clearance"].get("status") == "ready" else "code",
            code="CODE",
            paint=True,
            commercial=gate["status"] == "PASS",
            reason="Chữ, số liệu, logo hoặc sơ đồ được vẽ bằng code.",
        )
        return decision
    decision.update(code=plan["clearance"].get("code") or "no_method", reason=plan.get("requiredAsset") or "Không có phương pháp phù hợp.")
    return decision


def review_layers(decision: dict, findings: list | None = None) -> dict:
    """Technical checks and semantic review stay separate. One cannot close the other."""
    automatic = [item for item in (findings or []) if isinstance(item, dict)]
    if decision.get("code") in ASSET_ERROR_CODES or decision.get("method") == "blocked":
        technical = "BLOCKED"
    elif decision.get("code") in QUALITY_ERROR_CODES or automatic:
        technical = "FAIL"
    else:
        technical = "PASS"
    semantic = decision.get("semantic") or "unreviewed"
    if technical == "PASS" and semantic in {"pass", "not_required"} and decision.get("commercial"):
        overall = "PASS"
    elif technical == "FAIL" or semantic == "fail":
        overall = "FAIL"
    else:
        overall = "BLOCKED"
    return {
        "technical": technical,
        "semantic": semantic,
        "overall": overall,
        "technicalPassIsNotSemanticPass": technical == "PASS" and semantic != "pass",
    }


def attempt_round(scene: dict, assets=None, history: list | None = None) -> dict:
    """One attempt. A blocked picture is not retried as code, and nothing is sent."""
    decision = select_method(scene, assets)
    proposal = {
        "method": decision["method"],
        "code": decision["code"],
        "criteria": decision["criteria"],
        "status": "PASS" if decision["commercial"] else "BLOCKED",
    }
    return {
        "decision": decision,
        "review": review_layers(decision),
        "attempt": next_attempt(list(history or []), proposal),
        "sent": decision["sent"],
        "costUsd": 0,
    }


def next_attempt(history: list[dict], proposal: dict) -> dict:
    """A retry must change the method. Three misses stop the round."""
    done = [item for item in history if isinstance(item, dict)]
    if len(done) >= MAX_ATTEMPTS:
        return {
            "status": "BLOCKED",
            "code": "attempt_limit",
            "attempt": len(done),
            "reason": "Đã đủ ba phương án. Dừng và xem lại tài sản hoặc cách thể hiện.",
        }
    if done:
        last = done[-1]
        if proposal.get("method") == last.get("method") and proposal.get("code") == last.get("code"):
            return {
                "status": "BLOCKED",
                "code": "same_method",
                "attempt": len(done),
                "reason": "Lần thử lại phải đổi phương pháp theo lỗi đã thấy, không vẽ lại cùng một cách.",
            }
        previous = set(last.get("criteria") or [])
        current = set(proposal.get("criteria") or [])
        if previous and previous - current and proposal.get("status") == "PASS":
            return {
                "status": "BLOCKED",
                "code": "standard_lowered",
                "attempt": len(done),
                "reason": "Không đổi tiêu chí để biến một lần chưa đạt thành đạt.",
            }
    return {"status": "allowed", "code": "", "attempt": len(done) + 1, "reason": ""}

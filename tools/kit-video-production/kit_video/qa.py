"""Technical QA for a rendered cut. A passing file is not an artistic approval."""

from __future__ import annotations

import json
import re
import subprocess
from pathlib import Path

from kit_video.audio import require_ffmpeg
from kit_video.errors import ProductionError, issue
from kit_video.validate import load_project, validate_project
from kit_video.visual_quality import MOCKUP_KINDS, content_coverage


def qa_render(render_dir: Path, project_dir: Path) -> dict:
    require_ffmpeg()
    project, load_errors = load_project(project_dir)
    if project is None or load_errors:
        raise ProductionError("FAIL", load_errors or [issue("project", str(project_dir), "Không mở được dự án.")])
    errors, _warnings = validate_project(project)
    if errors:
        raise ProductionError("FAIL", errors)
    log_path = render_dir / "render-log.json"
    if not log_path.is_file():
        raise ProductionError("BLOCKED", [issue("render_log", str(log_path), "Không có render-log.json.")])
    log = json.loads(log_path.read_text(encoding="utf-8"))
    checks = []
    if log.get("preview"):
        checks.append(_check("video_file", "BLOCKED", "Thư mục này chỉ là preview, chưa có video."))
        return _report(render_dir, checks)
    video = render_dir / "video.mp4"
    if not video.is_file():
        checks.append(_check("video_file", "BLOCKED", "Không có video.mp4 trong thư mục render."))
        return _report(render_dir, checks)
    probe = _probe(video)
    checks.append(_decode(video))
    checks.extend(_streams(probe, log, project["manifest"]))
    checks.append(_black(video))
    checks.append(_audio_levels(video, log, project))
    checks.append(_silence(video, log, project))
    checks.extend(_picture(render_dir, log, project))
    checks.extend(_visual(render_dir, log, project))
    checks.append(_placeholders(log))
    checks.append(_check(
        "audio_listening",
        "NEEDS HUMAN REVIEW",
        "Máy không nghe được giọng đọc, phát âm thương hiệu, nhạc và việc SFX có khớp hành động khi xem liền mạch.",
    ))
    checks.append(_check(
        "acceptance",
        "NEEDS HUMAN REVIEW",
        "Chưa có người nghiệm thu hình và tiếng. finalVideo vẫn null, durationLocked vẫn false.",
    ))
    report = _report(render_dir, checks)
    (render_dir / "qa-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    _contact_sheet(render_dir)
    return report


def _report(render_dir: Path, checks: list[dict]) -> dict:
    design_ids = {"design_system"}
    visual_ids = {"visual_contract", "visual_frames", "visual_composition", "visual_meaning"}
    motion_ids = {"motion_plan"}
    audio_ids = {"audio_levels", "silence", "audio_listening"}
    named = design_ids | visual_ids | motion_ids | audio_ids | {"acceptance"}
    technical = [item for item in checks if item["id"] not in named]
    return {
        "renderDir": str(render_dir),
        "technicalStatus": _rollup(technical),
        "designStatus": _rollup([item for item in checks if item["id"] in design_ids]) if any(item["id"] in design_ids for item in checks) else "PASS",
        "visualStatus": _rollup([item for item in checks if item["id"] in visual_ids]) if any(item["id"] in visual_ids for item in checks) else "PASS",
        "motionStatus": _rollup([item for item in checks if item["id"] in motion_ids]) if any(item["id"] in motion_ids for item in checks) else "PASS",
        "audioStatus": _rollup([item for item in checks if item["id"] in audio_ids]) if any(item["id"] in audio_ids for item in checks) else "PASS",
        "acceptanceStatus": "NEEDS HUMAN REVIEW",
        "overallStatus": _rollup(checks),
        "checks": checks,
    }


def _rollup(checks: list[dict]) -> str:
    statuses = {item["status"] for item in checks}
    if "FAIL" in statuses:
        return "FAIL"
    if "BLOCKED" in statuses:
        return "BLOCKED"
    if "NEEDS REVISION" in statuses:
        return "NEEDS REVISION"
    if "NEEDS HUMAN REVIEW" in statuses:
        return "NEEDS HUMAN REVIEW"
    return "PASS"


def _check(check_id: str, status: str, evidence: str) -> dict:
    return {"id": check_id, "status": status, "evidence": evidence}


def _probe(video: Path) -> dict:
    completed = subprocess.run(
        ["ffprobe", "-v", "error", "-show_format", "-show_streams", "-of", "json", str(video)],
        capture_output=True, text=True, check=False,
    )
    if completed.returncode != 0:
        return {}
    return json.loads(completed.stdout or "{}")


def _decode(video: Path) -> dict:
    completed = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", str(video), "-f", "null", "-"],
        capture_output=True, text=True, check=False,
    )
    if completed.returncode != 0:
        return _check("decode", "FAIL", (completed.stderr or "decode failed")[-500:])
    return _check("decode", "PASS", "ffmpeg giải mã hết hình và tiếng, exit 0.")


def _streams(probe: dict, log: dict, manifest: dict) -> list[dict]:
    streams = probe.get("streams") or []
    video = next((item for item in streams if item.get("codec_type") == "video"), None)
    audio = next((item for item in streams if item.get("codec_type") == "audio"), None)
    checks = []
    if video is None or audio is None:
        checks.append(_check("streams", "FAIL", "Thiếu stream hình hoặc tiếng."))
        return checks
    fmt = manifest["format"]
    expected_frames = sum(item["frames"] for item in log["scenes"])
    expected = expected_frames / float(fmt["frameRate"])
    actual = float((probe.get("format") or {}).get("duration") or 0)
    tolerance = 1.5 / float(fmt["frameRate"]) + 0.08
    duration_ok = abs(actual - expected) <= tolerance
    checks.append(_check(
        "duration",
        "PASS" if duration_ok else "FAIL",
        f"Đo được {actual:.3f}s, lịch render {expected:.3f}s, dung sai {tolerance:.3f}s.",
    ))
    size_ok = int(video.get("width") or 0) == int(fmt["width"]) and int(video.get("height") or 0) == int(fmt["height"])
    checks.append(_check("resolution", "PASS" if size_ok else "FAIL", f"{video.get('width')}x{video.get('height')} so với {fmt['width']}x{fmt['height']}."))
    rate = _ratio(video.get("avg_frame_rate"))
    rate_ok = rate is not None and abs(rate - float(fmt["frameRate"])) < 0.05
    checks.append(_check("frame_rate", "PASS" if rate_ok else "FAIL", f"avg_frame_rate={video.get('avg_frame_rate')}."))
    codec_ok = video.get("codec_name") == "h264" and audio.get("codec_name") == "aac"
    checks.append(_check("codec", "PASS" if codec_ok else "FAIL", f"video={video.get('codec_name')} audio={audio.get('codec_name')}."))
    return checks


def _ratio(value: str | None) -> float | None:
    if not value or "/" not in value:
        return None
    num, den = value.split("/", 1)
    if float(den) == 0:
        return None
    return float(num) / float(den)


def _black(video: Path) -> dict:
    completed = subprocess.run(
        ["ffmpeg", "-i", str(video), "-vf", "blackdetect=d=0.2:pix_th=0.10:pic_th=0.98", "-an", "-f", "null", "-"],
        capture_output=True, text=True, check=False,
    )
    found = re.findall(r"black_start:(\S+)\s+black_end:(\S+)\s+black_duration:(\S+)", completed.stderr or "")
    if found:
        return _check("black_frames", "FAIL", "Phát hiện khung đen: " + "; ".join(",".join(item) for item in found))
    return _check("black_frames", "PASS", "blackdetect không thấy đoạn đen ngoài chủ đích.")


def _audio_levels(video: Path, log: dict, project: dict) -> dict:
    completed = subprocess.run(
        ["ffmpeg", "-i", str(video), "-af", "volumedetect", "-f", "null", "-"],
        capture_output=True, text=True, check=False,
    )
    text = completed.stderr or ""
    max_match = re.search(r"max_volume:\s*(-?\d+(?:\.\d+)?) dB", text)
    mean_match = re.search(r"mean_volume:\s*(-?\d+(?:\.\d+)?) dB", text)
    if not max_match or not mean_match:
        return _check("audio_levels", "BLOCKED", "volumedetect không trả mức.")
    maximum = float(max_match.group(1))
    mean = float(mean_match.group(1))
    if maximum > -0.4:
        return _check("audio_levels", "FAIL", f"Đỉnh {maximum:.2f} dB, có dấu hiệu chạm trần.")
    by_id = {scene["sceneId"]: scene for scene in project["shotPlan"]["scenes"]}
    has_voice = any(by_id[item["sceneId"]]["audio"].get("voiceTrackId") for item in log["scenes"])
    if has_voice and mean < -45:
        return _check("audio_levels", "FAIL", f"Có lời đọc nhưng mean_volume={mean:.2f} dB.")
    return _check("audio_levels", "PASS", f"max={maximum:.2f} dB, mean={mean:.2f} dB.")


def _silence(video: Path, log: dict, project: dict) -> dict:
    minimum = float(project["manifest"]["audioProfile"].get("silenceMinSeconds", 1.2))
    completed = subprocess.run(
        ["ffmpeg", "-i", str(video), "-af", f"silencedetect=n=-36dB:d={minimum}", "-f", "null", "-"],
        capture_output=True, text=True, check=False,
    )
    starts = [float(value) for value in re.findall(r"silence_start:\s*(\S+)", completed.stderr or "")]
    ends = [float(value) for value in re.findall(r"silence_end:\s*(\S+)", completed.stderr or "")]
    regions = list(zip(starts, ends))
    declared = _declared_silences(log, project)
    unexplained = [region for region in regions if not _covered(region, declared)]
    if unexplained:
        shown = ", ".join(f"{start:.2f}-{end:.2f}" for start, end in unexplained)
        return _check("silence", "FAIL", f"Khoảng lặng chưa được khai báo: {shown}.")
    if not regions:
        return _check("silence", "PASS", f"Không có khoảng lặng dài hơn {minimum:.2f}s.")
    return _check("silence", "PASS", f"{len(regions)} khoảng lặng đều có lý do trong shot plan.")


def _declared_silences(log: dict, project: dict) -> list[tuple[float, float]]:
    by_id = {scene["sceneId"]: scene for scene in project["shotPlan"]["scenes"]}
    declared = []
    offset = 0.0
    for item in log["scenes"]:
        scene = by_id[item["sceneId"]]
        for silence in scene["audio"]["intentionalSilences"]:
            declared.append((offset + float(silence["start"]), offset + float(silence["end"])))
        offset += float(item["durationSeconds"])
    return declared


def _covered(region: tuple[float, float], declared: list[tuple[float, float]]) -> bool:
    """A silence is explained when declared windows, taken together, cover it."""
    start, end = region
    cursor = start
    for left, right in sorted(declared):
        if right < cursor - 0.05:
            continue
        if left > cursor + 0.25:
            return False
        cursor = max(cursor, right)
        if cursor >= end - 0.05:
            return True
    return cursor >= end - 0.05


def _picture(render_dir: Path, log: dict, project: dict) -> list[dict]:
    layout_path = render_dir / "layout.json"
    if not layout_path.is_file():
        return [_check("text_safe_area", "BLOCKED", "Không có layout.json để kiểm chữ.")]
    layout = json.loads(layout_path.read_text(encoding="utf-8"))
    violations = layout.get("violations") or []
    checks = [_check(
        "text_safe_area",
        "FAIL" if violations else "PASS",
        f"{len(violations)} chữ vượt vùng an toàn." if violations else "Không ghi nhận chữ vượt vùng an toàn.",
    )]
    texts = layout.get("texts") or []
    by_id = {scene["sceneId"]: scene for scene in project["shotPlan"]["scenes"]}
    missing = []
    for item in log["scenes"]:
        for required in by_id[item["sceneId"]].get("onScreenText") or []:
            if not any(required == shown or required in shown for shown in texts):
                missing.append(required)
    checks.append(_check(
        "required_text",
        "FAIL" if missing else "PASS",
        "Thiếu trên hình: " + "; ".join(missing) if missing else "Các chữ đã khai có mặt trong layout.",
    ))
    drawn = layout.get("drawnAssets") or []
    demo_missing = []
    for item in log["scenes"]:
        scene = by_id[item["sceneId"]]
        assets = {row["assetId"]: row for row in project["manifest"]["assets"]}
        for asset_id in scene.get("assets") or []:
            row = assets.get(asset_id) or {}
            if row.get("demoData") and row.get("demoLabel") == "Dữ liệu minh họa":
                if not any(note.get("assetId") == asset_id and note.get("demoLabel") == "Dữ liệu minh họa" for note in drawn):
                    demo_missing.append(asset_id)
    checks.append(_check(
        "demo_label",
        "FAIL" if demo_missing else "PASS",
        "UI demo thiếu nhãn: " + ",".join(demo_missing) if demo_missing else "Không có UI demo nào thiếu nhãn, hoặc cảnh này không dùng UI demo.",
    ))
    return checks


def _visual(render_dir: Path, log: dict, project: dict) -> list[dict]:
    by_id = {scene["sceneId"]: scene for scene in project["shotPlan"]["scenes"]}
    designed = [
        item for item in log.get("scenes") or []
        if by_id.get(item["sceneId"], {}).get("templateId") == "designed_board"
    ]
    if not designed:
        return [
            _check("design_system", "PASS", "Lần render này không dùng designed board."),
            _check("visual_contract", "PASS", "Lần render này không dùng designed board."),
            _check("visual_composition", "PASS", "Lần render này không dùng designed board."),
            _check("motion_plan", "PASS", "Lần render này không dùng designed board."),
        ]
    layout_path = render_dir / "layout.json"
    layout = json.loads(layout_path.read_text(encoding="utf-8")) if layout_path.is_file() else {}
    decisions = {row.get("sceneId"): row for row in layout.get("visualDecisions") or []}
    missing = [item["sceneId"] for item in designed if decisions.get(item["sceneId"], {}).get("status") != "ok"]
    roles = (layout.get("contract") or {}).get("roles") or {}
    design_ok = "title" in roles and "body" in roles
    checks = [_check(
        "design_system",
        "PASS" if design_ok else "NEEDS REVISION",
        "Contract có bậc chữ title và body." if design_ok else "Layout thiếu bậc chữ của design system.",
    ), _check(
        "visual_contract",
        "NEEDS REVISION" if missing else "PASS",
        "Cảnh chưa qua hợp đồng hình: " + ", ".join(missing) if missing else "Mỗi designed board có quyết định chất lượng đạt.",
    )]
    pictures = {row.get("sceneId"): row for row in layout.get("compositionDecisions") or []}
    principles = (layout.get("artDirection") or {}).get("principles") or []
    weak = []
    for item in designed:
        row = pictures.get(item["sceneId"]) or {}
        if not row.get("family"):
            weak.append(item["sceneId"])
        elif "one-idea" in principles and not (row.get("idea") and row.get("subject") and row.get("focal")):
            weak.append(item["sceneId"])
    checks.append(_check(
        "visual_composition",
        "NEEDS REVISION" if weak else "PASS",
        "Cảnh chưa có họ hình: " + ", ".join(weak) if weak else "Mỗi cảnh designed board có họ hình và ý tưởng khi hồ sơ yêu cầu.",
    ))
    motion_gap = [
        item["sceneId"] for item in designed
        if not isinstance((pictures.get(item["sceneId"]) or {}).get("motion"), dict)
        or (pictures.get(item["sceneId"]) or {}).get("motion", {}).get("end") is None
    ]
    checks.append(_check(
        "motion_plan",
        "NEEDS REVISION" if motion_gap else "PASS",
        "Cảnh thiếu cửa sổ chuyển động: " + ", ".join(motion_gap) if motion_gap else "Mỗi cảnh có trạng thái chuyển động đầu và cuối.",
    ))
    palette = project["manifest"]["visualStyle"]["palette"]["background"]
    background = (int(palette[0]), int(palette[1]), int(palette[2]))
    area = project["manifest"]["visualStyle"]["safeArea"]
    safe = (float(area["top"]), float(area["right"]), float(area["bottom"]), float(area["left"]))
    sparse = []
    folder = render_dir / "frames"
    for item in designed:
        kind = str((by_id[item["sceneId"]].get("params") or {}).get("kind") or "")
        if kind not in MOCKUP_KINDS:
            continue
        frames = sorted(folder.glob(f"{item['sceneId']}-f*.png")) if folder.is_dir() else []
        if not frames:
            sparse.append(item["sceneId"])
            continue
        ratio = content_coverage(frames[-1], background, safe)
        if ratio < 0.5:
            sparse.append(f"{item['sceneId']} {ratio:.2f}")
    checks.append(_check(
        "visual_frames",
        "NEEDS REVISION" if sparse else "PASS",
        "Khung minh họa còn trống: " + ", ".join(sparse) if sparse else "Các khung minh họa chiếm đủ diện tích an toàn.",
    ))
    plans = layout.get("visualPlans") or []
    if plans:
        pending = [
            str(row.get("sceneId") or "")
            for row in plans
            if isinstance(row, dict) and (row.get("gate") or {}).get("semantic") == "unreviewed"
        ]
        checks.append(_check(
            "visual_meaning",
            "NEEDS HUMAN REVIEW" if pending else "PASS",
            "Cảnh có chủ thể hình nhưng chưa có người xem: " + ", ".join(pending) if pending else "Không còn cảnh hình nào chờ người xem, hoặc các cảnh này không yêu cầu.",
        ))
    return checks


def _placeholders(log: dict) -> dict:
    rows = log.get("placeholders") or []
    if not rows:
        return _check("placeholder_disclosure", "PASS", "Lần render này không dùng asset placeholder.")
    return _check("placeholder_disclosure", "PASS", "Placeholder được ghi trong render log, không tính là tài sản hoàn thiện: " + ", ".join(rows))


def _contact_sheet(render_dir: Path) -> None:
    folder = render_dir / "frames"
    if not folder.is_dir():
        return
    frames = sorted(folder.glob("*.png"))
    if not frames:
        return
    from PIL import Image

    chosen = [Image.open(path).convert("RGB") for path in frames[:24]]
    columns = 4 if len(chosen) > 4 else len(chosen)
    width = min(chosen[0].width, 480)
    height = int(chosen[0].height * (width / chosen[0].width))
    rows = (len(chosen) + columns - 1) // columns
    sheet = Image.new("RGB", (width * columns, height * rows), (20, 20, 20))
    for index, thumb in enumerate(chosen):
        sheet.paste(thumb.resize((width, height)), ((index % columns) * width, (index // columns) * height))
    sheet.save(render_dir / "contact-sheet.png")

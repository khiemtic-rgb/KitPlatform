"""Turn a structured script into a proposed shot plan. The plan is not approved."""

from __future__ import annotations

import json
from pathlib import Path


def parse_script(text: str) -> list[dict]:
    segments: list[dict] = []
    current: dict | None = None
    mode: str | None = None
    for raw in text.splitlines():
        line = raw.rstrip()
        if line.startswith("## segment "):
            current = {
                "id": line[len("## segment "):].strip(),
                "on_screen": [],
                "acceptance": [],
                "subjects": [],
                "dialogue": "",
            }
            segments.append(current)
            mode = None
            continue
        if current is None:
            continue
        if mode == "dialogue":
            if line.startswith(" ") or line.startswith("\t"):
                current["dialogue"] += line.strip() + "\n"
                continue
            if line == "":
                continue
            mode = None
        if line.startswith(("on_screen:", "acceptance:", "subjects:")):
            mode = line.split(":", 1)[0]
            continue
        if mode in {"on_screen", "acceptance", "subjects"} and line.strip().startswith("- "):
            current[mode].append(line.strip()[2:].strip())
            continue
        if mode in {"on_screen", "acceptance", "subjects"} and line.strip() == "":
            continue
        if ":" in line and not line.startswith(" "):
            key, _, value = line.partition(":")
            if value.strip() == "|":
                mode = key.strip()
                continue
            current[key.strip()] = value.strip()
            mode = None
    return segments


def propose_shot_plan(
    script_text: str,
    project_id: str,
    chars_per_second: float = 13.0,
    head_seconds: float = 0.4,
    tail_seconds: float = 0.9,
) -> dict:
    if chars_per_second <= 0:
        raise ValueError("chars_per_second phải > 0.")
    warnings = []
    scenes = []
    for segment in parse_script(script_text):
        dialogue = " ".join(segment.get("dialogue", "").split())
        spoken = (len(dialogue) / chars_per_second) if dialogue else 0.0
        estimate = spoken + head_seconds + tail_seconds
        hint = segment.get("duration_hint_seconds")
        timing = "estimated"
        if hint not in (None, ""):
            hint_value = float(hint)
            if estimate > hint_value + 0.25:
                warnings.append({
                    "code": "voice_exceeds_hint",
                    "sceneId": segment["id"],
                    "message": (
                        f"Ước lượng lời {estimate:.1f}s dài hơn gợi ý {hint_value:.1f}s. "
                        "Đề xuất nới cảnh. Không ép giọng đọc nhanh."
                    ),
                })
                timing = "exceeds_hint"
        metaphor = segment.get("metaphor") or None
        if metaphor == "":
            metaphor = None
        transition = segment.get("transition")
        scenes.append({
            "sceneId": segment["id"],
            "approvalStatus": "proposed",
            "storyGoal": segment.get("goal", ""),
            "durationSeconds": round(max(estimate, 0.5), 2),
            "durationSource": "fixed",
            "dialogue": dialogue,
            "action": segment.get("visual", ""),
            "visualMetaphor": metaphor,
            "subjects": segment.get("subjects") or [],
            "onScreenText": segment.get("on_screen") or [],
            "templateId": segment.get("template_hint") or "custom",
            "params": {},
            "assets": [],
            "audio": {
                "voiceTrackId": None,
                "musicAssetId": None,
                "sfx": [],
                "intentionalSilences": [],
                "syncNotes": segment.get("audio_note", ""),
            },
            "transitionOut": None if not transition else {"type": "described", "note": transition},
            "acceptance": segment.get("acceptance") or ["Cần người duyệt lời và hình trước khi dựng."],
            "timing": {"status": timing, "spokenEstimateSeconds": round(spoken, 2)},
        })
    return {
        "schemaVersion": "kit-video-shot-plan/1",
        "projectId": project_id,
        "approvalStatus": "proposed",
        "source": "script-analysis",
        "warnings": warnings,
        "scenes": scenes,
    }


def write_proposal(plan: dict, dest: Path) -> None:
    dest.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding="utf-8")

"""Command line for the KIT video production system."""

from __future__ import annotations

import argparse
import asyncio
import json
import sys
from pathlib import Path

from kit_video.analyze import propose_shot_plan, write_proposal
from kit_video.commercial import produce_commercial
from kit_video.errors import ProductionError
from kit_video.qa import qa_render
from kit_video.render import render_project
from kit_video.sfx import write_placeholders
from kit_video.validate import load_project, validate_project


def main(argv: list[str] | None = None) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(prog="kit-video")
    sub = parser.add_subparsers(dest="command", required=True)

    validate = sub.add_parser("validate")
    validate.add_argument("project")

    analyze = sub.add_parser("analyze")
    analyze.add_argument("script")
    analyze.add_argument("--project-id", required=True)
    analyze.add_argument("--out", required=True)
    analyze.add_argument("--chars-per-second", type=float, default=13.0)

    render = sub.add_parser("render")
    render.add_argument("project")
    render.add_argument("--repo", default="")
    render.add_argument("--scenes", default="")
    render.add_argument("--label", default="cut")
    render.add_argument("--allow-style-proof", action="store_true")
    render.add_argument("--preview-only", action="store_true")

    quality = sub.add_parser("qa")
    quality.add_argument("render_dir")
    quality.add_argument("--project", required=True)

    effects = sub.add_parser("sfx")
    effects.add_argument("dest")

    voice = sub.add_parser("voice")
    voice.add_argument("--text", required=True)
    voice.add_argument("--out", required=True)
    voice.add_argument("--voice", default="vi-VN-NamMinhNeural")

    board = sub.add_parser("storyboard")
    board.add_argument("--out", required=True)

    produce = sub.add_parser("produce")
    produce.add_argument("project")
    produce.add_argument("--repo", default="")

    args = parser.parse_args(argv)
    try:
        if args.command == "validate":
            return _validate(Path(args.project))
        if args.command == "analyze":
            return _analyze(args)
        if args.command == "render":
            return _render(args)
        if args.command == "qa":
            return _qa(args)
        if args.command == "sfx":
            written = write_placeholders(Path(args.dest))
            print("\n".join(str(path) for path in written))
            return 0
        if args.command == "voice":
            return _voice(args)
        if args.command == "storyboard":
            return _storyboard(args)
        if args.command == "produce":
            return _produce(args)
    except ProductionError as exc:
        print(exc.status, file=sys.stderr)
        for item in exc.errors:
            print(f"{item['code']}\t{item['path']}\t{item['message']}", file=sys.stderr)
        return 2 if exc.status == "BLOCKED" else 1
    return 1


def _validate(project_dir: Path) -> int:
    project, errors = load_project(project_dir)
    if project is None or project.get("shotPlan") is None:
        for item in errors:
            print(f"FAIL\t{item['code']}\t{item['message']}")
        return 1
    found, warnings = validate_project(project)
    for item in found:
        print(f"FAIL\t{item['code']}\t{item['path']}\t{item['message']}")
    for item in warnings:
        print(f"WARN\t{item['code']}\t{item['path']}\t{item['message']}")
    if found:
        return 1
    scenes = project["shotPlan"]["scenes"]
    total = sum(float(scene["durationSeconds"]) for scene in scenes)
    print(f"PASS\tsceneCount={len(scenes)}\tdurationSeconds={total:.2f}")
    return 0


def _analyze(args: argparse.Namespace) -> int:
    text = Path(args.script).read_text(encoding="utf-8")
    plan = propose_shot_plan(text, args.project_id, chars_per_second=args.chars_per_second)
    if plan["approvalStatus"] != "proposed":
        raise ProductionError("FAIL", [])
    write_proposal(plan, Path(args.out))
    print(f"proposed scenes={len(plan['scenes'])} warnings={len(plan['warnings'])} -> {args.out}")
    for item in plan["warnings"]:
        print(f"WARN\t{item['sceneId']}\t{item['message']}")
    return 0


def _render(args: argparse.Namespace) -> int:
    repo = Path(args.repo).resolve() if args.repo else Path.cwd()
    scene_ids = [item for item in args.scenes.split(",") if item] or None
    dest = render_project(
        Path(args.project),
        repo,
        scene_ids=scene_ids,
        label=args.label,
        allow_style_proof=args.allow_style_proof,
        preview_only=args.preview_only,
    )
    print(dest)
    return 0


def _qa(args: argparse.Namespace) -> int:
    report = qa_render(Path(args.render_dir), Path(args.project))
    print(f"technical={report['technicalStatus']} overall={report['overallStatus']}")
    for item in report["checks"]:
        print(f"{item['status']}\t{item['id']}\t{item['evidence']}")
    if report["technicalStatus"] == "FAIL":
        return 1
    if report["technicalStatus"] == "BLOCKED":
        return 2
    return 0


def _storyboard(args: argparse.Namespace) -> int:
    from kit_video.picture import write_storyboard

    style = {
        "profileId": "novixa-story-v2",
        "palette": {
            "background": [232, 226, 214],
            "ink": [0, 32, 96],
            "accent": [0, 144, 224],
            "paper": [250, 247, 240],
            "muted": [110, 102, 90],
            "accentAlt": [176, 92, 62],
            "weight": [24, 40, 72],
            "skin": [222, 184, 156],
            "wood": [166, 122, 78],
            "wall": [236, 232, 224],
            "floor": [214, 206, 192],
            "cloth": [0, 144, 224],
            "shirt": [232, 236, 242],
            "pants": [36, 48, 78],
            "hair": [24, 28, 40],
            "coin": [206, 164, 72],
        },
        "typography": {
            "fontRegular": "C:/Windows/Fonts/segoeui.ttf",
            "fontBold": "C:/Windows/Fonts/segoeuib.ttf",
        },
        "safeArea": {"top": 0.06, "right": 0.06, "bottom": 0.06, "left": 0.06},
    }
    beats = {
        "intro": [0.15, 2.5],
        "fast": [3.35, 4.7],
        "calendar": [7.25, 8.85],
        "transform": [9.35, 10.85],
        "drop": [11.15, 12.85],
        "lock": [12.7, 14.6],
    }
    print(write_storyboard(Path(args.out), style, beats))
    return 0


def _produce(args: argparse.Namespace) -> int:
    repo = Path(args.repo).resolve() if args.repo else Path.cwd()
    result = produce_commercial(Path(args.project), repo)
    print(result["status"])
    print(result["dir"])
    for item in result["blockers"]:
        print(f"{item['code']}\t{item['message']}")
    return 2 if result["status"] == "BLOCKED" else 0


def _voice(args: argparse.Namespace) -> int:
    import edge_tts

    dest = Path(args.out)
    dest.parent.mkdir(parents=True, exist_ok=True)
    mp3 = dest if dest.suffix.lower() == ".mp3" else dest.with_suffix(".mp3")

    async def synth() -> list[dict]:
        communicate = edge_tts.Communicate(args.text, args.voice, rate="+0%", boundary="WordBoundary")
        audio = bytearray()
        words = []
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                audio.extend(chunk["data"])
            elif chunk["type"] == "WordBoundary":
                words.append({
                    "text": chunk["text"],
                    "start": chunk["offset"] / 10_000_000,
                    "end": (chunk["offset"] + chunk["duration"]) / 10_000_000,
                })
        mp3.write_bytes(bytes(audio))
        return words

    words = asyncio.run(synth())
    (mp3.with_suffix(".boundaries.json")).write_text(json.dumps(words, ensure_ascii=False, indent=2), encoding="utf-8")
    wav = mp3.with_suffix(".wav")
    from kit_video.audio import _run

    _run(["ffmpeg", "-y", "-i", str(mp3), "-ar", "48000", "-ac", "2", str(wav)])
    print(wav)
    print(mp3.with_suffix(".boundaries.json"))
    return 0

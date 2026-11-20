"""Validator, analyzer, and a short render. No brand runtime is assumed."""

from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
import wave
from pathlib import Path

from kit_video.camera import CameraMove
from kit_video.commercial import assemble_preview, export_graphics_holds
from kit_video.motion_select import select_motion
from kit_video.still import access_report, goods_source, registry_view, select_still, submit_still
from kit_video.analyze import propose_shot_plan
from kit_video.audio import scene_duration
from kit_video.errors import ProductionError
from kit_video.qa import qa_render
from kit_video.render import render_project
from kit_video.standard import check_character_probe, check_visual_standard, graphics_overflow, may_submit
from kit_video.validate import load_project, validate_project
from kit_video.voice_direction import (
    check_sheet,
    check_standard,
    intro_edit_gate,
    intro_picture_blockers,
    load_standard,
    mix_plan,
    payload,
    prepare_tts_text,
    script_dialogues,
)

ROOT = Path(__file__).resolve().parents[3]
ENGINE = Path(__file__).resolve().parents[1] / "kit_video"
FONT = "C:/Windows/Fonts/segoeui.ttf"
FONT_B = "C:/Windows/Fonts/segoeuib.ttf"


def pipeline() -> list[dict]:
    stages = [
        "brief", "script_analysis", "story_structure", "shot_plan", "asset_plan",
        "asset_creation", "voiceover", "animation", "audio_mix", "render", "qa", "delivery",
    ]
    return [{"stage": stage, "automation": "manual", "status": "not_started", "notes": ""} for stage in stages]


def manifest(project_id: str, scenes: list[dict], **over) -> dict:
    data = {
        "schemaVersion": "kit-video-project/1",
        "projectId": project_id,
        "projectName": project_id,
        "brand": {"id": "fixture", "name": "Fixture"},
        "script": {"version": "1", "approvalStatus": "approved", "sourcePath": "script.txt"},
        "durationPolicy": {"mode": "explicit_scenes", "targetSeconds": None, "notes": ""},
        "storyStructure": [{"beatId": "only", "summary": "Một beat."}],
        "voice": {"profileId": "none", "language": "vi", "pronunciationNotes": "", "tracks": []},
        "assets": [],
        "visualStyle": {
            "profileId": "fixture",
            "palette": {
                "background": [230, 230, 230],
                "ink": [20, 20, 20],
                "accent": [20, 80, 120],
                "paper": [250, 250, 250],
                "muted": [90, 90, 90],
            },
            "typography": {"fontRegular": FONT, "fontBold": FONT_B},
            "safeArea": {"top": 0.06, "right": 0.06, "bottom": 0.06, "left": 0.06},
        },
        "audioProfile": {"ducking": False, "voiceGain": 1, "sfxGain": 0.2, "musicPolicy": "none"},
        "format": {"aspectRatio": "16:9", "width": 160, "height": 90, "frameRate": 10},
        "render": {
            "videoCodec": "libx264",
            "audioCodec": "aac",
            "crf": 28,
            "preset": "veryfast",
            "allowPlaceholders": True,
            "versioned": True,
            "outputRoot": "outputs",
        },
        "pipeline": pipeline(),
        "shotPlan": "shot-plan.json",
    }
    data.update(over)
    return data


def scene(scene_id: str, duration: float, text: str = "Xin chào") -> dict:
    return {
        "sceneId": scene_id,
        "approvalStatus": "approved",
        "storyGoal": "Kiểm thử",
        "durationSeconds": duration,
        "durationSource": "fixed",
        "dialogue": "",
        "action": "Chữ hiện",
        "visualMetaphor": None,
        "subjects": [],
        "onScreenText": [text],
        "templateId": "kinetic_type",
        "params": {"lines": [{"text": text, "at": 0.05, "scale": 0.12}]},
        "assets": [],
        "audio": {
            "voiceTrackId": None,
            "musicAssetId": None,
            "sfx": [],
            "intentionalSilences": [{"start": 0, "end": duration, "reason": "Cảnh kiểm thử không có tiếng đọc."}],
            "syncNotes": "",
        },
        "transitionOut": None,
        "acceptance": ["Chữ nằm trong khung."],
        "previewFractions": [0.5],
    }


def write_project(folder: Path, project_id: str, scenes: list[dict], **over) -> None:
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "manifest.json").write_text(json.dumps(manifest(project_id, scenes, **over), ensure_ascii=False), encoding="utf-8")
    shot = {
        "schemaVersion": "kit-video-shot-plan/1",
        "projectId": project_id,
        "approvalStatus": "approved",
        "scenes": scenes,
    }
    (folder / "shot-plan.json").write_text(json.dumps(shot, ensure_ascii=False), encoding="utf-8")


class SystemTests(unittest.TestCase):
    def test_engine_does_not_bake_a_runtime(self) -> None:
        text = "\n".join(path.read_text(encoding="utf-8") for path in ENGINE.glob("*.py"))
        self.assertNotIn("atempo", text)
        self.assertIsNone(re.search(r"(?<!\d)180(?!\d)", text))
        self.assertNotIn("SCENES =", text)

    def test_two_fixture_shapes_both_validate(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_project(root / "one", "one-scene", [scene("only", 2.0)])
            write_project(root / "four", "four-scenes", [scene(f"c{i}", 1.5 + i, f"Cảnh {i}") for i in range(1, 5)])
            one, errors = load_project(root / "one")
            four, errors_four = load_project(root / "four")
            self.assertEqual(errors, [])
            self.assertEqual(errors_four, [])
            self.assertEqual(validate_project(one)[0], [])
            self.assertEqual(validate_project(four)[0], [])
            self.assertEqual(len(one["shotPlan"]["scenes"]), 1)
            self.assertEqual(len(four["shotPlan"]["scenes"]), 4)
            self.assertNotEqual(
                sum(item["durationSeconds"] for item in one["shotPlan"]["scenes"]),
                sum(item["durationSeconds"] for item in four["shotPlan"]["scenes"]),
            )

    def test_repository_projects_differ(self) -> None:
        north, north_errors = load_project(ROOT / "proof/kit-video-production-v1/projects/reuse-short")
        south, south_errors = load_project(ROOT / "proof/kit-video-production-v1/projects/reuse-structured")
        self.assertEqual(north_errors, [])
        self.assertEqual(south_errors, [])
        self.assertEqual(validate_project(north)[0], [])
        self.assertEqual(validate_project(south)[0], [])
        self.assertNotEqual(len(north["shotPlan"]["scenes"]), len(south["shotPlan"]["scenes"]))
        self.assertNotEqual(north["manifest"]["format"]["aspectRatio"], south["manifest"]["format"]["aspectRatio"])
        north_total = sum(item["durationSeconds"] for item in north["shotPlan"]["scenes"])
        south_total = sum(item["durationSeconds"] for item in south["shotPlan"]["scenes"])
        self.assertNotEqual(north_total, south_total)
        self.assertNotIn("novixa", json.dumps(north["manifest"]).lower())

    def test_missing_asset_and_fake_ui_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            bad_scene = scene("ui", 1)
            bad_scene["assets"] = ["ghost"]
            write_project(folder, "bad", [bad_scene])
            project, _ = load_project(folder)
            codes = {item["code"] for item in validate_project(project)[0]}
            self.assertIn("asset", codes)

            write_project(folder, "ui", [scene("ui", 1)], assets=[{
                "assetId": "fake-ui",
                "kind": "screenshot",
                "source": "placeholder",
                "path": "missing.png",
                "pathBase": "project",
                "version": "1",
                "usageRights": "",
                "approval": "placeholder",
                "role": "product_ui",
                "label": "fake",
            }])
            project, _ = load_project(folder)
            codes = {item["code"] for item in validate_project(project)[0]}
            self.assertIn("product_ui", codes)

    def test_aspect_mismatch_fails(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            write_project(folder, "wide", [scene("only", 1)], format={"aspectRatio": "9:16", "width": 160, "height": 90, "frameRate": 10})
            project, _ = load_project(folder)
            self.assertTrue(validate_project(project)[0])

    def test_analyzer_proposes_and_does_not_squeeze_voice(self) -> None:
        script = """
## segment short
goal: Nói một câu dài hơn gợi ý.
template_hint: kinetic_type
duration_hint_seconds: 1
dialogue: |
  Đây là một câu đủ dài để vượt một giây khi ước lượng theo số ký tự.
on_screen:
  - Chữ
acceptance:
  - Người duyệt trước khi dựng.
## segment next
goal: Cảnh thứ hai.
dialogue: |
  Thêm một cảnh nữa.
on_screen:
  - Hai
acceptance:
  - Vẫn là đề xuất.
"""
        plan = propose_shot_plan(script, "sample")
        self.assertEqual(plan["approvalStatus"], "proposed")
        self.assertEqual(len(plan["scenes"]), 2)
        self.assertTrue(all(item["approvalStatus"] == "proposed" for item in plan["scenes"]))
        self.assertEqual(plan["scenes"][1]["templateId"], "custom")
        self.assertTrue(plan["warnings"])
        self.assertGreater(plan["scenes"][0]["durationSeconds"], 1)

    def test_long_voice_blocks_instead_of_speeding_up(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "voice.wav"
            with wave.open(str(path), "wb") as handle:
                handle.setnchannels(1)
                handle.setsampwidth(2)
                handle.setframerate(8000)
                handle.writeframes(b"\x00\x00" * 8000)
            with self.assertRaises(ProductionError) as caught:
                scene_duration({"sceneId": "x", "durationSeconds": 0.2, "durationSource": "fixed"}, path)
            self.assertEqual(caught.exception.status, "BLOCKED")
            self.assertIn("Không ép tốc độ", caught.exception.errors[0]["message"])

    @unittest.skipUnless(shutil.which("ffmpeg") and Path(FONT).is_file(), "ffmpeg or font missing")
    def test_render_subset_and_qa(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp) / "project"
            write_project(folder, "tiny-cut", [scene("a", 0.4, "Một"), scene("b", 0.6, "Hai")])
            first = render_project(folder, folder, label="all")
            second = render_project(folder, folder, scene_ids=["b"], label="only-b")
            self.assertTrue((first / "video.mp4").is_file())
            self.assertTrue((second / "video.mp4").is_file())
            self.assertNotEqual(first, second)
            full = qa_render(first, folder)
            part = qa_render(second, folder)
            self.assertEqual(full["technicalStatus"], "PASS")
            self.assertEqual(part["technicalStatus"], "PASS")
            self.assertEqual(full["overallStatus"], "NEEDS HUMAN REVIEW")
            log = json.loads((second / "render-log.json").read_text(encoding="utf-8"))
            self.assertEqual([item["sceneId"] for item in log["scenes"]], ["b"])


    def test_stock_story_follows_brand_input(self) -> None:
        from kit_video.picture import render_story_frame

        def style(accent, ink, skin) -> dict:
            return {
                "profileId": "fixture",
                "palette": {
                    "background": [230, 228, 222],
                    "ink": ink,
                    "accent": accent,
                    "paper": [250, 248, 242],
                    "skin": skin,
                    "wood": [150, 110, 74],
                },
                "typography": {"fontRegular": FONT, "fontBold": FONT_B},
                "safeArea": {"top": 0.06, "right": 0.06, "bottom": 0.06, "left": 0.06},
            }

        beats = {
            "intro": [0.1, 0.8],
            "fast": [0.9, 1.6],
            "calendar": [1.7, 2.3],
            "transform": [2.3, 3.0],
            "drop": [3.0, 3.8],
            "lock": [3.8, 4.6],
        }
        wide, wide_text = render_story_frame(
            style([0, 144, 224], [0, 32, 96], [222, 184, 156]),
            {"mode": "hybrid", "headline": "Hàng tồn kho cũng là vốn", "boxCount": 5, "fastCount": 2, "beats": beats, "headlineAt": 4.2},
            (480, 270),
            4.5,
            6,
        )
        tall, tall_text = render_story_frame(
            style([36, 122, 92], [24, 48, 36], [198, 154, 124]),
            {"mode": "metaphor", "headline": "Hàng nằm lại", "boxCount": 3, "fastCount": 1, "beats": beats, "headlineAt": 4.2},
            (270, 480),
            4.5,
            6,
        )
        self.assertIn("Hàng tồn kho cũng là vốn", wide_text)
        self.assertIn("Hàng nằm lại", tall_text)
        self.assertNotEqual(wide.resize((32, 32)).tobytes(), tall.resize((32, 32)).tobytes())
        self.assertEqual(wide.size, (480, 270))
        self.assertEqual(tall.size, (270, 480))

    def test_reference_stage_follows_config(self) -> None:
        from PIL import Image

        from kit_video.stage import pose_at, render_stage_frame

        layer = {
            "x": 0.2,
            "y": 0.4,
            "motion": {"start": 1.0, "end": 3.0, "to": [0.7, 0.6], "lift": 0.1, "spin": -8},
        }
        samples = [pose_at(layer, index / 30) for index in range(0, 120)]
        steps = [abs(samples[index + 1]["x"] - samples[index]["x"]) for index in range(len(samples) - 1)]
        self.assertLess(max(steps), 0.03)
        self.assertAlmostEqual(pose_at(layer, 0.2)["x"], 0.2)
        self.assertAlmostEqual(pose_at(layer, 3.2)["x"], 0.7, places=2)
        self.assertAlmostEqual(pose_at(layer, 1.0)["spin"], 0, places=2)
        self.assertAlmostEqual(pose_at(layer, 3.0)["spin"], 0, places=2)
        self.assertLess(pose_at(layer, 2.0)["y"], pose_at(layer, 2.0)["groundY"])

        folder = Path(tempfile.mkdtemp())
        try:
            Image.new("RGBA", (48, 27), (190, 176, 150, 255)).save(folder / "plate.png")
            Image.new("RGBA", (14, 32), (248, 248, 246, 255)).save(folder / "box.png")
            Image.new("RGBA", (18, 28), (176, 96, 32, 255)).save(folder / "bottle.png")
            stage = {
                "plate": "plate.png",
                "layers": [{
                    "id": "hero",
                    "asset": "box.png",
                    "anchor": "center",
                    "x": 0.28,
                    "y": 0.42,
                    "h": 0.28,
                    "shadow": True,
                    "callout": "{productLabel}",
                    "motion": {"start": 0.35, "end": 1.15, "to": [0.7, 0.62], "lift": 0.12, "spin": -8},
                }],
                "cards": [{"text": "{message}", "sub": "{productLabel}", "at": 1.25, "x": 0.5, "y": 0.2, "scale": 0.09}],
            }
            (folder / "stage.json").write_text(json.dumps(stage), encoding="utf-8")
            stage["layers"][0]["asset"] = "bottle.png"
            (folder / "stage-b.json").write_text(json.dumps(stage), encoding="utf-8")

            def style(accent: list[int]) -> dict:
                return {
                    "profileId": "fixture",
                    "palette": {
                        "background": [230, 228, 222],
                        "ink": [0, 32, 96],
                        "accent": accent,
                        "paper": [255, 250, 244],
                    },
                    "typography": {"fontRegular": FONT, "fontBold": FONT_B},
                    "safeArea": {"top": 0.06, "right": 0.06, "bottom": 0.06, "left": 0.06},
                }

            blue = {"stageFile": "stage.json", "productLabel": "08/2026", "message": "Hàng nằm lại là vốn"}
            green = {"stageFile": "stage-b.json", "productLabel": "Lô mới", "message": "Đã chuyển kệ"}
            frame_a, text_a = render_stage_frame(style([0, 144, 224]), blue, (320, 180), 1.6, 2, folder)
            frame_b, text_b = render_stage_frame(style([36, 122, 92]), green, (320, 180), 1.6, 2, folder)
            early, _ = render_stage_frame(style([0, 144, 224]), blue, (320, 180), 0.1, 2, folder)
            moved, _ = render_stage_frame(style([0, 144, 224]), blue, (320, 180), 0.9, 2, folder)
            self.assertIn("Hàng nằm lại là vốn", text_a)
            self.assertIn("08/2026", text_a)
            self.assertIn("Đã chuyển kệ", text_b)
            self.assertIn("Lô mới", text_b)
            self.assertNotEqual(frame_a.resize((32, 32)).tobytes(), frame_b.resize((32, 32)).tobytes())
            self.assertNotEqual(early.tobytes(), moved.tobytes())
        finally:
            shutil.rmtree(folder)

    def test_commercial_blocks_without_provider_clip(self) -> None:
        from kit_video.commercial import produce_commercial, replace_scene
        from kit_video.motion import build_request, submit

        calls = []

        def transport(request, secret):
            calls.append((request, secret))
            return {"id": "task-test"}

        blocked = submit(
            {"authEnv": "RUNWAY_API_KEY", "url": "https://api.dev.runwayml.com/v1/image_to_video", "json": {}},
            "",
            transport,
        )
        self.assertEqual(blocked["status"], "BLOCKED")
        self.assertEqual(calls, [])
        request = build_request(
            {"id": "runway", "model": "gen4_turbo", "mode": "image_to_video", "durationSeconds": 5, "ratio": "1280:720"},
            {"prompt": "She lifts one box."},
            "keyframe.png",
            "end.png",
        )
        sent = submit(request, "test-secret", transport)
        self.assertTrue(sent["sent"])
        self.assertEqual(sent["visualReview"], "VISUAL_REVIEW_REQUIRED")
        self.assertNotIn("test-secret", json.dumps(calls[0][0]))
        self.assertEqual(calls[0][0]["json"]["promptImage"][0]["position"], "first")
        self.assertEqual(calls[0][1], "test-secret")

        folder = Path(tempfile.mkdtemp())
        try:
            spec = {
                "projectId": "commercial-fixture",
                "brief": {"version": "t-1", "message": "Thử", "approval": "proposed"},
                "reference": {"video": "missing-reference.mp4"},
                "provider": {"id": "runway", "model": "gen4_turbo", "mode": "image_to_video", "durationSeconds": 5, "ratio": "1280:720"},
                "scenes": [
                    {"sceneId": "s01", "order": 1, "kind": "generate", "action": "Lấy hộp", "prompt": "She lifts one box.", "keyframe": None},
                    {"sceneId": "s02", "order": 2, "kind": "post", "action": "Chữ"},
                ],
                "endCard": {
                    "lines": ["NOVIXA", "Hàng nằm lại là vốn"],
                    "fontRegular": FONT,
                    "fontBold": FONT_B,
                    "seconds": 1,
                },
                "output": {"width": 320, "height": 180, "frameRate": 12, "outputRoot": "outputs"},
                "palette": {"ink": [0, 32, 96], "accent": [0, 144, 224], "paper": [255, 250, 244]},
                "assets": [],
            }
            (folder / "commercial.json").write_text(json.dumps(spec), encoding="utf-8")
            result = produce_commercial(folder, folder)
            record = result["record"]
            self.assertEqual(result["status"], "BLOCKED")
            self.assertIsNone(record["finalVideo"])
            self.assertFalse(record["post"]["isFinalCommercial"])
            self.assertTrue(Path(record["post"]["endCard"]).is_file())
            self.assertFalse((Path(result["dir"]) / "video.mp4").exists())
            codes = {item["code"] for item in record["blockers"]}
            self.assertIn("CREDENTIAL_MISSING", codes)
            self.assertIn("KEYFRAME_MISSING", codes)
            self.assertIn("REFERENCE_MISSING", codes)
            from kit_video.commercial import _blockers

            waiting = json.loads(json.dumps(spec))
            (folder / "kf.jpg").write_bytes(b"not-a-real-image")
            waiting["scenes"][0]["keyframe"] = {"path": "kf.jpg", "approval": "AWAITING_REVIEW"}
            waiting["reference"]["required"] = False
            codes = {item["code"] for item in _blockers(waiting, folder, folder)}
            self.assertIn("KEYFRAME_NOT_APPROVED", codes)
            self.assertNotIn("KEYFRAME_MISSING", codes)
            self.assertTrue(all(item["status"] == "VISUAL_REVIEW_REQUIRED" for item in record["visualChecklist"]))
            clip = folder / "one.mp4"
            shutil.copyfile(record["post"]["endCard"], clip)
            updated = replace_scene(Path(result["dir"]) / "production-record.json", "s01", clip)
            self.assertEqual(updated["scenes"][0]["status"], "CLIP_ATTACHED")
            self.assertIsNone(updated["finalVideo"])
            self.assertEqual(updated["visualReview"], "VISUAL_REVIEW_REQUIRED")
            self.assertFalse(updated["post"]["isFinalCommercial"])
        finally:
            shutil.rmtree(folder)

    def test_demo_board_locks_one_illustrative_set(self) -> None:
        from kit_video.demo_board import demo_issues, format_vnd_per_day, render_demo_board, sunday_share_percent

        spec = json.loads((ROOT / "proof/kit-video-production-v1/projects/novixa-pharmacy-commercial/commercial.json").read_text(encoding="utf-8"))
        data = spec["demoData"]
        self.assertEqual(data["label"], "Dữ liệu minh họa")
        self.assertEqual(data["caption"], "Dữ liệu mô phỏng")
        self.assertEqual(data["source"], "mockup")
        self.assertEqual(data["open"], "07:00")
        self.assertEqual(data["close"], "22:00")
        self.assertEqual(data["revenueVndPerDay"], 12500000)
        self.assertEqual(data["orders"], 86)
        self.assertEqual(data["inventoryOnShelf"], 214)
        self.assertEqual(data["orderSeries"][-1]["orders"], data["orders"])
        self.assertEqual(format_vnd_per_day(data["revenueVndPerDay"]), "12.500.000 VNĐ/ngày")
        self.assertEqual(sunday_share_percent(data), 17)
        self.assertEqual(sum(item["stock"] for item in data["products"]), data["inventoryOnShelf"])
        self.assertEqual([item["name"] for item in data["products"]], ["AMPICILLIN", "HOẠT HUYẾT", "VITAMIN C"])
        self.assertTrue(all(item["badge"] == "MOCKUP" for item in data["products"]))
        self.assertEqual(demo_issues(spec), [])
        folder = Path(tempfile.mkdtemp())
        try:
            lines = render_demo_board(folder / "board.png", data, FONT, FONT_B, (640, 360))
            self.assertEqual(lines["revenue"], "12.500.000 VNĐ/ngày")
            self.assertEqual(lines["orders"], "86")
            self.assertEqual(lines["open"], "07:00")
            self.assertEqual(lines["close"], "22:00")
            self.assertEqual(lines["inventory"], "214")
            self.assertEqual(lines["todayShare"], "17%")
            self.assertIn("Mockup", lines["sourceNote"])
            self.assertTrue((folder / "board.png").is_file())
        finally:
            shutil.rmtree(folder)
        conflict = json.loads(json.dumps(data))
        conflict["orderSeries"][-1]["orders"] = 10
        codes = {item["code"] for item in demo_issues({"demoData": conflict})}
        self.assertIn("DEMO_DATA_CONFLICT", codes)
        dosed = json.loads(json.dumps(data))
        dosed["products"][0]["dosage"] = "500 mg"
        codes = {item["code"] for item in demo_issues({"demoData": dosed})}
        self.assertIn("DEMO_DATA_CONFLICT", codes)


class VisualStandardTests(unittest.TestCase):
    def test_registry_matches_files_and_does_not_send(self):
        project = ROOT / "proof/kit-video-production-v1/projects/novixa-pharmacy-commercial"
        self.assertEqual(check_visual_standard(project, ROOT), [])
        self.assertEqual(graphics_overflow(project), [])
        self.assertFalse(may_submit("AWAITING_REVIEW", True))
        self.assertFalse(may_submit("APPROVED", False))
        folder = Path(tempfile.mkdtemp())
        try:
            shutil.copy(project / "commercial.json", folder / "commercial.json")
            standard = json.loads((project / "visual-standard-v1.json").read_text(encoding="utf-8"))
            standard["products"][0]["name"] = "TEN KHAC"
            (folder / "visual-standard-v1.json").write_text(json.dumps(standard), encoding="utf-8")
            codes = {item["code"] for item in check_visual_standard(folder, ROOT)}
            self.assertIn("PRODUCT_SOURCE", codes)
        finally:
            shutil.rmtree(folder)


class CharacterProbeTests(unittest.TestCase):
    def test_probe_keeps_one_real_portrait(self):
        project = ROOT / "proof/kit-video-production-v1/projects/novixa-pharmacy-commercial"
        self.assertEqual(check_character_probe(project), [])
        probe = json.loads((project / "probes/character-consistency-v1/probe.json").read_text(encoding="utf-8"))
        blocked = [item["id"] for item in probe["items"] if item["status"] == "BLOCKED"]
        self.assertEqual(len(blocked), 7)
        self.assertNotIn("APPROVED", {probe["approval"], *(item["approval"] for item in probe["items"])})


class CharacterReferenceTests(unittest.TestCase):
    def test_storyboard_crops_stay_unapproved(self):
        project = ROOT / "proof/kit-video-production-v1/projects/novixa-pharmacy-commercial"
        folder = project / "probes/character-reference-v1"
        meta = json.loads((folder / "reference.json").read_text(encoding="utf-8"))
        self.assertFalse(meta["integratedIntoProduction"])
        self.assertEqual(meta["approval"], "AWAITING_REVIEW")
        self.assertEqual(len(meta["items"]), 8)
        source = (folder / "source-storyboard.jpg").read_bytes()
        portrait = (project / "assets/pharmacist-nova-cartoon-v3.jpg").read_bytes()
        self.assertNotEqual(source, portrait)
        for item in meta["items"]:
            self.assertEqual(item["approval"], "AWAITING_REVIEW")
            self.assertTrue((folder / item["path"]).is_file())
            self.assertEqual(item["method"], "rectangular-crop")


class CharacterReferenceV2Tests(unittest.TestCase):
    def test_pose_corrections_are_not_faked(self):
        project = ROOT / "proof/kit-video-production-v1/projects/novixa-pharmacy-commercial"
        folder = project / "probes/character-reference-v2"
        meta = json.loads((folder / "reference.json").read_text(encoding="utf-8"))
        self.assertFalse(meta["integratedIntoProduction"])
        self.assertEqual(meta["approval"], "AWAITING_REVIEW")
        self.assertEqual(len(meta["items"]), 4)
        for item in meta["items"]:
            self.assertEqual(item["status"], "BLOCKED")
            self.assertEqual(item["method"], "not-created")
            self.assertIsNone(item["path"])
            self.assertNotEqual(item["approval"], "APPROVED")
            self.assertTrue((project / item["replaces"]).is_file())
        self.assertTrue((folder / "contact-sheet.jpg").is_file())


class FirstCommercialPlanTests(unittest.TestCase):
    def test_shot_plan_validates_and_stays_proposed(self):
        project_dir = ROOT / "proof/kit-video-production-v1/projects/novixa-pharmacy-commercial"
        project, load_errors = load_project(project_dir)
        self.assertEqual(load_errors, [])
        errors, _warnings = validate_project(project)
        self.assertEqual(errors, [])
        scenes = project["shotPlan"]["scenes"]
        self.assertEqual(sum(scene["durationSeconds"] for scene in scenes), 25)
        self.assertTrue(all(scene["approvalStatus"] == "proposed" for scene in scenes))
        self.assertNotIn("s04-operations", {scene["sceneId"] for scene in scenes if "character-v3" in scene["assets"]})
        self.assertNotIn("s05-inventory", {scene["sceneId"] for scene in scenes if "character-v3" in scene["assets"]})
        matrix = json.loads((project_dir / "production-matrix.json").read_text(encoding="utf-8"))
        self.assertEqual([row["sceneId"] for row in matrix["scenes"]], [scene["sceneId"] for scene in scenes])
        self.assertIsNone(matrix["finalVideo"])
        folder = Path(tempfile.mkdtemp())
        try:
            record = export_graphics_holds(project_dir, ROOT, folder)
            self.assertIsNone(record["finalVideo"])
            self.assertFalse(record["isFinalCommercial"])
            self.assertEqual(record["omitted"], ["s01-pharmacy", "s02-take-box", "s03-label"])
            made = {row["sceneId"]: row for row in record["scenes"]}
            self.assertEqual(set(made), {"s04-operations", "s05-inventory", "s06-logo"})
            for scene_id, planned in (("s04-operations", 4), ("s05-inventory", 4), ("s06-logo", 3)):
                self.assertAlmostEqual(made[scene_id]["measuredSeconds"], planned, delta=0.15)
                self.assertFalse(made[scene_id]["motionClaim"])
            probe = subprocess.run(
                ["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=pix_fmt", "-of", "csv=p=0", made["s04-operations"]["file"]],
                capture_output=True, text=True, check=False,
            )
            self.assertEqual(probe.stdout.strip(), "yuv420p")
            blocked = assemble_preview({})
            self.assertEqual(blocked["status"], "BLOCKED")
            self.assertIsNone(blocked["finalVideo"])
            self.assertIn("s02-take-box", blocked["missing"])
        finally:
            shutil.rmtree(folder)


class IntroFilmDurationTests(unittest.TestCase):
    def test_three_minute_hint_is_not_forced(self):
        folder = ROOT / "proof/kit-video-production-v1/projects/novixa-intro-film"
        plan = json.loads((folder / "shot-plan.proposed.json").read_text(encoding="utf-8"))
        manifest = json.loads((folder / "production-manifest.json").read_text(encoding="utf-8"))
        planned = round(sum(scene["durationSeconds"] for scene in plan["scenes"]), 2)
        listed = round(sum(scene["seconds"] for scene in manifest["scenes"]), 2)
        self.assertEqual(planned, listed)
        self.assertEqual(planned, 250.68)
        self.assertNotEqual(planned, manifest["hintSeconds"])
        self.assertFalse(manifest["durationLocked"])
        self.assertIsNone(manifest["finalVideo"])
        self.assertEqual(manifest["aiCost"], 0)
        self.assertTrue(all(scene["approvalStatus"] == "proposed" for scene in plan["scenes"]))
        self.assertTrue(all(scene["output"] is None for scene in manifest["scenes"]))
        self.assertEqual(manifest["voice"]["sample"]["status"], "MEASURED")
        self.assertEqual(manifest["voice"]["fullRead"]["status"], "MEASURED")
        self.assertEqual(manifest["voice"]["fullRead"]["approval"], "APPROVED")
        self.assertEqual(manifest["voice"]["fullRead"]["cost"], 0)
        self.assertNotEqual(manifest.get("filmPictureStatus"), "COMPLETE")
        self.assertNotEqual(manifest["measuredSpeechEndSeconds"], manifest["hintSeconds"])
        self.assertEqual(manifest["proposedPictureStatus"], "PROPOSED")
        self.assertTrue(all(scene["readStatus"] == "MEASURED" for scene in manifest["scenes"]))
        self.assertTrue(all(scene["approval"] != "APPROVED" for scene in manifest["scenes"]))
        read = ROOT / "proof/kit-video-production-v1" / manifest["voice"]["fullRead"]["path"]
        self.assertTrue(read.is_file())
        sample = folder / manifest["voice"]["sample"]["path"]
        self.assertTrue(sample.is_file())
        self.assertNotEqual(manifest["estimateStatus"], "APPROVED")
        plates = ROOT / "proof/kit-video-production-v1" / manifest["graphicsPlates"]["s12"]
        self.assertTrue(plates.is_file())


class VoiceDirectionTests(unittest.TestCase):
    def test_standard_separates_identity_from_delivery(self):
        standard = load_standard(ROOT)
        self.assertEqual(check_standard(standard), [])
        self.assertNotIn("gender", standard["presets"][0])
        self.assertTrue({"male", "female"} <= {item["gender"] for item in standard["profiles"]})
        controls = standard["adapters"][0]["controls"]
        self.assertNotIn("emotion", controls)
        self.assertNotIn("emphasis", controls)
        brand = next(item for item in standard["dictionary"] if item["entryId"] == "brand-novixa")
        self.assertEqual(brand["display"], "NOVIXA")
        self.assertEqual(brand["verification"], "VERIFIED")
        self.assertTrue(brand["listeningApproved"])
        self.assertEqual(prepare_tts_text("Logo vẫn là NOVIXA.", standard["dictionary"]), "Logo vẫn là Nôvixa.")
        self.assertEqual(prepare_tts_text("Tìm hiểu tại novixa chấm vn.", standard["dictionary"]), "Tìm hiểu tại novixa chấm vn.")
        with self.assertRaises(ProductionError):
            prepare_tts_text("Đọc [nghỉ] ở đây.", standard["dictionary"])

    def test_intro_sheet_keeps_the_script_and_a_cast(self):
        folder = ROOT / "proof/kit-video-production-v1/projects/novixa-intro-film"
        standard = load_standard(ROOT)
        spoken = script_dialogues(folder / "script.md")
        sheet = json.loads((folder / "voice-direction-sheet.json").read_text(encoding="utf-8"))
        self.assertEqual(check_sheet(sheet, spoken, standard), [])
        spoken_s05 = payload(sheet["scenes"][4], standard["dictionary"])
        self.assertIn("Nôvixa", spoken_s05)
        self.assertNotIn("Nô vi xa", spoken_s05)
        self.assertNotIn("NOVIXA", spoken_s05)
        self.assertIn("NOVIXA", sheet["scenes"][4]["dialogue"])
        self.assertEqual(sheet["approval"], "PROPOSED")
        self.assertEqual(sheet["decisions"]["pronunciation"]["approval"], "APPROVED")
        self.assertEqual(sheet["decisions"]["s04Pause"]["approval"], "APPROVED")
        self.assertEqual(sheet["decisions"]["s04Pause"]["appliesTo"], ["s04-expiry"])
        plan = mix_plan(
            [
                {"order": 1, "speakerId": "lead", "profileId": "vi-narrator-namminh-v1", "presetId": "narration-clear-v1", "dialogue": "Một.", "gapAfterSeconds": 0.3},
                {"order": 2, "speakerId": "guest", "profileId": "vi-narrator-hoaimy-v1", "presetId": "narration-clear-v1", "dialogue": "Hai.", "gapAfterSeconds": 0.3},
            ],
            standard["profiles"],
        )
        self.assertEqual([item["profileId"] for item in plan], ["vi-narrator-namminh-v1", "vi-narrator-hoaimy-v1"])
        manifest = json.loads((folder / "production-manifest.json").read_text(encoding="utf-8"))
        gate = intro_edit_gate(manifest)
        self.assertEqual(gate["status"], "BLOCKED")
        self.assertIsNone(gate["finalVideo"])
        self.assertFalse(gate["requestSent"])
        self.assertFalse(manifest["durationLocked"])
        self.assertNotEqual(manifest["approval"], "APPROVED")
        self.assertEqual(manifest["voice"]["decisions"]["pronunciation"]["approval"], "APPROVED")
        self.assertEqual(manifest["voice"]["decisions"]["s04Pause"]["approval"], "APPROVED")
        self.assertEqual(manifest["voice"]["decisions"]["urlReading"]["approval"], "APPROVED")
        self.assertEqual(manifest["voice"]["fullRead"]["approval"], "APPROVED")
        self.assertNotEqual(manifest.get("filmPictureStatus"), "COMPLETE")
        previous = ROOT / "proof/kit-video-production-v1" / manifest["voice"]["previousFullRead"]["path"]
        current = ROOT / "proof/kit-video-production-v1" / manifest["voice"]["fullRead"]["path"]
        self.assertTrue(previous.is_file())
        self.assertTrue(current.is_file())
        self.assertNotEqual(previous, current)

    def test_intro_picture_stays_blocked_on_unapproved_assets(self):
        intro = ROOT / "proof/kit-video-production-v1/projects/novixa-intro-film"
        outputs = ROOT / "proof/kit-video-production-v1/outputs/novixa-intro-film"
        before = {path.name for path in outputs.iterdir()}
        manifest = json.loads((intro / "production-manifest.json").read_text(encoding="utf-8"))
        auth = manifest["executionAuthorizationV1"]
        self.assertTrue(auth["characterApprovedForIntro"])
        self.assertEqual(auth["characterApproval"], "APPROVED")
        self.assertTrue(auth["paidCallAuthorized"])
        self.assertTrue(auth["expectedCostConfirmed"])
        self.assertIsNone(auth["sampleShot"])
        self.assertIsNone(manifest["finalVideo"])
        self.assertFalse(manifest["durationLocked"])
        commercial = ROOT / "proof/kit-video-production-v1/projects/novixa-pharmacy-commercial"
        standard = json.loads((commercial / "visual-standard-v1.json").read_text(encoding="utf-8"))
        self.assertEqual(standard["approval"], "AWAITING_REVIEW")
        commercial_manifest = json.loads((commercial / "manifest.json").read_text(encoding="utf-8"))
        commercial_assets = {row["assetId"]: row["approval"] for row in commercial_manifest["assets"]}
        self.assertEqual(commercial_assets["character-v3"], "proposed")
        self.assertEqual(commercial_assets["pharmacy-plate-v3"], "proposed")
        self.assertTrue((commercial / "assets/pharmacist-nova-cartoon-v3.jpg").is_file())
        self.assertTrue((commercial / "keyframes/v3/s01-pharmacy.jpg").is_file())
        rows = intro_picture_blockers(ROOT)
        by_code = {}
        for row in rows:
            by_code.setdefault(row["code"], []).append(row)
        self.assertNotIn("character_approval", by_code)
        self.assertNotIn("visual_standard", by_code)
        self.assertNotIn("asset_unapproved", by_code)
        self.assertNotIn("paid_call", by_code)
        self.assertIn("credential", by_code)
        self.assertEqual(by_code["credential"][0]["path"], "RUNWAY_API_KEY")
        self.assertTrue({"f04-profile", "f05-reach", "f06-grasp", "f08-point"} <= {row["path"] for row in by_code["pose_missing"]})
        self.assertTrue({"pose-reach", "pose-grasp", "pose-lift", "pose-point"} <= {row["path"] for row in by_code["asset_missing"]})
        self.assertTrue({"ui-pos-staff", "ui-inventory", "ui-reports"} <= {row["path"] for row in by_code["product_ui"]})
        scenes = by_code["scene_asset"]
        self.assertEqual(len(scenes), 12)
        self.assertTrue(all("APPROVED" not in row["message"].split(";")[0] for row in scenes))
        self.assertNotIn("logo", by_code)
        self.assertNotIn("lock", by_code)
        project, load_errors = load_project(intro)
        self.assertEqual(load_errors, [])
        errors, _warnings = validate_project(project)
        self.assertEqual(
            [item["code"] for item in errors],
            ["semantic_unreviewed", "product_source_missing", "semantic_unreviewed", "semantic_unreviewed", "semantic_unreviewed", "semantic_unreviewed", "semantic_unreviewed"],
        )
        self.assertEqual([item["path"] for item in errors], [
            "s01-day-opens", "s03-stock-cash", "s03-stock-cash", "s06-selling",
            "s08-connected-data", "s09-people", "s12-cta",
        ])
        with self.assertRaises(ProductionError) as caught:
            render_project(intro, ROOT, label="must-not-write")
        self.assertEqual(caught.exception.status, "FAIL")
        self.assertEqual(
            [item["code"] for item in caught.exception.errors],
            ["semantic_unreviewed", "product_source_missing", "semantic_unreviewed", "semantic_unreviewed", "semantic_unreviewed", "semantic_unreviewed", "semantic_unreviewed"],
        )
        env = os.environ.copy()
        env["PYTHONIOENCODING"] = "utf-8"
        for name in (
            "_build_intro_film_blender.py",
            "_composite_intro_frames.py",
            "_mux_intro_film_blender.py",
        ):
            refused = subprocess.run(
                [sys.executable, str(outputs / name)],
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                env=env,
            )
            self.assertNotEqual(refused.returncode, 0, name)
            self.assertIn("BLOCKED", refused.stderr, name)
        self.assertEqual(before, {path.name for path in outputs.iterdir()})


class DesignBoardTests(unittest.TestCase):
    def test_unknown_board_is_blocked_and_mockup_is_labeled(self) -> None:
        from PIL import Image, ImageDraw

        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            logo = Image.new("RGBA", (80, 80), (255, 255, 255, 255))
            ImageDraw.Draw(logo).rectangle((20, 20, 60, 60), fill=(0, 40, 120, 255))
            logo.save(folder / "logo.png")
            missing = scene("board", 1.2, "Tiêu đề")
            missing["templateId"] = "designed_board"
            missing["params"] = {"kind": "handmade-scene"}
            missing["approvalStatus"] = "style_proof"
            write_project(folder, "board-fixture", [missing])
            with self.assertRaises(ProductionError) as caught:
                render_project(folder, folder, allow_style_proof=True, preview_only=True, label="board")
            self.assertEqual(caught.exception.errors[0]["code"], "board_kind")

            row = scene("screen", 1.2, "Bán hàng")
            row["templateId"] = "designed_board"
            row["onScreenText"] = ["Bán hàng", "Dữ liệu minh họa"]
            row["params"] = {
                "kind": "app_screen",
                "title": "Bán hàng",
                "dataLabel": "Dữ liệu minh họa",
                "disclosure": "Mockup — chưa phải giao diện chính thức",
                "blocks": [{"kind": "table", "columns": ["Mẫu"], "rows": [["Hàng mẫu"]]}],
            }
            row["approvalStatus"] = "style_proof"
            write_project(folder, "board-fixture", [row])
            dest = render_project(folder, folder, allow_style_proof=True, preview_only=True, label="screen")
            layout = json.loads((dest / "layout.json").read_text(encoding="utf-8"))
            self.assertIn("Dữ liệu minh họa", layout["texts"])
            self.assertIn("Mockup — chưa phải giao diện chính thức", layout["texts"])
            self.assertNotIn("novixa", (ENGINE / "design.py").read_text(encoding="utf-8").lower())

    def test_verified_screenshot_pixels_lead_the_ticket(self) -> None:
        import hashlib
        from PIL import Image

        from kit_video.still import inspect_asset

        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            screen = folder / "screen.png"
            color = (255, 0, 128)
            Image.new("RGB", (320, 180), color).save(screen)
            digest = hashlib.sha256(screen.read_bytes()).hexdigest()
            row = {
                "assetId": "screen",
                "kind": "screenshot",
                "source": "user_supplied",
                "path": "screen.png",
                "pathBase": "project",
                "version": "1",
                "usageRights": "licensed for this film as illustration",
                "approval": "approved",
                "role": "reference",
                "demoData": True,
                "demoLabel": "Dữ liệu minh họa",
                "visualSource": "SOFTWARE_SCREENSHOT",
                "sha256": digest,
                "label": "màn minh họa",
            }
            forged = dict(row, source="generated", licenseVerified=True)
            self.assertIn("visual_source", inspect_asset(forged)["reasons"])
            rebuilt = dict(row, visualSource="AI_RECONSTRUCTED_UI")
            self.assertIn("visual_source", inspect_asset(rebuilt)["reasons"])
            self.assertTrue(inspect_asset(row, screen)["allowed"])
            sale = scene("till", 1.2, "Bán hàng")
            sale["approvalStatus"] = "style_proof"
            sale["templateId"] = "designed_board"
            sale["onScreenText"] = ["Bán hàng", "Ghi nhận dữ liệu"]
            sale["assets"] = ["screen"]
            sale["params"] = {
                "kind": "workspace",
                "title": "Bán hàng",
                "dataLabel": "Dữ liệu minh họa",
                "fields": ["Tìm tên hoặc mã"],
                "foot": "Dữ liệu minh họa. Tổng 121000.",
                "aside": {"title": "Thanh toán", "lines": [{"label": "Khách", "value": "Khách mẫu"}]},
                "evidence": {"lines": [{"qty": 2, "price": 60500, "amount": 121000}], "total": 121000, "note": "minh họa"},
                "intent": {"idea": "Một tờ ghi nhận", "subject": "transaction", "emotion": "clarity", "focal": "ticket"},
            }
            write_project(
                folder,
                "screen-fixture",
                [sale],
                assets=[row],
                format={"aspectRatio": "16:9", "width": 640, "height": 360, "frameRate": 10},
            )
            dest = render_project(folder, folder, scene_ids=["till"], allow_style_proof=True, preview_only=True, label="screen")
            self.assertFalse((dest / "video.mp4").exists())
            frame_path = next((dest / "frames").glob("till-*.png"))
            frame = Image.open(frame_path).convert("RGB")
            layout = json.loads((dest / "layout.json").read_text(encoding="utf-8"))
            placed = layout["pictureSources"][0]
            crop = frame.crop(tuple(placed["box"]))
            self.assertEqual(placed["placement"], "native")
            self.assertEqual(crop.tobytes(), Image.new("RGB", crop.size, color).tobytes())
            label_box = next(item["box"] for item in layout["notices"] if item.get("text") == "Dữ liệu minh họa")
            screen_box = placed["box"]
            self.assertFalse(label_box[3] > screen_box[1] and label_box[1] < screen_box[3] and label_box[2] > screen_box[0] and label_box[0] < screen_box[2])
            headline = placed["headline"]
            self.assertLessEqual(headline[3], screen_box[1])
            self.assertEqual(layout["texts"].count("Dữ liệu minh họa"), 1)
            self.assertEqual(sum(1 for item in layout["notices"] if item.get("text") == "Dữ liệu minh họa"), 1)
            self.assertEqual(layout["deferredOnScreenText"], ["Bán hàng", "Ghi nhận dữ liệu"])
            self.assertNotIn("Bán hàng", layout["texts"])
            self.assertNotIn("Ghi nhận dữ liệu", layout["texts"])
            self.assertNotIn("Khách mẫu", layout["texts"])
            self.assertNotIn("Tìm tên hoặc mã", layout["texts"])
            self.assertEqual(layout["pictureSources"][0]["sha256"], digest)
            self.assertEqual(layout["pictureSources"][0]["visualSource"], "SOFTWARE_SCREENSHOT")
            self.assertTrue(any(note.get("assetId") == "screen" and note.get("demoLabel") == "Dữ liệu minh họa" for note in layout["drawnAssets"]))
            bad = dict(row, sha256="0" * 64)
            write_project(
                folder,
                "screen-fixture",
                [sale],
                assets=[bad],
                format={"aspectRatio": "16:9", "width": 640, "height": 360, "frameRate": 10},
            )
            with self.assertRaises(ProductionError) as caught:
                render_project(folder, folder, scene_ids=["till"], allow_style_proof=True, preview_only=True, label="mismatch")
            self.assertEqual(caught.exception.errors[0]["code"], "asset_hash")

    def test_vietnamese_glyphs_are_checked_against_the_font(self) -> None:
        from kit_video.visual_quality import missing_glyphs

        sample = "Một nhà thuốc: ế ệ ộ ỗ ữ ỳ"
        self.assertEqual(missing_glyphs(FONT, sample), [])
        self.assertIn("ệ", missing_glyphs(r"C:/Windows/Fonts/wingding.ttf", sample))
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            row = scene("type", 1.2, sample)
            row["templateId"] = "designed_board"
            row["subjects"] = ["statement"]
            row["onScreenText"] = [sample]
            row["params"] = {
                "kind": "triptych",
                "columns": [{"index": "01", "title": "Nhà thuốc", "body": "Dữ liệu được sắp lại."}],
            }
            row["approvalStatus"] = "style_proof"
            write_project(
                folder,
                "type-fixture",
                [row],
                format={"aspectRatio": "16:9", "width": 1280, "height": 720, "frameRate": 10},
            )
            dest = render_project(folder, folder, allow_style_proof=True, preview_only=True, label="type")
            layout = json.loads((dest / "layout.json").read_text(encoding="utf-8"))
            self.assertTrue(any(sample in item for item in layout["texts"]))
            self.assertFalse(layout["violations"])

    def test_scene_asset_keeps_its_role_and_hash(self) -> None:
        import hashlib
        from PIL import Image

        from kit_video.planning import select_scene_asset

        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            plate = folder / "plate.png"
            Image.new("RGB", (640, 360), (40, 90, 140)).save(plate)
            digest = hashlib.sha256(plate.read_bytes()).hexdigest()
            setting = {
                "assetId": "plate",
                "kind": "image",
                "source": "user_supplied",
                "path": "plate.png",
                "pathBase": "project",
                "version": "1",
                "usageRights": "licensed for this film",
                "approval": "approved",
                "role": "setting",
                "label": "quầy",
                "sha256": digest,
            }
            goods = scene("goods", 1.0, "Hàng")
            goods["subjects"] = ["goods"]
            goods["params"] = {"intent": {"subject": "goods"}}
            goods["assets"] = ["plate"]
            refused = select_scene_asset(goods, {"plate": setting}, folder, folder)
            self.assertEqual(refused["code"], "product_source_missing")
            self.assertIsNone(refused["assetId"])

            demo = dict(setting, assetId="screen", role="reference", approval="proposed", demoData=True, sha256=digest)
            sale = scene("sale", 1.0, "Bán hàng")
            sale["params"] = {"intent": {"subject": "transaction"}}
            sale["assets"] = ["screen"]
            blocked = select_scene_asset(sale, {"screen": demo}, folder, folder)
            self.assertEqual(blocked["code"], "asset_not_cleared")
            self.assertIsNone(blocked["assetId"])
            empty = scene("sale-empty", 1.0, "Bán hàng")
            empty["params"] = {"intent": {"subject": "transaction"}}
            empty["assets"] = []
            self.assertEqual(select_scene_asset(empty, {"screen": demo})["code"], "official_ui_missing")

            missing = scene("still", 1.0, "Nhà thuốc")
            missing["params"] = {"kind": "hero_still", "assetId": "plate", "intent": {"subject": "still"}}
            missing["assets"] = ["gone"]
            gone = dict(setting, assetId="gone", path="missing.png")
            self.assertEqual(select_scene_asset(missing, {"gone": gone}, folder, folder)["code"], "asset_missing")
            bad = dict(setting, sha256="0" * 64)
            self.assertEqual(select_scene_asset(missing, {"plate": bad}, folder, folder)["code"], "asset_hash")

            row = scene("still", 1.2, "Một ngày ở nhà thuốc")
            row["templateId"] = "designed_board"
            row["approvalStatus"] = "style_proof"
            row["onScreenText"] = ["Một ngày ở nhà thuốc"]
            row["assets"] = ["plate"]
            row["params"] = {
                "kind": "hero_still",
                "assetId": "plate",
                "preset": "hold",
                "title": "Một ngày ở nhà thuốc",
                "intent": {"idea": "Ảnh nền và chữ", "subject": "still", "focal": "image"},
            }
            write_project(
                folder,
                "asset-fixture",
                [row],
                assets=[setting],
                format={"aspectRatio": "16:9", "width": 1280, "height": 720, "frameRate": 10},
            )
            dest = render_project(folder, folder, allow_style_proof=True, preview_only=True, label="asset")
            layout = json.loads((dest / "layout.json").read_text(encoding="utf-8"))
            self.assertEqual(layout["assetBindings"][0]["assetId"], "plate")
            self.assertTrue(layout["assetBindings"][0]["integrity"]["match"])
            self.assertTrue(layout["assetBindings"][0]["integrity"]["verified"])
            self.assertEqual(layout["assetBindings"][0]["fit"], "cover")
            self.assertTrue(any("Một ngày ở nhà thuốc" in item for item in layout["texts"]))
            self.assertFalse(layout["violations"])

            from kit_video.planning import ui_evidence

            plain = {key: value for key, value in setting.items() if key != "sha256"}
            preview = select_scene_asset(row, {"plate": plain}, folder, folder)
            self.assertEqual(preview["status"], "unverified")
            self.assertEqual(preview["code"], "hash_unrecorded")
            self.assertFalse(preview["integrity"]["verified"])
            locked = dict(row, approvalStatus="approved", sceneId="locked")
            self.assertEqual(select_scene_asset(locked, {"plate": plain}, folder, folder)["status"], "blocked")
            write_project(folder, "asset-fixture", [locked], assets=[plain])
            with self.assertRaises(ProductionError) as caught:
                render_project(folder, folder, preview_only=True, label="locked")
            self.assertIn("hash_unrecorded", {item["code"] for item in caught.exception.errors})

            priced = {
                "params": {
                    "intent": {"subject": "transaction"},
                    "evidence": {
                        "lines": [
                            {"qty": 2, "price": 10000, "amount": 20000},
                            {"qty": 1, "price": 5000, "amount": 5000},
                        ],
                        "total": 25000,
                    },
                }
            }
            self.assertTrue(ui_evidence(priced)["consistent"])
            self.assertFalse(ui_evidence(priced)["commercial"])
            priced["params"]["evidence"]["lines"][1]["amount"] = 9000
            self.assertEqual(ui_evidence(priced)["code"], "ui_inconsistent")
            sample = {"params": {"intent": {"subject": "transaction"}, "table": {"rows": [["Hàng mẫu A", "1", "mẫu"]]}}}
            self.assertEqual(ui_evidence(sample)["code"], "ui_unpriced")
            self.assertEqual(ui_evidence({"params": {"intent": {"subject": "transaction"}, "evidence": {"lines": [{"qty": 1, "price": 0, "amount": 0}], "total": 0}}})["code"], "ui_empty")


class MotionCapabilityTests(unittest.TestCase):
    def test_camera_refuses_a_move_the_plate_cannot_support(self) -> None:
        move = CameraMove.parse("push_in", 0.08)
        self.assertEqual(move.preset, "push_in")
        with self.assertRaises(ProductionError) as caught:
            CameraMove.parse("pan", 0.4)
        self.assertEqual(caught.exception.status, "BLOCKED")
        self.assertEqual(caught.exception.errors[0]["code"], "camera_amount")
        with self.assertRaises(ProductionError):
            CameraMove.parse("orbit", 0.02)

    def test_provider_choice_follows_kind_credential_and_cap(self) -> None:
        prices = {"gen4_turbo": 0.05}
        graphic = select_motion({"kind": "logo"}, credential=True, spent_usd=0, cap_usd=5, prices_usd_per_second=prices)
        self.assertEqual(graphic["provider"], "internal")
        self.assertEqual(graphic["costUsd"], 0)
        missing = select_motion(
            {"kind": "character_action", "model": "gen4_turbo", "seconds": 5},
            credential=False, spent_usd=0, cap_usd=5, prices_usd_per_second=prices,
        )
        self.assertEqual(missing["code"], "CREDENTIAL_MISSING")
        self.assertEqual(missing["costUsd"], 0.25)
        over = select_motion(
            {"kind": "character_action", "model": "gen4_turbo", "seconds": 20},
            credential=True, spent_usd=4.2, cap_usd=5, prices_usd_per_second=prices,
        )
        self.assertEqual(over["code"], "BUDGET")
        allowed = select_motion(
            {"kind": "character_action", "model": "gen4_turbo", "seconds": 5},
            credential=True, spent_usd=4.2, cap_usd=5, prices_usd_per_second=prices,
        )
        self.assertEqual(allowed["provider"], "runway")
        self.assertEqual(allowed["costUsd"], 0.25)
        unknown = select_motion(
            {"kind": "character_action", "model": "gen4.5", "seconds": 5},
            credential=True, spent_usd=0, cap_usd=5, prices_usd_per_second=prices,
        )
        self.assertEqual(unknown["code"], "PRICE_UNKNOWN")

    def test_still_source_stays_separate_from_code_graphics(self) -> None:
        chart = select_still({"role": "chart"})
        self.assertEqual(chart["provider"], "code")
        self.assertEqual(chart["costUsd"], 0)
        self.assertFalse(chart["sent"])
        picture = select_still({"role": "product", "contentKey": "shelf-a"})
        self.assertEqual(picture["provider"], "generated")
        self.assertEqual(picture["code"], "NOT_INTEGRATED")
        self.assertIsNone(picture["fallback"])
        self.assertFalse(picture["sent"])
        calls = []
        blocked = submit_still(picture, transport=lambda *_args: calls.append("sent"))
        self.assertEqual(blocked["sent"], False)
        self.assertEqual(calls, [])
        mock = {
            "assetId": "mock-goods",
            "contentKey": "shelf-a",
            "kind": "image",
            "role": "product",
            "source": "generated",
            "approval": "proposed",
            "usageRights": "unverified",
            "label": "mockup hàng",
            "demoData": True,
        }
        refused = select_still({"role": "product", "contentKey": "shelf-a"}, [mock])
        self.assertEqual(refused["code"], "NOT_INTEGRATED")
        cleared = {
            **mock,
            "assetId": "goods-file",
            "source": "user_supplied",
            "approval": "approved",
            "usageRights": "licensed for this film",
            "label": "Hàng trên kệ",
            "demoData": False,
            "licenseVerified": True,
        }
        reused = select_still({"role": "product", "contentKey": "shelf-a"}, [cleared])
        self.assertEqual(reused["provider"], "library")
        self.assertEqual(reused["code"], "REUSE")
        self.assertEqual(reused["assetId"], "goods-file")
        self.assertEqual(reused["costUsd"], 0)
        report = access_report()
        self.assertFalse(report["stillProviderIntegrated"])
        self.assertEqual(report["spentUsd"], 0)
        self.assertFalse(report["network"])
        self.assertIn("STILL_IMAGE_API_KEY", report["credentialsPresent"])

    def test_shared_park_gemini_is_used_only_when_authorized(self) -> None:
        from PIL import Image

        from kit_video.still import execute_still, inspect_asset, park_image

        secret = "park-secret-not-for-this-module"
        calls: list[dict] = []

        def transport(request: dict) -> dict:
            calls.append(request)
            return {"ok": True, "sent": True, "bytes": b""}

        denied = [
            None,
            {},
            {
                "provider": "gemini",
                "imagesEnabled": True,
                "apiKeyConfigured": True,
                "imageModel": "gemini-3.1-flash-image",
                "geminiApiKey": secret,
            },
            {"capability": "IMAGE", "providerId": "GEMINI", "modelId": "gemini-3.1-flash-image", "enabled": False, "credentialId": "cred-1"},
            {"capability": "IMAGE", "providerId": "GEMINI", "modelId": "gemini-3.1-flash-image", "enabled": True},
            {"capability": "IMAGE", "providerId": "GEMINI", "modelId": "gemini-2.5-flash-image", "enabled": True, "credentialId": "cred-1"},
            {"capability": "VIDEO", "providerId": "runway", "modelId": "gen4_turbo", "enabled": True, "credentialId": "cred-1"},
        ]
        for config in denied:
            decision = select_still({"role": "product", "contentKey": "shelf"}, park=config)
            self.assertEqual(decision["code"], "NOT_INTEGRATED")
            self.assertFalse(decision["sent"])
            self.assertEqual(decision["costUsd"], 0)
            self.assertFalse(decision["separateConfig"])
            held = execute_still(
                {"role": "product", "contentKey": "shelf"},
                park=config,
                transport=transport,
                dest=Path("not-written.png"),
            )
            self.assertFalse(held["sent"])
            self.assertFalse(held["providerExecuted"])
            self.assertEqual(held["costUsd"], 0)
            self.assertNotEqual(held["status"], "stored")
        self.assertEqual(calls, [])

        park = {
            "capability": "IMAGE",
            "providerId": "GEMINI",
            "modelId": "gemini-3.1-flash-image",
            "enabled": True,
            "credentialId": "cred-park",
            "geminiApiKey": secret,
            "imageModel": "gemini-2.5-flash-image",
        }
        ready = select_still({"role": "product", "contentKey": "shelf"}, park=park)
        self.assertEqual(ready["provider"], "gemini")
        self.assertEqual(ready["code"], "SHARED")
        self.assertEqual(ready["model"], "gemini-3.1-flash-image")
        self.assertEqual(ready["configSource"], "tenant-ai")
        self.assertFalse(ready["separateConfig"])
        self.assertFalse(ready["sent"])
        self.assertEqual(ready["costUsd"], 0)
        self.assertNotIn(secret, json.dumps(ready))
        self.assertTrue(park_image(park)["authorized"])
        self.assertFalse(park_image(park)["separateConfig"])
        quiet = execute_still({"role": "product", "contentKey": "shelf"}, park=park)
        self.assertEqual(quiet["status"], "ready")
        self.assertFalse(quiet["sent"])
        self.assertEqual(quiet["costUsd"], 0)
        self.assertEqual(calls, [])
        previous = os.environ.get("GEMINI_API_KEY")
        private = os.environ.get("STILL_IMAGE_API_KEY")
        os.environ["GEMINI_API_KEY"] = secret
        os.environ["STILL_IMAGE_API_KEY"] = secret
        try:
            alone = select_still({"role": "product", "contentKey": "shelf"})
        finally:
            if previous is None:
                os.environ.pop("GEMINI_API_KEY", None)
            else:
                os.environ["GEMINI_API_KEY"] = previous
            if private is None:
                os.environ.pop("STILL_IMAGE_API_KEY", None)
            else:
                os.environ["STILL_IMAGE_API_KEY"] = private
        self.assertEqual(alone["code"], "NOT_INTEGRATED")

        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            source = folder / "shared.png"
            dest = folder / "candidate" / "shared.png"
            Image.new("RGB", (8, 8), (12, 80, 140)).save(source)

            def host(request: dict) -> dict:
                calls.append(request)
                self.assertEqual(request["model"], "gemini-3.1-flash-image")
                self.assertEqual(request["aspectRatio"], "16:9")
                self.assertEqual(request["references"], [])
                self.assertNotIn("geminiApiKey", request)
                self.assertNotIn(secret, json.dumps(request))
                return {"ok": True, "sent": True, "bytes": source.read_bytes()}

            stored = execute_still(
                {"role": "product", "contentKey": "shelf", "prompt": "Kệ hàng không chữ"},
                park=park,
                transport=host,
                dest=dest,
            )
            self.assertEqual(stored["status"], "stored")
            self.assertTrue(stored["success"])
            self.assertTrue(stored["sent"])
            self.assertEqual(stored["costUsd"], 0)
            self.assertEqual(stored["visualSource"], "AI_VISUAL")
            self.assertEqual(stored["approval"], "proposed")
            self.assertFalse(stored["commercial"])
            self.assertNotIn(secret, json.dumps(stored))
            self.assertEqual(len(calls), 1)
            claimed = {
                "assetId": "shared-still",
                "kind": "screenshot",
                "source": "generated",
                "visualSource": stored["visualSource"],
                "approval": "approved",
                "usageRights": "licensed for this film",
                "demoData": False,
            }
            self.assertIn("visual_source", inspect_asset(claimed)["reasons"])

    def test_visual_demand_does_not_take_an_unrelated_asset(self) -> None:
        cleared = {
            "assetId": "shelf-photo",
            "contentKey": "shelf-a",
            "kind": "image",
            "role": "product",
            "source": "library",
            "approval": "approved",
            "usageRights": "owned",
            "label": "kệ",
        }
        park = {
            "capability": "IMAGE",
            "providerId": "GEMINI",
            "modelId": "gemini-3.1-flash-image",
            "enabled": True,
            "credentialId": "cred-park",
        }
        need = {
            "role": "product",
            "contentKey": "shelf-a",
            "visualDemand": {"subject": "tie-shoes", "brand": "famixa"},
        }
        blocked = select_still(need, [cleared], park=park)
        self.assertEqual(blocked["code"], "NEED_RESOLVER")
        self.assertEqual(blocked["provider"], "visual-intelligence")
        self.assertFalse(blocked["sent"])
        self.assertEqual(blocked["costUsd"], 0)
        self.assertNotEqual(blocked.get("assetId"), "shelf-photo")
        honored = select_still({
            **need,
            "resolution": {"code": "NO_FIT", "reason": "Không có tài nguyên đúng nội dung."},
        }, [cleared])
        self.assertEqual(honored["code"], "NO_FIT")
        self.assertEqual(honored["paidProviderCalls"], 0)
        self.assertFalse(honored["sent"])

    def test_prompt_routes_follow_the_scene_kind(self) -> None:
        from kit_video.motion import build_request
        from kit_video.still import compile_route, execute_still

        goods = {
            "sceneId": "s03-stock-cash",
            "dialogue": "Có những mặt hàng bán vèo vèo, nhưng cũng có những hộp cứ nằm yên trên kệ.",
            "action": "Hai nhóm hàng mẫu. Không có số tiền.",
            "visualMetaphor": "Hàng nằm yên là vốn bị khóa.",
            "onScreenText": ["Hàng tồn kho cũng là vốn"],
            "assets": [],
            "params": {"intent": {"subject": "goods", "idea": "Nhóm hàng nằm yên là khối nặng, nhóm bán nhanh chỉ là phần nhỏ"}},
        }
        visual = compile_route(goods, [])
        self.assertEqual(visual["route"], "AI_VISUAL")
        self.assertTrue(visual["send"])
        self.assertIn("vốn bị khóa", visual["prompt"])
        self.assertIn("bán nhanh", visual["prompt"])
        self.assertIn("nằm lâu", visual["prompt"])
        self.assertIn("Không vẽ số liệu", visual["prompt"])
        self.assertNotIn("ACC", visual["prompt"])

        screen = {
            "sceneId": "s06-selling",
            "assets": ["ui-pos-demo-cart-v3"],
            "params": {"edit": True, "editRegions": ["background"], "evidence": {"lines": [{"name": "ACC 200mg", "qty": 2, "price": 60500, "amount": 121000}], "total": 139400}},
        }
        shot = {
            "assetId": "ui-pos-demo-cart-v3",
            "kind": "screenshot",
            "source": "user_supplied",
            "visualSource": "SOFTWARE_SCREENSHOT",
            "demoData": True,
            "approval": "approved",
        }
        placed = compile_route(screen, [shot])
        self.assertEqual(placed["route"], "SOFTWARE_SCREENSHOT")
        self.assertFalse(placed["send"])
        self.assertEqual(placed["prompt"], "")
        self.assertIn("139400", " ".join(placed["immutable"]))
        calls: list[dict] = []
        held = execute_still(
            {"role": "product", "route": "SOFTWARE_SCREENSHOT", "visualSource": "SOFTWARE_SCREENSHOT", "prompt": "redraw the POS"},
            park={"capability": "IMAGE", "providerId": "GEMINI", "modelId": "gemini-3.1-flash-image", "enabled": True, "credentialId": "cred-park"},
            transport=lambda request: calls.append(request),
            dest=Path("unused-screen.png"),
        )
        self.assertEqual(held["status"], "composite")
        self.assertFalse(held["sent"])
        self.assertEqual(calls, [])

        edit = compile_route({
            "sceneId": "edit-background",
            "assets": ["plate"],
            "params": {"edit": True, "editRegions": ["background"], "immutable": ["logo", "price 139400"]},
        }, [{"assetId": "plate", "kind": "image", "visualSource": "AI_VISUAL"}])
        self.assertEqual(edit["route"], "IMAGE_EDIT")
        self.assertTrue(edit["send"])
        self.assertIn("Only change: background", edit["prompt"])
        self.assertIn("price 139400", edit["prompt"])
        refused = compile_route({
            "sceneId": "edit-open",
            "assets": ["plate"],
            "params": {"edit": True, "editRegions": ["background"]},
        }, [{"assetId": "plate", "kind": "image", "visualSource": "AI_VISUAL"}])
        self.assertFalse(refused["send"])

        motion = build_request(
            {"id": "runway", "model": "gen4_turbo", "durationSeconds": 5, "camera": "push_in"},
            {"prompt": "Redraw the interface as a new layout.", "action": "Giữ nguyên màn hình, đẩy nhẹ vào."},
            "frame.png",
        )
        text = motion["json"]["promptText"]
        self.assertIn("Do not redesign", text)
        self.assertIn("Duration 5 seconds", text)
        self.assertIn("push_in", text)
        self.assertIn("Giữ nguyên màn hình", text)
        self.assertNotIn("new layout", text)

    def test_uncleared_picture_cannot_reach_a_frame(self) -> None:
        from PIL import Image

        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            image = folder / "goods.png"
            Image.new("RGB", (80, 80), (180, 40, 40)).save(image)
            row = scene("goods", 1, "Hàng")
            row["templateId"] = "plate_camera"
            row["params"] = {"assetId": "mock-goods", "preset": "hold", "amount": 0, "caption": "Hàng"}
            row["assets"] = ["mock-goods"]
            row["approvalStatus"] = "style_proof"
            asset = {
                "assetId": "mock-goods",
                "kind": "image",
                "source": "generated",
                "path": "goods.png",
                "pathBase": "project",
                "version": "1",
                "usageRights": "unverified",
                "approval": "proposed",
                "role": "product",
                "label": "mockup",
                "demoData": True,
            }
            write_project(folder, "goods-fixture", [row], assets=[asset])
            project, load_errors = load_project(folder)
            self.assertEqual(load_errors, [])
            codes = {item["code"] for item in validate_project(project)[0]}
            self.assertIn("asset_not_cleared", codes)
            with self.assertRaises(ProductionError) as caught:
                render_project(folder, folder, allow_style_proof=True, preview_only=True, label="goods")
            self.assertIn("asset_not_cleared", {item["code"] for item in caught.exception.errors})
            self.assertFalse((folder / "outputs").exists() and any((folder / "outputs").rglob("*.png")))
            view = registry_view([asset], [row])
            self.assertEqual(view[0]["scenes"], ["goods"])
            self.assertFalse(view[0]["allowed"])
            intent = scene("shelf", 1, "Hàng")
            intent["params"] = {"intent": {"subject": "goods"}}
            intent["assets"] = []
            self.assertEqual(goods_source(intent, {})["code"], "product_source_missing")
            asset["approval"] = "approved"
            asset["usageRights"] = "licensed for this film"
            asset["source"] = "user_supplied"
            asset["label"] = "Hàng trên kệ"
            asset["demoData"] = False
            self.assertEqual(goods_source(intent, {"mock-goods": asset})["code"], "product_source_missing")
            intent["assets"] = ["mock-goods"]
            self.assertEqual(goods_source(intent, {"mock-goods": asset})["status"], "ready")

    def test_picture_hash_must_match_before_paint(self) -> None:
        from PIL import Image

        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            image = folder / "plate.png"
            Image.new("RGB", (320, 180), (40, 90, 140)).save(image)
            row = scene("plate", 1.2, "Biển")
            row["templateId"] = "plate_camera"
            row["params"] = {"assetId": "plate", "preset": "push_in", "amount": 0.08, "caption": "Biển"}
            row["assets"] = ["plate"]
            row["approvalStatus"] = "style_proof"
            write_project(folder, "hash-fixture", [row], assets=[{
                "assetId": "plate",
                "kind": "image",
                "source": "user_supplied",
                "path": "plate.png",
                "pathBase": "project",
                "version": "1",
                "usageRights": "test",
                "approval": "approved",
                "role": "setting",
                "label": "fixture",
                "sha256": "0" * 64,
            }])
            with self.assertRaises(ProductionError) as caught:
                render_project(folder, folder, allow_style_proof=True, preview_only=True, label="hash")
            self.assertIn("asset_hash", {item["code"] for item in caught.exception.errors})

    def test_cleared_product_picture_reaches_the_goods_frame(self) -> None:
        from PIL import Image

        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            Image.new("RGB", (240, 240), (176, 48, 32)).save(folder / "still.png")
            Image.new("RGB", (240, 240), (16, 120, 72)).save(folder / "fast.png")
            row = scene("goods", 1.0, "Hàng")
            row["templateId"] = "designed_board"
            row["approvalStatus"] = "style_proof"
            row["onScreenText"] = ["Hàng tồn", "Dữ liệu minh họa"]
            row["assets"] = ["still-goods", "fast-goods"]
            row["params"] = {
                "kind": "catalog",
                "title": "Hàng tồn",
                "disclosure": "Mockup — chưa phải giao diện chính thức",
                "dataLabel": "Dữ liệu minh họa",
                "summary": "Một khối lớn, một khối nhỏ.",
                "intent": {"idea": "Khác nhau bằng bố cục", "subject": "goods", "emotion": "clarity", "focal": "contrast"},
                "tiles": [
                    {"name": "Hàng mẫu nhanh", "state": "Bán nhanh", "tone": "accent"},
                    {"name": "Hàng mẫu nằm", "state": "Nằm yên", "tone": "alt"},
                ],
            }
            rows = [
                {
                    "assetId": "still-goods",
                    "kind": "image",
                    "source": "user_supplied",
                    "path": "still.png",
                    "pathBase": "project",
                    "version": "1",
                    "usageRights": "fixture",
                    "approval": "approved",
                    "role": "product",
                    "visualRole": "still",
                    "label": "Khối nằm",
                    "contentKey": "fixture-still",
                },
                {
                    "assetId": "fast-goods",
                    "kind": "image",
                    "source": "user_supplied",
                    "path": "fast.png",
                    "pathBase": "project",
                    "version": "1",
                    "usageRights": "fixture",
                    "approval": "approved",
                    "role": "product",
                    "visualRole": "fast",
                    "label": "Khối nhanh",
                    "contentKey": "fixture-fast",
                },
            ]
            write_project(
                folder,
                "goods-frame",
                [row],
                assets=rows,
                format={"aspectRatio": "16:9", "width": 1280, "height": 720, "frameRate": 8},
            )
            dest = render_project(folder, folder, allow_style_proof=True, preview_only=True, label="goods-frame")
            frame = Image.open(next((dest / "frames").glob("*.png"))).convert("RGB")
            self.assertEqual(frame.size, (1280, 720))
            colors = {
                frame.getpixel((x, y))
                for y in range(0, frame.height, 2)
                for x in range(0, frame.width, 2)
            }
            self.assertIn((176, 48, 32), colors)
            self.assertIn((16, 120, 72), colors)
            log = json.loads((dest / "render-log.json").read_text(encoding="utf-8"))
            clearance = {item["id"]: item["clearance"] for item in log["inputs"] if item["kind"] == "asset"}
            self.assertEqual(clearance, {"still-goods": "allowed", "fast-goods": "allowed"})
            again = select_still({"role": "product", "contentKey": "fixture-still"}, rows)
            self.assertEqual(again["code"], "REUSE")
            self.assertEqual(again["costUsd"], 0)

    def test_goods_scene_does_not_replace_a_missing_picture_with_cards(self) -> None:
        from kit_video.canvas import FrameContext
        from kit_video.design import designed_board
        from kit_video.visual_quality import assess_breathing, picture_share

        style = manifest("ignored", [])["visualStyle"]
        style["artDirection"] = {"principles": ["one-idea"], "light": "warm-paper", "depth": "flat"}
        row = scene("goods", 1, "Hàng")
        row["templateId"] = "designed_board"
        row["params"] = {
            "kind": "catalog",
            "title": "Hàng",
            "disclosure": "Mockup — chưa phải giao diện chính thức",
            "dataLabel": "Dữ liệu minh họa",
            "intent": {"idea": "Hai nhóm hàng", "subject": "goods", "emotion": "clarity", "focal": "contrast"},
            "tiles": [{"name": "Mẫu", "state": "Nằm", "tone": "alt"}],
        }
        ctx = FrameContext(row, style, 640, 360, 0.5, 1, {})
        with self.assertRaises(ProductionError) as caught:
            designed_board(ctx, {}, Path("."), Path("."))
        self.assertEqual(caught.exception.errors[0]["code"], "asset_required")
        safe = {"top": 0.06, "right": 0.06, "bottom": 0.06, "left": 0.06}
        self.assertEqual(assess_breathing({"Xin": [10, 10, 40, 30]}, 640, 360, safe, 8)[0]["code"], "spacing_flush")
        self.assertEqual(assess_breathing({"Xin": [80, 50, 200, 80]}, 640, 360, safe, 8), [])

        from PIL import Image

        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            Image.new("RGB", (320, 180), (20, 90, 40)).save(folder / "scene.png")
            plate = scene("plate", 1, "Hàng")
            plate["templateId"] = "designed_board"
            plate["approvalStatus"] = "style_proof"
            plate["assets"] = ["scene-plate"]
            plate["onScreenText"] = ["Hàng", "Dữ liệu minh họa"]
            plate["params"] = {
                "kind": "catalog",
                "title": "Hàng",
                "summary": "Một ảnh dẫn cảnh.",
                "disclosure": "Mockup — chưa phải giao diện chính thức",
                "dataLabel": "Dữ liệu minh họa",
                "intent": {"idea": "Ảnh hàng dẫn cảnh", "subject": "goods", "emotion": "clarity", "focal": "contrast"},
                "tiles": [
                    {"name": "Nhóm nằm", "state": "Nằm yên", "tone": "alt"},
                    {"name": "Nhóm nhanh", "state": "Bán nhanh", "tone": "accent"},
                ],
            }
            asset = {
                "assetId": "scene-plate",
                "kind": "image",
                "source": "user_supplied",
                "path": "scene.png",
                "pathBase": "project",
                "version": "1",
                "usageRights": "fixture",
                "approval": "approved",
                "role": "product",
                "visualRole": "scene",
                "label": "Một khung hàng",
            }
            write_project(
                folder,
                "scene-plate",
                [plate],
                assets=[asset],
                visualStyle=style,
                format={"aspectRatio": "16:9", "width": 640, "height": 360, "frameRate": 8},
            )
            project, load_errors = load_project(folder)
            self.assertEqual(load_errors, [])
            self.assertEqual([item["code"] for item in validate_project(project)[0]], ["semantic_unreviewed"])
            ctx = FrameContext(plate, style, 640, 360, 0.8, 1, {})
            designed_board(ctx, {"scene-plate": asset}, folder, folder)
            self.assertGreater(picture_share(ctx.cache["pictureBoxes"], 640, 360), 0.35)
            self.assertEqual(ctx.image.getpixel((320, 180))[:3], (20, 90, 40))
            self.assertIn("Nằm yên", ctx.texts)
            self.assertIn("Bán nhanh", ctx.texts)

    def test_shared_motion_modules_are_not_brand_specific(self) -> None:
        for name in ("camera.py", "motion_select.py", "still.py"):
            text = (ENGINE / name).read_text(encoding="utf-8").lower()
            self.assertNotIn("novixa", text)
            self.assertNotIn("famixa", text)

    def test_plate_camera_renders_a_registered_still(self) -> None:
        from PIL import Image

        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            image = folder / "plate.png"
            Image.new("RGB", (320, 180), (40, 90, 140)).save(image)
            row = scene("plate", 1.2, "Biển")
            row["templateId"] = "plate_camera"
            row["params"] = {"assetId": "plate", "preset": "push_in", "amount": 0.08, "caption": "Biển"}
            row["assets"] = ["plate"]
            row["approvalStatus"] = "style_proof"
            write_project(folder, "plate-fixture", [row], assets=[{
                "assetId": "plate",
                "kind": "image",
                "source": "user_supplied",
                "path": "plate.png",
                "pathBase": "project",
                "version": "1",
                "usageRights": "test",
                "approval": "approved",
                "role": "setting",
                "label": "fixture",
            }])
            dest = render_project(folder, folder, allow_style_proof=True, preview_only=True, label="plate")
            self.assertTrue((dest / "frames" / "plate-f0.50.png").is_file())
            layout = json.loads((dest / "layout.json").read_text(encoding="utf-8"))
            self.assertIn("Biển", layout["texts"])
            log = json.loads((dest / "render-log.json").read_text(encoding="utf-8"))
            plate = next(row for row in log["inputs"] if row["id"] == "plate")
            self.assertEqual(plate["sha256"], hashlib.sha256(image.read_bytes()).hexdigest())
            self.assertTrue(all(row["sha256"] for row in log["inputs"] if row["kind"] == "font"))


class ArchitectureTests(unittest.TestCase):
    def test_contract_budgets_space_without_squeezing_type(self) -> None:
        from kit_video.contract import DesignContract

        style = {
            "profileId": "north",
            "palette": {
                "background": [240, 236, 228],
                "ink": [12, 24, 48],
                "accent": [0, 110, 170],
                "paper": [252, 250, 246],
            },
            "safeArea": {"top": 0.06, "right": 0.06, "bottom": 0.06, "left": 0.06},
        }
        other = {
            **style,
            "profileId": "south",
            "palette": {**style["palette"], "ink": [120, 32, 24], "accent": [180, 90, 20]},
        }
        north = DesignContract.from_frame(style, 1280, 720)
        south = DesignContract.from_frame(other, 1280, 720)
        self.assertGreater(north.px("title"), north.px("body"))
        self.assertGreaterEqual(north.px("micro"), north.min_px)
        height, kept = north.row_height(12, 80, "body")
        self.assertGreaterEqual(height, north.px("body"))
        self.assertLess(kept, 12)
        self.assertTrue(north.readable_pair("ink", "paper"))
        self.assertFalse(north.readable_pair("ink", "ink"))
        self.assertEqual(north.place_block(0, 100, 20), 40)
        self.assertEqual(north.place_block(0, 100, 80), 0)
        self.assertNotEqual(north.palette["ink"], south.palette["ink"])
        self.assertTrue(north.supports("workspace"))
        self.assertFalse(north.supports("handmade-scene"))
        crowded = north.fit_board("workspace", {"table": {"rows": [["Mẫu"]] * 40}})
        self.assertEqual(crowded["status"], "blocked")
        self.assertEqual(crowded["code"], "layout_density")
        chosen = north.fit_board("", {"lanes": [{"label": "Việc"}]})
        self.assertEqual(chosen["status"], "ok")
        self.assertEqual(chosen["kind"], "lanes")
        intro, intro_errors = load_project(ROOT / "proof/kit-video-production-v1/projects/novixa-intro-film")
        self.assertEqual(intro_errors, [])
        intro_findings = validate_project(intro)[0]
        self.assertEqual(
            [item["code"] for item in intro_findings],
            ["semantic_unreviewed", "product_source_missing", "semantic_unreviewed", "semantic_unreviewed", "semantic_unreviewed", "semantic_unreviewed", "semantic_unreviewed"],
        )
        self.assertEqual([item["path"] for item in intro_findings], [
            "s01-day-opens", "s03-stock-cash", "s03-stock-cash", "s06-selling",
            "s08-connected-data", "s09-people", "s12-cta",
        ])
        for name in ("contract.py", "design.py", "visual_quality.py", "composition.py", "still.py", "planning.py"):
            text = (ENGINE / name).read_text(encoding="utf-8").lower()
            self.assertNotIn("novixa", text)
            self.assertNotIn("famixa", text)

    def test_two_brands_render_apart_and_the_log_names_the_inputs(self) -> None:
        from PIL import Image

        def paint(folder: Path, brand_id: str, background: list[int]) -> Path:
            row = scene("only", 1.0, "Bảng")
            row["templateId"] = "designed_board"
            row["approvalStatus"] = "style_proof"
            row["onScreenText"] = ["Bảng", "Dữ liệu minh họa"]
            row["params"] = {
                "kind": "lanes",
                "title": "Bảng",
                "disclosure": "Mockup — chưa phải giao diện chính thức",
                "dataLabel": "Dữ liệu minh họa",
                "lanes": [{"label": "Việc", "events": [{"text": "Mẫu", "at": 0.4}]}],
            }
            style = manifest("ignored", [])["visualStyle"]
            style["profileId"] = brand_id
            style["palette"] = {**style["palette"], "background": background, "ink": [16, 16, 16]}
            write_project(
                folder,
                brand_id,
                [row],
                brand={"id": brand_id, "name": brand_id},
                visualStyle=style,
                script={"version": "fixture-2", "approvalStatus": "approved", "sourcePath": "script.txt"},
            )
            return render_project(folder, folder, allow_style_proof=True, preview_only=True, label="brand")

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            north = paint(root / "north", "north-brand", [20, 40, 80])
            south = paint(root / "south", "south-brand", [210, 180, 60])
            north_px = Image.open(next((north / "frames").glob("*.png"))).getpixel((4, 40))
            south_px = Image.open(next((south / "frames").glob("*.png"))).getpixel((4, 40))
            self.assertNotEqual(north_px, south_px)
            log = json.loads((north / "render-log.json").read_text(encoding="utf-8"))
            self.assertEqual(log["engineVersion"], "1.8.0")
            self.assertEqual(log["brandId"], "north-brand")
            self.assertEqual(log["styleProfileId"], "north-brand")
            self.assertEqual(log["scriptVersion"], "fixture-2")
            self.assertTrue(log["sha256ManifestAndShotPlan"])
            layout = json.loads((north / "layout.json").read_text(encoding="utf-8"))
            self.assertEqual(layout["contract"]["profileId"], "north-brand")
            self.assertIn("title", layout["contract"]["roles"])
            self.assertEqual(layout["layoutDecisions"][0]["status"], "ok")
            self.assertEqual(layout["layoutDecisions"][0]["kind"], "lanes")

    def test_unreadable_density_is_refused_before_paint(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            row = scene("crowded", 1.2, "Bảng")
            row["templateId"] = "designed_board"
            row["approvalStatus"] = "style_proof"
            row["onScreenText"] = ["Bảng"]
            row["params"] = {
                "kind": "workspace",
                "title": "Bảng",
                "table": {"columns": ["Mục"], "rows": [["Mẫu"]] * 14},
            }
            write_project(folder, "crowded-board", [row])
            with self.assertRaises(ProductionError) as caught:
                render_project(folder, folder, allow_style_proof=True, preview_only=True, label="crowded")
            self.assertEqual(caught.exception.errors[0]["code"], "layout_density")

    def test_schematic_and_unlabeled_mockups_are_refused(self) -> None:
        from kit_video.contract import DesignContract
        from kit_video.visual_quality import assess_film, assess_scene

        style = manifest("ignored", [])["visualStyle"]
        contract = DesignContract.from_frame(style, 1280, 720)
        schematic = assess_scene(contract, "catalog", {
            "disclosure": "Mockup — chưa phải giao diện chính thức",
            "treatment": "lines",
            "tiles": [{"name": "Mẫu", "state": "Mẫu"}],
        })
        self.assertEqual(schematic["status"], "blocked")
        self.assertEqual(schematic["code"], "provisional_imagery")
        unlabeled = assess_scene(contract, "workspace", {"title": "Bán", "table": {"rows": [["Mẫu"]]}})
        self.assertEqual(unlabeled["code"], "mockup_unlabeled")
        weak = {
            **style,
            "palette": {**style["palette"], "ink": [250, 250, 250], "paper": [248, 248, 248]},
        }
        pale = assess_scene(DesignContract.from_frame(weak, 1280, 720), "brand_center", {})
        self.assertEqual(pale["code"], "contrast")
        repeated = assess_film([
            {"sceneId": f"s{index}", "templateId": "designed_board", "params": {"kind": "lanes", "title": "Một"}}
            for index in range(3)
        ])
        self.assertEqual(repeated[0]["code"], "visual_repeat")
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            row = scene("plain", 1.0, "Bảng")
            row["templateId"] = "designed_board"
            row["approvalStatus"] = "style_proof"
            row["params"] = {
                "kind": "catalog",
                "title": "Bảng",
                "treatment": "schematic",
                "disclosure": "Mockup — chưa phải giao diện chính thức",
                "tiles": [{"name": "Mẫu", "state": "Mẫu"}],
            }
            write_project(folder, "plain-board", [row])
            with self.assertRaises(ProductionError) as caught:
                render_project(folder, folder, allow_style_proof=True, preview_only=True, label="plain")
            self.assertEqual(caught.exception.errors[0]["code"], "provisional_imagery")

    def test_notices_are_drawn_once_and_do_not_stack(self) -> None:
        from kit_video.canvas import FrameContext
        from kit_video.contract import DesignContract
        from kit_video.design import designed_board, place_notices
        from kit_video.still import inspect_asset
        from kit_video.visual_quality import assess_scene

        def overlaps(a, b):
            width = min(a[2], b[2]) - max(a[0], b[0])
            height = min(a[3], b[3]) - max(a[1], b[1])
            return width > 0 and height > 0

        style = manifest("ignored", [])["visualStyle"]
        contract = DesignContract.from_frame(style, 1280, 720)
        self.assertEqual(
            assess_scene(contract, "workspace", {"title": "Bán", "dataLabel": "Dữ liệu minh họa"})["status"],
            "ok",
        )
        disclosure = "Mockup — chưa phải giao diện chính thức"
        demo = "Dữ liệu minh họa"
        row = scene("notices", 4, "Bán hàng")
        row["templateId"] = "designed_board"
        row["approvalStatus"] = "style_proof"
        row["onScreenText"] = ["Bán hàng"]
        row["params"] = {
            "kind": "workspace",
            "title": "Bán hàng",
            "disclosure": disclosure,
            "dataLabel": demo,
            "table": {"columns": ["Hàng", "SL"], "rows": [["ACC 200mg", "2"]]},
            "intent": {"idea": "Một bảng", "subject": "record", "emotion": "clarity", "focal": "row"},
        }
        draws = []
        original = FrameContext.text

        def logged(self, content, xy, size, fill, bold=False, anchor="lt", opacity=1):
            if content in {disclosure, demo}:
                draws.append((content, tuple(fill)))
            return original(self, content, xy, size, fill, bold=bold, anchor=anchor, opacity=opacity)

        FrameContext.text = logged
        try:
            ctx = FrameContext(row, style, 1280, 720, 3.4, 4.0, {})
            designed_board(ctx, {}, Path("."), Path("."))
            place_notices(ctx)
        finally:
            FrameContext.text = original
        self.assertEqual([item[0] for item in draws].count(disclosure), 1)
        self.assertEqual([item[0] for item in draws].count(demo), 1)
        paper = tuple(style["palette"]["paper"])
        for _text, fill in draws:
            self.assertEqual(fill, paper)
            self.assertNotEqual(fill, (176, 92, 62))
        boxes = [item["box"] for item in ctx._notice_boxes]
        self.assertEqual([item["text"] for item in ctx._notice_boxes], [disclosure, demo])
        left, top, right, bottom = ctx.safe()
        self.assertFalse(overlaps(boxes[0], boxes[1]))
        for box in boxes:
            self.assertGreaterEqual(box[0], left)
            self.assertGreaterEqual(box[1], top)
            self.assertLessEqual(box[2], right)
            self.assertLessEqual(box[3], bottom)
        wrapped = scene("wrap", 2, "Bảng")
        wrapped["params"] = {
            "disclosure": "Nhãn cảnh báo một. " * 3,
            "dataLabel": "Nhãn cảnh báo hai. " * 3,
        }
        narrow = FrameContext(wrapped, style, 480, 270, 1.0, 2.0, {})
        place_notices(narrow)
        again = len(narrow.cache["notices"])
        place_notices(narrow)
        self.assertEqual(len(narrow.cache["notices"]), again)
        pair = [item["box"] for item in narrow._notice_boxes]
        self.assertEqual(len(pair), 2)
        self.assertFalse(overlaps(pair[0], pair[1]))
        safe = narrow.safe()
        for box in pair:
            self.assertGreaterEqual(box[0], safe[0])
            self.assertGreaterEqual(box[1], safe[1])
            self.assertLessEqual(box[2], safe[2])
            self.assertLessEqual(box[3], safe[3])
        ordinary = {
            "assetId": "plate",
            "kind": "image",
            "role": "setting",
            "approval": "approved",
            "usageRights": "licensed for this film",
            "source": "user_supplied",
        }
        demo_row = dict(ordinary, assetId="cart", kind="screenshot", role="reference", demoData=True, demoLabel="Dữ liệu minh họa")
        self.assertEqual(inspect_asset(ordinary)["use"], "production")
        self.assertEqual(inspect_asset(demo_row)["use"], "illustration")
        self.assertFalse(inspect_asset(dict(demo_row, approval="proposed"))["allowed"])

    def test_composition_follows_intent_for_more_than_one_brand(self) -> None:
        from PIL import Image

        from kit_video.composition import compose, family_for
        from kit_video.visual_quality import assess_composition, assess_intent
        from kit_video.composition import art_profile, scene_intent

        east = {
            "profileId": "east",
            "artDirection": {
                "light": "warm-paper",
                "material": "paper",
                "depth": "layered",
                "principles": ["one-idea"],
            },
        }
        south = {
            "profileId": "south",
            "artDirection": {"light": "ink-field", "depth": "flat", "principles": ["one-idea"]},
        }
        schedule = {
            "kind": "lanes",
            "intent": {"idea": "Một ngày chạy dọc một trục", "subject": "schedule", "emotion": "pressure", "focal": "moment"},
            "lanes": [{"label": "Việc", "events": [{"text": "Mẫu", "at": 0.4}]}],
        }
        goods = {
            "kind": "catalog",
            "intent": {"idea": "Một khối nặng và một phần nhỏ", "subject": "goods", "emotion": "clarity", "focal": "contrast"},
            "tiles": [{"name": "Mẫu", "state": "Nằm", "tone": "alt"}],
        }
        self.assertEqual(family_for(schedule, "lanes"), "spine")
        self.assertEqual(family_for(goods, "catalog"), "mass-contrast")
        self.assertEqual(compose(east, schedule, "lanes", 0, 2)["ground"], "warm-paper")
        self.assertEqual(compose(south, {"kind": "triptych", "intent": {"idea": "Chuỗi", "subject": "statement", "emotion": "resolve", "focal": "line"}}, "triptych", 0, 2)["ground"], "ink-field")
        self.assertNotEqual(compose(east, schedule, "lanes", 0, 2)["family"], compose(east, goods, "catalog", 0, 2)["family"])
        missing = assess_intent(art_profile(east), scene_intent({}))
        self.assertEqual(missing["code"], "intent_missing")
        repeated = assess_composition([
            {"sceneId": f"s{index}", "templateId": "designed_board", "params": schedule}
            for index in range(3)
        ])
        self.assertEqual(repeated[0]["code"], "composition_repeat")

        def paint(folder: Path, brand_id: str, params: dict, background: list[int]) -> Path:
            row = scene("only", 1.0, "Mẫu")
            row["templateId"] = "designed_board"
            row["approvalStatus"] = "style_proof"
            row["onScreenText"] = ["Mẫu", "Dữ liệu minh họa"]
            row["params"] = {
                **params,
                "title": "Mẫu",
                "disclosure": "Mockup — chưa phải giao diện chính thức",
                "dataLabel": "Dữ liệu minh họa",
            }
            assets = []
            if (params.get("intent") or {}).get("subject") == "goods":
                folder.mkdir(parents=True, exist_ok=True)
                Image.new("RGB", (32, 32), (40, 80, 60)).save(folder / "goods.png")
                assets = [{
                    "assetId": "goods-file",
                    "kind": "image",
                    "source": "user_supplied",
                    "path": "goods.png",
                    "pathBase": "project",
                    "version": "1",
                    "usageRights": "fixture",
                    "approval": "approved",
                    "role": "product",
                    "label": "Hàng mẫu",
                }]
                row["assets"] = ["goods-file"]
            style = manifest("ignored", [])["visualStyle"]
            style["profileId"] = brand_id
            style["palette"] = {**style["palette"], "background": background, "ink": [16, 24, 48], "paper": [250, 246, 238]}
            style["artDirection"] = {"light": "warm-paper", "depth": "layered", "principles": ["one-idea"]}
            write_project(
                folder,
                brand_id,
                [row],
                brand={"id": brand_id, "name": brand_id},
                visualStyle=style,
                format={"aspectRatio": "16:9", "width": 640, "height": 360, "frameRate": 8},
                assets=assets,
            )
            return render_project(folder, folder, allow_style_proof=True, preview_only=True, label="compose")

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            spine = paint(root / "east", "kit-sample-east", schedule, [236, 228, 220])
            mass = paint(root / "south", "kit-sample-south", goods, [220, 230, 224])
            spine_layout = json.loads((spine / "layout.json").read_text(encoding="utf-8"))
            mass_layout = json.loads((mass / "layout.json").read_text(encoding="utf-8"))
            self.assertEqual(spine_layout["compositionDecisions"][0]["family"], "spine")
            self.assertEqual(mass_layout["compositionDecisions"][0]["family"], "mass-contrast")
            self.assertEqual(spine_layout["compositionDecisions"][0]["boundAssets"], [])
            spine_px = Image.open(next((spine / "frames").glob("*.png"))).resize((32, 18)).tobytes()
            mass_px = Image.open(next((mass / "frames").glob("*.png"))).resize((32, 18)).tobytes()
            self.assertNotEqual(spine_px, mass_px)

    def test_picture_plan_blocks_missing_assets_and_retries(self) -> None:
        from kit_video.planning import next_attempt, production_gate, visual_plan

        goods = {
            "sceneId": "goods",
            "approvalStatus": "style_proof",
            "storyGoal": "Cho thấy hai nhóm hàng.",
            "action": "Một khối nằm yên, một nhóm tách ra.",
            "onScreenText": ["Hàng mẫu"],
            "params": {"kind": "catalog", "intent": {"idea": "Khối và phần nhỏ", "subject": "goods", "focal": "contrast"}},
        }
        blocked = production_gate(visual_plan(goods, {}))
        self.assertEqual(blocked["status"], "BLOCKED")
        self.assertEqual(blocked["blockers"][0]["code"], "product_source_missing")
        demo = {
            "assetId": "screen",
            "kind": "screenshot",
            "role": "product_ui",
            "approval": "approved",
            "usageRights": "nội bộ",
            "demoData": True,
            "source": "library",
        }
        transaction = {
            "sceneId": "till",
            "approvalStatus": "style_proof",
            "assets": ["screen"],
            "params": {
                "kind": "workspace",
                "disclosure": "Mockup — chưa phải giao diện chính thức",
                "intent": {"idea": "Ghi một lần bán", "subject": "transaction", "focal": "ticket"},
            },
        }
        refused = production_gate(visual_plan(transaction, {"screen": demo}))
        self.assertEqual(refused["blockers"][0]["code"], "asset_not_cleared")
        posed = {
            "sceneId": "choice",
            "params": {
                "kind": "dashboard",
                "picture": {"actions": ["decide"]},
                "intent": {"idea": "Người chọn", "subject": "relation", "focal": "connection"},
            },
        }
        pose = production_gate(visual_plan(posed, {}))
        self.assertEqual(pose["blockers"][0]["code"], "pose_unsupported")
        ranked = {
            "sceneId": "ranked",
            "params": {
                "kind": "dashboard",
                "chart": {"title": "Mẫu", "bars": [{"label": "A", "amount": 0.2}, {"label": "B", "amount": 0.8}]},
                "intent": {"idea": "Nối", "subject": "relation", "focal": "connection"},
            },
        }
        compared = production_gate(visual_plan(ranked, {}))
        self.assertEqual(compared["status"], "FAIL")
        self.assertEqual(compared["failures"][0]["code"], "false_comparison")
        looked = production_gate(
            visual_plan(ranked, {}),
            review={"status": "PASS", "criteria": ["safe_area"]},
        )
        self.assertEqual(looked["blockers"][0]["code"], "standard_lowered")
        same = {"method": "code", "code": "cards", "criteria": ["meaning"]}
        self.assertEqual(next_attempt([same], dict(same))["code"], "same_method")
        self.assertEqual(next_attempt([same, same, same], {"method": "photo", "code": "other"})["code"], "attempt_limit")
        lowered = next_attempt(
            [{"method": "code", "code": "cards", "criteria": ["meaning", "product_picture"]}],
            {"method": "photo", "code": "other", "status": "PASS", "criteria": ["meaning"]},
        )
        self.assertEqual(lowered["code"], "standard_lowered")
        from kit_video.planning import attempt_round, select_method

        missing = select_method(goods, {})
        self.assertEqual(missing["method"], "blocked")
        self.assertEqual(missing["stillCode"], "NOT_INTEGRATED")
        self.assertNotEqual(missing["provider"], "code")
        self.assertFalse(missing["sent"])
        self.assertEqual(missing["costUsd"], 0)
        self.assertFalse(missing["commercial"])
        screen = select_method(transaction, {"screen": demo})
        self.assertEqual(screen["code"], "asset_not_cleared")
        self.assertNotEqual(screen["provider"], "code")
        self.assertFalse(screen["paint"])
        acted = select_method(posed, {"face": {"assetId": "face", "role": "character", "approval": "approved", "usageRights": "fixture", "kind": "image"}})
        self.assertEqual(acted["code"], "pose_unsupported")
        self.assertFalse(acted["paint"])
        ranked_try = attempt_round(ranked, {})
        self.assertEqual(ranked_try["decision"]["code"], "false_comparison")
        self.assertFalse(ranked_try["decision"]["commercial"])
        self.assertEqual(ranked_try["review"]["technical"], "FAIL")
        self.assertNotEqual(ranked_try["review"]["semantic"], "pass")
        self.assertTrue(ranked_try["review"]["technicalPassIsNotSemanticPass"] or ranked_try["review"]["technical"] != "PASS")
        again = attempt_round(goods, {}, history=[{"method": "blocked", "code": "product_source_missing", "criteria": missing["criteria"]}])
        self.assertEqual(again["attempt"]["code"], "same_method")
        substituted = attempt_round(goods, {}, history=[{
            "method": "blocked",
            "code": "product_source_missing",
            "criteria": missing["criteria"],
        }])
        self.assertNotEqual(substituted["decision"]["provider"], "code")

    def test_still_contract_stores_a_fixture_without_calling_a_provider(self) -> None:
        from PIL import Image

        from kit_video.planning import production_gate, visual_plan
        from kit_video.still import candidate_row, execute_still, inspect_asset, submit_still

        blocked = execute_still({"role": "product", "contentKey": "goods"})
        self.assertEqual(blocked["status"], "not_integrated")
        self.assertFalse(blocked["success"])
        self.assertFalse(blocked["providerExecuted"])
        self.assertFalse(blocked["sent"])
        self.assertIsNone(blocked["candidate"])
        self.assertEqual(blocked["costUsd"], 0)
        with self.assertRaises(RuntimeError):
            submit_still({"provider": "library", "code": "REUSE"}, transport=object())

        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            source = folder / "fixture.png"
            dest = folder / "candidate" / "fixture.png"
            Image.new("RGB", (8, 8), (20, 40, 60)).save(source)
            plan = {"scenes": [{"sceneId": "keep"}]}
            registry = [{"assetId": "kept", "approval": "proposed"}]
            before_plan = json.dumps(plan)
            before_registry = json.dumps(registry)
            stored = execute_still({"role": "product"}, source=source, dest=dest)
            self.assertTrue(dest.is_file())
            self.assertEqual(stored["status"], "stored")
            self.assertTrue(stored["success"])
            self.assertEqual(stored["origin"], "fixture")
            self.assertFalse(stored["providerExecuted"])
            self.assertFalse(stored["commercial"])
            self.assertEqual(stored["sha256"], hashlib.sha256(dest.read_bytes()).hexdigest())
            self.assertEqual(json.dumps(plan), before_plan)
            self.assertEqual(json.dumps(registry), before_registry)
            original = dest.read_bytes()
            other = folder / "other.png"
            Image.new("RGB", (8, 8), (1, 2, 3)).save(other)
            refused = execute_still({"role": "product"}, source=other, dest=dest)
            self.assertEqual(dest.read_bytes(), original)
            self.assertFalse(refused["success"])
            row = candidate_row(stored, "fixture-1", "product")
            self.assertFalse(inspect_asset(row)["allowed"])
            scene = {
                "sceneId": "goods",
                "approvalStatus": "style_proof",
                "assets": ["fixture-1"],
                "params": {"intent": {"idea": "Hàng", "subject": "goods", "focal": "contrast"}},
            }
            gate = production_gate(visual_plan(scene, {"fixture-1": row}))
            self.assertEqual(gate["status"], "BLOCKED")
            self.assertEqual(gate["blockers"][0]["code"], "product_source_missing")
            failed = execute_still({"role": "product"}, adapter=lambda _decision: (_ for _ in ()).throw(RuntimeError("down")))
            self.assertEqual(failed["status"], "failed")
            self.assertFalse(failed["success"])
            self.assertFalse(failed["providerExecuted"])
            invalid = execute_still({"role": "product"}, adapter=lambda _decision: {"ok": True, "bytes": b"not-a-png"})
            self.assertEqual(invalid["status"], "invalid_output")
            self.assertFalse(invalid["success"])
            self.assertIsNone(invalid["candidate"])
            mocked = execute_still(
                {"role": "product"},
                adapter=lambda _decision: {"ok": True, "bytes": source.read_bytes()},
            )
            self.assertEqual(mocked["status"], "mock_only")
            self.assertEqual(mocked["origin"], "mock")
            self.assertFalse(mocked["success"])
            self.assertFalse(mocked["providerExecuted"])

    def test_relation_chart_uses_only_the_given_structure(self) -> None:
        from kit_video.canvas import FrameContext
        from kit_video.templates import draw_scene

        style = manifest("ignored", [])["visualStyle"]
        row = {
            "sceneId": "relation",
            "templateId": "designed_board",
            "approvalStatus": "style_proof",
            "onScreenText": ["Một nơi"],
            "durationSeconds": 4,
            "params": {
                "kind": "dashboard",
                "title": "Nhìn một nơi",
                "subtitle": "Người vẫn quyết định.",
                "disclosure": "Mockup — chưa phải giao diện chính thức",
                "dataLabel": "Dữ liệu minh họa",
                "metrics": [
                    {"label": "Bán", "value": "mẫu", "note": "minh họa"},
                    {"label": "Kho", "value": "mẫu", "note": "minh họa"},
                ],
                "chart": {
                    "title": "Cơ cấu mẫu",
                    "bars": [{"label": "Bán", "amount": 1}, {"label": "Kho", "amount": 1}],
                },
                "notes": [
                    {"label": "Bán", "body": "Việc được ghi lại"},
                    {"label": "Kho", "body": "Hàng ở cùng nơi"},
                ],
                "callout": "Từ việc rời đến một nơi",
                "intent": {"idea": "Nối", "subject": "relation", "emotion": "clarity", "focal": "connection"},
            },
        }
        refused = json.loads(json.dumps(row))
        refused["params"]["chart"]["bars"] = [{"label": "Bán", "amount": 0.7}, {"label": "Kho", "amount": 0.4}]
        refused_ctx = FrameContext(refused, style, 1280, 720, 3.4, 4.0, {})
        with self.assertRaises(ProductionError) as caught:
            draw_scene(refused_ctx, {}, Path("."), Path("."))
        self.assertEqual(caught.exception.errors[0]["code"], "false_comparison")
        ctx = FrameContext(row, style, 1280, 720, 3.4, 4.0, {})
        draw_scene(ctx, {}, Path("."), Path("."))
        self.assertIn("Cơ cấu mẫu", ctx.texts)
        self.assertIn("Việc được ghi lại", ctx.texts)
        self.assertIn("Hàng ở cùng nơi", ctx.texts)
        joined = " ".join(ctx.texts).lower()
        self.assertNotIn("doanh thu", joined)
        accent = tuple(style["palette"]["accent"])
        image = ctx.image.convert("RGB")
        longest = 0
        for y in range(500, 680, 3):
            run = 0
            for x in range(180, 1100):
                if image.getpixel((x, y)) == accent:
                    run += 1
                    longest = max(longest, run)
                else:
                    run = 0
        self.assertGreater(longest, 80)
        self.assertFalse(any(item.get("code") == "type_collision" for item in ctx.cache.get("spacingFindings") or []))


class ValidationContractTests(unittest.TestCase):
    def test_one_pass_reports_every_shot_and_a_style_preview_stays_narrow(self) -> None:
        import hashlib
        from PIL import Image

        from kit_video.planning import scene_findings, select_scene_asset
        from kit_video.still import inspect_asset

        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            image = folder / "plate.png"
            Image.new("RGB", (32, 18), (10, 20, 30)).save(image)
            digest = hashlib.sha256(image.read_bytes()).hexdigest()
            good = {
                "assetId": "plate",
                "kind": "image",
                "source": "user_supplied",
                "path": "plate.png",
                "pathBase": "project",
                "version": "1",
                "usageRights": "licensed for this film",
                "approval": "approved",
                "role": "setting",
                "label": "nền",
                "sha256": digest,
            }
            self.assertTrue(inspect_asset(good)["allowed"])
            still = scene("still", 1.0, "Nền")
            still["approvalStatus"] = "style_proof"
            still["assets"] = ["plate"]
            still["params"] = {"kind": "hero_still", "assetId": "plate", "intent": {"idea": "Nền", "subject": "still", "focal": "image"}}
            choice = select_scene_asset(still, {"plate": good}, folder, folder)
            self.assertEqual(choice["status"], "ready")
            self.assertTrue(choice["integrity"]["verified"])
            self.assertIn("semantic_unreviewed", {item["code"] for item in scene_findings(still, {"plate": good})})
            missing = dict(good, path="gone.png")
            self.assertEqual(select_scene_asset(still, {"plate": missing}, folder, folder)["code"], "asset_missing")
            bad = dict(good, sha256="0" * 64)
            self.assertEqual(select_scene_asset(still, {"plate": bad}, folder, folder)["code"], "asset_hash")
            bare = {key: value for key, value in good.items() if key != "usageRights"}
            self.assertIn("usageRights", inspect_asset(bare)["reasons"])
            self.assertFalse(inspect_asset(dict(good, approval="proposed"))["allowed"])
            from kit_video.still import confirm_file
            demo = dict(good, demoData=True, kind="screenshot", role="reference")
            self.assertTrue(confirm_file(demo, image)["match"])
            self.assertIn("demo", inspect_asset(demo)["reasons"])
            self.assertEqual(select_scene_asset(still, {"plate": demo}, folder, folder)["code"], "setting_missing")
            goods = scene("shelf", 1.0, "Hàng")
            goods["approvalStatus"] = "style_proof"
            goods["assets"] = ["plate"]
            goods["params"] = {"kind": "catalog", "intent": {"idea": "Hàng", "subject": "goods", "focal": "contrast"}}
            product = scene_findings(goods, {"plate": good})
            self.assertIn("product_source_missing", {item["code"] for item in product})
            self.assertTrue(all(item["remedy"] for item in product if item["code"] == "product_source_missing"))
            sale = scene("till", 1.0, "Bán")
            sale["approvalStatus"] = "style_proof"
            sale["assets"] = []
            sale["params"] = {
                "kind": "workspace",
                "intent": {"idea": "Bán", "subject": "transaction", "focal": "ticket"},
                "table": {"rows": [["Hàng mẫu A", "1", "mẫu"]]},
            }
            line = scene("line", 1.0, "Câu")
            line["approvalStatus"] = "style_proof"
            write_project(folder, "matrix", [goods, sale, line])
            project, load_errors = load_project(folder)
            self.assertEqual(load_errors, [])
            found = [(item["code"], item["path"]) for item in validate_project(project)[0]]
            self.assertIn(("product_source_missing", "shelf"), found)
            self.assertTrue(next(item["remedy"] for item in validate_project(project)[0] if item["code"] == "product_source_missing"))
            self.assertIn(("semantic_unreviewed", "shelf"), found)
            self.assertIn(("official_ui_missing", "till"), found)
            self.assertIn(("ui_unpriced", "till"), found)
            self.assertIn(("semantic_unreviewed", "till"), found)
            preview = render_project(folder, folder, scene_ids=["line"], allow_style_proof=True, preview_only=True, label="line")
            self.assertTrue(any((preview / "frames").glob("line-*.png")))
            with self.assertRaises(ProductionError) as blocked:
                render_project(folder, folder, scene_ids=["shelf"], allow_style_proof=True, preview_only=True, label="shelf")
            blocked_codes = {item["code"] for item in blocked.exception.errors}
            self.assertIn("product_source_missing", blocked_codes)
            self.assertNotIn("official_ui_missing", blocked_codes)
            with self.assertRaises(ProductionError):
                render_project(folder, folder, scene_ids=["line"], allow_style_proof=True, preview_only=False, label="cut")
            approved = scene("locked", 1.0, "Khóa")
            write_project(folder, "matrix", [approved, goods])
            with self.assertRaises(ProductionError) as commercial:
                render_project(folder, folder, scene_ids=["locked"], preview_only=True, label="locked")
            self.assertIn("product_source_missing", {item["code"] for item in commercial.exception.errors})

    def test_approved_demo_is_illustration_not_production_data(self) -> None:
        import hashlib
        from PIL import Image

        from kit_video.planning import scene_findings
        from kit_video.still import inspect_asset

        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            image = folder / "screen.png"
            Image.new("RGB", (32, 18), (10, 20, 30)).save(image)
            digest = hashlib.sha256(image.read_bytes()).hexdigest()
            row = {
                "assetId": "screen",
                "kind": "screenshot",
                "source": "user_supplied",
                "path": "screen.png",
                "pathBase": "project",
                "usageRights": "licensed for this film as illustration",
                "approval": "approved",
                "role": "reference",
                "demoData": True,
                "demoLabel": "Dữ liệu minh họa",
                "origin": "demo tenant, in memory",
                "label": "màn minh họa",
                "sha256": digest,
            }
            unapproved = inspect_asset(dict(row, approval="proposed"))
            self.assertFalse(unapproved["allowed"])
            self.assertIn("demo", unapproved["reasons"])
            unlabeled = inspect_asset(dict(row, demoLabel=""))
            self.assertFalse(unlabeled["allowed"])
            self.assertIn("demo_label", unlabeled["reasons"])
            unlicensed = inspect_asset(dict(row, usageRights="none"))
            self.assertFalse(unlicensed["allowed"])
            self.assertIn("usageRights", unlicensed["reasons"])
            decision = inspect_asset(row, image)
            self.assertTrue(decision["allowed"])
            self.assertEqual(decision["use"], "illustration")
            self.assertFalse(decision["productionData"])
            self.assertTrue(row["demoData"])
            self.assertNotEqual(decision["use"], "production")
            self.assertIn("asset_hash", inspect_asset(dict(row, sha256="0" * 64), image)["reasons"])
            self.assertIn("asset_missing", inspect_asset(row, folder / "gone.png")["reasons"])
            self.assertIn("role", inspect_asset(dict(row, role="chart"))["reasons"])
            self.assertIn("kind", inspect_asset(dict(row, kind="vector_generated"))["reasons"])
            ordinary = dict(row, demoData=False, role="setting", kind="image")
            plain = inspect_asset(ordinary, folder / "gone.png")
            self.assertTrue(plain["allowed"])
            self.assertTrue(plain["productionData"])
            self.assertEqual(plain["use"], "production")
            sale = scene("till", 1.0, "Bán")
            sale["approvalStatus"] = "style_proof"
            sale["assets"] = ["screen"]
            sale["params"] = {"kind": "workspace", "intent": {"idea": "Bán", "subject": "transaction", "focal": "ticket"}}
            self.assertIn("semantic_unreviewed", {item["code"] for item in scene_findings(sale, {"screen": row})})

    def test_visual_review_clears_only_its_own_candidate(self) -> None:
        import hashlib
        from PIL import Image

        from kit_video.planning import candidate_id, visual_plan

        def reviewed(scene, assets, status="PASS"):
            scene["visualReview"] = {
                "sceneId": scene["sceneId"],
                "status": status,
                "criteria": list(visual_plan(scene, assets).get("criteria") or []),
                "candidate": candidate_id(scene),
                "frame": f"{scene['sceneId']}-f0.50.png",
            }

        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            image = folder / "plate.png"
            Image.new("RGB", (32, 18), (10, 20, 30)).save(image)
            digest = hashlib.sha256(image.read_bytes()).hexdigest()
            plate = {
                "assetId": "plate",
                "kind": "image",
                "source": "user_supplied",
                "path": "plate.png",
                "pathBase": "project",
                "usageRights": "licensed for this film",
                "approval": "approved",
                "role": "setting",
                "label": "nền",
                "sha256": digest,
            }
            still = scene("still", 1.0, "Nền")
            still["approvalStatus"] = "style_proof"
            still["assets"] = ["plate"]
            still["params"] = {"kind": "hero_still", "assetId": "plate", "intent": {"idea": "Nền", "subject": "still", "focal": "image"}}
            other = scene("other", 1.0, "Khác")
            other["approvalStatus"] = "style_proof"
            other["assets"] = ["plate"]
            other["params"] = {"kind": "hero_still", "assetId": "plate", "intent": {"idea": "Khác", "subject": "still", "focal": "image"}}
            library = {"plate": plate}
            write_project(folder, "review", [still, other], assets=[plate])
            project, load_errors = load_project(folder)
            self.assertEqual(load_errors, [])
            self.assertIn(("semantic_unreviewed", "still"), [(item["code"], item["path"]) for item in validate_project(project)[0]])
            self.assertIn(("semantic_unreviewed", "other"), [(item["code"], item["path"]) for item in validate_project(project)[0]])
            reviewed(still, library, "PASS")
            write_project(folder, "review", [still, other], assets=[plate])
            project, _load_errors = load_project(folder)
            codes = [(item["code"], item["path"]) for item in validate_project(project)[0]]
            self.assertNotIn(("semantic_unreviewed", "still"), codes)
            self.assertIn(("semantic_unreviewed", "other"), codes)
            still["visualReview"] = dict(still["visualReview"], status="FAIL", reason="Cần sửa bố cục.")
            write_project(folder, "review", [still, other], assets=[plate])
            failed = [(item["code"], item["path"]) for item in validate_project(load_project(folder)[0])[0]]
            self.assertIn(("semantic_fail", "still"), failed)
            self.assertNotIn(("semantic_unreviewed", "still"), failed)
            still["visualReview"] = dict(still["visualReview"], status="REVISE")
            write_project(folder, "review", [still, other], assets=[plate])
            revised = [(item["code"], item["path"]) for item in validate_project(load_project(folder)[0])[0]]
            self.assertIn(("semantic_unreviewed", "still"), revised)
            reviewed(still, library, "PASS")
            still["params"] = dict(still["params"], title="Đã đổi")
            write_project(folder, "review", [still, other], assets=[plate])
            stale = [(item["code"], item["path"]) for item in validate_project(load_project(folder)[0])[0]]
            self.assertIn(("semantic_unreviewed", "still"), stale)
            still["params"] = {"kind": "hero_still", "assetId": "plate", "intent": {"idea": "Nền", "subject": "still", "focal": "image"}}
            reviewed(still, library, "PASS")
            borrowed = dict(still["visualReview"], sceneId="other")
            still["visualReview"] = borrowed
            write_project(folder, "review", [still, other], assets=[plate])
            foreign = [(item["code"], item["path"]) for item in validate_project(load_project(folder)[0])[0]]
            self.assertIn(("semantic_unreviewed", "still"), foreign)
            shelf = scene("shelf", 1.0, "Hàng")
            shelf["approvalStatus"] = "approved"
            shelf["params"] = {"kind": "catalog", "intent": {"idea": "Hàng", "subject": "goods", "focal": "contrast"}}
            reviewed(shelf, {}, "PASS")
            write_project(folder, "review", [shelf], assets=[])
            blocked = [(item["code"], item["path"]) for item in validate_project(load_project(folder)[0])[0]]
            self.assertIn(("product_source_missing", "shelf"), blocked)
            bare = scene("bare", 1.0, "Nền")
            bare["assets"] = ["plate"]
            bare["params"] = {"kind": "hero_still", "assetId": "plate", "intent": {"idea": "Nền", "subject": "still", "focal": "image"}}
            write_project(folder, "review", [bare], assets=[plate])
            status_only = [(item["code"], item["path"]) for item in validate_project(load_project(folder)[0])[0]]
            self.assertIn(("semantic_unreviewed", "bare"), status_only)
            with self.assertRaises(ProductionError):
                render_project(folder, folder, preview_only=False, label="cut")
            bad = dict(plate, sha256="0" * 64)
            reviewed(bare, {"plate": bad}, "PASS")
            write_project(folder, "review", [bare], assets=[bad])
            with self.assertRaises(ProductionError) as hashed:
                render_project(folder, folder, scene_ids=["bare"], preview_only=True, label="hash")
            self.assertIn("asset_hash", {item["code"] for item in hashed.exception.errors})
            line = scene("line", 1.0, "Câu")
            line["approvalStatus"] = "style_proof"
            write_project(folder, "review", [line, shelf], assets=[])
            preview = render_project(folder, folder, scene_ids=["line"], allow_style_proof=True, preview_only=True, label="line")
            self.assertTrue(any((preview / "frames").glob("line-*.png")))
            self.assertFalse((preview / "video.mp4").exists())


if __name__ == "__main__":
    unittest.main()

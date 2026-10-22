import hashlib
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from publish_research_v1 import atomic_write_text, main, publish


def sha256(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def sample(marker: str, health: str = "OK") -> dict[str, dict]:
    return {
        "lab": {
            "experiments": [{"experiment_id": "EXP-ONE", "marker": marker}],
        },
        "status": {
            "runner_status": "SUCCEEDED",
            "cycle_outcome": "NO_NEW_SIGNAL",
            "last_run_at": "2026-10-22T03:04:02.201078+00:00",
            "api_key_used": False,
            "api_key_present_ignored": True,
            "marker": marker,
        },
        "report": {
            "report_kind": "PAPER_EXTENSION",
            "frozen_lab_result": "research-lab-v1/output/LAB_RESULT.json",
            "strategies": [{"experiment_id": "EXP-ONE", "marker": marker}],
        },
        "health": {"health": health, "last_run_at": "2026-10-22T03:04:02.201078+00:00", "marker": marker},
    }


def write_sources(directory: Path, payloads: dict[str, dict]) -> dict[str, Path]:
    directory.mkdir(parents=True, exist_ok=True)
    paths = {}
    for kind, payload in payloads.items():
        path = directory / f"{kind}.json"
        path.write_bytes(json.dumps(payload, ensure_ascii=False, indent=2).encode("utf-8"))
        paths[kind] = path
    return paths


class PublishResearchV1Tests(unittest.TestCase):
    def test_publish_writes_one_generation_and_leaves_sources_unchanged(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            sources = write_sources(root / "src", sample("GEN-1"))
            before = {kind: path.read_bytes() for kind, path in sources.items()}
            destination = root / "publication"
            manifest = publish(destination, sources, "2026-10-22T04:00:00+00:00")
            after = {kind: path.read_bytes() for kind, path in sources.items()}
            self.assertEqual(before, after)
            self.assertEqual(manifest["schemaVersion"], "research-publication/v1")
            self.assertEqual(manifest["publisher"], "research-lab-v1")
            self.assertEqual(manifest["publishedAt"], "2026-10-22T04:00:00+00:00")
            lab = (destination / "lab" / "LAB_RESULT.json").read_bytes()
            status = json.loads((destination / "paper" / "PAPER_OPS_STATUS.json").read_text(encoding="utf-8"))
            report = json.loads((destination / "paper" / "PAPER_RESEARCH_REPORT.json").read_text(encoding="utf-8"))
            health = (destination / "paper" / "PAPER_OPS_HEALTH.json").read_bytes()
            self.assertEqual(lab, before["lab"])
            self.assertEqual(health, before["health"])
            self.assertNotIn("api_key_used", status)
            self.assertNotIn("api_key_present_ignored", status)
            self.assertNotIn("frozen_lab_result", report)
            self.assertEqual(manifest["labResultSha256"], sha256(lab))
            self.assertEqual(manifest["paperStatusSha256"], sha256((destination / "paper" / "PAPER_OPS_STATUS.json").read_bytes()))
            self.assertEqual(manifest["paperReportSha256"], sha256((destination / "paper" / "PAPER_RESEARCH_REPORT.json").read_bytes()))
            self.assertEqual(manifest["paperHealthSha256"], sha256(health))
            self.assertEqual(json.loads((destination / "manifest.json").read_text(encoding="utf-8")), manifest)

    def test_missing_source_fails_without_creating_a_publication(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            sources = write_sources(root / "src", sample("GEN-1"))
            sources["lab"].unlink()
            destination = root / "publication"
            with self.assertRaises(FileNotFoundError):
                publish(destination, sources, "2026-10-22T04:00:00+00:00")
            self.assertFalse(destination.exists())

    def test_invalid_json_fails_without_replacing_an_existing_publication(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            sources = write_sources(root / "src", sample("GEN-1"))
            destination = root / "publication"
            publish(destination, sources, "2026-10-22T04:00:00+00:00")
            sources["report"].write_text("{not-json", encoding="utf-8")
            with self.assertRaises(ValueError):
                publish(destination, sources, "2026-10-22T05:00:00+00:00")
            kept = json.loads((destination / "manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(kept["publishedAt"], "2026-10-22T04:00:00+00:00")
            self.assertIn("GEN-1", (destination / "lab" / "LAB_RESULT.json").read_text(encoding="utf-8"))

    def test_second_publish_replaces_the_whole_generation(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            destination = root / "publication"
            first = write_sources(root / "a", sample("GEN-1"))
            second = write_sources(root / "b", sample("GEN-2", health="STALE"))
            publish(destination, first, "2026-10-22T04:00:00+00:00")
            manifest = publish(destination, second, "2026-10-22T05:00:00+00:00")
            tree = "\n".join(path.read_text(encoding="utf-8") for path in destination.rglob("*") if path.is_file())
            self.assertNotIn("GEN-1", tree)
            self.assertIn("GEN-2", tree)
            self.assertEqual(manifest["publishedAt"], "2026-10-22T05:00:00+00:00")
            for field, relative in (
                ("labResultSha256", Path("lab") / "LAB_RESULT.json"),
                ("paperStatusSha256", Path("paper") / "PAPER_OPS_STATUS.json"),
                ("paperReportSha256", Path("paper") / "PAPER_RESEARCH_REPORT.json"),
                ("paperHealthSha256", Path("paper") / "PAPER_OPS_HEALTH.json"),
            ):
                self.assertEqual(manifest[field], sha256((destination / relative).read_bytes()))
            self.assertFalse((root / ".publication.staging").exists())
            self.assertFalse((root / ".publication.previous").exists())

    def test_changed_artifact_bytes_no_longer_match_the_recorded_hash(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            destination = root / "publication"
            publish(destination, write_sources(root / "src", sample("GEN-1")), "2026-10-22T04:00:00+00:00")
            lab = destination / "lab" / "LAB_RESULT.json"
            lab.write_bytes(lab.read_bytes() + b" ")
            manifest = json.loads((destination / "manifest.json").read_text(encoding="utf-8"))
            self.assertNotEqual(manifest["labResultSha256"], sha256(lab.read_bytes()))

    def test_staging_rename_failure_restores_the_previous_tree_and_does_not_report_success(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            destination = root / "publication"
            publish(destination, write_sources(root / "a", sample("GEN-1")), "2026-10-22T04:00:00+00:00")
            second = write_sources(root / "b", sample("GEN-2"))
            original = Path.replace

            def replace(self, target):
                if self.name == ".publication.staging":
                    raise OSError("staging rename failed")
                return original(self, target)

            with mock.patch.object(Path, "replace", replace):
                with self.assertRaises(OSError):
                    publish(destination, second, "2026-10-22T05:00:00+00:00")
            kept = json.loads((destination / "manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(kept["publishedAt"], "2026-10-22T04:00:00+00:00")
            self.assertIn("GEN-1", (destination / "lab" / "LAB_RESULT.json").read_text(encoding="utf-8"))
            self.assertFalse((root / ".publication.previous").exists())

            stdout = io.StringIO()
            stderr = io.StringIO()
            argv = [
                "--destination", str(destination),
                "--lab", str(second["lab"]),
                "--status", str(second["status"]),
                "--report", str(second["report"]),
                "--health", str(second["health"]),
                "--published-at", "2026-10-22T05:00:00+00:00",
            ]
            with mock.patch.object(Path, "replace", replace):
                with mock.patch("sys.stdout", stdout), mock.patch("sys.stderr", stderr):
                    with self.assertRaises(OSError):
                        main(argv)
            self.assertNotIn("PUBLISH_OK", stdout.getvalue())
            self.assertEqual(
                json.loads((destination / "manifest.json").read_text(encoding="utf-8"))["publishedAt"],
                "2026-10-22T04:00:00+00:00",
            )
            recovered = publish(destination, second, "2026-10-22T06:00:00+00:00")
            self.assertEqual(recovered["publishedAt"], "2026-10-22T06:00:00+00:00")
            self.assertIn("GEN-2", (destination / "lab" / "LAB_RESULT.json").read_text(encoding="utf-8"))
            self.assertNotIn("GEN-1", (destination / "lab" / "LAB_RESULT.json").read_text(encoding="utf-8"))
            self.assertFalse((root / ".publication.staging").exists())
            self.assertFalse((root / ".publication.previous").exists())

    def test_atomic_write_replaces_text_without_leaving_a_temporary_file(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "LAB_RESULT.json"
            atomic_write_text(path, "{\n  \"experiments\": []\n}")
            self.assertEqual(path.read_text(encoding="utf-8"), "{\n  \"experiments\": []\n}")
            self.assertFalse(Path(str(path) + ".tmp").exists())


if __name__ == "__main__":
    unittest.main()

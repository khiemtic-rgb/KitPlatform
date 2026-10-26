"""Export a research-publication/v1 directory from completed artifacts.

Copies finished files only. Does not read Paper Ops live state, journals, cache,
logs, or the frozen snapshot. Does not rewrite the source artifacts.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path

SCHEMA_VERSION = "research-publication/v1"
PUBLISHER = "research-lab-v1"

OBJECTS = (
    ("labResultSha256", Path("lab") / "LAB_RESULT.json", "lab"),
    ("paperStatusSha256", Path("paper") / "PAPER_OPS_STATUS.json", "status"),
    ("paperReportSha256", Path("paper") / "PAPER_RESEARCH_REPORT.json", "report"),
    ("paperHealthSha256", Path("paper") / "PAPER_OPS_HEALTH.json", "health"),
)


def atomic_write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(text, encoding="utf-8")
    temporary.replace(path)


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def _load_object(path: Path) -> tuple[bytes, dict]:
    if not path.is_file():
        raise FileNotFoundError(path.name)
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        raw = raw[3:]
    try:
        parsed = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError(path.name) from exc
    if not isinstance(parsed, dict):
        raise ValueError(path.name)
    return raw, parsed


def _publication_bytes(kind: str, raw: bytes, parsed: dict) -> bytes:
    if kind == "lab" or kind == "health":
        return raw
    cleaned = dict(parsed)
    if kind == "status":
        for key in list(cleaned):
            if "api_key" in key:
                del cleaned[key]
    if kind == "report":
        cleaned.pop("frozen_lab_result", None)
    text = json.dumps(cleaned, ensure_ascii=False, indent=2) + "\n"
    return text.encode("utf-8")


def publish(destination: Path, sources: dict[str, Path], published_at: str) -> dict:
    payloads: dict[str, bytes] = {}
    for _field, _relative, kind in OBJECTS:
        raw, parsed = _load_object(sources[kind])
        payloads[kind] = _publication_bytes(kind, raw, parsed)
    hashes = {kind: sha256_bytes(payload) for kind, payload in payloads.items()}
    publication_id = "pub-" + sha256_bytes("".join(hashes[kind] for _field, _relative, kind in OBJECTS).encode("utf-8"))[:16]
    manifest = {
        "schemaVersion": SCHEMA_VERSION,
        "publicationId": publication_id,
        "publishedAt": published_at,
        "publisher": PUBLISHER,
        "labResultSha256": hashes["lab"],
        "paperStatusSha256": hashes["status"],
        "paperReportSha256": hashes["report"],
        "paperHealthSha256": hashes["health"],
    }
    _swap_in(destination, payloads, manifest)
    return manifest


def _swap_in(destination: Path, payloads: dict[str, bytes], manifest: dict) -> None:
    """Readers see either the previous complete tree or the next one, never a mix.

    The two renames are not one syscall. A crash between them leaves the
    publication missing until the next successful publish restores it from
    the previous tree. This is not a single-syscall atomic directory swap.
    """
    destination = destination.resolve()
    parent = destination.parent
    parent.mkdir(parents=True, exist_ok=True)
    staging = parent / f".{destination.name}.staging"
    previous = parent / f".{destination.name}.previous"
    if staging.exists():
        shutil.rmtree(staging)
    for _field, relative, kind in OBJECTS:
        target = staging / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        temporary = target.with_suffix(target.suffix + ".tmp")
        temporary.write_bytes(payloads[kind])
        temporary.replace(target)
    manifest_path = staging / "manifest.json"
    atomic_write_text(manifest_path, json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    if previous.exists():
        shutil.rmtree(previous)
    if destination.exists():
        destination.replace(previous)
    try:
        staging.replace(destination)
    except Exception:
        if not destination.exists() and previous.exists():
            previous.replace(destination)
        raise
    if previous.exists():
        shutil.rmtree(previous)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Publish research-publication/v1 from completed artifacts.")
    parser.add_argument("--destination", required=True, type=Path)
    parser.add_argument("--lab", required=True, type=Path)
    parser.add_argument("--status", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--health", required=True, type=Path)
    parser.add_argument("--published-at", default=None)
    args = parser.parse_args(argv)
    published_at = args.published_at or datetime.now(timezone.utc).isoformat()
    try:
        manifest = publish(
            args.destination,
            {"lab": args.lab, "status": args.status, "report": args.report, "health": args.health},
            published_at,
        )
    except (FileNotFoundError, ValueError) as exc:
        print("PUBLISH_FAILED %s" % exc, file=sys.stderr)
        return 1
    print("PUBLISH_OK id=%s" % manifest["publicationId"])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Novixa stills through the one shared Model AI image row.

The paid call stays in ParkStill, which uses the application credential protector.
This module does not read or print the secret.
"""

from __future__ import annotations

import json
import subprocess
import tempfile
from pathlib import Path

import psycopg2

from kit_video.still import PARK_IMAGE_MODEL, execute_still, park_image

_CATALOG_USD = 0.045
_RUNNER = Path(__file__).resolve().parents[1] / "ParkStill" / "ParkStill.csproj"


def _settings(repo: Path) -> dict:
    path = repo / "src" / "KitPlatform.Api" / "appsettings.Development.json"
    return json.loads(path.read_text(encoding="utf-8"))


def _connection(settings: dict):
    raw = str(settings["ConnectionStrings"]["Default"])
    parts = {}
    for item in raw.split(";"):
        if "=" not in item:
            continue
        key, value = item.split("=", 1)
        parts[key.strip().lower()] = value.strip()
    return psycopg2.connect(
        host=parts["host"],
        port=int(parts.get("port") or 5432),
        dbname=parts["database"],
        user=parts["username"],
        password=parts["password"],
    )


def load_shared_image(repo: Path) -> dict:
    """The enabled IMAGE row. No secret is returned."""
    conn = _connection(_settings(repo))
    conn.set_session(readonly=True, autocommit=True)
    try:
        cur = conn.cursor()
        cur.execute(
            """
            SELECT t.tenant_code, c.provider_id, c.model_id, c.enabled, c.credential_id::text
            FROM pack_content.production_tenant_ai_config c
            JOIN tenants t ON t.id = c.tenant_id
            WHERE c.capability = 'IMAGE' AND c.enabled IS TRUE
            """
        )
        rows = cur.fetchall()
    finally:
        conn.close()
    matched = [
        row for row in rows
        if str(row[1]).upper() == "GEMINI" and str(row[2]) == PARK_IMAGE_MODEL and row[3] is True and row[4]
    ]
    if len(matched) != 1:
        raise RuntimeError("Cần đúng một dòng Hình ảnh Gemini 3.1 đang bật.")
    tenant, provider, model, enabled, credential = matched[0]
    return {
        "capability": "IMAGE",
        "providerId": provider,
        "modelId": model,
        "enabled": bool(enabled),
        "credentialId": credential,
        "tenantCode": tenant,
    }


class SharedImageTransport:
    """One draft call per invocation of the existing protector. At most two calls."""

    def __init__(self, repo: Path):
        self.repo = repo
        self.calls = 0

    def __call__(self, request: dict) -> dict:
        if request.get("route") == "SOFTWARE_SCREENSHOT":
            return {"ok": False, "sent": False, "error": "Ảnh chụp gốc không gửi Gemini."}
        if request.get("model") != PARK_IMAGE_MODEL:
            return {"ok": False, "sent": False, "error": "Sai model."}
        if self.calls >= 2:
            return {"ok": False, "sent": False, "error": "Đã đủ hai lần gọi."}
        self.calls += 1
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            prompt = folder / "prompt.txt"
            dest = folder / "still.bin"
            prompt.write_text(str(request.get("prompt") or ""), encoding="utf-8")
            command = [
                "dotnet", "run", "--project", str(_RUNNER), "-c", "Release", "--no-build", "--",
                "--repo", str(self.repo),
                "--out", str(dest),
                "--prompt-file", str(prompt),
            ]
            references = [str(item) for item in (request.get("references") or []) if str(item).strip()]
            if references:
                command.extend(["--reference", references[0]])
            done = subprocess.run(command, capture_output=True, check=False)
            if done.returncode != 0 or not dest.is_file():
                detail = done.stderr.decode("utf-8", errors="replace")[:300]
                return {"ok": False, "sent": True, "error": detail or "ParkStill không trả ảnh."}
            payload = dest.read_bytes()
        if not payload.startswith(b"\x89PNG\r\n\x1a\n"):
            from io import BytesIO

            from PIL import Image

            image = Image.open(BytesIO(payload))
            out = BytesIO()
            image.convert("RGB").save(out, format="PNG")
            payload = out.getvalue()
        return {"ok": True, "sent": True, "bytes": payload}


def open_transport(repo: Path) -> tuple[dict, SharedImageTransport]:
    park = load_shared_image(repo)
    if not park_image(park)["authorized"]:
        raise RuntimeError("Dòng Hình ảnh dùng chung chưa được cấp quyền.")
    return park, SharedImageTransport(repo)


def create_still(repo: Path, park: dict, transport: SharedImageTransport, need: dict, dest: Path) -> dict:
    record = execute_still(need, park=park, transport=transport, dest=dest)
    record["catalogUsd"] = _CATALOG_USD if record.get("sent") else 0
    return record

"""Giọng máy tạm (edge-tts) để canh nhịp khi chưa có file ElevenLabs. Không dùng để đăng."""

from __future__ import annotations

import asyncio
import json
from pathlib import Path

import edge_tts

HERE = Path(__file__).resolve().parent
OUT = HERE / "job" / "voice-parts-temp"
VOICES = {"ds": ("vi-VN-HoaiMyNeural", "+0%"), "kh": ("vi-VN-NamMinhNeural", "+10%")}


async def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    spec = json.loads((HERE / "lines.json").read_text(encoding="utf-8"))
    for line in spec["lines"]:
        voice, rate = VOICES[line["who"]]
        await edge_tts.Communicate(line.get("tts", line["text"]), voice, rate=rate).save(str(OUT / f"{line['id']}.mp3"))
        print(line["id"], "OK")


if __name__ == "__main__":
    asyncio.run(main())

"""Approved-voice lines for the NOVIXA intro. Does not touch the listening sample."""

from __future__ import annotations

import asyncio
import json
import sys
from pathlib import Path

import edge_tts

sys.stdout.reconfigure(encoding="utf-8")

ROOT = Path(__file__).resolve().parent / "voice"
VOICE = "vi-VN-NamMinhNeural"
RATE = "+0%"
PITCH = "+0Hz"
VOLUME = "+0%"

LINES = [
    "Doanh thu cao… chưa chắc nhà thuốc đã kinh doanh hiệu quả.",
    "Bởi đằng sau những con số doanh thu là hàng tồn, giá vốn và vô số khoản chi cần được kiểm soát.",
    "Khi dữ liệu nằm rời rạc, việc điều hành dễ trở nên khó khăn. Điều bạn cần là một bức tranh vận hành rõ ràng hơn.",
    "Đó là lý do Nôvixa kết hợp các công cụ vận hành nhà thuốc trên một nền tảng, từ bán hàng đến quản lý tồn kho và theo dõi dữ liệu hoạt động.",
    "Khi thông tin được tổ chức rõ ràng, chủ nhà thuốc có thêm cơ sở để theo dõi tình hình, nhận biết những điểm cần chú ý và đưa ra quyết định.",
    "Nôvixa — nền tảng vận hành nhà thuốc thông minh. Khám phá Nôvixa tại nôvixa.vn.",
]


async def one(index: int, text: str) -> dict:
    last = None
    for attempt in range(5):
        try:
            comm = edge_tts.Communicate(
                text, VOICE, boundary="WordBoundary", rate=RATE, pitch=PITCH, volume=VOLUME
            )
            words = []
            audio = bytearray()
            async for msg in comm.stream():
                if msg["type"] == "WordBoundary":
                    words.append(
                        {
                            "text": msg["text"],
                            "offset": msg["offset"] / 10_000_000,
                            "duration": msg["duration"] / 10_000_000,
                        }
                    )
                elif msg["type"] == "audio":
                    audio.extend(msg["data"])
            if not audio:
                raise edge_tts.exceptions.NoAudioReceived("empty")
            path = ROOT / f"s{index}.mp3"
            path.write_bytes(audio)
            return {"index": index, "text": text, "bytes": len(audio), "words": words}
        except edge_tts.exceptions.NoAudioReceived as exc:
            last = exc
            print(f"retry s{index} {attempt + 1}", flush=True)
            await asyncio.sleep(1.4 * (attempt + 1))
    raise last


async def main() -> None:
    ROOT.mkdir(parents=True, exist_ok=True)
    rows = []
    for index, text in enumerate(LINES, start=1):
        row = await one(index, text)
        brand = [word for word in row["words"] if "vixa" in word["text"].lower() or "NOVIXA" in word["text"]]
        print(index, row["bytes"], " | ".join(f"{w['text']}@{w['offset']:.3f}/{w['duration']:.3f}" for w in row["words"]), flush=True)
        print("  brand", brand, flush=True)
        rows.append(row)
    (ROOT / "boundaries.json").write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    asyncio.run(main())

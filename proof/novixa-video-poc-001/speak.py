"""Free local voice assemble for NOVIXA video POC 001.

Uses the installed edge-tts package (Edge Read Aloud). No API key and no billed account.
Does not call ElevenLabs, Creatomate, Pollinations, Gemini, Runway, or Fal.
"""

from __future__ import annotations

import asyncio
import json
import subprocess
import sys
import wave
from pathlib import Path

import edge_tts

ROOT = Path(__file__).resolve().parent
WORK = ROOT / "job"
VOICE = "vi-VN-NamMinhNeural"
LEAD = 0.36
TAIL = 0.80

LINES = [
    ("hook_a", "Nhà thuốc bán được ba trăm triệu mỗi tháng.", "-8%", "-1Hz", 0.32),
    ("hook_b", "Đơn ra đều. Tiền vào đều.", "-4%", "-1Hz", 0.24),
    ("hook_c", "Cuối tháng, vẫn không biết mình thực sự còn lại bao nhiêu.", "-12%", "-2Hz", 0.48),
    ("problem_a", "Doanh thu chỉ cho biết khách đã trả bao nhiêu.", "-2%", "+0Hz", 0.28),
    ("problem_b1", "Nó không cho biết hàng nào đang nằm trong kho,", "-2%", "+0Hz", 0.16),
    ("problem_b1b", "Hàng nằm đó là tiền đang bị giữ lại.", "-4%", "+0Hz", 0.20),
    ("problem_b2", "lô nào sắp hết hạn,", "-2%", "+0Hz", 0.14),
    ("problem_b2b", "Lô cận hạn là hàng có thể không bán được nữa.", "-4%", "+0Hz", 0.20),
    ("problem_b3", "hay ca bán có lệch quỹ hay không.", "-4%", "+0Hz", 0.16),
    ("problem_b3b", "Lệch quỹ là tiền trong ca không khớp với sổ.", "-4%", "+0Hz", 0.40),
    ("insight_a", "Muốn hết thấp thỏm, phải nhìn cùng một lúc:", "-6%", "+0Hz", 0.26),
    ("insight_b", "tiền bán ra, giá trị hàng đang giữ, và rủi ro ngay trên quầy.", "-6%", "+0Hz", 0.28),
    ("insight_c", "Không cần một con số lãi được hứa trước. Cần thấy các số này cạnh nhau.", "-6%", "+0Hz", 0.42),
    ("solution_a", "Nô vi xa để màn hình tổng quan hiện doanh thu hôm nay và hàng sắp hết hạn.", "+0%", "+0Hz", 0.28),
    ("solution_b", "Báo cáo tồn kho tính giá trị hàng đang giữ theo giá vốn từng lô.", "+0%", "+0Hz", 0.24),
    ("solution_c", "Lệch quỹ xem ở báo cáo ca làm việc.", "+0%", "+0Hz", 0.22),
    ("solution_d", "Đó là những màn hình đang có, không phải một lời hứa về lợi nhuận.", "+0%", "+0Hz", 0.40),
    ("cta_a", "Ba trăm triệu là doanh thu.", "-10%", "-1Hz", 0.40),
    ("cta_b", "Phần còn lại, hãy mở số liệu ra mà xem.", "-8%", "-1Hz", TAIL),
]


def run(args: list[str]) -> None:
    completed = subprocess.run(args, check=False, capture_output=True, text=True)
    if completed.returncode != 0:
        raise SystemExit((completed.stderr or completed.stdout or "ffmpeg failed")[-2000:])


def wav_duration(path: Path) -> float:
    with wave.open(str(path), "rb") as handle:
        return handle.getnframes() / float(handle.getframerate())


def silence(path: Path, seconds: float) -> None:
    run(
        [
            "ffmpeg",
            "-y",
            "-hide_banner",
            "-loglevel",
            "error",
            "-f",
            "lavfi",
            "-i",
            "anullsrc=r=48000:cl=stereo",
            "-t",
            f"{seconds:.3f}",
            "-c:a",
            "pcm_s16le",
            str(path),
        ]
    )


def trim_edges(src: Path, dest: Path) -> None:
    run(
        [
            "ffmpeg",
            "-y",
            "-hide_banner",
            "-loglevel",
            "error",
            "-i",
            str(src),
            "-af",
            "areverse,silenceremove=start_periods=1:start_threshold=-40dB:start_silence=0.06,areverse,silenceremove=start_periods=1:start_threshold=-40dB:start_silence=0.03",
            "-c:a",
            "pcm_s16le",
            str(dest),
        ]
    )


def to_wav(src: Path, dest: Path) -> None:
    run(
        [
            "ffmpeg",
            "-y",
            "-hide_banner",
            "-loglevel",
            "error",
            "-i",
            str(src),
            "-ar",
            "48000",
            "-ac",
            "2",
            "-c:a",
            "pcm_s16le",
            str(dest),
        ]
    )


async def synthesize() -> None:
    parts = WORK / "voice-parts"
    parts.mkdir(parents=True, exist_ok=True)
    timeline: list[dict[str, object]] = []
    cursor = LEAD
    pieces: list[Path] = [parts / "lead.wav"]
    silence(pieces[0], LEAD)

    for name, text, rate, pitch, pause in LINES:
        mp3 = parts / f"{name}.mp3"
        wav = parts / f"{name}.wav"
        gap = parts / f"{name}-gap.wav"
        kwargs = {"rate": rate}
        if pitch != "+0Hz":
            kwargs["pitch"] = pitch
        last_error: Exception | None = None
        for attempt in range(3):
            try:
                communicate = edge_tts.Communicate(text, VOICE, **kwargs)
                await communicate.save(str(mp3))
                last_error = None
                break
            except edge_tts.exceptions.NoAudioReceived as exc:
                last_error = exc
                await asyncio.sleep(1.5 * (attempt + 1))
        if last_error is not None:
            raise last_error
        to_wav(mp3, wav)
        trimmed = parts / f"{name}-trim.wav"
        trim_edges(wav, trimmed)
        wav = trimmed
        spoken = wav_duration(wav)
        timeline.append(
            {
                "id": name,
                "text": text,
                "start": round(cursor, 3),
                "end": round(cursor + spoken, 3),
                "rate": rate,
                "pitch": pitch,
            }
        )
        cursor += spoken
        pieces.append(wav)
        silence(gap, pause)
        pieces.append(gap)
        cursor += pause

    concat = parts / "concat.txt"
    lines = ["ffconcat version 1.0"]
    for piece in pieces:
        escaped = str(piece.resolve()).replace("\\", "/").replace("'", r"'\''")
        lines.append(f"file '{escaped}'")
    concat.write_text("\n".join(lines) + "\n", encoding="utf-8")

    voice_wav = WORK / "voice.wav"
    run(
        [
            "ffmpeg",
            "-y",
            "-hide_banner",
            "-loglevel",
            "error",
            "-f",
            "concat",
            "-safe",
            "0",
            "-i",
            str(concat),
            "-c:a",
            "pcm_s16le",
            str(voice_wav),
        ]
    )
    duration = wav_duration(voice_wav)
    fade_start = max(0.0, duration - 0.06)
    voice_mp3 = WORK / "voice.mp3"
    run(
        [
            "ffmpeg",
            "-y",
            "-hide_banner",
            "-loglevel",
            "error",
            "-i",
            str(voice_wav),
            "-af",
            f"afade=t=in:st=0:d=0.03,afade=t=out:st={fade_start:.3f}:d=0.05",
            "-c:a",
            "libmp3lame",
            "-b:a",
            "128k",
            str(voice_mp3),
        ]
    )
    (WORK / "timeline.json").write_text(
        json.dumps({"voice": VOICE, "lead": LEAD, "duration": duration, "lines": timeline}, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(f"duration={duration:.3f}")


async def ensure_lines() -> None:
    parts = WORK / "voice-parts"
    parts.mkdir(parents=True, exist_ok=True)
    for name, text, rate, pitch, _pause in LINES:
        wav = parts / f"{name}.wav"
        if wav.exists():
            continue
        mp3 = parts / f"{name}.mp3"
        kwargs: dict[str, str] = {"rate": rate}
        if pitch != "+0Hz":
            kwargs["pitch"] = pitch
        last_error: Exception | None = None
        for attempt in range(3):
            try:
                communicate = edge_tts.Communicate(text, VOICE, **kwargs)
                await communicate.save(str(mp3))
                last_error = None
                break
            except edge_tts.exceptions.NoAudioReceived as exc:
                last_error = exc
                await asyncio.sleep(1.5 * (attempt + 1))
        if last_error is not None:
            raise last_error
        to_wav(mp3, wav)
        print("synthesized", name)


def assemble_existing() -> None:
    asyncio.run(ensure_lines())
    parts = WORK / "voice-parts"
    cursor = LEAD
    pieces: list[Path] = [parts / "lead.wav"]
    silence(pieces[0], LEAD)
    timeline: list[dict[str, object]] = []
    for name, text, rate, pitch, pause in LINES:
        source = parts / f"{name}.wav"
        trimmed = parts / f"{name}-trim.wav"
        trim_edges(source, trimmed)
        spoken = wav_duration(trimmed)
        timeline.append(
            {
                "id": name,
                "text": text,
                "start": round(cursor, 3),
                "end": round(cursor + spoken, 3),
                "rate": rate,
                "pitch": pitch,
            }
        )
        cursor += spoken
        pieces.append(trimmed)
        gap = parts / f"{name}-gap.wav"
        silence(gap, pause)
        pieces.append(gap)
        cursor += pause

    concat = parts / "concat.txt"
    lines = ["ffconcat version 1.0"]
    for piece in pieces:
        escaped = str(piece.resolve()).replace("\\", "/").replace("'", r"'\''")
        lines.append(f"file '{escaped}'")
    concat.write_text("\n".join(lines) + "\n", encoding="utf-8")
    voice_wav = WORK / "voice.wav"
    run(
        [
            "ffmpeg",
            "-y",
            "-hide_banner",
            "-loglevel",
            "error",
            "-f",
            "concat",
            "-safe",
            "0",
            "-i",
            str(concat),
            "-c:a",
            "pcm_s16le",
            str(voice_wav),
        ]
    )
    duration = wav_duration(voice_wav)
    (WORK / "timeline.json").write_text(
        json.dumps({"voice": VOICE, "lead": LEAD, "duration": duration, "lines": timeline}, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(f"duration={duration:.3f}")


if __name__ == "__main__":
    WORK.mkdir(parents=True, exist_ok=True)
    if "--assemble" in sys.argv:
        assemble_existing()
    else:
        asyncio.run(synthesize())

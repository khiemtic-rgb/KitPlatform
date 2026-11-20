"""Voice for NOVIXA video POC 002. edge-tts only. No API key."""

from __future__ import annotations

import asyncio
import subprocess
import sys
import wave
from pathlib import Path

import edge_tts

ROOT = Path(__file__).resolve().parent
WORK = ROOT / "job"
VOICE = "vi-VN-NamMinhNeural"
LEAD = 0.40

# id, text, rate, pitch, pause after
LINES = [
    ("close_a", "Đóng cửa rồi.", "-10%", "-2Hz", 0.70),
    ("close_b", "Hôm nay bán cũng đều.", "-6%", "-1Hz", 0.90),
    ("gap_a", "Nhưng doanh thu không nói hộ hàng nào còn nằm trong kho,", "-2%", "+0Hz", 0.45),
    ("gap_b", "lô nào sắp hết hạn.", "-6%", "-1Hz", 0.55),
    ("see", "Muốn hết thấp thỏm, phải nhìn được những số đó.", "-8%", "-1Hz", 0.20),
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
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo",
            "-t", f"{seconds:.3f}", "-c:a", "pcm_s16le", str(path),
        ]
    )


def trim_edges(src: Path, dest: Path) -> None:
    run(
        [
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
            "-i", str(src),
            "-af",
            "areverse,silenceremove=start_periods=1:start_threshold=-40dB:start_silence=0.06,areverse,silenceremove=start_periods=1:start_threshold=-40dB:start_silence=0.03",
            "-c:a", "pcm_s16le", str(dest),
        ]
    )


def to_wav(src: Path, dest: Path) -> None:
    run(
        [
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
            "-i", str(src), "-ar", "48000", "-ac", "2", "-c:a", "pcm_s16le", str(dest),
        ]
    )


async def synth(text: str, rate: str, pitch: str, dest: Path) -> None:
    kwargs = {"rate": rate}
    if pitch != "+0Hz":
        kwargs["pitch"] = pitch
    last = None
    for _ in range(3):
        try:
            await edge_tts.Communicate(text, VOICE, **kwargs).save(str(dest))
            return
        except edge_tts.exceptions.NoAudioReceived as exc:
            last = exc
            await asyncio.sleep(0.8)
    raise SystemExit(f"no audio for {dest.name}: {last}")


async def main() -> None:
    parts = WORK / "voice-parts"
    parts.mkdir(parents=True, exist_ok=True)
    pieces: list[Path] = []
    lead = parts / "lead.wav"
    silence(lead, LEAD)
    pieces.append(lead)
    cursor = LEAD
    marks: list[tuple[str, float, float]] = []
    for line_id, text, rate, pitch, pause in LINES:
        raw = parts / f"{line_id}.mp3"
        wav = parts / f"{line_id}.wav"
        if not wav.exists():
            await synth(text, rate, pitch, raw)
            to_wav(raw, parts / f"{line_id}.full.wav")
            trim_edges(parts / f"{line_id}.full.wav", wav)
        duration = wav_duration(wav)
        marks.append((line_id, cursor, cursor + duration))
        pieces.append(wav)
        cursor += duration
        gap = parts / f"{line_id}.gap.wav"
        silence(gap, pause)
        pieces.append(gap)
        cursor += pause
    concat = parts / "concat.txt"
    concat.write_text("".join(f"file '{p.as_posix()}'\n" for p in pieces), encoding="utf-8")
    out = WORK / "voice.wav"
    run(
        [
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
            "-f", "concat", "-safe", "0", "-i", str(concat),
            "-c:a", "pcm_s16le", str(out),
        ]
    )
    print(f"duration {wav_duration(out):.3f}")
    for line_id, start, end in marks:
        print(f"{line_id} {start:.3f} {end:.3f}")


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    asyncio.run(main())

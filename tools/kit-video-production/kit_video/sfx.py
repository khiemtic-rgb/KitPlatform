"""Synthesize short placeholder effects. These are not finished sound design."""

from __future__ import annotations

from pathlib import Path

from kit_video.audio import require_ffmpeg, _run


def write_placeholders(dest: Path) -> list[Path]:
    require_ffmpeg()
    dest.mkdir(parents=True, exist_ok=True)
    jobs = {
        "whoosh.wav": [
            "-f", "lavfi", "-i", "anoisesrc=color=pink:duration=0.32:sample_rate=48000:amplitude=0.25",
            "-af", "highpass=f=700,afade=t=in:st=0:d=0.04,afade=t=out:st=0.1:d=0.2,volume=0.45",
        ],
        "thud.wav": [
            "-f", "lavfi", "-i", "aevalsrc=0.55*exp(-9*t)*sin(2*PI*85*t):s=48000:d=0.35",
            "-af", "lowpass=f=400,volume=0.7",
        ],
        "paper.wav": [
            "-f", "lavfi", "-i", "anoisesrc=color=white:duration=0.14:sample_rate=48000:amplitude=0.2",
            "-af", "bandpass=f=1800:width_type=h:w=700,afade=t=out:st=0.04:d=0.1,volume=0.4",
        ],
        "lock.wav": [
            "-f", "lavfi", "-i", "aevalsrc=0.4*exp(-40*t)*sin(2*PI*1300*t):s=48000:d=0.08",
            "-af", "volume=0.55",
        ],
    }
    written = []
    for name, args in jobs.items():
        path = dest / name
        _run(["ffmpeg", "-y", *args, "-c:a", "pcm_s16le", str(path)])
        note = dest / f"{name}.label.txt"
        note.write_text("PLACEHOLDER — hiệu ứng tổng hợp để thử pipeline, không phải âm thanh hoàn thiện.\n", encoding="utf-8")
        written.append(path)
    return written

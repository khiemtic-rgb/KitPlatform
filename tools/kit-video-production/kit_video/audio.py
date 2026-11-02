"""Mix voice and SFX to the scene length. Speech is never time-compressed."""

from __future__ import annotations

import shutil
import subprocess
import wave
from pathlib import Path

from kit_video.errors import ProductionError, issue


def require_ffmpeg() -> None:
    missing = [name for name in ("ffmpeg", "ffprobe") if shutil.which(name) is None]
    if missing:
        raise ProductionError("BLOCKED", [issue("missing_tool", ",".join(missing), f"Không thấy {', '.join(missing)} trên PATH.")])


def media_duration(path: Path) -> float:
    if path.suffix.lower() == ".wav":
        with wave.open(str(path), "rb") as handle:
            return handle.getnframes() / float(handle.getframerate())
    require_ffmpeg()
    completed = subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", str(path)],
        capture_output=True,
        text=True,
        check=False,
    )
    if completed.returncode != 0 or not completed.stdout.strip():
        raise ProductionError("BLOCKED", [issue("audio_unreadable", str(path), f"Không đo được thời lượng {path.name}.")])
    return float(completed.stdout.strip())


def scene_duration(scene: dict, voice_path: Path | None) -> float:
    """Return the scene length in seconds. A longer voice blocks; it is not sped up."""
    declared = float(scene["durationSeconds"])
    if scene.get("durationSource") != "voice_track":
        if voice_path is not None:
            lead = float(scene.get("voiceLeadSeconds") or 0)
            if media_duration(voice_path) + lead > declared + 0.05:
                raise ProductionError("BLOCKED", [issue(
                    "voice_exceeds_scene",
                    scene["sceneId"],
                    "Lời đọc dài hơn cảnh. Hãy nới cảnh hoặc lời. Không ép tốc độ giọng.",
                )])
        return declared
    if voice_path is None:
        raise ProductionError("BLOCKED", [issue("voice_missing", scene["sceneId"], "durationSource=voice_track nhưng chưa có file lời.")])
    lead = float(scene.get("voiceLeadSeconds") or 0)
    tail = float(scene.get("voiceTailSeconds") or 0)
    return lead + media_duration(voice_path) + tail


def snap_frames(duration: float, frame_rate: float) -> tuple[int, float]:
    frames = max(1, int(round(duration * frame_rate)))
    return frames, frames / frame_rate


def write_silence(dest: Path, duration: float) -> None:
    require_ffmpeg()
    _run(["ffmpeg", "-y", "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo", "-t", f"{duration:.6f}", "-c:a", "pcm_s16le", str(dest)])


def mix_scene(
    dest: Path,
    duration: float,
    voice_path: Path | None,
    voice_lead: float,
    cues: list[tuple[Path, float, float]],
    voice_gain: float,
    sfx_gain: float,
    duck: bool,
) -> None:
    require_ffmpeg()
    if voice_path is None and not cues:
        write_silence(dest, duration)
        return
    command = ["ffmpeg", "-y"]
    filters = []
    labels = []
    index = 0
    if voice_path is not None:
        command.extend(["-i", str(voice_path)])
        delay = max(0, int(round(voice_lead * 1000)))
        split = ",asplit=2[voice][voice_sc]" if cues and duck else "[voice]"
        filters.append(
            f"[{index}:a]aresample=48000,aformat=channel_layouts=stereo,volume={voice_gain:.4f},adelay={delay}:all=1,apad=whole_dur={duration:.6f},atrim=0:{duration:.6f}{split}"
        )
        index += 1
    for cue_index, (path, at_seconds, gain) in enumerate(cues):
        command.extend(["-i", str(path)])
        delay = max(0, int(round(at_seconds * 1000)))
        label = f"s{cue_index}"
        filters.append(
            f"[{index}:a]aresample=48000,aformat=channel_layouts=stereo,volume={sfx_gain * gain:.4f},adelay={delay}|{delay}[{label}]"
        )
        labels.append(f"[{label}]")
        index += 1
    if labels:
        filters.append("".join(labels) + f"amix=inputs={len(labels)}:normalize=0:duration=longest:dropout_transition=0[sfx]")
        if voice_path is not None and duck:
            filters.append("[sfx][voice_sc]sidechaincompress=threshold=0.03:ratio=6:attack=20:release=280[ducked]")
            filters.append(f"[voice][ducked]amix=inputs=2:normalize=0:duration=longest:dropout_transition=0,alimiter=limit=0.89,atrim=0:{duration:.6f},apad=whole_dur={duration:.6f}[a]")
        elif voice_path is not None:
            filters.append(f"[voice][sfx]amix=inputs=2:normalize=0:duration=longest:dropout_transition=0,alimiter=limit=0.89,atrim=0:{duration:.6f},apad=whole_dur={duration:.6f}[a]")
        else:
            filters.append(f"[sfx]alimiter=limit=0.89,atrim=0:{duration:.6f},apad=whole_dur={duration:.6f}[a]")
    else:
        filters.append(f"[voice]alimiter=limit=0.89,atrim=0:{duration:.6f},apad=whole_dur={duration:.6f}[a]")
    command.extend(["-filter_complex", ";".join(filters), "-map", "[a]", "-c:a", "pcm_s16le", str(dest)])
    _run(command)


def concat_audio(parts: list[Path], dest: Path) -> None:
    require_ffmpeg()
    command = ["ffmpeg", "-y"]
    for part in parts:
        command.extend(["-i", str(part)])
    graph = "".join(f"[{index}:a]" for index in range(len(parts))) + f"concat=n={len(parts)}:v=0:a=1[a]"
    command.extend(["-filter_complex", graph, "-map", "[a]", "-c:a", "pcm_s16le", str(dest)])
    _run(command)


def _run(command: list[str]) -> None:
    completed = subprocess.run(command, capture_output=True, text=True, check=False)
    if completed.returncode != 0:
        detail = (completed.stderr or completed.stdout or "ffmpeg failed")[-1500:]
        raise ProductionError("FAIL", [issue("ffmpeg", command[0], detail)])

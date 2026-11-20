"""Composition for a designed scene.

The shot plan says what the scene means. This module chooses a picture family,
a ground, and a motion window from that intent and from the art-direction
profile. It does not name a brand or a film, and it does not paint.
"""

from __future__ import annotations

from kit_video.canvas import ease_out, progress


SUBJECTS = frozenset({
    "still",
    "portrait",
    "schedule",
    "goods",
    "record",
    "transaction",
    "stock",
    "relation",
    "mark",
    "statement",
    "lockup",
})

_FAMILY = {
    "still": "photographic",
    "portrait": "portrait-object",
    "schedule": "spine",
    "goods": "mass-contrast",
    "record": "foreground-row",
    "transaction": "ticket",
    "stock": "quantity",
    "relation": "hub",
    "statement": "sequence",
    "lockup": "lockup",
}

_WINDOWS = {
    "pressure": (0.03, 0.2),
    "calm": (0.08, 0.46),
    "clarity": (0.06, 0.3),
    "resolve": (0.08, 0.34),
    "warmth": (0.1, 0.42),
}


def art_profile(style: dict) -> dict:
    raw = style.get("artDirection") if isinstance(style.get("artDirection"), dict) else {}
    principles = raw.get("principles")
    return {
        "audience": str(raw.get("audience") or ""),
        "objective": str(raw.get("objective") or ""),
        "language": str(raw.get("language") or ""),
        "imagery": str(raw.get("imagery") or ""),
        "motion": str(raw.get("motion") or ""),
        "light": str(raw.get("light") or "warm-paper"),
        "material": str(raw.get("material") or "paper"),
        "depth": str(raw.get("depth") or "flat"),
        "principles": [str(item) for item in principles if isinstance(item, str)] if isinstance(principles, list) else [],
    }


def scene_intent(params: dict) -> dict:
    raw = params.get("intent") if isinstance(params.get("intent"), dict) else {}
    return {
        "idea": str(raw.get("idea") or "").strip(),
        "subject": str(raw.get("subject") or "").strip(),
        "emotion": str(raw.get("emotion") or "").strip(),
        "focal": str(raw.get("focal") or "").strip(),
    }


def family_for(params: dict, kind: str) -> str:
    intent = scene_intent(params)
    subject = intent["subject"]
    if subject == "mark" and intent["focal"] == "line":
        return "anchored-line"
    if subject == "mark":
        return "mark-field"
    if subject in _FAMILY:
        return _FAMILY[subject]
    return kind or "unspecified"


def ground_for(family: str, profile: dict) -> str:
    if family == "sequence":
        return "ink-field"
    if family == "photographic":
        return "photographic"
    return str(profile.get("light") or "warm-paper")


def motion_window(emotion: str) -> tuple[float, float]:
    return _WINDOWS.get(emotion, (0.06, 0.28))


def motion_amount(now: float, duration: float, emotion: str) -> float:
    start, end = motion_window(emotion)
    if duration <= 0:
        return 1.0
    return ease_out(progress(now, duration * start, duration * end))


def bound_assets(params: dict) -> list[str]:
    found = []
    for key in ("assetId", "logoAssetId"):
        value = params.get(key)
        if isinstance(value, str) and value and value not in found:
            found.append(value)
    return found


def compose(style: dict, params: dict, kind: str, now: float, duration: float) -> dict:
    profile = art_profile(style)
    intent = scene_intent(params)
    family = family_for(params, kind)
    start, end = motion_window(intent["emotion"])
    return {
        "profile": profile,
        "intent": intent,
        "family": family,
        "ground": ground_for(family, profile),
        "motion": {
            "emotion": intent["emotion"],
            "start": start,
            "end": end,
            "amount": motion_amount(now, duration, intent["emotion"]),
        },
        "boundAssets": bound_assets(params),
    }

"""Choose internal motion or a paid clip. Prices and the cap come from the caller."""

from __future__ import annotations

INTERNAL_KINDS = {"camera", "graphic", "logo", "type", "data", "metaphor", "cta"}


def select_motion(
    need: dict,
    *,
    credential: bool,
    spent_usd: float,
    cap_usd: float,
    prices_usd_per_second: dict,
) -> dict:
    kind = str(need.get("kind") or "")
    if kind in INTERNAL_KINDS:
        return {"provider": "internal", "costUsd": 0.0, "code": "INTERNAL"}
    if kind != "character_action":
        return {"provider": "blocked", "costUsd": 0.0, "code": "UNKNOWN_KIND"}
    model = str(need.get("model") or "")
    rate = prices_usd_per_second.get(model)
    seconds = float(need.get("seconds") or 0)
    if rate is None or seconds <= 0:
        return {"provider": "blocked", "costUsd": 0.0, "code": "PRICE_UNKNOWN"}
    cost = round(seconds * float(rate), 4)
    if not credential:
        return {"provider": "blocked", "costUsd": cost, "code": "CREDENTIAL_MISSING", "model": model}
    if spent_usd + cost > cap_usd + 1e-9:
        return {"provider": "blocked", "costUsd": cost, "code": "BUDGET", "model": model}
    return {"provider": "runway", "costUsd": cost, "code": "RUNWAY", "model": model}

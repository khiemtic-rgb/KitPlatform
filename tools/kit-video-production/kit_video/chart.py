"""Candles, volume, and indicators drawn from structured OHLCV.

A still image of a chart is not a source. The painter reads rows, checks them,
and computes overlays from those rows. It does not invent a missing print.
"""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw

from kit_video.canvas import FrameContext, ease_out, progress
from kit_video.errors import ProductionError, issue


def load_candles(path: Path) -> dict:
    payload = json.loads(path.read_text(encoding="utf-8"))
    rows = payload.get("candles")
    if not isinstance(rows, list) or not rows:
        raise ProductionError("FAIL", [issue("ohlcv", str(path), "File nến không có mảng candles.")])
    candles = []
    for index, row in enumerate(rows):
        if not isinstance(row, dict):
            raise ProductionError("FAIL", [issue("ohlcv", str(path), f"Nến {index} không phải object.")])
        try:
            candles.append({
                "t": int(row["t"]),
                "o": float(row["o"]),
                "h": float(row["h"]),
                "l": float(row["l"]),
                "c": float(row["c"]),
                "v": float(row["v"]),
            })
        except (KeyError, TypeError, ValueError) as exc:
            raise ProductionError("FAIL", [issue("ohlcv", str(path), f"Nến {index} thiếu trường số.")]) from exc
    return {
        "symbol": str(payload.get("symbol") or ""),
        "timeframe": str(payload.get("timeframe") or ""),
        "source": str(payload.get("source") or ""),
        "mode": str(payload.get("mode") or ""),
        "candles": candles,
    }


def ohlcv_errors(candles: list[dict]) -> list[str]:
    """Structural checks. A pretty candle that breaks these is refused."""
    found = []
    previous = None
    for index, row in enumerate(candles):
        open_, high, low, close, volume = row["o"], row["h"], row["l"], row["c"], row["v"]
        if high < max(open_, close) or low > min(open_, close) or high < low:
            found.append(f"{index}: high/low không bao open/close")
        if volume < 0:
            found.append(f"{index}: volume âm")
        if previous is not None and row["t"] <= previous:
            found.append(f"{index}: timestamp không tăng")
        previous = row["t"]
    return found


def sma(values: list[float], length: int) -> list[float | None]:
    out: list[float | None] = [None] * len(values)
    if length <= 0:
        return out
    total = 0.0
    for index, value in enumerate(values):
        total += value
        if index >= length:
            total -= values[index - length]
        if index >= length - 1:
            out[index] = total / length
    return out


def ema(values: list[float], length: int) -> list[float | None]:
    out: list[float | None] = [None] * len(values)
    if length <= 0 or len(values) < length:
        return out
    seed = sum(values[:length]) / length
    out[length - 1] = seed
    weight = 2 / (length + 1)
    previous = seed
    for index in range(length, len(values)):
        previous = values[index] * weight + previous * (1 - weight)
        out[index] = previous
    return out


def rsi(values: list[float], length: int = 14) -> list[float | None]:
    out: list[float | None] = [None] * len(values)
    if length <= 0 or len(values) <= length:
        return out
    gain = 0.0
    loss = 0.0
    for index in range(1, length + 1):
        delta = values[index] - values[index - 1]
        gain += max(delta, 0.0)
        loss += max(-delta, 0.0)
    avg_gain = gain / length
    avg_loss = loss / length

    def reading(up: float, down: float) -> float:
        if down == 0:
            return 100.0
        return 100 - (100 / (1 + up / down))

    out[length] = reading(avg_gain, avg_loss)
    for index in range(length + 1, len(values)):
        delta = values[index] - values[index - 1]
        avg_gain = (avg_gain * (length - 1) + max(delta, 0.0)) / length
        avg_loss = (avg_loss * (length - 1) + max(-delta, 0.0)) / length
        out[index] = reading(avg_gain, avg_loss)
    return out


def macd(values: list[float], fast: int = 12, slow: int = 26, signal: int = 9) -> tuple[list, list]:
    fast_line = ema(values, fast)
    slow_line = ema(values, slow)
    macd_line: list[float | None] = [None] * len(values)
    seeded = []
    for index, (left, right) in enumerate(zip(fast_line, slow_line)):
        if left is None or right is None:
            continue
        macd_line[index] = left - right
        seeded.append(left - right)
    signal_raw = ema(seeded, signal) if seeded else []
    signal_line: list[float | None] = [None] * len(values)
    cursor = 0
    for index, value in enumerate(macd_line):
        if value is None:
            continue
        if cursor < len(signal_raw):
            signal_line[index] = signal_raw[cursor]
        cursor += 1
    return macd_line, signal_line


def find_regimes(candles: list[dict], length: int = 48) -> dict:
    """Pick three non-overlapping windows from the same series. Empty if the tape is too short."""
    if len(candles) < length + 5:
        return {}
    scored = []
    for start in range(0, len(candles) - length):
        window = candles[start:start + length]
        first = window[0]["c"]
        last = window[-1]["c"]
        if first <= 0:
            continue
        change = last / first - 1
        high = max(row["h"] for row in window)
        low = min(row["l"] for row in window)
        span = (high - low) / first
        scored.append((start, change, span))
    used = []

    def take(predicate, rank):
        choices = [row for row in scored if predicate(row) and _free(row[0], length, used)]
        if not choices:
            return None
        choices.sort(key=rank)
        start = choices[0][0]
        used.append(start)
        return {"start": start, "count": length, "change": choices[0][1], "span": choices[0][2]}

    up = take(lambda row: row[1] >= 0.03, lambda row: -row[1])
    down = take(lambda row: row[1] <= -0.03, lambda row: row[1])
    flat = take(lambda row: abs(row[1]) <= 0.012 and row[2] >= 0.02, lambda row: abs(row[1]))
    if flat is None:
        flat = take(lambda row: abs(row[1]) <= 0.02 and row[2] >= 0.015, lambda row: abs(row[1]))
    return {"up": up, "down": down, "range": flat}


def _free(start: int, length: int, used: list[int]) -> bool:
    end = start + length
    for other in used:
        if not (end <= other or start >= other + length):
            return False
    return True


def _price(value: float) -> str:
    return f"{value:,.0f}".replace(",", ".")


def structure_notes(candles: list[dict]) -> dict:
    """Facts computed from the window. A label is omitted when the tape does not show that shape."""
    if len(candles) < 8:
        return {}
    closes = [row["c"] for row in candles]
    highs = [row["h"] for row in candles]
    first = closes[0]
    last = closes[-1]
    change = (last / first - 1) if first else 0.0
    peak_at = max(range(len(highs)), key=highs.__getitem__)
    trough_at = min(range(peak_at, len(candles)), key=lambda index: candles[index]["l"]) if peak_at < len(candles) - 1 else peak_at
    drop = 0.0
    if highs[peak_at] > 0 and trough_at > peak_at:
        drop = (highs[peak_at] - candles[trough_at]["l"]) / highs[peak_at]
    pullback = None
    for index in range(4, len(candles) - 4):
        left = max(highs[max(0, index - 8):index])
        right = min(row["l"] for row in candles[index:index + 4])
        if left <= 0:
            continue
        dip = (left - right) / left
        later = max(row["c"] for row in candles[index + 4:])
        if dip >= 0.015 and later > right and (pullback is None or dip > pullback["dip"]):
            pullback = {"at": index, "dip": round(dip, 4)}
    prior = 12 if len(candles) > 16 else 6
    breakout = None
    for index in range(prior, len(candles)):
        ceiling = max(highs[index - prior:index])
        if candles[index]["c"] > ceiling:
            breakout = {"at": index, "prior": prior}
    volumes = sorted(row["v"] for row in candles)
    median = volumes[len(volumes) // 2] or 1
    surge = None
    for index, row in enumerate(candles):
        if row["v"] >= median * 1.8 and (surge is None or row["v"] > candles[surge]["v"]):
            surge = index
    return {
        "change": round(change, 4),
        "peakAt": peak_at,
        "troughAt": trough_at,
        "priceDrop": round(drop, 4),
        "pullback": pullback,
        "breakout": breakout,
        "volumeSurgeAt": surge,
    }


def paint_market(ctx: FrameContext, series: dict) -> None:
    candles = series["candles"]
    errors = ohlcv_errors(candles)
    if errors:
        raise ProductionError("FAIL", [issue("ohlcv", ctx.scene["sceneId"], errors[0])])
    asked = str(ctx.params.get("symbol") or "")
    if asked and asked != str(series.get("symbol") or ""):
        raise ProductionError("FAIL", [issue("ohlcv", ctx.scene["sceneId"], "Symbol trên cảnh không khớp file nến.")])
    asked_mode = str(ctx.params.get("mode") or "")
    if asked_mode and asked_mode != str(series.get("mode") or ""):
        raise ProductionError("FAIL", [issue("ohlcv", ctx.scene["sceneId"], "Chế độ dữ liệu trên cảnh không khớp file nến.")])
    start = int(ctx.params.get("start", 0))
    count = int(ctx.params.get("count", 48))
    pan = int(ctx.params.get("panBars", 0))
    warmup = max(0, int(ctx.params.get("warmup", 0)))
    origin = start - warmup
    end = start + count + max(0, pan)
    if origin < 0 or end > len(candles) or count < 8:
        raise ProductionError("FAIL", [issue("ohlcv", ctx.scene["sceneId"], "Cửa sổ nến nằm ngoài dữ liệu.")])
    window = candles[origin:end]
    names = [str(item) for item in (ctx.params.get("indicators") or [])]
    closes = [row["c"] for row in window]
    overlays = {}
    if "sma20" in names:
        overlays["sma20"] = sma(closes, 20)
    if "rsi14" in names:
        overlays["rsi14"] = rsi(closes, 14)
    if "macd" in names:
        overlays["macd"] = macd(closes)
    slide = ease_out(progress(ctx.t, ctx.duration * 0.12, ctx.duration * 0.78))
    offset = warmup + int(round(pan * slide))
    visible = window[offset:offset + count]
    if len(visible) < count:
        visible = window[-count:]
        offset = len(window) - count
    _draw(ctx, visible, overlays, offset, names)
    _title(ctx, series)


def _draw(ctx: FrameContext, visible: list[dict], overlays: dict, offset: int, names: list[str]) -> None:
    left, top, right, bottom = ctx.safe()
    plot_left = left + 8
    plot_right = right - int(ctx.width * 0.08)
    title_h = int(ctx.height * 0.12)
    panel = "rsi14" if "rsi14" in overlays else ("macd" if "macd" in overlays else "")
    panel_h = int(ctx.height * 0.16) if panel else 0
    volume_h = int(ctx.height * 0.16)
    price_top = top + title_h
    price_bottom = bottom - volume_h - panel_h - 28
    volume_top = price_bottom + 8
    volume_bottom = volume_top + volume_h - 8
    highs = [row["h"] for row in visible]
    lows = [row["l"] for row in visible]
    pad = (max(highs) - min(lows)) * 0.08 or 1
    high = max(highs) + pad
    low = min(lows) - pad
    draw = ImageDraw.Draw(ctx.image)
    draw.rounded_rectangle((plot_left, price_top, plot_right, price_bottom), radius=12, fill=(*ctx.color("paper"), 255))
    for step in range(1, 4):
        y = price_top + (price_bottom - price_top) * step / 4
        draw.line((plot_left + 8, y, plot_right - 8, y), fill=(*ctx.color("muted"), 70), width=1)
        price = high - (high - low) * step / 4
        ctx.text(_price(price), (plot_right + 8, y), max(12, int(ctx.height * 0.018)), ctx.color("muted"), anchor="lm")
    slot = (plot_right - plot_left - 16) / len(visible)
    body = max(3, int(slot * 0.58))

    def y_of(price: float) -> float:
        return price_bottom - (price - low) / (high - low) * (price_bottom - price_top - 12) - 6

    focus = ctx.params.get("focus") if isinstance(ctx.params.get("focus"), dict) else None
    if isinstance(focus, dict):
        appear = ease_out(progress(ctx.t, ctx.duration * 0.28, ctx.duration * 0.48))
        if appear > 0.02:
            a = int(focus.get("start", 0)) - offset
            b = int(focus.get("end", a)) - offset
            a = max(0, min(len(visible) - 1, a))
            b = max(a, min(len(visible) - 1, b))
            x0 = plot_left + 8 + a * slot
            x1 = plot_left + 8 + (b + 1) * slot
            shade = Image.new("RGBA", (max(1, int(x1 - x0)), max(1, price_bottom - price_top)), (*ctx.color("accent"), int(70 * appear)))
            ctx.image.alpha_composite(shade, (int(x0), price_top))
            label = str(focus.get("label") or "")
            if label:
                ctx.text(label, (x0 + 8, price_top + 10), int(ctx.height * 0.022), ctx.color("ink"), bold=True, opacity=appear)
    up = ctx.color("accent")
    down = ctx.color("accentAlt") if "accentAlt" in ctx.palette else (176, 64, 58)
    for index, row in enumerate(visible):
        x = plot_left + 8 + index * slot + slot / 2
        color = up if row["c"] >= row["o"] else down
        draw.line((x, y_of(row["h"]), x, y_of(row["l"])), fill=(*color, 255), width=2)
        y0 = y_of(max(row["o"], row["c"]))
        y1 = y_of(min(row["o"], row["c"]))
        if y1 - y0 < 1:
            y1 = y0 + 1
        draw.rectangle((x - body / 2, y0, x + body / 2, y1), fill=(*color, 255))
    if "sma20" in overlays:
        points = []
        line = overlays["sma20"]
        for index in range(len(visible)):
            value = line[offset + index] if offset + index < len(line) else None
            if value is None:
                continue
            x = plot_left + 8 + index * slot + slot / 2
            points.append((x, y_of(value)))
        if len(points) >= 2:
            draw.line(points, fill=(40, 90, 176, 255), width=3)
            ctx.text("SMA 20", (points[-1][0] - 70, points[-1][1] - 16), int(ctx.height * 0.018), (40, 90, 176), bold=True)
    max_volume = max(row["v"] for row in visible) or 1
    draw.rounded_rectangle((plot_left, volume_top, plot_right, volume_bottom), radius=8, fill=(*ctx.color("paper"), 255))
    ctx.text("Khối lượng", (plot_left + 10, volume_top + 4), int(ctx.height * 0.016), ctx.color("muted"))
    for index, row in enumerate(visible):
        x = plot_left + 8 + index * slot + slot / 2
        bar_h = (row["v"] / max_volume) * (volume_bottom - volume_top - 22)
        color = up if row["c"] >= row["o"] else down
        draw.rectangle((x - body / 2, volume_bottom - 6 - bar_h, x + body / 2, volume_bottom - 6), fill=(*color, 168))
    if panel:
        panel_top = volume_bottom + 8
        panel_bottom = bottom - 4
        draw.rounded_rectangle((plot_left, panel_top, plot_right, panel_bottom), radius=8, fill=(*ctx.color("paper"), 255))
        if panel == "rsi14":
            draw.line((plot_left + 8, panel_top + (panel_bottom - panel_top) * 0.3, plot_right - 8, panel_top + (panel_bottom - panel_top) * 0.3), fill=(*down, 120), width=1)
            draw.line((plot_left + 8, panel_top + (panel_bottom - panel_top) * 0.7, plot_right - 8, panel_top + (panel_bottom - panel_top) * 0.7), fill=(*up, 120), width=1)
            series = overlays["rsi14"]
            lo, hi = 0.0, 100.0
            label = "RSI 14"
        else:
            macd_line, signal_line = overlays["macd"]
            series = macd_line
            values = [value for value in list(macd_line) + list(signal_line) if value is not None]
            lo = min(values) if values else -1.0
            hi = max(values) if values else 1.0
            if hi == lo:
                hi = lo + 1
            label = "MACD"
            signal_points = []
            for index in range(len(visible)):
                value = signal_line[offset + index] if offset + index < len(signal_line) else None
                if value is None:
                    continue
                x = plot_left + 8 + index * slot + slot / 2
                y = panel_bottom - 6 - ((value - lo) / (hi - lo)) * (panel_bottom - panel_top - 18)
                signal_points.append((x, y))
            if len(signal_points) >= 2:
                draw.line(signal_points, fill=(*down, 255), width=2)
        points = []
        for index in range(len(visible)):
            value = series[offset + index] if offset + index < len(series) else None
            if value is None:
                continue
            x = plot_left + 8 + index * slot + slot / 2
            y = panel_bottom - 6 - ((value - lo) / (hi - lo)) * (panel_bottom - panel_top - 18)
            points.append((x, y))
        if len(points) >= 2:
            draw.line(points, fill=(40, 90, 176, 255), width=2)
        ctx.text(label, (plot_left + 10, panel_top + 2), int(ctx.height * 0.016), (40, 90, 176), bold=True)
    if visible:
        ctx.text(_stamp(visible[0]["t"]), (plot_left + 8, price_bottom - 8), int(ctx.height * 0.016), ctx.color("muted"), anchor="lb")
        ctx.text(_stamp(visible[-1]["t"]), (plot_right - 8, price_bottom - 8), int(ctx.height * 0.016), ctx.color("muted"), anchor="rb")
    del names


def _title(ctx: FrameContext, series: dict) -> None:
    symbol = str(ctx.params.get("symbol") or series.get("symbol") or "")
    timeframe = str(ctx.params.get("timeframe") or series.get("timeframe") or "")
    mode = str(ctx.params.get("mode") or series.get("mode") or "")
    source = str(ctx.params.get("sourceName") or series.get("source") or "")
    headline = str(ctx.params.get("headline") or f"{symbol} · {timeframe}")
    left, top, _right, _bottom = ctx.safe()
    ctx.text(headline, (left, top), int(ctx.height * 0.04), ctx.color("ink"), bold=True)
    note = str(ctx.params.get("disclosure") or "")
    detail = " · ".join(part for part in (source, mode, note) if part)
    if detail:
        ctx.text(detail, (left, top + int(ctx.height * 0.046)), int(ctx.height * 0.02), ctx.color("muted"))


def _stamp(millis: int) -> str:
    from datetime import datetime, timezone
    moment = datetime.fromtimestamp(millis / 1000, timezone.utc)
    return moment.strftime("%Y-%m-%d %H:%M UTC")

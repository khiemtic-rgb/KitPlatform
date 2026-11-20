"""Short previews driven by the sequence selector. Does not rebuild the long films."""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "kit-video-production"))
sys.path.insert(0, str(ROOT / "tools" / "kit-video-production" / "tests"))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from kit_video.chart import load_candles
from kit_video.library import load_catalog
from kit_video.selection import evaluate, interpret, optimize_sequence, retrieve
from selection_cases import REVIEW_CRITERION, groups
from build_asset_proof import write_project

LIB = ROOT / "proof" / "kit-video-production-v1" / "library"
MARKET = LIB / "market"
DATA = "proof/kit-video-production-v1/library/market/BTCUSDT-1h.json"


def _rows_and_series():
    rows = load_catalog(LIB / "catalog.json")
    series = {
        "BTCUSDT": load_candles(MARKET / "BTCUSDT-1h.json"),
        "ETHUSDT": load_candles(MARKET / "ETHUSDT-1h.json"),
    }
    return rows, series


def _trace(outcomes: list[dict], rows: list[dict]) -> list[dict]:
    traced = []
    for outcome in outcomes:
        brief = outcome["brief"]
        found = {"accepted": [], "rejected": []}
        if brief["visual_intent"] not in {"chart", "unspecified"}:
            found = retrieve(brief, rows)
        chart = outcome.get("chart") or {}
        traced.append({
            "brief": brief,
            "selected": {
                "status": outcome["selected"]["status"],
                "assets": [asset.get("assetId") for asset in outcome["selected"]["assets"]],
                "reasons": outcome["selected"]["reasons"],
            },
            "beats": outcome["beats"],
            "chart": {key: chart.get(key) for key in ("status", "symbol", "timeframe", "source", "mode", "start", "count", "notes", "indicators", "highlight", "reason")} if chart else None,
            "accepted": [
                {"assetId": item["asset"]["assetId"], "score": item["score"], "parts": item["parts"], "reason": item["reason"]}
                for item in found["accepted"][:8]
            ],
            "rejected": found["rejected"][:8],
        })
    return traced


def _still_spec(outcome: dict, rows: list[dict]) -> dict:
    brief = outcome["brief"]
    assets = outcome["selected"]["assets"]
    if outcome["selected"]["status"] != "chosen" or not assets:
        raise SystemExit(f"Preview không có ảnh cho: {brief['narration_segment']}")
    duration = round(brief["time_range"][1] - brief["time_range"][0], 3)
    if brief["visual_intent"] == "composite":
        return {
            "id": brief["briefId"],
            "duration": duration,
            "dialogue": brief["narration_segment"],
            "goal": "Ghép giao diện với ảnh vỉ",
            "template": "layered_plate",
            "onScreenText": [],
            "assets": [asset["assetId"] for asset in assets],
            "continuity": True,
            "params": {
                "assetId": assets[0]["assetId"],
                "insetId": assets[1]["assetId"],
                "preset": "hold",
                "amount": 0,
                "caption": "Đối chiếu vỉ với màn tồn",
                "note": "Hai nguồn riêng. Không phải một cảnh chụp.",
                "continuity": True,
            },
        }
    row = assets[0]
    note = "Số trên hình là dữ liệu demo." if row.get("kind") == "screenshot" else f"{row.get('license', '')}. Ảnh tham chiếu."
    return {
        "id": brief["briefId"],
        "duration": duration,
        "dialogue": brief["narration_segment"],
        "goal": str(row.get("label") or brief["topic"]),
        "template": "annotated_plate",
        "onScreenText": [],
        "assets": [row["assetId"]],
        "params": {"assetId": row["assetId"], "preset": "hold", "amount": 0, "caption": str(row.get("label") or ""), "note": note},
    }


def _chart_specs(outcome: dict) -> list[dict]:
    chart = outcome["chart"]
    if chart.get("status") != "chosen":
        raise SystemExit(chart.get("reason") or "Không có chart.")
    start = int(chart["start"])
    count = int(chart["count"])
    highlight = chart.get("highlight") or {}
    shared = {
        "dataFile": DATA,
        "dataBase": "repo",
        "symbol": chart["symbol"],
        "timeframe": chart["timeframe"],
        "mode": chart["mode"],
        "sourceName": chart["source"],
        "disclosure": "Không phải khuyến nghị",
        "panBars": 0,
    }
    window_change = float(chart["notes"]["change"])
    percent = f"{round(window_change * 100, 1):.1f}".replace(".", ",")
    specs = []
    for beat in outcome["beats"]:
        duration = round(beat["end"] - beat["start"], 3)
        if beat["beatId"] == "wide":
            params = {**shared, "start": start, "count": count, "headline": "BTCUSDT khung một giờ"}
            template = "market_chart"
            shown = [params["headline"], params["disclosure"]]
        elif beat["beatId"] == "highlight":
            index = int(highlight["index"])
            offset = max(0, min(index - 6, count - 16))
            params = {
                **shared,
                "start": start + offset,
                "count": 16,
                "headline": "Nến tăng trong vùng",
                "focus": {"start": index - offset, "end": index - offset, "label": "Nến tăng"},
            }
            template = "market_chart"
            shown = [params["headline"], params["disclosure"]]
        elif beat["beatId"] == "sma":
            params = {**shared, "start": start, "count": count, "warmup": 19, "indicators": ["sma20"], "headline": "SMA 20 từ giá đóng cửa"}
            template = "market_chart"
            shown = [params["headline"], params["disclosure"], "SMA 20"]
        else:
            template = "kinetic_type"
            params = {"lines": [
                {"text": f"Đoạn này tăng {percent}%", "at": 0, "scale": 0.055, "bold": True},
                {"text": "Không phải khuyến nghị.", "at": 0.8, "scale": 0.036, "bold": False},
            ]}
            shown = []
        specs.append({
            "id": "t-" + beat["beatId"],
            "duration": duration,
            "dialogue": beat["new_information"],
            "goal": beat["emphasis"],
            "template": template,
            "onScreenText": shown,
            "assets": [],
            "params": params,
        })
    return specs


def main() -> None:
    rows, series = _rows_and_series()
    report = evaluate(groups(), rows, series)
    report["reviewCriterion"] = REVIEW_CRITERION
    (LIB / "selection-eval.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    if report["hardViolations"] or report["appropriateRate"] < 0.9 or report["chartsMet"] != report["charts"]:
        raise SystemExit("Bộ đánh giá chưa đạt. Không dựng preview.")
    novixa_lines = [
        ("s1-pack", "Đây là vỉ thuốc tham chiếu.", 0, 5),
        ("s2-hands", "Người đang kiểm một vỉ trên bàn.", 5, 10),
        ("s3-floor", "Không gian tủ thuốc của nhà thuốc bán lẻ.", 10, 15),
        ("s4-stock", "Tồn kho và hạn dùng được nhìn trên màn hình.", 15, 20),
        ("s5-join", "Giữ vỉ này cạnh màn tồn kho để đối chiếu.", 20, 25),
    ]
    novixa_briefs = []
    for scene_id, line, start, end in novixa_lines:
        brief = interpret(line, "NOVIXA", start, end)
        brief["briefId"] = scene_id
        novixa_briefs.append(brief)
    novixa_outcomes = optimize_sequence(novixa_briefs, rows, series)
    novixa_specs = [_still_spec(item, rows) for item in novixa_outcomes]
    trading_brief = interpret(
        "Bitcoin khung một giờ có một đoạn tăng rõ. Khoanh nến tăng mạnh nhất. Vẽ SMA 20 từ giá đóng cửa.",
        "AgentTrading",
        0,
        22,
    )
    trading_brief["briefId"] = "btc-rise"
    trading_outcomes = optimize_sequence([trading_brief], rows, series)
    trading_specs = _chart_specs(trading_outcomes[0])
    novixa_palette = {
        "background": [247, 249, 247], "ink": [0, 32, 96], "accent": [22, 140, 136],
        "paper": [255, 255, 255], "muted": [90, 104, 112], "accentAlt": [176, 96, 42], "weight": [24, 40, 72],
    }
    trading_palette = {
        "background": [232, 236, 242], "ink": [16, 28, 48], "accent": [18, 122, 82],
        "paper": [255, 252, 246], "muted": [84, 94, 110], "accentAlt": [164, 72, 64], "weight": [28, 36, 52],
    }
    write_project("novixa-select-preview", {"id": "novixa", "name": "Novixa — preview chọn cảnh"}, novixa_specs, rows, novixa_palette, "Preview do bộ chọn cảnh quyết định ảnh.")
    write_project("trading-select-preview", {"id": "agent-trading", "name": "Agent Trading — preview chọn cảnh"}, trading_specs, rows, trading_palette, "Preview chart lấy từ cửa sổ nến bộ chọn tìm được.")
    projects = ROOT / "proof" / "kit-video-production-v1" / "projects"
    (projects / "novixa-select-preview" / "scene-trace.json").write_text(json.dumps(_trace(novixa_outcomes, rows), ensure_ascii=False, indent=2), encoding="utf-8")
    (projects / "trading-select-preview" / "scene-trace.json").write_text(json.dumps(_trace(trading_outcomes, rows), ensure_ascii=False, indent=2), encoding="utf-8")
    print("eval", report["appropriate"], "/", report["scenes"], "baseline", round(report["baseline"]["appropriateRate"], 3), "jaccard", round(report["jaccard"]["appropriateRate"], 3))
    print("novixa", [spec["assets"] for spec in novixa_specs])
    print("chart", trading_outcomes[0]["chart"]["start"], trading_outcomes[0]["chart"]["notes"]["change"])


if __name__ == "__main__":
    main()

"""Short proofs for the asset gap. Does not rebuild the two long films."""

from __future__ import annotations

import json
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "kit-video-production"))
sys.path.insert(0, str(ROOT / "proof" / "kit-video-production-v1" / "library"))

from kit_video.analyze import parse_script, propose_shot_plan
from kit_video.chart import load_candles, ohlcv_errors, sma, structure_notes
from kit_video.library import assess_timeline, choose_asset, count_independent

import build_v2_films as films

LIB = ROOT / "proof" / "kit-video-production-v1" / "library"
PROJECTS = ROOT / "proof" / "kit-video-production-v1" / "projects"
DATA = "proof/kit-video-production-v1/library/market/BTCUSDT-1h.json"
COPIES = [
    ("incoming/Blister-of-tablets-jpg.jpg", "stills/ref-blister-foil.jpg"),
    ("incoming/Pills-and-medicines-02-jpg.jpg", "stills/ref-pack-still.jpg"),
    ("incoming/Brest-Greenberg-Pharmacy-Interior-2024-09-20-3798-jpg.jpg", "stills/ref-pharmacy-floor.jpg"),
    ("incoming/Person-holds-a-blister-pack-of-pills-while-sitting-at-a-table-jpg.jpg", "stills/ref-hands-blister.jpg"),
    ("incoming/HK-屯門醫院-Tuen-Mun-Hospital-ground-floor-Pharmacy-service-counter-window-n-waiting.jpg", "stills/ref-pharmacy-wait.jpg"),
]


def copy_stills() -> None:
    for source, dest in COPIES:
        src = LIB / source
        out = LIB / dest
        out.parent.mkdir(parents=True, exist_ok=True)
        if not src.is_file():
            raise SystemExit(f"Thiếu file đã tải: {src}")
        shutil.copyfile(src, out)


def _need(brand: str, category: str, keywords: list[str]) -> dict:
    return {"brand": brand, "category": category, "keywords": keywords}


def novixa_specs(rows: list[dict]) -> list[dict]:
    box = choose_asset(rows, _need("NOVIXA", "medicine.packaging", ["vỉ thuốc"]), [], [], [])
    place = choose_asset(rows, _need("NOVIXA", "environment.pharmacy", ["nhà thuốc"]), [], [], [])
    screen = choose_asset(rows, _need("NOVIXA", "", ["nvx-doc008-stock"]), [], [], [])
    for name, choice, expected in (
        ("vỉ", box, "ref-blister-foil"),
        ("nhà thuốc", place, "ref-pharmacy-floor"),
        ("tồn kho", screen, "nvx-doc008-stock"),
    ):
        if choice["status"] != "chosen" or choice["asset"]["assetId"] != expected:
            raise SystemExit(f"Selector không chọn {expected} cho {name}: {choice.get('reason')}")
    person = choose_asset(rows, _need("NOVIXA", "people.consult", ["dược sĩ", "tư vấn"]), [], [], [])
    desk = choose_asset(rows, _need("AgentTrading", "environment.desk", ["bàn giao dịch", "nhiều màn hình"]), [], [], [])
    if person["status"] != "gap" or desk["status"] != "gap":
        raise SystemExit("Selector đã lấp dược sĩ hoặc bàn giao dịch bằng ảnh không đúng việc.")
    action = choose_asset(rows, _need("NOVIXA", "people.handling", ["kiểm vỉ"]), [], [], [])
    if action["status"] != "chosen" or action["asset"]["assetId"] != "ref-hands-blister":
        raise SystemExit("Selector không chọn ảnh người kiểm vỉ.")
    foil = box["asset"]["assetId"]
    floor = place["asset"]["assetId"]
    stock = screen["asset"]["assetId"]
    hands = action["asset"]["assetId"]
    return [
        _plate("s1-pack", foil, "Vỉ thuốc tham chiếu", "CC BY-SA 4.0. Không phải sản phẩm Novixa.", "Đây là ảnh vỉ thuốc đã có giấy phép, không phải hộp của Novixa."),
        _plate("s2-action", hands, "Người kiểm vỉ thuốc", "CC BY 2.0. Không phải dược sĩ tại quầy.", "Người đang kiểm một vỉ. Đây không phải dược sĩ của Novixa."),
        _plate("s3-place", floor, "Không gian nhà thuốc", "CC BY-SA 3.0. Không phải cửa hàng Novixa.", "Đây là không gian nhà thuốc tham chiếu, không phải quầy của Novixa."),
        _plate("s4-ui", stock, "Màn tồn kho trên phần mềm", "Số trên hình là dữ liệu demo.", "Tồn kho và hạn dùng được nhìn trên màn hình phần mềm."),
        {
            "id": "s5-join",
            "duration": 5,
            "dialogue": "Ảnh vỉ đứng cạnh màn tồn. Hai nguồn này không phải một cảnh chụp.",
            "goal": "Ghép giao diện với ảnh vỉ",
            "template": "layered_plate",
            "onScreenText": [],
            "assets": [stock, foil],
            "continuity": True,
            "params": {
                "assetId": stock,
                "insetId": foil,
                "preset": "hold",
                "amount": 0,
                "caption": "Đối chiếu vỉ với màn tồn",
                "note": "Hai nguồn riêng. Không phải một cảnh chụp.",
                "continuity": True,
            },
        },
    ]


def _plate(scene_id: str, asset_id: str, caption: str, note: str, dialogue: str) -> dict:
    return {
        "id": scene_id,
        "duration": 5,
        "dialogue": dialogue,
        "goal": caption,
        "template": "annotated_plate",
        "onScreenText": [],
        "assets": [asset_id],
        "params": {"assetId": asset_id, "preset": "hold", "amount": 0, "caption": caption, "note": note},
    }


def trading_specs() -> tuple[list[dict], dict]:
    series = load_candles(ROOT / DATA)
    errors = ohlcv_errors(series["candles"])
    if errors:
        raise SystemExit("OHLCV lỗi: " + "; ".join(errors[:5]))
    start, count = 70, 48
    window = series["candles"][start:start + count]
    notes = structure_notes(window)
    change = float(notes["change"])
    if change < 0.03:
        raise SystemExit(f"Cửa sổ tăng không còn đúng: {change}")
    closes = [row["c"] for row in series["candles"]]
    full_sma = sma(closes, 20)
    warm = closes[start - 19:start + count]
    warm_sma = sma(warm, 20)
    expected = sum(closes[start - 19:start + 1]) / 20
    if abs(float(warm_sma[19]) - float(full_sma[start])) > 1e-6 or abs(float(warm_sma[19]) - expected) > 1e-6:
        raise SystemExit("SMA warmup không khớp trung bình 20 giá đóng cửa.")
    best = max(range(count), key=lambda index: (window[index]["c"] - window[index]["o"]) / window[index]["o"])
    if window[best]["c"] <= window[best]["o"]:
        raise SystemExit("Không có nến tăng để highlight.")
    region_offset = max(0, min(best - 6, count - 16))
    region_start = start + region_offset
    focus_at = best - region_offset
    percent = f"{round(change * 100, 1):.1f}".replace(".", ",")
    shared = {
        "dataFile": DATA,
        "dataBase": "repo",
        "symbol": "BTCUSDT",
        "timeframe": "1h",
        "mode": "historical",
        "sourceName": "binance-public-klines",
        "disclosure": "Không phải khuyến nghị",
        "panBars": 0,
    }
    specs = [
        _chart("s1-wide", {**shared, "start": start, "count": count, "headline": "BTCUSDT khung một giờ"}, "Bitcoin khung một giờ, nhìn cả đoạn tăng."),
        _chart("s2-region", {**shared, "start": region_start, "count": 16, "headline": "Vùng đang xét"}, "Thu hẹp vào vùng có nến tăng mạnh."),
        _chart("s3-highlight", {**shared, "start": region_start, "count": 16, "headline": "Nến tăng trong vùng", "focus": {"start": focus_at, "end": focus_at, "label": "Nến tăng"}}, "Đánh dấu đúng nến tăng trong vùng đó."),
        _chart("s4-sma", {**shared, "start": start, "count": count, "warmup": 19, "indicators": ["sma20"], "headline": "SMA 20 từ giá đóng cửa"}, "Đường trung bình dùng hai mươi giá đóng cửa, kể cả nến trước đoạn này."),
        {
            "id": "s5-close",
            "duration": 5,
            "dialogue": f"Đoạn này tăng {percent} phần trăm. Không phải khuyến nghị.",
            "goal": "Kết luận đúng số đã tính",
            "template": "kinetic_type",
            "onScreenText": [],
            "assets": [],
            "params": {"lines": [
                {"text": f"Đoạn này tăng {percent}%", "at": 0, "scale": 0.055, "bold": True},
                {"text": "Không phải khuyến nghị.", "at": 0.8, "scale": 0.036, "bold": False},
            ]},
        },
    ]
    evidence = {
        "symbol": series["symbol"],
        "timeframe": series["timeframe"],
        "source": series["source"],
        "mode": series["mode"],
        "rows": len(series["candles"]),
        "ohlcvErrors": errors,
        "window": {"start": start, "count": count, "change": change, "percentText": percent},
        "notes": notes,
        "highlight": {"localIndex": best, "open": window[best]["o"], "close": window[best]["c"]},
        "region": {"start": region_start, "count": 16, "focus": focus_at},
        "sma20": {"length": 20, "atBar": start, "value": full_sma[start], "warmup": 19},
        "rsi": "Wilder, độ dài 14, tính từ giá đóng cửa. Bản proof này không vẽ RSI.",
        "macd": "EMA 12 và 26, signal EMA 9. Bản proof này không vẽ MACD.",
        "unchangedFile": DATA,
    }
    return specs, evidence


def _chart(scene_id: str, params: dict, dialogue: str) -> dict:
    shown = [params["headline"], params["disclosure"]]
    if "sma20" in (params.get("indicators") or []):
        shown.append("SMA 20")
    return {
        "id": scene_id,
        "duration": 5,
        "dialogue": dialogue,
        "goal": params["headline"],
        "template": "market_chart",
        "onScreenText": shown,
        "assets": [],
        "params": params,
    }


def write_project(project_id: str, brand: dict, specs: list[dict], rows: list[dict], palette: dict, objective: str) -> None:
    project = PROJECTS / project_id
    project.mkdir(parents=True, exist_ok=True)
    script = films._script(project_id, specs)
    (project / "script.md").write_text(script, encoding="utf-8")
    proposed = propose_shot_plan(script, project_id)
    by_id = {item["id"]: item for item in specs}
    segments = {item["id"]: item for item in parse_script(script)}
    scenes = []
    for scene in proposed["scenes"]:
        spec = by_id[scene["sceneId"]]
        if segments[scene["sceneId"]].get("dialogue", "").strip() != spec["dialogue"]:
            raise SystemExit(f"Lời cảnh {scene['sceneId']} lệch spec.")
        scene["approvalStatus"] = "approved"
        scene["durationSeconds"] = spec["duration"]
        scene["durationSource"] = "fixed"
        scene["voiceLeadSeconds"] = 0
        scene["templateId"] = spec["template"]
        scene["params"] = spec["params"]
        scene["assets"] = spec["assets"]
        scene["onScreenText"] = spec["onScreenText"]
        scene["audio"]["voiceTrackId"] = None
        scene["audio"]["intentionalSilences"] = [{
            "start": 0,
            "end": spec["duration"],
            "reason": "Preview hình, chưa lồng tiếng.",
        }]
        scene["audio"]["syncNotes"] = "Chưa có lời đọc."
        scene["transitionOut"] = {"type": "cut", "note": "Cắt khi câu đã rõ."}
        scene["previewFractions"] = [0.15, 0.55, 0.9]
        scene["acceptance"] = ["Chữ đúng câu đã khai.", "Không dùng ảnh khác nội dung."]
        if spec.get("continuity"):
            scene["continuity"] = True
        scenes.append(scene)
    shot = {
        "schemaVersion": "kit-video-shot-plan/1",
        "projectId": project_id,
        "approvalStatus": "approved",
        "source": "script-analysis",
        "warnings": proposed["warnings"],
        "scenes": scenes,
    }
    (project / "shot-plan.json").write_text(json.dumps(shot, ensure_ascii=False, indent=2), encoding="utf-8")
    used = {asset_id for spec in specs for asset_id in spec["assets"]}
    assets = []
    for row in rows:
        if row["assetId"] not in used:
            continue
        item = {
            "assetId": row["assetId"],
            "kind": row["kind"],
            "source": "library",
            "path": row["path"],
            "pathBase": "repo",
            "version": "repository",
            "sha256": row["sha256"],
            "usageRights": row["usageRights"],
            "approval": "approved",
            "role": row["role"],
            "label": row["label"],
        }
        if row.get("visualSource"):
            item["visualSource"] = row["visualSource"]
        assets.append(item)
    total = sum(item["duration"] for item in specs)
    manifest = json.loads((PROJECTS / "novixa-long" / "manifest.json").read_text(encoding="utf-8"))
    manifest["projectId"] = project_id
    manifest["projectName"] = brand["name"]
    manifest["brand"] = brand
    manifest["script"]["notes"] = objective
    manifest["durationPolicy"] = {"mode": "target", "targetSeconds": total, "notes": "Bản proof ngắn, không phải video ba phút."}
    manifest["storyStructure"] = [{"beatId": spec["id"], "summary": spec["goal"]} for spec in specs]
    manifest["voice"]["tracks"] = []
    manifest["assets"] = assets
    manifest["visualStyle"]["palette"] = palette
    manifest["visualStyle"]["profileId"] = brand["id"] + "-proof"
    manifest["outputs"] = []
    for stage in manifest["pipeline"]:
        if stage["stage"] == "asset_creation":
            stage["notes"] = "Ảnh tham chiếu lấy từ Wikimedia Commons, có ghi giấy phép. Không gọi API trả phí."
    (project / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print("project", project_id, "seconds", total)


def _shot(project_id: str) -> list[dict]:
    path = PROJECTS / project_id / "shot-plan.json"
    if not path.is_file():
        return []
    return json.loads(path.read_text(encoding="utf-8"))["scenes"]


def main() -> None:
    copy_stills()
    rows = films.build_catalog()
    long_specs = [{"id": scene["sceneId"], "assets": scene.get("assets") or []} for scene in _shot("novixa-long")]
    films.mark_usage(rows, "novixa-long", long_specs)
    novixa = novixa_specs(rows)
    trading, evidence = trading_specs()
    novixa_palette = {
        "background": [247, 249, 247], "ink": [0, 32, 96], "accent": [22, 140, 136],
        "paper": [255, 255, 255], "muted": [90, 104, 112], "accentAlt": [176, 96, 42], "weight": [24, 40, 72],
    }
    trading_palette = {
        "background": [232, 236, 242], "ink": [16, 28, 48], "accent": [18, 122, 82],
        "paper": [255, 252, 246], "muted": [84, 94, 110], "accentAlt": [164, 72, 64], "weight": [28, 36, 52],
    }
    write_project("novixa-asset-proof", {"id": "novixa", "name": "Novixa — proof tài nguyên"}, novixa, rows, novixa_palette, "Vỉ thuốc, thao tác kiểm vỉ, không gian nhà thuốc, giao diện, và một cảnh ghép.")
    write_project("trading-beat-proof", {"id": "agent-trading", "name": "Agent Trading — proof nhịp biểu đồ"}, trading, rows, trading_palette, "Một đoạn Bitcoin đã kiểm tra, chia thành năm nhịp.")
    films.mark_usage(rows, "novixa-asset-proof", novixa)
    before = {
        "novixa-long": assess_timeline(_shot("novixa-long"), rows),
        "trading-long": assess_timeline(_shot("trading-long"), rows),
    }
    after = {
        "novixa-asset-proof": assess_timeline(_shot("novixa-asset-proof"), rows),
        "trading-beat-proof": assess_timeline(_shot("trading-beat-proof"), rows),
    }
    added = [row for row in rows if row.get("kind") == "photo" and row.get("approval") == "approved" and not row.get("derived")]
    report = {
        "independent": count_independent(rows),
        "added": [
            {
                "assetId": row["assetId"],
                "category": row["category"],
                "license": row.get("license"),
                "author": row.get("author"),
                "sourcePage": row.get("sourcePage"),
            }
            for row in added
        ],
        "rejected": [row["assetId"] for row in rows if row.get("approval") == "rejected"],
        "stillMissing": [
            "Hộp thuốc bán lẻ đủ các mặt, chai và vỉ mang nhãn riêng.",
            "Kho, quầy tư vấn, dược sĩ và khách tại quầy.",
            "Bàn giao dịch, màn hình đang có biểu đồ, thiết bị phân tích.",
        ],
        "chart": evidence,
        "timelineBefore": before,
        "timelineAfter": after,
        "target": {"NOVIXA": 80, "AgentTrading": 80, "Shared": 40},
        "targetMet": False,
    }
    (LIB / "asset-gap-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("added", [row["assetId"] for row in added])
    print("independent", report["independent"])
    print("percent", evidence["window"]["percentText"])
    print("static before", [item["sceneId"] for item in before["trading-long"]["findings"] if item["code"] == "static_hold"])
    print("static after", [item["sceneId"] for item in after["trading-beat-proof"]["findings"] if item["code"] == "static_hold"])


if __name__ == "__main__":
    main()

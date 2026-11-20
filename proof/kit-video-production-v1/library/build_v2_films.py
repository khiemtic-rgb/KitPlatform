"""Build the shared still catalog and the two long films.

Charts are not counted as stills. A near-duplicate is linked to its parent and
is not a second original. This file lives outside the engine on purpose.
"""

from __future__ import annotations

import json
import sys
import urllib.request
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "kit-video-production"))

from kit_video.analyze import parse_script, propose_shot_plan
from kit_video.chart import find_regimes, load_candles, macd, ohlcv_errors, sma, structure_notes
from kit_video.library import average_hash, choose_asset, count_independent, file_sha256, hamming, plan_usage

LIB = ROOT / "proof" / "kit-video-production-v1" / "library"
MARKET = LIB / "market"
RIGHTS = (
    "Ảnh chụp giao diện Novixa trong tài liệu nội bộ của repository. "
    "Dùng cho video giải thích sản phẩm. Số trên màn là dữ liệu demo và phải được ghi chú trên hình."
)

SCREENS = [
    ("docs/novixa/launch/DOC-008/assets/screenshots/01-dashboard.png", "software.dashboard", ["tổng quan", "dashboard", "nvx-doc008-dashboard"], "Màn tổng quan Novixa"),
    ("docs/novixa/launch/DOC-008/assets/screenshots/02-catalog-products.png", "software.catalog", ["danh mục", "sản phẩm", "nvx-doc008-catalog"], "Danh mục sản phẩm trên phần mềm"),
    ("docs/novixa/launch/DOC-008/assets/screenshots/03-procurement-grn.png", "inventory.receiving", ["nhập hàng", "kho", "nvx-doc008-grn"], "Màn nhập hàng"),
    ("docs/novixa/launch/DOC-008/assets/screenshots/04-inventory-stock.png", "inventory.stock", ["tồn kho", "hạn", "nvx-doc008-stock"], "Màn tồn kho"),
    ("docs/novixa/launch/DOC-008/assets/screenshots/05-sales-pos.png", "software.pos", ["bán hàng", "pos", "nvx-doc008-pos"], "Màn bán hàng tại quầy"),
    ("docs/novixa/launch/DOC-008/assets/screenshots/06-customer-crm.png", "customer.crm", ["khách hàng", "hồ sơ", "nvx-doc008-crm"], "Hồ sơ khách hàng"),
    ("docs/novixa/launch/DOC-008/assets/screenshots/07-o2o-drafts.png", "software.draft", ["đơn nháp", "nvx-doc008-draft"], "Đơn nháp"),
    ("docs/novixa/launch/DOC-008/assets/screenshots/08-reports-home.png", "software.reports", ["báo cáo", "nvx-doc008-reports"], "Màn báo cáo"),
    ("docs/novixa/launch/DOC-008/assets/screenshots/09-customer-app-home.png", "customer.app", ["ứng dụng khách", "nvx-doc008-app"], "Ứng dụng khách hàng"),
    ("docs/novixa/launch/DOC-008/assets/screenshots/10-staff-pos.png", "software.pos", ["pos nhân viên", "nvx-doc008-staff-pos"], "Màn bán hàng của nhân viên"),
    ("docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/01-login.png", "software.login", ["đăng nhập", "nvx-qld-login"], "Màn đăng nhập liên thông"),
    ("docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/02-dashboard.png", "software.dashboard", ["tổng quan liên thông", "nvx-qld-dashboard"], "Tổng quan liên thông"),
    ("docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/03-pos.png", "software.pos", ["bán hàng liên thông", "nvx-qld-pos"], "Bán hàng trong bộ liên thông"),
    ("docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/04-drug-master.png", "software.catalog", ["danh mục thuốc", "nvx-qld-drug"], "Danh mục thuốc trên phần mềm"),
    ("docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/05-inventory.png", "inventory.stock", ["tồn liên thông", "nvx-qld-stock"], "Tồn kho liên thông"),
    ("docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/06-grn.png", "inventory.receiving", ["nhập liên thông", "nvx-qld-grn"], "Nhập hàng liên thông"),
    ("docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/07-sale.png", "software.sale", ["hóa đơn", "nvx-qld-sale"], "Hóa đơn bán"),
    ("docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/08-report-nxt.png", "software.reports", ["xuất nhập tồn", "nvx-qld-nxt"], "Báo cáo xuất nhập tồn"),
    ("docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/09-report-revenue.png", "software.reports", ["doanh thu", "nvx-qld-revenue"], "Báo cáo doanh thu"),
    ("docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/10-drug-connectivity.png", "software.compliance", ["liên thông", "nvx-qld-connect"], "Liên thông dữ liệu thuốc"),
    ("docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/11-api-config.png", "software.compliance", ["cấu hình", "nvx-qld-api"], "Cấu hình kết nối"),
    ("docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/12-sync-log.png", "software.compliance", ["đồng bộ", "nvx-qld-sync"], "Nhật ký đồng bộ"),
]

LOGOS = [
    ("docs/novixa/assets/brand/novixa-logo.png", "NOVIXA", "brand.mark", ["logo-novixa"], "Logo Novixa gốc"),
]

REJECTED = [
    ("proof/kit-video-production-v1/projects/novixa-pharmacy-commercial/assets/labels/v5/pkg-vitamin-c.png", "NOVIXA", "medicine.packaging", "pkg-vitamin-c", "Thẻ vẽ phẳng, không phải ảnh hộp thuốc."),
    ("proof/kit-video-production-v1/projects/novixa-pharmacy-commercial/keyframes/v7/s01-pharmacy.jpg", "NOVIXA", "environment.pharmacy", "s01-pharmacy", "Minh họa hoạt hình, hộp hình khối, không dùng làm ảnh thật."),
    ("proof/kit-video-production-v1/library/incoming/Pills-in-blister-pack-jpg.jpg", "NOVIXA", "medicine.packaging", "ref-branded-blisters", "Tên thuốc thật chiếm khung. Không dùng làm ảnh sản phẩm."),
    ("proof/kit-video-production-v1/library/incoming/Pharmacy-counter-six--plus-Influx-of-volunteers-help-keep-Rader-pharmacy-at-full.jpg", "NOVIXA", "people.counter", "ref-counter-credential", "Nhãn trên màn hình có thông tin đăng nhập. Không dùng."),
    ("proof/kit-video-production-v1/library/incoming/Nellis-satellite-pharmacy-121023-F-TT327-099-jpg.jpg", "NOVIXA", "people.consult", "ref-pharmacy-brochure", "Tờ rơi, không phải thao tác tại quầy."),
    ("proof/kit-video-production-v1/library/incoming/dual-monitors.jpg", "AgentTrading", "environment.desk", "ref-gaming-monitors", "Hai màn hình tắt trong phòng máy. Không phải bàn giao dịch."),
]

PHOTOS = [
    {
        "path": "proof/kit-video-production-v1/library/stills/ref-blister-foil.jpg",
        "assetId": "ref-blister-foil",
        "brand": "NOVIXA",
        "category": "medicine.packaging",
        "keywords": ["vỉ thuốc", "ref-blister-foil"],
        "label": "Vỉ thuốc tham chiếu",
        "license": "CC BY-SA 4.0",
        "author": "Tarasna0922",
        "sourcePage": "https://commons.wikimedia.org/wiki/File:Blister_of_tablets.jpg",
        "usageRights": "Wikimedia Commons File:Blister of tablets.jpg, Tarasna0922, CC BY-SA 4.0. Ảnh tham chiếu, không phải sản phẩm Novixa.",
        "limits": ["Không phải sản phẩm Novixa.", "Ghi công Tarasna0922, CC BY-SA 4.0.", "Bản dựng lại có thể phải giữ điều khoản share-alike."],
    },
    {
        "path": "proof/kit-video-production-v1/library/stills/ref-pack-still.jpg",
        "assetId": "ref-pack-still",
        "brand": "NOVIXA",
        "category": "medicine.packaging",
        "keywords": ["chai thuốc", "lọ thuốc", "ref-pack-still"],
        "label": "Vỉ, chai và lọ tham chiếu",
        "license": "CC BY-SA 4.0",
        "author": "BuhaM",
        "sourcePage": "https://commons.wikimedia.org/wiki/File:Pills_and_medicines_02.jpg",
        "usageRights": "Wikimedia Commons File:Pills and medicines 02.jpg, BuhaM, CC BY-SA 4.0. Ảnh tham chiếu, không phải sản phẩm Novixa.",
        "limits": ["Ảnh studio, không phải hộp bán lẻ.", "Ghi công BuhaM, CC BY-SA 4.0."],
    },
    {
        "path": "proof/kit-video-production-v1/library/stills/ref-pharmacy-floor.jpg",
        "assetId": "ref-pharmacy-floor",
        "brand": "NOVIXA",
        "category": "environment.pharmacy",
        "keywords": ["nhà thuốc", "tủ thuốc", "ref-pharmacy-floor"],
        "label": "Không gian nhà thuốc bán lẻ",
        "license": "CC BY-SA 3.0",
        "author": "Mike1979 Russia",
        "sourcePage": "https://commons.wikimedia.org/wiki/File:Brest_Greenberg_Pharmacy_Interior_2024-09-20_3798.jpg",
        "usageRights": "Wikimedia Commons File:Brest Greenberg Pharmacy Interior 2024-09-20 3798.jpg, Mike1979 Russia, CC BY-SA 3.0. Không phải cửa hàng Novixa.",
        "limits": ["Không phải cửa hàng Novixa.", "Ghi công Mike1979 Russia, CC BY-SA 3.0."],
    },
    {
        "path": "proof/kit-video-production-v1/library/stills/ref-hands-blister.jpg",
        "assetId": "ref-hands-blister",
        "brand": "NOVIXA",
        "category": "people.handling",
        "keywords": ["kiểm vỉ", "ref-hands-blister"],
        "label": "Người kiểm vỉ thuốc",
        "license": "CC BY 2.0",
        "author": "Shixart1985",
        "sourcePage": "https://commons.wikimedia.org/wiki/File:Person_holds_a_blister_pack_of_pills_while_sitting_at_a_table.jpg",
        "usageRights": "Wikimedia Commons File:Person holds a blister pack of pills while sitting at a table.jpg, Shixart1985, CC BY 2.0. Không phải dược sĩ tại quầy.",
        "limits": ["Không phải dược sĩ Novixa.", "Ghi công Shixart1985, CC BY 2.0."],
    },
    {
        "path": "proof/kit-video-production-v1/library/stills/ref-pharmacy-wait.jpg",
        "assetId": "ref-pharmacy-wait",
        "brand": "NOVIXA",
        "category": "environment.pharmacy.wait",
        "keywords": ["phòng chờ", "ref-pharmacy-wait"],
        "label": "Phòng chờ quầy thuốc bệnh viện",
        "license": "CC BY-SA 4.0",
        "author": "YipshaiukOWL",
        "sourcePage": "https://commons.wikimedia.org/wiki/File:HK_%E5%B1%AF%E9%96%80%E9%86%AB%E9%99%A2_Tuen_Mun_Hospital_ground_floor_Pharmacy_service_counter_window_n_waiting_room_plastic_seats_July_2016_DSC.jpg",
        "usageRights": "Wikimedia Commons, YipshaiukOWL, CC BY-SA 4.0. Phòng chờ bệnh viện, không phải quầy bán lẻ Novixa.",
        "limits": ["Phòng chờ, quầy ở phía sau.", "Ghi công YipshaiukOWL, CC BY-SA 4.0."],
    },
]


def _row(path: Path, brand: str, category: str, keywords: list[str], label: str, approval: str, limits: list[str], kind: str) -> dict:
    from PIL import Image

    with Image.open(path) as image:
        width, height = image.size
    asset_id = keywords[-1] if keywords else path.stem
    return {
        "assetId": asset_id,
        "brand": brand,
        "kind": kind,
        "category": category,
        "keywords": keywords,
        "style": "software-screenshot" if kind == "screenshot" else "brand-mark",
        "width": width,
        "height": height,
        "aspect": round(width / height, 4) if height else None,
        "source": "first-party",
        "license": "Tài liệu nội bộ Novixa trong repository. Không phải ảnh stock.",
        "approval": approval,
        "path": path.relative_to(ROOT).as_posix(),
        "pathBase": "repo",
        "sha256": file_sha256(path),
        "averageHash": average_hash(path),
        "similarityGroup": asset_id,
        "parentId": None,
        "derived": False,
        "usage": [],
        "allow": ["explainer"],
        "limits": limits,
        "label": label,
        "role": "reference" if kind == "screenshot" else "brand_logo",
        "visualSource": "SOFTWARE_SCREENSHOT" if kind == "screenshot" else "",
        "usageRights": RIGHTS if kind == "screenshot" else "brand asset, use unmodified",
    }


def build_catalog() -> list[dict]:
    rows = []
    for rel, category, keywords, label in SCREENS:
        path = ROOT / rel
        if not path.is_file():
            continue
        rows.append(_row(path, "NOVIXA", category, keywords, label, "approved", ["Số trên màn là dữ liệu demo."], "screenshot"))
    for rel, brand, category, keywords, label in LOGOS:
        path = ROOT / rel
        if path.is_file():
            rows.append(_row(path, brand, category, keywords, label, "approved", ["Giữ nguyên tệp, không sửa byte."], "logo"))
    for spec in PHOTOS:
        path = ROOT / spec["path"]
        if path.is_file():
            rows.append(_photo(path, spec))
    for rel, brand, category, asset_id, reason in REJECTED:
        path = ROOT / rel
        if not path.is_file():
            continue
        row = _row(path, brand, category, [asset_id], path.name, "rejected", [reason], "image")
        row["visualSource"] = ""
        row["usageRights"] = "Không dùng. " + reason
        rows.append(row)
    _link_duplicates(rows)
    payload = {"assets": rows}
    LIB.mkdir(parents=True, exist_ok=True)
    (LIB / "catalog.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print("catalog", count_independent(rows))
    return rows


def _photo(path: Path, spec: dict) -> dict:
    from PIL import Image

    with Image.open(path) as image:
        width, height = image.size
    return {
        "assetId": spec["assetId"],
        "brand": spec["brand"],
        "kind": "photo",
        "category": spec["category"],
        "keywords": spec["keywords"],
        "style": "reference-photo",
        "width": width,
        "height": height,
        "aspect": round(width / height, 4) if height else None,
        "source": "wikimedia-commons",
        "license": spec["license"],
        "author": spec["author"],
        "sourcePage": spec["sourcePage"],
        "approval": "approved",
        "path": path.relative_to(ROOT).as_posix(),
        "pathBase": "repo",
        "sha256": file_sha256(path),
        "averageHash": average_hash(path),
        "similarityGroup": spec["assetId"],
        "parentId": None,
        "derived": False,
        "usage": [],
        "allow": ["explainer"],
        "limits": spec["limits"],
        "label": spec["label"],
        "role": "illustration",
        "visualSource": "",
        "usageRights": spec["usageRights"],
    }


def _link_duplicates(rows: list[dict]) -> None:
    originals = []
    for row in rows:
        if row.get("approval") == "rejected":
            continue
        digest = row.get("sha256")
        visual = int(row.get("averageHash") or 0)
        parent = None
        for earlier in originals:
            if earlier["sha256"] == digest or (visual and hamming(visual, int(earlier["averageHash"])) <= 3):
                parent = earlier
                break
        if parent is None:
            originals.append(row)
            continue
        row["parentId"] = parent["assetId"]
        row["derived"] = True
        row["similarityGroup"] = parent["similarityGroup"]
        row["approval"] = "review"
        row["limits"] = list(row["limits"]) + ["Gần giống một ảnh đã có. Không tính là tài nguyên độc lập và không dùng làm ảnh thứ hai."]


def fetch_market() -> None:
    MARKET.mkdir(parents=True, exist_ok=True)
    for symbol in ("BTCUSDT", "ETHUSDT"):
        dest = MARKET / f"{symbol}-1h.json"
        if dest.is_file():
            series = load_candles(dest)
            print(symbol, "cached", len(series["candles"]), "errors", len(ohlcv_errors(series["candles"])))
            continue
        payload = _download(symbol)
        dest.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
        series = load_candles(dest)
        print(symbol, "fetched", len(series["candles"]), "errors", len(ohlcv_errors(series["candles"])))


def _download(symbol: str) -> dict:
    query = f"symbol={symbol}&interval=1h&limit=1000"
    hosts = (
        "https://data-api.binance.vision/api/v3/klines?" + query,
        "https://api.binance.com/api/v3/klines?" + query,
    )
    last = None
    for url in hosts:
        try:
            with urllib.request.urlopen(url, timeout=40) as response:
                raw = json.loads(response.read().decode("utf-8"))
            candles = []
            for item in raw:
                candles.append({
                    "t": int(item[0]),
                    "o": float(item[1]),
                    "h": float(item[2]),
                    "l": float(item[3]),
                    "c": float(item[4]),
                    "v": float(item[5]),
                })
            return {
                "symbol": symbol,
                "timeframe": "1h",
                "source": "binance-public-klines",
                "mode": "historical",
                "url": url.split("?")[0],
                "candles": candles,
            }
        except Exception as exc:
            last = exc
    raise SystemExit(f"Không tải được nến {symbol}: {last}")


def _pct(change: float) -> str:
    return f"{abs(change) * 100:.1f}".replace(".", ",")


def _window(series: dict, found: dict | None, purpose: str) -> dict:
    candles = series["candles"]
    if not found:
        raise SystemExit(f"Không tìm được cửa sổ {purpose} cho {series['symbol']}.")
    start = int(found["start"])
    count = 48
    pan = 12
    notes = structure_notes(candles[start:start + count + pan])
    return {"start": start, "count": count, "pan": pan, "notes": notes, "change": found.get("change")}


def _chart_scene(scene_id: str, duration: float, dialogue: str, series_name: str, window: dict, indicators: list[str], headline: str, focus: dict | None) -> dict:
    labels = ["Khối lượng", headline, "Không phải khuyến nghị"]
    if "sma20" in indicators:
        labels.append("SMA 20")
    if "rsi14" in indicators:
        labels.append("RSI 14")
    if "macd" in indicators:
        labels.append("MACD")
    if focus and focus.get("label"):
        labels.append(str(focus["label"]))
    return {
        "id": scene_id,
        "duration": duration,
        "dialogue": dialogue,
        "goal": headline,
        "template": "market_chart",
        "onScreenText": labels,
        "params": {
            "dataFile": f"proof/kit-video-production-v1/library/market/{series_name}-1h.json",
            "dataBase": "repo",
            "symbol": series_name,
            "timeframe": "1h",
            "mode": "historical",
            "sourceName": "binance-public-klines",
            "headline": headline,
            "disclosure": "Không phải khuyến nghị",
            "start": window["start"],
            "count": window["count"],
            "panBars": window["pan"],
            "indicators": indicators,
            "focus": focus,
        },
        "assets": [],
        "need": None,
    }


def _flat_slice(candles: list[dict], length: int = 48) -> tuple | None:
    """Every close in the window stays near the first, so a sideways title matches the whole picture."""
    best = None
    for start in range(0, len(candles) - length):
        window = candles[start:start + length]
        first = window[0]["c"]
        if first <= 0:
            continue
        change = window[-1]["c"] / first - 1
        drift = max(abs(row["c"] / first - 1) for row in window)
        span = (max(row["h"] for row in window) - min(row["l"] for row in window)) / first
        if drift > 0.015 or abs(change) > 0.008 or span < 0.012:
            continue
        if best is None or drift < best[0]:
            best = (drift, start, change, span)
    return best


def _rising_slice(candles: list[dict], length: int = 48) -> tuple | None:
    """A window that finishes higher and does not give the gain back in the second half."""
    best = None
    for start in range(0, len(candles) - length):
        window = candles[start:start + length]
        first = window[0]["c"]
        last = window[-1]["c"]
        if first <= 0:
            continue
        change = last / first - 1
        if change < 0.03:
            continue
        late_min = min(row["c"] for row in window[length // 2:])
        high = max(row["h"] for row in window)
        early = sum(row["c"] for row in window[:length // 3])
        late = sum(row["c"] for row in window[-(length // 3):])
        if late_min < first * 1.005 or last < high * 0.97 or late <= early:
            continue
        if best is None or change > best[0]:
            best = (change, start)
    return best


def trading_specs() -> tuple[list[dict], dict]:
    btc = load_candles(MARKET / "BTCUSDT-1h.json")
    eth = load_candles(MARKET / "ETHUSDT-1h.json")
    btc_regimes = find_regimes(btc["candles"], 72)
    eth_regimes = find_regimes(eth["candles"], 72)
    report = {"btc": {key: value for key, value in btc_regimes.items()}, "eth": {key: value for key, value in eth_regimes.items()}, "ohlcv": {}}
    for name, series in (("BTCUSDT", btc), ("ETHUSDT", eth)):
        report["ohlcv"][name] = {
            "rows": len(series["candles"]),
            "errors": ohlcv_errors(series["candles"]),
            "first": series["candles"][0]["t"],
            "last": series["candles"][-1]["t"],
            "source": series["source"],
            "mode": series["mode"],
            "timeframe": series["timeframe"],
        }
    rise = _rising_slice(btc["candles"])
    if rise is None:
        raise SystemExit("Không có đoạn tăng nhìn rõ trên dữ liệu đã tải.")
    up = {
        "start": rise[1],
        "count": 48,
        "pan": 0,
        "notes": structure_notes(btc["candles"][rise[1]:rise[1] + 48]),
        "change": rise[0],
    }
    down = _window(btc, btc_regimes.get("down"), "down")
    flat_found = _flat_slice(eth["candles"])
    flat_name = "ETHUSDT"
    flat_series = eth
    if flat_found is None:
        flat_found = _flat_slice(btc["candles"])
        flat_name = "BTCUSDT"
        flat_series = btc
    if flat_found is None:
        raise SystemExit("Không có đoạn đi ngang trong dữ liệu đã tải.")
    flat = {
        "start": flat_found[1],
        "count": 48,
        "pan": 0,
        "notes": structure_notes(flat_series["candles"][flat_found[1]:flat_found[1] + 48]),
        "change": flat_found[2],
    }
    used = [int(up["start"]), int(down["start"])]
    macd_start = 0
    for start in range(0, len(btc["candles"]) - 72, 72):
        if all(start + 72 <= other or start >= other + 72 for other in used):
            macd_start = start
            break
    macd_window = {"start": macd_start, "count": 48, "pan": 12, "notes": {}, "change": None}
    closes = [row["c"] for row in btc["candles"][macd_start:macd_start + 60]]
    line, _signal = macd(closes)
    last = next(value for value in reversed(line) if value is not None)
    macd_title = "MACD dương cuối cửa sổ" if last >= 0 else "MACD âm cuối cửa sổ"
    pull = up["notes"].get("pullback")
    if pull:
        pull_len = 32
        rise_end = up["start"] + up["count"]
        pull_start = min(up["start"] + max(0, int(pull["at"]) - 12), rise_end - pull_len)
        pull_window = {
            "start": pull_start,
            "count": pull_len,
            "pan": 0,
            "notes": structure_notes(btc["candles"][pull_start:pull_start + pull_len]),
            "change": None,
        }
        rel = up["start"] + int(pull["at"]) - pull_start
        pull_title = "BTCUSDT có nhịp hồi trong đoạn tăng"
        pull_focus = {"start": max(0, rel - 4), "end": min(pull_len - 1, rel + 6), "label": "Nhịp hồi"}
        pull_line = "Trong đoạn tăng có một nhịp hồi. Nhịp này lấy từ chính các nến, không phải hình vẽ thêm."
    else:
        pull_window = up
        pull_title = "BTCUSDT nhìn gần đoạn tăng"
        pull_focus = {"start": 20, "end": 40, "label": "Đoạn giữa"}
        pull_line = "Cửa sổ này không có nhịp hồi đủ rõ. Hình chỉ đưa gần đoạn tăng."
    down_focus = {
        "start": int(down["notes"].get("peakAt") or 8),
        "end": int(down["notes"].get("troughAt") or 40),
        "label": "Mức giảm của giá",
    }
    surge = down["notes"].get("volumeSurgeAt")
    up_pct = _pct(up["notes"]["change"])
    down_pct = _pct(down["notes"]["change"])
    range_name = "Ethereum" if flat_name == "ETHUSDT" else "Bitcoin"
    specs = [
        _board_open("s1-open", 12, "Đọc giá trước khi nói về lệnh.", "Agent Trading", "Đọc giá trước khi nói về lệnh."),
        _chart_scene("s2-up", 26, f"Bitcoin khung một giờ, đoạn này tăng {up_pct} phần trăm. Đường trung bình được tính từ giá đóng cửa.", "BTCUSDT", up, ["sma20"], f"BTCUSDT tăng {up_pct}% · khung 1 giờ", {"start": 4, "end": 32, "label": "Đoạn tăng"}),
        _chart_scene("s3-pull", 20, pull_line, "BTCUSDT", pull_window, ["sma20"], pull_title, pull_focus),
        _chart_scene("s4-down", 26, f"Cùng mã, một đoạn khác giảm {down_pct} phần trăm. Vùng tô là mức giảm của giá, không phải lợi nhuận chiến lược.", "BTCUSDT", down, ["sma20"], f"BTCUSDT giảm {down_pct}% · khung 1 giờ", down_focus),
        _chart_scene("s5-range", 22, f"{range_name} khung một giờ nằm trong biên hẹp. RSI được tính từ giá đóng cửa.", flat_name, flat, ["rsi14"], f"{flat_name} biên hẹp · RSI 14", None),
        _chart_scene("s6-macd", 20, "MACD của một đoạn Bitcoin khác, tính từ giá đóng cửa. Không phải tín hiệu vào lệnh.", "BTCUSDT", macd_window, ["macd"], f"BTCUSDT · {macd_title}", None),
        _lanes("s7-agent", 16, "Agent đọc dữ liệu, xét điều kiện, rồi mới nói tới rủi ro.", "Việc agent làm", [
            {"label": "Nguồn", "events": [{"text": "Giá", "at": 0.7}]},
            {"label": "Xét", "events": [{"text": "Điều kiện", "at": 0.48}]},
            {"label": "Rủi ro", "events": [{"text": "Giới hạn", "at": 0.32}]},
        ], "Một nhịp của agent"),
        _triptych("s8-risk", 18, "Quan sát. Đánh giá. Đứng ngoài khi chưa đủ.", "Agent Trading", ["Quan sát", "Đánh giá", "Đứng ngoài"]),
        _board_open("s9-close", 20, "Lịch sử giá. Không phải khuyến nghị.", "Agent Trading", "Lịch sử giá. Không phải khuyến nghị."),
    ]
    report["windows"] = {
        "up": {**{k: v for k, v in up.items() if k != "notes"}, "notes": up["notes"]},
        "pullback": pull,
        "down": {**{k: v for k, v in down.items() if k != "notes"}, "notes": down["notes"], "volumeSurgeAt": surge},
        "range": {"symbol": flat_name, **{k: v for k, v in flat.items() if k != "notes"}, "notes": flat["notes"]},
        "macd": {"start": macd_start, "last": last, "title": macd_title},
    }
    _check_sum(specs)
    return specs, report


def _board_open(scene_id: str, duration: float, dialogue: str, wordmark: str, line: str) -> dict:
    return {
        "id": scene_id,
        "duration": duration,
        "dialogue": dialogue,
        "goal": line,
        "template": "brand_reveal" if wordmark == "Agent Trading" else "designed_board",
        "onScreenText": [wordmark, line] if wordmark == "Agent Trading" else [line],
        "params": {"wordmark": wordmark, "line": line} if wordmark == "Agent Trading" else {
            "kind": "brand_center",
            "line": line,
            "logoAssetId": "logo-novixa",
            "logoScale": 0.42,
        },
        "assets": [] if wordmark == "Agent Trading" else ["logo-novixa"],
        "need": None,
    }


def _lanes(scene_id: str, duration: float, dialogue: str, title: str, lanes: list[dict], goal: str, data_label: str = "Sơ đồ, không phải lệnh") -> dict:
    labels = [title, data_label] + [item["label"] for item in lanes]
    return {
        "id": scene_id,
        "duration": duration,
        "dialogue": dialogue,
        "goal": goal,
        "template": "designed_board",
        "onScreenText": labels,
        "params": {"kind": "lanes", "title": title, "dataLabel": data_label, "lanes": lanes},
        "assets": [],
        "need": None,
    }


def _triptych(scene_id: str, duration: float, dialogue: str, note: str, titles: list[str]) -> dict:
    columns = [{"index": f"{index + 1:02d}", "title": title, "body": ""} for index, title in enumerate(titles)]
    return {
        "id": scene_id,
        "duration": duration,
        "dialogue": dialogue,
        "goal": note,
        "template": "designed_board",
        "onScreenText": [titles[0], note],
        "params": {"kind": "triptych", "note": note, "columns": columns},
        "assets": [],
        "need": None,
    }


def novixa_specs(rows: list[dict]) -> tuple[list[dict], dict]:
    plates = [
        ("s2-dash", 14, "Màn tổng quan cho thấy nhịp của nhà thuốc.", "Màn tổng quan nhà thuốc", "nvx-doc008-dashboard"),
        ("s3-pos", 14, "Bán hàng nằm trên màn hình quầy.", "Màn bán hàng tại quầy", "nvx-doc008-pos"),
        ("s5-catalog", 14, "Danh mục sản phẩm là danh sách trên phần mềm, chưa phải ảnh hộp thuốc.", "Danh mục trên phần mềm", "nvx-doc008-catalog"),
        ("s6-stock", 14, "Tồn kho và hạn dùng được nhìn trên màn tồn.", "Màn tồn kho", "nvx-doc008-stock"),
        ("s8-grn", 14, "Nhập hàng có một màn riêng.", "Màn nhập hàng", "nvx-doc008-grn"),
        ("s9-crm", 14, "Hồ sơ khách nằm trong phần mềm.", "Hồ sơ khách hàng", "nvx-doc008-crm"),
        ("s10-report", 14, "Báo cáo mở từ màn báo cáo.", "Màn báo cáo", "nvx-doc008-reports"),
        ("s11-nxt", 14, "Xuất nhập tồn là một báo cáo khác, không dùng lại màn tổng quan.", "Báo cáo xuất nhập tồn", "nvx-qld-nxt"),
        ("s12-app", 12, "Khách có một màn ứng dụng riêng.", "Ứng dụng khách hàng", "nvx-doc008-app"),
        ("s13-link", 12, "Liên thông dữ liệu thuốc là một màn cấu hình và theo dõi.", "Liên thông dữ liệu thuốc", "nvx-qld-connect"),
    ]
    specs = [_board_open("s1-open", 10, "Novixa giữ bán hàng, tồn kho và hạn dùng trên một nhịp.", "Novixa", "Novixa giữ bán hàng, tồn kho và hạn dùng trên một nhịp.")]
    specs.append(_plate_request(plates[0]))
    specs.append(_plate_request(plates[1]))
    specs.append({
        "id": "s4-gap",
        "duration": 10,
        "dialogue": "Chưa có ảnh hộp thuốc đã duyệt. Không lấy ảnh khác để lấp chỗ này.",
        "goal": "Báo thiếu ảnh hộp thuốc",
        "template": "kinetic_type",
        "onScreenText": ["Chưa có ảnh hộp thuốc", "Không lấy ảnh khác để lấp"],
        "params": {"lines": [
            {"text": "Chưa có ảnh hộp thuốc", "at": 0.2, "scale": 0.055, "bold": True},
            {"text": "Không lấy ảnh khác để lấp", "at": 0.7, "scale": 0.04, "bold": False},
        ]},
        "assets": [],
        "need": {"brand": "NOVIXA", "category": "medicine.packaging", "keywords": ["hộp thuốc", "vỉ thuốc"]},
    })
    specs.extend(_plate_request(item) for item in plates[2:4])
    specs.append(_lanes("s7-flow", 12, "Một nhịp: bán, trừ tồn, rồi nhắc hạn.", "Một nhịp tại quầy", [
        {"label": "Bán", "events": [{"text": "Ghi đơn", "at": 0.72}]},
        {"label": "Kho", "events": [{"text": "Trừ tồn", "at": 0.5}]},
        {"label": "Hạn", "events": [{"text": "Nhắc", "at": 0.3}]},
    ], "Sơ đồ một nhịp tại quầy", "Sơ đồ nghiệp vụ"))
    specs.extend(_plate_request(item) for item in plates[4:])
    specs.append(_triptych("s14-close", 12, "Bán đúng. Theo dõi hạn. Giao rõ.", "Novixa", ["Bán đúng", "Theo dõi hạn", "Giao rõ"]))
    _check_sum(specs)
    choices = []
    used: list[str] = []
    recent: list[str] = []
    hashes: list[int] = []
    for spec in specs:
        if spec["template"] != "annotated_plate":
            continue
        choice = choose_asset(rows, spec["need"], used, recent[-2:], hashes)
        choices.append({"sceneId": spec["id"], "status": choice["status"], "assetId": (choice.get("asset") or {}).get("assetId"), "reason": choice.get("reason")})
        if choice["status"] != "chosen":
            raise SystemExit(f"Thiếu ảnh cho {spec['id']}: {choice['reason']}")
        asset = choice["asset"]
        spec["assets"] = [asset["assetId"]]
        spec["params"]["assetId"] = asset["assetId"]
        used.append(asset["assetId"])
        recent.append(str(asset.get("similarityGroup") or asset["assetId"]))
        if asset.get("averageHash"):
            hashes.append(int(asset["averageHash"]))
    gap = choose_asset(rows, specs[3]["need"], used, recent[-2:], hashes)
    if gap["status"] == "chosen" and str((gap.get("asset") or {}).get("category") or "") != "medicine.packaging":
        raise SystemExit("Cảnh hộp thuốc bị lấp bằng ảnh không phải bao bì.")
    if gap["status"] == "chosen":
        gap = {
            "status": "available",
            "assetId": gap["asset"]["assetId"],
            "kept": "kinetic_type",
            "reason": "Video dài đã dựng giữ thẻ thiếu. Ảnh bao bì dùng ở bản proof ngắn.",
        }
    probes = plan_usage(rows, [
        {"sceneId": "probe-box", "need": specs[3]["need"]},
        {"sceneId": "probe-person", "need": {"brand": "NOVIXA", "category": "people.consult", "keywords": ["dược sĩ", "tư vấn"]}},
        {"sceneId": "probe-desk", "need": {"brand": "AgentTrading", "category": "environment.desk", "keywords": ["bàn giao dịch", "nhiều màn hình"]}},
    ])
    return specs, {"plates": choices, "gap": gap, "probes": probes}


def _plate_request(item: tuple) -> dict:
    scene_id, duration, dialogue, caption, token = item
    return {
        "id": scene_id,
        "duration": duration,
        "dialogue": dialogue,
        "goal": caption,
        "template": "annotated_plate",
        "onScreenText": [caption, "Số trên hình là dữ liệu demo."],
        "params": {
            "preset": "push_in" if scene_id.endswith(("dash", "stock", "report", "app")) else "pan",
            "amount": 0.06,
            "caption": caption,
            "note": "Số trên hình là dữ liệu demo.",
        },
        "assets": [],
        "need": {"brand": "NOVIXA", "keywords": [token], "category": ""},
    }


def _check_sum(specs: list[dict]) -> None:
    total = round(sum(item["duration"] for item in specs), 3)
    if abs(total - 180) > 0.001:
        raise SystemExit(f"Tổng cảnh = {total}, cần đúng ba phút.")


def write_project(project_id: str, brand: dict, specs: list[dict], rows: list[dict], palette: dict, objective: str) -> Path:
    project = ROOT / "proof" / "kit-video-production-v1" / "projects" / project_id
    project.mkdir(parents=True, exist_ok=True)
    script = _script(project_id, specs)
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
        scene["voiceLeadSeconds"] = 0.35
        scene["templateId"] = spec["template"]
        scene["params"] = spec["params"]
        scene["assets"] = spec["assets"]
        scene["onScreenText"] = spec["onScreenText"]
        scene["audio"]["voiceTrackId"] = "vo-" + spec["id"]
        scene["audio"]["intentionalSilences"] = []
        scene["audio"]["syncNotes"] = "Lời vào sau 0,35 giây."
        scene["transitionOut"] = {"type": "cut", "note": "Cắt khi câu đã rõ."}
        scene["previewFractions"] = [0.15, 0.55, 0.9]
        scene["acceptance"] = ["Chữ đúng câu đã khai.", "Không dùng ảnh khác nội dung."]
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
    manifest = {
        "schemaVersion": "kit-video-project/1",
        "projectId": project_id,
        "projectName": brand["name"],
        "brand": brand,
        "script": {"version": "v2", "approvalStatus": "approved", "sourcePath": "script.md", "notes": objective},
        "durationPolicy": {"mode": "target", "targetSeconds": 180, "notes": "Tổng các cảnh khóa ở ba phút."},
        "storyStructure": [{"beatId": spec["id"], "summary": spec["goal"]} for spec in specs],
        "voice": {
            "profileId": "vi-VN-NamMinhNeural",
            "language": "vi",
            "pronunciationNotes": "Không tăng tốc giọng.",
            "tracks": [
                {"trackId": "vo-" + spec["id"], "approval": "proposed", "text": spec["dialogue"], "path": f"assets/voice/{spec['id']}.wav", "pathBase": "project"}
                for spec in specs
            ],
        },
        "assets": assets,
        "visualStyle": {
            "profileId": brand["id"] + "-v2",
            "artDirection": {
                "audience": "người xem giải thích sản phẩm",
                "objective": objective,
                "language": "tiếng Việt",
                "imagery": "screenshot-and-type" if brand["id"] == "novixa" else "market-data",
                "motion": "camera-and-chart",
                "light": "warm-paper" if brand["id"] == "novixa" else "cool-paper",
                "material": "paper",
                "depth": "flat",
                "principles": ["exact-copy"],
            },
            "palette": palette,
            "typography": {"fontRegular": "C:/Windows/Fonts/segoeui.ttf", "fontBold": "C:/Windows/Fonts/segoeuib.ttf"},
            "safeArea": {"top": 0.06, "right": 0.06, "bottom": 0.06, "left": 0.06},
        },
        "audioProfile": {"ducking": True, "voiceGain": 1, "sfxGain": 0.35, "musicPolicy": "none", "silenceMinSeconds": 1.2},
        "format": {"aspectRatio": "16:9", "width": 1280, "height": 720, "frameRate": 24},
        "render": {"videoCodec": "libx264", "audioCodec": "aac", "crf": 18, "preset": "veryfast", "allowPlaceholders": False, "versioned": True, "outputRoot": "../../outputs"},
        "pipeline": [
            {"stage": "brief", "automation": "manual", "status": "approved", "notes": objective},
            {"stage": "script_analysis", "automation": "assisted", "status": "done", "notes": "propose_shot_plan từ script.md."},
            {"stage": "story_structure", "automation": "manual", "status": "approved", "notes": "Các cảnh khóa đủ ba phút."},
            {"stage": "shot_plan", "automation": "assisted", "status": "approved", "notes": "Ảnh chọn bằng library.choose_asset. Biểu đồ từ file nến."},
            {"stage": "asset_plan", "automation": "automated", "status": "done", "notes": "Thiếu ảnh thì ghi gap, không thế ảnh khác."},
            {"stage": "asset_creation", "automation": "manual", "status": "done", "notes": "Không tạo ảnh stock. Dùng ảnh tài liệu và nến công khai."},
            {"stage": "voiceover", "automation": "automated", "status": "done", "notes": "edge-tts, tốc độ +0%. Track còn proposed cho đến khi có người nghe."},
            {"stage": "animation", "automation": "automated", "status": "done", "notes": "Máy quay nhẹ, nến và chỉ báo tính từ dữ liệu."},
            {"stage": "audio_mix", "automation": "automated", "status": "not_started", "notes": ""},
            {"stage": "render", "automation": "automated", "status": "in_progress", "notes": ""},
            {"stage": "qa", "automation": "automated", "status": "not_started", "notes": ""},
            {"stage": "delivery", "automation": "manual", "status": "not_started", "notes": "Không phát hành."},
        ],
        "shotPlan": "shot-plan.json",
        "outputs": [],
    }
    (project / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print("project", project)
    return project


def _script(project_id: str, specs: list[dict]) -> str:
    lines = [f"# {project_id}", ""]
    for spec in specs:
        lines.extend([
            f"## segment {spec['id']}",
            f"goal: {spec['goal']}",
            "dialogue: |",
            f"  {spec['dialogue']}",
            "on_screen:",
            *[f"- {item}" for item in spec["onScreenText"]],
            "acceptance:",
            "- Chữ đúng câu đã khai.",
            f"duration_hint_seconds: {spec['duration']}",
            f"template_hint: {spec['template']}",
            "",
        ])
    return "\n".join(lines)


def mark_usage(rows: list[dict], project_id: str, specs: list[dict]) -> None:
    for spec in specs:
        for asset_id in spec["assets"]:
            for row in rows:
                if row["assetId"] == asset_id:
                    row["usage"].append({"video": project_id, "sceneId": spec["id"]})
    (LIB / "catalog.json").write_text(json.dumps({"assets": rows}, ensure_ascii=False, indent=2), encoding="utf-8")


def write_voices(project: Path) -> None:
    import subprocess

    manifest = json.loads((project / "manifest.json").read_text(encoding="utf-8"))
    shot = json.loads((project / "shot-plan.json").read_text(encoding="utf-8"))
    by_id = {scene["sceneId"]: scene for scene in shot["scenes"]}
    launcher = ROOT / "tools" / "kit-video-production" / "kv.py"
    for track in manifest["voice"]["tracks"]:
        dest = project / track["path"]
        if not dest.is_file():
            last = None
            for _attempt in range(4):
                try:
                    subprocess.run([sys.executable, str(launcher), "voice", "--text", track["text"], "--out", str(dest)], check=True)
                    last = None
                    break
                except subprocess.CalledProcessError as exc:
                    last = exc
                    dest.with_suffix(".mp3").unlink(missing_ok=True)
                    import time
                    time.sleep(2)
            if last is not None:
                raise last
        scene = by_id[track["trackId"].replace("vo-", "", 1)]
        lead = float(scene["voiceLeadSeconds"])
        with wave.open(str(dest)) as handle:
            seconds = handle.getnframes() / handle.getframerate()
        if seconds + lead > float(scene["durationSeconds"]) + 0.05:
            raise SystemExit(f"Lời {track['trackId']} dài {seconds:.2f}s, cảnh chỉ {scene['durationSeconds']}s.")
        boundary = dest.with_suffix(".boundaries.json")
        words = json.loads(boundary.read_text(encoding="utf-8")) if boundary.is_file() else []
        spoken = max((float(item["end"]) for item in words), default=seconds)
        first = min((float(item["start"]) for item in words), default=0.0)
        head_end = min(float(scene["durationSeconds"]), lead + first + 0.3)
        tail = lead + spoken
        silences = []
        if head_end > 0.05:
            silences.append({"start": 0, "end": round(head_end, 3), "reason": "Hình vào trước lời."})
        if scene["durationSeconds"] - tail > 0.2:
            silences.append({
                "start": round(max(0, tail - 0.25), 3),
                "end": scene["durationSeconds"],
                "reason": "Lời đã hết. Giữ hình đến hết cảnh, không có nhạc.",
            })
        scene["audio"]["intentionalSilences"] = silences
        print(track["trackId"], f"wav={seconds:.2f}", f"speech={spoken:.2f}")
    (project / "shot-plan.json").write_text(json.dumps(shot, ensure_ascii=False, indent=2), encoding="utf-8")


def main() -> None:
    command = sys.argv[1] if len(sys.argv) > 1 else "films"
    rows = build_catalog()
    if command == "catalog":
        return
    fetch_market()
    assert sma([1, 2, 3, 4, 5], 3)[2] == 2
    assert ohlcv_errors([{"t": 1, "o": 1, "h": 1, "l": 2, "c": 1, "v": 1}])
    novixa_palette = {
        "background": [247, 249, 247], "ink": [0, 32, 96], "accent": [22, 140, 136],
        "paper": [255, 255, 255], "muted": [90, 104, 112], "accentAlt": [176, 96, 42], "weight": [24, 40, 72],
    }
    trading_palette = {
        "background": [232, 236, 242], "ink": [16, 28, 48], "accent": [18, 122, 82],
        "paper": [255, 252, 246], "muted": [84, 94, 110], "accentAlt": [164, 72, 64], "weight": [28, 36, 52],
    }
    novixa, novixa_report = novixa_specs(rows)
    trading, trading_report = trading_specs()
    write_project("novixa-long", {"id": "novixa", "name": "Novixa — video dài"}, novixa, rows, novixa_palette, "Giải thích nhịp nhà thuốc bằng giao diện thật và nói rõ chỗ thiếu ảnh.")
    write_project("trading-long", {"id": "agent-trading", "name": "Agent Trading — video dài"}, trading, rows, trading_palette, "Đọc lịch sử giá công khai. Không phải khuyến nghị và không phải lợi nhuận chiến lược.")
    mark_usage(rows, "novixa-long", novixa)
    approved = [row for row in rows if row.get("approval") == "approved" and not row.get("derived") and not row.get("parentId")]
    by_brand: dict[str, int] = {}
    for row in approved:
        by_brand[row["brand"]] = by_brand.get(row["brand"], 0) + 1
    report = {
        "independentApproved": by_brand,
        "independentApprovedTotal": len(approved),
        "catalogCounts": count_independent(rows),
        "rejected": [row["assetId"] for row in rows if row.get("approval") == "rejected"],
        "derived": [row["assetId"] for row in rows if row.get("derived")],
        "novixa": novixa_report,
        "trading": trading_report,
        "target": {"NOVIXA": 80, "AgentTrading": 80, "Shared": 40},
        "gapReason": "Đã thêm vài ảnh Commons có giấy phép. Vẫn thiếu hộp bán lẻ, kho, dược sĩ tại quầy và bàn giao dịch. Không mua asset và không gọi API ảnh trả phí.",
    }
    (LIB / "selection-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("approved", by_brand)
    if command == "voices":
        write_voices(ROOT / "proof" / "kit-video-production-v1" / "projects" / "novixa-long")
        write_voices(ROOT / "proof" / "kit-video-production-v1" / "projects" / "trading-long")


if __name__ == "__main__":
    main()

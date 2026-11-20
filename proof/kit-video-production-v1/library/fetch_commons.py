"""Download a shortlist of Commons files at video size. Review before cataloging."""

from __future__ import annotations

import json
import urllib.parse
import urllib.request
from pathlib import Path

UA = "KitPlatformVideoLibrary/1.0 (local catalog; educational explainer)"
ROOT = Path(__file__).resolve().parent / "incoming"
FILES = [
    "Pills in blister pack.jpg",
    "Blister of tablets.jpg",
    "Person holds a blister pack of pills while sitting at a table.jpg",
    "Pills and medicines 02.jpg",
    "Brest Greenberg Pharmacy Interior 2024-09-20 3798.jpg",
    "Pharmacy counter six, plus Influx of volunteers help keep Rader pharmacy at full speed 150831-A-DZ999-002.jpg",
    "Nellis satellite pharmacy 121023-F-TT327-099.jpg",
    "HK 屯門醫院 Tuen Mun Hospital ground floor Pharmacy service counter window n waiting room plastic seats July 2016 DSC.jpg",
]


def meta(title: str) -> dict:
    params = {
        "action": "query",
        "titles": "File:" + title,
        "prop": "imageinfo",
        "iiprop": "url|extmetadata",
        "format": "json",
    }
    url = "https://commons.wikimedia.org/w/api.php?" + urllib.parse.urlencode(params)
    request = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(request, timeout=40) as response:
        payload = json.loads(response.read().decode("utf-8"))
    page = next(iter((payload.get("query") or {}).get("pages", {}).values()))
    info = (page.get("imageinfo") or [{}])[0]
    extra = info.get("extmetadata") or {}
    return {
        "license": (extra.get("LicenseShortName") or {}).get("value", ""),
        "artist": (extra.get("Artist") or {}).get("value", ""),
        "credit": (extra.get("Credit") or {}).get("value", ""),
        "page": info.get("descriptionurl") or "",
    }


def download(title: str) -> None:
    safe = "".join(ch if ch.isalnum() or ch in "-_" else "-" for ch in title)[:80]
    dest = ROOT / (safe + ".jpg")
    if not dest.is_file():
        url = "https://commons.wikimedia.org/wiki/Special:FilePath/" + urllib.parse.quote(title) + "?width=1920"
        request = urllib.request.Request(url, headers={"User-Agent": UA})
        with urllib.request.urlopen(request, timeout=60) as response:
            dest.write_bytes(response.read())
    info = meta(title)
    (dest.with_suffix(".json")).write_text(json.dumps({"file": title, **info}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(dest.name, info["license"], dest.stat().st_size)


def main() -> None:
    ROOT.mkdir(parents=True, exist_ok=True)
    for title in FILES:
        try:
            download(title)
        except Exception as exc:
            print("FAIL", title, exc)


if __name__ == "__main__":
    main()

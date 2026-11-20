"""List Wikimedia Commons candidates. Prints license and size. Does not download."""

from __future__ import annotations

import json
import urllib.parse
import urllib.request

UA = "KitPlatformVideoLibrary/1.0 (local catalog; educational explainer)"
QUERIES = [
    "blister pack tablets",
    "pills blister",
    "pharmacy counter",
    "pharmacist consultation",
    "multiple monitors workstation",
    "office desk computer screens",
    "medicine packaging box",
]


def main() -> None:
    for query in QUERIES:
        params = {
            "action": "query",
            "generator": "search",
            "gsrsearch": "filetype:bitmap " + query,
            "gsrnamespace": "6",
            "gsrlimit": "8",
            "prop": "imageinfo",
            "iiprop": "url|size|mime|extmetadata",
            "format": "json",
        }
        url = "https://commons.wikimedia.org/w/api.php?" + urllib.parse.urlencode(params)
        request = urllib.request.Request(url, headers={"User-Agent": UA})
        with urllib.request.urlopen(request, timeout=40) as response:
            payload = json.loads(response.read().decode("utf-8"))
        pages = (payload.get("query") or {}).get("pages") or {}
        print("---", query)
        for page in pages.values():
            info = (page.get("imageinfo") or [{}])[0]
            meta = info.get("extmetadata") or {}
            license_name = (meta.get("LicenseShortName") or {}).get("value", "")
            mime = info.get("mime") or ""
            width = info.get("width") or 0
            if "pdf" in mime or "tiff" in mime or width < 1400:
                continue
            print(f"{license_name:18} {width}x{info.get('height')} {page.get('title')}")


if __name__ == "__main__":
    main()

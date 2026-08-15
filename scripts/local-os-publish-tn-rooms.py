"""One-off: curate TNUE + TNUS room lists into pack_local.listing (no prices)."""
from __future__ import annotations

import csv
import io
import re
import urllib.request
from pathlib import Path

TNUE_SRC = "b1111111-1111-1111-1111-111111111203"
TNUS_SRC = "b1111111-1111-1111-1111-111111111204"
TNUE_URL = "https://tuyensinh.tnue.edu.vn/thong-tin-cac-phong-tro-khu-vuc-xung-quanh-truong-dai-hoc-su-pham-dhtn"
TNUS_URL = "https://docs.google.com/spreadsheets/d/1zAv9IswrXGwDB4iKu7icj38SjpwPIwjH/edit"
TNUE_FILE = Path(
    r"C:\Users\Admin\.cursor\projects\e-KitPlatform\agent-tools\2ff13838-c148-4b6f-b715-70eac2b5784f.txt"
)
OUT = Path(r"E:\KitPlatform\migrations\302_pack_local_os_publish_tn_rooms.sql")

PRICE_RE = re.compile(
    r"(\d+[.,]?\d*\s*(triệu|trieu|tr\/|nghìn|nghin|\bk\b)|"
    r"\d+\s*tr\d*|"
    r"\d{2,3}\s*\.\s*\d{3}\s*(vnd|đ|d)?"
    r"|giá[^.]{0,40})",
    re.I,
)


def norm_phone(raw: str) -> str | None:
    digits = re.sub(r"\D", "", raw or "")
    if digits.startswith("84") and len(digits) >= 11:
        digits = "0" + digits[2:]
    if len(digits) == 9 and digits[0] in "35789":
        digits = "0" + digits
    if len(digits) == 10 and digits.startswith("0"):
        return digits
    if len(digits) == 11 and digits.startswith("0"):
        return digits
    return None


def clean(s: str) -> str:
    s = re.sub(r"\s+", " ", (s or "")).strip()
    return s


def strip_price(s: str) -> str:
    s = PRICE_RE.sub("", s)
    return clean(re.sub(r"\s[,;/]\s", " · ", s))


def sql_str(s: str | None) -> str:
    if not s:
        return "NULL"
    return "'" + s.replace("'", "''") + "'"


def parse_tnue(text: str) -> list[dict]:
    rows = []
    for line in text.splitlines():
        if line.count("|") < 7:
            continue
        if "Họ và tên" in line or "----" in line:
            continue
        parts = [clean(p) for p in line.strip().strip("|").split("|")]
        if len(parts) < 6:
            continue
        name, phone, addr = parts[0], parts[1], parts[2]
        amen = parts[5] if len(parts) > 5 else ""
        security = parts[6] if len(parts) > 6 else ""
        dist = parts[7] if len(parts) > 7 else ""
        p = norm_phone(phone)
        if not p or len(name) < 2:
            continue
        bits = [strip_price(x) for x in (amen, security, dist) if strip_price(x)]
        summary = " · ".join(bits)
        if summary:
            summary += ". "
        summary += "Giá: liên hệ chủ nhà (đổi theo thời điểm). Nguồn: tuyển sinh ĐH Sư phạm."
        rows.append(
            {
                "name": name[:80],
                "phone": p,
                "place": addr[:160] or "Gần ĐH Sư phạm, Thái Nguyên",
                "summary": summary[:1800],
                "title": f"Trọ gần ĐH Sư phạm — {addr[:48] or name}",
                "source_id": TNUE_SRC,
                "source_url": f"{TNUE_URL}#p={p}",
            }
        )
    return rows


def parse_tnus() -> list[dict]:
    url = (
        "https://docs.google.com/spreadsheets/d/1zAv9IswrXGwDB4iKu7icj38SjpwPIwjH/"
        "gviz/tq?tqx=out:csv&gid=1179678852"
    )
    req = urllib.request.Request(url, headers={"User-Agent": "ThaiNguyenLife/1.0"})
    raw = urllib.request.urlopen(req, timeout=20).read()
    text = raw.decode("utf-8-sig", errors="replace")
    reader = csv.reader(io.StringIO(text))
    header = next(reader, None)
    rows = []
    for rec in reader:
        if len(rec) < 4:
            continue
        name, addr, phone, rooms = rec[1], rec[2], rec[3], rec[4] if len(rec) > 4 else ""
        status = rec[5] if len(rec) > 5 else ""
        p = norm_phone(phone)
        if not p:
            continue
        name = clean(name)
        addr = clean(addr)
        extra = []
        if clean(rooms) and not PRICE_RE.search(rooms):
            extra.append(f"Khoảng {clean(rooms)} phòng")
        st = strip_price(status)
        if st:
            extra.append(st)
        summary = " · ".join(extra)
        if summary:
            summary += ". "
        summary += "Giá: liên hệ chủ nhà (đổi theo thời điểm). Nguồn: danh sách trọ gần TNUS."
        rows.append(
            {
                "name": (name or "Chủ trọ")[:80],
                "phone": p,
                "place": (addr or "Gần ĐH Khoa học (TNUS), Thái Nguyên")[:160],
                "summary": summary[:1800],
                "title": f"Trọ gần ĐH Khoa học — {(addr or name)[:48]}",
                "source_id": TNUS_SRC,
                "source_url": f"{TNUS_URL}#p={p}",
            }
        )
    return rows


def dedup(items: list[dict]) -> list[dict]:
    by_phone: dict[str, dict] = {}
    for row in items:
        prev = by_phone.get(row["phone"])
        if prev is None:
            by_phone[row["phone"]] = row
            continue
        # Prefer longer address; keep TNUE if tie and incoming is TNUE
        if len(row["place"]) > len(prev["place"]) + 8:
            by_phone[row["phone"]] = row
        elif row["source_id"] == TNUE_SRC and prev["source_id"] != TNUE_SRC:
            if len(row["place"]) >= len(prev["place"]) - 8:
                by_phone[row["phone"]] = row
    return list(by_phone.values())


def main() -> None:
    tnue = parse_tnue(TNUE_FILE.read_text(encoding="utf-8", errors="replace"))
    tnus = parse_tnus()
    rooms = dedup(tnue + tnus)
    rooms.sort(key=lambda r: (r["source_id"], r["place"]))
    lines = [
        "-- Local OS: curated rooms from TNUE page + TNUS sheet. No prices. Isolated park.",
        "DELETE FROM pack_local.listing",
        f"WHERE source_id IN ('{TNUE_SRC}', '{TNUS_SRC}');",
        "",
    ]
    for r in rooms:
        lines.append(
            "INSERT INTO pack_local.listing ("
            "kind, title, summary, organization_name, place_text, audience, city_code, "
            "source_kind, source_url, source_id, contact_phone, contact_name, "
            "price_month, trust, safety_flag, status, published_at, last_checked_at, expires_at"
            ") VALUES ("
            f"'room', {sql_str(r['title'][:140])}, {sql_str(r['summary'])}, "
            f"{sql_str(r['name'])}, {sql_str(r['place'])}, "
            "ARRAY['student']::text[], 'thai_nguyen', "
            f"'official_web', {sql_str(r['source_url'])}, '{r['source_id']}', "
            f"{sql_str(r['phone'])}, {sql_str(r['name'])}, "
            "NULL, 'SOURCE_TRUSTED', FALSE, 'ACTIVE', NOW(), NOW(), NOW() + INTERVAL '90 days'"
            ");"
        )
    lines.append(f"-- rooms={len(rooms)} tnue_raw={len(tnue)} tnus_raw={len(tnus)}")
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"wrote {OUT} rooms={len(rooms)} tnue={len(tnue)} tnus={len(tnus)}")


if __name__ == "__main__":
    main()

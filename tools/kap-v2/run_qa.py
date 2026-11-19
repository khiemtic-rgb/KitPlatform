# -*- coding: utf-8 -*-
"""Chạy 5 vai KAP V2 trên API dev. Không gọi capture-lead. Không in session token."""
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

API = "http://localhost:5290/api/public/assessment"
ROOT = Path(__file__).resolve().parent

EXPECT = {
    "Vai 1": {"first_prefix": "Hàng hết hạn:", "second": "Hàng cận hạn được phát hiện muộn"},
    "Vai 2": {"first": "Khách dùng thuốc lâu dài chưa được nhắc", "second": "Chưa tra được lịch sử mua của khách quen"},
    "Vai 3": {
        "first_prefix": "Hàng hết hạn:",
        "second_any": [
            "Nhà thuốc phụ thuộc vào chủ",
            "Nhân viên mới lâu mới tự đứng quầy",
            "Mỗi nhân viên tư vấn một kiểu",
            "Tiền cuối ngày hay lệch",
        ],
    },
    "Vai 4": {"first": "Chưa biết nhóm hàng nào lãi nhất", "second": "Chưa có cách tìm chỗ bán chậm"},
    "Vai 5": {"first": "Việc định kỳ chưa được ghi đều", "second": None, "insufficient": ["GROWTH"]},
}


def call(method, path, body=None, token=None):
    data = None if body is None else json.dumps(body).encode("utf-8")
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("X-Assessment-Session", token)
    try:
        with urllib.request.urlopen(req, timeout=60) as res:
            return json.loads(res.read().decode("utf-8"))
    except urllib.error.HTTPError as ex:
        detail = ex.read().decode("utf-8", errors="replace")[:400]
        raise SystemExit(f"{method} {path} -> {ex.code} {detail}") from ex


def title_ok(title, spec, key):
    if key == "second" and spec.get("second") is None and "second_any" not in spec:
        return title is None
    if key == "first" and "first" in spec:
        return title == spec["first"]
    if key == "first" and "first_prefix" in spec:
        return bool(title) and title.startswith(spec["first_prefix"])
    if key == "second" and "second" in spec:
        return title == spec["second"]
    if key == "second" and "second_any" in spec:
        return title in spec["second_any"]
    return False


def main():
    personas = json.loads((ROOT / "qa_personas.json").read_text(encoding="utf-8"))
    template = call("GET", "/templates/PHARMACY_V2")
    questions = {}
    for cat in template["categories"]:
        for dim in cat["dimensions"]:
            for q in dim["questions"]:
                questions[q["code"]] = q

    failed = False
    for name, answers in personas.items():
        created = call("POST", "/submissions", {
            "templateCode": "PHARMACY_V2",
            "templateVersion": "2.0",
            "source": "public_web",
            "locale": "vi-VN",
            "utmSource": "internal_test",
        })
        token = created["sessionToken"]
        sid = created["id"]
        responses = []
        for code, option in answers.items():
            q = questions.get(code)
            if not q:
                raise SystemExit(f"{name}: không có câu {code}")
            opt = next((o for o in q["options"] if o["code"] == option), None)
            if not opt:
                raise SystemExit(f"{name}: câu {code} không có phương án {option}")
            responses.append({"questionId": q["id"], "optionId": opt["id"]})
        call("PUT", f"/submissions/{sid}/responses", {"responses": responses}, token)
        done = call("POST", f"/submissions/{sid}/complete", {}, token)
        insights = done.get("previewInsights") or []
        titles = [i.get("title") for i in insights]
        first = titles[0] if titles else None
        second = titles[1] if len(titles) > 1 else None
        spec = EXPECT[name]
        ok = title_ok(first, spec, "first") and title_ok(second, spec, "second")
        print(f"{name} {'OK' if ok else 'LỆCH'} | {first} || {second}")
        for cat in done.get("categoryScores") or []:
            flag = "CHƯA ĐỦ" if cat.get("insufficientData") else f"{cat.get('scorePct')}/100"
            print(f"  {cat.get('code')}: {flag}")
        for code in spec.get("insufficient") or []:
            row = next((c for c in done.get("categoryScores") or [] if c.get("code") == code), None)
            if not row or not row.get("insufficientData"):
                ok = False
                print(f"  thiếu nhãn Chưa đủ dữ liệu cho {code}")
        if not ok:
            failed = True
        print(f"  submission {sid}")
    if failed:
        sys.exit(1)


if __name__ == "__main__":
    main()

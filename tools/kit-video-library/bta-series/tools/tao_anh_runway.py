"""Tạo ảnh AI tập 01 bằng Runway API (gen4_image), theo lô đã duyệt. Chạy trên máy chủ kênh.

    python tools\\kit-video-library\\bta-series\\tools\\tao_anh_runway.py --lo A           (chỉ xem kế hoạch + chi phí, không gửi)
    python tools\\kit-video-library\\bta-series\\tools\\tao_anh_runway.py --lo A --chay    (gửi thật, tốn credit)

- Khóa đọc từ biến môi trường RUNWAY_API_KEY (cùng biến Famixa dùng). Không in, không ghi khóa ra file.
- Câu lệnh lấy từ docs/marketing/bta/hinh-ai/01/lenh-tao-anh.json (khối phong cách A/B + từng ảnh).
- Ảnh đã có thì bỏ qua (không tạo lại, không tốn tiền). Muốn tạo lại: chuyển file cũ vào _loai/.
- Mỗi lần gửi ghi một dòng vào hinh-ai/01/chi-phi.jsonl (thời điểm, file, task id, credit ước tính, kết quả).
- Trần chi phí mặc định 100 credit cho một lần chạy (--tran để đổi).
"""
from __future__ import annotations

import argparse, json, os, sys, time, urllib.request, urllib.error
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]  # E:/KitPlatform
FOLDER = ROOT / "docs/marketing/bta/hinh-ai/01"
API = "https://api.dev.runwayml.com/v1"
VERSION = "2024-11-06"
CREDIT = {"720p": 5, "1080p": 8}  # gen4_image, theo bảng giá API Runway (credit API, 1 credit = 0,01 USD)


def call(method: str, url: str, key: str, body: dict | None = None) -> dict:
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method, headers={
        "Authorization": f"Bearer {key}", "X-Runway-Version": VERSION, "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return json.loads(r.read().decode() or "{}")
    except urllib.error.HTTPError as e:
        raise RuntimeError(f"HTTP {e.code}: {e.read().decode(errors='replace')[:400]}") from None


def log(row: dict) -> None:
    with open(FOLDER / "chi-phi.jsonl", "a", encoding="utf-8") as f:
        f.write(json.dumps(row, ensure_ascii=False) + "\n")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--lo", required=True, help="A (ảnh như thật) hoặc B (tranh Đông Hồ) hoặc tên file cụ thể")
    ap.add_argument("--chay", action="store_true", help="gửi thật (tốn credit)")
    ap.add_argument("--tran", type=int, default=100, help="trần credit cho lần chạy này")
    ap.add_argument("--do-phan-giai", default="1080p", choices=["720p", "1080p"])
    a = ap.parse_args()

    plan = json.loads((FOLDER / "lenh-tao-anh.json").read_text(encoding="utf-8"))
    ratio = "1920:1080" if a.do_phan_giai == "1080p" else "1280:720"
    todo = [x for x in plan["anh"] if x["lo"] == a.lo or x["file"] == a.lo]
    todo = [x for x in todo if not (FOLDER / x["file"]).exists()]
    cost = sum(CREDIT[a.do_phan_giai] * x.get("so_ban", 1) for x in todo)
    print(f"Lô {a.lo}: {len(todo)} ảnh cần tạo, ước tính {cost} credit API ({ratio}).")
    for x in todo:
        print(f"  - {x['file']}  x{x.get('so_ban', 1)}")
    if not todo:
        return 0
    if cost > a.tran:
        print(f"Vượt trần {a.tran} credit. Dừng."); return 2
    if not a.chay:
        print("Chưa gửi. Thêm --chay để tạo thật."); return 0
    key = os.environ.get("RUNWAY_API_KEY", "").strip()
    if not key:
        print("Thiếu biến môi trường RUNWAY_API_KEY. Không gửi."); return 3

    for x in todo:
        prompt = (plan["phong_cach"][x["kieu"]] + " " + x["lenh"]).strip()[:1000]
        for k in range(x.get("so_ban", 1)):
            name = x["file"] if k == 0 else x["file"].replace(".png", f".{chr(98 + k - 1)}.png")
            body = {"model": "gen4_image", "promptText": prompt, "ratio": ratio}
            if "seed" in x:
                body["seed"] = int(x["seed"]) + k
            row = {"luc": datetime.now().isoformat(timespec="seconds"), "file": name, "model": "gen4_image",
                   "ratio": ratio, "creditUocTinh": CREDIT[a.do_phan_giai]}
            try:
                task = call("POST", f"{API}/text_to_image", key, body)
                tid = task.get("id", ""); row["taskId"] = tid
                print(f"{name}: đã gửi, task {tid}")
                for _ in range(120):
                    time.sleep(5)
                    st = call("GET", f"{API}/tasks/{tid}", key)
                    if st.get("status") in ("SUCCEEDED", "FAILED", "CANCELLED"):
                        break
                row["ketQua"] = st.get("status")
                if st.get("status") != "SUCCEEDED":
                    row["loi"] = f"{st.get('failureCode', '')} {st.get('failure', '')}".strip()
                    print(f"{name}: {row['ketQua']} {row.get('loi', '')}")
                else:
                    url = (st.get("output") or [""])[0]
                    with urllib.request.urlopen(url, timeout=120) as r:
                        (FOLDER / name).write_bytes(r.read())
                    print(f"{name}: đã lưu")
            except Exception as e:  # ghi lỗi, chạy tiếp ảnh sau
                row["ketQua"] = "LOI"; row["loi"] = str(e)[:400]
                print(f"{name}: lỗi {row['loi']}")
            log(row)
    print("Xong. Gửi lại cho Claude để soát ảnh trước khi dùng.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

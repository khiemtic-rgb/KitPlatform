"""Dựng riêng PHẦN ĐẦU 3D từ ảnh chân dung (mặt nét gấp nhiều lần ảnh toàn thân) qua API Fal,
để ghép lên thân đã có xương (ghep_dau.py). Không cần Blender cho bước này.

  set FAL_KEY=...   (hoặc "fal_key_file" trong data/cau_hinh.json, file NGOÀI thư mục mã)
  sgd dau_3d Bo [--nguon meshy|rodin|ca_hai]      (mặc định ca_hai: thử cả hai để chọn bản giống ảnh gốc hơn)

Đầu vào: data/mo_hinh/anh_vao/<Ma>_chan_dung.png (ô chính diện của <Ma>_mat.png, vuông 1024, nền xám)
Kết quả: data/mo_hinh/<Ma>_dau_<nguon>/... (.glb + ảnh da), <Ma>_dau_<nguon>_fal.json
Chi phí: Meshy 6 image-to-3d 0,8 USD; Rodin Gen-2.5 0,4 USD (một lần mỗi nhân vật).
"""
import base64
import json
import os
import sys
import time
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "data")
OUT = os.path.join(DATA, "mo_hinh")

ENDPOINTS = {
    "meshy": "fal-ai/meshy/v6/image-to-3d",
    "rodin": "fal-ai/hyper3d/rodin/v2.5",
}


def payload(nguon, uri):
    if nguon == "meshy":
        return {"image_url": uri, "should_texture": True, "enable_pbr": False, "topology": "triangle",
                "target_polycount": 120000, "should_remesh": True, "symmetry_mode": "auto",
                "texture_image_url": uri, "enable_safety_checker": True}
    return {"image_urls": [uri], "tier": "Gen-2.5-High", "geometry_file_format": "glb", "material": "Shaded",
            "quality_mesh_option": "150K Triangle", "texture_delight": True, "hd_texture": True,
            "enable_safety_checker": True}


def load_cfg():
    with open(os.path.join(DATA, "cau_hinh.json"), encoding="utf-8") as f:
        return json.load(f)


def fal_key(cfg):
    key = os.environ.get("FAL_KEY")
    if not key and cfg.get("fal_key_file") and os.path.exists(cfg["fal_key_file"]):
        with open(cfg["fal_key_file"], encoding="utf-8") as f:
            key = f.read().strip()
    if not key:
        raise SystemExit("Thiếu khoá Fal: đặt FAL_KEY hoặc \"fal_key_file\" trong data/cau_hinh.json (file ngoài thư mục mã).")
    return key


def http(method, url, key, body=None, timeout=120):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Authorization", f"Key {key}")
    req.add_header("Accept", "application/json")
    if data is not None:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        raise SystemExit(f"Fal trả lỗi {e.code} ở {url}: {e.read().decode(errors='replace')[:800]}")


def call_fal(endpoint, body, key, wait_s=1500):
    sub = http("POST", f"https://queue.fal.run/{endpoint}", key, body)
    print("SGD_FAL gửi", endpoint, "mã", sub.get("request_id"), flush=True)
    t0, last = time.time(), ""
    while time.time() - t0 < wait_s:
        st = http("GET", sub["status_url"], key)
        s = st.get("status", "")
        if s != last:
            print("SGD_FAL trạng thái", s, f"{time.time() - t0:.0f}s", flush=True)
            last = s
        if s == "COMPLETED":
            return http("GET", sub["response_url"], key, timeout=300), sub.get("request_id")
        if s in ("FAILED", "ERROR", "CANCELLED"):
            raise SystemExit(f"Fal báo {s}: {json.dumps(st)[:800]}")
        time.sleep(5)
    raise SystemExit(f"Quá {wait_s}s chưa xong; mã {sub.get('request_id')}")


def files_in(obj, path=""):
    out = []
    if isinstance(obj, dict):
        if isinstance(obj.get("url"), str) and obj["url"].startswith("http"):
            out.append((path, obj["url"], obj.get("file_name") or os.path.basename(obj["url"].split("?")[0])))
        for k, v in obj.items():
            out += files_in(v, f"{path}.{k}" if path else k)
    elif isinstance(obj, list):
        for i, v in enumerate(obj):
            out += files_in(v, f"{path}[{i}]")
    return out


argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
nguon = argv[argv.index("--nguon") + 1] if "--nguon" in argv else "ca_hai"
codes = [a for a in argv if not a.startswith("--") and a not in ENDPOINTS and a != "ca_hai"]
if not codes:
    raise SystemExit("Thiếu mã nhân vật. Ví dụ: sgd dau_3d Bo")
key = fal_key(load_cfg())
for code in codes:
    src = os.path.join(OUT, "anh_vao", f"{code}_chan_dung.png")
    if not os.path.exists(src):
        raise SystemExit(f"Chưa có ảnh chân dung {src}")
    with open(src, "rb") as f:
        uri = "data:image/png;base64," + base64.b64encode(f.read()).decode()
    for n in (["meshy", "rodin"] if nguon == "ca_hai" else [nguon]):
        d = os.path.join(OUT, f"{code}_dau_{n}")
        os.makedirs(d, exist_ok=True)
        res, rid = call_fal(ENDPOINTS[n], payload(n, uri), key)
        with open(os.path.join(OUT, f"{code}_dau_{n}_fal.json"), "w", encoding="utf-8") as f:
            json.dump({"endpoint": ENDPOINTS[n], "request_id": rid, "ket_qua": res}, f, ensure_ascii=False, indent=1)
        seen = set()
        for k, url, name in files_in(res):
            name = name if name not in seen else f"{len(seen)}_{name}"
            seen.add(name)
            with urllib.request.urlopen(url, timeout=300) as r, open(os.path.join(d, name), "wb") as fo:
                fo.write(r.read())
            print("SGD_DAU tải", n, k, "->", os.path.join(d, name), flush=True)
        print("SGD_DAU_XONG", code, n, d, flush=True)

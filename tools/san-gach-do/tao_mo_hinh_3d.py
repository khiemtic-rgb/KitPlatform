"""Dựng mô hình 3D có xương cho nhân vật từ ảnh góc <Ma>_goc.png, qua API Fal.

  set FAL_KEY=...            (hoặc ghi khoá vào file nêu ở data/cau_hinh.json -> "fal_key_file", NGOÀI thư mục mã)
  blender --background --python tools/san-gach-do/tao_mo_hinh_3d.py -- Bo
  tuỳ chọn: --nguon meshy|tripo   (mặc định meshy: dựng + gắn xương trong 1 lần, ~1,4 USD)
            --chi-cat            chỉ cắt ảnh góc, không gọi Fal (xem trước ảnh gửi đi)
            --lam-lai            gọi Fal lại dù đã có mô hình (mặc định: có rồi thì chỉ kiểm tra, không tốn tiền)

Các bước:
 1. Cắt ảnh góc (5 hình: chính diện, 3/4, nghiêng, 3/4 bên kia, phía sau) thành ảnh vuông 1024 px
    -> data/mo_hinh/anh_vao/<Ma>_<goc>.png
 2. Gọi Fal (hàng đợi), chờ xong, lưu kết quả -> data/mo_hinh/<Ma>/ (glb/fbx...) + <Ma>_fal.json
 3. Nạp mô hình vào Blender, kiểm tra xương đổi được về khớp chuẩn (hông, đầu, tay, chân)
 4. Đạt thì ghi "mo_hinh_3d" vào data/nhan_vat.json và render ảnh xem trước (đứng, đi)
    -> data/mo_hinh/<Ma>_xem_truoc_khung_*.png ; báo cáo -> data/mo_hinh/<Ma>_ket_qua.json
"""
import base64
import json
import os
import sys
import time
import urllib.error
import urllib.request

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from sgd_core import body, rig, shot  # noqa: E402

DATA = os.path.join(HERE, "data")
OUT = os.path.join(DATA, "mo_hinh")

ENDPOINTS = {
    # Meshy 6: 1–4 ảnh nhiều góc, dựng tư thế chữ T, tự gắn xương người
    "meshy": "fal-ai/meshy/v6/multi-image-to-3d",
    # Tripo H3.1: 2–4 ảnh theo thứ tự [chính diện, trái, sau, phải]; chưa gắn xương
    "tripo": "tripo3d/h3.1/multiview-to-3d",
}
VIEWS = {"chinh_dien": 0, "ba_phan_tu": 1, "nghieng_trai": 2, "phia_sau": 4}


# ---------------------------------------------------------------- 1. cắt ảnh góc
def segments(a):
    H, W = a.shape[:2]
    corners = np.concatenate([a[:20, :20].reshape(-1, 3), a[:20, -20:].reshape(-1, 3),
                              a[-20:, :20].reshape(-1, 3), a[-20:, -20:].reshape(-1, 3)])
    bg = np.median(corners, axis=0)
    diff = np.abs(a.astype(int) - bg).max(-1) > 28
    on = diff.mean(0) > 0.004
    segs, s = [], None
    for x, v in enumerate(on):
        if v and s is None:
            s = x
        if not v and s is not None:
            if x - s > 15:
                segs.append((s, x))
            s = None
    if s is not None:
        segs.append((s, W))
    merged = []
    for s0, s1 in segs:
        if merged and s0 - merged[-1][1] < 12:
            merged[-1] = (merged[-1][0], s1)
        else:
            merged.append((s0, s1))
    rows = np.nonzero(diff.mean(1) > 0.002)[0]
    return bg, merged, (int(rows.min()), int(rows.max()))


def save_rgb(arr, path, size=1024):
    h, w = arr.shape[:2]
    im = bpy.data.images.new(os.path.basename(path), w, h, alpha=False)
    rgba = np.concatenate([np.flipud(arr) / 255.0, np.ones((h, w, 1))], axis=2).astype(np.float32)
    im.pixels.foreach_set(rgba.ravel())
    im.scale(size, size)
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


def crop_views(code, sheet):
    im = bpy.data.images.load(sheet)
    a = rig.sheet_rgb(im).astype(np.uint8)
    bg, segs, (r0, r1) = segments(a)
    if len(segs) != 5:
        raise SystemExit(f"{sheet}: tìm thấy {len(segs)} hình, cần đúng 5 (chính diện, 3/4, nghiêng, 3/4, sau)")
    os.makedirs(os.path.join(OUT, "anh_vao"), exist_ok=True)
    paths = {}
    H, W = a.shape[:2]
    for name, idx in VIEWS.items():
        x0, x1 = segs[idx]
        y0, y1 = max(0, r0 - 24), min(H, r1 + 24)
        x0, x1 = max(0, x0 - 24), min(W, x1 + 24)
        piece = a[y0:y1, x0:x1]
        side = max(piece.shape[:2])
        canvas = np.empty((side, side, 3), dtype=np.uint8)
        canvas[:] = bg.astype(np.uint8)
        oy, ox = (side - piece.shape[0]) // 2, (side - piece.shape[1]) // 2
        canvas[oy:oy + piece.shape[0], ox:ox + piece.shape[1]] = piece
        p = os.path.join(OUT, "anh_vao", f"{code}_{name}.png")
        save_rgb(canvas.astype(float), p)
        paths[name] = p
    return paths


def data_uri(path):
    with open(path, "rb") as f:
        return "data:image/png;base64," + base64.b64encode(f.read()).decode()


# ---------------------------------------------------------------- 2. gọi Fal
def fal_key(cfg):
    key = os.environ.get("FAL_KEY")
    if not key and cfg.get("fal_key_file") and os.path.exists(cfg["fal_key_file"]):
        with open(cfg["fal_key_file"], encoding="utf-8") as f:
            key = f.read().strip()
    if not key:
        raise SystemExit("Thiếu khoá Fal: đặt biến môi trường FAL_KEY, hoặc ghi khoá vào file nêu ở "
                         "data/cau_hinh.json -> \"fal_key_file\" (để NGOÀI thư mục mã).")
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


def call_fal(endpoint, payload, key, wait_s=1200):
    sub = http("POST", f"https://queue.fal.run/{endpoint}", key, payload)
    print("SGD_FAL gửi", endpoint, "mã", sub.get("request_id"))
    status_url, response_url = sub["status_url"], sub["response_url"]
    t0 = time.time()
    last = ""
    while time.time() - t0 < wait_s:
        st = http("GET", status_url, key)
        s = st.get("status", "")
        if s != last:
            print("SGD_FAL trạng thái", s, f"{time.time() - t0:.0f}s")
            last = s
        if s == "COMPLETED":
            return http("GET", response_url, key, timeout=300), sub.get("request_id")
        if s in ("FAILED", "ERROR", "CANCELLED"):
            raise SystemExit(f"Fal báo {s}: {json.dumps(st)[:800]}")
        time.sleep(5)
    raise SystemExit(f"Quá {wait_s}s chưa xong; mã yêu cầu {sub.get('request_id')}")


def files_in(obj, path=""):
    """Mọi file (url) trong kết quả: [(đường khoá, url, tên file)]."""
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


def download(url, path):
    with urllib.request.urlopen(url, timeout=300) as r, open(path, "wb") as f:
        f.write(r.read())


def pick_model(files):
    """Ưu tiên bản đã gắn xương (.glb), rồi .fbx gắn xương, rồi .glb thường."""
    def score(item):
        key, url, name = item
        k = (key + " " + name).lower()
        ext = os.path.splitext(name.lower())[1]
        rigged = any(w in k for w in ("rig", "character", "skin", "armature"))
        anim = "anim" in k
        return (rigged and not anim, ext == ".glb", ext == ".fbx", not anim)
    models = [f for f in files if os.path.splitext(f[2].lower())[1] in (".glb", ".gltf", ".fbx")]
    return max(models, key=score) if models else None


# ---------------------------------------------------------------- 3. kiểm tra mô hình
NEED = ["hips", "spine", "chest", "neck", "head", "upper_arm.L", "forearm.L", "hand.L", "upper_arm.R",
        "forearm.R", "hand.R", "thigh.L", "shin.L", "foot.L", "thigh.R", "shin.R", "foot.R"]


def verify(path, code):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm = body.load_model(path, code)
    mp = body.bone_map(arm)
    got = sorted({s for s in mp.values() if s})
    missing = [n for n in NEED if n not in got]
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    skinned = [o.name for o in meshes if any(m.type == "ARMATURE" for m in o.modifiers) or o.parent == arm]
    zs = []
    for o in meshes:
        if o.name in skinned:
            zs += [(o.matrix_world @ v.co).z for v in o.data.vertices]
    height = (max(zs) - min(zs)) if zs else 0.0
    want = float(shot.load_json("nhan_vat.json")[code].get("chieu_cao", 1.7))
    hip = body.hip_height(arm, mp, body._world_rest(arm))[1]
    ok_size = abs(height - want) < 0.25 * want and 0.3 * want < hip < 0.75 * want
    return {
        "xuong": len(arm.data.bones),
        "ten_khop": [b.name for b in arm.data.bones],
        "khop_chuan_nhan_duoc": got,
        "khop_chuan_thieu": missing,
        "luoi_gan_xuong": skinned,
        "cao_m": round(height, 3),
        "cao_hong_m": round(hip, 3),
        "ti_le_dung": ok_size,
        "dat": not missing and bool(skinned) and ok_size,
    }


def set_model(code, rel):
    p = os.path.join(DATA, "nhan_vat.json")
    with open(p, encoding="utf-8") as f:
        d = json.load(f)
    d[code]["mo_hinh_3d"] = rel.replace("\\", "/")
    lines = ["{"]
    keys = list(d)
    for i, k in enumerate(keys):
        lines.append(f"  {json.dumps(k, ensure_ascii=False)}: {json.dumps(d[k], ensure_ascii=False)}"
                     + ("," if i < len(keys) - 1 else ""))
    lines.append("}")
    with open(p, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


# ---------------------------------------------------------------- chạy
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
codes = [a for a in argv if not a.startswith("--") and a not in ENDPOINTS]
source = argv[argv.index("--nguon") + 1] if "--nguon" in argv else "meshy"
only_crop = "--chi-cat" in argv
redo = "--lam-lai" in argv
if not codes:
    raise SystemExit("Thiếu mã nhân vật. Ví dụ: ... tao_mo_hinh_3d.py -- Bo")

cfg = shot.config()
chars = shot.load_json("nhan_vat.json")
for code in codes:
    if code not in chars:
        raise SystemExit(f"Nhân vật '{code}' chưa có trong data/nhan_vat.json")
    info = chars[code]
    sheet = os.path.join(cfg["thu_muc_anh"], info.get("anh_goc", f"{code}_goc.png"))
    report = {"ma": code, "nguon": source, "anh_goc": sheet}
    os.makedirs(OUT, exist_ok=True)
    model_dir = os.path.join(OUT, code)
    final = os.path.join(OUT, f"{code}.glb")
    existing = next((os.path.join(OUT, f"{code}{e}") for e in (".glb", ".fbx") if os.path.exists(os.path.join(OUT, f"{code}{e}"))), None)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    views = crop_views(code, sheet)
    report["anh_gui"] = views
    print("SGD_MH cắt xong", code, views)
    if only_crop:
        continue

    if existing and not redo:
        print("SGD_MH đã có mô hình, không gọi Fal (thêm --lam-lai để làm lại):", existing)
        model_path = existing
    else:
        key = fal_key(cfg)
        h = float(info.get("chieu_cao", 1.7))
        if source == "meshy":
            payload = {
                "image_urls": [data_uri(views[v]) for v in ("chinh_dien", "ba_phan_tu", "nghieng_trai", "phia_sau")],
                "should_texture": True, "enable_pbr": False, "pose_mode": "t-pose",
                "enable_rigging": True, "rigging_height_meters": h,
                "topology": "triangle", "target_polycount": 50000, "should_remesh": True,
                "symmetry_mode": "auto", "enable_safety_checker": True,
            }
        else:
            payload = {
                "image_urls": [data_uri(views[v]) for v in ("chinh_dien", "nghieng_trai", "phia_sau")],
                "texture": True, "pbr": True, "texture_quality": "detailed", "geometry_quality": "detailed",
            }
        result, req_id = call_fal(ENDPOINTS[source], payload, key)
        os.makedirs(model_dir, exist_ok=True)
        with open(os.path.join(OUT, f"{code}_fal.json"), "w", encoding="utf-8") as f:
            json.dump({"endpoint": ENDPOINTS[source], "request_id": req_id, "ket_qua": result}, f, ensure_ascii=False, indent=1)
        files = files_in(result)
        for k, url, name in files:
            dst = os.path.join(model_dir, name)
            download(url, dst)
            print("SGD_MH tải", k, "->", dst)
        chosen = pick_model(files)
        if not chosen:
            raise SystemExit("Kết quả Fal không có file mô hình (.glb/.fbx). Xem data/mo_hinh/" + f"{code}_fal.json")
        ext = os.path.splitext(chosen[2])[1].lower()
        model_path = os.path.join(OUT, f"{code}{ext}")
        with open(os.path.join(model_dir, chosen[2]), "rb") as fi, open(model_path, "wb") as fo:
            fo.write(fi.read())
        report["file_chon"] = {"khoa": chosen[0], "ten": chosen[2]}

    check = verify(model_path, code)
    report["kiem_tra"] = check
    print("SGD_MH kiểm tra", code, "đạt" if check["dat"] else "CHƯA ĐẠT",
          "thiếu:", check["khop_chuan_thieu"], "xương:", check["xuong"])
    if check["dat"]:
        set_model(code, os.path.relpath(model_path, DATA))
        spec = {"loai": "than", "khung": "toan", "goc_may_do": 25, "ma": f"{code}_xem_truoc",
                "dien_vien": [{"nhan_vat": code, "vi_tri": [0, 0], "huong_do": 0,
                               "chuyen_dong": [{"ten": "dung_tho", "giay": 1.0}, {"ten": "di", "giay": 1.5},
                                               {"ten": "vay_tay"}]}]}
        paths, _, _ = shot.run(spec, stills=[1, 40, 80], still_only=True, out_override=OUT)
        report["xem_truoc"] = paths
    with open(os.path.join(OUT, f"{code}_ket_qua.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=1)
    print("SGD_MH xong", code, os.path.join(OUT, f"{code}_ket_qua.json"))

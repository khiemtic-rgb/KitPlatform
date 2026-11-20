"""Sửa đầu 3D (Rodin) theo ảnh gốc -> data/mo_hinh/<Ma>_dau.glb (load_model tự ghép lên thân).
Chạy trong máy có Blender-python (bpy) + OpenCV contrib (cv2.face) + mô hình LBF (moc_mat.py). Cách dùng:
    python3.11 sua_dau_3d.py Ma [--muc 0.85] [--da_min]
Các bước: lấy ảnh màu đầu -> tô lại tóc tối / bớt vàng da (to_mau) -> chụp thẳng (chup) -> 68 mốc trên
ảnh chụp và ảnh gốc (moc68) -> nắn hình theo mốc + tô mống mắt theo ảnh gốc, xuất glb (nan_dau)."""
import os, subprocess, sys, tempfile
HERE = os.path.dirname(os.path.abspath(__file__))
PY = sys.executable
ma = sys.argv[1]
muc = sys.argv[sys.argv.index("--muc") + 1] if "--muc" in sys.argv else "0.85"
if "--da_min" in sys.argv:                      # nhân vật trẻ: làm mịn da (bỏ vết sọc của bản dựng)
    os.environ["SGD_DA_MIN"] = "1"
mh = os.path.join(HERE, "data", "mo_hinh")
src = os.path.join(mh, f"{ma}_dau_rodin", "base_basic_shaded.glb")
ref = os.path.join(mh, "anh_vao", f"{ma}_chan_dung.png")
out = os.path.join(mh, f"{ma}_dau.glb")
tmp = tempfile.mkdtemp(prefix=f"sgd_dau_{ma}_")
D = os.path.join(HERE, "dau3d")


def run(*a, env=None):
    r = subprocess.run([PY, *a], capture_output=True, text=True, env=env)
    if r.returncode:
        sys.stderr.write(r.stdout[-3000:] + r.stderr[-3000:])
        raise SystemExit(f"loi o buoc {os.path.basename(a[0])}")
    return r.stdout


tex = os.path.join(tmp, "tex.png")
run("-c", f"import bpy;bpy.ops.import_scene.gltf(filepath={src!r});im=[i for i in bpy.data.images if i.size[0]>0][0];"
          f"im.filepath_raw={tex!r};im.file_format='PNG';im.save()")
print(run(os.path.join(D, "to_mau.py"), tex, os.path.join(tmp, "tex2.png")).strip())
env = dict(os.environ, TEX=os.path.join(tmp, "tex2.png"))
run(os.path.join(D, "chup.py"), "--", src, os.path.join(tmp, "r"), env=env)
os.chdir(tmp)
run(os.path.join(D, "moc68.py"), os.path.join(tmp, "r_thang.png"), ref)
lm_r = os.path.join(tmp, "lm_r_thang.png.json")
lm_g = os.path.join(tmp, "lm_" + os.path.basename(ref) + ".json")
o = run(os.path.join(D, "nan_dau.py"), "--", src, os.path.join(tmp, "tex2.png"), lm_r, lm_g, ref, out, muc,
        os.path.join(tmp, "r_thang.png"))
print("\n".join(l for l in o.splitlines() if l.startswith(("dich", "mau", "texel", "giam", "da min", "XONG"))))
print("SGD_SUA_DAU_XONG", ma, out)

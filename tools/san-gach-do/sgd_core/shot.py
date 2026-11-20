"""Chạy một cảnh từ file dữ liệu: đọc cấu hình + nhân vật + biểu cảm, đo (hoặc đọc số đo), dựng, diễn, xuất.

File cảnh (canh/*.json), chỉ "nhan_vat" là bắt buộc:
{
  "ma": "tap03_c08_bo",                 # tên file ra (mặc định: tên file cảnh)
  "nhan_vat": "Bo",                     # mã trong data/nhan_vat.json
  "khung": "can_mat",                   # can_mat | dac_ta | trung (stage.FRAMING)
  "nen": null,                          # null | [r,g,b] | "đường/dẫn/anh_boi_canh.png"
  "thoai": {"file": "x.wav", "bat_dau": 0.4},   # miệng theo thoại; bỏ trống = ngậm
  "nhep_mau": false,                    # true = nhịp nói mẫu (thử khi chưa có thoại)
  "do_dai": 4.0,                        # giây; mặc định = thoại + 0.5 giây, không thoại = 4 giây
  "bieu_cam": [{"giay": 0, "bieu_cam": "lo", "chuyen": 0.6}],
  "chop_mat": true,
  "ra": "tap03/c08_bo"                  # thư mục ra, tính từ thu_muc_ra
}
Đường dẫn tương đối trong file cảnh tính từ thư mục chứa file cảnh.
"""
import json
import math
import os

from . import anim, measure, stage

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(HERE, "data")


def load_json(name):
    with open(os.path.join(DATA, name), encoding="utf-8") as f:
        return {k: v for k, v in json.load(f).items() if not k.startswith("_")}


def config():
    cfg = load_json("cau_hinh.json")
    cfg["thu_muc_anh"] = os.environ.get("SGD_THU_MUC_ANH", cfg["thu_muc_anh"])
    cfg["thu_muc_ra"] = os.environ.get("SGD_THU_MUC_RA", cfg["thu_muc_ra"])
    return cfg


def sheet_path(cfg, code, info):
    name = info.get("anh_mat", f"{code}_mat.png")
    return name if os.path.isabs(name) else os.path.join(cfg["thu_muc_anh"], name)


def face_data(cfg, code, info):
    """Số đo của nhân vật: đọc data/do_dac/<ma>.json nếu còn khớp ảnh (hoặc đã khoá), không thì đo lại."""
    import bpy

    from .rig import sheet_rgb
    path = sheet_path(cfg, code, info)

    def loader():
        im = bpy.data.images.load(path, check_existing=True)
        return sheet_rgb(im)
    return measure.load_or_measure(code, path, loader, os.path.join(DATA, "do_dac"))


def resolve(base_dir, p):
    if not p:
        return p
    return p if os.path.isabs(p) else os.path.normpath(os.path.join(base_dir, p))


def sentence_anchors(spec, wav, offset):
    """Hàm k -> giây bắt đầu câu thoại thứ k (đếm từ 1) trong clip.

    Ưu tiên "thoai": {"cau_bat_dau": [...]} (giây, tính từ đầu file thoại); không có thì tự dò từ
    "thoai": {"loi": ["câu 1", "câu 2", ...]} và file thoại, in ra để kiểm và chốt lại nếu muốn.
    """
    thoai = spec.get("thoai") or {}
    starts = thoai.get("cau_bat_dau")
    if starts is None and thoai.get("loi") and wav:
        starts, note = anim.sentence_starts(wav, thoai["loi"])
        print("SGD_CAU", "tự dò:", starts, "; ".join(note))
    cache = {"s": starts}

    def at(k):
        if cache["s"] is None:
            raise ValueError('Dùng "cau" thì file cảnh cần "thoai": {"loi": [...]} hoặc {"cau_bat_dau": [...]}')
        if not 1 <= int(k) <= len(cache["s"]):
            raise ValueError(f"Câu {k} không có: thoại có {len(cache['s'])} câu")
        return offset + float(cache["s"][int(k) - 1])
    return at


def run(spec, base_dir=".", stills=None, still_only=False, out_override=None, frames_dir=None, tag=None):
    if spec.get("loai") == "than":
        return run_body(spec, base_dir, stills, still_only, out_override, frames_dir, tag)
    from . import rig as rigmod

    cfg = config()
    chars = load_json("nhan_vat.json")
    exprs = load_json("bieu_cam.json")
    code = spec["nhan_vat"]
    if code not in chars:
        raise KeyError(f"Nhân vật '{code}' chưa có trong data/nhan_vat.json")
    info = dict(chars[code])
    if spec.get("anh_mat"):
        info["anh_mat"] = resolve(base_dir, spec["anh_mat"])
    tag = tag or spec.get("ma") or code.lower()

    thoai = spec.get("thoai") or {}
    wav = resolve(base_dir, thoai.get("file"))
    offset = float(thoai.get("bat_dau", 0.0))
    if spec.get("do_dai"):
        seconds = float(spec["do_dai"])
    elif wav:
        seconds = offset + anim.audio_seconds(wav) + 0.5
    else:
        seconds = 4.0
    end = max(2, int(math.ceil(seconds * anim.FPS)))

    scene = stage.setup(cfg.get("render", {}), end)
    stage.background(scene, resolve(base_dir, spec["nen"]) if isinstance(spec.get("nen"), str) else spec.get("nen"))
    face_info = face_data(cfg, code, info)
    face = rigmod.build(scene, sheet_path(cfg, code, info), face_info, name=code, pose=info.get("dang"))
    stage.camera(scene, spec.get("khung", "can_mat"), info.get("khung"))

    fixed = spec.get("mat_khep_co_dinh")
    anim.idle(face, end, blink_off=not spec.get("chop_mat", True), fixed_blink=fixed)
    if wav:
        anim.mouth_from_audio(face, wav, end, int(round(offset * anim.FPS)))
    elif spec.get("nhep_mau"):
        anim.mouth_sample(face)
    else:
        face.set_mouth(1, 0.0)
    anchors = sentence_anchors(spec, wav, offset)
    timeline = [dict(it, giay=anchors(it["cau"]) + float(it.get("lech", 0.0))) if "cau" in it else it
                for it in spec.get("bieu_cam", [])]
    anim.expressions(face, timeline, exprs)
    anim.smooth(face.animated_owners())

    out_dir = os.path.normpath(out_override or os.path.join(cfg["thu_muc_ra"], spec.get("ra", code.lower())))
    stills = stills or [1, min(end, 32), min(end, 36)]
    paths, frames = stage.render(scene, out_dir, tag, stills, animation=not still_only, frames_dir=frames_dir)
    mp4 = None
    if frames:
        a_wav, a_off = _audio_track(spec, wav, offset, seconds, frames, tag, base_dir)
        mp4 = stage.encode(frames, tag, os.path.join(out_dir, f"{tag}.mp4"), a_wav,
                           stage.find_ffmpeg(cfg.get("ffmpeg")), a_off, n_frames=end)
    print("SGD_DONE", tag, paths[-1] if paths else "", mp4 or "")
    return paths, frames, mp4


def run_file(path, **kw):
    with open(path, encoding="utf-8") as f:
        spec = json.load(f)
    spec.setdefault("ma", os.path.splitext(os.path.basename(path))[0])
    return run(spec, base_dir=os.path.dirname(os.path.abspath(path)), **kw)


def _timeline(items, anchors):
    """Đổi danh sách động tác thành mốc bắt đầu (giây). Mặc định nối tiếp nhau; "bat_dau": giây; "cau": k (câu thoại)."""
    from . import body
    out, t = [], 0.0
    for it in items:
        it = dict(it)
        if "cau" in it:
            it["bat_dau"] = anchors(it["cau"]) + float(it.get("lech", 0.0))
        it.setdefault("bat_dau", t)
        clip = body.get_clip(it["ten"])
        dur = float(it.get("giay") or body.library().get(it["ten"], {}).get("giay") or clip.duration())
        it["giay"] = dur
        t = it["bat_dau"] + dur
        out.append(it)
    return out, t


def _reach(arm, other, vt, anchors, end, scene):
    """Một lần với tay: tay gần người kia nhất vươn tới bàn tay gần nhất của người kia, vào/ra mềm."""
    from . import body
    fps = anim.FPS

    def tsec(t):
        return anchors(int(t["cau"])) + float(t.get("lech", 0.0)) if isinstance(t, dict) else float(t)
    t0, t1 = tsec(vt["tu"]), tsec(vt["den"])
    ramp = float(vt.get("vao", 0.4))
    f0, f1 = max(1, int(t0 * fps) + 1), min(end, int(t1 * fps) + 1)
    inv_a = {v: k for k, v in body.bone_map(arm).items() if v}
    inv_o = {v: k for k, v in body.bone_map(other).items() if v}

    def hand(a_, inv_, s, f):
        scene.frame_set(f)
        return a_.matrix_world @ a_.pose.bones[inv_[f"hand.{s}"]].head
    fm = (f0 + f1) // 2
    side = vt.get("tay")
    if side not in ("L", "R"):
        side = min("LR", key=lambda s: min((hand(arm, inv_a, s, fm) - hand(other, inv_o, o, fm)).length for o in "LR"))
    # điểm cần tới mỗi khung: bàn tay người kia gần tay mình nhất (lấy trước khi sửa tay mình)
    tg = {}
    for f in range(f0, f1 + 1):
        mine = hand(arm, inv_a, side, f)
        tg[f] = min((hand(other, inv_o, o, f) for o in "LR"), key=lambda p: (p - mine).length)

    def wt(f):
        t = (f - 1) / fps
        if t < t0 or t > t1:
            return 0.0
        u = min(1.0, (t - t0) / ramp, (t1 - t) / ramp)
        return u * u * (3 - 2 * u)
    body.reach(arm, side, lambda f: tg.get(f), wt, range(f0, f1 + 1))
    print("SGD_VOI_TAY", side, f"{t0:.2f}-{t1:.2f}s")


def _face3d(face, d, idx, end, seconds, wav, offset, anchors, thoai):
    """Mặt diễn viên 3D: chớp mắt (lệch nhịp theo người), khẩu hình theo thoại, biểu cảm lông mày theo câu.

    "noi": true -> nói cả file thoại; "noi": [1, 3] -> chỉ nói các câu 1 và 3 (thoại nhiều người trong một file).
    "bieu_cam": [{"cau": 2, "bieu_cam": "lo", "chuyen": 0.5, "lech": 0.1}, ...] như cảnh mặt.
    """
    import numpy as np
    exprs = load_json("bieu_cam.json")
    anim.idle(face, end, blink_off=not d.get("chop_mat", True), phase=(idx * 37) % 60)
    noi = d.get("noi")
    loi = thoai.get("loi") or []
    if noi and wav and loi and d.get("khau_hinh", True) and hasattr(face, "set_viseme"):
        # khẩu hình theo âm: canh từng âm tiết của lời thoại vào tiếng, rồi đặt dáng môi
        from . import khau_hinh
        starts = [anchors(k + 1) - offset for k in range(len(loi))]
        syll = khau_hinh.can_am_tiet(wav, loi, starts, offset, seconds)
        mask = None
        if isinstance(noi, list):
            mask = np.zeros(end)
            for k in noi:
                a = anchors(k)
                b = anchors(k + 1) if k < len(loi) else seconds
                mask[max(0, int(a * anim.FPS) - 2):min(end, int(b * anim.FPS) + 1)] = 1.0
        curves = khau_hinh.duong_cong(syll, end, anim.FPS, mask=mask)
        khau_hinh.ap_vao_mat(face, curves)
        print("SGD_KHAU_HINH", f"{len(syll)} âm tiết")
    elif noi and wav:
        env = anim.audio_envelope(wav, anim.FPS, end, int(round(offset * anim.FPS)))
        if isinstance(noi, list):
            n_cau = len(thoai.get("cau_bat_dau") or thoai.get("loi") or [])
            mask = np.zeros(end)
            for k in noi:
                a = anchors(k)
                b = anchors(k + 1) if k < n_cau else seconds
                f0, f1 = max(0, int(a * anim.FPS) - 2), min(end, int(b * anim.FPS) + 1)
                mask[f0:f1] = 1.0
            env = env * mask
        for f, v in enumerate(env, start=1):
            face.set_mouth(f, float(v))
    else:
        face.set_mouth(1, 0.0)
    timeline = [dict(it, giay=anchors(it["cau"]) + float(it.get("lech", 0.0))) if "cau" in it else it
                for it in d.get("bieu_cam", [])]
    anim.expressions(face, timeline, exprs)
    anim.smooth(face.animated_owners())


def _audio_track(spec, wav, offset, seconds, frames_dir, tag, base_dir):
    """Có "am_thanh" trong cảnh: trộn thoại + nền + nhạc ra một wav (bắt đầu ở 0 giây). Không có: dùng thoại gốc."""
    if not spec.get("am_thanh"):
        return wav, offset
    from . import am_thanh
    path = os.path.join(frames_dir, f"{tag}_am_thanh.wav")
    am_thanh.mix(spec, wav, offset, seconds, path, base_dir)
    print("SGD_AM_THANH", path)
    return path, 0.0


def run_body(spec, base_dir=".", stills=None, still_only=False, out_override=None, frames_dir=None, tag=None):
    """Cảnh thân người: "loai": "than", "dien_vien": [{"nhan_vat", "vi_tri": [x, y], "huong_do", "chuyen_dong": [...]}]."""
    from . import body

    cfg = config()
    chars = load_json("nhan_vat.json")
    tag = tag or spec.get("ma") or "canh_than"
    thoai = spec.get("thoai") or {}
    wav = resolve(base_dir, thoai.get("file"))
    offset = float(thoai.get("bat_dau", 0.0))
    anchors = sentence_anchors(spec, wav, offset)

    plans, longest = [], 0.0
    for d in spec["dien_vien"]:
        items, t_end = _timeline(d.get("chuyen_dong", [{"ten": "dung_tho"}]), anchors)
        plans.append((d, items))
        longest = max(longest, t_end)
    if spec.get("do_dai"):
        seconds = float(spec["do_dai"])
    elif wav:
        seconds = max(longest, offset + anim.audio_seconds(wav) + 0.5)
    else:
        seconds = longest
    end = max(2, int(math.ceil(seconds * anim.FPS)))

    scene = stage.setup(cfg.get("render", {}), end)
    if spec.get("boi_canh"):                       # bối cảnh dựng sẵn (vd bờ đê) thay cho nền phẳng
        from . import boi_canh
        boi_canh.build(scene, spec["boi_canh"] if isinstance(spec["boi_canh"], dict) else {"kieu": spec["boi_canh"]})
    else:
        stage.body_set(scene, resolve(base_dir, spec["nen"]) if isinstance(spec.get("nen"), str) else spec.get("nen"))
    xs, ys = [], []
    arms_ = []
    for idx, (d, items) in enumerate(plans):
        code = d["nhan_vat"]
        if code not in chars:
            raise KeyError(f"Nhân vật '{code}' chưa có trong data/nhan_vat.json")
        info = chars[code]
        model = info.get("mo_hinh_3d")
        model = model if not model or os.path.isabs(model) else os.path.join(DATA, model)
        x, y = d.get("vi_tri", [0, 0])
        arm = body.actor(code, info, (x, y, 0), float(d.get("huong_do", 0.0)), model)
        # mặt 3D (chớp mắt, khẩu hình, lông mày): đo/gắn ở tư thế nghỉ, trước khi đặt chuyển động
        face = None
        if model and os.path.exists(model) and d.get("mat", True):
            from . import mat3d
            face = mat3d.attach(arm, code, model, DATA)
        # động tác còn thiếu đến hết cảnh: đứng thở
        last_end = items[-1]["bat_dau"] + items[-1]["giay"] if items else 0.0
        if last_end < seconds:
            items.append({"ten": "dung_tho", "bat_dau": last_end, "giay": seconds - last_end})
        body.bake_timeline(arm, items, end)
        arms_.append(arm)
        if d.get("hien"):                            # chỉ hiện trong khoảng [t0, t1] giây (đổi tức thì ở cú cắt)
            import bpy
            from . import quay
            t0, t1 = d["hien"][0], (d["hien"][1] if len(d["hien"]) > 1 else None)
            quay.set_visibility([arm] + [o for o in bpy.data.objects if o.parent == arm], float(t0),
                                None if t1 is None else float(t1), end)
        if face is not None:
            _face3d(face, d, idx, end, seconds, wav, offset, anchors, thoai)
        xs.append(x)
        ys.append(y)
    # với tay tới bàn tay người kia (trao/nhận đồ): "voi_tay": [{"toi": j, "tu": giây|{"cau"..}, "den": ..., "vao": 0.4}]
    for i, (d, _items) in enumerate(plans):
        for vt in d.get("voi_tay", []):
            _reach(arms_[i], arms_[int(vt["toi"])], vt, anchors, end, scene)
    if spec.get("canh_quay") or spec.get("do_vat"):
        from . import quay
        dvs = [p[0] for p in plans]

        def visible(i, f):
            h = dvs[i].get("hien")
            if not h:
                return True
            t = (f - 1) / anim.FPS
            return float(h[0]) <= t and (len(h) < 2 or h[1] is None or t < float(h[1]))
        tracks = quay.Tracks(scene, arms_, end)
        props = quay.build_props(scene, spec.get("do_vat"), tracks, arms_, anchors, end)
        for pr in props.values():                    # ba lô theo diễn viên: ẩn/hiện cùng người đeo
            if "dien_vien" in pr and dvs[pr["dien_vien"]].get("hien"):
                h = dvs[pr["dien_vien"]]["hien"]
                quay.set_visibility([pr["obj"]], float(h[0]), float(h[1]) if len(h) > 1 and h[1] is not None else None, end)
        if spec.get("canh_quay"):
            quay.build_cameras(scene, spec["canh_quay"], tracks, anchors, end, visible, props,
                               phia=int(spec.get("phia_may", -1)))
            out_dir = os.path.normpath(out_override or os.path.join(cfg["thu_muc_ra"], spec.get("ra", tag)))
            stills = stills or [1, max(1, end // 2), end]
            paths, frames = stage.render(scene, out_dir, tag, stills, animation=not still_only, frames_dir=frames_dir)
            mp4 = None
            if frames:
                a_wav, a_off = _audio_track(spec, wav, offset, seconds, frames, tag, base_dir)
                mp4 = stage.encode(frames, tag, os.path.join(out_dir, f"{tag}.mp4"), a_wav,
                                   stage.find_ffmpeg(cfg.get("ffmpeg")), a_off, n_frames=end)
            print("SGD_DONE", tag, paths[-1] if paths else "", mp4 or "")
            return paths, frames, mp4
    target = (sum(xs) / len(xs), sum(ys) / len(ys), 0)
    if spec.get("diem_nhin"):                      # tuỳ chọn: máy nhìn vào điểm [x, y] thay vì giữa các diễn viên
        target = (float(spec["diem_nhin"][0]), float(spec["diem_nhin"][1]), 0)
    cam = stage.body_camera(scene, spec.get("khung", "toan"), target, float(spec.get("goc_may_do", 20.0)), spec.get("may"))
    # cảnh cận: máy bám theo đầu người nói (hoặc diễn viên đầu tiên); tắt bằng "may_theo": false
    theo = spec.get("may_theo", spec.get("khung", "toan") in ("can_mat", "can_than", "trung_than"))
    if theo and arms_:
        who = next((a for a, d in zip(arms_, [p[0] for p in plans]) if d.get("noi")), arms_[0])
        hb = next(b for b, s_ in body.bone_map(who).items() if s_ == "head")
        pts = []
        for f in range(1, end + 1, 2):
            scene.frame_set(f)
            pts.append((f, (who.matrix_world @ who.pose.bones[hb].head).copy()))
        stage.follow(scene, cam, pts)

    out_dir = os.path.normpath(out_override or os.path.join(cfg["thu_muc_ra"], spec.get("ra", tag)))
    stills = stills or [1, max(1, end // 2), end]
    paths, frames = stage.render(scene, out_dir, tag, stills, animation=not still_only, frames_dir=frames_dir)
    mp4 = None
    if frames:
        a_wav, a_off = _audio_track(spec, wav, offset, seconds, frames, tag, base_dir)
        mp4 = stage.encode(frames, tag, os.path.join(out_dir, f"{tag}.mp4"), a_wav,
                           stage.find_ffmpeg(cfg.get("ffmpeg")), a_off, n_frames=end)
    print("SGD_DONE", tag, paths[-1] if paths else "", mp4 or "")
    return paths, frames, mp4

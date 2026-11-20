# Sân Gạch Đỏ — cảnh cận mặt trong Blender

Mặt nhân vật lấy từ ảnh mẫu `<Ma>_mat.png` (6 ô, ô trên-trái là mặt chính). Mí mắt, miệng, lông mày là các dải da trùng
khít ảnh mẫu (lúc nghỉ không lộ mép), cử động bằng shape key. Phần mềm tự đo mắt / môi / lông mày trên ảnh, nên
**thêm nhân vật, thêm cảnh, thêm biểu cảm đều chỉ là thêm dữ liệu, không sửa mã.**

Blender: `E:\KitPlatform\.dev\blender\blender-4.5.10-windows-x64\blender.exe` (không có trong PATH).
Cách gọn: dùng `sgd.cmd` cùng thư mục, vd `tools\san-gach-do\sgd.cmd kiem_tra Minh` hoặc
`tools\san-gach-do\sgd.cmd canh tools\san-gach-do\canh\tap03_c08_bo.json`. Dưới đây `blender` nghĩa là đường dẫn trên.

## Cấu trúc

```
tools/san-gach-do/
  render_shot.py        chạy cảnh từ file dữ liệu (lối vào chính)
  kiem_tra.py           kiểm tra nhân vật: mí khép có che kín mắt không
  char_motion.py        lối vào cũ theo biến môi trường (giữ cho lệnh cũ)
  bo_motion.py          bản riêng đầu tiên của Bố (số đo tay) — không dùng nữa, giữ làm mốc
  tts_vi.py             đọc thoại thử bằng giọng máy (espeak-ng), chỉ để thử khớp miệng
  tao_chuyen_dong_mau.py  tạo bộ chuyển động mẫu (bản tạm) vào data/chuyen_dong/
  kiem_tra_chuyen_dong.py kiểm tra chuyển động dùng lại được trên mọi bộ xương
  sgd_core/
    measure.py          đo mắt / môi / lông mày; lưu số đo ở data/do_dac/<Ma>.json
    rig.py              dựng mặt: lưới mặt, mí, miệng, lông mày
    anim.py             thở, nghiêng đầu, chớp mắt, miệng theo thoại, biểu cảm theo thời gian
    stage.py            cài đặt render, nền, cỡ cảnh, xuất khung, ghép tiếng
    body.py             thân người: xương chuẩn, thư viện chuyển động, chuyển sang nhân vật bất kỳ
    shot.py             đọc file cảnh và chạy cả chuỗi (cảnh mặt / cảnh thân người)
  data/
    cau_hinh.json       thư mục ảnh mẫu, thư mục ra, ffmpeg, độ phân giải
    nhan_vat.json       danh sách nhân vật
    bieu_cam.json       thư viện biểu cảm
    chuyen_dong.json    thư viện chuyển động (+ file .bvh/.fbx trong data/chuyen_dong/)
    xuong_chuan.json    bảng đổi tên khớp (Mixamo, CMU, Rigify) về tên chuẩn
    do_dac/             số đo tự sinh (sửa tay được)
  canh/                 mỗi cảnh một file .json (+ thoại trong canh/thoai/)
```

## Thêm nhân vật

1. Đặt ảnh mẫu `<Ma>_mat.png` vào thư mục ảnh (mặc định `F:\Noi_Com_Chung\Nhan_Vat\Bo_Chuan`).
2. Thêm một dòng vào `data/nhan_vat.json`: `"Ngoc": {"ten": "Ngọc"}`.
3. Kiểm tra: `blender --background --python tools/san-gach-do/kiem_tra.py -- Ngoc`
   → xem `kiem_tra/bao_cao.json` và ảnh `kiem_tra/ngoc_khep_khung_24.png` trong thư mục ra. "dat": true là dùng được.

Không đạt (thường do ảnh mẫu lạ: tóc che mắt, đeo kính): mở `data/do_dac/Ngoc.json`, sửa số (toạ độ -1..1 của ô mặt chính:
`eyes[].top/bottom` là mép trên/dưới khe mắt theo 11 điểm từ khoé trái sang khoé phải; `mouth`; `brows`), đặt `"khoa": true`
để phần mềm không đo đè, rồi chạy lại kiểm tra.

Tuỳ chọn trong `nhan_vat.json`: `"anh_mat"` (tên/đường dẫn ảnh khác), `"dang": {"xoay_do": 10, "lech_x": 0.22, "co": 1.45}`
(góc xoay ba phần tư, lệch ngang, cỡ mặt), `"khung": {"vi_tri": [x, y, z], "tieu_cu": 50}` (chỉnh máy riêng nhân vật này).

## Thêm cảnh

Tạo `canh/<ten_canh>.json`:

```json
{
  "nhan_vat": "Bo",
  "khung": "can_mat",
  "nen": null,
  "thoai": {"file": "thoai/tap03_c08_bo.wav", "bat_dau": 0.0},
  "bieu_cam": [
    {"giay": 0.3,  "bieu_cam": "lo",    "chuyen": 0.5},
    {"giay": 13.0, "bieu_cam": "nghen", "chuyen": 0.8}
  ],
  "chop_mat": true,
  "ra": "tap03/c08_bo"
}
```

Chạy: `blender --background --python tools/san-gach-do/render_shot.py -- tools/san-gach-do/canh/tap03_c08_bo.json`
Thử nhanh vài khung: thêm `--chi-anh --khung 1,40,330`.
Ra: `<thu_muc_ra>/tap03/c08_bo/tap03_c08_bo.mp4` (có tiếng nếu có thoại và có ffmpeg) + khung PNG.

- `khung`: `can_mat` (cận mặt, đã duyệt), `dac_ta` (đặc tả, sát hơn), `trung` (lùi xa hơn).
- `nen`: `null` (xám), `[0.2, 0.25, 0.3]` (màu), hoặc đường dẫn ảnh bối cảnh đặt sau đầu nhân vật.
- `thoai`: miệng mở-khép theo âm lượng file `.wav`; `bat_dau` là giây bắt đầu nói. Không có thoại: miệng ngậm
  (hoặc `"nhep_mau": true` để thử nhịp nói mẫu).
- `do_dai`: giây; mặc định bằng thoại + 0,5 giây, không thoại thì 4 giây.
- `bieu_cam`: các mốc giây và tên biểu cảm trong `data/bieu_cam.json`; `chuyen` là số giây chuyển dần.
- Nhiều cảnh một lần: liệt kê nhiều file sau `--`.

## Mốc theo câu thoại

Ghi lời thoại vào file cảnh, rồi gắn biểu cảm/động tác theo câu thay vì theo giây — đổi giọng, đổi tốc độ đọc
thì mốc tự dời theo:

```json
"thoai": {"file": "thoai/x.wav", "loi": ["Câu một.", "Câu hai, có dấu phẩy.", "Câu ba."]},
"bieu_cam": [{"cau": 3, "bieu_cam": "nghen", "chuyen": 0.8, "lech": -0.2}]
```

Phần mềm ghép các dấu câu trong lời với các khoảng lặng trong file thoại và in `SGD_CAU tự dò: [...]`.
Muốn chốt cứng (hoặc sửa tay) thì chép dãy đó vào `"cau_bat_dau": [...]`. `lech`: dời sớm/muộn (giây).

## Cảnh thân người (đi, đứng, ngồi, cử chỉ)

```json
{
  "loai": "than",
  "khung": "toan",
  "goc_may_do": 15,
  "thoai": {"file": "thoai/x.wav", "bat_dau": 1.8, "loi": ["..."]},
  "dien_vien": [
    {"nhan_vat": "Bo", "vi_tri": [2.3, 0], "huong_do": -90,
     "chuyen_dong": [{"ten": "di", "giay": 1.2}, {"ten": "dua_tien", "cau": 1}]},
    {"nhan_vat": "Minh", "vi_tri": [-0.45, 0], "huong_do": 90,
     "chuyen_dong": [{"ten": "dung_tho", "giay": 3}, {"ten": "gat_dau", "cau": 2}]}
  ]
}
```

- `khung`: `toan` (toàn thân), `trung_than`, `can_than`; `goc_may_do`: máy lệch sang phải bao nhiêu độ.
- `vi_tri` (mét), `huong_do`: 0 = nhìn về phía máy, 90 = nhìn sang phải khung, -90 = sang trái.
- `chuyen_dong`: nối tiếp nhau; mỗi mục có `ten` (trong data/chuyen_dong.json), tuỳ chọn `giay` (động tác lặp
  như đi/đứng thở thì kéo dài bao lâu cũng được), `bat_dau` (giây) hoặc `cau` (câu thoại). Hết danh sách thì tự đứng thở.
  Giữa hai động tác tự chuyển mềm 0,3 giây; đi xong thì động tác sau bắt đầu đúng chỗ dừng.
- Ví dụ chạy được: `canh/thu_than_bo_minh.json`.

Mỗi nhân vật dùng mô hình 3D trong `data/nhan_vat.json` → `"mo_hinh_3d": "mo_hinh/Bo.glb"` (.glb/.fbx, đã gắn
xương tư thế chữ T, vd qua Mixamo auto-rig). Chưa có thì dùng người mẫu tạm theo `chieu_cao` và `mau_tam`.

## Mô hình 3D từ ảnh góc (Fal)

`sgd mo_hinh Bo` — cắt 4 góc (chính diện, 3/4, nghiêng, sau) từ `<Ma>_goc.png`, gửi Fal `fal-ai/meshy/v6/multi-image-to-3d`
(dựng tư thế chữ T + tự gắn xương người, chiều cao lấy từ `chieu_cao`), tải về `data/mo_hinh/<Ma>/`, chép bản có xương thành
`data/mo_hinh/<Ma>.glb`, kiểm tra xương đổi đủ về khớp chuẩn rồi mới ghi `"mo_hinh_3d"` vào `nhan_vat.json` và render 3 ảnh xem trước.

- Khoá: biến môi trường `FAL_KEY`, hoặc `"fal_key_file"` trong `cau_hinh.json` trỏ tới file chứa khoá **ngoài** thư mục mã.
- Giá tham khảo: ~1,2 USD mô hình có màu + 0,2 USD gắn xương ≈ **1,4 USD/nhân vật**.
- Đã có `<Ma>.glb` thì lệnh chỉ kiểm tra + xem trước, **không** gọi Fal (thêm `--lam-lai` để làm lại).
- `--chi-cat`: chỉ cắt ảnh vào `data/mo_hinh/anh_vao/` để xem trước, không tốn tiền.
- `--nguon tripo`: Tripo H3.1 (rẻ hơn, nét hình tốt) nhưng **chưa có xương** — kiểm tra sẽ báo chưa đạt.
- Báo cáo: `data/mo_hinh/<Ma>_ket_qua.json`; kết quả gốc của Fal: `<Ma>_fal.json`. Tên xương Meshy ở `xuong_chuan.json` → `"meshy"`.

## Lớp chạm đất và kiểm tra độ mượt

Tiếp đất: chân đang lao nhanh thì gót giữ cách đất theo tốc độ (gót dài không quệt sàn), phần nâng chân được tính
trước cả đoạn và bắt đầu từ 3 khung trước khi chạm (gối không khựng khi nhảy xuống); đổi điểm tì gót → mũi không nhảy.
Đã đạt 60/60 (12 nhân vật × 5 chuỗi).

Khi nạp mô hình, đế + gót giày (dưới mắt cá) được gắn cứng vào xương bàn chân (gắn xương tự động hay chia gót cho
ống chân → gập cổ chân là gót chọc xuống sàn). Điểm chạm đất gồm cổ chân, gốc ngón, **gót theo lưới** (điểm lùi
về sau nhất của giày) và đầu mũi theo lưới.

Mọi cảnh thân người tự đi qua **lớp chạm đất** (`body.bake_timeline`, dùng chung mọi nhân vật, mọi động tác):
- động tác `"cham_dat": "ca_nguoi"` (mặc định): cả người hạ/nâng để bàn chân thấp nhất luôn chạm đất (hết lơ lửng/xuyên đất);
  `"chan"` (ngồi, đứng dậy): chỉ ghim bàn chân;
- bàn chân đang tì đất được giữ đứng yên (bù trượt), lúc nhấc chân thì về đúng chỗ động tác muốn theo đường mượt;
  trượt quá xa hoặc đứng yên mà chân lệch chỗ thì tự **chèn một bước nhỏ** (hai chân không bước cùng lúc);
- gối giải bằng IK hai đoạn, mềm khi gần duỗi thẳng, không bao giờ gập ra sau;
- động tác lặp (đi) vẫn chạy tiếp trong lúc chuyển sang động tác sau; đang đi mà dừng thì chuyển chậm hơn (0,5 giây).

`blender --background --python kiem_tra_muot.py -- Bo Minh Bin` — đo bằng số trên một chuỗi gồm mọi động tác:
giật (sai phân bậc 3 vị trí khớp; ngưỡng 10 cm/khung³ cho người 1,70 m, hoặc 1,3 lần độ giật vốn có của động tác
đang chạy), trượt chân (điểm đang tì đất), xuyên đất, lơ lửng, trên 5 chuỗi A, B, C, D, A_xoay → `kiem_tra/muot.json`.

Bước đi mẫu (`tao_chuyen_dong_mau.py`) dựng theo dáng đi chuẩn: ~1,22 m/giây, bước ~0,64 m, gối lúc trụ ~10°,
lúc đỡ người ~20°, lúc nhấc mũi chân ~40°, khi bước ~64°, hông nhấp nhô ~3 cm, mũi chân cách đất ~2–3 cm.
Thêm động tác mới: chạy lại lệnh này. `canh/thu_muot_bo.json` là cảnh xem bằng mắt cùng chuỗi đó.

## Động tác Mixamo (quay từ người thật)

`data/chuyen_dong/mixamo/*.fbx` — tải từ Mixamo (tài khoản Adobe miễn phí): FBX, không kèm da, 24 khung/giây, nhân vật
X Bot, KHÔNG "In Place" (giữ quãng đường đi). Khai báo trong `chuyen_dong.json` như mọi động tác khác.
- Bản quyền: dùng miễn phí cho phim (kể cả thương mại), **không được phát tán file gốc** ra ngoài nhóm làm phim
  → không đưa thư mục này lên kho mã công khai.
- Đang dùng: dung_tho, di, di_buon, di_cham_gia, chay, ngoi_xuong, ngoi_tho, dung_day, vay_tay, gat_dau, cui_chao,
  nhay_tai_cho, noi_chuyen. `dua_tien` vẫn là bản công thức. Bản công thức cũ giữ tên `<ten>_mau`.
- Sửa dáng đầu của bản quay (khai trong `chuyen_dong.json`, áp cho mọi nhân vật):
  `"chinh_dau": độ` (dương = cúi; chia 1/3 cổ, 2/3 đầu) — `di` +9 (bản quay hếch cằm ~10° so với lúc đứng),
  `di_buon` −30 (gục 66° → 36°), `vay_tay` +20, `noi_chuyen` +8;
  `"he_so_dau": k` nhân độ lệch đầu/cổ so với khung đầu, âm = đảo chiều — `gat_dau` −0,8 (bản Mixamo hất cằm lên
  23° → thành cúi 18° rồi ngẩng, kiểu gật "dạ").
- Chạy/nhảy/ngồi khai `"cham_dat": "chan"` (lúc bay không kéo người xuống đất). Động tác lặp ngắn (vẫy tay) khai
  `"giay"` mặc định.
- Thêm động tác: tải FBX theo cách trên → bỏ vào thư mục → thêm một dòng → chạy `kiem_tra_chuyen_dong.py`
  và `kiem_tra_muot.py` (chuỗi D có các động tác Mixamo).

## Mặt mô hình 3D: chớp mắt, khẩu hình, lông mày (`sgd_core/mat3d.py`)

Diễn viên có `mo_hinh_3d` tự có mặt điều khiển được, không cần làm gì thêm:
- lần đầu: chụp thẳng mặt ở tư thế nghỉ rồi dò, lưu `data/do_dac/<Ma>_mat3d.json` (dò lại khi file mô hình đổi
  hoặc cách dò đổi — `DO_PHIEN_BAN`; sửa tay thì đặt `"khoa": true`):
  - mắt: lòng trắng (ngưỡng chuẩn, lòng trắng ngả be/xám thì so với màu da của chính nhân vật), khung mắt khít
    (tự siết ngưỡng khi vùng mắt dính tóc/bóng);
  - mũi + cằm theo **hình khối** (đường nghiêng giữa mặt) → miệng chỉ tìm giữa mũi và cằm: một đường tối liền
    (sáng hơn cả trên lẫn dưới — lỗ mũi, bóng mũi không lọt), chịu được miệng cười/mếu (cong ±6 điểm ảnh);
  - lông mày: từng cột đi từ tâm mắt lên qua mi tới da rồi tới mép dưới lông mày, lần theo dáng vòm ra hai đuôi,
    gặp mảng tối dày (tóc mái) thì dừng; một bên hụt/lệch thì lấy đối xứng bên kia;
  - đã thử trên đủ 12 nhân vật (người lớn, trẻ con, tóc mái che, mắt to, miệng cười);
- mí mắt = màu da phủ từ trên xuống + đường mi (màu mí lấy da quanh mắt, bỏ mẫu dính viền mi/phấn mắt — so với da má); miệng = cằm hạ thật (shape key `Mo`) + hình miệng vẽ theo từng điểm
  ảnh (sắc nét dù lưới thưa): khe bầu dục giữa môi trên/môi dưới, răng trên, lưỡi, lòng miệng tối, viền môi dưới;
  lông mày = shape key `Nhuong`, `Lo` (dùng chung `data/bieu_cam.json` với mặt 2D);
- tự chớp mắt (lệch nhịp giữa các diễn viên). Khai trong diễn viên của cảnh thân người:
  - `"noi": true` — khẩu hình theo cả file thoại; `"noi": [1, 3]` — chỉ các câu 1 và 3 (nhiều người chung một file);
  - `"bieu_cam": [{"cau": 1, "bieu_cam": "lo", "chuyen": 0.5}]` — như cảnh mặt; `"chop_mat": false`, `"mat": false`.
- khung `"can_mat"` (cận mặt) / `"can_than"` / `"trung_than"`: máy quay tự bám nhẹ theo đầu người nói (làm mượt
  ~0,8 giây); tắt bằng `"may_theo": false`.
Đã đo trên Bố: mí khép kín 100% (lòng trắng lộ 303 → 0 điểm ảnh); khẩu hình tương quan 0,95 với âm lượng thoại, đúng
nhịp (lệch 1 khung còn 0,63); lúc im lặng miệng khép sạch. Ví dụ: `canh/tap03_c08_bo_3d.json`.

### Khẩu hình theo âm tiếng Việt (`sgd_core/khau_hinh.py`)

Khi cảnh có `"thoai": {"loi": [...]}`, mặt 3D nói theo **từng âm tiết** thay vì chỉ theo âm lượng:
- tách mỗi chữ thành phụ âm đầu / vần / âm cuối (`"tiền"` → `t` + `iê` + `n`), mỗi phần ra một dáng môi;
- 9 dáng trộn từ 5 kênh: mở (cằm + khe), kéo ngang (i/ê/e), chu tròn (o/ô/u — môi thu hẹp, nhô ra, ít răng),
  răng chạm môi (ph/v), mím (m/b/p — môi ép thành đường đậm, khép kín 2 khung);
- canh chữ vào tiếng: chia từng câu theo cụm dấu câu, cắt âm tiết tại các thung lũng âm lượng; miệng đi trước
  tiếng 1,5 khung như phim hoạt hình chuyên nghiệp; lúc nghỉ giữa câu miệng khép về dáng nghỉ;
- không có `"loi"` thì quay về khẩu hình theo âm lượng như cũ; tắt riêng một diễn viên bằng `"khau_hinh": false`.
Đã đo trên `tap03_c08_bo_3d`: 44/44 âm tiết canh được; môi khép đúng 7/7 chỗ m/b/p; môi chu 8/8 chữ o/ô/u;
răng chạm môi 5/5 chữ ph/v; miệng đi trước tiếng 1 khung.

## Thêm chuyển động

1. Lấy file động tác: Mixamo (tải FBX, chọn "Without Skin"), file .bvh quay động tác, hoặc AI tạo động tác.
2. Đặt vào `data/chuyen_dong/`, thêm một dòng vào `data/chuyen_dong.json`:
   `"dap_xe": {"file": "dap_xe.fbx", "lap": true, "mo_ta": "Đạp xe"}`.
3. Kiểm tra: `blender --background --python tools/san-gach-do/kiem_tra_chuyen_dong.py -- dap_xe`
   (áp lên 3 bộ xương khác hẳn nhau, đạt khi lệch ≤ 1% chiều cao).
Không phải sửa mã. Tên khớp lạ: thêm bảng vào `data/xuong_chuan.json`. Nguồn dùng trục Y hay Z hướng lên,
quay mặt hướng nào đều được tự dựng thẳng lại.

Bộ hiện có (bản tạm do `tao_chuyen_dong_mau.py` tạo bằng công thức): dung_tho, di, ngoi_xuong, dung_day, dua_tien,
vay_tay, gat_dau, cui_chao.

## Thêm biểu cảm

Thêm một dòng vào `data/bieu_cam.json`, trộn các dáng có sẵn:
`"hoang_mang": {"mo_ta": "Bối rối", "may": {"Lo": 0.6, "Nhuong": 0.6}}`.
Dáng có sẵn hiện tại: lông mày `Nhuong` (nhướn cả dải), `Lo` (đầu trong nâng, chụm vào).
Dáng mới (cười, nhíu giận, chu môi…) cần thêm shape key trong `sgd_core/rig.py` một lần, sau đó mọi nhân vật dùng được.

## Thoại thử

`python tools/san-gach-do/tts_vi.py ra.wav "Câu một." "Câu hai."` (cần `pip install espeakng-loader`). Giọng máy,
chỉ để thử nhịp; khi có giọng thật thì thay file `.wav` trong file cảnh, không sửa gì khác.

## Giới hạn

- Cảnh mặt: chỉ cận / trung cận nhìn gần thẳng (ô mặt chính của ảnh mẫu). Miệng theo âm lượng, chưa khép môi riêng m/b/p.
- Cảnh thân người: cần mô hình 3D có xương cho từng nhân vật (đang dùng người mẫu tạm). Mặt trên mô hình 3D chưa nói được —
  thoại vẫn để ở cảnh mặt; cảnh thân người chỉ lồng tiếng. Đạo cụ (xe đạp, quang gánh) và cầm nắm đồ vật chưa có.

## Cảnh điện ảnh: bối cảnh, nhiều góc máy, đạo cụ, trao tay

Ví dụ đầy đủ: `canh/tap03_c08_dien_anh.json` (Tập 3 cảnh 8 trên bờ đê). Các khoá mới (đều tuỳ chọn):

- `"boi_canh": {"kieu": "bo_de", "gio": "sang"|"chieu"}` — bối cảnh dựng bằng hình khối + vật liệu thủ tục
  (`sgd_core/boi_canh.py`, không cần file ngoài): mặt đê đất có vệt bánh xe, mái cỏ, ruộng lúa nước + lũy tre làng
  một bên, sông + hàng cây bên kia, cột điện + dây võng, trời và nắng sớm xiên, xa mờ dần như hơi sương.
  Mặt đê ở z = 0, chạy dọc trục X — đặt diễn viên trên đó.
- `"canh_quay": [...]` — mỗi cảnh một máy, tự cắt theo mốc (giây hoặc `{"cau": k, "lech": giây}`):
  `toan`, `hai_nguoi`, `qua_vai` (`"sau"`: vai tiền cảnh, `"nhin"`: người được quay), `can`, `tay` (cận đạo cụ).
  Máy luôn ở cùng một phía đường nối hai nhân vật (luật 180°); cận/qua vai có xoá phông (`"xoa_phong"`: f-stop),
  bám nhẹ theo đầu (`"theo"`), `"day_vao"` cho máy tiến chậm vào mặt lúc cảm xúc dâng.
- `"do_vat"` — `"tien"` đi theo bàn tay người cầm, đổi tay ở mốc `"tu"`; `{"gan_nhat": [t0, t1], "giua": [i, j]}`
  = đúng lúc hai bàn tay gần nhau nhất, `"chuyen": 8` = tiền đi mượt sang tay người nhận trong 8 khung.
  `"ba_lo"` đeo sau lưng một diễn viên (theo ngực, ẩn/hiện cùng người đeo).
- Diễn viên `"voi_tay": [{"toi": j, "tu": ..., "den": ..., "vao": 0.4}]` — tay vươn tới đúng bàn tay người j
  (IK vai–khuỷu–cổ tay, vào/ra mềm, khuỷu không chỉa lên): người nhận đồ chạm tay người đưa thật.
- Diễn viên `"hien": [t0, t1]` — chỉ hiện trong khoảng đó. Dùng để **cắt trên hành động**: hai bản cùng một nhân
  vật (vd Minh đối diện Bố tới cú cắt, sau cú cắt là Minh đã quay lưng bước đi) — người xem không thấy chỗ đổi.

## Mặt giống ảnh gốc + bàn tay đúng cỡ

Mô hình Meshy dựng từ ảnh toàn thân (mặt chỉ ~75 điểm ảnh) nên mặt tự đoán, không giống ảnh mặt gốc; bàn tay to
hơn ảnh gốc ~20%. Hai việc sửa, áp tự động mỗi lần nạp mô hình (`body.load_model`):

- **Bàn tay**: thu lưới bàn tay quanh cổ tay về 10,5% chiều cao (như bộ ảnh gốc), mềm dần lên cẳng tay.
- **Chiếu mặt gốc** (`sgd chieu_mat [Ma ...]`, chạy một lần sau khi có mô hình):
  1. 68 điểm mốc mặt (đường hàm, lông mày, mắt, mũi, miệng) trên ô chính diện của `<Ma>_mat.png` và trên ảnh chụp
     thẳng mặt mô hình — dò sẵn bằng `moc_mat.py` (OpenCV LBF), lưu `data/do_dac/<Ma>_moc_mat.json`;
  2. nắn hình khối mặt theo tỉ lệ ảnh gốc (giữ cỡ đầu, mỗi mốc dời tối đa 35% khoảng cách hai mắt) + làm mịn lòng
     mặt (bỏ rãnh sâu, mặt gồ ghề kiểu nặn tự động; gộp đỉnh trùng ở đường nối ảnh da nên không rách) ->
     `data/mo_hinh/<Ma>_mat_hinh.npy`;
  3. vẽ lại da mặt bằng ảnh gốc uốn theo 68 mốc (mép mờ dần, chừa tóc, chỉnh màu theo da) -> `<Ma>_da_mat.png`;
  4. ảnh so sánh trước | sau | ảnh gốc: `<Ma>_chieu_mat.png`.
  Miệng (khẩu hình) đặt theo 68 mốc đo trên mặt đã chiếu; mắt/lông mày (mí chớp) vẫn theo bộ dò màu.
  Muốn bỏ: xoá `<Ma>_mat_hinh.npy` và `<Ma>_da_mat.png`. `--chi-da`: chỉ vẽ da, không nắn hình.
  Mô hình mới: chạy lại `moc_mat.py` (cần Python có `opencv-contrib-python`, mô hình `lbfmodel.yaml`) rồi `sgd chieu_mat`.


## Đầu riêng theo ảnh gốc (ghép đầu)

Thân Meshy giữ nguyên (xương, dáng đi); phần đầu thay bằng đầu dựng riêng từ ảnh chân dung cho giống ảnh gốc.

1. `sgd dau_3d Ma --nguon rodin` (máy có FAL_KEY) -> `data/mo_hinh/<Ma>_dau_rodin/base_basic_shaded.glb`.
   Rodin giống ảnh gốc hơn Meshy (mặt, mũi, cằm); bản "Shaded" nướng ánh sáng vào màu phát sáng,
   bước 2 chuyển về màu nền.
2. `python3.11 sua_dau_3d.py Ma` (máy có bpy + OpenCV contrib + mô hình LBF; `SGD_LBF`, `SGD_HAAR`):
   tô lại tóc tối / bớt vàng da, nắn hình theo 68 mốc ảnh gốc (phía trước mặt, nhạt dần về sau gáy),
   tô mống mắt theo màu mắt ảnh gốc, giảm lưới còn ~50 nghìn tam giác -> `data/mo_hinh/<Ma>_dau.glb`.
   `--da_min` (nhân vật trẻ): là phẳng rãnh nhỏ trên hai má + lấp nét sậm trên da (Rodin hay khắc tóc mai lên má).
   Ảnh vào `anh_vao/<Ma>_chan_dung.png` = ô chính diện bình thường (trên cùng bên trái) của `<Ma>_mat.png`,
   đệm vuông nền xám, 1024x1024.
3. Có `<Ma>_dau.glb` thì `load_model` tự ghép (`sgd_core/ghep_dau.py`): khớp cằm + cỡ theo đầu cũ,
   cắt phẳng ngay dưới cằm (đầu mới bỏ cổ áo tượng bán thân; thân giữ cổ sạch), kéo đỉnh cổ thân cho
   mép trùng mép cắt đầu mới, trọng số cổ -> 100% xương đầu, nhập làm một lưới. Màu da thân (cổ,
   bàn tay, bàn chân) nhân theo màu da đầu mới trong vật liệu (thuộc tính `sgd_da`), áo không đổi.
   Khi đó bỏ qua `_mat_hinh.npy` / `_da_mat.png` / mốc `_moc_mat.json` của mặt cũ; mat3d tự đo lại
   (mí, khẩu hình chạy trên vật liệu đầu mới). Bỏ ghép: xoá / đổi tên `<Ma>_dau.glb`.
4. Mặt 3D có thêm dáng `NhamMi` (mắt nhắm: vùng mắt căng phẳng như mí thật, chỉ khi khép quá nửa) và
   `MepHa` (khoé miệng trễ, môi dưới mếu nhẹ — gỡ nụ cười sẵn có của mặt). `bieu_cam.json`: lo, lo_nhe, nghen,
   buon dùng MepHa. Động tác mới `dung_cui_dau` (đứng thở, cúi mặt).


## Âm thanh cảnh (am_thanh)

`"am_thanh": {"nen": "dong_que", "am_luong_nen": 0.12, "nhac": "buon_nhe" | "duong/dan/nhac.wav", "nhac_tu": 9.0,
"am_luong_nhac": 0.16, "ha_nhac_khi_thoai": 0.45}` trong file cảnh: trộn thoại + âm nền (gió, chim xa, lá — tổng hợp)
+ nhạc nền (tổng hợp tạm hoặc file có bản quyền), tự hạ nhạc khi có lời, ra `<cảnh>_am_thanh.wav` rồi ghép vào mp4.
Chỉ dùng numpy + wave (chạy được trong Blender trên máy người dùng).

## Render từng đoạn (máy ít RAM)

`set SGD_DOAN=1-100` rồi `sgd canh ...`: chỉ render khung 1..100, giữ khung đã có; đoạn cuối đủ khung thì tự ghép mp4.

## Màu và chớp mắt

Bối cảnh bờ đê: màu Standard + "Medium High Contrast", nắng sớm ấm hơn, trời bớt sáng (AgX làm xám trời nên không
dùng). Chớp mắt ~0,37 giây (khép nhanh, mở chậm hơn). Tờ tiền: xanh lục đậm ngả ô liu, hoa văn loang, viền sáng.

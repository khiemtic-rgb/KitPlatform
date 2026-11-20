# NOVIXA Intro — khôi phục định hướng hình

Bản để xem:

`proof/kit-video-production-v1/outputs/novixa-intro-film/20261023T131329Z-visual-direction-recovery/preview.mp4`

V3 và V4 giữ nguyên. Kịch bản và 12 WAV V3 không đổi. `finalVideo` vẫn null. `durationLocked` vẫn false. Đây không phải phim phát hành, và không phải footage 3D đã duyệt.

## Chuẩn đã xác minh

Chuẩn tạo hình đã được ghi, nhưng chưa được duyệt, nằm ở Character Bible và `visual-standard-v1.json`. Trạng thái của bộ chuẩn là `AWAITING_REVIEW`.

Nhân vật là dược sĩ hoạt hình 3D: mắt nâu lớn có điểm sáng, tóc búi khối, áo blouse trắng, áo mint, thẻ tên trống, ánh sáng trắng. Nguồn mặt là `assets/pharmacist-nova-cartoon-v3.jpg`. Tấm nhà thuốc cùng nhân vật, đủ 1280×720, là `keyframes/v3/s01-pharmacy.jpg`.

Không dùng làm chuẩn:

- `pharmacist-nova-style.jpg` và keyframe v1, v2: bán hiện thực, bible giữ làm lịch sử.
- Style proof hình học `outputs/novixa-style-proof/20261023T021435Z-style-proof/video.mp4`.
- Ảnh DOC-008.
- Bảng `keyframes/v7/s05-inventory.jpg`: có số lượng và hạn dùng giả.
- Nhân vật Famixa.
- Bốn tư thế trong character reference V2: chưa được tạo, trạng thái `BLOCKED`.

## Tài sản dùng trong bản này

Đã duyệt và dùng nguyên file: logo `docs/novixa/assets/brand/novixa-logo.png`.

Tham chiếu, chưa duyệt, thuộc quảng cáo nhà thuốc:

- `keyframes/v3/s01-pharmacy.jpg` là bối cảnh và chân dung chính.
- `probes/character-reference-v1/f07-lift.jpg` và `f08-point.jpg` là hai tư thế có sẵn. Ảnh gốc khoảng 249×279 pixel. Chữ hàm lượng trên hộp bị che bằng mặt hộp trắng. Chữ chú thích dưới khung bị cắt.

Đồ họa sản phẩm là lớp vẽ mới, có nhãn `Dữ liệu minh họa` khi nhìn như một màn làm việc. Không có số lô, hạn, doanh thu hay kết quả báo cáo.

## Đã kiểm trên video

Mở khung hình sau khi render. Nhân vật trên các cảnh là cùng một dược sĩ 3D, đứng trong nhà thuốc có kệ và chiều sâu. Không còn người hình khối.

S05 đặt logo gốc và một sơ đồ minh họa cạnh dược sĩ. S06 cắt từ tư thế cầm hộp trắng sang phiếu trắng đi tới một chồng phiếu. S07 lần lượt là kệ, lịch không ngày, rồi ba nhóm không số: còn trên kệ, cần theo dõi, cần kiểm tra. S08 đặt ba thẻ Quầy, Kho, Theo dõi ở hai bên, đường nối không cắt mặt. S09 là tư thế chỉ vào hộp trắng. S11 và S12 giữ logo, câu định vị và `novixa.vn` giữa hai dải kệ.

Tiếng AAC 48 kHz mono dài 198,145 giây, khớp 12 WAV V3, tương quan 0,99999. Hình H.264, 1280×720, 30 fps. Không nhạc.

Nguồn tạo lại: `outputs/novixa-intro-film/_build_visual_direction_recovery.py`.

## Giới hạn

Bản này ghép ảnh tĩnh. Không có video nhân vật, không có bước đi, không có khách, không có nhân viên thứ hai, không có cửa nhà thuốc. Hai tư thế cầm hộp và chỉ hộp được phóng từ ảnh nhỏ, nên mềm hơn tấm quầy. Hai bên của hai tư thế đó là phần kéo mờ của chính ảnh, không phải một cú máy.

Tấm với tay lên kệ (`f05-reach.jpg`) không được đưa vào vì chữ danh mục trên kệ không phải nhãn đã kiểm, và che chữ làm gãy kệ.

Muốn đi tiếp trong đúng chuẩn này cần:

1. Duyệt hoặc từ chối tấm quầy v3 và chân dung cartoon v3 cho phim intro.
2. Ảnh mới cùng nhận diện, đủ độ phân giải phim: đi từ kệ ra quầy, với tay tới hộp không chữ liều, khách cùng ngôn ngữ 3D, và một nhà thuốc rộng hơn. Không lật ảnh để tạo người mới.
3. Chuyển động nhân vật chỉ làm sau khi những ảnh đó được duyệt. Máy này không có Blender. Không gọi provider trả phí trong lần này.

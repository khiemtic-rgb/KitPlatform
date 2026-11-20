# NOVIXA Intro — chỉnh hình V4

Bản để xem:

`proof/kit-video-production-v1/outputs/novixa-intro-film/20261023T101237Z-visual-preview-v4/preview.mp4`

V3 giữ nguyên tại `20261023T100048Z-visual-preview-v3/preview.mp4`. Kịch bản và 12 WAV V3 không đổi. Đây vẫn là preview sáng tạo. `finalVideo` vẫn null. `durationLocked` vẫn false.

## Đã kiểm

Mở khung hình của các cảnh đã sửa, không chỉ đọc log. Đủ 12 chương đúng thứ tự. Tiếng AAC 48 kHz mono dài 198,145 giây, khớp 12 WAV V3, tương quan 0,99999. Hình H.264, 1280×720, 30 fps. Không nhạc. Không dùng ảnh DOC-008. Không có số lô, hạn, doanh thu hay kết quả báo cáo.

Nguồn tạo lại là `outputs/novixa-intro-film/_build_visual_preview_v4.py`. Cùng pipeline với V3: một luồng hình và một lần ghép PCM.

## Cải tiến nhìn thấy được

Nhân vật đứng đủ người trong nhà thuốc. Quầy chỉ che phần chân. Khách đứng ngoài quầy.

S01: cửa mở, dược sĩ đi từ kệ ra quầy, khách vào sau. S02: thêm nhân viên cạnh kệ, phiếu trắng và điện thoại trên quầy, chồng hộp không nhãn, khách đứng ngoài quầy. S03 và S04 giữ lịch trắng, chồng hộp, két không số và vòng khoanh nhóm; người đứng trong khoảng trống, không đè lên hộp.

S05–S08 kể quy trình bằng hành động, vẫn là minh họa khái niệm. S05: hộp trên quầy, logo gốc, rồi khung gồm hộp, phiếu trắng và ba điểm nối. Không phải ảnh POS. S06: hộp đi tới quầy và có vạch quét, sau đó phiếu trắng bay phía trên đầu người vào một chồng phiếu. S07 lần lượt là kệ, lịch không ngày, rồi cân không số. S08 nối quầy, kệ và tờ kẻ dòng phía trên người, không có số báo cáo.

S09 là dược sĩ và khách đối diện trong cùng nhà thuốc. S10 đặt ba câu chữ ngoài đầu người và ngoài cửa. S11 và S12 giữ chuyển động logo đã ổn ở V3.

## Giới hạn

Nhân vật và nhà thuốc vẫn là hoạt hình vẽ bằng code, cùng một kiểu hình. Đây không phải footage quay hoặc video nhân vật đã duyệt. Phần sản phẩm là minh họa hành động, không phải giao diện NOVIXA đã được chấp thuận.

Muốn thành phim cuối vẫn cần footage chuyển động đã duyệt cho nhà thuốc và dược sĩ, cùng quyết định có dùng ảnh giao diện thật hay không.

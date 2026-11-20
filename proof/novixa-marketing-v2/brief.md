# NOVIXA marketing V2

Phim dọc khoảng một phút, dựng local bằng FFmpeg. Không ghi artifact vào Production OS và không tạo binding ACCEPTED.

## Mục tiêu

Kể một mạch: nhà thuốc vẫn bán được, nhưng doanh thu, hàng tồn, lợi nhuận và dòng tiền chưa rõ; NOVIXA gom thông tin vận hành để người quản lý nhìn được việc cần xử lý.

## Giới hạn đã chốt trước khi dựng

- Không có công cụ animation local đã được nghiệm thu. Cảnh nhân vật là ảnh tĩnh cắt cảnh, không phải chuyển động được tạo ra.
- Không có ảnh hoặc footage nhà thuốc thật với quyền sử dụng đã xác nhận trong `docs/novixa`. Cảnh quầy là ảnh dựng, không phải nhà thuốc thực tế.
- Không đổi cách viết `NOVIXA`. Chưa có bản nghe đối chiếu cách đọc.
- Ảnh dashboard nguồn là 2880×1800. V2 chỉ ghép các vùng số liệu còn nguyên vào khung dọc.

## Nguồn giữ nguyên

- `proof/novixa-marketing-v1/`
- Shot E2E `EP01-SC01-SH01`, file `src/KitPlatform.Api/App_Data/kit-video-i2v/11111111111111111111111111111105/os/1a5e640dd8d4401db2ec6f005abdb444.mp4`
- Famixa build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`, shot `EP01-SC01-SH02`

"""Sân Gạch Đỏ — các lớp dùng chung cho cảnh Blender cận mặt.

measure : tự đo mắt / môi / lông mày trên ảnh mẫu, lưu ở data/do_dac (sửa tay + "khoa": true để giữ)
rig     : dựng mặt (mí, miệng, lông mày) từ ảnh mẫu + số đo
anim    : thở, nghiêng đầu, chớp mắt, miệng theo thoại, biểu cảm theo thời gian
stage   : cài đặt render, nền, cỡ cảnh, xuất khung, ghép tiếng
shot    : đọc một file cảnh (canh/*.json) và chạy cả chuỗi trên
"""

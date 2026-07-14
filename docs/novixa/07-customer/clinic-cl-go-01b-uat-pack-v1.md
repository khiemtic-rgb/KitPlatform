# CL-GO-01b — UAT pack (PK khách thật)

**Mã:** CL-GO-01b · **Neo:** [novixa-clinic-gd1-brief §8.1](../03-solution/novixa-clinic-gd1-brief.md)  
**Trạng thái nền tảng:** Ready for customer UAT · 2026-07-14 (Clinic P0 + mig 131 + API/Admin prod; Success EP02 riêng)

> Mục tiêu: khách xác nhận **tắt app lịch / hồ sơ / kê đơn cũ**. Lab DEMO_CLINIC (CL-GO-01) không thay được sign-off này.

---

## 0. Prod đã sẵn

| Thành phần | URL / ghi chú |
|------------|----------------|
| Admin | https://admin.novixa.vn |
| API | https://api.novixa.vn |
| Clinic vertical | Tenant PK thật (không dùng DEMO_CLINIC cho sign-off) |
| NT nhận đơn | Nhà thuốc Connect đã liên kết (vd. pilot NT) |

Ops xác nhận trước giờ UAT: login PK ok · module clinic bật · user `CLINIC_RECEPTION` / `CLINIC_PROVIDER` (hoặc ADMIN) · link Connect tới NT.

---

## 1. Phiên UAT (60–90 phút)

| Bước | Ai | Việc | Kết quả mong đợi |
|------|-----|------|------------------|
| 0 | Ops | Login Admin tenant PK · chọn đúng workspace clinic | Vào được màn lịch / BN / kê đơn |
| A1 | Lễ tân | Tạo lịch mới + **đổi giờ (reschedule)** | Không cần app lịch cũ |
| A2 | Lễ tân → BS | Check-in → visit → kê đơn → finalize Soft-CKS | Không cần app hồ sơ/Rx cũ |
| A3 | BS / Ops | Đơn tới NT liên kết; NT mở / nhận | Handoff Connect ok |
| A4 | Ops | Đăng nhập Lễ tân vs Bác sĩ (nếu có user riêng) | Không thấy màn thừa / 403 đúng chỗ |
| A5 | Chủ PK | Chốt ngày ngắt app cũ (hoặc chỉ đọc lưu trữ) | Ghi ngày vào bảng ký |

---

## 2. Bảng ký (copy vào email / biên bản)

| # | Tiêu chí | Pass? | Ghi chú |
|---|----------|-------|---------|
| A1 | Lịch + reschedule đủ thay app lịch cũ | ☐ | |
| A2 | Visit + Rx + finalize đủ thay app hồ sơ/Rx cũ | ☐ | |
| A3 | Đơn tới NT liên kết | ☐ | |
| A4 | Role đúng quyền | ☐ | |
| A5 | Ngày ngắt app cũ | ☐ | Ngày: ____ |

**PK:** ________ · **Tenant:** ________ · **Người ký:** ________ · **Ngày:** ________

---

## 3. Sau UAT (ops)

1. Lưu ảnh/email biên bản vào thư mục khách.  
2. Đánh dấu CL-GO-01b **Done** trong `novixa-clinic-gd1-brief.md` (§ bảng CL-GO + §8.1).  
3. Nếu fail: ghi blocker (role / Connect / UX) → hotfix trước khi customer cutover.

**Không** đóng CL-GO-01b chỉ vì smoke lab hoặc deploy xanh.

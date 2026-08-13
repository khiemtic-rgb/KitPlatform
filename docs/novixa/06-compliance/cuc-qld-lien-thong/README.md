# Bộ hồ sơ đăng ký liên thông — Cục Quản lý Dược

**Sản phẩm:** Novixa Pharmacy Management System  
**Đơn vị phát triển:** Công ty TNHH Truyền thông và Công nghệ KIT  
**Phiên bản hồ sơ:** 1.0  
**Ngày lập:** 10/07/2026  
**Liên hệ kỹ thuật:** care@novixa.vn · 0984.660.399 (Mr. Tuấn — phụ trách tích hợp)

---

## 1. Mục đích hồ sơ

Bộ tài liệu này trình bày đặc tả kỹ thuật phần mềm **Novixa** phục vụ đăng ký và được Cục Quản lý Dược — Bộ Y tế đồng ý **kết nối, liên thông dữ liệu** với **Hệ thống dược quốc gia** (CSDL Dược Quốc gia) theo các quy định hiện hành.

---

## 2. Danh mục tài liệu

| STT | Mã tài liệu | Tên tài liệu | File |
|-----|-------------|--------------|------|
| 1 | NVX-CQD-SFS-01 | Đặc tả chức năng phần mềm (Software Functional Specification) | [01-software-functional-specification-v1.md](./01-software-functional-specification-v1.md) · **Word:** [word/NVX-CQD-SFS-01.docx](./word/NVX-CQD-SFS-01.docx) |
| 2 | NVX-CQD-API-01 | Đặc tả kỹ thuật API liên thông (API Integration Specification) | [02-api-integration-specification-v1.md](./02-api-integration-specification-v1.md) · **Word:** [word/NVX-CQD-API-01.docx](./word/NVX-CQD-API-01.docx) |
| 3 | NVX-CQD-ARCH-01 | Tài liệu kiến trúc hệ thống (System Architecture Document) | [03-system-architecture-document-v1.md](./03-system-architecture-document-v1.md) · **Word:** [word/NVX-CQD-ARCH-01.docx](./word/NVX-CQD-ARCH-01.docx) |
| 4 | NVX-CQD-UAT-01 | Biên bản kiểm thử API CSDL dược (Sandbox v1.1) | [04-bien-ban-kiem-thu-api-csdl-duoc-sandbox-v1.md](./04-bien-ban-kiem-thu-api-csdl-duoc-sandbox-v1.md) · **PDF (gửi chuyên viên):** [word/NVX-CQD-UAT-01-Bien-ban-kiem-thu-API-Sandbox.pdf](./word/NVX-CQD-UAT-01-Bien-ban-kiem-thu-API-Sandbox.pdf) · **Word:** [word/NVX-CQD-UAT-01-Bien-ban-kiem-thu-API-Sandbox.docx](./word/NVX-CQD-UAT-01-Bien-ban-kiem-thu-API-Sandbox.docx) |

**Phụ lục đính kèm (nội bộ, tham chiếu kỹ thuật):**

| Phụ lục | Nội dung |
|---------|----------|
| PL-A | Map 23 trường QĐ 540 Bảng 1 → schema Novixa — [`phu-luc-a-qd540-field-map-v1.md`](./phu-luc-a-qd540-field-map-v1.md) · Word: [`word/NVX-CQD-PL-A-QD540-Field-Map.docx`](./word/NVX-CQD-PL-A-QD540-Field-Map.docx) |
| PL-B | Ảnh chụp màn hình giao diện — [`assets/screenshots/`](./assets/screenshots/) (12 file PNG) |
| PL-C | Giấy phép kinh doanh, đăng ký phần mềm (xem Mục 7) |

**Bản Word (nộp hồ sơ):** thư mục [`word/`](./word/) — chạy `.\export-word.ps1` để tái tạo.

**Trang bìa PDF:** [`word/NVX-CQD-00-Trang-bia.pdf`](./word/NVX-CQD-00-Trang-bia.pdf) — chỉ các hạng mục nộp Bộ (logo KIT, tiêu đề hồ sơ đề nghị, phần mềm Novixa, đơn vị đề nghị, địa điểm/ngày, phiên bản). Tái tạo: `node .\print\generate-cover-pdf.mjs` (cần Playwright Chromium).

**Biên bản UAT PDF:** [`word/NVX-CQD-UAT-01-Bien-ban-kiem-thu-API-Sandbox.pdf`](./word/NVX-CQD-UAT-01-Bien-ban-kiem-thu-API-Sandbox.pdf) — mẫu hành chính (quốc hiệu, số hiệu, căn cứ, kết luận, chữ ký). Tái tạo: `node .\print\generate-uat-pdf.mjs`.

---

## 3. Phụ lục ảnh chụp màn hình (PL-B)

Đã chụp và lưu tại [`assets/screenshots/`](./assets/screenshots/):

| STT | Màn hình | File | Trạng thái |
|-----|----------|------|------------|
| 1 | Đăng nhập | [`01-login.png`](./assets/screenshots/01-login.png) | ✅ |
| 2 | Dashboard tổng quan | [`02-dashboard.png`](./assets/screenshots/02-dashboard.png) | ✅ |
| 3 | POS bán hàng | [`03-pos.png`](./assets/screenshots/03-pos.png) | ✅ |
| 4 | Danh mục thuốc | [`04-drug-master.png`](./assets/screenshots/04-drug-master.png) | ✅ |
| 5 | Quản lý kho FEFO | [`05-inventory.png`](./assets/screenshots/05-inventory.png) | ✅ |
| 6 | Nhập thuốc (GRN) | [`06-grn.png`](./assets/screenshots/06-grn.png) | ✅ |
| 7 | Bán thuốc | [`07-sale.png`](./assets/screenshots/07-sale.png) | ✅ |
| 8 | Báo cáo nhập — xuất — tồn | [`08-report-nxt.png`](./assets/screenshots/08-report-nxt.png) | ✅ |
| 9 | Báo cáo doanh thu | [`09-report-revenue.png`](./assets/screenshots/09-report-revenue.png) | ✅ |
| 10 | Liên thông Cục QLD | [`10-drug-connectivity.png`](./assets/screenshots/10-drug-connectivity.png) | ✅ |
| 11 | Cấu hình API / CSDL QG | [`11-api-config.png`](./assets/screenshots/11-api-config.png) | ✅ |
| 12 | Nhật ký đồng bộ | [`12-sync-log.png`](./assets/screenshots/12-sync-log.png) | ✅ |

Chụp lại (nếu cần): `cd assets/screenshots && npm install && npm run capture` (yêu cầu API + Admin dev đang chạy).

---

## 4. Căn cứ pháp lý

| Văn bản | Nội dung liên quan |
|---------|-------------------|
| **QĐ 540/QĐ-QLD** (20/08/2018) | Chuẩn dữ liệu đầu ra Bảng 1 — mua/bán/tồn cơ sở bán lẻ |
| **QĐ 522/QĐ-BYT** | Danh mục thuốc CSDL Dược Quốc gia |
| **QĐ 777/QĐ-QLD** | Quy định kết nối liên thông cơ sở bán lẻ thuốc |
| **QĐ 228/QĐ-QLD** | Cập nhật Bảng 3 — dữ liệu đơn thuốc (roadmap) |
| **Thông tư 07/2018/TT-BYT** | GPP — quy trình bán thuốc kê đơn |
| **NĐ 54/2017/NĐ-CP** | Đơn vị tính thuốc |

---

## 5. Tóm tắt năng lực liên thông

| Hạng mục | Trạng thái Novixa v1.0 |
|----------|------------------------|
| Map 23 trường QĐ 540 Bảng 1 | ✅ Đã triển khai |
| Xuất JSON/CSV kiểm thử | ✅ API nội bộ |
| Tra cứu CSDL Dược QG (QĐ 522) | ✅ Mock + sẵn sàng chuyển Live |
| Đẩy dữ liệu lên Cổng liên thông BYT | 🔧 Connector QĐ 777 (giai đoạn triển khai) |
| Nhật ký gửi / retry / queue | ✅ Export log + kiến trúc Outbox |
| Bảo mật HTTPS, JWT, RBAC, Audit | ✅ |

---

## 6. Thông tin đơn vị phát triển

| Hạng mục | Nội dung |
|----------|----------|
| **Tên pháp nhân** | Công ty TNHH Truyền thông và Công nghệ KIT |
| **Tên viết tắt / thương hiệu** | KIT Technology · Novixa |
| **Mã số thuế** | 4601239671 |
| **Địa chỉ trụ sở** | KĐT Hồ Xương Rồng, phường Phan Đình Phùng, tỉnh Thái Nguyên |
| **Sản phẩm thương mại** | Novixa — ERP Nhà thuốc |
| **Nền tảng kỹ thuật** | KIT Platform (KitPlatform) |
| **Website** | https://novixa.vn |
| **API production** | https://api.novixa.vn |
| **Email liên hệ** | care@novixa.vn |
| **Hotline kỹ thuật** | 0984.660.399 |
| **Fanpage** | https://www.facebook.com/novixa68 |

---

## 7. Phụ lục pháp lý (PL-C)

| Tài liệu | Trạng thái | Ghi chú |
|----------|------------|---------|
| **Giấy chứng nhận đăng ký doanh nghiệp** | 📎 Đính kèm khi nộp | Bản scan PDF — MST **4601239671** |
| **Giấy phép kinh doanh lĩnh vực phần mềm** | 📎 Đính kèm khi nộp | Theo ngành nghề đăng ký trên GPKD |
| **Đăng ký bản quyền phần mềm / quyền SHCN** | ⏳ Bổ sung | Novixa Pharmacy Management System — đang hoàn thiện hồ sơ SHTT (nếu Cục QLD yêu cầu) |
| **Cam kết bảo mật dữ liệu** | ✅ Trong tài liệu SFS Mục 9 | HTTPS, JWT, RBAC, audit log |

> **Lưu ý nộp hồ sơ:** In hoặc upload kèm bản scan **GPKD** (mặt chính + phụ lục ngành nghề) và **CMND/CCCD người đại diện pháp luật** theo biểu mẫu Cục QLD hiện hành.

---

*Tài liệu nội bộ — phục vụ hồ sơ đăng ký liên thông Cục Quản lý Dược.*

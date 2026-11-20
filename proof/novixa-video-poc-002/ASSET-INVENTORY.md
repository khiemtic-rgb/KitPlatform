# Kiểm kê asset — NOVIXA video POC 002

Ngày kiểm: 2026-10-22. Không crop, không sao chép ảnh, không tải file mới, không tạo MP4.

Phạm vi đã mở: thư mục ảnh launch, logo, ảnh compliance (chỉ đo và băm, không chọn), ảnh tĩnh `pharmacy-storefront`, và tìm video trong `docs/` cùng `client/`. Không quét toàn bộ repository.

## Đã duyệt để dùng khi dựng — chỉ giao diện sản phẩm

Đây là ảnh chụp giao diện NOVIXA nằm trong repo. Không phải footage người thật. Số trên màn hình là dữ liệu demo, phải ghi chú khi xuất hiện trong video. Không có tên khách hàng trên hai ảnh được chọn.

### `docs/novixa/launch/DOC-008/assets/screenshots/01-dashboard.png`

- Nội dung đã mở: màn **Tổng quan**. Tenant `DEMO_PHARMACY`, người dùng `admin`. Thẻ **Doanh thu hôm nay** `60.058.235,08 đ`, doanh thu 7 ngày, 72 đơn. Biểu đồ danh mục. Khối **Cần chú ý**: **Sắp hết HSD (30 ngày) 8**, SP tồn thấp 336, lô tồn thấp 28.
- Khổ: 2880×1800, RGB, tỷ lệ 16:10. Dung lượng 355.745 byte.
- SHA-256: `94c726755cdfaa2b38691e3c089e92a1d39eb98325f5d12ed222b39382e544e9`
- Dữ liệu nhạy cảm: tên đăng nhập `admin`, mã tenant demo, số doanh thu demo. Không thấy tên khách hay hóa đơn.
- Phù hợp: cảnh 4. Một khung hình vừa có doanh thu hôm nay vừa có cảnh báo hết hạn, đúng câu «doanh thu không nói hộ hàng nào còn nằm… lô nào sắp hết hạn».
- Cách trình bày: crop dọc lấy thẻ doanh thu hôm nay và hàng chip «Cần chú ý». Để nguyên chữ demo. Chèn dòng «Giao diện thật · dữ liệu demo». Không che số để giả một nhà thuốc thật.
- Quyền: `INTERNAL_PRODUCT_UI`. Ảnh tài liệu sản phẩm trong repo. Không có giấy phép riêng; dùng để giới thiệu chính giao diện này.

### `docs/novixa/launch/DOC-008/assets/screenshots/08-reports-home.png`

- Nội dung đã mở: **Báo cáo — Thống kê**, lưới thẻ, chưa mở báo cáo chi tiết. **INV-01 Tồn kho & giá trị** ghi «Số lượng và giá trị tồn (qty × giá vốn lô)». **INV-02 Sắp hết hạn sử dụng**. **SALES-03 Báo cáo ca làm việc**: «Danh sách ca, quỹ tiền mặt và thu ròng trong ca».
- Khổ: 2880×1800, RGB, 16:10. Dung lượng 199.880 byte.
- SHA-256: `8b67dba78b22651d302ef23ef6c3c1ade5a24f0373bb0a6bf8015e9612290961`
- Dữ liệu nhạy cảm: nhãn `admin`. Không có số ca, số quỹ hay tên người trên các thẻ này. Thẻ mô tả báo cáo, chưa phải bảng số đã chạy.
- Phù hợp: dự phòng cho cảnh 4 nếu cần chỉ đúng tên báo cáo. Kịch bản 15 giây đã khóa không đọc «giá vốn» hay «ca làm việc», nên không bắt buộc.
- Cách trình bày: crop riêng từng thẻ INV-01, INV-02 hoặc SALES-03. Không phóng cả lưới 16:10 lên khung 9:16.
- Quyền: `INTERNAL_PRODUCT_UI`, cùng điều kiện với ảnh tổng quan.

### `docs/novixa/assets/brand/novixa-logo.png`

- Nội dung đã mở trước đó và giữ nguyên file: dấu N, chữ NOVIXA, dòng SMART PHARMACY SOLUTIONS, nền trắng.
- Khổ: 1254×1254, RGB. Dung lượng 855.023 byte.
- SHA-256: `008501e96549b05af42af24b2b8e9e068ba8c031fa57920c61632a710b32e1f4`
- Phù hợp: dấu kết, không phải một cảnh trong shot list.
- Quyền: `INTERNAL_BRAND`. Logo sản phẩm trong repo.

## Đã thấy, không duyệt

### Ảnh launch khác

Cùng thư mục `docs/novixa/launch/DOC-008/assets/screenshots/`, tất cả PNG đã băm. Không chọn vì không khớp câu chuyện hoặc dễ lộ dữ liệu khách.

| File | Khổ | SHA-256 | Lý do để ngoài |
|---|---|---|---|
| `02-catalog-products.png` | 2880×1800 | `4cdacdc58a73fd31d06ac5bcde19db0b0b60e2605f570f51fa5138317f19ad4e` | Danh mục, không phải cảnh đóng cửa |
| `03-procurement-grn.png` | 2880×1800 | `4a4dc7975a1d6505e727514c9d9a88f7dc95a3bc3fe91efe28f01efaaa255aa4` | Nhập hàng |
| `04-inventory-stock.png` | 2880×1800 | `572a90cb61cd8bc1b1de4ef726a9f6bc4c744564dfa7d56b35445bb475a6e6f2` | Bảng tồn theo mã và số lượng. Đã mở ở POC 001: không phải màn giá trị tồn theo giá vốn |
| `05-sales-pos.png` | 2880×1800 | `8044bb9cd92c3567f54edfccacedc29fc21c69d5b1dff7815471b7e6214b81b3` | Quầy bán trên phần mềm, không thay footage người |
| `06-customer-crm.png` | 2880×1800 | `a48656e4f3de8e78ea95349a811ef931aa74ff8a700bf0f3dec6122b150e8646` | Màn khách hàng. Không mở để chọn. Không đưa vào video |
| `07-o2o-drafts.png` | 2880×1800 | `0adc840a8d7ef4acbb1774cf20eb655f225358fa8ce59410f6dcd96fc0eecd9c` | Đơn nháp |
| `09-customer-app-home.png` | 780×1688 | `b444ec56ef15894fb1d41d67a0d75a178d1b2099733a460b3a33188e298fc36e` | App khách |
| `10-staff-pos.png` | 780×1688 | `360d28e1ab42c07bf3d37f1d40bcf0feb5f1c02abcd6168a9db59200b6668af9` | POS nhân viên |

### Ảnh compliance

`docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/` có 12 PNG 2880×1800, trừ nhóm đã băm. `10-drug-connectivity.png` và `12-sync-log.png` trùng hash `8d8db1d553d74cb9ae63e1c5335718fe5534e7f285df6196a85f1f346c191349`. Không mở nội dung và không chọn. Bộ này phục vụ hồ sơ liên thông, không phải footage.

### Ảnh tĩnh storefront — `RIGHTS_UNVERIFIED`

`client/pharmacy-storefront/public/brand/about/`. Trang giới thiệu (`client/pharmacy-storefront/src/tenants/xuanhoa.ts`) gắn alt «Nhà thuốc Xuân Hòa». Trong repo không có giấy phép, không có văn bản nhà thuốc giao ảnh, không có giấy người được nhận diện.

| File | Khổ | SHA-256 | Đã nhìn thấy | Vì sao không duyệt |
|---|---|---|---|---|
| `about-gallery-01.jpg` | 1200×800 | `1a63b2e2b5bdbdb7aa6184d617588875d54cd5abe18039d09aeb33fbcb9181ae` | Mặt tiền ban ngày, biển hiệu bị làm mờ | Không phải giờ đóng cửa. Nguồn chưa chứng minh |
| `about-gallery-02.jpg` | 1200×800 | `550bbe2ee7bd2f17e5f3c343f870a1ce78e617b1d266130f81a230feef4b4678` | Lối đi giữa kệ kính | Ảnh tĩnh, ngang, không có người kiểm hàng. Nguồn chưa chứng minh |
| `about-gallery-03.jpg` | 1200×800 | `4c8cfd1ddda3a657ee563a0abc2dc992f708865714833270c48d61cdf88adad1` | Dược sĩ và khách tại quầy | Có người nhận diện được. Sai cảnh «sau giờ bán» |
| `about-team-banner.jpg` | 1200×800 | `9f2bad4df3dbbedc3fc20194d87e3afb9ffc77fcbf99f93ed5110d3ab9ac86cf` | Bốn người áo blouse cười trước kệ | Ảnh nhóm, không có giấy xuất hiện |

`about-cert-01.jpg`, `about-cert-02.jpg`, `about-cert-03.jpg` là file giấy phép/chứng nhận theo tên trong tenant. Không mở và không dùng.

Ảnh bài viết và ảnh vỏ sản phẩm trong `public/brand/articles/` và `public/brand/products/` (Panadol, Hapacol, Berocca, Eugica, Accu-Chek, Wellwoman) là ảnh hàng hóa của hãng khác. Không có giấy dùng cho video marketing. Không chọn.

Ảnh `client/customer-app/public/home/` (avatar, `hero-meds.jpg`, `ai-robot.jpg`) không phải quầy nhà thuốc. Không chọn.

## Footage

Tìm `mp4`, `mov`, `webm`, `mkv` trong `docs/` và `client/`: không có file. Không có clip người đóng cửa, sổ cuối ngày, hay kệ thuốc.

# Báo cáo chuẩn bị asset — POC 002

## Trạng thái repo

- Branch: `recovery/production-source-20261021`
- HEAD: `8c59ceb4f3be537898106ec058ea547406f722e5`
- Working tree bẩn từ trước. `ContentVideoService.cs` đang sửa. `MarketingLocalVideoRenderer.cs`, `proof/novixa-video-poc-001/` và `proof/novixa-video-poc-002/` chưa theo dõi.
- Không commit, push, merge, deploy, migration. Không gọi API tính phí. Không tạo MP4. Không sửa POC 001, renderer hay `ContentVideoService`.

## Đã kiểm

- `docs/novixa/launch/DOC-008/assets/screenshots/` — 10 PNG. Mở `01-dashboard.png` và `08-reports-home.png`. Băm toàn bộ.
- `docs/novixa/assets/brand/novixa-logo.png` — logo nội bộ.
- `docs/novixa/06-compliance/cuc-qld-lien-thong/assets/screenshots/` — 12 PNG, chỉ băm, không chọn.
- `client/pharmacy-storefront/public/brand/` — mở bốn ảnh about. Không thấy giấy phép cạnh file hay trong `xuanhoa.ts`.
- `docs/` và `client/`: không có `mp4`, `mov`, `webm`, `mkv`.

## Việc đã chọn

Duyệt cho bước dựng sau này, khi đã có footage:

- `01-dashboard.png` cho cảnh 4. SHA-256 `94c726755cdfaa2b38691e3c089e92a1d39eb98325f5d12ed222b39382e544e9`.
- `08-reports-home.png` chỉ là phương án phụ. SHA-256 `8b67dba78b22651d302ef23ef6c3c1ade5a24f0373bb0a6bf8015e9612290961`.
- Logo cho nốt kết, không phải một cảnh.

Không duyệt ảnh Xuân Hòa trên storefront. Caption trên site không phải giấy quyền. Ảnh có người nhận diện được và biển hiệu bị làm mờ.

## Khoảng trống

Cảnh 1, 2 và 3 không có clip đã xác minh quyền. Ảnh kệ `about-gallery-02.jpg` không đủ để mở cảnh 3.

## Kết luận

`BLOCKED — WAITING FOR LICENSED FOOTAGE`

Được chuyển sang dựng khi có đủ:

1. Ba clip trong `FOOTAGE-CAPTURE-GUIDE.md`, cùng một nhà thuốc, cùng một người, cùng buổi tối đóng cửa.
2. Câu xác nhận của người xuất hiện và của chủ nhà thuốc cho phép dùng trong video marketing.
3. Giữ nguyên cảnh 4 bằng `01-dashboard.png`, có dòng dữ liệu demo.

Thiếu một trong ba điều trên thì chưa dựng. Không thay người và quầy bằng giao diện hay bằng ảnh chưa rõ quyền.

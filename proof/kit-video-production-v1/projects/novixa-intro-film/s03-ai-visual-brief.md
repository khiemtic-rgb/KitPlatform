# S03 — brief hình AI_VISUAL (chưa gọi API)

Cảnh: `s03-stock-cash`. Thông điệp đã duyệt: hàng nằm yên là vốn bị khóa. Hai nhóm: ít hàng bán nhanh, nhiều hàng nằm yên. Không có số tiền.

Trạng thái brief: chuẩn bị để duyệt. Chưa tạo ảnh. Chưa gắn asset. `product_source_missing` vẫn còn. `finalVideo` vẫn null. `durationLocked` vẫn false.

## Vì sao chưa gọi

- Model trong catalog Content: `gemini-3.1-flash-image`. `gemini-2.5-flash-image` đã nghỉ, không dùng làm fallback.
- Đơn giá trong `famixa-ai-provider-cost.ts` là **ước tính** `usdPerImage: 0.045`, `costKind: ESTIMATE`, `billingBasis: PUBLISHED_LIST_0_5K`. Đây không phải hóa đơn hay báo giá đã xác minh cho pipeline Intro.
- Pipeline Intro chưa có adapter ảnh. `execute_still` với provider generated vẫn `NOT_INTEGRATED` và không gửi request.
- Chưa có phê duyệt gọi API và chưa có trần chi được chấp nhận.

Đề xuất khi được phép: **một** ảnh, trần chi bằng đúng một lần ước tính catalog (0,045 USD) cho đến khi có đơn giá đã xác minh. Vượt trần thì dừng. Không gọi trong nhiệm vụ này.

## Prompt đã chốt để duyệt

Dùng cùng hướng với `visual-style-guide.md`. Không prompt riêng tùy hứng.

```
Stylized 3D still of a small bright pharmacy stock room, same soft daylight and clean white-and-teal shelving as a modern pharmacy illustration. No people.

Left and center: a large, dense, still mass of plain white medicine boxes and bottles that have clearly been sitting. A few boxes are slightly dusty in the light. This group is the heavy subject.

Right: only two small, neat stacks of plain white boxes, set apart, with a little clear space around them, as the fast-moving goods.

No letters, no numbers, no logos, no barcodes, no prices, no brand names, no expiry dates, no lot codes, no currency, no screens, no cash register, no readable labels of any kind. Boxes are blank. Warm, trustworthy, calm. 16:9.
```

Chữ tiếng Việt `Hàng tồn kho cũng là vốn` sẽ do lớp code đặt sau, không nằm trong ảnh.

## Review trước khi gắn vào cảnh

- Đọc được ngay sự khác giữa khối hàng nằm yên và nhóm bán nhanh, ở kích thước 1280×720.
- Không có chữ, số, giá, tên thuốc.
- Cùng chất stylized với `pharmacy-plate`. Không phải screenshot POS và không phải ảnh quầy thật.
- Không có người mới khác `pharmacist-portrait`.
- Hash sha256, `visualSource: AI_VISUAL`, `role: product`, quyền sử dụng là một câu cụ thể, `approval` vẫn `proposed` cho đến khi người duyệt.
- Không hạ `product_source_missing` trước khi asset đó được gắn và hash khớp.

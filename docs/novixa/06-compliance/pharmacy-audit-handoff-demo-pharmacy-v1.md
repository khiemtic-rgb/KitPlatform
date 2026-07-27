# Gói handoff thẩm định — Phân hệ Pharmacy (DEMO_PHARMACY)

**Ngày:** 2026-07-27  
**Phạm vi:** Chỉ tenant `DEMO_PHARMACY` — **không** dùng / không đụng `NT_XUANHOA` (đang vận hành thật).  
**Seed:** `migrations/237_demo_pharmacy_audit_handoff.sql`

---

## 1. Môi trường & tài khoản test

| Hạng mục | Giá trị |
|---|---|
| Admin SPA | https://admin.novixa.vn |
| POS | https://pos.novixa.vn |
| Customer app | https://app.novixa.vn |
| API base | https://api.novixa.vn |
| Health | https://api.novixa.vn/api/health |
| Tenant code | `DEMO_PHARMACY` |
| Ghi chú môi trường | Cùng hạ tầng production, **tenant demo tách biệt**. Dữ liệu gắn nhãn “thẩm định / demo”. |
| UI slim | `features.audit_slim_nav=true` — xem §5 (ẩn menu ngoài phạm vi thẩm định; bật lại sau khi xong) |

### Tài khoản UI

| Vai trò | Username | Password | Mục đích |
|---|---|---|---|
| Administrator | `admin` | `Admin@123` | Thiết lập, phân quyền, danh mục thuốc, báo cáo tồn, cấu hình |
| Dược sĩ / Nhân viên | `pharmacist` | `Admin@123` | Bán hàng POS, nhập kho, thao tác đơn thuốc |

Đăng nhập Admin/POS: chọn / nhập tenant **`DEMO_PHARMACY`**, rồi username + password.

> Mật khẩu demo cố định — chỉ dùng cho thẩm định. Đổi ngay sau khi kết thúc đợt test nếu cần.

### ID tham chiếu cố định (seed)

| Đối tượng | UUID |
|---|---|
| Tenant | `11111111-1111-1111-1111-111111111101` |
| Chi nhánh HN01 | `11111111-1111-1111-1111-111111111201` |
| Kho chính WH_MAIN | `22222222-2222-2222-2222-222222222201` |
| Paracetamol 500mg | `66666666-6666-6666-6666-666666666601` |
| Đơn vị Viên (PARA) | `77777777-7777-7777-7777-777777777701` |
| Amoxicillin 500mg | `66666666-6666-6666-6666-666666666603` |
| Đơn vị Viên (AMOX) | `77777777-7777-7777-7777-777777777704` |
| NCC001 | `88888888-8888-8888-8888-888888888801` |
| Đơn Rx mẫu RX-DEMO-001 | `a237c011-cccc-4ccc-8ccc-cccccccccc11` |

---

## 2. Dữ liệu mẫu đã nạp

### Danh mục thuốc (đã map mã CSDL Dược QG — mock catalog)

| Mã nội bộ | Tên | `national_drug_id` | Số ĐK | Loại | Tồn (lô) |
|---|---|---|---|---|---|
| PARA500 | Paracetamol 500mg | `DRUG-VN-000001` | VD-1234-23 | OTC | LOT2026A: 1000; LOT2027B: 500 |
| PARA_EXTRA | Paracetamol Extra | `DRUG-VN-000004` | VD-3456-24 | OTC | LOT2026C: 300 |
| AMOX500 | Amoxicillin 500mg | `DRUG-VN-000002` | VD-5678-22 | Rx | LOTAMOX01: 400 |
| VITC1000 | Vitamin C 1000mg | `DRUG-VN-000003` | VD-9012-21 | OTC | LOTVITC01: 200 |

Barcode PARA: `8934567890012` (dùng nhanh trên POS).

> **Lưu ý kỹ thuật:** Production hiện `NationalDrugCatalog:Mode = mock` (catalog nội bộ chuẩn QĐ 522 field-map). Mã `DRUG-VN-*` là mã sandbox/mock đã gắn sẵn trên SKU demo để test ánh xạ / xuất liên thông — chưa phải live CSDL QG.

Đơn thuốc mẫu: `RX-DEMO-001` (đã verify) kê AMOX500 × 20 viên cho khách `0909123456`.

---

## 3. API Docs & endpoint

| Hạng mục | Giá trị |
|---|---|
| OpenAPI (file) | `client/admin/openapi/swagger.json` (xuất bằng `scripts/export-openapi.ps1`) |
| Swagger UI live | Chỉ bật ở Development (`http://localhost:5290/swagger`) — **không** public trên prod |
| API test base | `https://api.novixa.vn` |
| Auth | `POST /api/auth/login` → Bearer JWT |

### Đăng nhập

```http
POST /api/auth/login
Content-Type: application/json

{
  "tenantCode": "DEMO_PHARMACY",
  "username": "admin",
  "password": "Admin@123"
}
```

**Response (rút gọn):**

```json
{
  "accessToken": "<jwt>",
  "accessTokenExpiresAt": "2026-07-27T12:00:00+00:00",
  "user": { "username": "admin", "tenantCode": "DEMO_PHARMACY" }
}
```

Header cho mọi API sau: `Authorization: Bearer <accessToken>`

WarehouseId dùng trong mẫu: `22222222-2222-2222-2222-222222222201`

---

## 4. Mẫu Request / Response — 5 luồng

### 4.1 Nhập kho (Goods Receipt)

Tạo phiếu nhập (có thể không gắn PO; bắt buộc `vatTreatmentId` — lấy `GET /api/procurement/vat-treatments`):

```http
POST /api/procurement/goods-receipts
```

```json
{
  "purchaseOrderId": null,
  "supplierId": "88888888-8888-8888-8888-888888888801",
  "warehouseId": "22222222-2222-2222-2222-222222222201",
  "receiptDate": "2026-07-27",
  "notes": "Audit handoff GRN",
  "supplierInvoiceNumber": "HD-DEMO-001",
  "vatTreatmentId": "<uuid-vat_5-or-kct>",
  "items": [
    {
      "productId": "66666666-6666-6666-6666-666666666601",
      "productUnitId": "77777777-7777-7777-7777-777777777701",
      "batchNumber": "LOT-AUDIT-01",
      "expiryDate": "2029-12-31",
      "quantity": 50,
      "unitCost": 350
    }
  ]
}
```

**Response:** `201` — body có `id`, `grnNumber`, `status` (draft).

Hoàn tất nhập (cộng tồn):

```http
POST /api/procurement/goods-receipts/{id}/complete
```

**Response:** `200` — `status` completed; tồn lô tăng.

---

### 4.2 Bán không theo đơn (OTC / POS)

Mở ca (nếu cần):

```http
POST /api/sales/shifts/open
{ "warehouseId": "22222222-2222-2222-2222-222222222201" }
```

Tạo + hoàn tất bán ngay:

```http
POST /api/sales/orders
```

```json
{
  "warehouseId": "22222222-2222-2222-2222-222222222201",
  "customerId": null,
  "priceType": 1,
  "saveAsDraft": false,
  "items": [
    {
      "productId": "66666666-6666-6666-6666-666666666601",
      "productUnitId": "77777777-7777-7777-7777-777777777701",
      "quantity": 2
    }
  ],
  "payments": [
    { "paymentMethod": 1, "amount": 1000 }
  ]
}
```

**Response:** `201` — `status: 2` (completed), `totalAmount`, `items[]`, trừ tồn FEFO.

Lookup barcode: `GET /api/sales/pos/lookup?barcode=8934567890012&warehouseId=22222222-2222-2222-2222-222222222201`

---

### 4.3 Bán theo đơn

1) Lấy đơn Rx: `GET /api/pharmacy/prescriptions/a237c011-cccc-4ccc-8ccc-cccccccccc11`  
2) (Tuỳ UI) POS load: `GET /api/pharmacy/prescriptions/{id}/pos-load?warehouseId=22222222-2222-2222-2222-222222222201`  
3) Bán kèm `prescriptionId`:

```http
POST /api/sales/orders
```

```json
{
  "warehouseId": "22222222-2222-2222-2222-222222222201",
  "customerId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01",
  "priceType": 1,
  "prescriptionId": "a237c011-cccc-4ccc-8ccc-cccccccccc11",
  "items": [
    {
      "productId": "66666666-6666-6666-6666-666666666603",
      "productUnitId": "77777777-7777-7777-7777-777777777704",
      "quantity": 20
    }
  ],
  "payments": [
    { "paymentMethod": 1, "amount": 50000 }
  ]
}
```

**Response:** `201` — đơn hoàn tất; Rx chuyển trạng thái dispensed / partially_dispensed tùy số lượng.

---

### 4.4 Hủy hóa đơn

| Trường hợp | API |
|---|---|
| Hủy nháp (draft) | `POST /api/sales/orders/{id}/cancel` |
| Đơn đã hoàn tất | Tạo phiếu trả: `POST /api/sales/orders/{id}/returns` (không “xóa” hóa đơn đã ghi sổ) |

**Hủy nháp — Request:** (body rỗng)  
**Response:** `200` — `status` cancelled.

**Trả hàng (hủy nghiệp vụ sau bán):**

```json
{
  "reason": "Audit void / khách trả",
  "items": [
    { "salesOrderItemId": "<item-id-from-sale>", "quantity": 1 }
  ],
  "payments": [
    { "paymentMethod": 1, "amount": 500 }
  ]
}
```

**Response:** `201` — `returnNumber`, hoàn tồn.

---

### 4.5 Báo cáo tồn kho

```http
GET /api/inventory/stock/products?warehouseId=22222222-2222-2222-2222-222222222201
GET /api/inventory/stock/batches?warehouseId=22222222-2222-2222-2222-222222222201&search=LOT2026A
GET /api/reports/inventory/stock-snapshot?warehouseId=22222222-2222-2222-2222-222222222201
```

> `stock-snapshot` (reports) cần quyền `reports.read` → dùng tài khoản **admin**.  
> `inventory/stock/*` dùng được với **pharmacist** (`inventory.read`).

**Response (rút gọn stock products):**

```json
{
  "items": [
    {
      "productId": "66666666-6666-6666-6666-666666666601",
      "productCode": "PARA500",
      "productName": "Paracetamol 500mg",
      "totalQuantity": 1500
    }
  ],
  "total": 4
}
```

---

## 5. UI slim thẩm định (ghi nhận) & bật lại sau khi xong

**Mục đích:** Trong lúc thẩm định, menu Admin trên `DEMO_PHARMACY` chỉ giữ luồng Pharmacy cốt lõi (bán / mua / kho / công nợ / khách / SP / báo cáo / cấu hình cơ bản), tránh hỏi lan sang People, Cockpit, App KH, pack thương mại.

**Phạm vi:** Chỉ `DEMO_PHARMACY`. `NT_XUANHOA` và tenant khác **không** bị ảnh hưởng.

### 5.1 Đã ẩn gì (trạng thái thẩm định)

| Menu / tab | Cơ chế |
|---|---|
| Phát triển Nhân sự (`/people`) | `audit_slim_nav` — Learning **không** gắn `platformModule`, nên bắt buộc ẩn bằng flag này |
| Cockpit chủ NT | `audit_slim_nav` (giữ module `reports` để báo cáo tồn vẫn dùng được) |
| Bán hàng → Đơn từ app, Chat khách | Tắt module `customer_app` (+ gate FE `sales.appOrders` / `sales.chat`) |
| Khách hàng → Tương tác app | Tắt `customer_app` (`customer.engagement`) |
| Sản phẩm → Gộp SP trùng | `audit_slim_nav` (ẩn nút + chặn route) |
| Cấu hình → Gói Novixa | `audit_slim_nav` |
| Cấu hình → App khách hàng | Tắt `customer_app` |

### 5.2 Code / migration liên quan

| File | Vai trò |
|---|---|
| `client/admin/src/shared/platform/audit-slim-nav.ts` | Đọc `features.audit_slim_nav` |
| `AppLayout`, `SystemLayout`, `SalesLayout`, `Dashboard`, merge/Cockpit pages | Ẩn menu / redirect khi slim |
| `platform-feature-map.ts` / `product-phases.ts` | Gate app-orders / chat theo `customer_app` |
| `migrations/237_demo_pharmacy_audit_handoff.sql` | Seed DEMO (đã gồm slim modules + flag) |
| `migrations/238_demo_pharmacy_audit_slim_nav.sql` | Bật slim trên DEMO đã tồn tại |
| `scripts/demo-pharmacy-restore-full-nav.sql` | **Bật lại full nav** sau thẩm định (xem §5.3) |

Commits tham chiếu: `896ba11` (seed handoff), `a3cd17c` (slim nav), `3229cb7` (ẩn Learning).

### 5.3 Sau thẩm định — bật lại để demo khách

Chạy SQL **chỉ** trên tenant `DEMO_PHARMACY` (file `scripts/demo-pharmacy-restore-full-nav.sql`):

1. `audit_slim_nav` → `false` (hoặc xóa key)
2. Thêm lại `customer_app`, `reservations`, `learning` vào `enabled_modules` + `allowed_modules` (nếu thiếu)
3. Hard refresh Admin (Ctrl+F5), login lại `DEMO_PHARMACY`

Không cần redeploy FE nếu code slim đã có trên VPS — chỉ đổi settings DB là đủ.

**Kiểm tra sau restore:** sidebar có lại Phát triển Nhân sự, Cockpit (nếu có quyền), Đơn từ app / Chat, Tương tác app, Gói Novixa, App khách hàng, nút Gộp SP.

**Muốn slim lại (đợt thẩm định sau):** chạy lại logic trong `238_demo_pharmacy_audit_slim_nav.sql` (hoặc set `audit_slim_nav=true` và bỏ `customer_app` / `reservations` / `learning` khỏi enabled).

---

## 6. Checklist gửi đội thẩm định

- [x] URL Admin / POS / API
- [x] Tài khoản Admin + Dược sĩ trên `DEMO_PHARMACY`
- [x] SKU map `national_drug_id` + tồn kho
- [x] OpenAPI file + mẫu 5 luồng API
- [x] Không đụng tenant vận hành thật (`NT_XUANHOA`)
- [x] Verify live login + stock API (2026-07-27)
- [x] UI slim ngoài phạm vi thẩm định (§5) — nhớ **bật lại** theo §5.3 trước khi demo khách

## 7. Liên quan

- Kiến trúc / liên thông CSDL QG: `docs/novixa/06-compliance/cuc-qld-lien-thong/`
- RBAC Pharmacy (STAFF không xem doanh thu/cockpit): `docs/novixa/03-operations/pharmacy-rbac-deploy-sync-runbook-v1.md`
- Restore full nav: `scripts/demo-pharmacy-restore-full-nav.sql`

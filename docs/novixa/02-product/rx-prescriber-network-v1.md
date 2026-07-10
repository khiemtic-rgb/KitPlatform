# Novixa — Mạng bác sĩ (Prescriber Network) V1

**Mã:** NVX-PRD-12 · **Tier:** T1/T2 · **Trạng thái:** Approved (roadmap) · **Version:** 1.0  
**Ngày:** 2026-07-10 · **Owner:** Product  
**Phụ thuộc:** [rx-prescription-module-v1.md](./rx-prescription-module-v1.md) (v1.3)

> **Định vị:** Novixa **không** làm mạng xã hội y khoa hay EMR đầy đủ. Đây là lớp **danh tính BS + liên kết tin cậy với nhà thuốc + phát hành đơn điện tử có truy vết cấp phát**.

---

## 1. Mục tiêu

| Stakeholder | Giá trị |
|-------------|---------|
| **Bác sĩ** | Kê đơn nhanh; biết đơn vào đúng NT; lịch sử theo từng NT |
| **Nhà thuốc** | Nguồn đơn có căn cứ; kiểm soát ai gửi đơn; báo cáo theo BS |
| **Novixa** | Network effect — càng nhiều NT + BS active → thay Zalo/giấy bằng e-Rx |

**Pain gốc (Rx-1):** Chủ quầy mất tiền vì NV bán Rx không qua máy. **Pain mạng (Rx-2+):** BS và NT làm việc rời rạc qua điện thoại — không truy vết, trùng hồ sơ KH.

---

## 2. Kiến trúc 3 lớp

```
┌─────────────────────────────────────────────────────────┐
│  Lớp 3 — Network (phase B/C)                            │
│  Directory, dashboard, mẫu đơn, QR, tele partner        │
├─────────────────────────────────────────────────────────┤
│  Lớp 2 — Rx-2 MVP (phase A)                             │
│  Portal OTP, link D11/D12, chọn NT, kê signed, notify  │
├─────────────────────────────────────────────────────────┤
│  Lớp 1 — Rx-1 pilot (done)                              │
│  Strict POS, verify, linked_prescribers tenant-scoped    │
└─────────────────────────────────────────────────────────┘
```

---

## 3. Pharmacy Directory

BS tìm NT trong mạng Novixa khi **xin liên kết** (D12).

### 3.1 Dữ liệu hiển thị (read-only)

| Field | Nguồn |
|-------|--------|
| `tenant_code` | `tenants.tenant_code` |
| `tenant_name` | `tenants.tenant_name` |
| `address` | Chi nhánh head office (`branches.is_head_office`) |
| `phone` | Chi nhánh head office |
| `province` / `district` | Parse từ address hoặc metadata sau |

**Không hiển thị:** tồn kho, doanh thu, danh sách KH, user nội bộ.

### 3.2 Opt-in

- Cột `tenants.rx_directory_discoverable` (migration 100)
- Mặc định `false`; NT bật trong Admin → Cài đặt e-Rx
- Chỉ NT có module e-Rx active mới searchable

### 3.3 API

```
GET /api/prescriber-portal/pharmacies/directory?q=&province=
```

Auth: prescriber JWT. Rate limit theo SĐT.

---

## 4. Identity & liên kết

### 4.1 Bảng master

| Bảng | Grain |
|------|-------|
| `prescribers` | 1 người / CCHN hoặc SĐT |
| `prescriber_tenant_links` | 1 cặp BS–NT, state machine |
| `linked_prescribers` | View tenant (Rx-1); sync từ link `active` |

### 4.2 State machine (D11, D12)

```
                    NT invite
         ┌──────────────────────────► pending_nt_invite
         │                                    │
         │ BS request                         │ BS accept
         ▼                                    ▼
  pending_nt_approval ──NT approve──► active ◄──┘
         │                                    │
         │ reject / revoke                    │ revoke
         ▼                                    ▼
      rejected / revoked (đơn cũ giữ, không kê mới)
```

### 4.3 Xác minh CCHN (D15)

| Giai đoạn | Cách |
|-----------|------|
| Pilot | Manual — admin NT hoặc Novixa ops duyệt; **số CCHN bắt buộc** khi tạo/mời/cập nhật BS và trước khi duyệt link / kê đơn |
| Sau | API registry / upload ảnh CCHN (nếu có nguồn chính thức) |

Trạng thái BS: `pending_verification` → `active` | `suspended`

---

## 5. Trình tự triển khai

### Phase 0 — Nền (tuần này)

| # | Deliverable | Trạng thái |
|---|-------------|------------|
| 0.1 | Spec v1.3 (D11–D16) | ✅ |
| 0.2 | Roadmap doc (file này) | ✅ |
| 0.3 | Migration **100** schema | ✅ |
| 0.4 | Handoff Rx-2 cập nhật | ✅ |

### Phase A — Rx-2 MVP (3–4 tuần)

| # | Hạng mục |
|---|----------|
| A1 | Prescriber OTP auth + JWT riêng |
| A2 | API link: invite / accept / request / approve / reject / revoke |
| A3 | Portal UI: login, NT list, chọn NT, kê đơn → `signed` |
| A4 | Admin UI: hàng chờ link, mời BS |
| A5 | Pharmacy directory search |
| A6 | Notify stub (in-app / SMS) đơn mới + lời mời |
| A7 | CRM customer search theo tenant trên portal |
| A8 | Block `controlled` trên portal (D16) |

**Acceptance Rx-2:**

- BS active tại NT X → kê đơn → admin NT X thấy `signed` → POS bán strict
- BS chưa link NT Y → không kê được về Y
- NT mời → BS accept → active
- BS xin NT → NT approve → active

### Phase B — Network v1 (sau 50+ đơn pilot)

| # | Hạng mục |
|---|----------|
| B1 | Mẫu đơn / toa thường dùng (private per BS) |
| B2 | Tìm thuốc theo catalog NT đã chọn |
| B3 | Cảnh báo tồn khi kê |
| B4 | Dashboard BS (đơn/tháng, NT) |
| B5 | Dashboard NT (top BS, SLA) |
| B6 | QR / deep link đơn sẵn |

### Phase C — Mở rộng (6–12 tháng)

| # | Hạng mục |
|---|----------|
| C1 | Directory BS opt-in (NT tìm BS mời) |
| C2 | P13 app khách — timeline e-Rx |
| C3 | Tele-Rx slot (ClinicOS / partner) |
| C4 | Liên thông BYT |
| C5 | Refill / tái kê có audit |

---

## 6. Nguyên tắc sản phẩm (không vi phạm)

1. **Không** ranking công khai BS theo doanh số thuốc  
2. **Không** quảng cáo thuốc trên portal BS  
3. **Không** gộp CRM KH cross-tenant  
4. **Không** kê cross-tenant (đơn luôn thuộc NT đích)  
5. **Không** bỏ audit — mọi link / kê / hủy đều log  
6. Portal **không** thay tư vấn dược — NT vẫn verify/soi tuỳ chính sách

---

## 7. Pilot NT_XUANHOA

| Hạng mục | Cách làm |
|----------|----------|
| BS trong sổ Rx-1 | Backfill migration 100 → `prescriber` + link `active` |
| Directory | Bật `rx_directory_discoverable = true` cho Xuân Hòa khi sẵn sàng demo |
| BS cài app | Claim SĐT pilot → accept invite (nếu NT mời) |
| 50+ đơn | Đo POS block, SLA verify, % đơn portal vs NV nhập |

---

## 8. Tham chiếu

- [Module e-Rx v1.3](./rx-prescription-module-v1.md)
- [Quy trình quầy](../06-compliance/rx-dispensing-quy-trinh-quay-v1.md)
- Handoff: `.cursor/handoff/rx-module-v1.md`
- Customer P13: [customer-app-backlog-p12-p18.md](../07-customer/customer-app-backlog-p12-p18.md)

---

*Owner: Product · Next: Rx-2 implementation (Phase A)*

# Checklist go-live pilot — 2 nhà thuốc, 2 chủ

Dùng trước khi đưa PharmaCore vào vận hành thực tế.

> **Quan trọng — không bắt buộc 1 nhà thuốc = 1 database.**  
> Schema và API đã **multi-tenant** (`tenant_id` trên mọi bảng nghiệp vụ, JWT mang `tenant_id`, đăng nhập theo `tenantCode`).  
> Pilot 2 chủ có thể chọn **một trong hai mô hình** bên dưới. Mô hình B là hướng mở rộng 10 → 1000 nhà thuốc.

Tham chiếu thêm:
- [demo-pos-checklist.md](./demo-pos-checklist.md)
- [demo-procurement-checklist.md](./demo-procurement-checklist.md)
- [demo-inventory-count-checklist.md](./demo-inventory-count-checklist.md)

---

## 0. Chọn mô hình triển khai

| | **A — Tách hẳn (pilot 2 chủ độc lập)** | **B — SaaS (khuyến nghị khi ≥ 3 nhà thuốc)** |
|---|--------|--------|
| Database | 2 DB (hoặc 2 instance) | **1 DB** (sau này shard khi rất lớn) |
| API / Admin | 2 bộ URL riêng | **1 API**, 1 admin (`admin.domain.vn`) |
| Tenant | 1 tenant / DB | **N tenant / DB** (`bootstrap-first-tenant.ps1` mỗi nhà thuốc) |
| Đăng nhập | `admin` + mật khẩu (1 tenant/DB) | `tenantCode` + `admin` + mật khẩu |
| Phù hợp | 2 chủ không liên quan, cần cách ly tối đa | 10, 100, 1000 nhà thuốc cùng nền tảng |
| Nhược điểm | 10 nhà thuốc → 10 DB → vận hành rối | Cần kỷ luật bảo mật + backup 1 DB lớn |

**Mở rộng 1000 nhà thuốc (Mô hình B):** không tạo 1000 DB. Thường dùng **1 cluster PostgreSQL** (hoặc **shard** theo vùng: ví dụ 10 DB × ~100 tenant), **1 codebase API**, thêm tenant bằng script/bootstrap (Phase 2: màn **Super Admin** tạo tenant trên UI).

**Novixa (Mô hình B — khuyến nghị):** xem [docs/novixa-deploy.md](../../docs/novixa-deploy.md).  
Tạo nhà thuốc qua **https://admin.novixa.vn/setup** (form trực quan), không cần script sau migrate.

Phần **0A** mô tả Mô hình A (2 DB). Phần **0B** — Novixa / SaaS 1 DB.

---

## 0B. Kiến trúc Novixa — 1 DB, nhiều nhà thuốc (khuyến nghị)

| Thành phần | URL |
|------------|-----|
| API | `https://api.novixa.vn` |
| Admin | `https://admin.novixa.vn` |
| App khách | `https://novixa.vn` |
| Database | **1** `novixa_prod` |

- [ ] `run-migrations-prod.ps1` **một lần** trên `novixa_prod`
- [ ] Biến môi trường theo `docs/novixa-production.env.example`
- [ ] `Platform__ProvisioningKey` ≥ 16 ký tự (cho nhà thuốc thứ 2 trở đi)
- [ ] Mở **/setup** → tạo `NT_A`, `NT_B` (2 chủ pilot)
- [ ] Đăng nhập admin: **Mã nhà thuốc** + user + pass
- [ ] App khách: cùng **Mã nhà thuốc** + OTP

---

## 0A. Kiến trúc pilot — Mô hình A (2 DB tách biệt)

| Nhà thuốc | Database | API URL | Admin URL | Customer app URL |
|-----------|----------|---------|-----------|------------------|
| Chủ A | `pharmacore_nt_a` | `https://api-a.domain.vn` | `https://admin-a.domain.vn` | `https://app-a.domain.vn` |
| Chủ B | `pharmacore_nt_b` | `https://api-b.domain.vn` | `https://admin-b.domain.vn` | `https://app-b.domain.vn` |

- [ ] Mỗi nhà thuốc có **PostgreSQL riêng** (hoặc instance riêng)
- [ ] `Jwt:Secret` **khác nhau**, ≥ 32 ký tự, không dùng dev-secret
- [ ] `Cors:AllowedOrigins` trỏ đúng domain admin + customer app của từng nhà thuốc
- [ ] Backup DB tự động hàng ngày (pg_dump hoặc snapshot cloud)
- [ ] SSL/TLS trên reverse proxy (nginx/IIS/Caddy)

---

## 1. Build & deploy artifact

Trên máy build (hoặc CI), **chạy riêng cho từng nhà thuốc** với URL API tương ứng:

```powershell
.\scripts\deploy-production.ps1 -ApiBaseUrl "https://api-a.domain.vn"
# Copy publish\ → server A

.\scripts\deploy-production.ps1 -ApiBaseUrl "https://api-b.domain.vn"
# Copy publish\ → server B
```

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `ConnectionStrings__Default` trỏ đúng DB của nhà thuốc đó
- [ ] SMS OTP (`CustomerAppSms__HttpUrl`) — nếu chưa có gateway thật, **tắt/hạn chế** customer app OTP cho khách (pilot nội bộ)
- [ ] `NationalDrugCatalog:Mode=mock` (chưa kết nối CSDL Dược QG thật)

---

## 2. Database — schema Production (không seed demo)

**Không** dùng `run-migrations.ps1` trên Production (script đó có demo seed, loyalty demo, CDP demo).

```powershell
# Lần đầu: tạo DB + user (tuỳ hạ tầng), rồi:
.\scripts\run-migrations-prod.ps1 -ConnectionString "postgresql://pharmacore:***@host:5432/pharmacore_nt_a"
```

Script prod **không** chạy:
- `seed/001_demo_data.sql`, `seed/002`, `seed/003`
- `020_loyalty_demo_transactions.sql`, `021_loyalty_demo_vouchers.sql`
- `027_customer_app_cdp_consent_demo.sql`, `032_customer_address_demo.sql`

Script prod **có** `039_reports_permissions.sql` (quyền Báo cáo).

- [ ] Migration chạy hết không lỗi
- [ ] `SELECT COUNT(*) FROM permissions` ≥ 15
- [ ] `SELECT COUNT(*) FROM tenants` = 0 (trước bootstrap)

---

## 3. Bootstrap tenant đầu tiên (mỗi nhà thuốc một lần)

```powershell
.\scripts\bootstrap-first-tenant.ps1 `
  -ConnectionString "postgresql://pharmacore:***@host:5432/pharmacore_nt_a" `
  -TenantCode "NT_A" `
  -TenantName "Nhà Thuốc An" `
  -BranchCode "CN01" `
  -BranchName "Quầy chính" `
  -BranchAddress "..." `
  -BranchPhone "..." `
  -AdminUsername "admin" `
  -AdminEmail "admin@nhathuoc-a.vn" `
  -AdminPassword "<mat-khau-manh>" `
  -LoyaltyEnabled
```

- [ ] Đăng nhập admin thành công
- [ ] **Đổi mật khẩu** ngay (Hệ thống → Người dùng) — không giữ mật khẩu tạm
- [ ] Tạo thêm user thu ngân / kho (nếu cần) với quyền tối thiểu

---

## 4. Thiết lập nghiệp vụ (từng nhà thuốc)

### Danh mục & kho

- [ ] Nhập **nhà cung cấp** thật
- [ ] Nhập **sản phẩm** (mã, barcode, đơn vị, giá bán)
- [ ] Liên kết CSDL Dược QG: chỉ mock — ghi chú số đăng ký thủ công nếu cần
- [ ] Kiểm tra **kho mặc định** gắn đúng chi nhánh

### Tồn đầu kỳ

- [ ] Nhập tồn mở đầu qua **Mua hàng → Nhập kho** hoặc điều chỉnh tồn (có duyệt)
- [ ] Ghi **số lô, HSD** cho thuốc có quản lý lô
- [ ] Sales → Cài đặt: chọn **Gợi ý lô** hoặc **Nhãn lô bắt buộc** theo quy trình nhà thuốc

### Bán hàng & mua hàng

- [ ] Chạy [demo-pos-checklist.md](./demo-pos-checklist.md) với **sản phẩm thật** (không barcode demo seed)
- [ ] Chạy [demo-procurement-checklist.md](./demo-procurement-checklist.md) — PO → GRN → cập nhật tồn
- [ ] Chạy [demo-inventory-count-checklist.md](./demo-inventory-count-checklist.md) — kiểm kê → duyệt điều chỉnh

### Loyalty (tuỳ chọn pilot)

- [ ] Sales → Cài đặt loyalty: tỷ lệ tích điểm, % đổi tối đa
- [ ] Test POS đổi điểm với khách có SĐT
- [ ] Customer app: OTP SMS — chỉ bật khi đã có gateway

### Báo cáo

- [ ] Menu **Báo cáo** hiển thị (cần `reports.read` — ADMIN có sẵn sau bootstrap)
- [ ] Xuất CSV một báo cáo doanh thu / tồn kho
- [ ] In màn hình (Phase 1) — chấp nhận chưa có mẫu A4 chính thức

---

## 5. Bảo mật & vận hành

- [ ] Không commit `.env`, connection string, JWT secret
- [ ] Firewall: PostgreSQL chỉ mở cho API server
- [ ] Log API + rotate log
- [ ] Kế hoạch restore backup (thử restore 1 lần trên máy test)
- [ ] Liên hệ hỗ trợ kỹ thuật / hotline nội bộ cho 2 chủ

---

## 6. Phạm vi chưa go-live (ghi rõ với chủ)

| Hạng mục | Trạng thái pilot |
|----------|------------------|
| CSDL Dược QG live | Mock — chưa đăng ký QĐ 522 |
| Mẫu in báo cáo A4/PDF | CSV + in màn hình |
| SMS OTP customer app | Cần gateway riêng |
| Multi-tenant 1 DB nhiều chủ | **Khuyến nghị từ ~3 nhà thuốc** (Mô hình B ở trên) |
| Hóa đơn điện tử / thuế | Ngoài phạm vi Phase 1 |

---

## 7. Sign-off (mỗi nhà thuốc)

| Hạng mục | Chủ A | Chủ B | Ngày |
|----------|-------|-------|------|
| Migrate prod OK | ☐ | ☐ | |
| Bootstrap admin OK | ☐ | ☐ | |
| Danh mục + tồn đầu | ☐ | ☐ | |
| POS test pass | ☐ | ☐ | |
| Mua hàng + kiểm kê pass | ☐ | ☐ | |
| Backup cấu hình | ☐ | ☐ | |

**Người triển khai:** _______________  
**Chủ nhà thuốc:** _______________

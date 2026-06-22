# PharmaCore

ERP nhà thuốc đa quốc gia — **production-ready foundation**.

**Đường dẫn:** `E:\PharmaCore`

## Stack

| Layer | Công nghệ |
|-------|-----------|
| API | .NET 8 Web API + Swagger |
| Database | PostgreSQL 16 |
| Data access | Dapper + Npgsql |
| Kiến trúc | Clean Architecture (Domain / Application / Infrastructure / Api) |

## Cấu trúc

```
PharmaCore/
├── docker-compose.yml          # PostgreSQL
├── migrations/                 # SQL 49 bảng
│   ├── 001_extensions.sql
│   ├── 002_identity.sql      … 007_customer_app.sql
│   └── seed/001_demo_data.sql
├── client/
│   └── admin/                  # React Admin Web (Phase 1)
├── novixa-site/                # Website giới thiệu novixa.vn (tách deploy)
├── scripts/run-migrations.ps1
├── src/
│   ├── PharmaCore.Api/         # REST API, Controllers
│   ├── PharmaCore.Application/ # Business logic (sắp tới)
│   ├── PharmaCore.Domain/      # Entities, enums
│   └── PharmaCore.Infrastructure/ # DB, external services
└── docs/                       # ERD, decisions, roadmap
```

## Modules & bảng (49)

| Module | Bảng |
|--------|------|
| Identity | 11 |
| Catalog | 8 |
| Inventory | 7 |
| Procurement | 6 |
| Sales/POS | 7 |
| Customer App | 10 |

## Khởi chạy nhanh

### 1. Database

```powershell
docker compose up -d
.\scripts\run-migrations.ps1
```

### 2. API + Web (khuyến nghị)

```powershell
# Cần Node.js 20+ — https://nodejs.org
.\run-dev.bat
```

| URL | Mô tả |
|-----|--------|
| http://localhost:5173 | Admin Web (login trực quan) |
| http://localhost:5290/swagger | API Swagger |

Login Web: `admin` / `Admin@123`

### 3. Chỉ API

```powershell
.\run.bat
```

### 3. Kiểm tra

- `GET /api/health` — trạng thái service
- `GET /api/health/db` — kết nối DB + số bảng
- Swagger: `http://localhost:5290/swagger`

### 4. Đăng nhập (JWT)

```http
POST /api/auth/login
{ "username": "admin", "password": "Admin@123" }
```

| Endpoint | Mô tả |
|----------|--------|
| `POST /api/auth/login` | Lấy access + refresh token |
| `POST /api/auth/refresh` | Làm mới token |
| `POST /api/auth/logout` | Thu hồi refresh token (cần Bearer) |
| `GET /api/auth/me` | Thông tin user hiện tại (cần Bearer) |

Swagger: bấm **Authorize** → `Bearer {accessToken}`

## Demo data

| Thông tin | Giá trị |
|-----------|---------|
| Tenant | `DEMO_PHARMACY` |
| User | `admin` / `Admin@123` |
| Sản phẩm | Paracetamol 500mg, Paracetamol Extra, Amoxicillin, Vitamin C |
| Tồn kho | 2 lô Paracetamol (FEFO demo) |
| Khách hàng | Trần Thị Mai — 120 điểm loyalty |

## Nguyên tắc kiến trúc

- Catalog **không** lưu tồn / giá trên `products`
- `stock_movements` = sổ cái; `quantity_available` = cache
- Xuất kho **FEFO** theo `inventory_batches.expiry_date`
- Kiểm kê & điều chuyển theo **batch**
- Customer App: loyalty + nhắc uống thuốc

## Bước tiếp theo (code)

1. ~~Auth JWT~~ ✅
2. API Catalog: products, barcodes, prices
3. Service FEFO + stock movement
4. API Sales POS
5. Customer API (OTP, điểm, reminders)

Xem `docs/next-steps.md`

## Sales / POS — tiếp tục phát triển

**Novixa site v1** đã live trên `novixa.vn` — mở lại phát triển **Sales/POS** (checkout, FEFO, ca bán, trả hàng).

| Luồng | Trạng thái |
|-------|------------|
| Quét mã / giỏ / chiết khấu | ✅ |
| Mở ca / chốt ca | ✅ |
| Preview & chốt FEFO | ✅ (đang tinh chỉnh UX) |
| Thanh toán đa hình thức | ✅ |
| Trả hàng / in hóa đơn | ✅ |
| CDP consent + outbox | ✅ API; worker CDP backlog |

Website marketing: [`novixa-site/`](novixa-site/) — deploy tách, không ảnh hưởng API/DB.

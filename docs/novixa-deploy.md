# Novixa — Hướng dẫn triển khai Production

> **Tier:** T2/T3 · **Canonical ops:** [docs/novixa/05-operations/deployment-model-v1.md](./novixa/05-operations/deployment-model-v1.md)

Tài liệu thực hành deploy ERP Novixa (KitPlatform) lên VPS — bổ sung cho `client/admin/pilot-go-live-checklist.md`.

---

## 1. Kiến trúc mục tiêu

| Thành phần | URL |
|------------|-----|
| API | `https://api.novixa.vn` |
| Admin | `https://admin.novixa.vn` |
| Customer App | `https://app.novixa.vn` |
| POS | `https://pos.novixa.vn` |
| Survey | `https://survey.novixa.vn` |
| Prescriber | `https://prescriber.novixa.vn` |
| Partner | `https://partner.novixa.vn` |
| DB | PostgreSQL `novixa_prod` (multi-tenant) |

Marketing: `https://novixa.vn` / `www` — Cloudflare Pages, **không** gọi API từ browser (không cần CORS).

**CORS:** `Cors__AllowedOrigins__*` trong `api.env` **thay thế** toàn bộ list appsettings. Phải đủ 6 SPA ở trên. Deploy chạy `ensure-novixa-cors-env.sh`; API Production còn union `RequiredNovixaSpaOrigins`. Verify: `.\scripts\verify-novixa-cors.ps1`.

---

## 2. Yêu cầu trước deploy

- VPS Ubuntu 24.04, ≥2 vCPU, ≥4GB RAM (khuyến nghị founding)
- DNS A records: `api`, `admin`, `app`, `pos` → IP VPS
- Domain email cho Certbot (vd. `care@novixa.vn`)
- Máy dev Windows: PowerShell, SSH, .NET SDK (build)

---

## 3. Build artifact

```powershell
cd E:\KitPlatform
.\scripts\deploy-production.ps1 -ApiBaseUrl "https://api.novixa.vn"
```

Kiểm tra `publish/api`, `publish/admin`, `publish/customer-app`.

---

## 4. Upload & bootstrap

```powershell
.\scripts\upload-to-vps.ps1 -SshTarget root@<VPS_IP>
```

Trên VPS (lần đầu):

```bash
sudo CERTBOT_EMAIL=care@novixa.vn bash /tmp/kit-platform-upload/deploy/ubuntu/bootstrap-vps.sh
```

**Lưu file secrets:** `/etc/kit-platform/secrets.generated` (backup an toàn, không commit).

Bootstrap sẽ:

1. Cài runtime, nginx, PostgreSQL  
2. Chạy **migrations production** (không demo seed)  
3. Tạo `/etc/kit-platform/api.env`  
4. Khởi động API + SMS stub pilot  
5. SSL Certbot (nếu DNS OK)

---

## 5. Tạo nhà thuốc đầu tiên

1. Mở `https://admin.novixa.vn/setup`  
2. Nhập `Platform__ProvisioningKey` (trong `secrets.generated` hoặc `api.env`)  
3. Tạo tenant: mã NT, tên, chi nhánh đầu tiên  
4. Đăng nhập: **Mã nhà thuốc** + user + password  

Tenant thứ 2+: cùng flow `/setup`.

---

## 6. Cập nhật release

```powershell
.\scripts\upload-to-vps.ps1 -SshTarget root@<VPS_IP>
# Trên VPS:
sudo systemctl restart kit-platform-api
# Nếu có migration mới:
sudo bash /opt/kit-platform/run-migrations-prod.sh "postgresql://KitPlatform:***@127.0.0.1:5432/novixa_prod"
```

---

## 7. Backup

Cron daily (trên VPS):

```bash
# deploy/ubuntu/backup-db.sh — cấu hình DB_NAME=novixa_prod
```

---

## 8. Cấu hình môi trường

Xem [novixa-production.env.example](./novixa-production.env.example).

Validate local trước deploy:

```powershell
.\scripts\validate-production-config.ps1
```

---

## 9. Pilot flags — tắt trước khách thật

| Biến | Production khách |
|------|------------------|
| `CustomerAppAuth__ExposePilotOtpInAdmin` | `true` (staff đọc mã tại quầy thay SMS) |
| `CustomerAppAuth__ExposePilotOtpOnCustomerApp` | `false` |
| `CustomerAppSms__Provider` | Gateway thật (không `Log`) |

---

## 10. Checklist go-live

→ `client/admin/pilot-go-live-checklist.md` §0B

---

## 11. Scripts tham chiếu

| Script | Mục đích |
|--------|----------|
| `scripts/deploy-production.ps1` | Build publish/ |
| `scripts/upload-to-vps.ps1` | SCP artifact |
| `deploy/ubuntu/bootstrap-vps.sh` | First-time VPS |
| `deploy/ubuntu/run-migrations-prod.sh` | Migration prod |
| `scripts/bootstrap-first-tenant.ps1` | Alternative CLI tenant |

---

*Cập nhật: 2026-07-04 · Owner: DevOps*

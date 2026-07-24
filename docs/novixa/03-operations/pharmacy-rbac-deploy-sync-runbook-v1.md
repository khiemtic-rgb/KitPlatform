# Runbook — Bảo vệ Pharmacy khi làm Family OS / Care / deploy

**Mã:** NVX-OPS-PHARM-SYNC-01 · **Ngày:** 2026-07-24  
**Bối cảnh:** Đợt Family OS (và restore không commit) đã làm regress RBAC Pharmacy: STAFF thấy doanh thu / báo cáo / Cockpit; mất tab People; Đơn bán mất lọc “hôm nay”.

---

## 1. Nguyên tắc (đọc trước khi đụng code)

1. **Nguồn chuẩn là Git (`origin/main`), không phải VPS.** VPS chỉ có artifact build (DLL + SPA). Không “kéo source từ máy chủ về” được đầy đủ.
2. **Mọi sửa Pharmacy RBAC / báo cáo / Cockpit / Đơn bán phải commit + push trước khi chuyển sang Family OS / Care / epic lớn khác.**
3. **Không gộp một commit khổng lồ** Family OS + Pharmacy auth. Tách PR/commit theo sản phẩm.
4. **STAFF (`sales.pos` / `sales.read` / `success.checklist`) không được xem doanh thu cửa hàng, báo cáo tài chính, hay Owner Cockpit.**

---

## 2. Checklist trước khi bắt đầu epic mới (Family OS, Care, …)

- [ ] `git status -sb` sạch phần Pharmacy RBAC (hoặc đã commit/push).
- [ ] `git pull --rebase origin main` — local không lệch remote.
- [ ] Ghi nhánh feature riêng: `feature/...` — không làm lâu trên working tree uncommitted.
- [ ] Nếu đụng `AppLayout`, `usePermission`, `Program.cs`, `router.tsx`, `registry.tsx`: diff kỹ phần Pharmacy trước khi merge.

**File “đỏ” — đụng là phải giữ gate Pharmacy:**

| Khu vực | Path |
|--------|------|
| FE permissions | `client/admin/src/shared/auth/usePermission.ts`, `permission-picker.ts`, `permission-labels.ts` |
| Sidebar | `client/admin/src/shared/components/AppLayout.tsx` |
| Dashboard / analytics | `client/admin/src/modules/dashboard/**`, `DashboardService.cs` |
| Báo cáo | `ReportsAuthorizationExtensions.cs`, `ReportsLayout.tsx`, `ReportViewPage.tsx` |
| Cockpit / Success | `SuccessAuthorizationExtensions.cs`, `OwnerCockpitPage.tsx`, controllers `Success/*` |
| Đơn bán (lọc ngày) | `SalesOrderListPage.tsx`, `SalesController` `from`/`to`, `SalesRepository` date filter |
| POS chọn kho | `GET /inventory/warehouses` = `InventoryPolicies.WarehouseLookup` (`sales.pos` OK) — **không** đòi `inventory.read` |
| People / Learning | `client/admin/src/modules/learning/**`, `learning.api.ts`, routes `/people` |
| Learning API (bắt buộc cùng FE) | `Controllers/Learning/**`, `Infrastructure/Learning/**`, `migrations/*learning*.sql` |
| Gộp SP / KH trùng | `ProductDuplicateMergePage`, `CustomerMerge*`, mig `189`/`190` — **commit ngay**, không để chỉ stash |

**Cảnh báo (2026-07-24):** FE People từng deploy mà **thiếu Learning API trên git/DLL** → HTTP 404 khi mở bài (`…222210`). Backend phải commit + deploy API cùng lúc với FE.

**Cảnh báo (2026-07-24 b):** Gộp SP/KH trùng từng chỉ nằm `stash` → mất working tree. Restore → **commit + push trước** epic khác.

---

## 3. Quy tắc RBAC Pharmacy (không được “đơn giản hóa” lại)

| Surface | Ai được | Không được kế thừa từ |
|--------|---------|------------------------|
| KPI / biểu đồ Tổng quan | `reports.read` / `reports.export` / ADMIN | `sales.*`, `success.checklist` |
| Module Báo cáo + chạy BC | `reports.read` / `reports.export` / ADMIN | `sales.read` |
| Xuất Excel/CSV | **chỉ** `reports.export` (hoặc ADMIN) | `reports.read` |
| Owner Cockpit + Loss | `success.read` **hoặc** `reports.*` / ADMIN·MANAGER·BRANCH_MANAGER | `sales.read`, `success.checklist` |
| Checklist ca | `success.checklist` / `success.read` / `sales.pos` | — |
| Đơn bán mặc định | Range ngày = **hôm nay**; tìm SĐT/số đơn thì bỏ giới hạn ngày | — |

API policies phải khớp FE. Đặc biệt **không** mở lại `ReportsPolicies.Read` với `sales.read` / `inventory.read`.

---

## 4. Trình tự deploy an toàn

```text
1. git fetch origin && git rev-parse HEAD origin/main   # tip local = origin/main
2. Sửa + test local
   - Bắt buộc: cd client/admin && npm run build   # tsc --noEmit — TS6133 chặn prod
   - API: dotnet publish nếu đổi C#
3. git add (đúng file) → commit → git push origin main
4. scripts/deploy-production.ps1 -ApiBaseUrl "https://api.novixa.vn" -UseExistingNodeModules
5. scripts/deploy-update-vps.ps1          # mặc định SkipMigrations
   scripts/deploy-update-vps.ps1 -RunMigrations   # chỉ khi có migration mới (191/210/211, …)
6. Verify bundle: so MD5 assets/PosPage-*.js (local dist vs https://admin.novixa.vn) — xem pharmacy-pos-customer-incidents-v1.md §5
7. Kiểm tra: https://api.novixa.vn/api/health (+ /api/health/db)
8. Hard refresh admin (Ctrl+F5) cho user test
9. Smoke STAFF (vd. khiemtic @ NT_XUANHOA):
   - Không KPI doanh thu trên Tổng quan
   - Không vào /reports (403 / redirect)
   - Không vào /success/cockpit (redirect checklist hoặc /)
   - Đơn bán mặc định hôm nay
10. Smoke ADMIN: báo cáo + Cockpit + xuất CSV (nếu có reports.export)
11. POS/KH (nếu đụng): checklist pharmacy-pos-customer-incidents-v1.md §3
```

**Không làm:** deploy working tree chưa commit; amend commit đã push; force push `main`; commit `artifacts/` hoặc `publish/`.

---

## 5. Sau deploy — đồng bộ máy

| Việc | Cách |
|------|------|
| Khóa bản tốt trên remote | `git push` ngay sau khi deploy ổn |
| Máy khác / agent sau | `git pull` — **không** tin VPS là source |
| WIP chưa sẵn sàng | Giữ dirty riêng (Family OS…) hoặc `git stash` / nhánh khác — **không** để RBAC Pharmacy chỉ nằm uncommitted |
| Mất file (như learning) | Khôi phục từ git / transcript extract → **commit ngay** |

---

## 6. Kiểm tra nhanh “còn lệch không?” (sau epic lớn)

```text
FE:  useCanViewStoreAnalytics / useCanReportsRead|Export / useCanAccessOwnerCockpit
API: ReportsPolicies, SuccessPolicies.Owner|Checklist, DashboardService redaction
DB:  STAFF không có reports.* / success.read (chỉ checklist + sales.pos/read nếu cần quầy)
UX:  ShiftChecklist không hiện link Cockpit nếu không có quyền owner
```

---

## 7. Sự cố đã gặp (để nhớ)

| Triệu chứng | Nguyên nhân gốc | Cách khóa |
|-------------|-----------------|-----------|
| STAFF thấy doanh thu Tổng quan | `reports.read` trên STAFF **hoặc** FE/API coi `success.read` / `sales.*` là analytics | Gỡ `reports.*` khỏi STAFF; analytics = `reports.*` only |
| STAFF xem + xuất BC | `ReportsPolicies.Read` cho phép `sales.read`; nút CSV không check `reports.export` | Siết policy; gate nút xuất |
| STAFF vào Cockpit thấy Doanh số | Cockpit dùng `DashboardPolicies.Read` (mở theo sales) | `SuccessPolicies.Owner` |
| Mất tab People | Module learning chưa commit, bị mất disk | Commit `modules/learning/**` |
| Đơn bán không mặc định hôm nay | `from`/`to` + RangePicker chỉ nằm stash | Commit vertical slice FE+API |
| POS quick-add KH trống SĐT/tên | Ant `Select` `onSearch('')` on blur xóa state trước drawer | `lastCustomerSearchRef` + prefill `''` not `undefined` — `ad9231d` |
| Deploy prod fail / VPS thiếu fix POS | `npm run build` = `tsc --noEmit` — unused var TS6133 | Xóa dead code; build local trước deploy |
| Learning mail 404 | FE path `/api/learning/...` + axios base `/api` | Path `/learning/mail/...` only |
| Thư “owner/manager” cho STAFF | `LearningWriteGuard` bọc route mail | Mail route không WriteGuard |

**Chi tiết POS/KH/Learning (07/2026):** [pharmacy-pos-customer-incidents-v1.md](./pharmacy-pos-customer-incidents-v1.md)

---

## 8. Liên quan

- Deploy: `scripts/deploy-production.ps1`, `scripts/deploy-update-vps.ps1`
- POS/KH/Learning incidents: [pharmacy-pos-customer-incidents-v1.md](./pharmacy-pos-customer-incidents-v1.md)
- Hypercare chung: [hypercare-week1-4-runbook-v1.md](./hypercare-week1-4-runbook-v1.md)
- Cursor rules: `.cursor/rules/pharmacy-rbac-deploy-sync.mdc`, `.cursor/rules/pos-ant-select-prefill.mdc`

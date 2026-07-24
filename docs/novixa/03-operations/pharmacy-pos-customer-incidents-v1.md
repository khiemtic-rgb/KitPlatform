# Ops — POS / Khách hàng / Learning incidents (2026-07)

**Mã:** NVX-OPS-POS-CUST-01 · **Ngày:** 2026-07-24  
**Liên quan:** [pharmacy-rbac-deploy-sync-runbook-v1.md](./pharmacy-rbac-deploy-sync-runbook-v1.md) · `.cursor/rules/pharmacy-rbac-deploy-sync.mdc` · `.cursor/rules/pos-ant-select-prefill.mdc`

Tài liệu ghi lại chuỗi sự cố POS / khách hàng / Learning (07/2026), mẫu nguyên nhân gốc, và checklist verify + deploy để không lặp lại.

**Nguồn chuẩn (SoT):** `origin/main` — không phải VPS, không phải working tree chưa commit.

---

## 1. Dòng thời gian sửa (theo `git log origin/main`)

| # | Triệu chứng | Commit | Phạm vi | Ghi chú |
|---|-------------|--------|---------|---------|
| 1 | Trang KH “Gần giống ≥ 80%” treo / chậm | `229fa29` | API + mig `191_customer_name_trgm.sql` | O(n²) similarity → `pg_trgm` `%` + GIN index + batch counts |
| 2 | Khách có SĐT nhưng không mua nợ mặc định | `35c130b` | API + mig `210_customer_allow_credit_with_phone.sql` + FE checkbox | Backfill `allow_credit=true` khi có phone hợp lệ |
| 3 | Gộp SP/KH trùng dùng chung quyền write | `0489460` | RBAC + mig `211_merge_permissions.sql` | SoD: `catalog.merge`, `sales.customers.merge` — chỉ ADMIN/MANAGER mặc định |
| 4 | POS không tìm được KH theo SĐT | `c86c572` | API `GET /sales/customers?search=` + FE label | Trước đó preload ~50 + lọc client → miss phone |
| 5 | Learning mailbox HTTP 404 | `08a7eff` | FE `learning.api.ts` | Path `/api/learning/mail/...` + axios `baseURL=/api` → **double `/api/api`** |
| 6 | Thư hiện “dành cho owner/manager” | `70e6e5b` | FE `router.tsx` | `LearningWriteGuard` bọc nhầm route `/people/mail` — đã bỏ guard khỏi mail |
| 7 | POS quick-add KH: cả tên và SĐT trống | `c90d435` → `4dd95a9` → `ad9231d` | FE `PosPage.tsx`, `CustomerFormDrawer.tsx` | v1 prefill từ state bị Ant Select blur xóa; fix `lastCustomerSearchRef` + `''` not `undefined`; `ad9231d` xóa unused var (TS6133 chặn build prod) |

**Restore trước đó (context):** `ce7470c` / `58473c9` — gộp SP/KH trùng từ stash; `48eeb33` / `2f0f0dd` — Learning API backend + doc sync.

---

## 2. Mẫu nguyên nhân gốc (đọc trước khi sửa tương tự)

### 2.1 Ant Design `Select` + quick-add prefill (POS)

**Triệu chứng:** User gõ SĐT/tên → bấm nút thêm KH nhanh → drawer mở nhưng cả hai field trống.

**Gốc:** `Select` `showSearch` + `onSearch` gọi `onSearch('')` khi **blur** (kể cả click nút UserAdd). State search bị wipe trước khi drawer mở.

**Khóa:**

- Giữ query thật trong `useRef` (`lastCustomerSearchRef`) — chỉ cập nhật khi `trimmed !== ''`.
- Mở drawer: `setQuickCreatePrefill(guessPhoneOrName(lastCustomerSearchRef.current))`.
- `CustomerFormDrawer`: prefill với `initialPhone?.trim() ?? ''` (và tên tương tự) — **`undefined` không hiện trong Input**.
- File đỏ: `client/admin/src/modules/sales/PosPage.tsx`, `client/admin/src/modules/customer/CustomerFormDrawer.tsx`.
- Rule Cursor: `.cursor/rules/pos-ant-select-prefill.mdc`.

### 2.2 Axios `baseURL` + path API

**Triệu chứng:** HTTP 404 trên route có vẻ đúng; Network tab thấy `/api/api/...`.

**Gốc:** `http.ts` dùng `baseURL: apiPath('/api')`. Mọi path trong `*.api.ts` phải **không** lặp prefix `/api`.

**Khóa:** Path dạng `/learning/mail/threads`, không `/api/learning/...`. Kiểm tra Network trước khi đổi backend route.

### 2.3 Client-only filter vs server search

**Triệu chứng:** POS / dropdown KH không match SĐT; chỉ thấy ~50 bản ghi preload.

**Gốc:** Preload list + `filter()` phía client không cover phone normalization / full dataset.

**Khóa:** Debounced `searchCustomers(query)` → `GET /sales/customers?search=`; label hiển thị `fullName · phone · customerCode`.

### 2.4 RBAC SoD — gộp trùng

**Triệu chứng:** Nhân viên catalog.write có thể gộp KH/SP trùng.

**Gốc:** Merge dùng chung quyền write.

**Khóa:** Quyền riêng `catalog.merge`, `sales.customers.merge` (mig `211`); gate FE + API; **không** kế thừa từ `catalog.write` / `sales.customers`.

### 2.5 Similar-clusters performance

**Triệu chứng:** UI “Gần giống ≥ 80%” treo.

**Gốc:** O(n²) so sánh chuỗi trên full bảng.

**Khóa:** `pg_trgm` + GIN (`191`); batch counts; biểu thức normalize khớp repository.

### 2.6 Production build `tsc` (TS6133)

**Triệu chứng:** Fix FE chạy dev OK nhưng `deploy-production.ps1` / `npm run build` fail; VPS không nhận bundle mới.

**Gốc:** `client/admin/package.json` — `"build": "tsc --noEmit && vite build"`. Biến/import không dùng = **fail cứng**.

**Khóa:** Luôn chạy `npm run build` trong `client/admin` trước deploy; không commit dead state “tạm” rồi deploy.

### 2.7 Learning: FE deploy thiếu API

**Triệu chứng:** Tab People có nhưng API 404.

**Gốc:** FE merge trước; backend Learning chưa push/deploy.

**Khóa:** Vertical slice FE + `Controllers/Learning/**` + migrations; xem runbook mục file đỏ.

### 2.8 `LearningWriteGuard` quá rộng

**Triệu chứng:** STAFF có `learning.read` vẫn thấy “chỉ owner/manager” ở Thư.

**Gốc:** Guard write bọc route đọc mail.

**Khóa:** `LearningWriteGuard` chỉ bọc content/enroll/evaluations/recognize/grow — **không** bọc `path="mail"`.

---

## 3. Checklist verify — POS customer search + quick-add

Chạy trên **admin build prod** (hoặc https://admin.novixa.vn sau deploy + **hard refresh** Ctrl+F5).

### 3.1 Tìm khách (POS)

- [ ] Mở POS → chọn kho → ô khách.
- [ ] Gõ SĐT (≥ 8 số, có/không format) → thấy KH đúng trong dropdown (debounce ~250ms).
- [ ] Gõ tên một phần → thấy KH; label có `tên · SĐT · mã` khi có dữ liệu.
- [ ] Chọn KH → bán thử / loyalty load OK.

### 3.2 Quick-add prefill

- [ ] Gõ SĐT chưa có (vd. `0909999888`) — **chưa blur ra ngoài**.
- [ ] Click nút thêm KH (UserAdd) ngay sau khi gõ.
- [ ] Drawer mở: **SĐT đã điền**; tên trống hoặc đoán từ chuỗi nếu gõ tên.
- [ ] Gõ tên (không phải SĐT) → quick-add → **tên điền**, SĐT trống.
- [ ] `allowCredit` mặc định **bật** khi có SĐT (mig `210` + FE).
- [ ] Lưu KH → chọn được trên POS ngay.

### 3.3 Regression RBAC (không regress STAFF analytics)

- [ ] STAFF: không KPI doanh thu Tổng quan; không `/reports`; không Cockpit doanh số.
- [ ] Gộp trùng: chỉ role có `catalog.merge` / `sales.customers.merge`.

### 3.4 Learning mail (nếu đụng module)

- [ ] STAFF có `learning.read`: mở `/people/mail` — không banner “owner/manager”.
- [ ] Network: `GET .../api/learning/mail/threads` — **một** `/api`, status 200.

---

## 4. Checklist deploy — khe hở đã phát hiện và cách đóng

| Khe hở | Hậu quả đã gặp | Cách đóng |
|--------|----------------|-----------|
| Deploy working tree chưa commit/push | VPS/local lệch; mất fix khi đổi epic | `git status` sạch app code → commit → push → build từ **HEAD đã push** |
| Bỏ qua `npm run build` (tsc) | TS6133 chặn deploy; prod thiếu fix POS | Bước bắt buộc trong `deploy-production.ps1`; CI/local: `cd client/admin && npm run build` |
| Chỉ deploy FE khi có mig API | allow_credit / merge perms / trgm không có trên DB | `-RunMigrations` khi có file mới trong `migrations/` (191, 210, 211, …) |
| Chỉ deploy API khi đổi FE path | 404 hoặc UI cũ | Xác định **FE-only / API-only / cả hai** trước deploy |
| Không hard-refresh admin | User thấy bundle cũ dù VPS đã update | Ctrl+F5; so hash `index.html` → `assets/*.js` |
| Tin VPS là SoT | Không restore source; hash lệch | SoT = `origin/main`; verify hash bundle (xem §5) |
| Commit artifacts `publish/` / `artifacts/` | Repo bẩn; nhầm bản deploy | Không commit; `publish/` tái tạo bởi `deploy-production.ps1` |
| Epic Family OS dirty cùng tree | Nhầm deploy / regress Pharmacy | Tách nhánh; Pharmacy fix commit riêng trước epic lớn |

### Trình tự deploy (tóm tắt)

```text
1. git fetch origin && git rev-parse HEAD origin/main  # phải khớp tip đã push
2. Sửa + npm run build (admin) + dotnet publish (nếu API)
3. git commit + git push origin main
4. .\scripts\deploy-production.ps1 -ApiBaseUrl "https://api.novixa.vn" [-UseExistingNodeModules]
5. .\scripts\deploy-update-vps.ps1                    # SkipMigrations mặc định
   .\scripts\deploy-update-vps.ps1 -RunMigrations      # khi có mig mới
6. Verify hash bundle (§5) + smoke §3 + https://api.novixa.vn/api/health
7. Hard refresh admin cho user
```

**Docs-only commit:** push git đủ; VPS admin/API **N/A** redeploy — vẫn verify code tip trên VPS đã có từ deploy app trước.

---

## 5. Verify VPS ↔ local build (không cần SSH)

Sau `npm run build` tại `client/admin` (cùng commit với `origin/main`):

```powershell
# Lấy tên chunk từ dist/index.html hoặc trang live
$asset = "assets/PosPage-D0dEe6fS.js"   # ví dụ — đổi theo build hiện tại
$local = Get-FileHash "client\admin\dist\$asset" -Algorithm MD5
# Tải cùng path từ https://admin.novixa.vn/$asset và so MD5
```

Khớp MD5 ⇒ admin prod align với local build cùng commit. Ghi nhận lần verify 2026-07-24: `PosPage-D0dEe6fS.js` + `index-DWyL7DqL.js` khớp với tip `ad9231d`.

---

## 6. File đỏ (POS / KH / Learning)

| Khu vực | Path |
|--------|------|
| POS search + quick-add | `client/admin/src/modules/sales/PosPage.tsx` |
| Drawer KH | `client/admin/src/modules/customer/CustomerFormDrawer.tsx` |
| API search KH POS | `client/admin/src/shared/api/sales.api.ts` → `GET /sales/customers` |
| Similar clusters | `CustomersController.cs`, `191_customer_name_trgm.sql` |
| Merge RBAC | `211_merge_permissions.sql`, merge pages catalog/customer |
| Learning mail paths | `client/admin/src/shared/api/learning.api.ts`, `router.tsx` (mail **không** WriteGuard) |
| HTTP client | `client/admin/src/shared/api/http.ts` (`baseURL`) |

---

## 7. Liên quan

- Runbook RBAC + deploy: [pharmacy-rbac-deploy-sync-runbook-v1.md](./pharmacy-rbac-deploy-sync-runbook-v1.md)
- Cursor: `.cursor/rules/pharmacy-rbac-deploy-sync.mdc`, `.cursor/rules/pos-ant-select-prefill.mdc`
- Scripts: `scripts/deploy-production.ps1`, `scripts/deploy-update-vps.ps1`

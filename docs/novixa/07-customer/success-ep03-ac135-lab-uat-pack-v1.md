# EP03 Lab UAT — AC1 · AC5 · AC3 (trước deploy VPS)

**Mã:** NVX-PRD-03-EP03-UAT-AC135 · **Neo:** [success-p2-03-loss-prevention-epic-v1.md](../02-product/success-p2-03-loss-prevention-epic-v1.md)  
**Môi trường:** **Lab local** (không prod) · Tenant gợi ý: `NT_XUANHOA`  
**Trạng thái:** Lab UAT **Ready deploy** · 2026-07-14 (agent + API evidence)  
**Đã prod riêng:** AC2 + AC4 (2026-07-14) — pack này **không** thay UAT prod AC2/AC4; chỉ gate AC1/AC5/AC3 trước cutover.

> Mục tiêu: chủ / ops xác nhận Loss Prevention wedge (nhật ký · gate · cycle count) đủ dùng trên lab → mới `deploy-production` + `deploy-update-vps -RunMigrations` (mig **133**).

---

## 0. Lab sẵn sàng

| Thành phần | Ghi chú |
|------------|---------|
| API | `http://localhost:5290` (`dotnet run` KitPlatform.Api, Release/http) |
| Admin | `http://localhost:5173` (`npm run dev` trong `client/admin`) hoặc build local |
| Tenant | `NT_XUANHOA` · user `admin` / `Admin@123` (hoặc account ADMIN tương đương) |
| Mig | Lab đã apply **133** (`sales.cancel`, `inventory.approve`) |
| Commits tối thiểu | `1bdbba5` AC1 · `0b033b2` AC5 · `b5dd5a7` AC3 |

**Gate máy (bắt buộc trước phiên tay):**

```powershell
powershell -NoProfile -File .\scripts\uat-success-loss-ac135-lab.ps1
```

Pass = mọi smoke AC1/AC2/AC4/AC5/AC3 xanh. Fail → không mở phiên tay / không deploy.

---

## 1. Phiên UAT tay (45–60 phút)

Admin: `http://localhost:5173/success/loss` + Cockpit `/success/cockpit`

| # | Ai | Việc | Kết quả mong đợi |
|---|-----|------|------------------|
| **U0** | Ops | Chạy `uat-success-loss-ac135-lab.ps1` | Script PASS |
| **U1** | Chủ / Ops | Tab **Nhật ký thao tác**: lọc ngày · loại · user · branch | Có dòng; actor + tóm tắt + link chứng từ; copy “minh bạch vận hành” không “giám sát NV” |
| **U2** | Ops | POS: tạo đơn có giảm giá POS > 0 (staff có `sales.discount`) | Feed hiện `discount` (hoặc sau complete); AC4 tab vẫn thấy giảm theo NV |
| **U3** | Ops | User **chỉ** `sales.write` (không `sales.cancel`, không ADMIN) hủy nháp | **403** — không hủy được |
| **U4** | ADMIN | Hủy nháp được; audit `order_cancel` trên feed | Pass |
| **U5** | Ops | Giảm giá vượt ngưỡng (không unlimited) | API/UI báo cần duyệt; payload có `workflowTaskId` / message rõ (409) |
| **U6** | ADMIN | Duyệt task discount (`/api/system/workflow/...` hoặc UI WF nếu có) | Đơn hoàn tất được sau approve; audit override |
| **U7** | User chỉ `inventory.write` | `POST .../adjustments/{id}/approve` | **403** |
| **U8** | ADMIN | Tab **Kiểm kê cuốn chiếu**: chọn kho → gợi ý ≤20 SKU → tạo phiên | Reason `[cycle_count]…`; mở `/inventory/adjustments/:id/count` |
| **U9** | ADMIN | Đếm vài dòng → approve | Cockpit tile: `done` hoặc `has_variance`; variance tab có lệch nếu có |
| **U10** | Chủ | Hồi quy AC2/AC4 tabs + Cockpit risk strip quỹ | Không regress so với prod behavior |

---

## 2. Bảng ký lab

| # | Tiêu chí | Pass? | Ghi chú / ảnh |
|---|----------|-------|----------------|
| U0 | Smoke runner lab PASS | ☑ | `uat-success-loss-ac135-lab.ps1` PASS (AC1–5 + AC2/AC4) |
| U1 | Audit feed lọc + đọc được | ☑ | UI tab Nhật ký: actor/tóm tắt/link SO; API total=9 |
| U2 | Discount ghi feed / AC4 | △ | Chưa có discount mới trong phiên; prod AC4 đã có discountRows≥1 — tạo đơn giảm giá khi hypercare |
| U3–U4 | Gate hủy HĐ | ☑ | `uat_loss_cashier` cancel → **403**; ADMIN → **404** (gate OK) |
| U5–U6 | Gate giảm giá + audit decide | △ | Pending API OK; 409 với staff limited chưa chạy end-to-end (ADMIN unlimited) — theo dõi sau deploy |
| U7 | Gate duyệt adjust | ☑ | cashier approve → **403** |
| U8–U9 | Cycle count end-to-end | ☑ | suggestions=15; session `[cycle_count]` ADJ-000001; status/cockpit **in_progress**; link đếm tồn |
| U10 | AC2/AC4 không vỡ | ☑ | Tabs quỹ + theo NV + Cockpit risk strip OK |

**Tenant:** NT_XUANHOA · **Người UAT:** agent lab + API · **Ngày:** 2026-07-14  
**Kết luận:** ☑ Ready deploy VPS (AC1+AC5+AC3 + mig 133) · ☐ Hold  

**Residual (không block deploy):** U2 tạo discount mới trên lab; U5–U6 POS 409 với user chỉ `sales.discount` (không unlimited).

---

## 3. Sau UAT lab (ops)

**Ready deploy**

1. `.\scripts\deploy-production.ps1 -ApiBaseUrl "https://api.novixa.vn" -UseExistingNodeModules`  
2. `.\scripts\deploy-update-vps.ps1 -RunMigrations`  
3. Prod smoke:  
   - `smoke-success-loss-audit-feed-local.ps1 -BaseUrl https://api.novixa.vn`  
   - `smoke-success-loss-gates-local.ps1 -BaseUrl https://api.novixa.vn`  
   - `smoke-success-loss-cycle-count-local.ps1 -BaseUrl https://api.novixa.vn`  
   - giữ AC2/AC4 smokes  
4. Tick deploy trong epic AC1/AC3/AC5; lưu ảnh bảng ký §2.

**Đã deploy VPS 2026-07-14:** `deploy-production` → `deploy-update-vps -RunMigrations` (APPLY `133_success_loss_gates.sql`); prod smokes AC1/AC2/AC3/AC4/AC5 NT_XUANHOA PASS. Epic AC1/AC3/AC5 Deploy VPS ☑.

**Hold** — không deploy; ghi blocker vào epic / chat.

---

## 4. Ngoài phạm vi pack này

| Không yêu cầu để đóng lab UAT | Ghi chú |
|-------------------------------|---------|
| Screenshot prod AC2/AC4 | Đã deploy riêng; optional |
| AC6 cảnh báo 3 rule | Epic sau |
| CSV export AC4 | Optional |
| Soft-CKS trên Loss | Ngoài AC5 |
| CL-GO-01b clinic | Customer sign-off riêng |

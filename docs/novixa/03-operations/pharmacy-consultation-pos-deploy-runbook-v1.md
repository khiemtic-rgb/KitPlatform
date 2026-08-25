# Runbook — Tư vấn tại quầy (POS) · deploy Pharmacy prod

**Mã:** NVX-OPS-PHARM-CONSULT-01 · **Ngày:** 2026-08-25  
**Tenant pilot:** `NT_XUANHOA` (Nhà thuốc Xuân Hòa) — **không** seed / SQL hàng loạt trên tenant khác.

---

## 1. Phạm vi release

- Modal **Tư vấn tại quầy** trên POS (icon thuốc cạnh chọn khách).
- Lưu phiên `pharmacy_consultation_sessions`, liên kết hóa đơn khi chốt đơn.
- Gợi ý OTC theo taxonomy + rule + tồn kho (không thay thế đánh giá dược sĩ).
- Cấu hình AI: **Bán hàng → Cài đặt POS → AI tư vấn quầy**.

---

## 2. Migration (prod manifest)

Chạy theo `deploy/ubuntu/migration-files.prod.txt` (đoạn 294–300):

| File | Nội dung |
|------|----------|
| `294_pharmacy_consultation_mvp1.sql` | Bảng phiên tư vấn |
| `295_pharmacy_symptom_taxonomy_schema.sql` | Taxonomy + risk/question |
| `296_pharmacy_symptom_taxonomy_seed.sql` | Seed global triệu chứng + rules |
| `297_pharmacy_consultation_dehydration_tune.sql` | Tune mất nước |
| `298_pharmacy_consultation_ent_otc.sql` | ENT / khàn tiếng OTC |
| `299_pharmacy_consultation_preliminary_assessment.sql` | Cột nhận định sơ bộ |
| `300_pharmacy_consultation_customer_snapshot.sql` | Snapshot hồ sơ khách |

Verify:

```sql
SELECT filename FROM kit_schema_migrations WHERE filename LIKE '%pharmacy_consultation%' OR filename LIKE '%symptom_taxonomy%';
SELECT COUNT(*) FROM pharmacy_symptom;  -- ~190
```

---

## 3. Deploy artifact

1. `git pull origin main` — code consultation đã merge.
2. `npm run build` trong `client/admin` (tsc bắt buộc).
3. Deploy API + admin SPA theo runbook Pharmacy chuẩn.
4. **Không** chạy seed `DEMO_PHARMACY` / `239_demo_*` trên `NT_XUANHOA`.

---

## 4. Cấu hình sau deploy

1. **Gemini (tuỳ chọn nhưng nên có):** Cài đặt POS → AI tư vấn quầy — key tenant hoặc `GEMINI_API_KEY` trên server / fallback Content Park.
2. **RBAC:** Nhân viên quầy cần `sales.read` (phân tích/gợi ý) + `sales.pos` (lưu phiên, liên kết đơn).
3. **Sản phẩm:** OTC gợi ý theo `category_code` (`HO_HAP`, `GIAM_DAU`, …) và keyword tên SP — kiểm tra danh mục Xuân Hòa nếu gợi ý yếu.

---

## 5. Smoke test (NT_XUANHOA)

| Bước | Kỳ vọng |
|------|---------|
| POS → chọn khách → mở Tư vấn | Catalog triệu chứng load |
| Chọn 1–2 triệu chứng + Phân tích | Nhận định sơ bộ / cảnh báo |
| Gợi ý sản phẩm | Có OTC tồn > 0, thêm giỏ |
| Lưu phiên → đóng modal | Chip «Đã lưu phiên tư vấn» |
| Chốt đơn | Phiên `linked` trong DB; chip ẩn |
| Bổ sung hồ sơ / thêm khách nhanh | NS, giới tính, địa chỉ, ghi chú (tùy chọn) |

API nhanh (đã login tenant):

- `GET /api/sales/pos/consultation/symptom-options`
- `POST /api/sales/pos/consultation/extract`
- `POST /api/sales/pos/consultation/suggest` (cần `warehouseId`)

---

## 6. Rollback

- UI: ẩn nút tư vấn = revert FE (không xóa data).
- DB: **không** drop bảng phiên trên prod nếu đã có dữ liệu thật — chỉ rollback code.

---

## 7. Giới hạn MVP (đã chốt)

- Không hàng đợi dược sĩ app — dùng «Chuyển dược sĩ» ghi chú + lưu phiên.
- Không báo cáo admin danh sách phiên (data có trong DB).
- Disclaimer luôn hiển thị trong modal.

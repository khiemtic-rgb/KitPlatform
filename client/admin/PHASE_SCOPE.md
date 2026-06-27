# Phạm vi Giai đoạn 1 / 2 — Admin Web



Cấu hình tập trung: [`src/shared/product/product-phases.ts`](./src/shared/product/product-phases.ts)



- `CURRENT_PRODUCT_PHASE = 1` — gói vận hành nhà thuốc / chuỗi nhỏ (đủ tính năng cạnh tranh)

- `CURRENT_PRODUCT_PHASE = 2` — thêm **module thuế/kế toán chuyên sâu** (báo cáo, export, HĐĐT)



**Nguyên tắc:** Giai đoạn 1 có cấu hình thuế cơ bản và tính thuế trên PO; Giai đoạn 2 mới làm nghiệp vụ thuế sâu (chưa có UI thì ẩn bằng feature flag).



## Giai đoạn 1 — Hoàn thiện (bật)



| Khu vực | Tính năng |

|---------|-----------|

| Danh mục | Sản phẩm, danh mục, thương hiệu, hoạt chất |

| Kho | Tồn, kho, nhập đầu kỳ, chuyển, điều chỉnh, kiểm kê |

| Mua hàng | PO, GRN, NCC, **cấu hình Thuế GTGT**, **công nợ**, **thanh toán NCC** |

| Bán hàng | POS, đơn bán, đơn từ app, **đặt trước app**, **chat KH**, trả hàng, ca, **tích điểm**, **voucher**, thông tin NH |

| Khách hàng | Hồ sơ KH, app OTP (customer-app) |

| Hệ thống | User, role, chi nhánh |
| **Báo cáo** | Wave 1: doanh thu, ca, nhập GRN, công nợ, tồn, HSD — xem [REPORTS_WAVE1.md](./REPORTS_WAVE1.md) |



### Thuế — phạm vi Giai đoạn 1 (cơ bản, không chuyên sâu)



- Tab **Thuế GTGT**: CRUD loại thuế (%VAT, không chịu thuế…).

- PO/GRN: chọn loại thuế, hiển thị Tạm tính / Thuế / Tổng.

- **Công nợ & thanh toán** vẫn tính theo **giá trị GRN trước thuế** (`SUM(line_total)`).

- Thanh toán NCC nhập số tiền thủ công — có thể khác tổng PO sau thuế cho đến khi Giai đoạn 2 thống nhất.



## Giai đoạn 1 — Chưa triển khai (`feature: false`)



| Feature key | Nội dung (Giai đoạn 2) |

|-------------|-------------------------|

| `reports.taxInput` | Báo cáo thuế GTGT đầu vào |

| `reports.accountingExport` | Export chứng từ sang kế toán / Excel chuẩn |



Guard: [`useProductNavGuard`](./src/shared/product/useProductNavGuard.ts) — chỉ áp dụng khi feature tắt (hiện tại chỉ các mục báo cáo/export trên).



## Giai đoạn 2 — Mở rộng (thiết kế sẵn)



1. **`CURRENT_PRODUCT_PHASE >= 2`** — bật `reports.taxInput`, `reports.accountingExport`.

2. **Schema PO không đổi** — đã có `subtotal`, `tax_amount`, `total_amount`, `vat_treatment_id`.

3. **Export kế toán** — cột chuẩn: `PHASE_2_ACCOUNTING_EXPORT_COLUMNS` (documentType, số chứng từ, NCC, MST, loại thuế, %VAT, tiền…).

4. **Báo cáo thuế đầu vào** — tổng hợp PO/GRN đã ghi sổ + thanh toán; UI route mới.

5. **Tùy chọn G2:** công nợ = tổng sau thuế, lưu thuế trên GRN, tích hợp HĐĐT.



## Bật thử Giai đoạn 2 (dev)



```ts

// product-phases.ts

export const CURRENT_PRODUCT_PHASE = 2 as const;

```



Rebuild admin — menu báo cáo thuế / export kế toán sẽ hiện khi triển khai UI tương ứng.


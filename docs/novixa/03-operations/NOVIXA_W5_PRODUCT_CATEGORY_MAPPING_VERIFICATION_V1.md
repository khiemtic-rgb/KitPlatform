# NOVIXA W5 — Xác minh ánh xạ nhóm hàng V1

Ngày lập: 2026-10-31. Phạm vi: đọc repository `E:\KitPlatform`. Không truy vấn database, không sửa code.

Bốn trạng thái dưới đây không tương đương:

| Trạng thái | Nghĩa trong báo cáo này |
|---|---|
| `CATEGORY_EXISTS` | Chuỗi `category_code` xuất hiện trong seed `product_categories` của repository. |
| `CATEGORY_HAS_PRODUCTS` | Có dòng `products.category_id` trỏ tới category đó trong seed/fixture. |
| `CATEGORY_MAPPING_VERIFIED` | Rule tri thức và danh mục sản phẩm bị ràng buộc cùng một mã, và copilot đọc đúng cột đó. |
| `CATEGORY_SEMANTICALLY_APPROPRIATE` | Sản phẩm trong nhóm, theo tên trong fixture, nằm trong phạm vi triệu chứng của rule. |

## 1. Executive summary

Năm chuỗi `HO_HAP`, `GIAM_DAU`, `DA_DAY`, `VITAMIN`, `NGAO_DUOC` **có mặt** trong seed danh mục sản phẩm demo và **trùng ký tự** với `category_codes` của 35 rule. Copilot so sánh đúng cột `product_categories.category_code`.

Điều đó chưa đủ để tiếp tục dùng category matching khi đưa tri thức vào vận hành.

- Không có khóa ngoại từ `pharmacy_knowledge_rule.category_codes` sang `product_categories`. Hai bên chỉ trùng chuỗi.
- `pharmacy_symptom.category_code` là bảng khác (`respiratory`, `digestive`, …), không phải năm mã này.
- Sản phẩm gắn năm mã chỉ có trong seed demo tenant `11111111-1111-1111-1111-111111111101`. Các file đó không nằm trong `deploy/ubuntu/migration-files.prod.txt`. Catalog production **NOT VERIFIED**.
- `SearchOtcSuggestionsAsync` dùng **OR**: khớp nhóm là đủ, không cần từ khóa.
- Fixture demo cho thấy `VITAMIN` chứa vitamin và canxi, không chứa kháng histamin hay melatonin. Rule dị ứng, phát ban, mề đay, mất ngủ vẫn nhận các SKU đó chỉ nhờ nhóm hàng.
- Không có test nào gắn năm mã này với kết quả gợi ý OTC. Test pass không chứng minh mapping.

## 2. Sơ đồ nguồn

```text
pharmacy_symptom_category          product_categories
code: respiratory, digestive, ...  (tenant_id, category_code) UNIQUE
        |                          id
        | pharmacy_symptom         |
        | .category_code           products.category_id  --> một sản phẩm, một nhóm
        v                          category_code: HO_HAP | GIAM_DAU | DA_DAY | VITAMIN | NGAO_DUOC | KHANG_SINH | ...
pharmacy_knowledge_rule
.category_codes TEXT[]  ---------- không có FK -----------> so sánh chuỗi trong SearchOtcSuggestionsAsync
HO_HAP, GIAM_DAU, ...
```

Luồng copilot:

1. `PharmacyConsultationService.SuggestAsync` chặn `stop_sale` / `refer_medical` / `refer_pharmacist` trước khi tìm hàng (`PharmacyOtcSuggestionGate.ShowsOrdinaryOtcList`).
2. `ResolveKnowledgeAsync` lấy `category_codes` từ version đã ghim và `PUBLISHED`.
3. `ConsultationRepository.SearchOtcSuggestionsAsync` lọc tenant, warehouse, tồn bán được, rồi OR giữa nhóm hàng và từ khóa.

Schema nhóm hàng: `migrations/003_catalog.sql` dòng 3–16, `uq_product_categories_code` trên `(tenant_id, category_code)`. Sản phẩm: cùng file dòng 32–35, `products.category_id` tham chiếu `product_categories(id)`. Không có bảng nối nhiều nhóm.

Schema tri thức: `migrations/295_pharmacy_symptom_taxonomy_schema.sql` dòng 17–33 (`pharmacy_symptom_category`) và dòng 126–136 (`category_codes TEXT[]`). Không có `REFERENCES product_categories`.

## 3. Bảng năm category

Nguồn sản phẩm dưới đây là seed demo, không phải catalog đang chạy.

| Code | CATEGORY_EXISTS | CATEGORY_HAS_PRODUCTS | CATEGORY_MAPPING_VERIFIED | CATEGORY_SEMANTICALLY_APPROPRIATE |
|---|---|---|---|---|
| `GIAM_DAU` | Có. `001_demo_data.sql` dòng 72, tên «Giảm đau hạ sốt». `237` dòng 125 lặp mã, không thêm SKU mới ngoài paracetamol. | Có trong fixture. PARA500, PARA_EXTRA, DECK001 Panadol Extra, DECK002 Efferalgan, DECK003 Brufen, DECK016 Tatanol. | Chuỗi trùng rule. Không có FK. Catalog ngoài demo **NOT VERIFIED**. | Tên fixture là giảm đau / hạ sốt. Khớp cả nhóm vẫn kéo mọi SKU trong nhóm, kể cả khi rule chỉ nói một hoạt chất. |
| `VITAMIN` | Có. `001_demo_data.sql` dòng 74, tên «Vitamin & bổ sung». `237` dòng 127. | Có. VITC1000 Vitamin C; DECK006 Berocca; DECK007 Calcium Corbiere; DECK017 Redoxon. | Chuỗi trùng rule. Không có FK. Catalog ngoài demo **NOT VERIFIED**. | Fixture là vitamin, khoáng, canxi. Rule dị ứng / mề đay / mất ngủ dùng mã này cho kháng histamin và melatonin. Fixture không ủng hộ nghĩa đó. |
| `DA_DAY` | Có. `004_deck_rich_demo.sql` dòng 23, tên «Dạ dày — tiêu hóa». `239` dòng 62, tên «Da day — tieu hoa». | Có. DECK008 Gastropulgite, DECK009 Smecta, DECK010 Omeprazole, DECK019 Motilium. | Chuỗi trùng rule. Không có FK. Catalog ngoài demo **NOT VERIFIED**. | Fixture gộp tiêu chảy, PPI và domperidone trong một mã. Một rule táo bón hoặc đau bụng vẫn khớp cả bốn SKU. |
| `HO_HAP` | Có. `004` dòng 24, tên «Hô hấp — cảm cúm». `239` dòng 63, tên «Ho hap — cam cum». | Có. DECK011 Decolgen, DECK012 Terpin Codeine (`drug_type` 2), DECK013 ACC, DECK020 Prospan. | Chuỗi trùng rule. Không có FK. Catalog ngoài demo **NOT VERIFIED**. | Tên nhóm là hô hấp / cảm cúm. Fixture trộn cảm cúm, long đờm và siro ho kê đơn. Siro kê đơn bị lọc OTC loại, không bị loại vì sai nhóm. |
| `NGAO_DUOC` | Có. `004` dòng 25, tên «Ngâm — dầu gió». `239` dòng 64, tên «Ngoai dung — dau gio». Cùng mã, hai tên. | Có. DECK014 Dầu gió Xanh, DECK015 Salonpas. | Chuỗi trùng rule. Không có FK. Catalog ngoài demo **NOT VERIFIED**. | Hai tên seed không thống nhất. SKU fixture là dầu gió và miếng dán. Rule ngứa da dùng mã này cạnh `VITAMIN`. |

`KHANG_SINH` có trong `001` dòng 73 và bị copilot loại riêng. Nó không thuộc năm mã của rule.

## 4. Bằng chứng

### Schema và chỗ copilot đọc

`SearchOtcSuggestionsAsync` viết hoa mã nhóm đầu vào rồi so sánh bằng:

```307:317:src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Consultation/ConsultationRepository.cs
              AND (
                (cardinality(@CategoryCodes::text[]) > 0 AND c.category_code = ANY(@CategoryCodes))
                OR (
                  cardinality(@KeywordPatterns::text[]) > 0
                  AND (
                    p.product_name ILIKE ANY(@KeywordPatterns)
                    OR COALESCE(p.generic_name, '') ILIKE ANY(@KeywordPatterns)
                    OR COALESCE(p.description, '') ILIKE ANY(@KeywordPatterns)
                  )
                )
              )
```

Join nhóm hàng: cùng file dòng 265–266, `product_categories` theo `products.category_id` và cùng `tenant_id`. Mã được `Trim().ToUpperInvariant()` ở dòng 227–230. Cột trong SQL **không** được `UPPER`. Seed demo ghi mã chữ hoa nên khớp. Mã tenant viết thường sẽ không khớp nhánh nhóm. `CatalogRepository.GetCategoryCodeMapAsync` dòng 838 lại `UPPER(category_code)` và so không phân biệt hoa thường. Hai đường không giống nhau.

Bộ lọc khác, cùng hàm, dòng 283–306 và 318–327:

- Lô đúng `tenant_id` và `warehouse_id`, `quantity_available > 0`, `expiry_date` trống hoặc chưa qua ngày.
- `products.deleted_at IS NULL`, `status = 1`.
- `dispensing_class` suy ra hoặc ghi sẵn phải là `otc`.
- `drug_type` phải là 1. Cột `dispensing_class` thêm ở `migrations/095_rx_dispensing_class.sql` dòng 5, mặc định `otc`.
- `category_code <> 'KHANG_SINH'`.
- Từ loại trừ áp trên tên, hoạt chất, mô tả.
- Phải có một `product_units`. Giá không phải điều kiện lọc.
- `LIMIT` bị kẹp 1–12, dòng 340. Sắp theo số bán 90 ngày, rồi tồn, rồi tên.

Tenant của JWT không được thay bằng tenant trong body. Warehouse được kiểm ở `SuggestAsync` dòng 173.

### Seed nào khai báo mã

| File | Việc làm | Trong prod manifest |
|---|---|---|
| `migrations/003_catalog.sql` | Tạo bảng, không ghi năm mã | Có, dòng 5 của `migration-files.prod.txt` |
| `migrations/seed/001_demo_data.sql` dòng 70–74 | `GIAM_DAU`, `KHANG_SINH`, `VITAMIN` | Không có tên file này trong manifest |
| `migrations/seed/004_deck_rich_demo.sql` dòng 21–25 | `DA_DAY`, `HO_HAP`, `NGAO_DUOC` | Không |
| `migrations/237_demo_pharmacy_audit_handoff.sql` dòng 122–127 | Lặp `GIAM_DAU`, `KHANG_SINH`, `VITAMIN` | Không thấy trong `migration-files.prod.txt` |
| `migrations/239_demo_pharmacy_realistic_data.sql` dòng 60–64 | Lặp `DA_DAY`, `HO_HAP`, `NGAO_DUOC` | Không |
| `migrations/296_pharmacy_symptom_taxonomy_seed.sql` dòng 510–541 | Gắn năm mã vào rule | Có, dòng 220 của manifest |

`004` dòng 25 gọi `NGAO_DUOC` là «Ngâm — dầu gió». `239` dòng 64 gọi cùng mã là «Ngoai dung — dau gio». Cả hai `ON CONFLICT DO NOTHING`. Tên giữ lại phụ thuộc file chạy trước. Copilot không đọc `category_name`.

`296` dòng 9–28 tạo `pharmacy_symptom_category` với `respiratory`, `digestive`, `skin_allergy`, `neuro_sleep`, `other`. Đó không phải `HO_HAP`. Rule hô hấp vừa thuộc symptom category `respiratory` vừa khai `category_codes = HO_HAP`.

`ConsultationOtcRules.cs` dòng 19–169 lặp lại các mảng nhóm này trong code. Copilot W5 lấy nhóm từ version đã ghim, không lấy từ class này để tìm hàng. Class này vẫn cấp `GetExcludeKeywords` (dòng 191–204).

### Test

Tìm `HO_HAP`, `GIAM_DAU`, `SearchOtcSuggestions` trong các file test: không có chỗ nào gắn năm mã demo với một sản phẩm trả về. Test W5 dùng symptom và version tổng hợp. Chúng không chứng minh mapping category.

## 5. Rule rủi ro cao — khớp chỉ nhờ nhóm

Danh sách SKU lấy từ seed demo ở mục 4. Nếu tên, `generic_name` hoặc `description` trong các file đó không chứa từ khóa của rule, SKU thuộc nhánh B. Đây không phải hàng đang bán trên production.

**KR_ALLERGY, KR_RASH, KR_HIVES.** Nhóm `VITAMIN`. Từ khóa là loratadine, cetirizine, tên thương mại kháng histamin và chữ dị ứng / phát ban / mề đay (`296` dòng 531, 533, 534). Bốn SKU vitamin/canxi không chứa các chuỗi đó. Cả bốn khớp chỉ nhờ nhóm: VITC1000, DECK006, DECK007, DECK017.

**KR_ITCHY_SKIN.** Nhóm `VITAMIN` và `NGAO_DUOC` (`296` dòng 532). Thêm DECK014 dầu gió và DECK015 Salonpas. Mô tả fixture không chứa `ngua` / `ngứa`. Sáu SKU khớp chỉ nhờ nhóm.

**KR_DIZZINESS.** Nhóm `VITAMIN` (`296` dòng 539). Từ khóa có `berocca` và `vitamin`. Berocca, Vitamin C và Redoxon dính từ khóa. Calcium Corbiere (`004` dòng 71, `239` dòng 124–125) không có chuỗi `vitamin` trong tên, hoạt chất hay mô tả của hai file đó. SKU này khớp chỉ nhờ nhóm.

**KR_INSOMNIA.** Nhóm `VITAMIN` (`296` dòng 540). Từ khóa `melatonin`, `mat ngu`, `mất ngủ`. Không SKU fixture nào chứa các chuỗi đó. Cả bốn SKU vitamin khớp chỉ nhờ nhóm.

**KR_OTHER.** Từ khóa rỗng (`296` dòng 541). Nhóm `VITAMIN`, `GIAM_DAU`, `HO_HAP`. Mọi OTC còn hàng trong ba nhóm đều khớp. Trong fixture, nhánh OTC là: bốn SKU vitamin; PARA500, PARA_EXTRA, DECK001, DECK002, DECK003, DECK016; DECK011, DECK013, DECK020. DECK012 Terpin Codeine cùng nhóm `HO_HAP` nhưng `drug_type` 2 và `dispensing_class` prescription (`239` dòng 134–135) nên bị lọc OTC loại ra.

**KR_DEHYDRATION.** Nhóm `DA_DAY`, `VITAMIN` (`297` dòng 46). Từ khóa là oresol, ORS, bù nước, điện giải. Bốn SKU `DA_DAY` và bốn SKU `VITAMIN` trong fixture không chứa các chuỗi đó. Cả tám khớp chỉ nhờ nhóm, gồm Omeprazole và Motilium.

**KR_HEADACHE.** Nhóm `GIAM_DAU` (`296` dòng 520). Từ khóa gồm paracetamol, panadol, efferalgan, ibuprofen, brufen, tatanol. Sáu SKU `GIAM_DAU` của fixture đều dính ít nhất một từ khóa. Fixture này không có thêm SKU khớp chỉ nhờ nhóm. Một sản phẩm cùng nhóm mà tên không chứa các chuỗi đó vẫn được trả về. Fixture không có SKU như vậy. Catalog thật **NOT VERIFIED**.

**KR_JOINT_PAIN.** Nhóm `GIAM_DAU`, `NGAO_DUOC` (`296` dòng 536). Từ khóa ibuprofen, brufen, salonpas, đau khớp. Khớp chỉ nhờ nhóm trong fixture: PARA500, PARA_EXTRA, Panadol Extra, Efferalgan, Tatanol, Dầu gió Xanh. Brufen khớp từ khóa. Salonpas khớp từ khóa `salonpas`.

## 6. Bốn tình huống matching

Điều kiện là OR ở `ConsultationRepository.cs` dòng 307–317, sau các bộ lọc mục 4. Không đề xuất đổi OR thành AND trong tài liệu này.

| Tình huống | Điều kiện sau bộ lọc | Kết quả |
|---|---|---|
| A. Khớp từ khóa, không khớp nhóm | Tên, hoạt chất hoặc mô tả `ILIKE %từ khóa%`. `category_code` không nằm trong mảng rule, hoặc sản phẩm không có nhóm. | Vẫn trả về. Ví dụ cơ chế: sản phẩm nhóm `DA_DAY` vẫn ra khi rule chỉ có `HO_HAP` nếu tên chứa từ khóa của rule. |
| B. Khớp nhóm, không khớp từ khóa | `category_code = ANY(category_codes)`. Không chuỗi từ khóa nào nằm trong tên, hoạt chất, mô tả. | Vẫn trả về. Đây là các SKU mục 5. |
| C. Khớp cả hai | Cả hai nhánh đúng. | Trả về một lần. SQL không chấm điểm nhánh nào. |
| D. Không khớp cả hai | Ngoài mảng nhóm và ngoài mọi từ khóa. | Không trả về, dù còn hàng và là OTC. |

`KR_OTHER` chỉ có nhánh B và D, vì mảng từ khóa rỗng và `cardinality = 0` làm nhánh từ khóa sai (`ConsultationRepository.cs` dòng 309–310).

Rủi ro của OR theo nhóm: recall của copilot bằng mọi OTC trong nhóm, không bằng giao của nhóm và từ khóa. Với fixture demo, nhóm `VITAMIN` và `DA_DAY` kéo SKU ngoài phạm vi chữ trong `reason_vi`. Giới hạn 12 dòng (`ConsultationRepository.cs` dòng 340) cắt danh sách theo sức bán 90 ngày, không theo độ gần triệu chứng.

## 7. Đã xác minh và NOT VERIFIED

Đã xác minh từ file:

- Năm chuỗi tồn tại như `product_categories.category_code` trên tenant demo cố định trong `001`, `004`, `237`, `239`.
- Cùng năm chuỗi nằm trong `category_codes` của rule `296` / `297` / `298`.
- Copilot so sánh với `product_categories.category_code` của đúng tenant, bằng OR.
- Fixture demo có SKU cho cả năm mã.
- Hai hệ category symptom và product không dùng chung mã.
- Test không chứng minh mapping.

**NOT VERIFIED:**

- Bất kỳ `product_categories` nào trên database đang chạy, gồm production và database local.
- Nhà thuốc dùng mã khác năm chuỗi này. Khi đó nhánh nhóm không khớp; nhánh từ khóa vẫn chạy.
- Nhà thuốc ghi mã chữ thường.
- Độ đầy của từng nhóm ngoài 20 SKU DECK và 4 SKU `001`.
- Ý nghĩa lâm sàng của việc một SKU fixture xuất hiện trong gợi ý.

## 8. Category nào còn đưa vào đánh giá chuyên môn

Category matching **chưa đủ đáng tin để bật khi vận hành**. Chuỗi thì khớp trong seed demo. Nghĩa của nhóm và catalog thật thì chưa.

Có thể đưa vào buổi chuyên môn như hồ sơ đối chiếu chuỗi, với điều kiện người phụ trách danh mục xác nhận mã trên catalog thật:

- `GIAM_DAU`, `HO_HAP`, `DA_DAY`, `NGAO_DUOC`: mã trùng và fixture cùng chủ đề rộng với tên nhóm. Buổi chuyên môn cần trả lời nhóm có được phép khớp cả nhóm hay không. `KR_JOINT_PAIN` và `KR_DEHYDRATION` cần xem trước vì fixture đã có SKU khớp chỉ nhờ nhóm và nằm ngoài từ khóa.
- `VITAMIN`: chưa nên coi là nhóm của rule dị ứng, ngứa, phát ban, mề đay, chóng mặt, mất ngủ. Fixture mâu thuẫn với từ khóa của các rule đó. `KR_ALLERGY`, `KR_ITCHY_SKIN`, `KR_RASH`, `KR_HIVES`, `KR_DIZZINESS`, `KR_INSOMNIA` xử lý trước.
- `KR_OTHER`: không đưa vào vận hành trên cơ sở category matching. Từ khóa rỗng, ba nhóm đều là điều kiện đủ.

Không có category nào đạt `CATEGORY_MAPPING_VERIFIED` cho catalog vận hành. Cả năm chỉ đạt `CATEGORY_EXISTS` và `CATEGORY_HAS_PRODUCTS` trong seed demo.

## 9. Câu hỏi cần xác nhận

1. Catalog nhà thuốc đang dùng có đúng năm `category_code` này, đúng chữ hoa, trên `product_categories` không?
2. `NGAO_DUOC` là «ngoài dùng» hay «ngâm», theo tên nào trong `004` dòng 25 và `239` dòng 64?
3. Một sản phẩm chỉ được một `category_id` (`003_catalog.sql` dòng 35). Kháng histamin đang nằm ở mã nào, nếu không phải `VITAMIN`?
4. Danh mục thật trong `GIAM_DAU` có SKU không chứa paracetamol, ibuprofen, panadol, efferalgan, tatanol không? Rule đau đầu sẽ trả các SKU đó.
5. `DA_DAY` có đang chứa đồng thời smecta, omeprazole và domperidone như DECK008–DECK010 và DECK019 không?
6. Ai xác nhận một nhóm được phép trả mọi OTC trong nhóm, kể cả khi tên không chứa từ khóa?
7. `KR_OTHER` có bị loại khỏi mọi đợt vận hành cho đến khi có từ khóa và nhóm hẹp hơn không?

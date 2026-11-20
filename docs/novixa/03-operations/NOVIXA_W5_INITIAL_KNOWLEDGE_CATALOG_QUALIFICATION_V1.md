# NOVIXA W5 — Hồ sơ đánh giá catalog tri thức ban đầu V1

Ngày lập: 2026-10-31. Phạm vi: đọc repository `E:\KitPlatform`. Không phê duyệt chuyên môn, không xuất bản, không sửa rule, không đụng database.

Đây là hồ sơ để chuyên gia xem. Không có rule nào trong tài liệu này được coi là `PUBLISHED`.

## 1. Executive summary

Catalog seed có đúng 35 `rule_code`: 32 trong `migrations/296_pharmacy_symptom_taxonomy_seed.sql`, 1 trong `297_pharmacy_consultation_dehydration_tune.sql`, 2 trong `298_pharmacy_consultation_ent_otc.sql`.

Không rule nào ghi `reference_source`. Cột này chỉ xuất hiện từ migration `414` và seed không gán giá trị. Nhãn `source` mặc định của bảng là `novixa`. Đó là nhãn hệ thống, không phải căn cứ chuyên môn.

**Không có ứng viên nào đủ điều kiện xuất bản.** Thiếu nguồn là khoảng trống của cả 35 mã.

Phân loại chính, mỗi mã một nhãn:

| Nhãn | Số | Ý nghĩa |
|---|---:|---|
| `CANDIDATE_FOR_CLINICAL_REVIEW` | 17 | Đủ symptom, từ khóa không rỗng, nhóm hàng, lý do và priority để chuyên gia đọc. Chưa được xuất bản. |
| `NEEDS_CLINICAL_REVIEW` | 17 | Nội dung seed tự nêu điểm cần dược sĩ, hoặc ánh xạ sang domperidone, omeprazole/PPI, ibuprofen, melatonin, hoặc nhóm `VITAMIN` không được seed giải thích. |
| `NEEDS_SOURCE` | 35 | Mọi mã đều thiếu nguồn. Đây là cờ kèm theo, không thay nhãn chính. |
| `NOT_ELIGIBLE` | 1 | `KR_OTHER`: từ khóa rỗng, vẫn gắn ba nhóm hàng. |

Product matching không phải bằng chứng an toàn. Sàn an toàn nằm ở `ConsultationSafetyRules` và cổng `PharmacyOtcSuggestionGate`, chạy trước khi tìm sản phẩm.

Mức chắc chắn của các trường copy từ seed: cao. Mức chắc chắn rằng category code trong seed trùng category code sản phẩm của một nhà thuốc: **NOT VERIFIED**.

## 2. Ba lớp không được gộp

### A. Product matching

Copilot chỉ đọc pin `APPROVED` của đúng tenant, trỏ tới version `PUBLISHED` còn hiệu lực, rồi mới tìm hàng. Không pin thì không có từ khóa và không có nhóm hàng.

`SearchOtcSuggestionsAsync` khớp một sản phẩm OTC còn hàng khi **một trong hai** điều sau đúng:

- `product_categories.category_code` nằm trong `category_codes` của rule, hoặc
- `product_name`, `generic_name` hoặc `description` khớp `ILIKE %từ khóa%`.

Khớp theo nhóm hàng không cần từ khóa. Sản phẩm loại kháng sinh (`KHANG_SINH`), không phải OTC, hoặc hết hàng bán được thì bị loại. Tồn kho lọc theo tenant và warehouse.

`exclude_keywords` trên cả 35 rule trong seed là mảng rỗng, vì lệnh INSERT không ghi cột này và schema mặc định `'{}'`.

Ngoài rule, `ConsultationOtcRules.GetExcludeKeywords` thêm từ loại trừ lúc chạy, không ghi vào rule:

- Trẻ dưới 2 tuổi, mang thai hoặc cho con bú: `ibuprofen`, `brufen`, `aspirin`, `nsaid`, `codeine`.
- Mang thai, thêm: `domperidone`, `motilium`.

Nhà thuốc chỉ được thêm từ loại trừ sau khi đã ghim. Không được thêm từ khóa hoặc nhóm hàng.

### B. Clinical safety

`ConsultationSafetyRules.Evaluate` luôn chạy trước tìm sản phẩm. Ba mức `stop_sale`, `refer_medical`, `refer_pharmacist` làm `PharmacyOtcSuggestionGate` trả danh sách sản phẩm rỗng. `caution` và `none` vẫn cho tìm hàng.

Luật hardcoded không gắn với từng `KR_*`, trừ hai chỗ đọc mã triệu chứng:

- Triệu chứng `allergy`: cờ `allergy_symptom`, mức `caution`.
- Trẻ nhỏ và có sốt: `infant_fever`, mức `refer_medical`.

Các cờ còn lại phụ thuộc tuổi, mang thai, cho con bú và red flag, không phụ thuộc rule gợi ý. Catalog `pharmacy_safety_flag_review` không phải nguồn thực thi.

Trong 35 symptom của catalog này, seed `296` chỉ nối risk flag cho `dehydration_signs` → `severe_dehydration` (`refer_medical`, câu trong seed: «Mất nước nặng - bù nước và khám ngay.»). Các symptom `KR_*` còn lại không có dòng trong `pharmacy_symptom_risk_rule`.

### C. Clinical evidence

Seed không có tài liệu hướng dẫn, tờ rơi, hoặc nguồn trích. `reason_vi` là một câu mô tả cách khớp, không phải chỉ định. Khoảng hiệu lực lâm sàng không được viết trong seed. `effective_from` nhận `DEFAULT CURRENT_DATE` lúc INSERT. `effective_to` để trống. Version mặc định là `1`.

## 3. Danh mục 35 quy tắc

Cột loại là nhãn chính. Cả 35 đều `NEEDS_SOURCE`. Excluded keywords trên rule: không có. Reference source: không có. Effective period lâm sàng: không có trong seed.

| rule_code | Symptom | Nhóm hàng | Priority | Loại chính |
|---|---|---|---:|---|
| KR_COUGH_DRY | cough_dry / Ho khan | HO_HAP | 100 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_COUGH_PHLEGM | cough_phlegm / Ho có đờm | HO_HAP | 110 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_COUGH | cough / Ho | HO_HAP | 120 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_RUNNY_NOSE | runny_nose / Sổ mũi | HO_HAP | 130 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_NASAL_CONG | nasal_congestion / Nghẹt mũi | HO_HAP | 140 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_SORE_THROAT | sore_throat / Đau họng | HO_HAP, GIAM_DAU | 150 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_SNEEZING | sneezing / Hắt hơi | HO_HAP | 160 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_COMMON_COLD | common_cold / Cảm lạnh / cảm cúm | HO_HAP | 170 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_FLU | flu_symptoms / Triệu chứng cúm | HO_HAP, GIAM_DAU | 180 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_FEVER | fever / Sốt | GIAM_DAU | 190 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_HEADACHE | headache / Đau đầu | GIAM_DAU | 200 | NEEDS_CLINICAL_REVIEW |
| KR_BODY_ACHE | body_ache / Đau cơ / đau nhức | GIAM_DAU, NGAO_DUOC | 210 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_CHILLS | chills / Rét run | GIAM_DAU, HO_HAP | 220 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_DIARRHEA | diarrhea / Tiêu chảy | DA_DAY | 230 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_NAUSEA | nausea / Buồn nôn | DA_DAY | 240 | NEEDS_CLINICAL_REVIEW |
| KR_VOMITING | vomiting / Nôn / ói | DA_DAY | 250 | NEEDS_CLINICAL_REVIEW |
| KR_HEARTBURN | heartburn / Ợ nóng | DA_DAY | 260 | NEEDS_CLINICAL_REVIEW |
| KR_REFLUX | reflux / Trào ngược / ợ chua | DA_DAY | 270 | NEEDS_CLINICAL_REVIEW |
| KR_CONSTIPATION | constipation / Táo bón | DA_DAY | 280 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_BLOATING | bloating / Đầy bụng | DA_DAY | 290 | NEEDS_CLINICAL_REVIEW |
| KR_ABDOMINAL | abdominal_pain / Đau bụng | DA_DAY | 300 | NEEDS_CLINICAL_REVIEW |
| KR_ALLERGY | allergy / Dị ứng | VITAMIN | 310 | NEEDS_CLINICAL_REVIEW |
| KR_ITCHY_SKIN | itchy_skin / Ngứa da | VITAMIN, NGAO_DUOC | 320 | NEEDS_CLINICAL_REVIEW |
| KR_RASH | rash / Phát ban | VITAMIN | 330 | NEEDS_CLINICAL_REVIEW |
| KR_HIVES | hives / Mề đay / mẩn đỏ | VITAMIN | 340 | NEEDS_CLINICAL_REVIEW |
| KR_BACK_PAIN | back_pain / Đau lưng | GIAM_DAU, NGAO_DUOC | 350 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_JOINT_PAIN | joint_pain / Đau khớp | GIAM_DAU, NGAO_DUOC | 360 | NEEDS_CLINICAL_REVIEW |
| KR_TOOTHACHE | toothache / Đau răng | GIAM_DAU | 370 | NEEDS_CLINICAL_REVIEW |
| KR_FATIGUE | fatigue / Mệt mỏi | VITAMIN | 380 | NEEDS_CLINICAL_REVIEW |
| KR_DIZZINESS | dizziness / Chóng mặt | VITAMIN | 390 | NEEDS_CLINICAL_REVIEW |
| KR_INSOMNIA | insomnia / Mất ngủ / khó ngủ | VITAMIN | 400 | NEEDS_CLINICAL_REVIEW |
| KR_OTHER | other / Khác / chưa rõ | VITAMIN, GIAM_DAU, HO_HAP | 410 | NOT_ELIGIBLE |
| KR_DEHYDRATION | dehydration_signs / Dấu hiệu mất nước | DA_DAY, VITAMIN | 235 | NEEDS_CLINICAL_REVIEW |
| KR_HOARSENESS | voice_hoarseness / Khàn tiếng | HO_HAP, GIAM_DAU | 145 | CANDIDATE_FOR_CLINICAL_REVIEW |
| KR_THROAT_CLEARING | throat_clearing / Khản tiếng / vướng họng | HO_HAP | 146 | CANDIDATE_FOR_CLINICAL_REVIEW |

`consultation_mode` trong seed: các symptom trên là `otc_assist`, trừ hai điểm được sửa sau. `dehydration_signs` bắt đầu là `capture_only` rồi `297` đổi thành `otc_assist`. `voice_hoarseness` bắt đầu là `capture_only` rồi `298` đổi thành `otc_assist`. `throat_clearing` đã là `otc_assist` trong `296`. Copilot chỉ lấy symptom `otc_assist`.

Priority nhỏ hơn được sắp trước (`ORDER BY` priority tăng dần).

## 4. Hồ sơ từng quy tắc

Các mục dưới dùng chung, trừ khi ghi riêng:

- Excluded keywords trên rule: không có.
- Reference source: không có.
- Effective period lâm sàng: không có trong seed. `effective_to` trống.
- Thiếu: nguồn trích dẫn; bằng chứng rằng mã nhóm hàng trùng danh mục sản phẩm của nhà thuốc (**NOT VERIFIED**).
- Safety chung: sàn hardcoded và cổng ba mức chặn. Không có risk flag riêng, trừ `KR_DEHYDRATION`.
- Đề xuất: không xuất bản trong đợt này. Đưa vào đúng hàng ở mục 8.

### Ứng viên để chuyên gia đọc

**KR_COUGH_DRY.** Symptom `cough_dry` (Ho khan), nhóm symptom `respiratory`, `otc_assist`, mã `RESP-002`. Keywords: `acc`, `acetylcysteine`, `prospan`, `ivy`, `ho khan`, `long dom`, `long đờm`. Categories: `HO_HAP`. Rationale: «Ho khan - long đờm / giảm ho OTC». Priority 100. Rủi ro khớp sai: mọi sản phẩm nhóm `HO_HAP`, kể cả khi tên không chứa từ khóa. Seed có câu hỏi tuổi, thời gian, sốt, khó thở, mức độ cho symptom này. Đề xuất: chuyên gia đọc cách khớp, chưa xuất bản.

**KR_COUGH_PHLEGM.** Symptom `cough_phlegm` (Ho có đờm), `RESP-003`. Keywords: `acc`, `acetylcysteine`, `prospan`, `ivy`, `long dom`, `long đờm`. Categories: `HO_HAP`. Rationale: «Ho đờm - long đờm OTC». Priority 110. Cùng rủi ro khớp cả nhóm `HO_HAP`. Cùng bộ câu hỏi với ho khan. Đề xuất: chuyên gia đọc, chưa xuất bản.

**KR_COUGH.** Symptom `cough` (Ho), `RESP-001`. Keywords: `prospan`, `decolgen`, `cam cum`, `ho`. Categories: `HO_HAP`. Rationale: «Ho - giảm ho / cảm cúm OTC». Priority 120. Rủi ro khớp sai: từ khóa `ho` dài 2 ký tự, SQL dùng `ILIKE %ho%` trên tên, hoạt chất và mô tả. Thêm khớp cả nhóm `HO_HAP`. Có bộ câu hỏi tuổi, thời gian, sốt, khó thở, mức độ. Đề xuất: chuyên gia xem từ khóa ngắn trước khi xét xuất bản. Chưa xuất bản.

**KR_RUNNY_NOSE.** Symptom `runny_nose` (Sổ mũi), `RESP-004`. Keywords: `decolgen`, `cam cum`, `so mui`, `sổ mũi`, `phenylephrine`. Categories: `HO_HAP`. Rationale: «Sổ mũi - thuốc cảm cúm OTC». Priority 130. Rủi ro: khớp cả nhóm `HO_HAP` và tên thương mại. Đề xuất: chuyên gia đọc, chưa xuất bản.

**KR_NASAL_CONG.** Symptom `nasal_congestion` (Nghẹt mũi), `RESP-005`. Keywords: `decolgen`, `cam cum`, `nghet mui`, `nghẹt mũi`. Categories: `HO_HAP`. Rationale: «Nghẹt mũi - thuốc cảm cúm OTC». Priority 140. Rủi ro: khớp cả nhóm `HO_HAP`. Đề xuất: chuyên gia đọc, chưa xuất bản.

**KR_SORE_THROAT.** Symptom `sore_throat` (Đau họng), `RESP-006`. Keywords: `decolgen`, `cam cum`, `dau hong`, `đau họng`, `paracetamol`. Categories: `HO_HAP`, `GIAM_DAU`. Rationale: «Đau họng - giảm đau / cảm cúm OTC». Priority 150. Rủi ro: hai nhóm hàng, nên một sản phẩm giảm đau không mang từ khóa hô hấp vẫn có thể khớp nếu category là `GIAM_DAU`. Đề xuất: chuyên gia đọc phạm vi hai nhóm, chưa xuất bản.

**KR_SNEEZING.** Symptom `sneezing` (Hắt hơi), `RESP-007`. Keywords: `decolgen`, `cam cum`, `hat hoi`, `hắt hơi`. Categories: `HO_HAP`. Rationale: «Hắt hơi - cảm cúm OTC». Priority 160. Rủi ro: khớp cả nhóm `HO_HAP`. Đề xuất: chuyên gia đọc, chưa xuất bản.

**KR_COMMON_COLD.** Symptom `common_cold` (Cảm lạnh / cảm cúm), `RESP-008`. Keywords: `decolgen`, `cam cum`, `cam lanh`, `cảm lạnh`. Categories: `HO_HAP`. Rationale: «Cảm lạnh - thuốc cảm cúm OTC». Priority 170. Rủi ro: khớp cả nhóm `HO_HAP`. Đề xuất: chuyên gia đọc, chưa xuất bản.

**KR_FLU.** Symptom `flu_symptoms` (Triệu chứng cúm), `RESP-009`. Keywords: `decolgen`, `cam cum`, `paracetamol`, `cum`, `cúm`. Categories: `HO_HAP`, `GIAM_DAU`. Rationale: «Cúm - thuốc cảm cúm / hạ sốt OTC». Priority 180. Rủi ro: hai nhóm hàng. Từ khóa `cum` là chuỗi con. Đề xuất: chuyên gia xem từ khóa ngắn và hai nhóm hàng, chưa xuất bản.

**KR_FEVER.** Symptom `fever` (Sốt), `FEV-001`. Keywords: `paracetamol`, `panadol`, `efferalgan`, `tatanol`, `ha sot`, `hạ sốt`. Categories: `GIAM_DAU`. Rationale: «Sốt - hạ sốt paracetamol OTC». Priority 190. Rủi ro: khớp cả nhóm `GIAM_DAU`, không chỉ paracetamol. Trẻ nhỏ có sốt bị `infant_fever` / `refer_medical` và không ra danh sách sản phẩm. Có câu hỏi tuổi, thời gian, mức độ, thuốc đang dùng. Đề xuất: chuyên gia đọc, chưa xuất bản.

**KR_BODY_ACHE.** Symptom `body_ache` (Đau cơ / đau nhức), `FEV-003`. Keywords: `paracetamol`, `panadol`, `salonpas`, `dau nhuc`, `đau nhức`. Categories: `GIAM_DAU`, `NGAO_DUOC`. Rationale: «Đau nhức - giảm đau OTC». Priority 210. Rủi ro: hai nhóm hàng. Đề xuất: chuyên gia đọc, chưa xuất bản.

**KR_CHILLS.** Symptom `chills` (Rét run), `FEV-004`. Keywords: `paracetamol`, `decolgen`, `cam cum`. Categories: `GIAM_DAU`, `HO_HAP`. Rationale: «Rét run - hạ sốt / cảm cúm OTC». Priority 220. Rủi ro: hai nhóm hàng. Đề xuất: chuyên gia đọc, chưa xuất bản.

**KR_DIARRHEA.** Symptom `diarrhea` (Tiêu chảy), `DIG-001`. Keywords: `smecta`, `diosmectite`, `gastropulgite`, `tieu chay`, `tiêu chảy`. Categories: `DA_DAY`. Rationale: «Tiêu chảy - men vi sinh / smecta OTC». Priority 230. Thiếu thêm: rationale nói men vi sinh, danh sách từ khóa không có cụm men vi sinh. Rủi ro: khớp cả nhóm `DA_DAY`. Có câu hỏi tuổi, thời gian, sốt, mức độ, thuốc đang dùng. Đề xuất: chuyên gia đối chiếu câu rationale với từ khóa, chưa xuất bản.

**KR_CONSTIPATION.** Symptom `constipation` (Táo bón), `DIG-007`. Keywords: `constipation`, `tao bon`, `táo bón`, `fiber`. Categories: `DA_DAY`. Rationale: «Táo bón - nhuận tràng OTC». Priority 280. Rủi ro: khớp cả nhóm `DA_DAY`. Đề xuất: chuyên gia đọc, chưa xuất bản.

**KR_BACK_PAIN.** Symptom `back_pain` (Đau lưng), `MUS-001`. Keywords: `paracetamol`, `salonpas`, `dau lung`, `đau lưng`. Categories: `GIAM_DAU`, `NGAO_DUOC`. Rationale: «Đau lưng - giảm đau / dán OTC». Priority 350. Rủi ro: hai nhóm hàng. Đề xuất: chuyên gia đọc, chưa xuất bản.

**KR_HOARSENESS.** Symptom `voice_hoarseness` (Khàn tiếng), `ENT-009`. `298` đặt `consultation_mode = otc_assist`. Keywords: `prospan`, `acc`, `acetylcysteine`, `decolgen`, `cam cum`, `khan tieng`, `khàn tiếng`, `ho khan`, `dau hong`, `đau họng`, `long dom`, `long đờm`. Categories: `HO_HAP`, `GIAM_DAU`. Rationale: «Khàn tiếng / ho — long đờm / giảm ho OTC». Priority 145. Rủi ro: hai nhóm hàng và nhiều tên thương mại. Đề xuất: chuyên gia đọc, chưa xuất bản.

**KR_THROAT_CLEARING.** Symptom `throat_clearing` (Khản tiếng / vướng họng), `ENT-008`, đã `otc_assist` trong `296`. Keywords: `prospan`, `decolgen`, `cam cum`, `vuong hong`, `vướng họng`, `khan tieng`, `khàn tiếng`, `dau hong`, `đau họng`. Categories: `HO_HAP`. Rationale: «Vướng họng / khản tiếng — giảm ho / cảm cúm OTC». Priority 146. Rủi ro: khớp cả nhóm `HO_HAP`. Đề xuất: chuyên gia đọc, chưa xuất bản.

### Cần đánh giá lâm sàng riêng

**KR_HEADACHE.** Symptom `headache` (Đau đầu), `FEV-002`. Keywords: `paracetamol`, `panadol`, `efferalgan`, `ibuprofen`, `brufen`, `dau dau`, `đau đầu`. Categories: `GIAM_DAU`. Rationale: «Đau đầu - giảm đau OTC». Priority 200. Rủi ro khớp sai: cả nhóm `GIAM_DAU`, cộng từ khóa ibuprofen. Runtime chỉ loại `ibuprofen`/`brufen` khi trẻ dưới 2 tuổi, mang thai hoặc cho con bú. Các trường hợp khác seed không loại. An toàn liên quan: sàn chung; không có risk flag cho symptom `headache` trong seed `296`. Đề xuất: dược sĩ xem từ khóa ibuprofen trước mọi bàn xuất bản. Chưa xuất bản.

**KR_JOINT_PAIN.** Symptom `joint_pain` (Đau khớp), `MUS-002`. Keywords: `ibuprofen`, `brufen`, `salonpas`, `dau khop`, `đau khớp`. Categories: `GIAM_DAU`, `NGAO_DUOC`. Rationale: «Đau khớp - giảm đau OTC». Priority 360. Cùng khoảng trống ibuprofen như trên, và khớp hai nhóm hàng. Đề xuất: dược sĩ xem riêng. Chưa xuất bản.

**KR_NAUSEA.** Symptom `nausea` (Buồn nôn), `DIG-002`. Keywords: `motilium`, `domperidone`, `buon non`, `buồn nôn`. Categories: `DA_DAY`. Rationale: «Buồn nôn - chống nôn OTC (cần DS nếu mang thai)». Priority 240. Rủi ro: rationale chỉ nói cần dược sĩ nếu mang thai. Runtime loại `domperidone`/`motilium` khi mang thai, không loại ở các trường hợp khác. Khớp cả nhóm `DA_DAY`. Đề xuất: không đưa vào đợt rà soát xuất bản thường. Chưa xuất bản.

**KR_VOMITING.** Symptom `vomiting` (Nôn / ói), `DIG-003`. Keywords: `motilium`, `domperidone`, `non`, `nôn`, `smecta`. Categories: `DA_DAY`. Rationale: «Nôn - chống nôn / bù nước OTC». Priority 250. Rủi ro: từ khóa `non` là chuỗi con; domperidone nằm trong từ khóa; khớp cả nhóm `DA_DAY`. Seed không nói giới hạn ngoài câu rationale. Loại domperidone lúc chạy chỉ khi mang thai. Đề xuất: dược sĩ xem riêng. Chưa xuất bản.

**KR_BLOATING.** Symptom `bloating` (Đầy bụng), `DIG-008`. Keywords: `simethicone`, `day bung`, `đầy bụng`, `motilium`. Categories: `DA_DAY`. Rationale: «Đầy bụng - tiêu hóa OTC». Priority 290. Rủi ro: `motilium` nằm trong từ khóa trong khi rationale không nhắc domperidone. Khớp cả nhóm `DA_DAY`. Đề xuất: dược sĩ xem vì tên thương mại này. Chưa xuất bản.

**KR_HEARTBURN.** Symptom `heartburn` (Ợ nóng), `DIG-005`. Keywords: `omeprazole`, `antacid`, `o nong`, `ợ nóng`. Categories: `DA_DAY`. Rationale: «Ợ nóng - kháng acid OTC». Priority 260. Rủi ro: rationale nói kháng acid, từ khóa có `omeprazole`. Khớp cả nhóm `DA_DAY`. Seed không có điều kiện tuổi, thời gian dùng, hay loại trừ. Đề xuất: dược sĩ xem riêng. Chưa xuất bản.

**KR_REFLUX.** Symptom `reflux` (Trào ngược / ợ chua), `DIG-006`. Keywords: `omeprazole`, `trao nguoc`, `trào ngược`. Categories: `DA_DAY`. Rationale: «Trào ngược - PPI OTC». Priority 270. Rủi ro: rationale tự gọi PPI. Khớp cả nhóm `DA_DAY`, nên một sản phẩm tiêu hóa khác trong nhóm vẫn khớp dù không phải PPI. Seed không có giới hạn dùng. Đề xuất: dược sĩ xem riêng. Chưa xuất bản.

**KR_ABDOMINAL.** Symptom `abdominal_pain` (Đau bụng), `DIG-004`. Keywords: `smecta`, `dau bung`, `đau bụng`. Categories: `DA_DAY`. Rationale: «Đau bụng - cần hỏi thêm / DS nếu dữ dội». Priority 300. Rủi ro: câu rationale nói cần hỏi thêm, trong khi khớp nhóm `DA_DAY` không đọc mức độ. Seed có cờ `severe_abdominal_pain` (`refer_medical`) nhưng **không** nối cờ đó với symptom `abdominal_pain`. Đề xuất: dược sĩ xem khoảng trống này. Chưa xuất bản.

**KR_ALLERGY.** Symptom `allergy` (Dị ứng), `SKI-001`. Keywords: `loratadine`, `cetirizine`, `clarityne`, `zyrtec`, `di ung`, `dị ứng`. Categories: `VITAMIN`. Rationale: «Dị ứng - kháng histamin OTC (hỏi tiền sử)». Priority 310. Rủi ro: từ khóa là kháng histamin, nhóm hàng lại là `VITAMIN`. Khớp OR nên một sản phẩm vitamin trong nhóm `VITAMIN` vẫn khớp dù tên không chứa từ khóa. Hardcoded `allergy_symptom` là `caution`, vẫn cho tìm sản phẩm. `anaphylaxis` / `stop_sale` chỉ nối với symptom `severe_allergy`, không nối với `allergy`. Đề xuất: dược sĩ xem lệch nhóm hàng. Chưa xuất bản.

**KR_ITCHY_SKIN.** Symptom `itchy_skin` (Ngứa da), `SKI-002`. Keywords: `loratadine`, `cetirizine`, `ngua`, `ngứa`. Categories: `VITAMIN`, `NGAO_DUOC`. Rationale: «Ngứa da - kháng histamin / bôi OTC». Priority 320. Rủi ro: cùng lệch `VITAMIN`, thêm cả nhóm `NGAO_DUOC`. Đề xuất: dược sĩ xem. Chưa xuất bản.

**KR_RASH.** Symptom `rash` (Phát ban), `SKI-003`. Keywords: `loratadine`, `cetirizine`, `phat ban`, `phát ban`. Categories: `VITAMIN`. Rationale: «Phát ban - kháng histamin OTC». Priority 330. Rủi ro: lệch nhóm `VITAMIN` so với từ khóa kháng histamin. Đề xuất: dược sĩ xem. Chưa xuất bản.

**KR_HIVES.** Symptom `hives` (Mề đay / mẩn đỏ), `SKI-004`. Keywords: `loratadine`, `cetirizine`, `me day`, `mề đay`. Categories: `VITAMIN`. Rationale: «Mề đay - kháng histamin OTC». Priority 340. Rủi ro: lệch nhóm `VITAMIN`. Seed không nối mề đay với `anaphylaxis`. Đề xuất: dược sĩ xem. Chưa xuất bản.

**KR_FATIGUE.** Symptom `fatigue` (Mệt mỏi), `NEU-004`. Keywords: `berocca`, `redoxon`, `vitamin`, `met moi`, `mệt mỏi`. Categories: `VITAMIN`. Rationale: «Mệt mỏi - vitamin / bổ sung OTC». Priority 380. Rủi ro: từ khóa `vitamin` rất rộng, cộng khớp cả nhóm `VITAMIN`. Seed không nêu nguyên nhân mệt mỏi nào được loại. Đề xuất: dược sĩ xem phạm vi. Chưa xuất bản.

**KR_DIZZINESS.** Symptom `dizziness` (Chóng mặt), `NEU-003`. Keywords: `berocca`, `vitamin`, `chong mat`, `chóng mặt`. Categories: `VITAMIN`. Rationale: «Chóng mặt - cần hỏi thêm / DS». Priority 390. Rủi ro: rationale nói cần hỏi thêm và cần dược sĩ, trong khi matching vẫn là vitamin hoặc cả nhóm `VITAMIN`. Seed không nối chóng mặt với `stroke_symptoms`. Đề xuất: không đưa vào hàng xuất bản. Chưa xuất bản.

**KR_INSOMNIA.** Symptom `insomnia` (Mất ngủ / khó ngủ), `NEU-001`. Keywords: `melatonin`, `mat ngu`, `mất ngủ`. Categories: `VITAMIN`. Rationale: «Mất ngủ - OTC an thần nhẹ (cần DS)». Priority 400. Rủi ro: rationale nói cần dược sĩ. Nhóm hàng là `VITAMIN` trong khi từ khóa có `melatonin`. Khớp cả nhóm `VITAMIN`. Đề xuất: dược sĩ xem riêng. Chưa xuất bản.

**KR_TOOTHACHE.** Symptom `toothache` (Đau răng), `FEV-007`. Keywords: `paracetamol`, `panadol`, `dau rang`, `đau răng`. Categories: `GIAM_DAU`. Rationale: «Đau răng - giảm đau OTC (nên khám nha)». Priority 370. Rủi ro: rationale nói nên khám nha, matching vẫn là cả nhóm `GIAM_DAU`. Đề xuất: dược sĩ xác nhận câu «nên khám nha» có chặn gợi ý hay chỉ là chữ trong rationale. Code hiện không chặn vì câu này. Chưa xuất bản.

**KR_DEHYDRATION.** Symptom `dehydration_signs` (Dấu hiệu mất nước), `DIG-018`. `297` đổi mode thành `otc_assist`. Keywords: `oresol`, `rehydration`, `ors`, `bù nước`, `dien giai`, `điện giải`, `mat nuoc`, `mất nước`, `oral rehydration`. Categories: `DA_DAY`, `VITAMIN`. Rationale: «Mất nước — bù nước / điện giải OTC». Priority 235. Safety riêng: seed nối symptom này với `severe_dehydration`, mức `refer_medical`. Nếu cờ đó có `publication_status = PUBLISHED`, cổng an toàn trả sản phẩm rỗng trước khi dùng rule. Rủi ro còn lại khi cờ không được đọc: khớp hai nhóm `DA_DAY` và `VITAMIN`. Đề xuất: dược sĩ xem quan hệ giữa rule gợi ý ORS và cờ mất nước nặng. Chưa xuất bản.

### Không đủ điều kiện xem xét xuất bản

**KR_OTHER.** Symptom `other` (Khác / chưa rõ), `OTH-001`, `otc_assist`. Keywords: mảng rỗng. Categories: `VITAMIN`, `GIAM_DAU`, `HO_HAP`. Rationale: «Triệu chứng khác - gợi ý OTC phổ biến». Priority 410. Vì từ khóa rỗng, nhánh từ khóa không khớp. Nhánh nhóm hàng vẫn khớp mọi OTC thuộc một trong ba nhóm. Không có exclude trên rule. Không có nguồn. Đề xuất: không đưa vào buổi xét xuất bản. Không xuất bản.

## 5. Phân tích các nhóm rủi ro

Chỉ mô tả điều seed và code đang làm. Không kết luận kê đơn hay điều trị. Nơi seed không nêu giới hạn, ghi **NOT VERIFIED**.

**Domperidone.** Có trong từ khóa `KR_NAUSEA`, `KR_VOMITING` (`domperidone`, `motilium`) và `KR_BLOATING` (`motilium`). `KR_NAUSEA` viết «cần DS nếu mang thai». `KR_VOMITING` và `KR_BLOATING` không viết điều kiện đó. Code chỉ thêm exclude `domperidone`/`motilium` khi `IsPregnant == true`. Không thấy exclude cho trẻ em, người cao tuổi, bệnh tim, hoặc thời gian dùng. Những giới hạn đó **NOT VERIFIED** trong source. Khớp nhóm `DA_DAY` có thể trả sản phẩm tiêu hóa khác, không chỉ domperidone.

**Omeprazole / PPI.** `KR_HEARTBURN` có từ khóa `omeprazole` nhưng rationale nói «kháng acid». `KR_REFLUX` rationale nói «PPI OTC» và từ khóa `omeprazole`. Seed không ghi thời gian dùng, tuổi, hay dấu hiệu cần khám. Những giới hạn **NOT VERIFIED**. Khớp nhóm `DA_DAY` rộng hơn PPI.

**Ibuprofen.** Có trong `KR_HEADACHE` và `KR_JOINT_PAIN`. Exclude `ibuprofen`/`brufen`/`aspirin`/`nsaid`/`codeine` chỉ khi trẻ dưới 2 tuổi, mang thai hoặc cho con bú. Seed không nêu giới hạn khác. **NOT VERIFIED**. Khớp nhóm `GIAM_DAU` còn trả sản phẩm giảm đau khác trong nhóm.

**Nhóm VITAMIN.** `KR_ALLERGY`, `KR_RASH`, `KR_HIVES` để kháng histamin trong từ khóa nhưng category là `VITAMIN`. `KR_ITCHY_SKIN` thêm `NGAO_DUOC`. `KR_FATIGUE` và `KR_DIZZINESS` dùng từ khóa `vitamin` và category `VITAMIN`. `KR_INSOMNIA` để `melatonin` trong từ khóa và category `VITAMIN`. `KR_OTHER` và `KR_DEHYDRATION` cũng có `VITAMIN`. Seed không định nghĩa category `VITAMIN` gồm những sản phẩm nào. Việc một vitamin trong nhóm đó xuất hiện khi khách chọn dị ứng hoặc chóng mặt là hệ quả của SQL `OR`, không phải một chỉ định được viết trong seed.

**Chóng mặt.** `KR_DIZZINESS` rationale: «cần hỏi thêm / DS». Matching vẫn là vitamin hoặc cả nhóm `VITAMIN`. Seed không nối symptom `dizziness` với `stroke_symptoms`. Khoảng trống đó có trong source. Ý nghĩa lâm sàng của khoảng trống: **NOT VERIFIED**.

**Buồn nôn và nôn.** Xem domperidone ở trên. `KR_VOMITING` còn từ khóa `non` và `smecta`. Seed không nối `vomiting` với `vomiting_blood`.

**Đau bụng.** `KR_ABDOMINAL` rationale nói cần hỏi thêm và cần dược sĩ nếu dữ dội. Cờ `severe_abdominal_pain` tồn tại trong seed nhưng không được nối với `abdominal_pain`. Matching vẫn là `smecta` hoặc cả nhóm `DA_DAY`.

**Mất ngủ.** `KR_INSOMNIA` rationale nói cần dược sĩ và «OTC an thần nhẹ». Từ khóa có `melatonin`. Category là `VITAMIN`. Seed không ghi tuổi, thời gian, hay thuốc không được gợi ý. **NOT VERIFIED**.

**KR_OTHER.** Từ khóa rỗng. Ba nhóm `VITAMIN`, `GIAM_DAU`, `HO_HAP` vẫn là điều kiện khớp. Đây là ánh xạ rộng nhất trong catalog.

## 6. Nguồn còn thiếu

Cả 35 mã thiếu:

- `reference_source` trên `pharmacy_knowledge_rule`.
- Tài liệu được trích trong `reason_vi`.
- Định nghĩa vận hành của các mã `HO_HAP`, `GIAM_DAU`, `DA_DAY`, `VITAMIN`, `NGAO_DUOC` so với `product_categories.category_code`.
- `exclude_keywords` cho các hoạt chất mà rationale đã gọi là cần dược sĩ.
- Khoảng hiệu lực lâm sàng. Ngày `effective_from` kỹ thuật không phải hạn dùng chuyên môn.

`source = novixa` không lấp các mục trên.

## 7. Câu hỏi cho chuyên gia

1. Mã nhóm hàng trong seed có phải mã category sản phẩm đang dùng tại nhà thuốc không? Nếu không, nhánh khớp theo nhóm đang khớp nhầm hoặc không khớp gì.
2. Với `KR_COUGH`, từ khóa `ho` và với `KR_VOMITING`, từ khóa `non`, chuỗi con này có được giữ không?
3. `KR_DIARRHEA` viết «men vi sinh» nhưng từ khóa không có cụm đó. Câu rationale nào là nội dung muốn giữ?
4. `KR_HEARTBURN` viết kháng acid nhưng từ khóa có `omeprazole`. Hai nội dung này có được ở cùng một rule không?
5. Domperidone trong `KR_NAUSEA`, `KR_VOMITING`, `KR_BLOATING` có được phép nằm trong từ khóa gợi ý không, và exclude hiện chỉ khi mang thai đã đủ theo ý seed chưa?
6. Ibuprofen trong `KR_HEADACHE` và `KR_JOINT_PAIN` có được phép nằm trong từ khóa không?
7. Vì sao dị ứng, ngứa, phát ban và mề đay dùng category `VITAMIN`?
8. `KR_DIZZINESS` nói cần dược sĩ. Rule có được phép tiếp tục khớp vitamin không?
9. `KR_INSOMNIA` nói cần dược sĩ và có từ khóa `melatonin` cùng category `VITAMIN`. Cách khớp nào được giữ?
10. `KR_ABDOMINAL` nói cần hỏi thêm nếu đau dữ dội, nhưng `severe_abdominal_pain` không nối với symptom này. Có cần nối không? Tài liệu này không tự nối.
11. `KR_DEHYDRATION` gợi ý ORS trong khi symptom nối với cờ `severe_dehydration` mức `refer_medical`. Khi cờ có hiệu lực, danh sách sản phẩm trống. Chuyên gia có muốn giữ cả hai không?
12. `KR_TOOTHACHE` nói «nên khám nha». Câu đó có chỉ là chữ rationale, đúng như code đang làm, hay phải chặn gợi ý?
13. `KR_OTHER` có bị loại khỏi mọi đợt xuất bản không?

Chưa có câu trả lời nào trong source.

## 8. Đề xuất catalog V1

Không xuất bản rule nào ở đợt này. Không có ứng viên xuất bản.

**Đưa cho chuyên gia đọc trước, sau khi đã gắn nguồn.** Mức chắc chắn của việc «đủ cấu trúc»: cao. Mức chắc chắn của việc «được xuất bản»: không có.

`KR_COUGH_DRY`, `KR_COUGH_PHLEGM`, `KR_RUNNY_NOSE`, `KR_NASAL_CONG`, `KR_SNEEZING`, `KR_COMMON_COLD`, `KR_FEVER`, `KR_BODY_ACHE`, `KR_CHILLS`, `KR_CONSTIPATION`, `KR_BACK_PAIN`, `KR_THROAT_CLEARING`.

Đọc cùng đợt nhưng xem từ khóa ngắn hoặc hai nhóm hàng trước: `KR_COUGH`, `KR_SORE_THROAT`, `KR_FLU`, `KR_DIARRHEA`, `KR_HOARSENESS`.

**Không đưa vào buổi xét xuất bản cho đến khi có biên bản dược sĩ.** Mức chắc chắn rằng seed đang chứa các điểm này: cao. Mức chắc chắn về quyết định lâm sàng: **NOT VERIFIED**.

`KR_HEADACHE`, `KR_JOINT_PAIN`, `KR_NAUSEA`, `KR_VOMITING`, `KR_BLOATING`, `KR_HEARTBURN`, `KR_REFLUX`, `KR_ABDOMINAL`, `KR_ALLERGY`, `KR_ITCHY_SKIN`, `KR_RASH`, `KR_HIVES`, `KR_FATIGUE`, `KR_DIZZINESS`, `KR_INSOMNIA`, `KR_TOOTHACHE`, `KR_DEHYDRATION`.

**Không nên xuất bản với nội dung seed hiện tại:** `KR_OTHER`.

**Thứ tự xử lý đề xuất**

1. Trả lời câu hỏi category code có khớp danh mục sản phẩm hay không. Nếu không khớp, buổi rà soát từ khóa vẫn có ích, buổi rà soát nhóm hàng thì chưa.
2. Tách `KR_OTHER` khỏi mọi danh sách xuất bản.
3. Rà riêng domperidone, omeprazole/PPI, ibuprofen, nhóm `VITAMIN`, chóng mặt, buồn nôn/nôn, đau bụng, mất ngủ.
4. Gắn `reference_source` cho rule nào còn được giữ. Thiếu nguồn thì không chuyển sang `PUBLISHED`.
5. Chỉ sau các bước trên mới chọn rule cấu trúc đủ để xét xuất bản. Việc chọn đó không nằm trong tài liệu này.

## 9. Giới hạn và NOT VERIFIED

- Không đọc database production. Số dòng và trạng thái đang chạy trên production **NOT VERIFIED**.
- Không đọc danh mục sản phẩm của một nhà thuốc. Việc mã `HO_HAP`, `GIAM_DAU`, `DA_DAY`, `VITAMIN`, `NGAO_DUOC` có tồn tại trên `product_categories` **NOT VERIFIED**.
- Không có hướng dẫn điều trị trong repo cho các hoạt chất được seed nêu tên. Giới hạn tuổi, thời gian dùng, chống chỉ định ngoài đoạn exclude đã trích **NOT VERIFIED**.
- Test W5/W6 chứng minh cổng an toàn, pin theo tenant, và việc không đọc bảng review làm nguồn gợi ý. Chúng không chứng minh nội dung lâm sàng của 35 rule.
- `effective_from` kỹ thuật phụ thuộc ngày INSERT. Không suy ra một niên hạn chuyên môn.
- Tài liệu này không xuất bản, không thu hồi, không tạo adoption, không cấp quyền, và không sửa seed.

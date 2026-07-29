# Famixa · Growth Balance™ — Giá trị quan tâm & tránh 3 lệch

**Mã:** KIT-PRD-FO-GB-01 · **Pack:** FamilyOS / Famixa  
**Ngày:** 2026-07-29 · **Trạng thái:** Constitution + capture lite shipped  
**Phụ thuộc:** [famixa-dna-blueprint-roadmap-v1.md](./famixa-dna-blueprint-roadmap-v1.md) · [famixa-self-calibration-playbook-v1.md](./famixa-self-calibration-playbook-v1.md) · `FamilyGrowthBalance`

> **Outcome sản phẩm:** Cá nhân hóa để **mọi nhà** (mọi tầng nguồn lực / nhận thức) **thấy giá trị của việc quan tâm đến con** — và giữ con khỏi lệch: **tự ti · thiếu phấn đấu · dễ hư**.

---

## 1. Câu gốc (bổ sung DNA)

> Quan tâm không phải nuông chiều.  
> Quan tâm là **hiện diện + tiêu chuẩn vừa sức + bằng chứng nhỏ** — đúng nhà bạn.

Famixa không bán “quản lý việc nhà”.  
Famixa giúp phụ huynh **quan tâm có phương pháp**: ấm nhưng có khung, cao nhưng không đè, dễ nhưng không buông.

---

## 2. Ba lệch cần tránh (tam giác rủi ro)

```text
              Thiếu phấn đấu
             (ảo giác / lười chuẩn)
                    /\
                   /  \
                  /    \
           Tự ti /------\ Dễ hư
     (sợ thử / so sánh)  (nuông / không khung)
```

| Lệch | Dấu hiệu nhà | Sai hướng phụ huynh | Hướng Famixa |
|------|--------------|---------------------|--------------|
| **Tự ti** | Sợ thử, rút lui sau cú sốc, “con kém” | So sánh, mắng năng lực, nhồi đề | An toàn cảm xúc → chuỗi thắng nhỏ có bằng chứng |
| **Thiếu phấn đấu** | Tưởng mình khá, lướt bài, né khó | Khen sáo / chỉ điểm số / buông chuẩn | Retrieval, tiêu chuẩn vừa, khen nỗ lực cụ thể |
| **Dễ hư** | Không khung, thương = chiều, hậu quả mờ | Chỉ chiều cảm xúc, tránh khó chịu | Thương + thỏa thuận rõ; Inbox 👍/👎; Mode có giới hạn |

**Cân bằng (target):** tự tin dựa bằng chứng · chịu khó vừa sức · biết giới hạn nhà.

---

## 3. “Thấy giá trị quan tâm” — cá nhân hóa theo nhà

Không một slogan cho mọi tầng lớp. **Cùng outcome, khác lời & bước:**

| Nguồn lực nhà (`resourceBand`) | Giá trị quan tâm (care value) họ cần nghe | Bước điển hình |
|-------------------------------|-------------------------------------------|----------------|
| `tight` — ít thời gian / eo hẹp | “1 phút hiện diện tốt hơn 1 giờ mắng.” | 1 việc/ngày · 👍 · không checklist dài |
| `moderate` — trung bình | “Quan tâm = khung nhẹ + khen đúng việc.” | DNA Next + Inbox · không so sánh |
| `abundant` — dư thời gian/điều kiện | “Nhiều điều kiện ≠ nuông; tiêu chuẩn rõ vẫn cần.” | Wallet/thỏa thuận · tránh mua chuộc thay hiện diện |

| Nhận thức phụ huynh (suy ra từ wording, không quiz IQ) | Cách nói |
|--------------------------------------------------------|----------|
| Thấp jargon / mới tiếp cận | Câu ngắn, ví dụ đời thường, không “Growth Zone / Twin” |
| Đã quen Coach | Có thể dùng phase / tip dài hơn (Pro) |

**Free phải đủ** để nhà `tight` thấy giá trị — không khóa “quan tâm có phương pháp” sau paywall.

---

## 4. Dữ liệu lưu (soát / nâng cấp)

Trong `family_blueprint.layers_json`:

```json
{
  "resources": {
    "band": "tight|moderate|abundant|unknown",
    "capturedAt": "…"
  },
  "growthBalance": {
    "primaryWorry": "tu_ti|thieu_phan_dau|de_hu|balance_ok|unknown",
    "phase": "…",
    "updatedAt": "…",
    "history": [ { "at", "worry", "band", "phase" } ]
  }
}
```

DNA derived:

- `careValueVi` — vì sao quan tâm quan trọng **với nhà này**  
- `growthBalanceLabelVi` — lệch đang ưu tiên / đang cân bằng  
- `coachTipVi` / `nextStepVi` — ưu tiên Growth Balance khi đã capture worry; còn lại Self-Calibration

Code SoT: `FamilyGrowthBalance`.

---

## 5. Ưu tiên triển khai

| Ưu tiên | Việc | Trạng thái |
|---------|------|------------|
| P0 | SoT + rule tip theo worry × resourceBand | **Shipped** |
| P0 | Capture thêm: “Lo lớn nhất” + “Nhà mình thời gian/điều kiện” | **Shipped** |
| P0 | DNA hiện care value + tip cân bằng | **Shipped** |
| P1 | Coach Pro FAQ theo 3 lệch | Planned |
| P1 | L7 Resources giàu hơn (time / học thêm / ông bà) | Sóng B |
| P2 | Growth Zone 3 trục narrative (không %) | Sóng B–C |
| P2 | Website: kể “quan tâm có phương pháp” — không “app quản con” | Memo gói |

---

## 6. Nguyên tắc bất biến

1. Quan tâm ≠ nuông; quan tâm ≠ đè.  
2. Không xếp hạng nhà / trường / thu nhập.  
3. Nhà `tight` không bị pitch feature Pro như điều kiện để yêu con đúng.  
4. Sau cú sốc (tự ti): cảm xúc trước, thành tích sau.  
5. Khi thiếu phấn đấu: chuẩn vừa sức + bằng chứng — không sỉ nhục.  
6. Khi dễ hư: khung + thương — không chỉ siết hay chỉ chiều.  
7. Mọi đổi Routine / Wallet vẫn qua Inbox.

---

## 7. Checklist nâng cấp (khi soát lại)

- [ ] Copy Free Home: 1 dòng care value theo `resourceBand`  
- [ ] Coach Pro: 3 FAQ cố định (tu_ti / thieu_phan_dau / de_hu)  
- [ ] Letter tháng: 1 đoạn “nhà đang cân bằng lệch nào”  
- [ ] Infer worry từ calibration phase (peer_shock → tu_ti, bubble_risk → thieu_phan_dau) khi chưa capture  
- [ ] famixa.vn: hero “quan tâm có phương pháp” align memo gói  
- [ ] Không dùng ngôn ngữ kỳ thị tầng lớp (“nhà nghèo/giàu”)

---

## 8. Chốt một dòng

**Cá nhân hóa = giúp mọi nhà thấy: quan tâm con có giá trị — và giữ cân bằng giữa tự tin, phấn đấu, và không hư.**

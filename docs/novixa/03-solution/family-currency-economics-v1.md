# Family Currency Economics v1 — SAO như tiền tệ gia đình

**Mã:** KIT-PRD-FO-CUR-01 · **Pack:** FamilyOS · **Version:** 1.0  
**Ngày:** 2026-07-27 · **Owner:** Product + Behavior OS  
**Phụ thuộc:** [behavior-os-north-star-v1.md](./behavior-os-north-star-v1.md) · [family-os-pack-brief-v1.md](./family-os-pack-brief-v1.md)  
**Preset máy đọc:** [presets/family-currency-settings-sample-v1.json](./presets/family-currency-settings-sample-v1.json)  
**Trạng thái:** IMPLEMENTING local (C0–C5 code) · mig `245_pack_family_currency_economics.sql` · Family OS park deploy vẫn áp dụng

---

## 1. Vấn đề kinh tế hành vi

Hệ thống hiện tại (`star_reward` cố định trên template + late-tier) tạo **giá trị phẳng**:

| Pattern title (heuristic) | Sao mặc định |
|---------------------------|--------------|
| bài / học / toán | 20 |
| ngủ / đánh răng | **15** |
| còn lại | 10 |

Não trẻ học: *mọi tick ≈ cùng một lớp thưởng*. Thậm chí vệ sinh cá nhân còn được **nhiều hơn** việc nhà — ngược với mục tiêu “phản ánh giá trị giáo dục”.

**Mục tiêu Famixa:** SAO = *Family Currency* (khan hiếm, phản ánh giá trị), không phải điểm farm.

---

## 2. Năm nguyên tắc (SoT)

1. **Ngân sách sao tối đa / ngày** — khan hiếm có chủ đích.  
2. **AI (rule-based) phân bổ** theo nhóm đóng góp phát triển — phụ huynh cấu hình ngân sách & trọng số, không phải từng việc.  
3. **Thưởng tiến bộ & chủ động** — vượt kế hoạch, tự bắt đầu, học thêm ngoài plan.  
4. **Giảm dần khi thành thói quen** — chuyển sang huy hiệu / mốc trưởng thành (nối `habit_stage`).  
5. **Giải trí rất ít sao** — phần lớn từ Growth / Responsibility / Kindness.

---

## 3. Gap vs code hiện tại

| Năng lực đề xuất | Hiện trạng Famixa | Ghi chú |
|------------------|-------------------|---------|
| Daily star budget | Không có | Có precedent: `FamilyScreenWallet` (phút/tuần) |
| Phân bổ theo category % | Không | `priority` chỉ UI, không vào calculator |
| 3 loại sao (G/R/K) | 1 ledger phẳng | Cần `star_kind` trên ledger + balance 3 cột |
| Zero-star / trách nhiệm | Không | Heuristic còn cho đánh răng **15** |
| Decay theo habit stage | Reminder suppress only (Wave 1) | Cần `star_multiplier_by_stage` |
| Stretch / vượt chuẩn | Không | Cần `plan_target` + `actual` trên commitment |
| Huy hiệu | Không | Có `behavior_event` để trigger |
| Late-tier | Có (`family_star_settings`) | **Giữ** — áp sau khi đã có base từ budget allocator |

---

## 4. Mô hình đề xuất (tóm tắt)

```
daily_budget(age_band | parent_override)
    → allocate by category_weights
        → per-task base (within pool, AI/rules)
            → × habit_stage_multiplier
            → + stretch_bonus | + initiative_bonus
            → × late_tier (existing)
            → post to star_ledger with star_kind ∈ {growth, responsibility, kindness}
```

Việc `eligible_for_stars = false` (trách nhiệm thuần) → **0 sao**, chỉ streak + huy hiệu.

Khi `habit_stage ∈ {autonomous, maintained}` và policy `graduate_stars = true` → base = 0; message: *“Hành vi này đã tốt nghiệp.”* + ghi nhận streak.

---

## 5. Bộ cài đặt mẫu (preset `balanced_v1`)

Chi tiết máy đọc nằm trong JSON. Tóm tắt vận hành:

### 5.1 Ngân sách ngày theo tuổi

| Age band | Ngân sách ⭐/ngày | Ghi chú |
|----------|------------------|---------|
| `6_10` | 20 | Tiểu học |
| `11_15` | 30 | THCS (mẫu chính) |
| `16_18` | 40 | THPT |
| `custom` | parent 10–80 | Tự cấu hình |

Hard cap: không post vượt `daily_budget` (trừ `stretch_overflow_policy = soft_cap` cho bonus vượt chuẩn tối đa +20% budget).

### 5.2 Trọng số nhóm (tỷ lệ ngân sách)

| Nhóm | Code | % | Star kind mặc định |
|------|------|---|-------------------|
| Phát triển bản thân | `growth` | 48% | Growth |
| Trách nhiệm | `responsibility` | 28% | Responsibility |
| Hành vi tích cực | `kindness` | 16% | Kindness |
| Động viên nhỏ | `cue` | 6% | Responsibility |
| Giải trí | `play` | 2% | — (gần như 0) |

### 5.3 Việc không có sao (trách nhiệm thuần)

Sau giai đoạn hình thành (`habit_forming` trở đi mặc định):

- Đánh răng, chào hỏi, đi học, đi ngủ đúng giờ (baseline)  
- Có thể bật `formation_stars: true` chỉ khi stage ∈ {`new`,`guided`} với trần rất thấp (1–2⭐) rồi tốt nghiệp.

### 5.4 Nhân theo habit stage (thưởng tiến bộ, không farm lặp)

| Stage | Multiplier base | Ý |
|-------|-----------------|---|
| `new` | 1.00 | Hình thành |
| `guided` | 0.85 | |
| `assisted` | 0.70 | |
| `habit_forming` | 0.50 | |
| `autonomous` | 0.00 | Tốt nghiệp sao (nhắc đã suppress) |
| `maintained` | 0.00 | Chỉ huy hiệu / streak |

**Initiative** (self-start hoặc vượt plan): vẫn được `initiative_bonus` / `stretch_bonus` dù base đã decay — *thưởng tiến bộ, không thưởng lặp lại*.

### 5.5 Ví dụ ngày 30⭐ (age `11_15`)

| Việc | Nhóm | Base | Kind |
|------|------|------|------|
| Học 90 phút | growth | 8 | G |
| Làm bài tập | growth | 6 | G |
| Rửa bát | responsibility | 4 | R |
| Đọc sách | growth | 4 | G |
| Giúp em học | kindness | 3 | K |
| Dậy đúng giờ | cue | 2 | R |
| Mini game | play | 1 | — |
| Check-in | play | 1 | — |
| **Tổng** | | **29** | |
| Đánh răng / chào hỏi | duty | **0** | huy hiệu streak |

Stretch: kế hoạch 10 câu toán → làm 15 → +2⭐ Growth (trong soft cap).

### 5.6 Ba loại sao (balance riêng)

| Kind | Label VI | Nguồn chính |
|------|----------|-------------|
| `growth` | Sao Phát triển | Học, đọc, kỹ năng |
| `responsibility` | Sao Trách nhiệm | Việc nhà, đúng giờ, giữ lời |
| `kindness` | Sao Tử tế | Giúp đỡ, trung thực, xin lỗi/cảm ơn |

Redeem: catalog có thể yêu cầu **tổ hợp** (vd. Movie Night = 15G + 8R + 5K) để chống farm một chiều.

### 5.7 Huy hiệu (không trả sao)

| Badge code | Điều kiện | Unlock |
|------------|-----------|--------|
| `read_30d` | Đọc sách 30 ngày liên tiếp | avatar frame |
| `room_tidy_14d` | Phòng gọn 14 ngày | story chapter |
| `peace_7d` | Không cãi bố mẹ 7 ngày | title |
| `help_10x` | Giúp người khác 10 lần | badge wall |

Trigger từ `behavior_event` + streak — không qua `star_ledger`.

---

## 6. Khuyến nghị triển khai (wave gợi ý)

| Wave | Scope | Phụ thuộc |
|------|-------|-----------|
| **C0** | Schema settings + preset JSON load (admin) — chưa đổi calculator | Park local |
| **C1** | Daily budget + category allocator → ghi `star_reward` động mỗi ngày | C0 |
| **C2** | `eligible_for_stars` + multiplier theo `habit_stage` | Behavior Wave 1 |
| **C3** | `star_kind` 3 cột balance + redeem mix | C1 |
| **C4** | Stretch / initiative bonus | C1 + self-start |
| **C5** | Badge catalog | `behavior_event` |

Giữ nguyên late-tier UI hiện có; gắn *sau* allocator.

---

## 7. Non-goals (v1 design)

- LLM tự “định giá” từng việc mỗi ngày (dùng rule + parent override)  
- Sao âm / debt giữa các kind  
- Deploy pilot hàng loạt khi Family OS vẫn park  
- Đụng Pharmacy / DEMO freeze

---

## 8. Acceptance (khi implement)

- [x] C0 Schema + API currency-settings + preset `balanced_v1` (admin Cài đặt sao)
- [x] C1 Daily budget allocator gắn `FamilyStarService.ComputeAwardAsync`
- [x] C2 Duty 0 sao + multiplier `habit_stage` (tốt nghiệp autonomous/maintained)
- [x] C3 `star_kind` trên ledger + balance G/R/K (redeem mix cột catalog sẵn; enforce mix = follow-up)
- [x] C4 Stretch (`actual > plan`) + initiative (`self_start`)
- [x] C5 Badge seed + award sau post sao (đọc 30d / phòng 14d / giúp 10x)
- [ ] UI family-app hiển thị 3 balance + copy tốt nghiệp (polish)
- [ ] Redeem enforce tổ hợp G+R+K
- [ ] Hai task cùng heuristic cũ không còn cùng payout nếu category khác (verify E2E)

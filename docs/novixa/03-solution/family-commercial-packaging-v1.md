# Famixa Commercial Packaging v1

**Mã:** KIT-PRD-FO-PKG-01 · **Ngày:** 2026-07-27  
**Trạng thái:** LOCAL · Family OS park deploy vẫn áp dụng

## Thesis

Định giá theo **giá trị AI / kết quả phụ huynh**, không theo số task.  
Gói chủ lực: **Family Peace Plan (Pro) 199.000đ/tháng**.

## Tầng

| Tier | Plan codes | Giá | Max trẻ | Outcome name |
|------|------------|-----|---------|--------------|
| Free | *(hết trial / expired)* | 0đ | 1 | Trải nghiệm Famixa |
| Plus | `plus_month`, `starter_month` (legacy), `plus_year` | 99k / 990k năm | 2 | Family Growth Plan |
| Pro | `family_pro_month`, `family_pro_year` | 199k / 1.99tr | ∞ | Family Peace Plan |
| AI+ | `family_ai_plus_month`, `family_ai_plus_year` | 399k / 3.99tr | ∞ | Đồng hành AI chuyên sâu |

**Trial 30 ngày** = quyền năng **Pro** (để thấy kết quả trước khi trả tiền).

## Capability matrix (SoT code)

`FamilyPlanCapabilityMatrix` trong `FamilyCapabilityContracts.cs`:

| Capability | Free | Plus | Pro | AI+ |
|------------|------|------|-----|-----|
| core_routine | ✓ | ✓ | ✓ | ✓ |
| weekly_insight | ✓ | ✓ | ✓ | ✓ |
| timeline | | ✓ | ✓ | ✓ |
| behavior_twin | | ✓ | ✓ | ✓ |
| ai_suggest | | ✓ | ✓ | ✓ |
| behavior_coach | | | ✓ | ✓ |
| parenting_coach | | | ✓ | ✓ |
| growth_report | | | ✓ | ✓ |
| screen_negotiate | | | ✓ | ✓ |
| monthly_letter | | | ✓ | ✓ |
| family_replay | | | ✓ | ✓ |
| parent_success_checkin | | | ✓ | ✓ |
| ai_plus_deep | | | | ✓ (stub) |

## API

- `GET …/subscription` — kèm `tierCode`, `capabilities`, `recommendedUpgradePlanCode`
- `GET …/capabilities` — pack tường minh
- Checkout: Plus / Pro / AI+ (+ năm); default Pro; `free` không checkout

## Mig

`248_pack_family_commercial_packaging_v1.sql` — seed catalog + cho phép `amount_vnd >= 0`.

## Non-goals v1

- Marketplace hoa hồng  
- AI+ gọi điện / study plan đầy đủ  
- Guarantee copy 60 ngày (messaging later)  
- Deploy pilot

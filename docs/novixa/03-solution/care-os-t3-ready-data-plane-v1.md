# Novixa Care OS — T3-ready data plane (định hình giải pháp)

**Mã:** NVX-SOL-09 · **Tier:** T1/T2 · **Trạng thái:** Draft · **Version:** 1.0 · **Ngày:** 2026-07-22

**Owner:** Product / Engineering · **Liên quan:** [operational-positioning-v1.md](../01-company/operational-positioning-v1.md) (NVX-CMP-05)

---

## 1. Mục tiêu giai đoạn này

Triển khai **lõi kỹ thuật T3-ready** để **định hình giải pháp** Community Health:

- Schema + event types + cohort catalog + KPI catalog  
- API đọc/ghi tối thiểu (manual / shaping)  
- Readiness map: cái gì chạy được ngay, cái gì cần dữ liệu, cái gì cố ý chưa làm  

**Không** đưa vào thực tiễn vận hành Community Health (không UI outcome công khai, không claim KPI live).

---

## 2. Đã triển khai (lõi)

| Thành phần | Vị trí | Ghi chú |
|------------|--------|---------|
| Migration `pack_care` | `migrations/195_pack_care_t3_ready.sql` | Schema + seed cohort/KPI |
| Module `care_os` | `platform_module_registry` + package `care_os` | **Opt-in** — không bật sẵn mọi tenant |
| Pack Application/Infrastructure | `src/Packs/Care/` | Contracts + Dapper repos |
| API | `GET/POST /api/care-os/*` | Gate: `[RequirePlatformModule(care_os)]` |
| Overview readiness | `GET /api/care-os/overview` | Non-goals + runnable_when |

### 2.1 Bảng

| Bảng | Vai trò |
|------|---------|
| `pack_care.care_event` | Nhật ký care facts (append) |
| `pack_care.care_cohort_definition` | Catalog cohort (seed HTA, ĐTĐ, …) |
| `pack_care.care_cohort_membership` | Gán khách → cohort (manual) |
| `pack_care.care_kpi_definition` | 7 KPI từ NVX-CMP-05 (status=`design`) |
| `pack_care.care_metric_snapshot` | **Reserve** cho aggregate sau này |

### 2.2 API (sau khi enable module `care_os`)

| Method | Path | Chạy thực? |
|--------|------|------------|
| GET | `/api/care-os/overview` | **Ngay** (metadata) |
| GET | `/api/care-os/kpis` | **Ngay** (catalog; chưa có số) |
| GET | `/api/care-os/cohorts` | **Ngay** (catalog) |
| GET/POST | `/api/care-os/events` | **Ngay** (manual shaping) |
| GET/POST | `/api/care-os/cohort-memberships` | **Ngay** nếu có `customer_id` hợp lệ |

Enable module (ví dụ local): thêm `care_os` vào `settings.platform.enabled_modules` của tenant (cùng cơ chế FamilyOS/Connect).

---

## 3. Cần dữ liệu mới chạy thực — chú thích rõ

| Hạng mục | Cần dữ liệu gì | Khi nào chạy thực được |
|----------|----------------|------------------------|
| KPI `hta_followup_coverage` | Membership cohort `chronic_hypertension` + event `follow_up_*` | Sau rule/manual gán HTA **và** follow-up events trong ≥30 ngày; **cần** compute worker (chưa có) |
| KPI `repurchase_reminder_conversion` | Event `repurchase_suggested` + `repurchase_converted` từ bán thật | Sau dual-write từ `repurchase_suggestions` + sales complete |
| KPI `follow_up_overdue_count` | Dual-write từ `care_reminders` | Sau adapter CustomerApp → `care_event` |
| KPI `referral_completion_rate` | Dual-write Connect referral | Sau hook `pack_connect.referrals` |
| KPI `care_effectiveness_index` | Các KPI con status=`live` | **T3 later** — ≥1 quý KPI con ổn định |
| KPI `shift_quality_exceptions` | Exception từ Success checklist | Sau dual-write Success |
| KPI `academy_counseling_pass_rate` | Learning enroll/pass | Sau dual-write `pack_learning` |
| Auto cohort HTA/ĐTĐ | Rule + tín hiệu Rx/tag/adherence | Sau rule engine (chưa có) + đủ tín hiệu |
| `care_metric_snapshot` rows | Job aggregate theo `compute_hints` | Khi viết worker; trước đó bảng luôn trống |
| Dashboard Community Health UI | Snapshot + narrative GTM | Cố ý sau khi 1–2 KPI có số thật |

`care_kpi_definition.runnable_when` và `GET /overview` → `readiness[]` lặp lại các điều kiện trên trong runtime.

---

## 4. Cố ý chưa triển khai (và lý do)

| Hạng mục | Lý do |
|----------|--------|
| Dual-write CustomerApp (`medication_reminders`, `care_reminders`, adherence, repurchase) | Channel tables đã tồn tại; wire từng nguồn khi có traffic + AC — tránh big-bang |
| Dual-write Connect referral/booking | Connect độc lập; hook khi ưu tiên bài toán #4 |
| Dual-write Success / Learning | Tương tự — không chặn lõi schema |
| Rule engine gán cohort tự động | Cần product rules + dữ liệu Rx/counseling; premature optimization |
| Worker tính `care_metric_snapshot` | Không có event volume → snapshot vô nghĩa |
| Admin UI “Community Health” | Định hình lõi trước; UI khi có số liệu thật |
| Public claim / báo cáo ra ngoài | Trái mục tiêu “chưa đưa vào thực tiễn” |
| Thay thế / migrate bảng `public.*` care | NSF-CARE: CustomerApp = adapter; Care OS = plane mới |
| AI chẩn đoán / kê đơn | Anti-claim pháp lý |

---

## 5. Hướng mở rộng (giữ lõi ổn định)

1. Adapter A: CustomerApp adherence/reminder → `care_event`  
2. Adapter B: Connect referral → `care_event`  
3. Rule v1: gán `chronic_hypertension` từ tag/Rx class (khi có)  
4. Worker: nightly snapshot cho 1–2 KPI  
5. Admin Care Queue (T2 product) đọc membership + open events  
6. Chỉ khi đó mới kể T3 outcome trên deck có số  

---

## 6. Tham chiếu code

- Migration: `migrations/195_pack_care_t3_ready.sql`  
- Pack: `src/Packs/Care/`  
- Controller: `src/KitPlatform.Api/Controllers/Care/CareOsController.cs`  
- Module code: `PlatformModuleCodes.CareOs`  
- Positioning: NVX-CMP-05  

---

## Changelog

| Version | Ngày | Thay đổi |
|---------|------|----------|
| 1.0 | 2026-07-22 | Lõi T3-ready + readiness annotations |

---

*Owner: Product / Engineering · Review: khi bật dual-write đầu tiên*

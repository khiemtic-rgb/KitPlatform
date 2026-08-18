# KIT Marketing Park V1 — Quản trị nội dung đa brand / đa kênh

**Mã:** `KIT-CNT-01` · **Version:** 1.1 · **Ngày:** 2026-08-11  
**Status:** WAVE 1 + product isolation (org `KIT_MKT`, package `marketing_park`)  
**Owner:** Platform / GTM ops  

**Liên quan:**
- Ngân sách tham chiếu: canvas `content-hub-budget` (30/60/120 bài·tháng)
- Cô lập park: cùng kỷ luật Family OS — **không** gộp `migration-files.prod.txt` Pharmacy
- **Mã tổ chức:** `KIT_MKT` · **Package:** `marketing_park` · **Module:** `kit_content`
- Thương hiệu nội dung (KIT / Novixa / Famixa / Vân Đỉnh Trà) là **brand row trong park**, không phải tenant ERP
- PHC / Assessment: **một connector CTA**, không phải trục toàn park
- Marketing sites hiện có: Astro+Sveltia (`novixa-site`, …), WordPress (Vân Đỉnh Trà), Facebook Pages

---

## 1. Mục tiêu sản phẩm

Xây **park marketing độc lập** trong KitPlatform để:

1. Quản lý **nhiều Brand / Website / Social Page** mà không phải sửa schema mỗi lần thêm site.
2. Workflow thống nhất: **Topic → AI variants → duyệt → lịch → phân phối → đo**.
3. Có **cài đặt trần ngân sách** (global + theo brand) để kiểm soát chi phí AI/ảnh khi scale.
4. **Không đụng** lõi Pharmacy ERP / Family OS / Care khi vận hành content — đăng nhập `KIT_MKT`, không hiện trên tenant Novixa/Famixa.

**Không phải mục tiêu V1:** spam Group / FB cá nhân; Content Intelligence tự học 100%; thay CMS biên tập sâu của từng site.

---

## 2. Biên giới park (isolation)

| Mục | Quy tắc |
|-----|---------|
| Pack / module code | `kit_content` |
| Tenant package | `marketing_park` |
| Org / tenant_code | `KIT_MKT` |
| Schema DB | `pack_content` |
| Migration manifest | `deploy/ubuntu/migration-files.content.txt` — **riêng**, giống `migration-files.family-os.txt` |
| Deploy VPS Content | Script/apply riêng; **chỉ khi user yêu cầu** |
| Pharmacy `migration-files.prod.txt` | **Cấm** merge Content mig cho đến khi GA + phê duyệt off-hours |
| Dependency Pharmacy | Không đọc/ghi bảng bán hàng, tồn, công nợ |
| Dependency Survey/PHC | Chỉ lưu `cta_url` / UTM / optional `assessment_category` — không đổi scoring |
| Multi-tenant ERP | Không gắn 1 `tenant_id` pharmacy; vận hành trên org Marketing riêng |

Cursor rule (khi implement): `.cursor/rules/content-park.mdc` — mirror tinh thần family-os park (không gộp prod pharmacy, không force deploy).

---

## 3. Mô hình mở rộng (không phải nâng cấp phá vỡ)

Thêm web/page sau này = thêm **row cấu hình**, không migration mới.

```
Org (KIT)
 └── BrandProfile          ← Novixa | Vân Đỉnh Trà | …
      ├── SiteTarget       ← Astro | WordPress | …  (connector type + credentials ref)
      ├── ChannelTarget    ← Facebook Page | IG | LinkedIn | …
      ├── BudgetPolicy     ← trần USD / tháng (override)
      ├── ToneKit          ← voice, cấm dùng, visual style
      └── ContentTopic[]
           └── ContentVariant[]   ← web | fb_long | fb_short | li | ig | group_suggested
                └── ContentAsset[]  ← image candidates
                     └── PublishJob[] → PublishAttempt / metrics
```

**Nguyên tắc extension:**
- `connector_type` enum mở rộng được (`astro_git`, `wordpress_rest`, `facebook_page`, `buffer`, …).
- Credential chỉ lưu **secret ref** (env / vault), không hardcode token trong DB row content.
- CTA là field trên Topic/Campaign, không hardwire PHC.

---

## 4. Cài đặt trần ngân sách (bắt buộc V1)

### 4.1 Phạm vi

| Cấp | Field | Mặc định đề xuất |
|-----|--------|------------------|
| **Global (Org)** | `monthly_ceiling_usd` | **120** |
| Global | `max_image_candidates_per_item` | 3 |
| Global | `regen_multiplier_budget` | 1.2 (dự phòng 20%) |
| Global | `default_image_tier` | `balanced` (`lean` \| `balanced` \| `premium`) |
| **Brand override** | `monthly_ceiling_usd` nullable | null = dùng global |
| Brand | `image_tier` nullable | null = dùng global |
| Brand | `pause_when_exceeded` | true |

### 4.2 Hành vi khi chạm trần

1. Hệ thống ước tính cost trước khi enqueue gen ảnh (theo bảng RATE nội bộ cấu hình được).
2. Nếu `projected_month_spend + estimate > ceiling` → **chặn auto-gen**, status `BudgetBlocked`, notify Admin.
3. User vẫn có thể: hạ tier · giảm candidate · tăng trần (role quyền) · gen thủ công 1 ảnh (ghi audit).
4. Dashboard tháng: spend ước tính vs trần (global + từng brand).

### 4.3 Bảng RATE (cấu hình, không hardcode code)

Seed theo canvas ngân sách (có thể chỉnh trong Admin không cần release):

| Tier | USD / ảnh gen (seed) | Ghi chú |
|------|----------------------|---------|
| lean | 0.02 | Imagen mid / mini-class |
| balanced | 0.05 | Default |
| premium | 0.14 | Gần ChatGPT Free visual |

Text pack estimate seed: `~0.08 USD / topic` (multi-variant).

---

## 5. Thực thể V1 (schema tối thiểu)

### 5.1 Bảng cốt lõi

| Table | Vai trò |
|-------|---------|
| `content_org_settings` | Trần global, RATE JSON, default tier |
| `content_brand` | Brand profile, tone, pause flags |
| `content_brand_budget` | Override trần / tier / spend tháng (cache) |
| `content_site_target` | Web đích: type, base_url, connector config |
| `content_channel_target` | Page/IG/LI: type, external_id, connector |
| `content_campaign` | Chiến dịch (optional group topics) |
| `content_topic` | 1 chủ đề / ý tưởng |
| `content_variant` | Bản theo kênh (web, fb, …) |
| `content_asset` | Ảnh/file; `is_selected` |
| `content_publish_job` | Hàng đợi publish |
| `content_publish_log` | Attempt + error |
| `content_usage_ledger` | Mỗi lần gọi AI/ảnh: brand, kind, estimate_usd, at |
| `content_performance` | Metrics thô (views/clicks/UTM) — **nhập tay** (mig 311). Không Pixel / API MXH ở wave này. |

### 5.2 Topic fields quan trọng

- `brand_id`, `title`, `pillar` (free text hoặc enum brand-specific)
- `goal` (traffic | seo | lead | phc | other)
- `cta_url`, `utm_campaign`
- `priority` (P0/P1/P2)
- `status`: `Draft | Generating | Review | Approved | Scheduled | Published | BudgetBlocked | Rejected`

### 5.3 Variant kinds (seed)

`web_long`, `fb_page`, `fb_short`, `linkedin`, `instagram`, `group_suggested`, `seo_meta`

Thêm kind mới = seed/config, không alter phá vỡ.

---

## 6. Connector V1 vs sau

| Connector | V1 | Ghi chú |
|-----------|----|---------|
| Manual export / copy | ✅ | Luôn có |
| Astro Git (commit MD + image) | ✅ P0 Novixa-class sites | Dựa trên Sveltia/Git đã có |
| WordPress REST | ✅ P0 Vân Đỉnh Trà | Create post + media |
| Facebook Page Graph | ✅ P0 | Mở rộng script fanpage hiện có |
| Buffer | ◐ P1 | Optional scheduler đa kênh |
| Instagram / LinkedIn native | ◐ P1–P2 | Theo quyền API |
| Group / FB personal | ❌ auto | Chỉ `group_suggested` + người đăng |

---

## 7. Admin UX (shell V1)

Module Admin **Content** (menu platform, không nằm trong tenant Pharmacy):

1. **Brands** — CRUD + ToneKit + Sites/Channels  
2. **Budget** — trần global, override brand, ledger tháng, cảnh báo %  
3. **Topics** — list / filter brand / status / priority  
4. **Topic detail** — variants, assets (chọn 1/3 ảnh), Approve  
5. **Queue** — publish jobs, retry  
6. **Settings** — RATE, max candidates, connectors health  

Quyền: `content.read`, `content.write`, `content.publish`, `content.budget.manage` (platform roles).

---

## 8. AI pipeline (trong park)

```
ApproveGenerate(topic)
  → check budget
  → generate variants (text)
  → generate N image candidates
  → write usage_ledger
  → status = Review
Human picks image + edits copy
  → ApprovePublish / Schedule
Distributor worker
  → per site/channel connector
  → logs
```

Worker: BackgroundService hoặc outbox pattern **trong pack Content** — không dùng Hangfire Pharmacy. Không chặn request API bán hàng.

---

## 9. Lộ trình triển khai

### Wave 0 — SoT + park skeleton (tuần này)
- Doc này + `migration-files.content.txt` trống/placeholder  
- Pack definition + DI stub  
- Admin route shell “Content”  
- **Org settings + Budget UI** (trần $120 seed)

### Wave 1 — Factory + duyệt
- Brand / Site / Channel CRUD  
- Topic + Generate variants + 3 ảnh + chọn  
- Ledger + chặn trần  
- Export thủ công vẫn dùng được

### Wave 2 — Distribute P0
- Connector Astro Git + WordPress + Facebook Page  
- Schedule đơn giản (publish_at)

### Wave 3 — Scale kênh
- Buffer / IG / LI  
- Performance: nhập tay trên góc brand (`content_performance`). Auto-pull MXH = sau.

### Wave 2.1 — Governance (đang chạy)
- Creative Brief mỏng trên `content_package.extra_json` (objective, emotion, format, visual, duration) — không bảng ContentIdea
- Quality gate **chặn** duyệt nếu thiếu Brief; **chặn đăng** nếu web mỏng / thiếu angle / claim cấm
- Group / caption dài = gợi ý, không chặn auto web/Fanpage

### Wave 4 — Intelligence (sau data 30 ngày)
- Gợi ý topic theo performance (optional)

---

## 10. Ảnh hưởng park khác

| Park | Ảnh hưởng |
|------|-----------|
| Pharmacy ERP | Không — mig riêng, không đọc bảng nghiệp vụ |
| Family OS | Không — không share worker/mig |
| Survey/PHC | Chỉ link CTA khi brand Novixa cấu hình |
| novixa-site | Có thể nhận commit MD qua connector — cùng mô hình CMS Git hiện tại |
| WP Vân Đỉnh Trà | Chỉ REST — ngoài Kit DB |

---

## 11. Chi phí vận hành (gắn settings)

- Trần mặc định seed: **$120 / tháng / org** (override per brand).  
- Tham chiếu chi tiết volume 30/60/120: canvas ngân sách Content Hub.  
- V1 bắt buộc: không gen ảnh khi vượt trần (trừ override có audit).

---

## 12. Acceptance V1

- [ ] Thêm Brand + 1 Site Astro + 1 Site WP + 4 Channel Page **không** cần migration mới  
- [ ] Đặt trần global $120 và brand override; gen bị chặn khi vượt  
- [ ] 1 Topic → nhiều Variant + 3 ảnh → chọn → Approved  
- [ ] Publish thử: ≥1 web connector + ≥1 FB Page  
- [ ] Mig chỉ trong `migration-files.content.txt`  
- [ ] Pharmacy smoke (login, POS read) không đổi hành vi  

---

## 13. Quyết định đã chốt (từ trao đổi)

| # | Quyết định |
|---|------------|
| D1 | Xây **Content Park trong KitPlatform**, không dừng ở Make/Sheet vĩnh viễn |
| D2 | **Mig riêng** — không vào Pharmacy prod manifest |
| D3 | PHC chỉ là **một CTA/channel tin**, không phải xương sống park |
| D4 | 4 web + 4 page **ngang nhau** về mô hình dữ liệu |
| D5 | Ảnh: 3 candidates + duyệt; hướng chất lượng gần ChatGPT Free trong ngân sách |
| D6 | Có **cài đặt trần ngân sách** trong Admin |

---

## 14. Việc tiếp theo (implement)

1. Seed `content_org_settings.monthly_ceiling_usd = 120` (hoặc số user chốt).  
2. Scaffold pack + mig `001` settings/brand/topic.  
3. Admin Budget + Brands shell.  
4. Nối generator (tái sử dụng kinh nghiệm `novixa-site` Gemini/image scripts qua service boundary).

**Không** triển khai connector hàng loạt trước khi Budget + Topic Review chạy ổn.

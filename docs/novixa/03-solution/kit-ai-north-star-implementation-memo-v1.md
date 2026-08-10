# KitPlatform · AI North Star & Implementation Memo v1

**Mã:** KIT-AI-NS-01 · **Phạm vi:** KitPlatform Kernel + AI Gateway + Packs (Novixa / Famixa / future)  
**Ngày:** 2026-08-10 · **Trạng thái:** SoT triển khai (memo)  
**Độc giả:** Founder · Tech lead · Product pack owners  

**Liên quan:**  
[platform-kernel-and-solution-packs-v1.md](./platform-kernel-and-solution-packs-v1.md) ·  
[enterprise-architecture-gap-matrix-v1.md](./enterprise-architecture-gap-matrix-v1.md) ·  
[behavior-os-north-star-v1.md](./behavior-os-north-star-v1.md) ·  
[parent-success-engine-v1.md](./parent-success-engine-v1.md) ·  
[famixa-product-design-manifesto-v2.md](./famixa-product-design-manifesto-v2.md)

---

## 0. Mục tiêu dài hạn (North Star)

> **KitPlatform = nền tảng nghiệp vụ đa ngành + trí tuệ domain + ổ cắm não AI thay được.**  
> Không phải “KIT GPT”. Không xây lại platform khi OpenAI/Gemini/Claude đổi đời.  
> Khi có khách và tiềm lực: **mở ngang** (thêm pack) và **mở sâu** (cùng ngành khôn hơn) trên cùng xương sống.

Ba năng lực phải giữ mãi:

| Năng lực | Nghĩa |
|----------|--------|
| **Swap brains** | Đổi / thêm LLM, fallback, multi-provider bằng config |
| **Scale ngang** | Thêm ngành / pack / quốc gia / tenant — không fork kernel |
| **Scale sâu** | Cùng pack: rules → memory → LLM lời → Agent → learning loop |

**Câu nhớ:** *Freeze = máy & ổ cắm · Swap = não & dây · Grow = thêm phòng / đào tầng khi nhà có người và có tiền.*

---

## 1. Kiến trúc đích (không tự train foundation LLM)

```
                    ┌─────────────────────────────────────┐
                    │           Product surfaces           │
                    │   Novixa UI · Famixa UI · Admin      │
                    └──────────────────┬──────────────────┘
                                       │
                    ┌──────────────────▼──────────────────┐
                    │     Pack Intelligence (domain)       │
                    │  Rules · Workflow · Tools · RAG pack │
                    │  Novixa ≠ Famixa (tách knowledge)    │
                    └──────────────────┬──────────────────┘
                                       │ xin: role + payload đã sanitize
                    ┌──────────────────▼──────────────────┐
                    │            KIT AI Gateway            │
                    │  Policy Gate · Budget · Catalog      │
                    │  Router · Meter · Audit · Kill-switch│
                    └──────────────────┬──────────────────┘
                                       │
              ┌────────────────────────┼────────────────────────┐
              ▼                        ▼                        ▼
          OpenAI                   Gemini                    Claude …
           (LLM)                    (LLM)                     (LLM)
                                       │
                    ┌──────────────────▼──────────────────┐
                    │     KitPlatform Kernel (SoT data)    │
                    │  Tenant · Auth · Billing · Events    │
                    │  Pack data planes — AI không đụng DB │
                    └─────────────────────────────────────┘
```

### Vai trò từng lớp

| Lớp | Việc | Không làm |
|-----|------|-----------|
| **LLM** | Ngôn ngữ, tóm tắt, viết lời trên tín hiệu đã có | Tự truy cập DB; tự quyết số liệu nghiệp vụ |
| **Agent** (sau này) | Gọi **tool allowlist** theo vòng; plan hẹp | Agent “thần thông” mọi quyền |
| **Pack Intelligence** | Luật nghề VN + tính toán + schema tool | Nhét OpenAI SDK vào từng màn hình |
| **Gateway** | Chọn não, ngân sách, meter, fallback | Chứa nghiệp vụ nhà thuốc / gia đình |
| **Kernel** | Tenant, quyền, SoT, billing, event | Biết “model nào đang hot” |

**Nguyên tắc sản phẩm:** *Deterministic trước, generative sau.*  
Số liệu / quyền / trạng thái do Platform + rules chốt → LLM (nếu có) chỉ diễn đạt → Output Guard → UI hoặc cache.

---

## 2. Freeze / Swap / Grow

Dán cạnh mọi thiết kế AI / pack mới. Hỏi: cột nào?

| **FREEZE** (đóng băng sớm) | **SWAP** (thay được) | **GROW** (phình sau) |
|----------------------------|----------------------|----------------------|
| Kernel đa tenant + isolation | Provider LLM | Pack ngành mới (ngang) |
| Auth / RBAC / audit | Model id trong catalog | Depth trong pack (sâu) |
| Pack boundary | Prompt / temperature theo `role` | Gói AI+ / entitlement |
| Billing + capability codes | Fallback chain → template | RAG knowledge domain lớn |
| SoT nghiệp vụ (API/events/DB) | UI copy / theme | Agent hẹp + thêm tool |
| AI Gateway contract (`role` → call) | Cache TTL, max tokens/role | Learning loop có hệ thống |
| Policy Gate (consent, allowlist) | Kill-switch, số budget | Multi-provider + route giá |
| Meter schema (tenant×product×feature×$) | Adapter SDK provider | Fine-tune domain (tuỳ vốn) |
| Identity proposal/digest + feedback hook | Cách viết lời trên output đã chốt | Insight ẩn danh xuyên tenant |
| LLM không quyết nghiệp vụ, không đụng DB | | Observability burn/quality |

**Quy tắc đọc**

- **Freeze:** “5 năm nữa vẫn cần?” → spec + test + không phá contract.  
- **Swap:** “Não / vendor đổi tuần sau?” → chỉ catalog / adapter.  
- **Grow:** “Chưa có khách/vốn đã cần?” → feature flag; bật theo milestone.

---

## 3. KIT AI Principles (12 quy tắc cứng)

1. **LLM không quyết định nghiệp vụ.** Số liệu, quyền, trạng thái do Platform / rule engine chốt trước; LLM chỉ diễn đạt hoặc giải thích kết quả đã xác nhận.  
2. **LLM không truy cập database / secret trực tiếp.** Mọi dữ liệu vào model qua API/tool KIT, đã lọc tenant và quyền.  
3. **Mọi lời gọi AI đi qua AI Gateway + Policy Gate.** Provider, budget, rate limit, allowlist tool, audit — không gọi thẳng từ feature.  
4. **Tenant isolation mặc định.** Không dùng data khách A phục vụ khách B trừ consent + ẩn danh / tổng hợp được duyệt.  
5. **Deterministic trước, generative sau.** Rules/tính toán/playbook trước; LLM sau (nếu cần).  
6. **Agent chỉ dùng tool trong allowlist.** Có schema, timeout; kết quả số qua bước kiểm tra KIT trước khi LLM viết câu.  
7. **Human-in-the-loop với hành động có hậu quả.** Nhắc hàng loạt, nhập hàng lớn, đổi lịch con, trừ tiền… → đề xuất hoặc người xác nhận.  
8. **Fail closed / fallback an toàn.** Lỗi LLM / hết budget / không pass gate → template/rule sẵn; không UI trống, không bịa số.  
9. **Learning loop có chủ đích.** “Thông minh hơn” = feedback + analytics → cải knowledge / prompt / **rules** — không giả định model tự học từ mỗi chat.  
10. **Chi phí & bề mặt có kiểm soát.** Không gọi LLM mặc định mọi màn; ưu tiên cache; đo “đúng việc” trước “hay lời”.  
11. **Mọi lời gọi LLM qua Model Catalog + `role`.** Cấm hardcode model trong feature pack.  
12. **Mọi lời gọi gắn budget scope và meter.** Hết hạn mức → fallback deterministic (nguyên tắc 8).

### 5 non-goals

1. Không tự xây foundation LLM / “KIT GPT” cạnh tranh OpenAI–Google–Anthropic.  
2. Không LLM free-form chat thay luật sản phẩm (Famixa: không chatbot dạy con thay bố mẹ).  
3. Không dump raw PII / lịch sử gia đình / dữ liệu nhà thuốc “cho AI hiểu hết”.  
4. Không fine-tune domain sớm khi chưa có Policy Gate, meter, feedback loop và case gắn nhãn đủ.  
5. Không dùng chung một “não” Novixa–Famixa — cùng Gateway; tách knowledge, prompt pack, tool pack, metric.

### Non-goal chi phí (bổ sung)

- Không gọi LLM mà không meter.  
- Không share quota ngân sách giữa hai tenant.  
- Không Agent không trần số vòng tool / trần chi phí phiên.

---

## 4. Model Catalog & chi phí (bắt buộc khi có LLM)

### 4.1 Model Catalog (config)

Mỗi dòng não có ít nhất: `provider`, `modelId`, `role` (`rewrite` | `summarize` | `agent_plan` | `vision` | …), `maxTokensIn/Out`, `unitCostEstimate`, `enabled`, `fallbackChain`.

Feature chỉ xin **`role`** (vd. `famixa.praise_rewrite`). Gateway chọn model.

### 4.2 Budget đa trục

| Trục | Mục đích |
|------|----------|
| Tenant | Trần nhà thuốc / gia đình theo tháng |
| Product | Novixa vs Famixa vs platform |
| Feature | Digest ≠ morning note ≠ agent |
| Tier | Free = 0 hoặc cực mỏng; AI+ mở rộng |
| Global/day | Trần công ty — tránh hóa đơn bất ngờ |

### 4.3 Meter tối thiểu mỗi request

`tenantId, product, feature, role, provider, model, tokensIn, tokensOut, latencyMs, success, fallbackReason, costEstimate`  
(Không log raw PII dài.)

### 4.4 Routing đủ dùng

| Việc | Não |
|------|-----|
| Viết lại lời / note ngắn | Model nhỏ, rẻ |
| Phân tích nhiều bước | Model mạnh hơn, ít lần, có cache |
| Fail / chậm / hết tiền | Model rẻ hơn hoặc **không LLM** |

### 4.5 Ops

- API key chỉ server / secret store  
- Hard cap phía provider + soft/hard cap KIT  
- Alert 50% / 80% / 100%  
- Kill-switch một flag: tắt LLM toàn cục, giữ rule engine

---

## 5. “Thông minh hơn theo thời gian” — tách 3 tầng

| Tầng | Tự khôn mỗi ngày? | Ai chịu |
|------|-------------------|---------|
| LLM gốc (GPT/Gemini/…) | **Không** từ chat khách | Nhà cung cấp update model |
| Context tenant + rules pack | **Có cảm giác** khôn hơn | Product KIT (memory, school, habit…) |
| Feedback → analytics → đổi rules/prompt/knowledge | **Có**, nếu thiết kế loop | Product + ops KIT |

**Marketing an toàn (Famixa):**  
*“Càng dùng, Famixa càng hiểu nhà bạn — nhờ nhớ nhịp và luật chăm sóc, không phải AI tự học lén chuyện riêng của con.”*

**Learning loop bắt buộc thiết kế sớm (kể cả khi chưa bật LLM):**

```
USER → AI/Rule response → Feedback (👍👎 + lý do)
         → KIT Analytics → Knowledge | Prompt | Rules → tốt hơn
```

Cross-tenant learning chỉ sau ẩn danh + policy — không mặc định.

---

## 6. Chiều ngang / chiều sâu (Grow có điều kiện)

### Ngang

```
Kernel → Pack Pharmacy | Pack Family | Pack Future…
         cùng Auth, Billing, Files, AI Gateway, Observability
```

### Sâu (trong một pack)

```
L1 Rules & workflows
L2 Memory / signals
L3 Generative (LLM lời) — khi Gateway+budget sẵn
L4 Agent hẹp + tools
L5 Learning loop có dashboard
L6 (tuỳ vốn) RAG lớn / fine-tune domain — không foundation
```

**Điều kiện mở tầng sâu (ví dụ):** retention ổn · meter chạy · use case ROI rõ · feedback có gắn feature.  
Không đủ điều kiện → không mở Agent toàn cục.

---

## 7. Lộ trình triển khai

### Phase 0 — Domain intelligence không LLM *(đang / tiếp tục)*

- Famixa: Behavior OS, School Season, habit, twin, digest template…  
- Novixa: nghiệp vụ + báo cáo tính toán trước khi nghĩ AI  
- **Done khi:** giá trị sản phẩm không phụ thuộc LLM  

### Phase 1 — AI Gateway skeleton

- `IAiGateway` / completion theo `role`  
- 1 provider  
- Meter table + kill-switch  
- Fallback = template  
- **Done khi:** 1 feature gọi qua Gateway (có thể staging)  

### Phase 2 — Catalog + Budget + Tier

- Model catalog config  
- Budget tenant × feature × tier  
- Admin usage nội bộ tối giản  
- Free vs AI+ rõ trên capability  
- **Done khi:** hết budget → fallback, có log  

### Phase 3 — Multi-provider + fallback chain

- Adapter 2  
- Role rẻ vs role mạnh  
- Alert burn rate  
- **Done khi:** đổi model production không sửa pack UI  

### Phase 4 — Agent hẹp (Ưu tiên Novixa)

- 3–5 tools allowlist / use case đo được (vd. tồn → đề xuất nhập đã tính số)  
- Human-in-the-loop hành động lớn  
- **Done khi:** LLM không tự bịa số tồn / doanh thu  

### Phase 5 — Learning loop + tối ưu chi phí

- 👍👎 + lý do trên mọi bề mặt AI  
- Dashboard: top fail, top chi phí, % fallback  
- Rút ngắn prompt, cache, hạ model  
- **Done khi:** cải rules/knowledge theo tháng có chủ  

### Phase 6 — Tuỳ vốn (không bắt buộc)

- RAG kho lớn / fine-tune domain  
- **Không:** tự research foundation model  

---

## 8. Ưu tiên sản phẩm

| Pack | Trước | Sau (khi Gateway sẵn) |
|------|--------|------------------------|
| **Famixa** | Rules, quiet hours, habit, twin, template | Rewrite lời / digest / letter trên signal đã chốt; cache; AI+ |
| **Novixa** | Tính tồn, doanh thu, quy trình | Báo cáo “vì sao” + checklist; Agent hẹp nhập hàng |
| **Platform** | Tenant, billing, packs, events | Gateway, catalog, meter, policy |

Famixa **không** lấy Agent chat làm trung tâm sản phẩm (khớp manifesto Fami).

---

## 9. Checklist chống xây lại (definition of ready cho epic AI)

Trước khi merge epic gắn LLM/Agent:

- [ ] Có `role` trong catalog (không hardcode model)  
- [ ] Đi qua Gateway + Policy (tenant, quyền)  
- [ ] Có budget scope + meter  
- [ ] Có fallback deterministic  
- [ ] Output không được đổi số liệu SoT  
- [ ] Có chỗ gắn feedback (dù UI tối giản)  
- [ ] Kill-switch không phá rule path  
- [ ] Pack knowledge không copy-paste từ pack khác  

---

## 10. Anti-patterns (cấm)

- Gọi OpenAI từ controller pack “cho nhanh”  
- Chatbot Free cho mọi user không meter  
- Đưa raw ledger / nhật ký con full vào prompt  
- “AI quyết định” sao / tồn / quiet hours  
- Fine-tune vì FOMO trước khi có feedback loop  
- Một prompt God phục vụ cả nhà thuốc lẫn gia đình  

---

## 11. Câu SoT một dòng

> **KitPlatform thuê bộ não ngôn ngữ; tự xây dữ liệu, quyền, luật nghiệp vụ, ổ cắm AI và vòng học từ feedback. Não đổi — ổ cắm giữ. Khách nhiều — pack ngang và tầng sâu thêm, không đúc lại máy.**

---

*Owner: KitPlatform / Founder review · Cập nhật khi Phase 1 Gateway có ticket cụ thể trong backlog kỹ thuật.*

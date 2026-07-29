# Ghi nhớ gói Famixa — nguồn nội dung website

**Mã:** KIT-MEMO-FO-PKG-WEB-01 · **Ngày:** 2026-07-29  
**Đối tượng:** Agent / người viết copy cho `famixa-site` / `famixa.vn`

## Quy tắc bắt buộc

Khi được yêu cầu **làm website / landing / pricing / so sánh gói** Famixa:

1. **Lấy nội dung gói từ các tài liệu SoT dưới đây** — không bịa giá, capability, hay outcome mới.
2. **Ưu tiên code** nếu lệch doc: `FamilyPlanCapabilityMatrix` trong  
   `src/Packs/FamilyOS/.../FamilyCapabilityContracts.cs`
3. **Không** copy messaging từ Pharmacy / Novixa admin / gói khác.

## SoT cần đọc (theo thứ tự)

| # | File | Lấy gì |
|---|------|--------|
| 1 | [`family-commercial-packaging-v1.md`](./family-commercial-packaging-v1.md) | Giá, plan codes, max trẻ, capability matrix, trial = Pro |
| 2 | [`famixa-dna-blueprint-roadmap-v1.md`](./famixa-dna-blueprint-roadmap-v1.md) | DNA, câu gốc, cảm nhận 1 câu / gói, Blueprint vs form Settings |
| 3 | `FamilyCapabilityContracts.cs` | SoT capability / tier / upgrade hint (nếu lệch #1–2) |

## 4 gói — khung copy (rút từ SoT)

| Gói | Outcome | Giá tham chiếu | Cảm nhận (1 câu) |
|-----|---------|----------------|------------------|
| **Free** | Trải nghiệm Famixa | 0đ · max 1 trẻ | “Nhà có nhịp ngày — Famixa ghi nhận.” |
| **Plus** | Family Growth Plan | 99k/tháng · 990k/năm · max 2 trẻ | “Nhà đang lớn lên — tôi thấy tín hiệu & đề xuất.” |
| **Pro** (hero) | Family Peace Plan | 199k/tháng · 1.99tr/năm · ∞ trẻ | “Famixa đồng hành — bớt nhắc, có chứng cứ & thỏa thuận.” |
| **AI+** | Đồng hành AI chuyên sâu | 399k/tháng · 3.99tr/năm · ∞ trẻ | “Có playbook tuần & nhìn trước giai đoạn — như mentor riêng.” |

**Trial 30 ngày = quyền Pro** (để sống Peace trước paywall).  
Website **ưu tiên CTA Pro**, không ép Plus làm hero.

## DNA / positioning (bắt buộc giữ)

> Every Family is Unique. Every Family Can Grow.

- AI không đưa công thức chung — đề xuất *cho nhà bạn, lúc này*.
- Không MDM / đo Screen Time máy; Screen Time = thỏa thuận trong nhà.
- Growth Zone ≠ bảng điểm xếp hạng.
- Không pitch “app checklist / quản lý việc nhà”.
- Kịch bản tự tin / bubble trường: chỉ dùng ngôn ngữ DNA — SoT [`famixa-self-calibration-playbook-v1.md`](./famixa-self-calibration-playbook-v1.md) (không “chữa tự ti”).
- Positioning quan tâm: [`famixa-growth-balance-v1.md`](./famixa-growth-balance-v1.md) — “quan tâm có phương pháp”, tránh tự ti / thiếu phấn đấu / dễ hư; không “app quản con”.

## Capability (không liệt kê task count)

Dùng matrix trong packaging doc. Website có thể nhóm:

- Free: nhịp ngày + insight tuần  
- Plus: + timeline / twin / AI suggest  
- Pro: + Coach / Report / Wallet / Letter / Replay  
- AI+: + Deep Playbook (`ai_plus_deep`)

## Anti-patterns

- Đổi giá / số trẻ / tên outcome mà không cập nhật SoT trước  
- Gọi Plus là gói chính  
- Promise “AI gọi điện / chat LLM tự do / chặn app”  
- Form “8 lớp Blueprint” trên marketing như Settings phức tạp  

## Checklist trước khi ship copy web

- [ ] Giá & tên gói khớp packaging v1  
- [ ] Hero CTA = Pro (Peace Plan)  
- [ ] Trial = trải nghiệm Pro  
- [ ] DNA tagline có mặt (hoặc paraphrase trung thành)  
- [ ] Capability không vượt matrix code  

---

*Khi user nói “làm website / cập nhật pricing / so sánh gói” — đọc memo này trước, rồi mới sửa `famixa-site`.*

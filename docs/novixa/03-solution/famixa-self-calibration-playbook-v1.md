# Famixa · Self-Calibration Playbook v1

**Mã:** KIT-PRD-FO-CAL-01 · **Pack:** FamilyOS / Famixa  
**Ngày:** 2026-07-29 · **Trạng thái:** Wave A+ shipped (capture + DNA/Coach) · nâng cấp theo checklist §7  
**Phụ thuộc:** [famixa-dna-blueprint-roadmap-v1.md](./famixa-dna-blueprint-roadmap-v1.md) · [behavior-os-north-star-v1.md](./behavior-os-north-star-v1.md) · `FamilySelfCalibration` (code)

> **Mục đích file này:** SoT kịch bản “nền yếu + tự đánh giá cao + bubble trường → cú sốc bạn ngoài → mất tự tin” — để **soát lại / nâng cấp** khi mở Sóng B–C. Không thay capability matrix.

---

## 1. Vấn đề (persona)

Con học **trường A** (môi trường tương đối dễ / ít đối chiếu ngoài):

1. Nền học lực thực tế **yếu hơn** mức con (và đôi khi nhà) tưởng.  
2. Trong bubble → **ảo giác khá** (overconfidence).  
3. Tiếp xúc bạn trường khác / kỳ thi ngoài → thấy khoảng cách → **sụp tự tin**, tránh thử, hoặc phủ nhận.

**Famixa không** xếp hạng trường / so sánh nhà khác công khai (non-goal DNA).  
**Famixa có** giúp phụ huynh: *hiệu chỉnh tự đánh giá bằng bằng chứng nhỏ*, chuẩn bị trước cú sốc, và dựng lại tự tin an toàn sau cú sốc.

---

## 2. Liên kết sản phẩm đã có

| Mảnh | Vai trò | Giới hạn |
|------|---------|----------|
| Behavior OS `illusion_risk` | Phát hiện “học ảo” ở **một việc** (skim + tự bảo hiểu) | Không biết bubble trường / peer shock |
| Twin `learning_depth` | Độ sâu thói quen học | Không narrative tự tin |
| Blueprint L1/L6/L8 (Wave A) | DNA card cơ bản | Chưa có L3/L4 calibration trước CAL-01 |
| **CAL-01 (file này + code)** | L3/L4 sparse + phase + Coach tip + DNA Next | Chưa Growth Zone axis / Timeline |

---

## 3. Mô hình dữ liệu (soát lại tại đây)

Lưu **sparse** trong `pack_family.family_blueprint.layers_json` (không bảng riêng v1).

### 3.1 L4 `context.school` (môi trường)

| Field | Values | Ý nghĩa |
|-------|--------|---------|
| `code` | `bubble_easy` \| `mixed` \| `competitive` \| `unknown` | Cảm nhận phụ huynh về mức đối chiếu ngoài của trường |
| `capturedAt` | ISO | Lần capture gần nhất |
| `source` | `parent_capture` \| `inferred` | |

### 3.2 L3 `child.selfCalibration` (trait học / tự đánh giá)

| Field | Values | Ý nghĩa |
|-------|--------|---------|
| `selfView` | `overestimates` \| `calibrated` \| `underestimates` \| `unknown` | Con tự đánh giá so với thực lực (theo phụ huynh) |
| `peerShock` | `none` \| `mild` \| `sharp` \| `unknown` | Cú sốc gần đây khi gặp bạn / môi trường khó hơn |
| `illusionHits7d` | int | Đếm soft từ Behavior `illusion_risk` (7 ngày, best-effort) |
| `phase` | xem §4 | Phase playbook hiện tại |
| `updatedAt` | ISO | |
| `history` | array ≤ 8 | Snapshot `{ at, phase, selfView, peerShock, school }` — để soát / nâng cấp |

### 3.3 DNA / Coach derived (không form dài)

- `dna.nextStepVi` — bước hôm nay theo phase  
- `dna.coachTipVi` — 1 câu Coach cho phụ huynh  
- Free teaser: vẫn khóa Focus/Next đầy đủ; có thể hiện CTA capture ngắn

**Code SoT labels / next / tip:** `FamilySelfCalibration` trong Application pack.

---

## 4. Phase playbook (ưu tiên)

| Phase | Điều kiện (rule) | Ưu tiên phụ huynh | Next step (hướng) |
|-------|------------------|-------------------|-------------------|
| `needs_capture` | Thiếu school hoặc selfView | Hỏi 1–2 câu | Capture nhẹ |
| `bubble_risk` | `bubble_easy` + `overestimates` (+ illusion) · peerShock ≠ sharp | **Chuẩn bị trước cú sốc** | Evidence nhỏ, không khen “giỏi nhất lớp” |
| `peer_shock` | `peerShock` = mild/sharp | **Ổn định cảm xúc + khung** | Tách giá trị khỏi xếp hạng; 1 việc chứng minh được |
| `rebuild` | Sau shock hoặc underestimates nặng | **Dựng lại tự tin bằng bằng chứng** | Chuỗi thắng nhỏ 3 ngày |
| `steady` | calibrated + không shock | Giữ nhịp | Duy trì retrieval / hỏi “học được gì” |

**Ưu tiên ship:** `peer_shock` > `bubble_risk` > `rebuild` > `steady` > `needs_capture`.

---

## 5. Nguyên tắc Coach (bất biến)

1. Không nói “con yếu / trường kém”.  
2. Không so sánh công khai với bạn / anh chị / trường khác.  
3. Tách: *nỗ lực + bằng chứng* ≠ *xếp hạng*.  
4. Trước shock: hiệu chỉnh êm (retrieval, hỏi giải thích).  
5. Sau shock: an toàn cảm xúc trước, học lực sau (24–72h).  
6. Mọi mutate Routine vẫn qua Inbox (AFE).

---

## 6. API / UI đã ship (CAL-01)

| Surface | Chi tiết |
|---------|----------|
| `POST …/blueprint/calibration` | Capture L3/L4 + ghi `history` + refresh DNA |
| `GET …/blueprint/dna` | + `calibrationPhaseCode`, `calibrationLabelVi`, `coachTipVi`, `needsCalibrationCapture` |
| DNA card UI | CTA “Famixa hiểu đúng nhà hơn” → sheet 2–3 câu |
| Behavior retrieval | Khi `illusion_risk` → soft bump `illusionHits7d` trên Blueprint (best-effort) |

Gói: capture + tip DNA **Free được**; Coach sâu / Brief dài vẫn gate Pro (`parenting_coach`) ở Sóng B.

---

## 7. Checklist nâng cấp (khi cần soát)

### P1 — Sóng B
- [ ] Coach panel Pro đọc phase → FAQ / doThis cố định theo `FamilySelfCalibration`
- [ ] Growth Zone trục `self_calibration` (narrative, không điểm %)
- [ ] Letter / ROP tháng: 1 đoạn “hiệu chỉnh tự tin” nếu phase ≠ steady

### P2 — Sóng C
- [ ] L2 Stage Timeline: trước kỳ thi / trại hè / giao lưu trường khác → proposal Inbox “chuẩn bị an toàn”
- [ ] Infer `bubble_easy` từ tín hiệu (nhiều illusion + ít retrieval) — vẫn hỏi 1 câu xác nhận
- [ ] Bảng `family_calibration_event` nếu cần analytics (hiện history JSON đủ)

### P3 — AI+
- [ ] Deep Playbook tuần theo phase (`ai_plus_deep`)
- [ ] Không LLM free-form; template + Blueprint refs

### Không làm
- [ ] Scraping điểm trường / xếp hạng trường  
- [ ] Peer leaderboard giữa các nhà Famixa  
- [ ] Form 8 lớp bắt buộc lúc setup  

---

## 8. Cách soát lại nhanh (ops / product)

1. Đọc `layers_json->child.selfCalibration` + `context.school` trên family pilot.  
2. Đối chiếu `history[]` với phase hiện tại.  
3. Xem `illusion_risk` events 7 ngày (Behavior).  
4. Nếu rule phase lệch thực tế phụ huynh → sửa rule trong `FamilySelfCalibration.ResolvePhase` + cập nhật §4 file này.  
5. Website copy: **không** pitch “chữa tự ti”; dùng ngôn ngữ DNA (“hiểu nhà bạn / bước phù hợp”) — xem memo gói.

---

## 9. Chốt một dòng

**Hiệu chỉnh tự tin bằng bằng chứng nhỏ — không bằng xếp hạng.**  
Dữ liệu sparse trong Blueprint + playbook code là SoT để nâng cấp tiếp.

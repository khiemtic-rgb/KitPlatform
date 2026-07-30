# Famixa — IA Home & Báo cáo V2

**Mã:** FMX-SOL-21 · **Trạng thái:** Approved (local) · **Version:** 2.0
**Ngày:** 2026-07-29 · **Owner:** Product · **App:** `client/family-app` (parent board)

> **Nguyên tắc gốc:** Home không phải danh mục tính năng. Home là **trợ lý điều hành gia đình 30 giây**.
> Report / Letter / Replay / Timeline / Calendar là **ứng dụng con** trong OS, không phải nội dung chính của Home.

---

## 1. Vấn đề V1

Mọi card có **trọng số visual gần như nhau** → không có điểm nhấn, bố mẹ mới không biết nhìn đâu trước.

Nặng nhất ở tab **Báo cáo** (`FamilyValuePanel`): stack phẳng ~10 card cùng cỡ —
Onboarding → Coach (+4 FAQ) → Health Score → 3Q → ROP → Gương tuần → Wins → Letter → Replay → Ghi nhận → Timeline.

Hệ quả: Health Score (KPI cốt lõi, lý do trả phí) bị **chìm giữa stack**; Coach thành wiki thay vì brief.

---

## 2. Ba câu hỏi Home phải trả lời

| # | Câu hỏi | Thành phần |
|---|---------|-----------|
| 1 | Hôm nay gia đình thế nào? | Health Score (hero) |
| 2 | AI muốn mình làm gì **ngay**? | 1 đề xuất + 1 CTA |
| 3 | Việc gì cần ưu tiên hôm nay? | Hôm nay: n việc / n trễ |

Mọi thứ khác = drill-in.

---

## 3. Bố cục chuẩn

### 3.1 Home (`tab = home`, `ph-b4`)

```
Greeting + Health/Brief
→ AI: 1 đề xuất  [Thực hiện]
→ Hôm nay: n việc · n trễ
→ Tiến độ cả nhà (1 dòng)
→ Insight tuần (1 dòng)
→ Lối tắt ≤ 4 (theo ngữ cảnh)
```

### 3.2 Báo cáo (`tab = value`, `FamilyValuePanel`) — **hub + drill-in**

```
[Hero]   Family Health Score  86 · nhãn · delta · chi tiết (collapsed)
[AI]     1 việc nên làm hôm nay  [Xem cách làm]
[Row]    Gương tuần — 1 dòng insight        ›
[Row]    3Q tối — trạng thái hôm nay        ›
[List]   Growth Report (ROP)                ›
         Letter tháng                       ›
         Family Replay                      ›
         Timeline kỷ niệm                   ›
         Wins & Ghi nhận bố mẹ              ›
```

Drill-in = **một màn một mục tiêu**: back + đúng một nội dung, giữ nguyên nội dung/paywall V1.

---

## 4. Quy tắc bắt buộc

1. **Health Score = hero duy nhất** của tab Báo cáo; breakdown nằm trong `details`, không chiếm viewport đầu.
2. **AI mỗi ngày một việc.** “Tránh” + FAQ → drill-in Coach, không nằm ở hub.
3. **Không hơn 2 tầng.** Hub → drill-in. Không drill-in trong drill-in.
4. **Module bị chặn gói vẫn hiện dòng** + pill “Peace Plan”; bấm vào mở teaser/paywall — không ẩn (giữ đường upsell).
5. **Không lặp điều hướng.** Đã có row trong hub thì bỏ chip nhảy nhanh ở header.
6. **Lối tắt Home ≤ 4** và ưu tiên theo ngữ cảnh; phần còn lại thuộc tab/hub.
7. Deep-link cũ (`fv-3q`, `fv-rop`, `fv-ai-letter`, `fv-replay`, `fv-ai-wins`) phải map sang drill-in tương ứng.

---

## 5. Không làm

- Không xoá Report / Timeline / Replay / Letter — vẫn là retention + upsell, chỉ **không ở viewport đầu**.
- Không biến hub thành portal icon-grid.
- Không thêm KPI thứ hai cạnh Health Score ở hero.

---

## 6. Liên quan

- `client/family-app/src/modules/flow/FamilyValuePanel.tsx` — hub + drill-in
- `client/family-app/src/modules/flow/ParentBoardView.tsx` — tab bar, header Báo cáo
- `client/family-app/src/styles/app.css` — `.fv-hub*`, `.fv-hero*`, `.ph-b4*`
- Gói / paywall: `client/family-app/src/shared/billing/famixa-plan-copy.ts`

# Famixa — IA màn hình Con V2

**Mã:** FMX-SOL-22 · **Trạng thái:** Approved (local) · **Version:** 2.0  
**Ngày:** 2026-07-30 · **Owner:** Product · **App:** `client/family-app` (`KidFocusView`)

> **Nguyên tắc gốc:** Home của con không phải danh mục phần thưởng. Home là **một việc tiếp theo trong 10 giây**.
> Streak / Vườn / Lời khen / Challenge / Xin bố mẹ là **ứng dụng con**, không chiếm viewport đầu.

Song hành với IA bố mẹ: `famixa-home-report-ia-v2.md`.

---

## 1. Vấn đề V1

Trên phone, viewport đầu = Challenge + Behavior OS + Movie Night + XP nhà — **CTA “Mình đã làm” bị đẩy xuống dưới fold**.

Movie Night lặp 3–4 chỗ (header pill, hero card, “Con vừa tạo ra”, Tasks). Behavior OS dùng ngôn ngữ sản phẩm (`Behavior OS`) trên bề mặt con. Nhiều CTA ngang hàng: làm việc / đính kèm ảnh / FAB / đề xuất việc / xin phút màn hình.

---

## 2. Ba câu hỏi Home con phải trả lời

| # | Câu hỏi | Thành phần |
|---|---------|-----------|
| 1 | Việc tiếp theo của mình là gì? | Mission hero + 1 CTA chính |
| 2 | Foxy muốn mình làm gì ngay? | 1 câu Foxy (không carousel 4 tin) |
| 3 | Cả nhà gần phần thưởng chưa? | 1 dòng Movie Night |

Mọi thứ khác = hub row → drill-in (hoặc tab sẵn có).

---

## 3. Bố cục chuẩn (`tab = home`)

```
[optional] Soft-lock
Greeting + ⭐ + Movie mini (header)
→ [Hero] Nhiệm vụ tiếp theo  [Mình đã làm]
         · Đính kèm ảnh (link phụ)
→ [Foxy] 1 câu coach
→ [Strip] Movie Night n% · còn k việc
→ [Hub rows]
     Challenge tuần này          ›
     Lời khen hôm nay            ›
     Streak                      ›
     Khu vườn                    ›  (→ tab Nhật ký khi cần)
     Xin bố mẹ                   ›
```

Drill-in = **một màn một mục tiêu**: back + đúng một khối (khen / streak / garden / ask / challenge). Không drill-in trong drill-in.

---

## 4. Quy tắc bắt buộc

1. **Mission = hero duy nhất** của Home con; không card Movie Night / XP nhà cạnh hero.
2. **Một CTA hoàn thành** trên Home (`Mình đã làm`); ảnh = secondary; FAB trùng cùng hành động.
3. **Không hơn 2 tầng.** Hub → drill-in.
4. **Không lặp KPI.** Movie Night chỉ 1 strip + header pill (cùng số liệu).
5. **Không “Behavior OS”** trên bề mặt con — gộp cue vào câu Foxy.
6. **Xin bố mẹ** (đề xuất việc / phút màn hình) không cạnh CTA làm việc — nằm trong hub row.

---

## 5. Không làm

- Không xoá Streak / Vườn / Khen / Challenge — chỉ **không ở viewport đầu**.
- Không portal icon-grid.
- Không đổi tabbar 4 tab + FAB.

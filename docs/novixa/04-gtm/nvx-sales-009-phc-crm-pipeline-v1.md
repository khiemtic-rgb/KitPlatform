# NVX-SALES-009 — CRM pipeline PHC (Sheet 50 NT)

**Nguyên tắc:** Chưa có list ≠ dừng CRM. **50 NT là mục tiêu**, không điều kiện bắt đầu.

**Chưa có danh sách?** → bắt đầu bằng [003 — Lấy list từ 0](./nvx-sales-003-prospect-list-from-zero-v1.md) (Research trước, CRM sau khi có SĐT).

**File data:** [nvx-sales-009-phc-crm-pipeline-template.csv](./nvx-sales-009-phc-crm-pipeline-template.csv)

---

## 1. Pipeline stages (cột `status`)

Dùng **đúng một** giá trị:

| Status | Nghĩa | Bước kế típ |
|--------|--------|-------------|
| `Lead` | Có tên/SĐT, chưa liên hệ | Gửi Zalo (005) hoặc gọi mở đầu |
| `Contacted` | Đã nhắn/gọi ≥1 lần | Xin lịch Health Check |
| `Interested` | Đồng ý nghe / xin lịch HC | Chốt slot HC |
| `HealthCheck` | Đang làm / đã hoàn thành khảo sát | Gửi Owner Pack |
| `Assessed` | Đã gửi Kết quả tóm tắt | Gọi 10–15' (004) |
| `Demo` | Đã demo theo nỗi đau | Đề xuất Pilot |
| `PilotProposed` | Đã nói giá Pilot 299/598 | Chốt kickoff |
| `Pilot` | Đang chạy 30 ngày | Review ngày 30 |
| `Paid` | Chuyển gói chính thức | Handoff CS |
| `Lost` | Dừng — **bắt buộc** `lost_reason` | Phân tích tuần |

Không tạo thêm status ad-hoc. Nếu cần tạm dừng: giữ stage hiện tại + note `paused_until=YYYY-MM-DD`.

---

## 2. Lost reasons (cột `lost_reason`) — bắt buộc khi Lost

| Code | Khi dùng |
|------|----------|
| `no_need` | Không thấy nhu cầu đổi |
| `happy_current_sw` | Hài lòng phần mềm hiện tại |
| `price` | Giá / cảm thấy đắt |
| `fear_switch` | Sợ chuyển đổi / phiền nhân viên |
| `no_diff` | Chưa thấy khác biệt so với hiện tại |
| `no_time` | Thiếu thời gian làm HC / Pilot |
| `no_owner` | Thiếu người phụ trách phía NT |
| `bad_timing` | Chưa đúng thời điểm (mùa vụ, đang mở cửa…) |
| `missing_feature` | Thiếu tính năng cụ thể (ghi `notes`) |
| `other` | Khác (ghi rõ `notes`) |

**Quy tắc:** Lost mà trống `lost_reason` = không hợp lệ (sửa trong 24h).

---

## 3. Cột bắt buộc / khuyến nghị

| Cột | Bắt buộc | Ghi chú |
|-----|----------|---------|
| `pharmacy_name` | Có | |
| `owner_name` | Có nếu đã liên hệ | |
| `phone_zalo` | Có | |
| `district` | Có (ICP Thái Nguyên) | |
| `stores` | Có | Số cơ sở |
| `source` | Có | list / referral / web / event / other |
| `status` | Có | Enum §1 |
| `pain_1` | Từ `Assessed` | Copy từ Owner Pack |
| `owner_pack_sent` | Từ `Assessed` | `yes` / `no` |
| `pilot_focus` | Từ `PilotProposed` | 299k vận hành / 598k +dev |
| `next_action` | Luôn | 1 việc cụ thể |
| `next_action_date` | Luôn nếu chưa Lost/Paid | |
| `rep` | Có | Người phụ trách sales |
| `lost_reason` | Nếu Lost | Enum §2 |
| `submission_id` | Nếu có HC | Liên kết báo cáo |
| `last_contact_at` | Có | YYYY-MM-DD |
| `notes` | Không | Ngắn |

---

## 4. Cách dùng Sheet (50 NT Thái Nguyên)

1. Import CSV vào Google Sheets (File → Import → Replace current sheet).  
2. Data → Create a filter.  
3. Sheet thứ 2 (optional): Pivot `status` + count; Pivot `lost_reason` + count.  
4. Mỗi thứ 2: review chỉ lead có `next_action_date` ≤ hôm nay.  
5. Không xóa dòng Lost — giữ để học roadmap.

### View đề xuất (filter sẵn)

- **Hôm nay:** `next_action_date` = today, status ∉ Lost,Paid  
- **Funnel tuần:** group by status  
- **Owner Pack chờ gọi:** status = `Assessed`  
- **Pilot đang chạy:** status = `Pilot`  
- **Lost analysis:** status = `Lost`

---

## 5. KPI tuần (điền mỗi thứ 2)

| Chỉ số | Mục tiêu gợi ý (tuần) |
|--------|------------------------|
| Lead mới | ≥ 5 |
| Contacted | ≥ 80% lead tồn |
| Health Check hoàn thành | ≥ 3 |
| Owner Pack gửi | = số HC hoàn thành |
| Cuộc gọi sau Assessed | ≥ 1 lần / lead Assessed |
| PilotProposed | ≥ 1 |
| Pilot kickoff | theo pipeline |
| Paid | theo tháng |
| % Lost có reason | **100%** |

Không tối ưu “số tin Zalo” — tối ưu **Assessed → PilotProposed**.

---

## 6. Liên kết Sales Kit

- [004 Call](./nvx-sales-004-call-script-phc-v1.md)  
- [005 Zalo](./nvx-sales-005-zalo-script-phc-v1.md)  
- [007 Pilot](./nvx-sales-007-pilot-close-v1.md)  
- [Checklist sau Owner Pack](./phc-sales-call-checklist-after-owner-pack-v1.md)

---

## 7. Khi nào mới cần `phc_lead` trong DB

Giữ Sheet khi:

- < ~50 lead active đồng thời  
- 1–2 sales  
- Chưa cần báo cáo tenant chung  

Chỉ cân nhắc entity/API khi có nhiều người bán + cần gắn submission/Owner Pack tự động trên CRM nội bộ.

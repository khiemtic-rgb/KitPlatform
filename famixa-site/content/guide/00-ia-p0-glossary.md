# Famixa Family Guide — IA P0 + Glossary UI

> **SoT tip local (2026-07-30)** · Chỉ viết / đăng nội dung nằm trong trang này.  
> App: `client/family-app` · URL local: `http://localhost:5178` · Product URL: `https://home.famixa.vn`  
> Trạng thái: **LOCKED P0** — Journal / Timeline / Health Score / OTP **không** thuộc P0.

---

## 1. Information Architecture P0 (đăng web)

**Hub:** `/vi/huong-dan/` (EN sau: `/en/guide/`)

```
01. Bắt đầu với Famixa          ← P0 ship
│   ├── Tạo tài khoản / nhà mới           /vi/huong-dan/tao-tai-khoan
│   ├── Đăng nhập vào nhà                 /vi/huong-dan/dang-nhap
│   ├── Gia nhập nhà bằng mã mời          /vi/huong-dan/gia-nhap-bang-ma
│   ├── Chọn ai đang dùng (Who)           /vi/huong-dan/chon-thanh-vien
│   ├── Thêm thành viên                   /vi/huong-dan/them-thanh-vien
│   ├── Mời người thân (Mã nhà)           /vi/huong-dan/moi-thanh-vien
│   ├── Lịch hôm nay (Parent + Kid)       /vi/huong-dan/lich-hom-nay
│   ├── Thiết lập Routine đầu tiên        /vi/huong-dan/routine-dau-tien
│   └── Tài khoản & Cài đặt cơ bản        /vi/huong-dan/cai-dat
│
02. Dùng mỗi ngày               ← P1 (outline only, chưa viết full)
│   ├── Movie Night / Family Challenge
│   ├── Foxy kể (khen / streak / vườn)
│   ├── Hỏi bố mẹ (Ask + ví màn hình)
│   └── Coach / Brief (theo gói)
│
03. Gói & trial                 ← P1
│   └── Trial Pro · ân hạn 3 ngày · Peace Plan
│
04. Sự cố thường gặp            ← P0 FAQ ngắn trong từng bài + hub sau
│   ├── Quên mật khẩu / đăng nhập lại
│   ├── Đổi thiết bị / đăng xuất
│   └── Mã PIN bố mẹ (trên máy này)
│
⛔ Parked (không viết P0)
    Journal độc lập · Family Timeline · Health Score · OTP/SMS verify · RBAC nội bộ
```

### Thứ tự đọc đề xuất (funnel)

1. Tạo tài khoản → 2. Chọn thành viên → 3. Thêm / mời người → 4. Lịch hôm nay → 5. Routine → 6. Cài đặt

---

## 2. Glossary UI (copy đúng app — dùng nguyên văn)

### Thương hiệu & nhân vật

| Thuật ngữ UI | Nghĩa cho cha mẹ | Không viết |
|---|---|---|
| **Famixa** | App lịch sống gia đình | Family OS / Kit / Module… |
| **Nhà** | Không gian gia đình trên Famixa | Tenant / workspace (trừ khi hướng dẫn chọn nhiều nhà) |
| **Foxy** | Bạn đồng hành / kể chuyện với con | AI bot / chatbot |
| **AI Setup** | Wizard thiết lập hồ sơ nhà (onboarding) | “module AI” |

### Màn hình & nút (route thật)

| Màn hình (UI) | Route | Nút / nhãn chính |
|---|---|---|
| **Tạo nhà Famixa** | `/unlock` (register) | Họ và tên bố/mẹ · Email · Mật khẩu · **Tạo nhà & tiếp tục →** · Có mã mời từ nhà sẵn có? |
| **Đăng nhập** | `/unlock` (login) | Email · Mật khẩu · **Vào nhà →** · Cách khác (mã gia đình) |
| **Xin chào! / chọn thành viên** | `/who` | Thẻ tên · **Thêm thành viên** · **Mời tham gia** · **Quản lý** · **Ưu đãi Famixa** |
| **Mời tham gia nhà** (sheet) | `/who` | Mã mời · **Sao chép mã** · **Chia sẻ…** · **Gửi SMS** |
| **Quản trị gia đình** | `/family-admin` | Thành viên · Routine · Mã nhà · AI Setup · Tài khoản / Cài đặt |
| **Thành viên** | `/family-admin/members` | Thêm tên + vai trò · danh sách |
| **Mã nhà** | `/family-admin/invite` | Sao chép · Chia sẻ · Tạo mã mới · QR Code |
| **Routine** | `/family-admin/routine` | Đang dùng · Chọn chế độ · **Lưu chế độ** · Xem việc · Chỉnh nhẹ |
| **Tài khoản & Cài đặt** | `/family-admin/settings` | Gia hạn gói · Nhắc việc · Mã PIN bố mẹ · Đăng xuất thiết bị |
| **Hôm nay / lịch ngày** | `/today` | (Parent board / Kid focus — P0 mô tả “làm việc hôm nay”) |
| **AI Onboarding** | `/onboarding` | Welcome Foxy · Bỏ qua lần này → `/today` |
| **Gói / thanh toán** | `/pay` | Peace Plan / gia hạn |

### Vai trò thành viên (`ROLE_LABEL`)

| Code nội bộ | Hiển thị UI | Ghi chú guide |
|---|---|---|
| `guardian` | **Bố/Mẹ** | Quản trị, mời, routine, cài đặt |
| `caregiver` | **Người chăm sóc** | Người lớn hỗ trợ |
| `child` | **Con** | Lịch kid, Foxy, Ask |
| `viewer` | **Xem** | Hiếm — P0 không nhấn mạnh |

### Chế độ Routine (`FAMILY_MODE_OPTIONS`)

| UI | Hint UI |
|---|---|
| Bình thường | Theo lịch năm học |
| Nghỉ hè | Nhịp nhẹ hơn |
| Thi học kỳ | Ưu tiên học |
| Du lịch | Tạm đổi routine |
| Cuối tuần | T7–CN |
| Nghỉ lễ | Vài ngày |

### Billing (chỉ từ ngữ đã ship)

| UI | Ý nghĩa P0 |
|---|---|
| **Dùng thử Pro** / trial | ~30 ngày dùng đủ Peace / Pro entitlements |
| **Ân hạn 3 ngày** | Sau hết trial vẫn Pro ngắn hạn rồi về Free |
| **Peace Plan** | Gói trả phí giữ Coach / ROP… |
| **Gia hạn gói** | Nút trong Cài đặt |

### Từ cấm trong bài P0

`Module` · `Screen` · `API` · `tenant` · `workspace` (trừ bài chọn nhiều nhà) · `RBAC` · `OTP` · `endpoint` · `DTO` · tên tiếng Anh kỹ thuật không có trên UI.

### Từ thay thế

| Đừng viết | Viết |
|---|---|
| Đăng ký tenant | **Tạo nhà Famixa** |
| Join invite flow | **Gia nhập bằng mã mời** / **Mời tham gia** |
| Parent PIN gate | **Mã PIN bố mẹ** (4 số, trên máy này) |
| Day flow commitments | **Việc hôm nay** / **lịch ngày** |
| Team unlock | **Movie Night** / **Family Challenge** (P1) |

---

## 3. Scope viết bài P0 (checklist editor)

Mỗi bài **chỉ** được:

- Dùng nhãn nút đúng bảng §2
- Nêu lợi ích gia đình (vì sao dùng), không phóng đại
- Gắn ảnh trong `public/images/guide/` hoặc ghi `[Ảnh minh họa: <slug>]`
- Có mục **Mẹo nhà** (không bắt buộc “AI Tip” nếu bài không liên quan Coach)
- FAQ 3–5 câu khớp hành vi tip hiện tại
- Meta + URL theo slug §1

Mỗi bài **không** được:

- Bịa bước / nút không có trên tip
- Hứa Journal / Timeline / Health Score / mã OTP
- Trộn Pharmacy / Novixa admin

---

## 4. Ảnh P0 (thư mục web)

| File | Bài |
|---|---|
| `images/guide/01-tao-tai-khoan-dang-ky.png` | Tạo tài khoản — **đã chụp** |
| `images/guide/02-dang-nhap.png` | Đăng nhập — **đã chụp** |
| `images/guide/02b-dang-nhap-cach-khac.png` | Đăng nhập · Cách khác — **đã chụp** |
| `images/guide/03-ma-moi-collapse.png` | Đăng ký · Nhập mã — **đã chụp** |
| `images/guide/04-chon-thanh-vien.png` | Who — *cần session + API :5290* |
| `images/guide/05-moi-tham-gia-sheet.png` | Sheet mời — *cần session* |
| `images/guide/06-quan-tri.png` | Hub quản trị — *cần session* |
| `images/guide/07-thanh-vien.png` | Thành viên — *cần session* |
| `images/guide/08-ma-nha.png` | Mã nhà — *cần session* |
| `images/guide/09-routine.png` | Routine — *cần session* |
| `images/guide/10-cai-dat.png` | Cài đặt — *cần session* |
| `images/guide/11-lich-hom-nay.png` | Today — *cần session* |

---

## 5. Chốt vận hành

| Mục | Quyết định |
|---|---|
| Ngôn ngữ P0 | Tiếng Việt trước |
| Độ dài how-to | 400–800 từ core; FAQ/SEO block cuối |
| Verify | `status: verified` chỉ khi khớp screenshot tip |
| Đổi IA | Chỉ khi product đổi nhãn / route — cập nhật **trang này trước**, rồi sửa bài |

**Owner tip:** Family Guide SoT = file này + các bài trong `famixa-site/content/guide/*.md`.

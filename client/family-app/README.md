# FamilyOS Mobile App

Smartphone-first Daily Flow for FamilyOS Starter (Famixa).

**Dev URL:** http://localhost:5178  

## Demo GTM (chỉ xem)

```
http://localhost:5178/demo
```

- Nhà **2 bé** (Bảo Nhi + Đức Huy), lịch hè 2026 + năm học sẵn
- `seasonOn=false` lúc hè; giờ đi học đã seed trên blueprint
- Không sửa được (client + server gate `viewer`)

Hoặc đăng nhập tay: `DEMO_FAMILY` / `demo` / `Admin@123`

## Admin đầy đủ (local)

Unlock: `DEMO_FAMILY` / `admin` / `Admin@123` → chọn **Bảo Nhi** / **Đức Huy** hoặc **Mẹ/Bố**

## UX value

- **Bé:** một việc / màn hình — “Đến giờ” / “Tiếp theo” + nút **Xong rồi!** rất to  
- **Bố mẹ:** bảng nhà — ai cần chú ý, tiến độ từng người  
- Soft lock: giữ để đổi người / mã bố mẹ `1234` (bỏ qua ở chế độ demo)

## Run

```powershell
cd client/family-app
npm install
npm run dev
```

Requires local API + seed:

```powershell
.\scripts\seed-family-os-local.ps1
```

# FamilyOS Mobile App

Smartphone-first Daily Flow for FamilyOS Starter.

**Dev URL:** http://localhost:5178  
**Demo:** unlock with `DEMO_FAMILY` / `admin` / `Admin@123` → pick **Minh** (1 việc) hoặc **Mẹ/Bố** (bảng nhà)

## UX value

- **Bé:** một việc / màn hình — “Đến giờ” / “Tiếp theo” + nút **Xong rồi!** rất to  
- **Bố mẹ:** bảng nhà — ai cần chú ý, tiến độ từng người, không hỏi “xong chưa?”  
- **Nhắc nhanh:** copy/share câu nhắc sang Zalo (local only — chưa deploy / chưa push server)  
- Soft lock: giữ để đổi người / mã bố mẹ `1234`

## UX (bé + smartphone)

1. **Parent unlock once** — form only for adults; session stays on the phone
2. **Who are you?** — kids first, big name chips (single child = full-width)
3. **Today** — child sees only their commitments, unfinished first, 64px check boxes
4. **Celebrate** — ring + card when everything is done
5. **Safe areas** — `100dvh`, notch padding, `font-size: 16px` inputs (no iOS zoom)

Guardian view still shows whole-family progress and a refresh control.

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

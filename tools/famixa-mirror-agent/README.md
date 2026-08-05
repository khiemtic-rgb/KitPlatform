# Famixa Mirror Agent (Windows) — M1

Nhẹ, **không khóa máy**. Chỉ gửi:
- Heartbeat (app đang foreground)
- Thời lượng app trong ngày → FamilyOS Mirror

## Cài 1 thao tác (khuyến nghị)

1. Đăng nhập **Famixa** bằng tài khoản bố/mẹ **trên máy Windows của con** (hoặc tải file rồi copy sang máy đó).
2. Chọn đúng **một con** (không phải “Cả nhà”).
3. Card **Gương tối** → **Tải & cài Agent Windows**.
4. Double-click file `Famixa-Cai-Agent-….cmd` vừa tải.
5. Quay lại Famixa → **Làm mới gương**.

Installer sẽ:
- Cài vào `%LOCALAPPDATA%\Famixa\MirrorAgent`
- Ghi `config.json` (family / con / token phiên đăng nhập)
- Đăng ký Task Scheduler (chạy lại khi đăng nhập Windows)
- Khởi động Agent ngay

> Script tải từ cùng origin Famixa (`/mirror-agent/…`). Local: `http://localhost:5178`.  
> Token trong file `.cmd` như mật khẩu tạm — chỉ dùng máy nhà, không gửi chat công khai.

## Cài tay (dev)

1. Copy thư mục `tools/famixa-mirror-agent` (hoặc dùng bản trong `client/family-app/public/mirror-agent`).
2. `Copy-Item config.example.json config.json` và điền token / ids.
3. `powershell -ExecutionPolicy Bypass -File .\Run-FamixaMirrorAgent.ps1`
4. (Tuỳ chọn) `.\Register-ScheduledTask.ps1`

## API dùng

- `POST /api/family-os/families/{id}/mirror/heartbeat`
- `POST /api/family-os/families/{id}/mirror/usage`
- `GET  /api/family-os/families/{id}/mirror/day`

Token phải thuộc tenant có module FamilyOS. Giải thích với con trước khi bật (Manifesto / minh bạch).

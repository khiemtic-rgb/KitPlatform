# PharmaCore — Customer App Phase 2

> Chat title: **PharmaCore · Customer App — P2: CDP & Catalog**
> Base commit: `4775e4d`

PharmaCore E:\PharmaCore — tiếp Phase 2 (sau commit 4775e4d)

ĐÃ XONG (đừng sửa lại trừ khi bug):
- Customer App https://localhost:5174 (HTTPS dev)
- OTP, loyalty decimal, reminders toggle theo id (RemindersPage + reminder-normalize)
- POS loyalty redeem (max %, điểm lẻ), migrations 019–026
- run-dev.bat: API + Admin + Customer App

PHASE 2 — ƯU TIÊN:
1. ✅ API tra cứu sản phẩm cho khách (thay DEMO_REMINDER_PRODUCTS)
2. ✅ CDP consent → kiểm tra trước khi gửi nhắc/SMS (customer_consents)
3. ✅ App Push / PWA

QUY TẮC:
- Reminder: list phẳng, patch theo id, không tách section Switch
- Loyalty: decimal sau migration 026
- Restart API: .\scripts\restart-api.ps1

# Family OS — GA / đại trà checklist v1

> **Trạng thái:** Draft cổng mở đại trà. Pilot `family.kittech.vn` **không** đồng nghĩa GA.
> SoT deploy pilot: [`family-os-sync-runbook-v1.md`](./family-os-sync-runbook-v1.md).
> Product deploy production: vẫn **chưa chốt** cho đến khi checklist này được ký.

## 0. Chính sách migration

| Quyết định | Nội dung |
|------------|----------|
| **Hiện tại (pilot)** | Chỉ `deploy/ubuntu/migration-files.family-os.txt`. **Không** gộp vào `migration-files.prod.txt`. |
| **Khi GA** | Product + Ops chọn một: (A) promote có chủ đích các mig Family sang prod manifest sau review, **hoặc** (B) giữ lane Family riêng dài hạn với owner rõ. |
| **Pharmacy freeze** | Không gộp PR Family OS auth + Pharmacy auth. Không đụng `NT_XUANHOA` / DEMO_PHARMACY ngoài phạm vi Pharmacy. |

## 1. Gate bắt buộc trước mỗi ship GA

- [ ] `git status` sạch phần Family/auth đang ship; `HEAD` = `origin/main` (hoặc tag release)
- [ ] `cd client/family-app && npm run build` (tsc sạch)
- [ ] `cd client/family-app && npm run test:unit` (routine-focus selfcheck)
- [ ] API build nếu đụng `src/Packs/FamilyOS`
- [ ] Push trước khi deploy VPS
- [ ] Mig chỉ qua manifest **family-os** (trừ khi đã chốt promote GA)
- [ ] Post-mig Grant `250` / privilege check theo runbook
- [ ] Smoke: `api.novixa.vn/health`, `family.kittech.vn` hard-refresh
- [ ] Smoke **≥2 tenant không phải DEMO cục bộ**: tạo nhà → thêm 2 con → onboarding gắn routine → day-flow có việc → đổi Mode cả nhà → Routine chip từng con

## 2. Golden path tenant trống (fail nếu thiếu)

1. Unlock / tạo phiên parent  
2. Chọn / tạo family + members (ít nhất 1 child)  
3. Onboarding **phải** `applyOnboardingPlan` thành công — **không** vào Home nếu routine fail  
4. `/today` có lịch; nếu chưa có routine → màn setup (không mở Parent board trống)  
5. PIN: lần đầu bắt đổi khỏi `1234`  
6. Offline: banner “cần mạng” (v1 online-only)

## 3. Sản phẩm tối thiểu GA (v1)

| Hạng mục | Chuẩn |
|----------|--------|
| Mode / nhịp | Cả nhà |
| Lịch việc theo con | P0 filter + gán `memberId` trên Routine |
| Soft-lock / PIN | Device-local; đã đổi khỏi mặc định |
| PWA | Cài được + push; **không** cam kết offline sản phẩm |
| Billing max children | Gate theo gói (đã có) |

## 4. Không chặn pilot, chặn đại trà nếu còn mở

- [ ] Hypercare owner + kênh escalation 48h đầu GA  
- [ ] Trial / self-serve signup playbook (nếu mở)  
- [ ] Thông báo “v1 cần mạng” trong onboarding / store listing  
- [ ] Quyết định P2 override lịch lệch mùa giữa các con (không bắt buộc GA v1)

## 5. Ký mở GA

| Vai trò | Tên | Ngày | Đồng ý |
|---------|-----|------|--------|
| Product | | | [ ] |
| Ops / Deploy | | | [ ] |
| Eng lead Family | | | [ ] |

**Chỉ khi bảng trên đủ chữ ký** mới được coi là “đáp ứng triển khai đại trà”.

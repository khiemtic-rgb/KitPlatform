# Research publication publisher

Bản phát hành của publisher `research-publication/v1` là file trong thư mục này:

`tools/research-publication/publish_research_v1.py`

SHA-256, tính lại 2026-10-22, nguồn và bản đóng gói trùng nhau:

`D1F3EF3D066F70B167848FC9AA5245107B9ED7347E29C6C4F5E9CAE3AE333361`

Nguồn đối chiếu lịch sử nằm ở `E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1\publish_research_v1.py`. Thư mục đó không phải git repo và không phải bản triển khai độc lập. Sau nhiệm vụ `SB-RESEARCH-RC-002`, file trong KitPlatform là source of truth. Logic publisher không đổi so với nguồn; hai test được thêm ở `tests/test_publish_research_v1.py`.

## Chạy

Từ thư mục này:

```
python publish_research_v1.py --destination <thư mục publication> --lab <LAB_RESULT.json> --status <PAPER_OPS_STATUS.json> --report <PAPER_RESEARCH_REPORT.json> --health <PAPER_OPS_HEALTH.json>
```

`--published-at` là tùy chọn, UTC. Thiếu thì script lấy giờ UTC hiện tại. Không đưa journal, snapshot, ledger, cache, credential hay thư mục lab vào `--destination` hoặc vào Git.

Đích nhận đúng:

- `manifest.json`
- `lab/LAB_RESULT.json`
- `paper/PAPER_OPS_STATUS.json`
- `paper/PAPER_RESEARCH_REPORT.json`
- `paper/PAPER_OPS_HEALTH.json`

File thiếu hoặc JSON không hợp lệ in `PUBLISH_FAILED` và trả mã 1 trước khi đổi thư mục đích. Status bỏ mọi key có `api_key`. Report bỏ `frozen_lab_result`.

Đổi chỗ là staging rồi hai lần rename, không phải một syscall. Nếu lần rename thứ hai lỗi, script trả publication trước đó về chỗ cũ rồi ném lỗi. Không in `PUBLISH_OK`. Script xóa `.previous` sau khi thành công, nên không có rollback tự động của một publication đã publish xong. Crash giữa hai lần rename có thể làm publication biến mất cho đến lần publish thành công sau. Bản sao operator giữ riêng vẫn là cách khôi phục nội dung.

## Cập nhật sau này

1. Sửa file chuẩn trong thư mục này.
2. Chạy `python -m unittest tests.test_publish_research_v1` tại thư mục này.
3. Tính lại SHA-256.
4. Ghi hash mới và lý do diff vào `src/Packs/PersonalIntelligence/SECOND_BRAIN_RESEARCH_DEPLOY_READINESS.md`.
5. Review diff trước khi phát hành. Không copy dữ liệu nghiên cứu vào commit.

Trạng thái release nằm ở tài liệu readiness đó. File này chỉ là bản ghi đóng gói.

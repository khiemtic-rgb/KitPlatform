# KIT — hệ thống sản xuất video V1

Bộ tài liệu này mô tả công cụ dựng video kể chuyện bằng hình, chạy cục bộ, đọc thời lượng và số cảnh từ từng dự án.

Công cụ nằm ở `tools/kit-video-production`. Dự án và file xuất của lần này nằm ở `proof/kit-video-production-v1`. Công cụ không ghi vào Production OS, không sửa logo, và không ghi đè các phim đã có trong `proof/novixa-*`.

## Tài liệu

1. [Kiến trúc](architecture.md)
2. [Quy chuẩn hình](visual-style-guide.md)
3. [Schema manifest](manifest-schema.md)
4. [Schema shot plan](shot-plan-schema.md)
5. [Quản lý asset](asset-management.md)
6. [Catalog template](scene-template-catalog.md)
7. [Render và QA](render-qa-workflow.md)
8. [Tạo dự án mới](new-project-guide.md)
9. [Báo cáo pilot NOVIXA](novixa-pilot-report.md)
10. [Giới hạn](limitations.md)
11. [Style proof V2](style-proof-v2-report.md)
12. [Visual engine V2 — prototype theo reference](visual-engine-v2-report.md)
13. [Pipeline quảng cáo](commercial-pipeline.md)
14. [Character Bible](novixa-character-bible-v1.md)
15. [Asset Bible](novixa-asset-bible-v1.md)
16. [Graphics Bible](novixa-graphics-bible-v1.md)
17. [Motion Bible](novixa-motion-bible-v1.md)
18. [Cảnh kiểm chứng lấy hộp](novixa-take-box-scene-spec-v1.md)
19. [Probe nhận diện nhân vật](novixa-character-consistency-probe-v1.md)
20. [Video đầu tay — cổng dừng](novixa-first-video-gate-v1.md)
21. [Tham chiếu 8 khung nhân vật](novixa-character-reference-v1.md)
22. [Tham chiếu nhân vật V2](novixa-character-reference-v2.md)
23. [Chuẩn giọng đọc V1](voice-direction-standard-v1.md)
24. [Kế hoạch hình intro NOVIXA](novixa-intro-film-visual-production-plan-v1.md)
25. [Preview đồ họa một phần intro](novixa-intro-film-partial-graphics-preview-v1.md)
26. [Phát triển hình intro V2](novixa-intro-film-visual-development-v2.md)
27. [Bản dựng tích hợp intro V2](novixa-intro-film-integrated-v2.md)
28. [Preview hình intro V3](novixa-intro-film-visual-preview-v3.md)
29. [Chỉnh hình intro V4](novixa-intro-film-visual-refinement-v4.md)
30. [Khôi phục định hướng hình intro](novixa-intro-film-visual-direction-recovery.md)
31. [Shot nhân vật intro V1](novixa-intro-film-character-shot-v1.md)
32. [Phương án sản xuất intro V1](novixa-intro-film-production-strategy-v1.md)
33. [Cổng triển khai intro V1](novixa-intro-film-execution-gate-v1.md)

## Lệnh

Từ thư mục gốc repository:

```text
python tools/kit-video-production/kv.py validate proof/kit-video-production-v1/projects/reuse-short
python tools/kit-video-production/kv.py analyze <script.md> --project-id <id> --out <shot-plan.proposed.json>
python tools/kit-video-production/kv.py render <project-dir> --repo E:\KitPlatform --label cut
python tools/kit-video-production/kv.py qa <render-dir> --project <project-dir>
```

Bản đề xuất từ `analyze` luôn có `approvalStatus: proposed`. Lệnh `render` từ chối cảnh `proposed`. Cảnh `style_proof` chỉ dựng khi có `--allow-style-proof`.

# KIT_MKT — Quy hoạch kiến trúc tổng thể

**Trạng thái:** Đề xuất để phê duyệt. Chưa sửa code, schema, dữ liệu hay production.  
**Phạm vi:** Park Content của tenant `KIT_MKT`, schema `pack_content`, module `kit_content`.  
**Không thuộc phạm vi:** Pharmacy, Family OS, Local OS, và mọi migration ngoài `deploy/ubuntu/migration-files.content.txt`.

Bộ này là một kiến trúc đích. Các file không được đọc như những phương án thay thế cho nhau.

| File | Nội dung |
|---|---|
| [A-MASTER-ARCHITECTURE.md](./A-MASTER-ARCHITECTURE.md) | Ranh giới module, luồng nghiệp vụ, quy tắc Brand Context |
| [B-DOMAIN-MODEL.md](./B-DOMAIN-MODEL.md) | Thực thể, khóa, quan hệ, quy tắc trạng thái |
| [C-INFORMATION-ARCHITECTURE.md](./C-INFORMATION-ARCHITECTURE.md) | Menu, route, ngữ cảnh điều hướng |
| [D-CURRENT-TO-TARGET-MAPPING.md](./D-CURRENT-TO-TARGET-MAPPING.md) | Giữ, mở rộng, tạo mới, chuyển dữ liệu |
| [E-MIGRATION-ROADMAP.md](./E-MIGRATION-ROADMAP.md) | Thứ tự triển khai, kiểm thử, rollback |

Căn cứ hiện trạng: audit đọc trực tiếp `content-nav.tsx`, `router.tsx`, `ContentLayout.tsx`, `ContentServices.CreatePoolAsync`, `migrations/281`, `282`, `287`, `288`, `333`, `383`, `kit-video-famixa-adapter.ts`. Những chỗ chưa đối chiếu dữ liệu thật được ghi ở cuối file này và không bị kết luận là lỗi.

## Quyết định đã khóa

| ID | Quyết định |
|---|---|
| D1 | Brand Context là một ngữ cảnh của cả khu Content, nằm trên URL và do `ContentLayout` cung cấp. Trang con không giữ lựa chọn brand riêng. |
| D2 | Idea mới không có brand và không có topic. Bỏ nhánh tự chọn brand có mã chứa `KIT` hoặc brand active đầu tiên. |
| D3 | `content_series` / `content_episode` là tổ chức nội dung. `series_build` và `video_production` là thực thi sản xuất. Không gộp. |
| D4 | Mã series unique theo brand. Mã episode unique theo series. Migration dừng nếu phát hiện trùng, không tự đổi mã. |
| D5 | `video_project.brand_id` là quan hệ với Brand. `brand_code` chỉ là bản sao để tương thích, không dùng để lọc hay phân quyền. |
| D6 | Film Type là bảng mới thuộc Brand. `video_universe` vẫn là thế giới bên trong một project. |
| D7 | Mỗi Film Type chọn một `workflow_key`: `famixa_series`, `factory_short`, hoặc `guided_production`. |
| D8 | Không seed tên Film Type mẫu (giáo dục, quảng cáo, giới thiệu sản phẩm). Đó là ví dụ thiết kế, không phải dữ liệu. |
| D9 | Video Studio không mở công việc sản xuất khi thiếu Brand hoặc Film Type, và không tự chọn giá trị thay người dùng. |
| D10 | Ba đồng hồ riêng: trạng thái sản xuất, trạng thái biên tập của Content, trạng thái xuất bản. |
| D11 | Calendar chỉ đọc `publish_job`. Không đưa trạng thái shot hay tiến độ Famixa vào lịch. |
| D12 | Quan hệ video với Content dùng cột đã có hoặc một cột mới trên bảng thực thi. Không tạo bảng đầu ra thứ ba. |
| D13 | Phim độc lập được phép chưa có Content. Muốn lên lịch xuất bản thì phải có Content trước. |
| D14 | Bản render trong `proof/kit-video-production-v1/outputs/novixa-pain-stock/` không được nhập vào sản phẩm trong các giai đoạn của roadmap này. |
| D15 | Adapter Famixa nhận project từ Film Type đang mở. Hằng `FAMIXA` / `FAMILY_A` chỉ còn là lối tương thích cho đúng project đã seed, rồi bỏ khi project đó đã có `brand_id` và `film_type_id`. |
| D16 | Ngân sách, cấu hình AI, credential, site, channel, publish log và cổng ADMIN giữ nguyên chỗ đang đứng. Không tách service mới. |
| D17 | Route cũ tiếp tục mở được cho đến khi project Famixa đã gắn brand và Film Type. Sau mốc đó `/content/videos` chuyển sang Studio. |
| D18 | Các Idea đã nằm trên brand nhà không bị sửa `brand_id` tự động. Việc tách là một bước dữ liệu sau khi có danh sách đối soát. |

## Chưa đủ bằng chứng

Các câu dưới đây cần đối soát bằng truy vấn đọc, trước giai đoạn đụng dữ liệu. Chúng không đổi kiến trúc.

1. Có hai `content_series.code` hoặc `content_episode.code` trùng nhau hay không. Schema đang unique toàn cục (`migrations/383_pack_content_article_series.sql`), nên trùng chỉ có thể xuất hiện nếu constraint đã bị gỡ ở một môi trường. Cách xác minh: đếm nhóm trùng trước khi đổi unique.
2. Brand row nào khớp `video_project.brand_code = 'famixa'` của project seed trong `migrations/333_pack_content_kit_video_engine.sql`. Nếu không có hàng khớp, project đó để `brand_id` trống và Studio chặn, không đoán.
3. Bao nhiêu Core Idea đang nằm trên brand được `CreatePoolAsync` chọn hộ. Hàm này nằm trong `ContentServices.cs`. Danh sách đó là đầu vào của D18, không phải lý do để giữ cách gán mặc định.
4. Calendar có đang vẽ `content_episode.planned_at` như một mốc sản xuất hay không. Đọc `ContentCalendarPage.tsx` cho thấy lịch được nhóm theo lineage của mục xuất bản. Phải rà nốt nguồn `at` trước khi khóa tiêu chí kiểm thử của D11.

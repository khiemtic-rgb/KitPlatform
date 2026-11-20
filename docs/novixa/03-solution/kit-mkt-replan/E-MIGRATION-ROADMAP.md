# E. Lộ trình chuyển đổi

Chưa được thực thi. Mỗi giai đoạn chỉ bắt đầu sau khi giai đoạn trước đạt tiêu chí và sau một phê duyệt riêng để sửa code. Không gộp hai giai đoạn có schema vào cùng một lần deploy.

Manifest duy nhất khi có migration: `deploy/ubuntu/migration-files.content.txt`. Không đụng `migration-files.prod.txt`, Family OS, hay Local OS.

## Phụ thuộc

```text
G0 Đối soát đọc
  → G1 Ngữ cảnh brand trên UI, chưa đổi schema
    → G2 Idea không brand
      → G3 Film Type và khóa project
        → G4 Nối Content với đầu ra video
          → G5 Đổi menu và chuyển /content/videos
G0 → G6 Đổi unique mã, độc lập với G2–G5, nhưng không đi trước G0
G4 → mới được xét đưa proof Novixa vào một project. Việc đó nằm ngoài roadmap này (D14)
```

## G0 — Đối soát, chỉ đọc

Chạy ba nhóm truy vấn trong [D](./D-CURRENT-TO-TARGET-MAPPING.md) trên database sẽ được migrate. Ghi số hàng, không sửa.

Thêm một việc đọc code: liệt kê mọi chỗ `ContentCalendarPage` lấy trường thời gian. Nếu có nguồn khác `publish_job.publish_at`, ghi vào tiêu chí của G5, vẫn không gọi đó là lỗi dữ liệu.

Cổng qua: có biên bản số liệu. Trùng mã thì G6 bị khóa. Không có brand khớp `famixa` thì G3 không bịa brand, và D17 giữ `/content/videos`.

## G1 — Brand Context, chưa schema

Sửa UI: `ContentLayout` đọc `?brand=`. Các trang bỏ state brand riêng và đọc context. `brand=all` khóa nút tạo. Idea không nhận brand của context. Session trình duyệt chỉ để mở lại lần sau.

Tiêu chí:

- Đổi brand ở Topics thấy cùng brand ở Series, Packages, Calendar, Videos.
- Tạo Idea khi đang chọn Novixa vẫn không ghi `brand_id` của Novixa. Ở giai đoạn này cột vẫn NOT NULL, nên nút tạo Idea mới bị tắt cho đến G2 nếu không còn đường ghi hợp lệ. Không được bật lại brand nhà để nút hoạt động.
- Route cũ mở được.
- Đăng nhập không phải ADMIN vẫn bị `ContentLayout` đẩy ra, như hiện tại.

Rollback: hoàn UI, vì chưa có migration.

## G2 — Idea không gắn brand mặc định

Migration additive trên `content_package`:

- `brand_id` và `topic_id` thành nullable.
- Check: không brand thì không topic và không `source_package_id`; có brand thì có topic.
- Không `UPDATE` hàng cũ.

Sửa `CreatePoolAsync`: bỏ khối chọn brand nhà. Insert Idea với hai khóa NULL.

Tiêu chí:

- Idea mới có `brand_id` NULL và không có hàng `topic`.
- Áp brand fit vẫn tạo Angle có brand và topic.
- Series vẫn không tạo được từ Idea chưa có Angle.
- Package và topic cũ còn nguyên brand.

Rollback: chỉ làm được khi chưa có Idea NULL. Migration xuống trả lại `NOT NULL` trong điều kiện đó. Nếu đã có Idea NULL, xoá các Idea đó bằng thao tác nghiệp vụ trước, rồi mới siết constraint. Không xoá Angle hay topic.

## G3 — Film Type và project

Tạo `film_type`. Thêm `video_project.brand_id` và `film_type_id`, cả hai nullable. Không seed tên phim mẫu. Không tạo brand.

Backfill duy nhất: một brand khớp đúng `famixa` thì gán `brand_id` cho project `FAMIXA`. Nhiều hơn một hoặc không có thì bỏ qua và ghi danh sách.

Màn `/content/settings/film-types` tạo và lưu Film Type. Gắn `film_type_id` cho project là thao tác trên màn đó, không phải trong migration.

Adapter nhận project từ Film Type khi đã có id. Khi chưa có, project seed vẫn mở bằng hằng số cũ.

Tiêu chí:

- Hai Film Type của cùng brand không đọc chung universe.
- Project có Film Type của brand khác bị API từ chối.
- `/content/videos` vẫn vào được tab Famixa hiện tại.
- `video_universe` không xuất hiện như mục cấu hình kiểu phim.

Rollback: bảng mới và hai cột nullable có thể bỏ nếu chưa có hàng Film Type do người dùng tạo. Nếu đã có, gỡ khóa trên project trước, rồi mới drop. Không đụng `series_build`.

## G4 — Content và đầu ra

Thêm `topic.content_format` mặc định `article`. Thêm `video_production.content_topic_id` và `selected_for_publish`, cùng unique một phần theo topic.

Nút trên topic format `video` mở Studio. Studio từ chối tạo việc khi thiếu brand hoặc Film Type. Phim không topic vẫn tạo được và không sinh `publish_job`.

`FINAL` không đổi `topic.status` và không ghi `publish_at`.

Tiêu chí:

- Topic cũ có format `article`.
- Một topic không có hai production cùng được chọn để xuất bản.
- Factory job mới mang `brand_id` của ngữ cảnh và `topic_id` của Content nguồn.
- Job factory không bị nhân thành một hàng `video_production`.

Rollback: cột mới nullable hoặc có mặc định. Gỡ unique một phần trước khi drop cột. Không xoá `video_job`.

## G5 — Điều hướng

Đổi `content-nav.tsx` theo [C](./C-INFORMATION-ARCHITECTURE.md). Alias route cũ còn hoạt động. Cập nhật smoke đang kỳ vọng `CONTENT_NAV_OUTSIDE`.

`/content/videos` chỉ redirect khi project Famixa đã có cả `brand_id` và `film_type_id`. Chưa đủ thì trang ba tab hiện tại vẫn là lối làm việc.

Calendar: bỏ mọi nguồn thời gian không phải `publish_job` khỏi lưới lịch. Nếu G0 thấy `planned_at` đang được vẽ, chuyển nó sang nhãn kế hoạch nội dung hoặc bỏ khỏi lưới, không đưa vào ngày đăng.

Tiêu chí:

- Sidebar còn năm cửa cộng thanh brand.
- Vào Studio khi URL đã có brand và film thì không có bước hỏi lại.
- Vào Studio khi thiếu một trong hai thì dừng ở cổng.
- Đổi brand không giữ film của brand trước.
- Một lịch đăng vẫn trỏ đúng topic và brand như trước G5.

Rollback: trả nav và route alias. Không có migration ở giai đoạn này.

## G6 — Unique mã, sau khi G0 sạch

Đổi `uq_content_series_code` thành unique `(brand_id, code)`. Đổi unique `content_episode.code` thành unique `(series_id, code)`. Giữ unique `(series_id, episode_no)`.

Nếu câu đếm trùng của G0 có hàng, giai đoạn này không chạy. Người vận hành đặt lại mã bằng tay, chạy lại câu đếm, rồi mới đổi index. Migration không tự thêm hậu tố.

Rollback: tạo lại unique toàn cục chỉ khi không có hai mã trùng trong cùng phạm vi mới. Nếu đã có hai series cùng mã ở hai brand, không siết lại unique toàn cục.

## Kiểm thử hồi quy chung

Chạy ở G1, G2, G4 và G5:

- Tạo Content độc lập, không series, vẫn duyệt và lên lịch được.
- Tạo series từ Angle, episode chưa có topic, rồi gắn topic sau.
- Hai brand không thấy series, package, job của nhau khi không ở `brand=all`.
- `brand=all` xem được danh sách và không tạo được bản ghi.
- Famixa mở được project cũ trước khi có Film Type, và không mở nhầm asset của project khác sau khi có Film Type.
- Topic `Approved` mà production chưa `FINAL` vẫn không tự có ngày đăng.
- Production `FINAL` mà topic chưa `Approved` không có mặt trên Calendar.
- User không phải ADMIN không ở lại `/content`.

## Rủi ro

| Rủi ro | Cách giữ |
|---|---|
| Idea cũ bị hiểu nhầm là của brand nhà | D18, không sửa hàng; biên bản G0 |
| Mất lối dựng Famixa giữa chừng | D17, `/content/videos` ở lại đến khi đủ khóa |
| Trùng mã khi bỏ unique toàn cục | G6 fail closed |
| Gán nhầm brand cho project `FAMIXA` | Chỉ gán khi đúng một brand khớp; không insert brand |
| Lịch biến thành bảng tiến độ | D11, một nguồn `publish_job` |
| Hai graph series bị nối khoá | Không thêm FK giữa `content_series` và `series_build` |
| Proof Novixa bị đưa vào Famixa | D14, không có bước import |

## Việc cần có trước khi viết code

Không còn lựa chọn kiến trúc mở. Trước G1 chỉ cần một phê duyệt để bắt đầu đúng giai đoạn G1, sau khi G0 được phép chạy dưới dạng truy vấn đọc.

Chưa phê duyệt thì không sửa `content-nav.tsx`, không thêm migration, và không đổi `CreatePoolAsync`.

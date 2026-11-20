# Agent đăng bài đủ thông tin như đăng tay (YouTube, Fanpage, TikTok)

Chủ dự án đã duyệt: build, test, commit các file dưới đây, deploy lên VPS B, khởi động lại kit-famixa-api.
KHÔNG commit `src/KitPlatform.Api/appsettings.Development.json`. Không in hay commit giá trị bí mật.

## Thay đổi

### Backend
- `Application/ContentPublishExtras.cs` (mới), `Infrastructure/ContentPublishExtrasService.cs` (mới), DI trong `ContentPackDependencyInjection.cs`.
- `ContentRepository.MergeTopicExtraAsync` (gộp khóa vào `topic.extra_json`).
- API `POST api/content/topics/{topicId}/publish-extras` (multipart: `thumbnail?`, `cover?`, `youtubePlaylist?`, `coverTimestampMs?`) trong `ContentYoutubeController.cs`; relay cho phép đường này (`ContentRemoteRelayController.cs`).
  - Ảnh lưu thành asset `kind=image`, meta `role=thumbnail|cover`, tối đa 2 MB, JPG/PNG. Còn lại lưu `topic.extra_json.publish`.
- `ContentPublishService.cs`:
  - YouTube: `defaultLanguage`/`defaultAudioLanguage` (mặc định `vi`, cấu hình kênh `youtube.language`), `categoryId` theo `youtube.categoryId` (mặc định 27), embeddable, publicStatsViewable. Shorts dùng ảnh bìa dọc (`cover`) nếu có. Danh sách phát theo tên (`publish.youtubePlaylist` hoặc `youtube.playlist`): tìm đúng tên trên kênh, chưa có thì tạo công khai. Lỗi danh sách phát chỉ là cảnh báo.
  - Fanpage: video đăng kèm `title`; video thường gửi `thumb` (ảnh bìa ngang); Reel đặt ảnh bìa qua `POST /{video-id}/thumbnails` (best effort). Bản viết fb_page tách "Bình luận đầu" ra khỏi thân bài (trước đây dòng đánh dấu bị đăng nguyên văn) và đăng thành bình luận đầu sau khi bài lên. Link CTA chuyển xuống bình luận đầu nếu có.
  - Video có fb_page do bước 5 duyệt (meta `manual_import`) thì dùng nội dung Fanpage đó, không dùng caption TikTok.
  - TikTok: đăng thẳng kèm caption (Direct Post) khi bật `Content:TiktokDirectPost=true` (hoặc env `TIKTOK_DIRECT_POST=true`) và kênh không đặt `tiktok.mode=inbox`. Hỏi creator_info, chọn quyền xem (`tiktok.privacy`, mặc định PUBLIC_TO_EVERYONE nếu được phép), nhãn nội dung AI (`is_aigc`, mặc định bật), khung bìa `coverTimestampMs`. TikTok từ chối (thiếu `video.publish`, ứng dụng chưa duyệt, video dài quá) thì tự gửi hộp nháp như cũ, ghi lý do vào kết quả job.
- `ContentYoutubeClient.cs`: `BuildUploadMetadata` thêm tham số tùy chọn `categoryId`, `language`; thêm `FindPlaylistByTitleAsync`. Không đổi `IContentYoutubeApi`.
- `ContentTiktokClient.cs`: `DirectScopes`, `QueryCreatorInfoAsync`, `DirectPostAsync`, `ContentTiktokScopeException`; auth URL xin `video.publish` chỉ khi bật đăng thẳng.
- `ContentFacebookConfig.cs`: thêm quyền `pages_manage_engagement` vào Scopes (để đăng bình luận đầu dưới bài Trang; ứng dụng Meta đã thêm quyền này).
- `ContentFacebookCaption.cs`: `Split()` + ưu tiên fb_page do bước 5 duyệt. Test thêm trong `tests/KitPlatform.Content.Tests/ContentFacebookCaptionTests.cs`.

### Admin (Video Studio bước 5)
- `novixa-cover.ts` (mới): tạo ảnh bìa trong trình duyệt từ khung hình video + chữ tiêu đề (ngang 1280x720, dọc 1080x1920, JPG dưới 2 MB); dùng ảnh có sẵn.
- `NovixaExportStep.tsx`: mỗi video có ô ảnh bìa (giây lấy khung, chèn tiêu đề, tạo lại, dùng ảnh có sẵn); ô "Danh sách phát YouTube" (nhớ theo thương hiệu). Khi duyệt: sau khi gửi video, gửi ảnh bìa + danh sách phát + khung bìa TikTok (`publish-extras`), lỗi bước này không chặn xếp lịch. Bản ghi `extras` để bấm lại không gửi trùng.
- `content-youtube.api.ts`, `content-remote.api.ts`, `film-production-orchestrator.ts` (thêm `extras`).

## Việc cần làm
1. `dotnet build` + `dotnet test tests/KitPlatform.Content.Tests` (đặc biệt ContentFacebookCaptionTests, ContentYoutubeMetadataTests, ContentYoutubeExtrasTests). `npm run build` (hoặc `tsc --noEmit`) trong `client/admin`. Sửa lỗi biên dịch nếu có, giữ đúng hành vi mô tả trên.
2. Commit (chỉ các file trên + handoff này), push, deploy VPS B như các lần trước, restart kit-famixa-api. Báo kết quả `systemctl status` và log khởi động.
3. KHÔNG bật `TIKTOK_DIRECT_POST` trên B lúc này. Chỉ bật khi chủ dự án xác nhận ứng dụng TikTok đã bật Direct Post (scope `video.publish`) trong cổng nhà phát triển; sau khi bật phải kết nối lại kênh TikTok để lấy quyền mới.
4. Kiểm tra nhanh trên B (thương hiệu đang tạm dừng đăng nên không đăng thật): gọi `publish-extras` cho một bài thử với ảnh JPG nhỏ, xác nhận asset role thumbnail được tạo và `extra_json.publish` có dữ liệu. Xóa bài thử sau đó.

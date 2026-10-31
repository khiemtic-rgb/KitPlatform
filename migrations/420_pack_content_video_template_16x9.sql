-- KitPlatform 420: template video ngang 16:9 cho video dài YouTube
-- Manifest: deploy/ubuntu/migration-files.content.txt
-- "Video thành phẩm đã nhập" kiểm kích thước theo template. Trước đây chỉ có 9:16 và 1:1,
-- nên video dài 1920x1080 bị từ chối. Thêm template 16:9; client tự chọn template theo kích thước file.

INSERT INTO pack_content.video_template (
    code, name, provider, aspect_ratio, duration_sec, description, config_json, sort_order
)
SELECT 'youtube_long_16x9',
       'YouTube dài 16:9',
       'storyboard_local',
       '16:9',
       600,
       'Video dài ngang 1920x1080 dựng ngoài hệ thống, nhập qua Video thành phẩm đã nhập.',
       '{"platform":"youtube","format":"long"}'::jsonb,
       40
WHERE NOT EXISTS (
    SELECT 1 FROM pack_content.video_template t WHERE t.code = 'youtube_long_16x9'
);

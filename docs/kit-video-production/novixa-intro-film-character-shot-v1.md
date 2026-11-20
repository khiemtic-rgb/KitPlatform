# NOVIXA Intro — shot nhân vật V1

Không có shot mẫu.

`finalVideo` vẫn null. `durationLocked` vẫn false. V3, V4 và bản khôi phục định hướng giữ nguyên. Kịch bản và 12 WAV V3 không đổi.

Bản `20261023T131329Z-visual-direction-recovery` chỉ là bằng chứng phong cách. Lần này không nhân bản tấm đó thành 12 cảnh.

## Trạng thái chuẩn

`visual-standard-v1.json` và Character Bible vẫn `AWAITING_REVIEW`. Manifest quảng cáo vẫn để hai asset ở `proposed`. Ngày 2026-10-24, `executionAuthorizationV1` của Intro Film ghi hai file này là đã duyệt cho riêng phim intro:

- `assets/pharmacist-nova-cartoon-v3.jpg`: một góc gần chính diện.
- `keyframes/v3/s01-pharmacy.jpg`: cùng nhân vật, cùng góc, đứng sau quầy, 1280×720.

Vẫn chưa duyệt, kể cả cho intro:

- `probes/character-reference-v1/f07-lift.jpg` và `f08-point.jpg`: ảnh tĩnh khoảng 249×279 pixel, chờ duyệt.

Trong chính file chuẩn, bốn tư thế đưa tay, nắm, nhấc và chỉ vẫn là `spec-only`. Character reference V2 chưa tạo được bốn khung cần sửa. Không có mesh, file Blender, GLB hay FBX của dược sĩ hay nhà thuốc.

Logo `docs/novixa/assets/brand/novixa-logo.png` là file đã duyệt. Không đụng DOC-008 và không dùng nhân vật Famixa.

## Pipeline đã kiểm trên máy này

Có FFmpeg. Pillow vẽ được chữ và đồ họa. `ffmpeg minterpolate` chỉ nội suy khung của một video đã có. Nó không tạo được bước đi, với tay hay tương tác từ một ảnh bán thân.

Không có Blender, Houdini, PyTorch, Diffusers, renderer OpenGL, ComfyUI, và không có model 3D trong repo.

Đường image-to-video trong `kit_video` chỉ lập job cho Runway, Veo hoặc Fal. Cả ba cần khóa trả phí. `RUNWAY_API_KEY`, `VEO_JOIN_FILE` và `FAL_KEY` đều trống. Video tham chiếu `docs/kit-video-production/references/novixa-gemini-reference.mp4` không có. Lệnh `produce` vẫn từ chối keyframe chưa `APPROVED`. Không có request nào được gửi.

## Vì sao chưa có shot

Một shot đạt yêu cầu phải cho thấy dược sĩ 3D đổi tư thế trong nhà thuốc 3D, đúng lời một cảnh, và đủ nét ở 1280×720. Hai ảnh đã duyệt cho intro vẫn là ảnh tĩnh một góc. Dựng chuyển động từ ảnh đó khi chưa có tư thế tay và chưa có phép gọi provider sẽ không phải shot sản xuất.

Phóng, lia, ghép hai ảnh, hay nội suy giữa tấm quầy và tấm cầm hộp không phải hành động nhân vật. Những cách đó không được dùng để thay shot mẫu.

## Phần không bị chặn

Logo và chuyển động đồ họa S11–S12 đã có ở các bản trước. Không dựng lại, vì một thẻ logo mới không trả lời câu hỏi shot nhân vật.

## Cách mở khóa

1. Tạo hình cartoon 3D và tấm quầy v3 đã được duyệt cho intro. Duyệt ảnh không tự sinh ra chuyển động.
2. Chọn một đường tạo khung hình mới của đúng nhân vật đó. Hai đường đang có trong repo đều chưa chạy được: render từ mesh cục bộ, hoặc image-to-video sau khi có khóa và có phép dùng provider. Máy này chưa có mesh và chưa có khóa.
3. Shot đầu nên là một hành động ngắn đã có trong lời S01 hoặc S06: với tay lấy một hộp không chữ liều, rồi đưa hộp về phía quầy. Cùng mặt, cùng áo, cùng ánh sáng với tấm đã được duyệt. Không có khách cho đến khi có nhân vật khách cùng ngôn ngữ.
4. Chỉ sau khi shot đó được xem và chấp nhận hướng sản xuất mới dựng tiếp S01–S02 và S09–S10. S05–S08 gắn đồ họa minh họa vào shot đó, không đặt thẻ lên một ảnh đứng.

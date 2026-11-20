# NOVIXA Intro — cổng triển khai V1

Dừng trước thử nghiệm 5 giây vì thiếu khóa. Không có clip. Chưa gọi Runway. Không dựng đoạn ba nhịp và không dựng 12 cảnh.

`finalVideo` vẫn null. `durationLocked` vẫn false. Kịch bản và 12 WAV V3 không đổi.

## Phê duyệt đã có

Người phụ trách cho phép triển khai theo `novixa-intro-film-production-strategy-v1.md`. Phạm vi đó gồm thử nghiệm một hành động khoảng 5 giây, rồi mới xét đoạn ba nhịp.

## Cổng đang chặn

Phê duyệt triển khai không duyệt chi phí. Ngày 2026-10-24 người phụ trách duyệt hai file sau cho riêng NOVIXA Intro Film. `executionAuthorizationV1.characterApprovedForIntro` là true và `characterApproval` là `APPROVED`. Phê duyệt này không đổi `visual-standard-v1.json` và không đổi approval của dự án quảng cáo.

- `projects/novixa-pharmacy-commercial/assets/pharmacist-nova-cartoon-v3.jpg`
- `projects/novixa-pharmacy-commercial/keyframes/v3/s01-pharmacy.jpg`

`visual-standard-v1.json` vẫn `approval: AWAITING_REVIEW`. Hai asset tương ứng trong manifest quảng cáo vẫn `proposed`.

`RUNWAY_API_KEY` vẫn trống trên process, user và machine. Không có request nào được gửi. Đơn giá Runway Dev ngày 2026-10-24: 1 credit = $0.01. `gen4_turbo` là 5 credit mỗi giây, không có phụ phí độ phân giải. Một clip 5 giây, 1280:720, một ảnh đầu, là 25 credit, tức $0.25. `gen4.5` là 12 credit mỗi giây và không được chọn. Provider khác không được gọi.

## Chưa làm

Không tạo video mới. Không sửa WAV, kịch bản, logo, hay hai ảnh nhân vật. Không dùng DOC-008, không dùng nhân vật Famixa, không nội suy ảnh tĩnh.

## Để mở cổng

1. Hai file ở trên đã được duyệt cho intro. Duyệt đó không mở các ảnh khác và không mở dự án quảng cáo.
2. Phép gọi và đơn giá của một clip `gen4_turbo` 5 giây đã có. Request chỉ gửi sau khi `RUNWAY_API_KEY` có trong môi trường người dùng, ngoài repo. Không dán khóa vào chat, source, manifest hay log.

Đoạn ba nhịp chỉ bắt đầu sau khi clip 5 giây được xem và đạt: cùng mặt, cùng áo, cùng ánh sáng, và hành động với tay đọc được.

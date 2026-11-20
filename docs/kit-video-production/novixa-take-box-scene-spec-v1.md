# Cảnh kiểm chứng — lấy hộp AMPICILLIN

Mã `s02-take-box-probe`. Trạng thái `SPEC_ONLY`. Chưa có keyframe đầu, keyframe cuối, và chưa có video.

## Việc cảnh phải làm

Dược sĩ nhìn kệ thuốc kê đơn, đưa tay tới hộp AMPICILLIN, nắm, nhấc khỏi kệ, xoay mặt tên về phía máy và chỉ vào tên. Cuối cảnh nhìn khách và cười. Hành động lấy từ Motion Bible, mục 2 đến 7.

Nhân vật, biển kệ và mặt hộp lấy từ Character Bible, Asset Bible và Graphics Bible. Tên hộp là AMPICILLIN. Không thêm liều dùng.

## Vì sao chưa có hình

Tấm nhân vật hiện có là một góc đứng sau quầy, tay không cầm hộp. Ghép một mặt hộp lên tấm đó không tạo được nắm tay hay hướng chỉ. Dịch chuyển cả tấm cũng không phải tương tác.

Nhiệm vụ này không gọi dịch vụ tạo ảnh hay video. Khi có duyệt hình và có phép dùng provider, cảnh này mới được gửi một lần, từ keyframe đầu và cuối đã có tay thật trong hình.

## Blocker

- Chưa có tư thế `pose-reach`, `pose-grasp`, `pose-lift`, `pose-point`.
- Keyframe v7 chưa được người duyệt.
- Chưa có credential và chưa có phép gửi request.

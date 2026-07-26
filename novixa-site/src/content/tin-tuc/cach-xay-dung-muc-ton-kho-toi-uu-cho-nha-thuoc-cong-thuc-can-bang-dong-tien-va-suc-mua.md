---
title: 'Cách xây dựng mức tồn kho tối ưu cho nhà thuốc: Công thức cân bằng dòng tiền và sức mua'
description: ''
category: quan-tri-nha-thuoc
subcategory: doanh-thu
image: /images/tin-tuc/cach-xay-dung-muc-ton-kho-toi-uu.png
pubDate: 2026-08-21
lang: vi
---

"Đặt nhiều hàng thì đọng vốn, đặt ít hàng thì đứt gãy doanh thu."

Đây là bài toán hóc húa tốn nhiều sức lực nhất của các chủ nhà thuốc. Nếu cài đặt mức tồn kho quá cao, bạn sẽ đối mặt với rủi ro đọng vốn, tăng chi phí lưu kho, và nguy cơ hàng biến thành "tồn kho chết" hoặc cận hạn (FEFO). Ngược lại, nếu siết tồn kho quá mức, nhà thuốc sẽ thường xuyên rơi vào cảnh hết hàng (Out-of-Stock), khiến dược sĩ phải lắc đầu từ chối khách, đẩy người bệnh sang nhà thuốc đối thủ và đánh mất uy tín điểm bán.

Xây dựng **mức tồn kho tối ưu** không phải là việc đoán mò hay dựa vào cảm tính "hình như dạo này bán chạy". Đó là một bài toán khoa học vận hành dựa trên **Dữ liệu bán hàng thực tế (POS), Thời gian cung ứng của nhà phân phối (Lead Time) và Tần suất xoay vòng kho.**

## Phân tích: 3 chỉ số cốt lõi để tính toán mức tồn kho tối ưu

Để xây dựng được hạn mức tồn kho chuẩn xác cho từng mã thuốc, bạn cần xác định rõ 3 biến số đầu vào:

1. **Mức tiêu thụ trung bình ngày (Average Daily Sales - ADS):** Số lượng đơn vị sản phẩm bán ra trung bình mỗi ngày (tính trên dữ liệu 30–90 ngày gần nhất trên phần mềm POS).
2. **Thời gian giao hàng của Nhà cung cấp (Lead Time - LT):** Số ngày tính từ lúc nhà thuốc phát đơn đặt hàng cho đến khi hàng được giao tới kho và sẵn sàng bán.
3. **Mức tồn kho an toàn (Safety Stock - SS):** Lượng hàng dự phòng để bù đắp cho những biến động đột biến (khách mua tăng vọt hoặc nhà cung cấp giao hàng chậm hơn dự kiến).

## Công thức thực chiến: Mô hình Tồn kho Min - Max

Mô hình Min - Max là phương pháp chuẩn hóa hiệu quả nhất giúp tự động hóa việc đặt hàng tại nhà thuốc. Hệ thống sẽ tự động phát cảnh báo nhập hàng khi kho chạm mốc Min, và gợi ý số lượng cần đặt để không vượt quá mốc Max.

```plain
[Mức Tồn Tối Thiểu (Min)] = (ADS x Lead Time) + Safety Stock
[Mức Tồn Tối Đa (Max)]   = Min + (ADS x Chu kỳ đặt hàng)
[Số Lượng Cần Đặt Bù]     = Max - Tồn Kho Thực Tế

```

### Ví dụ minh họa thực tế:

Nhà thuốc của bạn bán sản phẩm **Panadol Extra**:

- **ADS:** Bán trung bình **10 hộp/ngày**.
- **Lead Time:** Trình dược viên giao hàng trong **2 ngày**.
- **Chu kỳ đặt hàng:** Nhà thuốc chốt đơn **7 ngày/lần**.
- **Safety Stock:** Dự phòng bán trong **3 ngày** = $10 \times 3 = 30$ hộp.

**Áp dụng công thức:**

- **Ngưỡng Min:** $(10 \times 2) + 30 = 50$ hộp.
_(Khi tồn kho xuống còn 50 hộp, hệ thống lập tức báo cần đặt hàng ngay)._
- **Ngưỡng Max:** $50 + (10 \times 7) = 120$ hộp.
_(Số lượng tồn kho tối đa không bao giờ nên vượt quá 120 hộp để tránh đọng vốn)._
- **Thực tế đặt hàng:** Khi kho còn 40 hộp, số lượng cần đặt bù là: $120 - 40 = 80$ hộp.

## 4 Bước triển khai xây dựng hạn mức tồn kho tối ưu tại quầy

### Bước 1: Phân loại danh mục sản phẩm theo Mô hình ABC

Không phải mã thuốc nào cũng áp dụng chung một công thức. Hãy trích xuất dữ liệu POS để chia kho hàng thành 3 nhóm:

- **Nhóm A (20% số mã - 70% doanh thu):** Các thuốc chủ lực, thuốc điều trị mãn tính. _Chiến lược:_ Giữ Safety Stock cao hơn một chút để tuyệt đối không đứt hàng.
- **Nhóm B (30% số mã - 20% doanh thu):** Thuốc thông thường, TPCN phổ biến. _Chiến lược:_ Áp dụng công thức Min - Max tiêu chuẩn.
- **Nhóm C (50% số mã - 10% doanh thu):** Thuốc bán chậm, hàng đặc trị hiếm. _Chiến lược:_ Siết chặt Safety Stock về mức tối thiểu, đặt hàng theo đơn thực tế.

### Bước 2: Chuẩn hóa dữ liệu Lead Time của từng Nhà cung cấp

Lập bảng theo dõi thời gian giao hàng thực tế của các đối tác: Nhà phân phối lớn (giao trong 24h), công ty dược tỉnh (giao 2–3 ngày), hàng nhập khẩu (giao 5–7 ngày). Dữ liệu Lead Time càng chuẩn, chỉ số Min calculated càng chính xác.

### Bước 3: Cài đặt hạn mức Min - Max vào Phần mềm POS

Đưa toàn bộ thông số Min - Max đã tính toán vào phần mềm quản lý nhà thuốc. Thiết lập cơ chế tự động: Khi kiểm kho ca trực hoặc sau mỗi giao dịch bán hàng, nếu mã thuốc chạm ngưỡng Min, phần mềm sẽ tự động xuất **Danh mục gợi ý đặt hàng (Reorder List)**.

### Bước 4: Điều chỉnh định mức theo Mùa và Biến động thị trường

Định mức Min - Max không phải là con số cố định vĩnh viễn. Chủ nhà thuốc cần điều chỉnh định mức theo mùa y khoa:

- _Mùa đông/Mùa mưa:_ Tăng Min - Max nhóm thuốc hô hấp, cảm cúm, siro ho.
- _Mùa hè:_ Tăng Min - Max nhóm thuốc tiêu hóa, men vi sinh, xịt chống muỗi.

## Checklist: 10 câu hỏi tự đánh giá Năng lực quản trị tồn kho tối ưu

1. [ ] Bạn có biết chính xác Mức tiêu thụ trung bình ngày (ADS) của Top 20 sản phẩm bán chạy nhất không?
2. [ ] Phần mềm POS của bạn có tính năng tự động gợi ý đặt hàng khi kho chạm ngưỡng Min không?
3. [ ] Bạn có danh sách phân loại danh mục sản phẩm ABC dựa trên dữ liệu doanh thu thực tế không?
4. [ ] Thời gian giao hàng (Lead Time) của từng nhà cung cấp có được ghi nhận và theo dõi cụ thể không?
5. [ ] Bạn có cài đặt mức Tồn kho an toàn (Safety Stock) riêng cho nhóm thuốc điều trị mãn tính không?
6. [ ] Định mức Min - Max tại nhà thuốc có được rà soát và điều chỉnh lại theo mùa (3–6 tháng/lần) không?
7. [ ] Dược sĩ tại quầy có thói quen đặt hàng dựa trên danh mục gợi ý POS thay vì gọi hàng theo cảm tính không?
8. [ ] Tỷ lệ đứt hàng (Out-of-Stock) đối với các mặt hàng nhóm A có được kiểm soát dưới 1% không?
9. [ ] Việc đảo hàng FEFO có được kết hợp đồng bộ mỗi khi nhập hàng bù theo định mức Min - Max không?
10. [ ] Bạn có đo lường được Tốc độ quay vòng kho (Inventory Turnover) hàng tháng không?

## Lời kết

Xây dựng mức tồn kho tối ưu là chiếc cầu nối bền vững giữa việc tối ưu hóa dòng tiền và nâng cao chất lượng dịch vụ khách hàng. Khi bạn làm chủ được công thức Min - Max, phân loại kho ABC bài bản và vận hành tự động trên dữ liệu POS, nhà thuốc sẽ loại bỏ hoàn toàn cảnh đọng vốn âm thầm hay thất thoát khách hàng, tạo nền tảng vững chắc để bứt phá doanh thu.

> **Novixa** đồng hành cùng các nhà thuốc chuẩn hóa quy trình vận hành SOP, tự động hóa quản trị kho FEFO, tối ưu định mức Min-Max và kiểm soát dòng tiền thời gian thực, giúp bạn giải phóng sức lao động và tối ưu hiệu quả kinh doanh.

# NOVIXA video POC 001 — kịch bản 60 giây

Tình huống kể, không phải số liệu của một khách hàng thật.
Cách đọc thương hiệu: «Nô vi xa». Chữ trên hình vẫn là NOVIXA.

## Lời thoại

1. Nhà thuốc bán được ba trăm triệu mỗi tháng.
2. Đơn ra đều. Tiền vào đều.
3. Cuối tháng, vẫn không biết mình thực sự còn lại bao nhiêu.
4. Doanh thu chỉ cho biết khách đã trả bao nhiêu.
5. Nó không cho biết hàng nào đang nằm trong kho,
6. Hàng nằm đó là tiền đang bị giữ lại.
7. lô nào sắp hết hạn,
8. Lô cận hạn là hàng có thể không bán được nữa.
9. hay ca bán có lệch quỹ hay không.
10. Lệch quỹ là tiền trong ca không khớp với sổ.
11. Muốn hết thấp thỏm, phải nhìn cùng một lúc:
12. tiền bán ra, giá trị hàng đang giữ, và rủi ro ngay trên quầy.
13. Không cần một con số lãi được hứa trước. Cần thấy các số này cạnh nhau.
14. Nô vi xa để màn hình tổng quan hiện doanh thu hôm nay và hàng sắp hết hạn.
15. Báo cáo tồn kho tính giá trị hàng đang giữ theo giá vốn từng lô.
16. Lệch quỹ xem ở báo cáo ca làm việc.
17. Đó là những màn hình đang có, không phải một lời hứa về lợi nhuận.
18. Ba trăm triệu là doanh thu.
19. Phần còn lại, hãy mở số liệu ra mà xem.

## Vai trò từng câu

| Câu | Vai trò |
|---|---|
| 1 | Mở bằng một mức doanh thu dễ hình dung. Đây là tình huống, không phải kết quả khách hàng. |
| 2 | Cho thấy bề ngoài việc bán hàng đang chạy. |
| 3 | Đặt nghịch lý: tiền vào đều vẫn không biết phần còn lại. |
| 4 | Định nghĩa doanh thu bằng việc nó thực sự đo. |
| 5, 7, 9 | Ba khoảng trống: hàng trong kho, lô sắp hết hạn, lệch quỹ ca bán. |
| 6, 8, 10 | Hệ quả của từng khoảng trống, không phải số liệu khách hàng. |
| 11–12 | Chuyển từ lo lắng sang việc cần nhìn cùng lúc ba nhóm số. |
| 13 | Chặn cách hiểu sai: không cần một con số lãi được hứa. |
| 14 | Chỉ đúng màn hình Tổng quan: doanh thu hôm nay và hàng sắp hết hạn. |
| 15 | Chỉ đúng báo cáo tồn kho: số lượng nhân giá vốn từng lô. |
| 16 | Chỉ đúng báo cáo ca làm việc. |
| 17 | Khóa lại: đây là màn hình đang có, không phải lời hứa lợi nhuận. |
| 18 | Nhắc lại con số mở đầu và gọi đúng tên nó là doanh thu. |
| 19 | Kêu gọi mở số liệu. Không hẹn mức lãi, mức tiết kiệm hay hiệu quả chưa chứng minh. |

## Chỉ dẫn diễn xuất

Giọng nam `vi-VN-NamMinhNeural`. Pipeline video không có TTS miễn phí; bản thử dùng Edge Read Aloud, không có khóa và không có tài khoản tính phí. Giọng này không có style buồn/nghiêm. Diễn xuất làm được bằng tốc độ, cao độ và khoảng nghỉ.

Thông số đã áp khi tạo tiếng nằm trong `speak.py`, biến `LINES`. Tóm tắt: câu mở chậm hơn (−8% đến −12%, thấp hơn 1–2 Hz); phần vấn đề gần tốc độ nói thường; phần sản phẩm ở tốc độ thường; câu kết trở lại nhịp chậm. Giọng không có style cảm xúc. Khoảng nghỉ giữa câu được cắt phần im ở đầu và cuối file TTS, rồi chèn nghỉ khoảng 0,14–0,48 giây. Đầu bản nghỉ 0,36 giây.

Đầu bản có 0,36 giây nghỉ để tiếng đầu không bị cắt.

## Tự kiểm trước khi tạo hình và tiếng

- Mỗi câu có một việc: nêu tình huống, khoét khoảng trống, hoặc chỉ một màn hình đã có trong sản phẩm.
- Không có câu «giải pháp toàn diện», «tối ưu», «bứt phá», «hiệu quả đã được chứng minh».
- «Ba trăm triệu» là tình huống của kịch bản, không được đọc như doanh thu của một nhà thuốc có tên.
- Không nói NOVIXA tính được lợi nhuận. Sản phẩm có doanh thu, giá trị tồn theo giá vốn, hạn dùng và báo cáo ca; không có màn hình lợi nhuận trong bản dịch tiếng Việt đã kiểm.
- Câu 14 bám nhãn Tổng quan: «Doanh thu hôm nay» và cảnh báo sắp hết hạn.
- Câu 15 bám nhãn báo cáo «Tồn kho & giá trị»: số lượng nhân giá vốn lô.
- Câu 16 bám «Báo cáo ca làm việc»: danh sách ca và quỹ tiền mặt.
- Câu 6, 8, 10 nói hệ quả vận hành, không nói NOVIXA đã làm tăng hay giảm số tiền đó.
- «Nô vi xa» chỉ xuất hiện khi bắt đầu phần sản phẩm, không lặp ở câu kết.

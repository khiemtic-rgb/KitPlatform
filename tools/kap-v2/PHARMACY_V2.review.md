# Bộ câu hỏi PHARMACY_V2 v2.0 — bản duyệt

Đánh dấu ✎ ở câu cần sửa chữ. Câu có 🩺 cần dược sĩ xác nhận. ⤷ là câu rẽ nhánh, chỉ hiện khi người làm chọn nỗi đau tương ứng ở câu P3.

Số câu người làm thấy: PAIN_CUSTOMER → 31, PAIN_INVENTORY → 31, PAIN_STAFF → 31, PAIN_NUMBERS → 31, PAIN_COMPLIANCE → 31, PAIN_GROWTH → 31

## Bắt đầu (PROFILE)

**P2. Nhà thuốc của anh/chị có mấy cơ sở?**  _(không tính điểm)_
- [—] 1 cơ sở
- [—] 2–3 cơ sở
- [—] 4–5 cơ sở
- [—] Trên 5 cơ sở

**P3. Việc nào đang làm anh/chị đau đầu nhất lúc này?**  _(không tính điểm; leadTag=pain_point)_
> Chọn một việc. Phần sau sẽ hỏi kỹ hơn đúng việc này.
- [—] Khách ít quay lại
- [—] Hàng tồn, cận hạn, hết hàng
- [—] Nhân viên làm không đều tay
- [—] Không nắm rõ lãi lỗ
- [—] Sổ sách, lo khi có kiểm tra
- [—] Muốn mở thêm nhưng chưa sẵn sàng

**P5. Mỗi đơn khách trả trung bình khoảng bao nhiêu?**  _(không tính điểm; dùng cho ước tính tiền)_
> Ước lượng là được.
- [—] Dưới 80.000đ
- [—] 80.000–150.000đ
- [—] 150.000–300.000đ
- [—] Trên 300.000đ
- [—] Không rõ

## Khách hàng (CUSTOMER)

**C1. Trong 10 khách gần nhất, khoảng mấy người là khách quen?**  _(không tính điểm; dùng cho ước tính tiền)_
> Không có đáp án đúng sai. Nhà thuốc gần bệnh viện thường ít khách quen hơn nhà thuốc trong khu dân cư.
- [—] 0–2 người
- [—] 3–5 người
- [—] 6–8 người
- [—] 9–10 người
- [—] Không để ý

**C2. Khách quen quay lại, nhân viên biết lần trước họ mua gì bằng cách nào?**
- [1] Không biết — phải hỏi lại khách
- [2] Nhân viên nhớ mặt, nhớ thuốc
- [3] Tra sổ hoặc tìm trong máy, mất vài phút
- [4] Nhập số điện thoại là thấy ngay

  - **Phát hiện (khi trả lời mức 1–2):** Khách quen quay lại mà nhân viên không biết lần trước họ mua gì, nên khó tư vấn tiếp và khó bán thêm đúng nhu cầu.
  - **Tuần này làm được:** Mở một cuốn sổ (hoặc một trang Excel) cho 20 khách quen nhất: tên, số điện thoại, thuốc hay mua. Nhân viên ghi thêm sau mỗi lần bán cho họ.
  - **Đo bằng:** Sau 30 ngày: số khách quen đã có hồ sơ.
  - **Novixa:** Hồ sơ khách gắn với từng đơn bán, tra bằng số điện thoại.

**C3. Khách dùng thuốc lâu dài (huyết áp, tiểu đường…) sắp hết thuốc, nhà thuốc có nhắc không?**
- [1] Không nhắc
- [2] Chỉ khi khách nhờ
- [3] Nhắc vài khách quen qua điện thoại, Zalo
- [4] Có danh sách, nhắc đều theo lịch

  - **Phát hiện (khi trả lời mức 1–2):** Khách dùng thuốc lâu dài không được nhắc khi sắp hết thuốc. Đây là nhóm khách dễ chuyển sang chỗ khác nhất.
  - **Tuần này làm được:** Lập danh sách khách mua thuốc huyết áp, tiểu đường trong tháng, ghi ngày dự kiến hết thuốc. Mỗi sáng xem danh sách và nhắn Zalo cho người sắp hết.
  - **Đo bằng:** Sau 30 ngày: trong số khách được nhắc, bao nhiêu người quay lại mua.
  - **Novixa:** Nhắc lịch mua lại tự động theo đơn trước.

**C4. Tháng trước, nhà thuốc chủ động hỏi thăm lại khoảng bao nhiêu khách sau khi bán?**
- [1] Không ai
- [2] Vài khách
- [3] Khoảng 10–30 khách
- [4] Trên 30 khách
- [—] Không theo dõi

  - **Phát hiện (khi trả lời mức 1–2):** Sau khi bán, nhà thuốc gần như không hỏi thăm lại khách.
  - **Tuần này làm được:** Mỗi ngày chọn 3 khách mua thuốc điều trị (kháng sinh, thuốc đau dạ dày…), 2–3 ngày sau nhắn hỏi thăm thuốc có hợp không.
  - **Đo bằng:** Số khách được hỏi thăm mỗi tuần.
  - **Novixa:** Danh sách khách cần hỏi thăm tự sinh theo đơn.

**C5. Anh/chị có biết tháng này bao nhiêu khách mới, bao nhiêu khách cũ quay lại không?**
- [1] Không biết
- [2] Chỉ có cảm giác chung
- [3] Đếm được nếu ngồi lục sổ
- [4] Xem được con số ngay

  - **Phát hiện (khi trả lời mức 1–2):** Nhà thuốc chưa biết mỗi tháng có bao nhiêu khách mới, bao nhiêu khách cũ quay lại, nên không biết việc giữ khách đang tốt lên hay xấu đi.
  - **Tuần này làm được:** Trong 1 tuần, mỗi đơn bán đánh dấu 'mới' hoặc 'quen' vào sổ. Cuối tuần đếm tổng.
  - **Đo bằng:** Tỷ lệ khách quen trên tổng khách, đo lại sau 30 ngày.
  - **Novixa:** Báo cáo khách mới và khách quay lại theo tháng.

**C6. Theo anh/chị, khách không quay lại chủ yếu vì sao?**  _(không tính điểm; ⤷ khi P3=PAIN_CUSTOMER)_
- [—] Giá cao hơn chỗ khác
- [—] Chỗ khác tiện đường hơn
- [—] Không được nhắc nên quên
- [—] Lúc cần thì nhà thuốc hết hàng
- [—] Không rõ

## Kho (INVENTORY)

**I1. Lần gần nhất anh/chị phát hiện một lô sắp hết hạn là nhờ đâu?**
- [1] Tình cờ lúc lấy hàng cho khách
- [2] Lúc kiểm kê
- [3] Rà sổ theo lịch định kỳ
- [4] Phần mềm báo trước
- [—] Chưa gặp trường hợp này

  - **Phát hiện (khi trả lời mức 1–2):** Hàng sắp hết hạn được phát hiện tình cờ hoặc khi đã quá muộn để xử lý.
  - **Tuần này làm được:** Thứ Hai hằng tuần, rà các lô hết hạn trong 3 tháng tới. Dán nhãn màu lên hộp và xếp ra trước để bán trước.
  - **Đo bằng:** Tiền hàng phải hủy hoặc trả lại tháng sau so với tháng này.
  - **Novixa:** Cảnh báo cận hạn theo từng lô.

**I2. Tháng trước, tiền hàng hết hạn phải hủy hoặc trả lại khoảng bao nhiêu?**  _(không tính điểm; dùng cho ước tính tiền)_
> Ước lượng là được.
- [—] Gần như không có
- [—] Dưới 1 triệu
- [—] 1–3 triệu
- [—] 3–7 triệu
- [—] Trên 7 triệu
- [—] Không rõ

**I3. Tuần trước, khoảng bao nhiêu lần khách hỏi mua mà nhà thuốc hết hàng?**
- [1] Trên 15 lần
- [2] 6–15 lần
- [3] 2–5 lần
- [4] 0–1 lần
- [—] Không để ý

  - **Phát hiện (khi trả lời mức 1–2):** Khách hỏi mua mà nhà thuốc hết hàng khá thường xuyên. Mỗi lần như vậy là một đơn có thể mất, và khách có thể không quay lại.
  - **Tuần này làm được:** Để một tờ giấy ở quầy: mỗi lần hết hàng khi khách hỏi, ghi tên thuốc. Cuối tuần nhập bổ sung những mặt hàng bị ghi từ 2 lần trở lên.
  - **Đo bằng:** Số lần hết hàng mỗi tuần.
  - **Novixa:** Mức tồn tối thiểu và gợi ý đặt hàng.

**I4. Khi đặt hàng, anh/chị quyết định số lượng dựa vào đâu?**
- [1] Thấy vơi thì nhập
- [2] Theo kinh nghiệm và gợi ý của trình dược viên
- [3] Xem lại lượng bán tháng trước
- [4] Có mức tồn tối thiểu cho từng mặt hàng

  - **Phát hiện (khi trả lời mức 1–2):** Số lượng đặt hàng dựa vào cảm giác hoặc gợi ý của trình dược viên, nên dễ vừa thiếu hàng bán chạy vừa dư hàng bán chậm.
  - **Tuần này làm được:** Chọn 30 mặt hàng bán chạy nhất. Ghi mức tồn tối thiểu cho từng món (bằng khoảng 1 tuần bán), dưới mức đó thì đặt.
  - **Đo bằng:** Số lần hết hàng của 30 mặt hàng này.
  - **Novixa:** Mức tồn tối thiểu và gợi ý đặt hàng.

**I5. Lần kiểm kê toàn bộ kho gần nhất là khi nào?**
- [1] Chưa bao giờ hoặc không nhớ
- [2] Hơn 6 tháng trước
- [3] Trong 1–6 tháng qua
- [4] Trong tháng này, hoặc kiểm từng nhóm hàng đều đặn

  - **Phát hiện (khi trả lời mức 1–2):** Đã lâu chưa kiểm kê toàn bộ kho, nên số trên sổ hoặc trên máy có thể lệch xa với hàng thật.
  - **Tuần này làm được:** Không cần kiểm kê cả kho cùng lúc. Mỗi tuần kiểm một nhóm hàng (ví dụ tuần này kiểm nhóm kháng sinh), ghi lại phần lệch.
  - **Đo bằng:** Tỷ lệ lệch giữa sổ và hàng thật của từng nhóm.
  - **Novixa:** Kiểm kê theo nhóm hàng trên điện thoại.

**I6. Anh/chị có kể ngay được 5 mặt hàng bán chậm nhất đang giữ vốn không?**
- [1] Không
- [2] Đoán được
- [3] Có, nhưng phải lục sổ
- [4] Có, xem được ngay

  - **Phát hiện (khi trả lời mức 1–2):** Chưa biết hàng nào bán chậm đang giữ vốn.
  - **Tuần này làm được:** Đi một vòng kệ, đánh dấu những hộp còn nguyên và có bụi. Lập danh sách 10 mặt hàng đó và ngừng nhập thêm.
  - **Đo bằng:** Tiền vốn nằm trong 10 mặt hàng này, đo lại sau 60 ngày.
  - **Novixa:** Báo cáo hàng chậm bán theo giá trị tồn.

**I7. Hàng cận hạn hiện được xử lý thế nào?**  _(không tính điểm; ⤷ khi P3=PAIN_INVENTORY)_
- [—] Để đến khi hết hạn rồi hủy
- [—] Trả nhà cung cấp nếu được
- [—] Giảm giá, ưu tiên bán trước
- [—] Chưa có cách làm thống nhất

## Vận hành (OPERATIONS)

**O1. Nếu anh/chị nghỉ 3 ngày, nhà thuốc chạy thế nào?**
- [1] Phải đóng cửa hoặc bán rất hạn chế
- [2] Vẫn mở nhưng nhân viên gọi hỏi liên tục
- [3] Chạy được, vài việc phải chờ anh/chị về
- [4] Chạy bình thường

  - **Phát hiện (khi trả lời mức 1–2):** Nhà thuốc phụ thuộc vào anh/chị. Vắng mặt vài ngày là việc bán bị ảnh hưởng.
  - **Tuần này làm được:** Viết ra 10 câu nhân viên hay gọi hỏi anh/chị nhất, kèm câu trả lời. Dán ở quầy.
  - **Đo bằng:** Số cuộc gọi hỏi việc trong tuần anh/chị vắng mặt.
  - **Novixa:** — không gắn sản phẩm

**O2. Nhân viên mới mất bao lâu thì tự đứng quầy được?**
- [1] Trên 2 tháng
- [2] 1–2 tháng
- [3] 2–4 tuần
- [4] Dưới 2 tuần — có tài liệu và người kèm
- [—] Chưa từng tuyển nhân viên

  - **Phát hiện (khi trả lời mức 1–2):** Nhân viên mới mất nhiều thời gian mới tự đứng quầy được.
  - **Tuần này làm được:** Viết danh sách những việc nhân viên mới phải biết trong tuần đầu, và ai kèm từng việc.
  - **Đo bằng:** Số ngày để nhân viên mới tự đứng quầy, ở lần tuyển tiếp theo.
  - **Novixa:** — không gắn sản phẩm

**O3. Cùng một bệnh thông thường như cảm cúm, các nhân viên có tư vấn giống nhau không?**
- [1] Mỗi người một kiểu
- [2] Gần giống nhau, do quen tay
- [3] Có hướng dẫn chung nhưng chưa đủ
- [4] Có hướng dẫn cho các bệnh hay gặp
- [—] Chỉ mình tôi đứng quầy

  - **Phát hiện (khi trả lời mức 1–2):** Mỗi nhân viên tư vấn một kiểu. Khách có thể nhận lời khuyên khác nhau ở cùng một nhà thuốc.
  - **Tuần này làm được:** Chọn 5 bệnh hay gặp nhất. Mỗi bệnh viết 1 trang: hỏi khách gì, khuyên gì, khi nào khuyên đi khám. Cả nhóm cùng thống nhất.
  - **Đo bằng:** Số bệnh đã có hướng dẫn chung.
  - **Novixa:** — không gắn sản phẩm

**O4. Cuối ngày, tiền thu có khớp với số đã bán không?**
- [1] Thường lệch, không rõ vì sao
- [2] Đôi khi lệch
- [3] Ít lệch, tìm được nguyên nhân
- [4] Khớp, có đối chiếu hằng ngày

  - **Phát hiện (khi trả lời mức 1–2):** Tiền cuối ngày hay lệch với số đã bán mà không rõ nguyên nhân. Đây có thể là thất thoát.
  - **Tuần này làm được:** Đối chiếu tiền với số bán ngay cuối mỗi ca, không để đến cuối ngày. Ghi phần lệch và tên người trực ca.
  - **Đo bằng:** Số ngày lệch tiền trong tháng.
  - **Novixa:** Chốt ca theo từng nhân viên.

**O5. Các việc định kỳ (ghi nhiệt độ, độ ẩm, vệ sinh, sổ sách) có người phụ trách và ghi lại không?**  _(🩺)_
- [1] Chưa làm đều
- [2] Làm khi sắp có kiểm tra
- [3] Làm đều nhưng hay thiếu sổ
- [4] Có người phụ trách, ghi đủ hằng ngày

  - **Phát hiện (khi trả lời mức 1–2):** Các việc định kỳ như ghi nhiệt độ, độ ẩm và sổ sách chưa được làm đều. Đây thường là phần bị nhắc khi có kiểm tra.
  - **Tuần này làm được:** Giao cho một người cụ thể, đặt giờ cố định mỗi ngày, và để sổ ngay cạnh nhiệt kế.
  - **Đo bằng:** Số ngày trong tháng có ghi đủ.
  - **Novixa:** — không gắn sản phẩm

**O6. Lần kiểm tra gần nhất, nhà thuốc bị nhắc ở phần nào?**  _(không tính điểm; ⤷ khi P3=PAIN_COMPLIANCE; 🩺)_
- [—] Chưa bị kiểm tra lần nào
- [—] Sổ sách, ghi chép
- [—] Bảo quản, sắp xếp thuốc
- [—] Hồ sơ nhân sự, bằng cấp
- [—] Không bị nhắc gì

**O7. Việc nào với nhân viên làm anh/chị mệt nhất?**  _(không tính điểm; ⤷ khi P3=PAIN_STAFF)_
- [—] Nghỉ việc nhiều, phải tuyển lại
- [—] Tư vấn chưa tốt
- [—] Không chủ động, phải nhắc từng việc
- [—] Khó kiểm soát tiền, hàng

## Kinh doanh (BUSINESS)

**B1. Anh/chị có biết tháng trước nhà thuốc lãi bao nhiêu không?**
- [1] Không — chỉ biết khi tổng kết cuối năm
- [2] Đoán theo tiền còn trong két
- [3] Tính được nhưng mất công
- [4] Biết rõ con số hằng tháng

  - **Phát hiện (khi trả lời mức 1–2):** Chưa biết rõ mỗi tháng nhà thuốc lãi bao nhiêu.
  - **Tuần này làm được:** Cuối tháng này, ghi 4 con số: tiền bán, tiền nhập hàng, tiền thuê, tiền lương và các chi phí khác. Lấy tiền bán trừ ba khoản sau là ra con số lãi đầu tiên.
  - **Đo bằng:** Có con số lãi của 2 tháng liền để so sánh.
  - **Novixa:** Báo cáo lãi gộp theo ngày và theo tháng.

**B2. Anh/chị có biết nhóm hàng nào lãi nhiều nhất (không phải bán nhiều nhất)?**
- [1] Không
- [2] Đoán được
- [3] Biết, nhờ tính tay
- [4] Có số liệu rõ ràng

  - **Phát hiện (khi trả lời mức 1–2):** Chưa biết nhóm hàng nào thật sự mang lại lãi. Bán nhiều chưa chắc đã lãi nhiều.
  - **Tuần này làm được:** Lấy 20 mặt hàng bán chạy nhất, ghi giá nhập và giá bán của từng món. Xếp theo tiền lãi mỗi hộp.
  - **Đo bằng:** Danh sách 10 mặt hàng lãi nhất để ưu tiên trưng bày và tư vấn.
  - **Novixa:** Lãi gộp theo mặt hàng và nhóm hàng.

**B3. Lần khuyến mại gần nhất, anh/chị có biết nó lời hay lỗ không?**
- [1] Không biết
- [2] Chỉ thấy đông khách hơn
- [3] So được số bán trước và sau
- [4] Tính được phần lãi thêm sau khi trừ giảm giá
- [—] Chưa làm khuyến mại

  - **Phát hiện (khi trả lời mức 1–2):** Khuyến mại được làm mà chưa biết lời hay lỗ.
  - **Tuần này làm được:** Lần khuyến mại tới, ghi số bán của mặt hàng đó 2 tuần trước và 2 tuần trong đợt. Trừ phần đã giảm giá để biết lời hay lỗ.
  - **Đo bằng:** Đợt khuyến mại tới có lãi thêm hay không.
  - **Novixa:** — không gắn sản phẩm

**B4. Tháng nào bán chậm hẳn, việc đầu tiên anh/chị làm là gì?**
- [1] Chờ xem tháng sau
- [2] Đoán nguyên nhân, thử giảm giá
- [3] Lục sổ xem chậm ở đâu
- [4] Xem số theo nhóm hàng, nhóm khách để tìm chỗ chậm

  - **Phát hiện (khi trả lời mức 1–2):** Khi bán chậm lại, chưa có cách tìm ra chậm ở đâu.
  - **Tuần này làm được:** Chia tiền bán theo 5 nhóm hàng chính và ghi theo tuần. Khi bán chậm thì so nhóm nào giảm.
  - **Đo bằng:** Có số bán theo nhóm hàng của 4 tuần liền.
  - **Novixa:** Số bán theo nhóm hàng, nhóm khách.

**B5. Anh/chị muốn mỗi ngày nhìn thấy con số nào nhất?**  _(không tính điểm; ⤷ khi P3=PAIN_NUMBERS)_
- [—] Lãi trong ngày
- [—] Hàng sắp hết, hàng sắp hết hạn
- [—] Khách mới, khách quay lại
- [—] Kết quả của từng nhân viên

## Dữ liệu & Công nghệ (TECH)

**T1. Việc bán hàng hiện được ghi lại bằng gì?**
- [1] Sổ tay, hoặc không ghi
- [2] Excel
- [3] Phần mềm trên một máy tại quầy
- [4] Phần mềm, xem được cả trên điện thoại

  - **Phát hiện (khi trả lời mức 1–2):** Việc bán hàng chưa được ghi lại để tra cứu và tính toán được.
  - **Tuần này làm được:** Bắt đầu bằng một file Excel với 4 cột: ngày, mặt hàng, số lượng, tiền. Ghi trong 1 tuần để thấy mình cần gì.
  - **Đo bằng:** Số ngày trong tuần có ghi đủ.
  - **Novixa:** Phần mềm bán hàng cho nhà thuốc.

**T2. Khi không ở nhà thuốc, anh/chị biết hôm nay bán được bao nhiêu bằng cách nào?**
- [1] Không biết đến khi về
- [2] Gọi điện hỏi
- [3] Nhân viên chụp sổ gửi qua Zalo
- [4] Tự xem trên điện thoại

  - **Phát hiện (khi trả lời mức 1–2):** Không ở nhà thuốc thì không nắm được tình hình bán hàng.
  - **Tuần này làm được:** Cuối mỗi ca, nhân viên nhắn Zalo 3 con số: số đơn, tiền thu, những mặt hàng bị hết.
  - **Đo bằng:** Số ngày nhận đủ tin nhắn.
  - **Novixa:** Xem số bán trên điện thoại.

**T3. Thông tin bán hàng, kho và khách hàng đang nằm ở đâu?**
- [1] Chưa ghi đủ cả ba
- [2] Mỗi thứ một nơi
- [3] Cùng một phần mềm nhưng ít dùng phần kho hoặc khách
- [4] Cùng một phần mềm, dùng đủ cả ba

  - **Phát hiện (khi trả lời mức 1–2):** Thông tin bán hàng, kho và khách mỗi thứ một nơi, nên không trả lời được những câu như 'khách nào hay mua hàng sắp hết hạn này'.
  - **Tuần này làm được:** Ghi số điện thoại của khách vào cùng chỗ ghi đơn bán, kể cả khi chỉ dùng sổ.
  - **Đo bằng:** Tỷ lệ đơn bán có ghi số điện thoại khách.
  - **Novixa:** Bán hàng, kho và khách hàng trên cùng một phần mềm.

**T4. Việc kết nối liên thông dữ liệu bán thuốc theo yêu cầu của ngành dược đang thế nào?**  _(🩺)_
- [1] Chưa kết nối
- [2] Đang làm, chưa ổn định
- [3] Có kết nối nhưng nhập tay nhiều
- [4] Kết nối tự động từ phần mềm bán hàng
- [—] Không rõ yêu cầu này

  - **Phát hiện (khi trả lời mức 1–2):** Việc liên thông dữ liệu bán thuốc chưa ổn định hoặc chưa rõ yêu cầu.
  - **Tuần này làm được:** Hỏi lại cơ quan quản lý dược địa phương hoặc nhà cung cấp phần mềm xem nhà thuốc đang thiếu bước nào.
  - **Đo bằng:** Nhà thuốc biết rõ mình đã đủ yêu cầu hay chưa.
  - **Novixa:** Kết nối liên thông từ phần mềm bán hàng (cần xác nhận phạm vi hỗ trợ trước khi ghi vào báo cáo).

## Phát triển (GROWTH)

**G1. Anh/chị đã có mục tiêu cụ thể cho 12 tháng tới chưa?**
- [1] Chưa nghĩ tới
- [2] Có ý định chung
- [3] Có mục tiêu, chưa có kế hoạch
- [4] Có mục tiêu, kế hoạch và theo dõi hằng tháng

  - **Phát hiện (khi trả lời mức 1–2):** Chưa có mục tiêu cụ thể cho năm tới.
  - **Tuần này làm được:** Viết 1 mục tiêu đo được cho 90 ngày tới, ví dụ 'giảm hàng hủy còn dưới 1 triệu/tháng'.
  - **Đo bằng:** Có mục tiêu và được xem lại mỗi tháng.
  - **Novixa:** — không gắn sản phẩm

**G2. Nếu mở thêm một cơ sở, cách làm hiện nay có áp dụng được ngay không?**
- [1] Phải làm lại từ đầu
- [2] Chỉ áp dụng được một phần, phụ thuộc vào người
- [3] Được, nếu anh/chị trực tiếp kèm
- [4] Được — đã có quy trình và phần mềm dùng chung
- [—] Chưa có ý định mở thêm

  - **Phát hiện (khi trả lời mức 1–2):** Cách làm hiện nay phụ thuộc vào người, nên khó áp dụng sang cơ sở mới.
  - **Tuần này làm được:** Ghi lại 3 quy trình quan trọng nhất (nhận hàng, bán hàng, chốt ca) thành từng bước.
  - **Đo bằng:** Số quy trình đã được viết ra.
  - **Novixa:** Phần mềm dùng chung cho nhiều cơ sở.

**G3. Anh/chị dự định mở thêm trong bao lâu?**  _(không tính điểm; ⤷ khi P3=PAIN_GROWTH)_
- [—] Trong 6 tháng
- [—] 6–12 tháng
- [—] Sau 1 năm
- [—] Chưa chắc

**G5. Nếu chỉ chọn một việc để cải thiện trong 30 ngày tới, anh/chị chọn việc nào?**  _(không tính điểm; leadTag=priority_need)_
> Báo cáo sẽ đặt việc này lên đầu.
- [—] Giữ chân khách quen
- [—] Giảm hàng tồn, hàng cận hạn
- [—] Nhân viên làm đều tay
- [—] Nắm rõ lãi lỗ
- [—] Sổ sách gọn, sẵn sàng khi kiểm tra
- [—] Kết nối bác sĩ, đơn thuốc online
- [—] Việc khác

## Ước tính bằng tiền

- **EXPIRED_PER_YEAR** = `I2 * 12` (hiện khi `I2 >= 1000000`)
  - Câu hiển thị: Theo số anh/chị nhập, hàng hết hạn phải hủy hoặc trả lại khoảng {value} mỗi năm.
  - Ghi chú bắt buộc: Ước tính từ con số anh/chị nhập, nhân 12 tháng. Chưa trừ phần trả lại được nhà cung cấp.
- **STOCKOUT_PER_YEAR** = `I3 * 52 * P5` (hiện khi `I3 >= 2`)
  - Câu hiển thị: Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng {value} tiền bán mỗi năm.
  - Ghi chú bắt buộc: Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn.

## Nguyên nhân gốc — BẢN NHÁP, CẦN DƯỢC SĨ DUYỆT

Chưa được duyệt. Sinh vào `assessment_root_cause_kb` khi điểm câu ≤ 2. O5 giữ nguyên câu phát hiện phía trên, không sửa chữ.

- **C3** (CUSTOMER) `RC_V2_C3_REFILL` — Khách thuốc dài ngày không được nhắc. Hiện khi `response.C3.score <= 2`. Câu C3: nhắc khách dùng thuốc lâu dài.
  - Khách dùng thuốc lâu dài không được nhắc khi sắp hết thuốc. Đây là nhóm khách dễ chuyển sang chỗ khác nhất.
- **I1** (INVENTORY) `RC_V2_I1_EXPIRY` — Hàng cận hạn được phát hiện muộn. Hiện khi `response.I1.score <= 2`. Câu I1: theo dõi hàng cận hạn.
  - Hàng sắp hết hạn được phát hiện tình cờ hoặc khi đã quá muộn để xử lý.
- **I3** (INVENTORY) `RC_V2_I3_STOCKOUT` — Hết hàng khi khách hỏi mua. Hiện khi `response.I3.score <= 2`. Câu I3: hết hàng lúc khách hỏi.
  - Khách hỏi mua mà nhà thuốc hết hàng khá thường xuyên. Mỗi lần như vậy là một đơn có thể mất, và khách có thể không quay lại.
- **B1** (BUSINESS) `RC_V2_B1_PROFIT` — Chưa biết rõ lãi mỗi tháng. Hiện khi `response.B1.score <= 2`. Câu B1: lãi mỗi tháng.
  - Chưa biết rõ mỗi tháng nhà thuốc lãi bao nhiêu.
- **O1** (OPERATIONS) `RC_V2_O1_OWNER` — Nhà thuốc phụ thuộc vào chủ. Hiện khi `response.O1.score <= 2`. Câu O1: nhà thuốc khi chủ vắng.
  - Nhà thuốc phụ thuộc vào anh/chị. Vắng mặt vài ngày là việc bán bị ảnh hưởng.
- **O5** (OPERATIONS) `RC_V2_O5_LOG` — Việc định kỳ chưa được ghi đều. Hiện khi `response.O5.score <= 2`. Câu O5: sổ định kỳ.
  - Các việc định kỳ như ghi nhiệt độ, độ ẩm và sổ sách chưa được làm đều. Đây thường là phần bị nhắc khi có kiểm tra.
- **I4** (INVENTORY) `RC_V2_I4_ORDER` — Đặt hàng theo cảm giác. Hiện khi `response.I4.score <= 2`. Câu I4: cách đặt hàng.
  - Số lượng đặt hàng dựa vào cảm giác hoặc gợi ý của trình dược viên, nên dễ vừa thiếu hàng bán chạy vừa dư hàng bán chậm.
- **C2** (CUSTOMER) `RC_V2_C2_PROFILE` — Khách quen không có hồ sơ. Hiện khi `response.C2.score <= 2`. Câu C2: hồ sơ khách quen.
  - Khách quen quay lại mà nhân viên không biết lần trước họ mua gì, nên khó tư vấn tiếp và khó bán thêm đúng nhu cầu.

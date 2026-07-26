---
title: Những lỗi thường gặp khi nhập hàng tại nhà thuốc và cách triệt tiêu rủi ro
description: ''
category: quan-tri-nha-thuoc
subcategory: doanh-thu
image: /images/tin-tuc/loi-thuong-gap-khi-nhap-hang.png
pubDate: 2026-08-24
lang: vi
---

Nhập hàng (Inbound Ops) là điểm xuất phát của toàn bộ chuỗi vận hành tại nhà thuốc. Một quy trình bán hàng có thể chuẩn chỉnh, dược sĩ có thể tư vấn hay, nhưng nếu **đã sai ngay từ khâu nhận hàng vào kho**, mọi nỗ lực phía sau đều bị ảnh hưởng nặng nề.

Thực tế tại nhiều nhà thuốc cho thấy, khâu nhập hàng thường diễn ra khá vội vã. Khi trình dược viên giao kiện hàng đến, dược sĩ tại quầy—vừa phải bán hàng cho khách, vừa tranh thủ nhận hàng—rất dễ thực hiện kiểm đếm qua loa, ký nhận cho xong rồi đẩy kiện hàng vào góc quầy.

Chính sự chủ quan và thiếu quy trình đối soát nghiêm ngặt này đã mở đường cho hàng loạt rủi ro: Thuốc bị móp vỡ, giao thiếu số lượng, sai hàm lượng, nhận phải lô hàng sát hạn dùng hay đứt gãy dữ liệu POS. **Tiền thất thoát ở khâu nhập hàng là khoản thất thoát vô lý và đau đớn nhất vì nó hoàn toàn có thể phòng ngừa.**

## Phân tích: 5 lỗi kinh điển khi nhập hàng tàn phá dòng tiền nhà thuốc

Khi rà soát thực tế vận hành tại các điểm bán, 5 sai lầm dưới đây là nguyên nhân trực tiếp dẫn đến thất thoát tài sản và hỗn loạn tồn kho:

### 1. Nhận hàng "mù" – Không đối soát thực tế với Hóa đơn & Đơn đặt (PO)

Dược sĩ chỉ đếm tổng số thùng/hộp bên ngoài mà không mở từng hộp kiểm tra chi tiết bên trong, hoặc chỉ nhìn hóa đơn rồi ký nhận. Hậu quả là nhà phân phối giao thiếu hàng, giao sai quy cách (hộp 3 vỉ giao thành hộp 1 vỉ), hoặc giao nhầm mã thuốc nhưng nhà thuốc vẫn thanh toán đủ 100% tiền mặt/chuyển khoản.

### 2. Bỏ qua việc kiểm tra Số lô (Lot) và Hạn sử dụng (Expiry Date)

Đây là lỗi chết người đối với nguyên tắc quản trị kho **FEFO** (First Expired, First Out - Hết hạn trước, xuất trước). Dược sĩ nhận hàng nhưng không kiểm tra xem hạn sử dụng còn lại là bao lâu, hoặc nhập lên phần mềm POS một đằng nhưng hạn dùng thực tế trên vỏ hộp lại là một nẻo. Nhà thuốc vô tình nhận về những lô hàng cận hạn (dưới 6–12 tháng) mà không hề hay biết để có chính sách xử lý kịp thời.

### 3. "Dễ tính" với hàng lỗi bao bì, móp hộp, rách tem niêm phong

Vì nể nang trình dược viên quen biết hoặc nghĩ "bên trong thuốc vẫn tốt", dược sĩ vẫn ký nhận các hộp thuốc bị móp góc, rách tem, đứt vỉ hay mờ nhạt hạn dùng. Đến khi đưa lên kệ xuất bán, khách hàng kiên quyết từ chối mua sản phẩm lỗi vỏ. Hộp thuốc đó lập tức trở thành **Tồn kho chết (Deadstock)** và nhà thuốc phải gánh chịu toàn bộ thiệt hại.

### 4. Sai lệch giữa Giá nhập thực tế và Giá trên Phần mềm POS

Nhập hàng về nhưng không đối soát kỹ bảng giá, chiết khấu và chương trình quà tặng kèm. Dược sĩ gõ nhầm giá nhập trên phần mềm POS khiến báo cáo Biên lợi nhuận gộp (Gross Margin) bị sai lệch hoàn toàn, hoặc quên không cập nhật giá bán lẻ mới khi nhà sản xuất điều chỉnh giá, làm nhà thuốc bị tổn hại lợi nhuận âm thầm.

### 5. Nhập kho một đằng, Sắp xếp quầy kệ một nẻo (Tạo thành hàng ẩn)

Hàng nhận xong không được phân loại và đưa ngay lên vị trí quy định trên kệ, mà để nằm lại trong thùng carton/góc kho lưu trữ. Khi khách hỏi mua, dược sĩ nhìn lên kệ thấy hết nên báo hết hàng, trong khi hàng thực tế vẫn nằm yên trong thùng vừa nhập. Điều này vừa làm mất doanh thu, vừa sinh ra hiện tượng "dư hàng nhưng vẫn thiếu hàng".

## Giải pháp: Quy trình Nhập kho 4 Bước chuẩn GPP & Công nghệ đối soát

Toàn bộ sai sót khi nhập hàng đều có thể triệt tiêu nếu nhà thuốc đưa hoạt động này vào Quy trình thao tác chuẩn (SOP Inbound) nghiêm ngặt:

```plain
[B1: Kiểm tra Cảm quan & Bao bì] ──► [B2: Đối soát 3 Bên (PO - Hóa đơn - Thực tế)] ──► [B3: Nhập POS chính xác Số lô/Hạn dùng] ──► [B4: Sắp xếp FEFO lên kệ]

```

### Bước 1: Kiểm tra Cảm quan & Ngoại quan sản phẩm

- **Thao tác:** Khai mở kiện hàng trực tiếp trước mặt người giao. Kiểm tra 100% tình trạng bao bì: Hộp nguyên tem, không móp vỡ, không bị ẩm ướt, chữ in rõ ràng. Kiên quyết từ chối nhận các sản phẩm lỗi bao bì.

### Bước 2: Đối soát 3 bên (Purchase Order - Invoice - Physical Stock)

- **Thao tác:** Đặt tờ đơn hàng đã đặt (PO) bên cạnh Hóa đơn giao hàng (Invoice) và Sản phẩm thực tế. Đối soát theo công thức "3 Đúng": **Đúng tên/hàm lượng ──► Đúng quy cách/số lượng ──► Đúng giá/chiết khấu**.

### Bước 3: Số hóa khâu nhập kho trên phần mềm POS

- **Thao tác:** Sử dụng máy quét mã vạch quét trực tiếp từng sản phẩm để nhập kho. **Bắt buộc nhập chính xác 100% Số lô (Lot) và Hạn sử dụng (Exp)** hiển thị trên bao bì vào phần mềm. Đây là cơ sở dữ liệu nền tảng để hệ thống tự động chạy cảnh báo FEFO sau này.

### Bước 4: Phân loại & Trưng bày chuẩn FEFO lập tức

- **Thao tác:** Bóc xếp hàng hóa ra khỏi thùng ngay sau khi nhập hệ thống. Đưa hàng lên tủ/kệ đúng vị trí quy định: _Lô hàng mới hạn dài hơn xếp vào trong/dưới đáy; Lô hàng cũ hạn ngắn hơn đưa ra ngoài/trên cùng_.

## Checklist: 10 câu hỏi tự kiểm tra "Sức khỏe quy trình nhập hàng" tại nhà thuốc

1. [ ] Dược sĩ có kiểm tra trực tiếp tình trạng vỏ hộp (móp, rách, mở tem) trước khi ký nhận không?
2. [ ] Nhà thuốc có quy định hạn sử dụng tối thiểu bắt buộc đối với từng nhóm hàng khi nhận kho không?
3. [ ] Dược sĩ có thực hiện đối soát giữa Hóa đơn giao hàng và Tồn kho thực tế từng sản phẩm không?
4. [ ] 100% Số lô (Lot) và Hạn sử dụng (Exp) thực tế có được nhập chính xác vào phần mềm POS không?
5. [ ] Bạn có kiểm tra lại giá nhập thực tế và tỷ lệ chiết khấu so với đơn đặt hàng ban đầu không?
6. [ ] Hàng mới nhập về có được phân loại và đưa ngay lên quầy kệ trong vòng 2 giờ sau khi nhận không?
7. [ ] Dược sĩ có tuân thủ nguyên tắc xếp hàng FEFO (Lô cũ ra ngoài, lô mới vào trong) khi bổ sung hàng không?
8. [ ] Danh mục công việc (Checklist) ca trực có bao gồm việc rà soát và hoàn tất các phiếu nhập kho dở dang không?
9. [ ] Khi phát hiện hàng giao sai hoặc lỗi, nhà thuốc có biên bản ghi nhận sự cố để làm việc với nhà cung cấp không?
10. [ ] Quản lý có định kỳ kiểm tra đột xuất (Manager Validation) thao tác nhập kho của dược sĩ không?

## Lời kết

Khâu nhập hàng không đơn thuần là việc bê những thùng thuốc vào kho và trao đi một xấp tiền. Đó là "tuyến phòng thủ" đầu tiên bảo vệ tài sản, dòng tiền và tiêu chuẩn an toàn y khoa của nhà thuốc. Khi bạn chuẩn hóa được quy trình nhập hàng 4 bước, yêu cầu kỷ luật đối soát từng số lô/hạn dùng và quản trị bằng dữ liệu POS minh bạch, bạn sẽ bịt kín mọi kẽ hở thất thoát, giữ cho kho hàng luôn sạch sẽ và tối ưu lợi nhuận thuần cho điểm bán.

> **Novixa** đồng hành cùng các nhà thuốc chuẩn hóa quy trình bán hàng SOP, tự động hóa quản trị POS, CRM, cảnh báo kho FEFO và kiểm soát dòng tiền thời gian thực, giúp bạn giải phóng sức lao động và tối ưu hiệu quả kinh doanh.

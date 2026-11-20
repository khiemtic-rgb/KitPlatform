# novixa-long

## segment s1-open
goal: Novixa giữ bán hàng, tồn kho và hạn dùng trên một nhịp.
dialogue: |
  Novixa giữ bán hàng, tồn kho và hạn dùng trên một nhịp.
on_screen:
- Novixa giữ bán hàng, tồn kho và hạn dùng trên một nhịp.
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 10
template_hint: designed_board

## segment s2-dash
goal: Màn tổng quan nhà thuốc
dialogue: |
  Màn tổng quan cho thấy nhịp của nhà thuốc.
on_screen:
- Màn tổng quan nhà thuốc
- Số trên hình là dữ liệu demo.
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 14
template_hint: annotated_plate

## segment s3-pos
goal: Màn bán hàng tại quầy
dialogue: |
  Bán hàng nằm trên màn hình quầy.
on_screen:
- Màn bán hàng tại quầy
- Số trên hình là dữ liệu demo.
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 14
template_hint: annotated_plate

## segment s4-gap
goal: Báo thiếu ảnh hộp thuốc
dialogue: |
  Chưa có ảnh hộp thuốc đã duyệt. Không lấy ảnh khác để lấp chỗ này.
on_screen:
- Chưa có ảnh hộp thuốc
- Không lấy ảnh khác để lấp
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 10
template_hint: kinetic_type

## segment s5-catalog
goal: Danh mục trên phần mềm
dialogue: |
  Danh mục sản phẩm là danh sách trên phần mềm, chưa phải ảnh hộp thuốc.
on_screen:
- Danh mục trên phần mềm
- Số trên hình là dữ liệu demo.
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 14
template_hint: annotated_plate

## segment s6-stock
goal: Màn tồn kho
dialogue: |
  Tồn kho và hạn dùng được nhìn trên màn tồn.
on_screen:
- Màn tồn kho
- Số trên hình là dữ liệu demo.
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 14
template_hint: annotated_plate

## segment s7-flow
goal: Sơ đồ một nhịp tại quầy
dialogue: |
  Một nhịp: bán, trừ tồn, rồi nhắc hạn.
on_screen:
- Một nhịp tại quầy
- Sơ đồ nghiệp vụ
- Bán
- Kho
- Hạn
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 12
template_hint: designed_board

## segment s8-grn
goal: Màn nhập hàng
dialogue: |
  Nhập hàng có một màn riêng.
on_screen:
- Màn nhập hàng
- Số trên hình là dữ liệu demo.
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 14
template_hint: annotated_plate

## segment s9-crm
goal: Hồ sơ khách hàng
dialogue: |
  Hồ sơ khách nằm trong phần mềm.
on_screen:
- Hồ sơ khách hàng
- Số trên hình là dữ liệu demo.
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 14
template_hint: annotated_plate

## segment s10-report
goal: Màn báo cáo
dialogue: |
  Báo cáo mở từ màn báo cáo.
on_screen:
- Màn báo cáo
- Số trên hình là dữ liệu demo.
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 14
template_hint: annotated_plate

## segment s11-nxt
goal: Báo cáo xuất nhập tồn
dialogue: |
  Xuất nhập tồn là một báo cáo khác, không dùng lại màn tổng quan.
on_screen:
- Báo cáo xuất nhập tồn
- Số trên hình là dữ liệu demo.
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 14
template_hint: annotated_plate

## segment s12-app
goal: Ứng dụng khách hàng
dialogue: |
  Khách có một màn ứng dụng riêng.
on_screen:
- Ứng dụng khách hàng
- Số trên hình là dữ liệu demo.
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 12
template_hint: annotated_plate

## segment s13-link
goal: Liên thông dữ liệu thuốc
dialogue: |
  Liên thông dữ liệu thuốc là một màn cấu hình và theo dõi.
on_screen:
- Liên thông dữ liệu thuốc
- Số trên hình là dữ liệu demo.
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 12
template_hint: annotated_plate

## segment s14-close
goal: Novixa
dialogue: |
  Bán đúng. Theo dõi hạn. Giao rõ.
on_screen:
- Bán đúng
- Novixa
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 12
template_hint: designed_board

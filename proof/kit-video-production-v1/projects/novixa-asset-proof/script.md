# novixa-asset-proof

## segment s1-pack
goal: Vỉ thuốc tham chiếu
dialogue: |
  Đây là ảnh vỉ thuốc đã có giấy phép, không phải hộp của Novixa.
on_screen:
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 5
template_hint: annotated_plate

## segment s2-action
goal: Người kiểm vỉ thuốc
dialogue: |
  Người đang kiểm một vỉ. Đây không phải dược sĩ của Novixa.
on_screen:
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 5
template_hint: annotated_plate

## segment s3-place
goal: Không gian nhà thuốc
dialogue: |
  Đây là không gian nhà thuốc tham chiếu, không phải quầy của Novixa.
on_screen:
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 5
template_hint: annotated_plate

## segment s4-ui
goal: Màn tồn kho trên phần mềm
dialogue: |
  Tồn kho và hạn dùng được nhìn trên màn hình phần mềm.
on_screen:
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 5
template_hint: annotated_plate

## segment s5-join
goal: Ghép giao diện với ảnh vỉ
dialogue: |
  Ảnh vỉ đứng cạnh màn tồn. Hai nguồn này không phải một cảnh chụp.
on_screen:
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 5
template_hint: layered_plate

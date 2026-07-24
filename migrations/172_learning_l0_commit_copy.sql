-- KitPlatform 172: L0 — copy cam kết khớp UI «Ký cam kết điện tử»

UPDATE pack_learning.module
SET
    body_markdown = replace(
        body_markdown,
        'Khi bấm xác nhận phía dưới, bạn xác nhận đã:',
        'Khi bạn **ký cam kết điện tử** ở hộp bên dưới, hệ thống ghi nhận trên tài khoản Novixa rằng bạn đã:'
    ),
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222210'
  AND body_markdown LIKE '%Khi bấm xác nhận phía dưới%';

-- 1 điểm đổi = 100 đ. Mức mua để được 1 điểm giữ nguyên.
-- Chỉ sửa chương trình còn đang gán giá đổi bằng mức tích (quy tắc cũ).
UPDATE loyalty_programs
SET amount_per_point = 100,
    updated_at = NOW()
WHERE amount_per_point = points_per_amount
  AND amount_per_point <> 100;

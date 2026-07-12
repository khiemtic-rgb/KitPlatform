-- KitPlatform 123: Việt hóa chuyên khoa legacy (Da khoa → Đa khoa, Noi → Nội tổng quát, …)
-- Chạy bằng role sở hữu bảng (thường pharmacore). pack_connect có thể thiếu quyền — UI vẫn normalize.

UPDATE pack_clinic.clinic_provider
SET specialty = 'Đa khoa',
    updated_at = NOW()
WHERE specialty IS NOT NULL
  AND BTRIM(specialty) IN ('Da khoa', 'da khoa', 'DA KHOA', 'Dakhoa', 'dakhoa');

UPDATE pack_clinic.clinic_provider
SET specialty = 'Nội tổng quát',
    updated_at = NOW()
WHERE specialty IS NOT NULL
  AND BTRIM(specialty) IN (
      'Noi',
      'noi',
      'NOI',
      'Noi tong quat',
      'Noi tong quát',
      'noi tong quat',
      'Nội tong quat'
  );

DO $$
BEGIN
    UPDATE pack_connect.doctors
    SET specialty = 'Đa khoa',
        updated_at = NOW()
    WHERE specialty IS NOT NULL
      AND BTRIM(specialty) IN ('Da khoa', 'da khoa', 'DA KHOA', 'Dakhoa', 'dakhoa');

    UPDATE pack_connect.doctors
    SET specialty = 'Nội tổng quát',
        updated_at = NOW()
    WHERE specialty IS NOT NULL
      AND BTRIM(specialty) IN (
          'Noi',
          'noi',
          'NOI',
          'Noi tong quat',
          'Noi tong quát',
          'noi tong quat',
          'Nội tong quat'
      );
EXCEPTION
    WHEN insufficient_privilege THEN
        RAISE NOTICE '123: skip pack_connect.doctors (no privilege) — UI vẫn chuẩn hóa khi chọn.';
    WHEN undefined_table THEN
        RAISE NOTICE '123: pack_connect.doctors missing — skip.';
END $$;

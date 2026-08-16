-- Editorial rewrite of KIT_LOCAL job listings. Facts only from the original paste.
-- Skip: test phone, MLM-style "8–30 triệu", karaoke without address, duplicate Thượng Đỉnh.

UPDATE pack_local.listing SET
  title = 'Tuyển công nhân Shinsung tại Điềm Thụy',
  summary = $s$Tuyển công nhân tại nhà máy Shinsung, Điềm Thụy, Thái Nguyên.

Thu nhập: lương cơ bản + phụ cấp 7,4 triệu (chưa tăng ca). Ngày 427.000đ, đêm 505.000đ, chủ nhật 707.000–870.000đ.
Thời gian: làm ngồi; được mang điện thoại vào xưởng.
Địa điểm: Điềm Thụy, Thái Nguyên.
Liên hệ: 0357056663.$s$,
  place_text = 'Điềm Thụy, Thái Nguyên',
  contact_phone = '0357056663',
  salary_text = 'Lương cơ bản + phụ cấp 7,4 triệu (chưa tăng ca)',
  working_time = NULL,
  employment_type = 'full_time',
  source_kind = 'admin_write',
  status = 'ACTIVE',
  published_at = COALESCE(published_at, NOW()),
  expires_at = COALESCE(expires_at, NOW() + INTERVAL '14 days'),
  last_checked_at = NOW(),
  updated_at = NOW()
WHERE id = '01a005d7-f718-739a-b868-231af6176676';

UPDATE pack_local.listing SET
  title = 'Tuyển nhân viên phục vụ — Thượng Đỉnh Nướng 24H',
  summary = $s$Nhà hàng Thượng Đỉnh Nướng 24H tuyển nhân viên phục vụ toàn thời gian, bán thời gian và cuối tuần.

Thu nhập: 23.000–30.000đ/giờ tùy vị trí. Cuối tuần 25.000đ/giờ.
Thời gian: ca đầy đủ 9h–14h và 17h–23h. Bán thời gian: sáng 9h/10h–13h/14h; chiều 16h30–22h; tối 18h30–24h; khuya 23h đến hết khách.
Địa điểm: 373A Hoàng Văn Thụ và 101 Bắc Sơn, phường Phan Đình Phùng, Thái Nguyên.
Liên hệ: 0868619814, 0367827490.$s$,
  place_text = '373A Hoàng Văn Thụ và 101 Bắc Sơn, phường Phan Đình Phùng',
  contact_phone = '0868619814',
  salary_text = '23.000–30.000đ/giờ (cuối tuần 25.000đ/giờ)',
  working_time = 'Nhiều ca sáng–tối; có ca cuối tuần',
  employment_type = 'part_time',
  source_kind = 'admin_write',
  status = 'ACTIVE',
  published_at = COALESCE(published_at, NOW()),
  expires_at = COALESCE(expires_at, NOW() + INTERVAL '14 days'),
  last_checked_at = NOW(),
  updated_at = NOW()
WHERE id = '01a005d7-8c60-7b8f-a4f8-d58bb8e62a18';

UPDATE pack_local.listing SET
  title = 'Tuyển nhân viên bán trà sữa mang đi',
  summary = $s$Tuyển nhân viên bán trà sữa mang đi tại Thái Nguyên.

Thu nhập: bán thời gian 22.000đ/giờ; toàn thời gian 24.000đ/giờ; thử việc 20.000đ/giờ.
Thời gian: ca sáng 8h–13h; ca 8h–16h hoặc 8h–18h.
Địa điểm: 321 Lương Ngọc Quyến, Thái Nguyên.
Yêu cầu: từ 18 tuổi, trung thực, chủ động.
Liên hệ: 0357531523.$s$,
  place_text = '321 Lương Ngọc Quyến, Thái Nguyên',
  contact_phone = '0357531523',
  salary_text = 'Bán thời gian 22.000đ/giờ; toàn thời gian 24.000đ/giờ',
  working_time = 'Ca sáng 8h–13h; ca 8h–16h hoặc 8h–18h',
  employment_type = 'part_time',
  source_kind = 'admin_write',
  status = 'ACTIVE',
  published_at = COALESCE(published_at, NOW()),
  expires_at = COALESCE(expires_at, NOW() + INTERVAL '14 days'),
  last_checked_at = NOW(),
  updated_at = NOW()
WHERE id = '01a00825-3578-7c91-9993-92c3ce03b69e';

UPDATE pack_local.listing SET
  title = 'Tuyển nhân viên quán cà phê — toàn thời gian và bán thời gian',
  summary = $s$Quán cà phê tuyển 1 nhân viên toàn thời gian và 1 nhân viên bán thời gian.

Thu nhập: toàn thời gian 6,5–8 triệu + thưởng; bán thời gian 18.000–20.000đ/giờ.
Địa điểm: 165 đường Tân Thịnh, gần cổng phụ Cao đẳng Kinh tế, phường Quyết Thắng, Thái Nguyên.
Liên hệ: 0926281996.$s$,
  place_text = '165 Tân Thịnh, phường Quyết Thắng, Thái Nguyên',
  contact_phone = '0926281996',
  salary_text = 'Toàn thời gian 6,5–8 triệu + thưởng; bán thời gian 18.000–20.000đ/giờ',
  working_time = NULL,
  employment_type = NULL,
  source_kind = 'admin_write',
  status = 'ACTIVE',
  published_at = COALESCE(published_at, NOW()),
  expires_at = COALESCE(expires_at, NOW() + INTERVAL '14 days'),
  last_checked_at = NOW(),
  updated_at = NOW()
WHERE id = '01a005d6-787c-7db5-b9d8-d57e33cb02e9';

UPDATE pack_local.listing SET
  title = 'Tuyển barista và nhân viên phục vụ — Vert',
  summary = $s$Quán Vert tuyển barista / pha chế và nhân viên phục vụ, bán thời gian hoặc toàn thời gian.

Thu nhập: 18.000–22.000đ/giờ.
Địa điểm: đối diện 341 Phan Bội Châu, Thái Nguyên.
Yêu cầu: chưa có kinh nghiệm vẫn nhận; ưu tiên người sẵn sàng học việc.
Liên hệ: 0766408636.$s$,
  place_text = 'Đối diện 341 Phan Bội Châu, Thái Nguyên',
  contact_phone = '0766408636',
  salary_text = '18.000–22.000đ/giờ',
  working_time = 'Bán thời gian hoặc toàn thời gian',
  employment_type = 'part_time',
  source_kind = 'admin_write',
  status = 'ACTIVE',
  published_at = COALESCE(published_at, NOW()),
  expires_at = COALESCE(expires_at, NOW() + INTERVAL '14 days'),
  last_checked_at = NOW(),
  updated_at = NOW()
WHERE id = '01a00821-29d8-7339-b535-2007cfbe6daf';

UPDATE pack_local.listing SET
  title = 'Tuyển nhân viên bán hàng TokyoLife',
  summary = $s$TokyoLife tuyển nhân viên bán hàng tại Thái Nguyên và Phổ Yên. Không yêu cầu kinh nghiệm.

Thu nhập: 7–10 triệu/tháng.
Thời gian: 8h–16h hoặc 14h–22h.
Địa điểm: 670 Phạm Văn Đồng, Phổ Yên và 182 Lương Ngọc Quyến, Thái Nguyên.
Liên hệ: 0328943419.$s$,
  place_text = '670 Phạm Văn Đồng, Phổ Yên · 182 Lương Ngọc Quyến, Thái Nguyên',
  contact_phone = '0328943419',
  salary_text = '7–10 triệu/tháng',
  working_time = '8h–16h hoặc 14h–22h',
  employment_type = 'full_time',
  source_kind = 'admin_write',
  status = 'ACTIVE',
  published_at = COALESCE(published_at, NOW()),
  expires_at = COALESCE(expires_at, NOW() + INTERVAL '14 days'),
  last_checked_at = NOW(),
  updated_at = NOW()
WHERE id = 'b0000000-0000-7000-8000-000000000102';

UPDATE pack_local.listing SET
  title = 'Tuyển nhân viên bán thời gian — lẩu nướng Tít Mít',
  summary = $s$Quán lẩu nướng Tít Mít tuyển nhân viên bán thời gian. Giờ linh hoạt, phù hợp sinh viên.

Thu nhập: 23.000đ/giờ.
Thời gian: linh hoạt.
Địa điểm: chân đường Bắc Sơn, tổ 75 Phan Đình Phùng, Thái Nguyên.
Liên hệ: 0358487525.$s$,
  place_text = 'Chân đường Bắc Sơn, tổ 75 Phan Đình Phùng, Thái Nguyên',
  contact_phone = '0358487525',
  salary_text = '23.000đ/giờ',
  working_time = 'Linh hoạt',
  employment_type = 'part_time',
  source_kind = 'admin_write',
  status = 'ACTIVE',
  published_at = COALESCE(published_at, NOW()),
  expires_at = COALESCE(expires_at, NOW() + INTERVAL '14 days'),
  last_checked_at = NOW(),
  updated_at = NOW()
WHERE id = 'b0000000-0000-7000-8000-000000000104';

UPDATE pack_local.listing SET
  title = 'Tuyển nhân viên quán ăn ca tối',
  summary = $s$Quán ăn gần Trung tâm Học liệu tuyển nhân viên ca tối.

Thu nhập: 20.000đ/giờ.
Thời gian: 17h–22h30.
Địa điểm: cổng phụ Trung tâm Học liệu, Thái Nguyên.
Liên hệ: 0389226339.$s$,
  place_text = 'Cổng phụ Trung tâm Học liệu, Thái Nguyên',
  contact_phone = '0389226339',
  salary_text = '20.000đ/giờ',
  working_time = '17h–22h30',
  employment_type = 'part_time',
  source_kind = 'admin_write',
  status = 'ACTIVE',
  published_at = COALESCE(published_at, NOW()),
  expires_at = COALESCE(expires_at, NOW() + INTERVAL '14 days'),
  last_checked_at = NOW(),
  updated_at = NOW()
WHERE id = 'b0000000-0000-7000-8000-000000000103';

UPDATE pack_local.listing SET
  title = 'Tuyển nhân viên phục vụ — nhà hàng Sen Hồ',
  summary = $s$Nhà hàng Sen Hồ tuyển nhân viên phục vụ bán thời gian.

Thu nhập: 28.000đ/giờ.
Thời gian: 17h–22h, thứ Hai đến chủ nhật.
Địa điểm: nhà hàng Sen Hồ, Thái Nguyên.
Liên hệ: 0984660399.$s$,
  place_text = 'Nhà hàng Sen Hồ, Thái Nguyên',
  contact_phone = '0984660399',
  salary_text = '28.000đ/giờ',
  working_time = '17h–22h, T2–CN',
  employment_type = 'part_time',
  source_kind = 'admin_write',
  status = 'ACTIVE',
  published_at = COALESCE(published_at, NOW()),
  expires_at = COALESCE(expires_at, NOW() + INTERVAL '14 days'),
  last_checked_at = NOW(),
  updated_at = NOW()
WHERE id = '01a00458-a24a-76c7-aec6-f3a77af03b94';

-- Stay hidden: MLM-style bait, karaoke without address, test listing, duplicate Thượng Đỉnh.
UPDATE pack_local.listing SET last_checked_at = NOW(), updated_at = NOW()
WHERE id IN (
  '01a005d4-69ff-73a2-a7bf-d9f7f19939fc',
  'b0000000-0000-7000-8000-000000000105',
  '01a00085-1c11-7b7e-badc-57f41a2a3d76',
  'b0000000-0000-7000-8000-000000000101'
);

SELECT status, count(*) FROM pack_local.listing WHERE kind = 'job' GROUP BY 1 ORDER BY 1;
SELECT left(title, 70) AS title, status, source_kind
FROM pack_local.listing WHERE kind = 'job' AND status = 'ACTIVE' ORDER BY title;

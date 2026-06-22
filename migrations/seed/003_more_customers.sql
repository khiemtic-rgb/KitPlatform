-- Thêm khách hàng demo (POS) — an toàn chạy lại trên DB đã có KH001
INSERT INTO customers (id, tenant_id, customer_code, full_name, phone, email, date_of_birth)
VALUES
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02', '11111111-1111-1111-1111-111111111101', 'KH002', 'Nguyễn Văn An', '0909234567', 'an.nguyen@email.com', '1985-03-22'),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa03', '11111111-1111-1111-1111-111111111101', 'KH003', 'Lê Hoàng Bình', '0909345678', 'binh.le@email.com', '1978-11-08'),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa04', '11111111-1111-1111-1111-111111111101', 'KH004', 'Phạm Thu Hà', '0909456789', 'ha.pham@email.com', '1992-07-30'),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa05', '11111111-1111-1111-1111-111111111101', 'KH005', 'Hoàng Minh Đức', '0909567890', 'duc.hoang@email.com', '1988-01-14'),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa06', '11111111-1111-1111-1111-111111111101', 'KH006', 'Võ Thị Lan', '0909678901', 'lan.vo@email.com', '1995-09-03')
ON CONFLICT (tenant_id, customer_code) DO NOTHING;

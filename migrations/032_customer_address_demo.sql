-- Demo delivery address for OTP test customer (Trần Thị Mai)
INSERT INTO customer_addresses (
    customer_id, label, recipient_name, phone, address_line, ward, district, province, is_default
)
SELECT
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'Nhà',
    'Trần Thị Mai',
    '0909123456',
    '123 Nguyễn Huệ',
    'Bến Nghé',
    'Quận 1',
    'TP. Hồ Chí Minh',
    TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM customer_addresses
    WHERE customer_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'
);

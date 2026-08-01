-- 256: Customer avatar for customer-app profile
ALTER TABLE customers
    ADD COLUMN IF NOT EXISTS avatar_url TEXT;

COMMENT ON COLUMN customers.avatar_url IS
    'Relative upload path e.g. /uploads/avatars/{tenantN}/{accountN}/{file} — customer-app profile photo.';

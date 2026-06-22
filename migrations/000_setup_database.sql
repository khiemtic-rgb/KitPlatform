-- Chạy bằng user postgres (superuser)
-- Tạo user + database cho PharmaCore

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'pharmacore') THEN
        CREATE ROLE pharmacore WITH LOGIN PASSWORD 'pharmacore_dev_2026';
    END IF;
END $$;

ALTER ROLE pharmacore WITH LOGIN PASSWORD 'pharmacore_dev_2026';

SELECT 'CREATE DATABASE pharmacore OWNER pharmacore'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'pharmacore')\gexec

GRANT ALL PRIVILEGES ON DATABASE pharmacore TO pharmacore;

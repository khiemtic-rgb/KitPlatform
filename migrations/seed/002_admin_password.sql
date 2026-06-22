-- Cập nhật mật khẩu admin demo: Admin@123
UPDATE users
SET password_hash = '$2a$11$Oq8dLLVbqREcBk4VBW0ELOuBQneydTDK7VLpR9FcHEiQdWoUTQyJS'
WHERE username = 'admin';

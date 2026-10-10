-- OPTIONAL: hanya untuk environment DEV baru; isi user/email harus sesuai Entra ID sebelum login.
BEGIN;
INSERT INTO divisions(id,name) VALUES (1,'IT Division') ON CONFLICT(id) DO NOTHING;
INSERT INTO departments(id,division_id,name) VALUES
 (1,1,'Project'),(2,1,'Infrastructure'),(3,1,'IT Operations') ON CONFLICT(id) DO NOTHING;
-- Sesuaikan email dan mapping role sebelum membuat akun user;
-- tidak ada user palsu yang otomatis dibuat di database PostgreSQL.
SELECT setval(pg_get_serial_sequence('divisions','id'),GREATEST(1,(SELECT MAX(id) FROM divisions)));
SELECT setval(pg_get_serial_sequence('departments','id'),GREATEST(1,(SELECT MAX(id) FROM departments)));
COMMIT;

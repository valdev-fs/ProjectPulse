-- Schema-only upgrade for existing PostgreSQL v0.3. Run after backup.
-- Existing PRJ IDs require a separate, reviewed FK-safe backfill; not done automatically.
BEGIN;
ALTER TABLE users ADD COLUMN IF NOT EXISTS reports_to_pm_user_id integer REFERENCES users(id) ON DELETE RESTRICT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS is_active boolean NOT NULL DEFAULT TRUE;
ALTER TABLE projects ADD COLUMN IF NOT EXISTS timeline_owner_user_id integer REFERENCES users(id) ON DELETE RESTRICT;
ALTER TABLE projects ADD COLUMN IF NOT EXISTS requester_name text NOT NULL DEFAULT '';
ALTER TABLE projects ADD COLUMN IF NOT EXISTS requester_department text NOT NULL DEFAULT '';
ALTER TABLE projects ADD COLUMN IF NOT EXISTS current_status text NOT NULL DEFAULT 'Requirement';
CREATE TABLE IF NOT EXISTS req_counters (date_key text PRIMARY KEY,last_number integer NOT NULL CHECK(last_number BETWEEN 0 AND 999));
CREATE INDEX IF NOT EXISTS ix_users_reports_to_pm ON users(reports_to_pm_user_id);
COMMIT;

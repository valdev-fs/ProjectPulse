"""Dependency-free source contract + SQLite FK migration smoke (not .NET build)."""
import re
import sqlite3
from pathlib import Path
root=Path(__file__).resolve().parents[1]
c=(root/'backend/Program.cs').read_text()
model=(root/'backend/Models.cs').read_text()
ui=(root/'frontend/src/App.tsx').read_text()
sql=(root/'sql/001_postgresql_init.sql').read_text()
for fragment in ['"Admin" => db.Projects','/admin/users','/admin/projects/{projectId}/timeline-owner',
                 '/projects/{projectId}/current-status','/milestones/{milestoneId:int}/dates',
                 'TimelineOwnerUserId','RequesterDepartment','RequesterName','ReqCounters',
                 'IsProjectBA','DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7))']:
    assert fragment in c, f'Missing backend contract {fragment}'
for fragment in ['ReportsToPmUserId','IsActive','CurrentStatus','TimelineOwnerUserId','RequesterDepartment']:
    assert fragment in model
for fragment in ['User Administration','PortfolioCharts','CurrentStatusEdit','MilestoneDatesEdit',
                 'AdminUserEdit','TimelineOwnerEdit','HO Requester Name','REQ ID']:
    assert fragment in ui, f'Missing UI {fragment}'
for table in ['req_counters','reports_to_pm_user_id','timeline_owner_user_id','requester_department','current_status']:
    assert table in sql
for banned in ['PRJ-'+str(i).zfill(3) for i in range(1,7)]:
    assert banned not in (root/'backend/SeedData.cs').read_text()

# Representative relational FK smoke for an old v0.3 SQLite DB migrated in-place
conn=sqlite3.connect(':memory:')
conn.execute('PRAGMA foreign_keys=ON')
conn.executescript('''
CREATE TABLE projects(id TEXT PRIMARY KEY,start_date TEXT);
CREATE TABLE project_members(project_id TEXT REFERENCES projects(id),user_id INTEGER,PRIMARY KEY(project_id,user_id));
CREATE TABLE milestones(id INTEGER PRIMARY KEY,project_id TEXT REFERENCES projects(id));
CREATE TABLE tasks(id TEXT PRIMARY KEY,project_id TEXT REFERENCES projects(id));
CREATE TABLE issues(id TEXT PRIMARY KEY,project_id TEXT REFERENCES projects(id));
CREATE TABLE task_activities(id INTEGER PRIMARY KEY,project_id TEXT);
CREATE TABLE project_baselines(id INTEGER PRIMARY KEY,project_id TEXT);
INSERT INTO projects VALUES('PRJ-001','2026-10-01');
INSERT INTO project_members VALUES('PRJ-001',7);
INSERT INTO milestones VALUES(1,'PRJ-001');
INSERT INTO tasks VALUES('TSK-1','PRJ-001');
INSERT INTO issues VALUES('ISS-1','PRJ-001');
INSERT INTO task_activities VALUES(1,'PRJ-001');
INSERT INTO project_baselines VALUES(1,'PRJ-001');
''')
conn.commit()
conn.execute('PRAGMA foreign_keys=OFF')
with conn:
    for table in ['project_members','milestones','tasks','issues','task_activities','project_baselines','projects']:
        col='id' if table=='projects' else 'project_id'
        conn.execute(f'UPDATE {table} SET {col}=? WHERE {col}=?',('REQ20261001001','PRJ-001'))
conn.execute('PRAGMA foreign_keys=ON')
assert conn.execute('PRAGMA foreign_key_check').fetchall()==[]
assert conn.execute('SELECT project_id FROM tasks').fetchone()[0]=='REQ20261001001'
assert conn.execute('SELECT project_id FROM project_members').fetchone()[0]=='REQ20261001001'
print('PASS: backend/front-end/sql source contracts, SQLite legacy key migration & foreign keys')

# ProjectPulse v0.4 — Admin, Resource Borrowing & Executive Visuals

## Changes
1. Add **Admin** role and member create/edit/disable UI (`admin@example.test`, initial password `Demo@12345`). Admin can change BA home reporting PM independently of project membership.
2. BA `reports_to_pm_user_id` denotes home team and is separate from `project_members`. Example: Maya is assigned to home PM Budi (PM02), but participates in MCS & MCS HO owned by Rina (PM01).
3. PM can create projects with an initial timeline-owner BA, assign project BA members, and monitor. PM cannot create/edit milestones, change dates/progress, task status, project phase or health. **Timeline-owner BA** manages milestones, forecast, milestone progress, phase and health. **Assigned BA** manages own tasks/issues.
4. Project ID is generated as `REQYYYYMMDDNNN` using WIB date; unique SQL primary key and per-date database counter. Existing local `PRJ-...` keys migrate, including referencing tables.
5. `requester_name` + `requester_department` represent internal Head Office contacts with **no login account**.
6. `current_status` is distinct from `health`; e.g. Development + Blocked.
7. Portfolio charts on Executive, Department and PM pages: donut health, delivery pipeline by phase, delivery calendar by month.
8. Local SQLite database remains the default; initial fresh PostgreSQL schema updated. Separate schema-only PostgreSQL upgrade script is provided but does **not** rename pre-existing PRJ IDs.

## Access matrix

| Feature | Admin | Division/Dept Head | PM | Assigned BA | Timeline Owner BA |
|---|---|---|---|---|---|
| View projects in scope | Yes | Yes | Yes | Yes | Yes |
| Manage employee and BA home team | Yes | No | No | No | No |
| Assign borrowed BA | Yes | No | Own projects | No | No |
| Create project | No | No | Yes | No | No |
| Create/edit milestone/timeline | No | No | No | No | Yes |
| Update milestone progress / phase / health | No | No | No | No | Yes |
| Create own tasks / report issues | No | No | No | Yes | Yes |
| Update own tasks/issues | No | No | No | Yes | Yes |
| Change timeline owner | Yes | No | No | No | No |

## Current limitations

- Local demo password reset has no proof of identity and is intentionally disabled in hosted JWT mode.
- Project manager responsibility and reporting PM may differ. Changing a BA's home PM does not reassign their project tasks or timeline ownership.
- Admin deactivation does not automatically redistribute existing assignments. Reassign the timeline owner first.
- Project date change UI currently allows editing planned dates with a reason; a formal baseline approval workflow remains future work.
- Charts are client-side aggregations of authorized project records, not a separate analytics warehouse.
- Build was not run here because .NET SDK and frontend dependencies were not present in the artifact environment.

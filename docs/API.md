## Demo authentication v0.3 (localhost + SQLite Development only)

- `GET /api/auth/demo-users`: daftar user dummy untuk selector email di login (tanpa password hash).
- `POST /api/auth/login` body `{ "email": "ba01@example.test", "password": "Demo@12345" }` → `{ token, expiresAt }`.
- `POST /api/auth/reset` body `{ "email": "ba01@example.test", "newPassword": "NewDemo@123" }` → reset tanpa email, membatalkan seluruh session user. **TIDAK AMAN untuk dipublikasikan**.
- `POST /api/auth/logout`: Header `Authorization: Bearer <token>` → invalidate session token.
- Semua `/api/*` selain auth/health butuh `Authorization: Bearer <token>` lokal.
- Token disimpan sementara di `sessionStorage`. Server menyimpan hanya SHA256 hash token.
- Pada Postgres mode `Jwt`, demo endpoints di atas **tidak ada**; autentikasi via bearer JWT dari Entra ID. Client MSAL masih pekerjaan lanjutan.

# API contract — v0.2

**Base URL lokal:** `http://127.0.0.1:5088/api`

Set header `X-Demo-User: 7` untuk demo **loopback SQLite saja**. Untuk PostgreSQL development wajib `Authorization: Bearer <JWT>` valid serta pemetaan email terverifikasi ke `users.email`.

| Method | Endpoint | Role yang dapat menggunakan |
|---|---|---|
| GET | `/health` | Public health check |
| GET | `/me` | Semua role |
| GET | `/users` | Semua role, users 1 divisi |
| GET | `/departments` | Semua role, departemen 1 divisi |
| GET | `/portfolio` | Div/Dept Head: scope masing-masing; PM/BA: project assigned |
| GET | `/projects` | Scope masing-masing |
| GET | `/projects/{projectId}` | Scope masing-masing; milestone/task/issue/activity |
| POST | `/projects` | PM (project baru) |
| POST | `/projects/{projectId}/members` | PM owner project |
| POST | `/projects/{projectId}/milestones` | PM owner project |
| PATCH | `/milestones/{milestoneId}/progress` | PM owner project |
| PATCH | `/projects/{projectId}/health` | PM owner project, wajib alasan |
| GET | `/tasks/my` | Semua role (task owner/contributor pada project visible) |
| POST | `/projects/{projectId}/tasks` | PM atau BA member (BA owner harus dirinya sendiri) |
| PATCH | `/tasks/{taskId}` | PM atau BA owner/contributor; `version` wajib |
| POST | `/projects/{projectId}/issues` | PM/BA member |
| PATCH | `/issues/{issueId}` | PM atau issue owner |

## Sample request (PowerShell Windows)

```powershell
$base = "http://127.0.0.1:5088/api"
$headers = @{ "X-Demo-User"="7" }
Invoke-RestMethod "$base/me" -Headers $headers
Invoke-RestMethod "$base/projects" -Headers $headers
Invoke-RestMethod "$base/tasks/my" -Headers $headers
```

Update task existing:

```powershell
$body = @{ status="In Progress"; version=1; note="Review source selesai" } | ConvertTo-Json
Invoke-RestMethod "$base/tasks/TSK-101" -Method Patch -Headers $headers -ContentType "application/json" -Body $body
```

HTTP 409 berarti `version` task tidak sesuai versi terbaru. Reload task, review perubahan orang lain, lalu kirim versi terbaru. Berbeda dari success; jangan retry buta.

Project baru:

```json
{
  "name": "My New Project",
  "description": "Example project",
  "departmentId": 1,
  "startDate": "2026-10-12",
  "baselineEnd": "2026-12-15"
}
```

Create milestone:

```json
{
  "name": "Design",
  "weight": 15,
  "plannedStart": "2026-10-12",
  "plannedEnd": "2026-10-19"
}
```

Update health:

```json
{"health":"At Risk","reason":"Forecast UAT diperkirakan mundur 3 hari akibat isu data quality."}
```

## Response shape

JSON menggunakan camelCase (`projectId`, `dueDate`, `ownerUserId`, ...). DB PostgreSQL menggunakan snake_case; mapping EF Core ditangani backend.

## Keterbatasan API v0.2

Belum ada pagination, rate limits, background jobs, idempotency key, attachment storage, baseline approval, atau JWT provisioning UI. `POST /projects` tidak membuat milestones otomatis. `GET /portfolio` agregasi in-memory sesuai project scope; untuk volume besar perlu read model/materialized query tersendiri.


## v0.4 API and access changes
- `GET /api/admin/users` (Admin): active/inactive user list with reporting PM (no password hashes).
- `POST /api/admin/users` (Admin): `{fullName,email,role,departmentId,reportsToPmUserId}`; initial local password `Demo@12345`, hashed per account.
- `PATCH /api/admin/users/{id}` (Admin): includes `isActive` and reporting PM; home team changes do not modify `project_members`.
- `POST /api/admin/projects/{reqId}/members` (Admin): assign BA as a borrowed resource.
- `PATCH /api/admin/projects/{reqId}/timeline-owner` (Admin): `{userId}` of assigned active BA.
- `POST /api/projects` (PM): `{name,description,departmentId,startDate,baselineEnd,timelineOwnerUserId,requesterName,requesterDepartment}`; returns `REQYYYYMMDDNNN`.
- `POST /api/projects/{reqId}/members` (owning PM): assign active BA; does not modify their home reporting line.
- `POST /api/projects/{reqId}/milestones` (timeline-owner BA only): milestone, weight and plan.
- `PATCH /api/milestones/{id}/dates` (timeline-owner BA only): `{plannedStart,plannedEnd,forecastEnd,reason}`.
- `PATCH /api/milestones/{id}/progress` (timeline-owner BA only): `{progress}`.
- `PATCH /api/projects/{reqId}/current-status` (timeline-owner BA only): `{currentStatus,note}`.
- `PATCH /api/projects/{reqId}/health` (timeline-owner BA only): `{health,reason}`.
- `GET /api/projects` and `GET /api/portfolio`: now include `currentStatus`, `requesterName`, `requesterDepartment`, `timelineOwnerUserId`.
- `PATCH /api/tasks/{id}`: assigned BA owner/contributor may edit status and due date, with optimistic concurrency version.

**Current status:** Requirement, Design, Development, SIT, UAT, Implementation, PIR, On Hold, Completed.
**REQ ID:** primary key 14 characters, using WIB date and a 3-digit daily sequence. A duplicate sequence race results in 409 and can be retried.

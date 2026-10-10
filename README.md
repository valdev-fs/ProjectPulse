# ProjectPulse — Starter MVP v0.4 (Admin + BA Timeline + REQ ID)

Project portfolio monitoring: **BA → Project Manager → Department Head → Division Head**.

**Status:** source-code starter yang belum diuji build end-to-end (lingkungan pembuat artifact tidak mempunyai .NET SDK maupun koneksi paket npm/NuGet). Disediakan prototype HTML standalone yang dapat dibuka tanpa instalasi. **Bukan versi production-ready.**

## Stack

| Lingkungan | Frontend | Backend | Database | Login |
|---|---|---|---|---|
| Local Windows (sekarang) | React 19 + TS + Vite 7 | ASP.NET Core / .NET 10 | **SQLite file `backend/Data/projectpulse.db`** | **Login email/password demo, loopback saja** |
| Server development (nanti) | React build | ASP.NET Core / .NET 10 | **PostgreSQL 17+** | JWT / Microsoft Microsoft Entra ID (integrasi frontend MSAL belum dibuat) |

Prinsip: backend EF Core memakai provider `Sqlite` atau `Postgres` melalui konfigurasi. Business API tidak diganti. **Data SQLite tidak secara otomatis berpindah ke PostgreSQL.** Skema PostgreSQL dibuat lewat skrip SQL tersendiri; migrasi data perlu prosedur ETL / import terpisah.

## 1. Menjalankan lokal Windows 11

Prerequisite:
- **.NET SDK 10** (`dotnet --version`)
- **Node.js 22.12+** (`node --version`; 22.16+ sudah memadai)
- npm (bundled with Node.js)

Ekstrak ZIP ke contoh `C:\work\ProjectPulse`. Buka **dua Windows Terminal / PowerShell**.

Terminal A — backend:

```powershell
cd C:\work\ProjectPulse\backend
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://127.0.0.1:5088"
dotnet restore
dotnet run --no-launch-profile
```

Backend akan membuat file SQLite `backend\Data\projectpulse.db` dan data contoh pada **start pertama**. Tes di browser: <http://127.0.0.1:5088/api/health>.

Terminal B — frontend:

```powershell
cd C:\work\ProjectPulse\frontend
npm.cmd install
npm.cmd run dev
```

Buka <http://127.0.0.1:5173>. Proxy Vite meneruskan `/api` ke backend port 5088.

**Jangan** membuka `frontend/index.html` langsung sebagai file; frontend React membutuhkan Vite/API. Untuk demo tanpa backend, gunakan `prototype/index.html` (UI konsep yang sebelumnya dibuat, data tersimpan di localStorage).

### Login demo lokal (v0.4)

Akses `http://127.0.0.1:5173`. Tampilan pertama sekarang **login page**, bukan dashboard dengan dropdown role. Klik nama akun demo untuk mengisi email, masukkan password, lalu Sign In. Dashboard dipilih dari `role` user yang diverifikasi API. Logout tersedia di sidebar.

**Password awal SEMUA akun: `Demo@12345`**. Ini hanya password dummy yang ditetapkan saat seed. Di database, password **tidak disimpan sebagai plaintext atau reversible encryption**, melainkan PBKDF2-HMAC-SHA256 (210.000 iterasi, random salt per user). Session lokal memakai token 256-bit acak; hanya hash SHA256 token yang disimpan di SQLite, kedaluwarsa setelah 8 jam; disimpan di `sessionStorage` browser dan dihapus saat logout.

| Email | Nama (dummy) | Role | Lingkup |
|---|---|---|---|
| admin@example.test | Fajar Administrator | Admin | User management and BA reporting line |
| division@example.test | Arman Prasetyo | DivisionHead | Semua departemen IT Division |
| head.project@example.test | Dimas Hartono | DepartmentHead | Department Project |
| head.infrastructure@example.test | Citra Rahmawati | DepartmentHead | Infrastructure |
| head.operations@example.test | Rahmat Hakim | DepartmentHead | IT Operations |
| pm01@example.test | Rina Anggraini | PM | Project yang dipimpin |
| pm02@example.test | Budi Setiawan | PM | Project yang dipimpin |
| ba01@example.test | Andi Pratama | BA | Project/task sesuai assignment |
| ba02@example.test | Sari Dewi | BA | Project/task sesuai assignment |
| ba03@example.test | Maya Lestari | BA | Project/task sesuai assignment |
| ba04@example.test | Nadia Putri | BA | Akun tambahan (belum ada task) |

**Organisasi dummy**: IT Division → Department Project → Project Management (PM) → Business Analyst (BA). Ada department dummy lain (Infrastructure dan IT Operations) agar helicopter view punya contoh lintas-department. Level PM dan BA adalah penugasan role pada unit, bukan tabel departemen terpisah.

**Reset password lokal**: klik “Lupa password? Reset untuk demo”, masukkan email akun dan password baru (minimum 10 karakter), konfirmasi, lalu submit. Reset berhasil akan membatalkan semua session aktif akun tersebut. **Tidak ada verifikasi email atau identitas**, sehingga siapa pun yang tahu alamat akun demo bisa meresetnya. Ini **sengaja hanya untuk demonstrasi lokal**; jangan menggunakannya di jaringan atau production. Endpoint reset/login demo tidak dimap sama sekali ketika mode `Jwt` dipakai. Login dan reset dibatasi frekuensinya per alamat IP (8 request/menit, termasuk daftar akun demo).

**Upgrade dari v0.2**: Jika sudah ada `Data/projectpulse.db`, backend otomatis menambah kolom password hash dan tabel session tanpa menghapus task/project, lalu mengisi credential dummy untuk akun lama. Email akun v0.2 yang lama (mis. `head.data@example.test`) **tetap dipertahankan** agar existing data konsisten. Halaman login mengambil daftar akun yang benar-benar ada di DB, jadi gunakan email yang ditampilkan di situ. Reset database lokal jika ingin akun/email yang sama persis seperti tabel di atas.

### Yang baru di v0.4

- **Admin User Administration**: add/edit/disable member; change home PM team of an assigned BA without deleting project assignments; assign BA on project and reassign timeline owner.
- **Borrowing BA**: if Maya reports to Budi, Rina can still assign Maya to an MCS project in Rina's portfolio. A home team change does not affect project membership.
- **PM**: create project and choose the BA timeline owner; assign additional BAs (no milestone/progress update permission).
- **Timeline-owner BA**: owns milestone creation, planned/forecast date changes, phase updates, milestone progress and health. Assigned BAs manage their own tasks/issues.
- **REQ ID**: `REQYYYYMMDDNNN` using WIB creation date and a daily DB counter. New SQL PK remains unique. For example `REQ20261010001`.
- **HO requester**: name and department, informational only, **not a login account**.
- **Current Status**: Requirement, Design, Development, SIT, UAT, Implementation, PIR, On Hold, Completed. Separate from Health.
- **Executive charts**: health donut, project phases / pipeline, monthly delivery calendar at PM, Dept Head, Division Head.

**Existing local SQLite data:** Before first v0.4 startup, stop backend and **backup** `backend/Data/projectpulse.db` (and related WAL files if present). Bootstrapping upgrades columns and converts legacy `PRJ-*` project PKs plus dependent foreign keys into `REQ*`; normal task/project data is preserved. The upgrade is **not** a substitute for a regular production migration framework.

**PostgreSQL development:** Use latest `sql/001_postgresql_init.sql` for a fresh DB. The `sql/003_postgresql_upgrade_v04.sql` is schema-only; it intentionally does not rekey old `PRJ-*` data. Hosted authentication requires production Microsoft Entra ID integration, not the local demo login/reset.

See `docs/CHANGELOG_v0.4.md` for the authorization matrix and full API changes.

### Branding / logo yang bisa diganti

Semua asset berada di `frontend/public/branding/`:
- `logo.svg` — master logo (vektor, rekomendasi mengganti file ini untuk sidebar dan login).
- `logo.png` — logo raster resolusi tinggi.
- `favicon.ico` — ikon tab browser (16, 32, 48, 64, 128, 256 pixel).

Untuk rebranding, ganti file-file itu dengan nama yang sama, lalu refresh halaman (hard reload jika favicon masih di-cache). Warna tema dikendalikan CSS di `frontend/src/style.css`. Tidak ada hardcode logo di komponen.

### Reset database lokal

Untuk mengulang seed dari nol: **matikan backend terlebih dahulu**, backup data bila perlu, lalu hapus `backend\Data\projectpulse.db` dan file journal/WAL jika ada. Saat backend dijalankan, skema & seed dibuat lagi. `EnsureCreated` tidak menerapkan schema migrations pada DB yang sudah ada.

## 2. MVP scope & limitations

Implemented in source: project portfolio per role, BA task/issue updates, admin member management and team reassignment, project resource borrowing, REQ ID, HO requester, current-status phase, BA timeline and progress updates, portfolio charts, local session/password demo, PostgreSQL schema.

Not production-ready: end-to-end .NET build validation, SSO/MSAL wiring, approval workflow for baseline changes, audit-independent admin action logs, critical path, attachments, scheduler and notification service. The hosted environment cannot use local password login; enable JWT and implement frontend MSAL flow before deployment.

## 3. Deploy ke PostgreSQL development (nanti)

**Tidak perlu melakukan langkah ini sekarang.**

1. Minta DBA menyediakan database/role PostgreSQL dan SSL; jalankan `sql/001_postgresql_init.sql` satu kali pada database **baru**, misalnya:
   ```powershell
   psql "host=DB_HOST dbname=projectpulse user=SCHEMA_OWNER sslmode=require" -v ON_ERROR_STOP=1 -f sql/001_postgresql_init.sql
   ```
2. Opsional, jalankan `sql/002_postgresql_initial_master.sql` (division/department contoh) lalu masukkan `users.email` **sesuai identity perusahaan yang terverifikasi**, beserta role dan department. Admin CRUD endpoints exist; hosted authentication still requires Entra integration.
3. Set environment variable pada **backend server**, bukan di frontend/browser:
   ```powershell
   $env:Database__Provider="Postgres"
   $env:ConnectionStrings__Postgres="Host=DB_HOST;Port=5432;Database=projectpulse;Username=app_user;Password=SECRET;SSL Mode=Require"
   $env:Security__Mode="Jwt"
   $env:Security__JwtAuthority="https://login.microsoftonline.com/TENANT_ID/v2.0"
   $env:Security__JwtAudience="api://API_APP_CLIENT_ID"
   ```
4. Register Web/API pada Microsoft Entra ID. Configure allowed token scopes, authority and audience. **Frontend belum menyertakan MSAL login**; tambahkan before exposing app to users. Ubah frontend `api.ts` untuk mengirim bearer token, **bukan** header demo. Jangan menggunakan mode `Local` di server PostgreSQL; backend sengaja menolaknya.
5. Deploy backend via IIS/container/reverse proxy HTTPS, frontend static files. Batasi network/security group ke PostgreSQL, buat backup, audit, dan secrets di secret manager.
6. Siapkan migration pipeline dan validasi parity SQLite/PostgreSQL. SQL baseline di folder ini **bukan** auto-migration untuk database berisi data. Gunakan EF Core migrations terpisah per provider atau versioned SQL scripts dengan DBA review. Rujukan: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers

**Catatan penting:** NuGet Npgsql/EF Core 10 digunakan agar support .NET 10; jangan campur major version EF Core 9 vs 10. Tidak ada password sebenarnya di source.

## 4. Aturan bisnis inti

- `Task.owner_user_id`: satu PIC utama; `task_contributors`: lebih dari satu contributor.
- `project_members`: multi-project & multi-BA.
- BA yang ditugaskan bisa mengelola task miliknya atau task tempat ia menjadi contributor. PM membuat project, memilih BA timeline owner dan menambahkan BA; Dept/Division Head read-only.
- Semua izin project dicek **pada backend** di setiap endpoint, bukan melalui penyembunyian menu saja.
- Progress task **bukan** sumber langsung progress project. BA timeline owner memutakhirkan milestone; progress project = Σ(weight × progress)/100 ketika total bobot 100.
- Health project (`On Track / At Risk / Delayed / Blocked`) memerlukan evaluasi dan alasan BA timeline owner; tidak otomatis hijau karena task banyak Done.
- Baseline & forecast dua nilai berbeda. **Baseline approval formal belum dibangun**.
- `Task.Version` optimistic concurrency pada PATCH. Activity log mencatat perubahan task, health, current status dan timeline.

## 5. Struktur proyek

```text
ProjectPulse/
├─ backend/
│  ├─ Program.cs                 # endpoint, identity, role/scope authorization
│  ├─ Models.cs                  # domain entities
│  ├─ AppDbContext.cs            # EF Core mapping & snake_case columns
│  ├─ SeedData.cs                # sample local SQLite
│  ├─ LocalUpgrade.cs            # v0.2/v0.3 local SQLite compatibility upgrade
│  ├─ ProjectPulse.Api.csproj
│  └─ appsettings.json
├─ frontend/
│  ├─ package.json
│  └─ src/ (React UI, API adapter, CSS)
├─ sql/
│  ├─ 001_postgresql_init.sql
│  ├─ 002_postgresql_initial_master.sql
│  └─ 003_postgresql_upgrade_v04.sql
├─ docs/ERD.md
├─ docs/API.md
├─ docs/UI_UX_BLUEPRINT.md
├─ prototype/index.html          # HTML UX prototype standalone
└─ docker-compose.postgres.yml   # opsional; PostgreSQL test, bukan deployment utama
```

## 6. Urutan implementasi berikutnya

1. Build + smoke-test backend dan frontend pada PC Windows yang punya SDK, perbaiki kalau ada masalah compile/runtime.
2. Integrasi login Entra ID/MSAL, provisioning user, role permission matrix yang final, cross-department sharing policy.
3. Versioned migrations + approval workflow baseline, evidences, notifikasi, data freshness dan KPI resmi.
4. Automated tests, audit append-only, observability, load testing, packaging deployment DEV.

**Blueprint UI awal** tetap ada di `docs/UI_UX_BLUEPRINT.md`. Perubahan di source MVP v0.4 adalah implementasi engineering awal, bukan jaminan seluruh UX prototype sebelumnya sudah 1:1 terhubung API.

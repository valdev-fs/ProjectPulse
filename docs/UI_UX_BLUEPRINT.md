# ProjectPulse — UI/UX Blueprint (v0.1)

**Tipe produk:** Enterprise Project Portfolio Management / Project Monitoring System  
**Target pengguna:** Business Analyst, Project Manager, Department Head, Division Head  
**Tahap:** UI/UX concept dan functional prototype, belum production-ready  
**Demo data:** fiktif dan hanya untuk eksplorasi interface

## 1. Sasaran produk

Satu aplikasi untuk input dan pemeliharaan task, milestone, issue, serta timeline oleh tim project. Pimpinan memperoleh satu sumber data yang dapat ditelusuri dari agregasi divisi hingga detail task individual.

### Prinsip produk
1. **Single source of truth:** dashboard merupakan agregasi dari data operasional, bukan input laporan lain.
2. **Drill-down:** Division → Department → Project → Milestone → Task / Issue.
3. **Traceability:** setiap progress, penggeseran baseline, dan penyelesaian issue memiliki audit trail.
4. **Accountability:** setiap task memiliki satu PIC utama dan dapat memiliki banyak contributor.
5. **Evidence-based health:** health project mempertimbangkan keterlambatan, blocker, dan freshness data.
6. **Multi-assignment:** satu BA bisa berada pada banyak project; satu project bisa memiliki banyak BA.

## 2. Navigasi dan tampilan

| Layar | Sasaran user | Elemen utama | Aksi utama |
|---|---|---|---|
| Portfolio Overview | Division Head | Portfolio KPI, health by department, critical attention, all projects | Drill-down project, export |
| Department Monitoring | Department Head | KPI satu department, daftar project dan issue aktif | Filter department, drill-down project |
| PM Workspace | Project Manager | Project delivery, overdue task, issue queue, task register | Review deadline, buka / update task |
| My Tasks | Business Analyst | Kanban To Do / In Progress / Done, overdue, assigned tasks | Update status, PIC, due date, catatan; tambah task |
| Project Detail | Semua sesuai akses | Project health, milestone, Gantt, task register, issue register | Update task, tambah issue, resolve issue |

### Pola navigasi
- Sidebar berisi 5 workspace pada prototype agar semua layar bisa dieksplorasi.
- Pada aplikasi real, menu yang terlihat disesuaikan dengan permission.
- Membuka project dari dashboard mempertahankan asal navigasi untuk tombol kembali.
- Pencarian global harus mendukung project dan task sesuai scope akses user.
- Pada setiap tabel penting, tersedia mekanisme drill-down yang konsisten.

## 3. Hierarki organisasi & ownership

```text
Division
  └── Department
       └── Project
            ├── ProjectMembers (PM, BA, Developer, QC, stakeholder)
            ├── Milestones
            │    └── Tasks
            │         └── TaskAssignments (PIC utama + contributor)
            ├── Issues / Risks
            ├── ProjectBaselines
            └── ActivityLog / AuditLog
```

- Project memiliki satu accountable PM utama. Co-PM atau acting PM bisa ditambahkan di fase berikutnya.
- Task memiliki satu accountable owner; contributor tidak mengubah owner.
- Pengguna dapat memiliki lebih dari satu peran dan lebih dari satu scope organisasi.
- Visibilitas tidak selalu hanya department pembuat project: **cross-department sharing** perlu tabel ProjectAccess / ProjectStakeholders.

## 4. RBAC awal

| Permission | BA | PM | Department Head | Division Head |
|---|---|---|---|---|
| Lihat project yang ditugaskan | ✓ | ✓ | ✓ | ✓ |
| Update task yang menjadi tanggung jawab | ✓ | ✓ | Opsional | Opsional |
| Buat project | Request / opsional | ✓ | Opsional | Opsional |
| Ubah baseline project | Request approval | Request approval | Approve (kebijakan) | Approval strategis (kebijakan) |
| Buat issue / kendala | ✓ | ✓ | ✓ | ✓ |
| Resolve issue | Owner / PM | ✓ | Escalated only | Escalated only |
| Lihat portfolio department | Sesuai akses | Sesuai akses | ✓ | ✓ |
| Lihat seluruh portfolio divisi | Tidak | Tidak | Tidak | ✓ |

**Catatan implementasi:** permission di atas adalah usulan awal; authority final perlu validasi proses bisnis. API backend wajib mengecek scope project dan organisasi untuk setiap request; bukan sekadar menyembunyikan menu pada frontend.

## 5. Kebutuhan data minimal

### Project
`project_id`, `project_name`, `department_id`, `division_id`, `pm_user_id`, `description`, `priority`, `start_date`, `baseline_end_date`, `forecast_end_date`, `actual_end_date`, `lifecycle_status`, `health_status`, `created_at`, `updated_at`.

### Milestone
`milestone_id`, `project_id`, `name`, `weight_percent`, `baseline_start`, `baseline_end`, `forecast_start`, `forecast_end`, `actual_start`, `actual_end`, `completion_percent`, `owner_user_id`, `status`.

### Task
`task_id`, `project_id`, `milestone_id`, `title`, `description`, `owner_user_id`, `priority`, `status`, `baseline_due`, `current_due`, `completed_at`, `estimated_hours`, `actual_hours`, `created_at`, `updated_at`, `row_version`.

### Assignment & Activity
`project_members(project_id, user_id, role, joined_at, left_at)`; `task_assignments(task_id, user_id, assignment_type)`; `task_activities(task_id, actor_id, old_value, new_value, note, occurred_at)`.

### Issue
`issue_id`, `project_id`, `task_id?`, `title`, `severity`, `impact`, `action_plan`, `owner_user_id`, `due_at`, `escalation_level`, `status`, `resolved_at`.

### Baseline & Audit
`project_baselines(project_id, version, approved_by, approved_at, reason, snapshot_json)`; `audit_log(actor_id, entity_type, entity_id, action, before_json, after_json, timestamp)`.

## 6. Rules yang perlu disepakati bersama stakeholder

- **Overdue task:** due date telah lewat dan task bukan Done / Cancelled.
- **Milestone progress:** dihitung dari deliverable yang accepted, bukan rata-rata mentah dari task. Ada approval checkpoint.
- **Project progress:** `Σ (milestone_weight × milestone_completion) / Σ milestone_weight`; seluruh milestone memiliki total weight 100% dan hanya progress yang tervalidasi boleh masuk KPI executive.
- **Project health:** status utama tersendiri (On Track / At Risk / Delayed / Blocked). Rekomendasi evaluasi: keberadaan blocker critical, lateness critical path, forecast slip, issue severity dan overdue.
- **Stale data:** tampilkan indikator jika tidak ada update dalam N hari kerja (N perlu disepakati, misalnya 3).
- **Baseline vs forecast:** baseline immutable per versi dan revisi harus disetujui; forecast boleh dinamis tanpa menghapus baseline historis.
- **Task completion:** BA dapat menandai Done, tetapi sign-off milestone ditentukan oleh PM atau approver yang sah.
- **Issue escalation:** SLA berdasarkan severity dan dampak, dengan owner dan target resolution yang wajib.

> Jangan menetapkan project health hanya berdasarkan persentase completion. Status kesehatan proyek perlu policy resmi dan log audit.

## 7. Jalur interaksi contoh

1. **BA update:** My Tasks → pilih task → edit status / catatan → Save → activity recorded → PM queue berubah → portfolio read model ikut terhitung ulang.
2. **PM investigation:** PM Workspace → overdue task → lihat project/milestone → review reschedule request → eskalasi issue.
3. **Head monitoring:** Department Dashboard → filter delayed → pilih project → periksa milestone penyebab deviasi → lihat owner task.
4. **Executive intervention:** Portfolio Overview → Blocked → buka Critical Attention → baca dampak & action plan → arahkan eskalasi ke owner.

## 8. UI design system

- Background: `#F5F7FB`; sidebar: `#101C32`; primary: `#366BEF`.
- Health: On Track hijau, At Risk amber, Delayed oranye/merah, Blocked merah.
- Typography: system/Inter-like; 14px body, 27px page heading, 12px table cells.
- Card radius: 13px; spacing grid: 18px; full-width layout responsif.
- Always present status in text *and* color (aksesibilitas).
- Consistent names and date formatting `DD MMM YYYY`.

## 9. Urutan desain dan implementasi

**Desain lanjutan**
1. Review lima layar dan finalisasi KPI/role.
2. Validasi entity & access rules dari proses internal.
3. Prototype alur approval baseline, project creation, notifications, attachment/evidence.
4. Wireframe mobile / tablet; design QA.

**MVP engineering**
1. SSO Entra ID, user/role/org scope.
2. Project + project members + milestones.
3. Task assignment, task update, activity history.
4. Issue & escalation.
5. Aggregation dashboard + drill-down.
6. Gantt baseline/forecast + notification + audit.

## 10. Batas prototype HTML

- Data demo dipakai untuk komunikasi desain, bukan live API.
- Simulasi perubahan disimpan di localStorage browser, dapat di-reset; tidak ada database atau authentication.
- Progress project dan Gantt adalah ilustrasi **dan tidak dihitung otomatis dari task demo**.
- Tombol role switcher untuk mengeksplorasi layar, bukan enforcement keamanan.
- Belum mencakup approvals, activity realtime multi-user, file attachment, notification delivery, audit persisten, atau export dengan policy izin.

# ProjectPulse v0.3 — Demo Login & Rebrand

## Perubahan
- Login page responsive: email/password login, demo account selector, info organisasi IT Division → Department Project → PM → BA.
- Akses berdasarkan user nyata pada session backend, dropdown `X-Demo-User` tidak digunakan lagi.
- 10 user fiktif dengan shared initial password `Demo@12345`; hanya password hash PBKDF2-HMAC-SHA256 bersalt yang disimpan di SQLite.
- Sesi demo pakai bearer token acak dan hashed session tokens dengan expiry 8 jam; logout invalidate token.
- Reset password tanpa email khusus untuk localhost demo, invalidates semua session user dan dibatasi rate-limit.
- Skema SQLite v0.2 di-upgrade otomatis (menambah kolom password_hash dan tabel local_sessions) tanpa menghapus project/task existing.
- Master demo baru: IT Division, Project, Infrastructure, IT Operations. Existing user emails preserved when upgrading old DB.
- Logo `P` + pulse: SVG, PNG, favicon ICO multi-size, replaceable under frontend/public/branding.
- PostgreSQL tetap dipakai di development deployment setelah Entra ID/MSAL siap. **Mode lokal tidak bisa dijalankan dengan PostgreSQL**.

## Belum diuji
- `dotnet build` / backend run (SDK tidak tersedia di lingkungan paket builder).
- React `npm install` / production build (registry dependencies tidak tersedia dalam lingkungan paket builder).
- Browser end-to-end (backend tidak dapat berjalan di lingkungan paket builder).

## Peringatan keamanan
Fitur reset publik tanpa email ini **hanya untuk localhost & data dummy**. Jangan dipindahkan ke environment development/production. Development PostgreSQL butuh login enterprise JWT dan belum ada integrasi frontend MSAL.

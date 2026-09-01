# QA Project Review Report: minirouter

**Review Date:** 2026-08-29 06:30 WIB
**Reviewed By:** Quinn (Test Architect) — @qa
**Scope:** Full project review (task `qa-review-build.md` adapted to repo scope — [AUTO-DECISION] no storyId given; all 10 phases executed where applicable)
**Signal:** **CONCERNS**

---

## Executive Summary

Build hijau, 104/104 tests lulus, format bersih, dan dashboard SPA berfungsi. Namun ada 1 regresi fitur pada story berstatus Done (8.7 cost persistence tidak pernah dieksekusi di production path), 1 config deployment yang patah (docker-compose port mismatch), 1 celah auth pada management plane saat di-deploy remote, dan 2 dependency vulnerability (NuGet HIGH + npm HIGH).

## Phase Results

| Phase | Result | Evidence |
|---|---|---|
| 0 Context | OK | git log 0a6baf1..; AIOX config loaded; 21 story files |
| 1 Subtasks | OK (note) | Latest story dashboard-navigation.1.1 In Review→Done (uncommitted); untracked `plan/` |
| 2 Build | SUCCESS | `dotnet build` 0 error, 2 warning (NU1903 ×2) |
| 3 Tests | PASS | dotnet 84/84 (19s); vitest 20/20 (4 files) |
| 4 Browser | PASS | SPA render OK, hash router `#/analytics` OK, `/health` 200, console bersih (no error overlay) |
| 5 Database | OK | SQLite migrations idempotent (catch SqliteErrorCode==1); live DB queryable |
| 6 Code/Security | CONCERNS | 2 dependency vuln HIGH; management API + /_shutdown unauth (remote exposure); SQL parameterized penuh; masking OK; AOT warnings |
| 7 Regression | PASS* | Semua suite lulus; *kecuali regresi Story 8.7 (lihat F-1) |

## Findings

### F-1 — MAJOR (missing AC implementation, story Done)
**Story 8.7 "Done (2026-08-12)": cost tidak pernah dihitung/di-persist end-to-end.**
- `Services/PricingTable.cs:34` `CalculateCost()` — **0 production caller** (hanya Tests/PricingTableTests.cs).
- `Services/ProxyService.cs` — 0 referensi `Cost`; semua site `new RequestLog` (line 470, 515, 633) tidak set `Cost`/`CostEstimated`.
- `Services/LogService.cs:105-110` — INSERT `request_log` **tidak menyertakan kolom `cost`/`cost_estimated`**, sementara parameter `$cost`/`$cost_estimated` (line 120-121) dibuat tapi tak terpakai (dead binding).
- Read path `LogService.cs:179-180, 219-220` + `/api/analytics/*` → selalu null/0. Dashboard cost selalu kosong.
- Story file claim "ProxyService.cs (Modified — compute cost at log time)" tidak akurat terhadap kode saat ini (kemungkinan hilang saat refactor proxy, story 1.4/8.x).
- Verifikasi live: `/api/logs?limit=3` → semua `cost:null` (konsisten; tidak konklusif sendiri karena semua baris failure).

### F-2 — MAJOR (deployment broken)
**docker-compose port mismatch.** `docker-compose.yml` maps `5050:5000`, tapi container listen di `8080` (Dockerfile: `ENV ASPNETCORE_URLS=http://+:8080`, `EXPOSE 8080`). Workflow docker yang didokumentasikan tidak akan reachable di host:5050.

### F-3 — HIGH (security, deployment-dependent; CONFIRMED by code)
**Management plane + remote shutdown tanpa auth.** `POST /_shutdown` (Program.cs:571) memanggil `StopApplication()` tanpa filter; `/api/providers` CRUD, `/api/providers/test` (fetch ke URL arbitrary → SSRF vector), `/api/keys` CRUD (bisa mint API key), `/api/logs` — semua tanpa auth. Bind localhost (default dev) aman by-design, TAPI Dockerfile bind `http://+:8080` (all interfaces) dan README memasarkan VPS deployment → kill-switch + manajemen provider-key terekspos ke network. Rekomendasi: pasang `ApiKeyEndpointFilter` (atau admin key) untuk `/api/*` + `/_shutdown`, atau default bind loopback di Docker.

### F-4 — HIGH (dependency; CONFIRMED by build)
NU1903: `SQLitePCLRaw.lib.e_sqlite3 2.1.11` known HIGH vulnerability (GHSA-2m69-gcr7-jv3q) via `Microsoft.Data.Sqlite 10.0.10`. Fix: bump `Microsoft.Data.Sqlite` / pin `SQLitePCLRaw` ≥ patched.

### F-5 — MEDIUM (AOT risk)
Warning `IL2026/IL3050` di `Services/Translation/AnthropicRequestTranslator.cs` (129, 147, 151, 186): `JsonArray.Add<T>` tidak AOT-safe padahal `PublishAot=true`. Risiko runtime breakage di path translasi `/v1/messages` saat Release AOT publish. Perlu smoke test publish AOT + refactor ke AOT-safe JSON.

### F-6 — MEDIUM (dependency)
`npm audit`: `nanoid <3.3.18` HIGH (GHSA-2v37-7h3g-55p8), transitive di devDependency tree frontend. `npm audit fix` tersedia. Exposur dev-only.

### F-7 — MINOR
Dead code Razor: folder `Pages/` di-exclude dari compile (`MininRouter.csproj`: `Compile Remove="Pages\**"`) tapi masih ada di tree + docs story 2.x. Hapus atau tandai legacy.

### F-8 — MINOR (test hygiene)
vitest `Logs.svelte:54` melempar `ERR_INVALID_URL` untuk fetch relatif `/api/providers` di jsdom — non-failing tapi mengotori output. Stub `fetch` di setupTests.

### F-9 — MINOR
Duplikasi icon lib di `frontend/package.json`: `lucide-svelte` DAN `@lucide/svelte`.

### F-10 — NIT
`Program.cs` CLI-mode: `cliModel` diinterpolasi mentah ke template JSON (`"model": "{{cliModel}}"`) — `"` pada `-m` akan menghasilkan JSON malformed. Serialize seperti `cliPrompt`.

## What Passed (positif, dengan bukti)

- **SQL injection: bersih.** Semua query parameterized termasuk `LIMIT $limit` (`LogService.cs:141-152`), `$hash` (`ApiKeyService.cs:209`).
- **Secret hygiene: bersih.** `git ls-files` → `.env`, `providers.json`, `minirouter.db` tidak ter-track; `git check-ignore` mengonfirmasi. Tidak ada hardcoded key di source.
- **Key masking: OK live.** `GET /api/providers` → `apiKeyMasked:"***jCvr"` dst.
- **Atomic provider writes (Story 8.2): OK.** `PersistAsync` — tmp file + `fs.Flush(true)` + backup + `File.Move` (`ProviderService.cs:182-200`).
- **API key storage: OK.** 32-byte `RandomNumberGenerator`, disimpan sebagai SHA-256 hash + prefix.
- **Proxy header hygiene: OK.** Authorization/Host/Content-Length client di-strip sebelum forward (`ProxyService.cs:78-83, 219-224`); upstream key dipasang per-request.
- **CORS: restriktif.** `WithOrigins("http://localhost:5173","http://localhost:3000")` (Program.cs:279-283).
- **Dynamic checks:** `/health` 200; SPA 200; hash router Home→Analytics OK; tanpa error overlay.

## Recommendations

### Must fix (sebelum siklus rilis berikutnya)
1. F-1: reopen Story 8.7 — hitung cost di success path ProxyService, perbaiki INSERT (tambahkan kolom `cost`,`cost_estimated`), tambah test persistence.
2. F-2: ganti mapping compose → `"5050:8080"`.
3. F-4: bump `Microsoft.Data.Sqlite` (patched SQLitePCLRaw).
4. F-3: auth pada `/api/*` + `/_shutdown` untuk bind non-loopback (atau default loopback di Docker).

### Suggested
5. F-5: smoke test `dotnet publish -r osx-arm64/linux-x64 -c Release` + refactor translator ke AOT-safe.
6. F-6: `npm audit fix`; F-7 hapus Pages/; F-8 stub fetch; F-9 pilih satu icon lib; F-10 serialize cliModel.

## Signal: CONCERNS

**Reason:** Build + 104 tests hijau dan tidak ada CRITICAL, tetapi ada missing AC implementation pada story Done (F-1), deployment config patah (F-2), dan exposure auth saat remote deployment (F-3). Layak lanjut dengan flag; F-1..F-4 wajib masuk backlog perbaikan segera.

**Next Actions:**
1. Lead: revert Story 8.7 → In Progress + buat fix request (F-1, F-2, F-4).
2. Dev: eksekusi fix list di atas.
3. Re-review: `*review-build` scope story fix setelah patch.

---
*Generated by Quinn (@qa) via qa-review-build (project-scope adaptation), 2026-08-29.*

# Project Status

**Last updated:** 27 October 2026
**Purpose:** Where the project actually stands — what is built, what is verified, what is not started, and what to do next.

> Starting a new session? Read `SESSION_HANDOVER.md` first — it covers the whole picture including why decisions were made and what reversed.

This is the living progress document. The others change rarely:

| Document | What it is |
|---|---|
| `SESSION_HANDOVER.md` | The whole picture, decisions and what reversed. Read first in a new session. |
| `School_Management_System_Architecture_and_Decisions.md` | Decisions and why. Revision 7. |
| `Master_Schema_Design.md` | The `master` schema design and its reasoning. |
| `HOW_TO_VERIFY.md` | How to check anything is correct — connections, migrations, permissions, tables. |
| **`PROJECT_STATUS.md`** (this file) | Current state and next actions. |

---

## 1. Where we are in one paragraph

The foundation is built and running. A .NET 10 solution with six projects compiles clean with zero warnings. The `master` and `school_main` schemas exist in a local PostgreSQL 18 database, created by EF Core migrations, with a two-role permission model that has been tested and holds. Nothing above the data layer exists yet: there is no dependency injection wiring, no login, no screens, and no tests. The next step is making the application start and seed itself.

**Scope reminder:** version 1 is **one school, one branch, one computer** (Model A). Multi-branch, LAN, multi-school and cloud are deferred — architecture document sections 1.1 to 1.3.

---

## 2. Environment

| Thing | Value |
|---|---|
| .NET SDK | 10.0.300 |
| EF Core | 10.0.14 |
| Npgsql provider | 10.0.3 |
| `dotnet-ef` CLI | 10.0.14, installed globally |
| PostgreSQL | 18.1, local service `postgresql-x64-18`, port 5432 |
| psql | `C:\Program Files\PostgreSQL\18\bin\psql.exe` (not on PATH) |
| Solution file | `SchoolManagementSystem.slnx` (new .slnx format) |
| Git remote | `https://github.com/zafar-dotcom/school-management-system` (private), branch `main` |

### Database

| | |
|---|---|
| Databases | `sms` (app), `sms_test` (integration tests) |
| Schemas in each | `master`, `school_main` |
| Owner / migration role | `sms_owner` — owns schemas, runs migrations |
| Application role | `role_school_main` — read/write data only, cannot alter schema |
| Credentials | .NET user secrets, id `4a4b251b-307d-4933-b84e-29e4e4f64e49`, shared by `SMS.Web` and `SMS.Infrastructure` |

### The three connection strings

They are never used at the same time. Different processes, different moments, different powers.

| Secret | Logs in as | Used by | When |
|---|---|---|---|
| `ConnectionStrings:Owner` | `sms_owner` | `dotnet ef`, and later the installer | Only when changing the schema |
| `ConnectionStrings:App` | `role_school_main` | The running application | Every request |
| `ConnectionStrings:TestOwner` | `sms_owner` | Integration tests, against `sms_test` | Test runs |

All three point at the same server; `Owner` and `App` point at the same database. The difference is **privilege**. If the application connected as `sms_owner`, a bug or an injection could `DROP TABLE`. As `role_school_main` it cannot — verified in section 4.

**Where the real file lives — not in this repository:**

```
%APPDATA%\Microsoft\UserSecrets\4a4b251b-307d-4933-b84e-29e4e4f64e49\secrets.json
```

That location is the point: a password outside the repository cannot be committed. **Never copy it into the project folder.**

`src/SMS.Infrastructure/secrets.example.json` is a committed template showing the structure with placeholders, plus the three commands to run on a new machine. It contains no real values.

> **Important:** the local PostgreSQL is **not** the company server. pgAdmin was originally connected to a remote company instance at `138.199.194.175`, which holds unrelated production and UAT databases. All work for this project goes on `localhost` only. Verify with `SELECT inet_server_addr();` — it must return `::1` or `127.0.0.1`.

---

## 3. What is built

### Solution structure

```
D:\myprojectt\
├── SchoolManagementSystem.slnx
├── src\
│   ├── SMS.Domain\          entities, enums — no EF Core reference
│   ├── SMS.Application\     ITenantContext, IClock
│   ├── SMS.Infrastructure\  DbContexts, configurations, migrations, interceptor
│   │   └── secrets.example.json   template only — no real values
│   └── SMS.Web\             MVC project (template default, not yet wired)
├── tests\
│   ├── SMS.UnitTests\       empty
│   └── SMS.IntegrationTests\ empty
├── db\
│   ├── 01_create_database_and_roles.sql   run manually, done (passwords scrubbed)
│   ├── 02_master_schema.sql               generated — NOT the thing that ran
│   └── 03_school_schema.sql               generated — NOT the thing that ran
├── SESSION_HANDOVER.md
├── HOW_TO_VERIFY.md
├── PROJECT_STATUS.md
├── School_Management_System_Architecture_and_Decisions.md
└── Master_Schema_Design.md
```

Project references follow the Clean Architecture rule: `Application` → `Domain`; `Infrastructure` → `Application`; `Web` → both. **`Application` does not reference `Infrastructure`.**

### Domain (`SMS.Domain`)

| File | Contents |
|---|---|
| `Common/AuditableEntity.cs` | `Id`, created/updated stamps |
| `Common/BranchScopedEntity.cs` | adds non-nullable `BranchId` |
| `Common/Enums.cs` | 8 enums including `SchoolRole`, `BranchScope`, `LicenseStatus` |
| `Master/School.cs` | tenant record, schema name, schema version |
| `Master/Branch.cs` | campus, `IsDefault`, required `TimeZone` |
| `Master/AppUser.cs` | `IdentityUser<Guid>` plus `DisplayName`, `Status`, `IsPlatformAdmin` |
| `Master/SchoolMembership.cs` | role + branch scope + `MembershipVersion` |
| `Master/SchoolMembershipBranch.cs` | branch list for Specific scope |
| `Master/Installation.cs` | installation key, clock high-water mark |
| `Master/License.cs` | yearly subscription with renewal history |
| `Master/AuditLogEntry.cs` | audit trail, `jsonb` changes, `inet` address |
| `SchoolData/SchoolClass.cs` | first branch-scoped entity |

### Infrastructure (`SMS.Infrastructure`)

- `MasterDbContext` — derives from `IdentityUserContext<AppUser, Guid>`, default schema `master`
- `SchoolDbContext` — default schema `school_main`, global `BranchId` query filter
- Ten `IEntityTypeConfiguration` classes, no EF attributes on domain types
- `AuditingInterceptor` — stamps audit columns, assigns `BranchId` server-side on insert
- `ModelBuilderExtensions.UseXminConcurrencyToken()` — shared concurrency helper
- `DesignTimeFactories` — reads the connection string from user secrets
- Two migration histories: `Migrations/Master` and `Migrations/SchoolSchema`

### Database — applied and verified

| Schema | Tables |
|---|---|
| `master` | `school`, `branch`, `app_user`, `app_user_claim`, `app_user_login`, `app_user_token`, `school_membership`, `school_membership_branch`, `installation`, `license`, `audit_log`, `__ef_migrations_history` |
| `school_main` | `class`, `__ef_migrations_history` |

Migrations applied: `20261026180448_InitialMaster`, `20261026180204_InitialSchool`.

**How they were applied — and why the `.sql` files are not how.** The schema was created by `dotnet ef database update`, which runs the migrations from the compiled assembly. The files `db/02_master_schema.sql` and `db/03_school_schema.sql` are *generated from those same migrations* and produce an identical result, but they were not executed.

They are kept for three reasons: so the SQL can be reviewed before it runs; because the installer will ship them to a school's server, which has no .NET SDK or `dotnet-ef`; and as a per-release record of what changed. They are generated with `--idempotent`, so every statement is guarded and re-running is safe.

**Keep them in step.** After adding any migration, regenerate both scripts — otherwise the installer ships a schema older than the code. Commands in section 8.

---

## 4. What has been verified, not just written

These were checked against the running database, not assumed:

- **Two-role model holds.** `role_school_main` can `SELECT` from `master.school` but `CREATE TABLE master.hack` returns *permission denied for schema master*. The application cannot alter its own schema.
- **No Identity role tables.** `asp_net_roles` and `asp_net_user_roles` do not exist, so authorization cannot accidentally be read from them.
- **Composite foreign keys present** on `school_membership_branch` — `(branch_id, school_id)` and `(membership_id, school_id)`. A membership at one school cannot reference another school's branch.
- **Three partial unique indexes exist**: `uq_branch_one_default_per_school`, `uq_installation_single_row`, `uq_license_one_active`.
- **Three check constraints exist**: `ck_school_slug_format`, `ck_school_schema_format`, `ck_license_dates`.
- **`xmin` is used as the concurrency token** — no version column was created on any table.
- **Naming is snake_case throughout**; nothing in the schema needs quoting in psql.
- **Build is clean** — zero errors, zero warnings.

---

## 5. What is NOT built

Nothing below has been started.

### Immediate foundation gaps

| Item | Notes |
|---|---|
| Dependency injection wiring | `Program.cs` is still the MVC template default |
| `ITenantContext` implementation | Interface exists; nothing implements it |
| `IClock` implementation | Interface exists; nothing implements it |
| Interceptor registration | `AuditingInterceptor` is written but not attached to either context |
| Shared connection and transaction across the two contexts | Required before any operation writes to both schemas — `Master_Schema_Design.md` §6 |
| Schema version check | `school.schema_version` column exists; nothing reads it at startup |
| Serilog | Not installed |
| Identity / cookie authentication | Not configured |
| Login screen and layout | Not started |
| Setup seed | No school, branch, user or licence row exists — the database is empty |

### Not started at all

- Licence validation, signature checking, warning/grace/read-only states
- Licence generator tool (internal, for signing licence files)
- Installer and packaging
- Any test, of any kind — both test projects are empty
- All feature modules: academic setup, teachers, students, attendance, fees, exams, reports

### Housekeeping gap

**There is no version control.** The project is not a git repository and has no `.gitignore`. There is now real code with no history and no backup. This should be fixed before the next work session — see section 7.

---

## 6. Gotchas discovered — worth keeping

Each of these cost time and will recur on another machine or another developer's setup.

| # | Issue | Resolution |
|---|---|---|
| 1 | `UseXminAsConcurrencyToken()` **no longer exists** in the EF Core 10 Npgsql provider, though tutorials still show it | Replaced by `UseXminConcurrencyToken()` in `ModelBuilderExtensions.cs`, which sets a shadow `uint` property named `xmin` as the row version |
| 2 | A namespace called `School` shadows the `School` entity type and breaks the build | Namespaces renamed to `SMS.Domain.SchoolData` and `SMS.Infrastructure.Persistence.SchoolSchema` |
| 3 | PostgreSQL `inet` cannot map to `string` | `AuditLogEntry.IpAddress` is `System.Net.IPAddress` — the database now validates the address on write |
| 4 | Identity's side tables keep PascalCase names (`"AspNetUserClaims"`) because Identity sets them itself; the snake_case convention skips them | Explicit `ToTable` in `IdentityTableConfiguration.cs` → `app_user_claim`, `app_user_login`, `app_user_token` |
| 5 | `CREATE DATABASE cannot run inside a transaction block` in pgAdmin | pgAdmin wraps multi-statement runs in a transaction. Select the single `CREATE DATABASE` line and press F5 — selecting text runs only the selection |
| 6 | `LC_COLLATE 'en_US.UTF-8'` fails on a Windows PostgreSQL install | Omit the locale clauses; inherit the server default |
| 7 | `dotnet ef migrations remove` tries to connect to the database and fails if credentials are not set | Delete the migration folder and re-add instead, when nothing has been applied anywhere |
| 8 | `dotnet ef migrations script --no-build` silently uses a stale assembly and produces an empty script | Do not pass `--no-build` after adding a migration |
| 9 | `user` is a reserved word in PostgreSQL | Every Identity table carries an `app_` prefix |
| 10 | pgAdmin's blank "Host name/address" field does not mean localhost in the server list view | Confirm with `SELECT inet_server_addr();` — never trust the dialog |
| 11 | `migrations list` prints *"Pending status not shown. Unable to determine which migrations have been applied"* — the message names only two causes and omits the common one | You passed `--no-connect`, so EF deliberately did not look. Harmless. Without that flag, the same warning does mean a real connection failure |

---

## 7. Next actions, in order

### A. ✅ Rotate the database passwords — DONE

Both roles were given new passwords, and the originals were scrubbed from `db/01_create_database_and_roles.sql`, which now holds placeholders again. No password exists anywhere in the repository.

**Re-setting the secrets after any future rotation** — run from `D:\myprojectt`, substituting the real values:

```powershell
dotnet user-secrets set "ConnectionStrings:Owner"     "Host=localhost;Port=5432;Database=sms;Username=sms_owner;Password=NEW"        --project src/SMS.Infrastructure
dotnet user-secrets set "ConnectionStrings:App"       "Host=localhost;Port=5432;Database=sms;Username=role_school_main;Password=NEW" --project src/SMS.Infrastructure
dotnet user-secrets set "ConnectionStrings:TestOwner" "Host=localhost;Port=5432;Database=sms_test;Username=sms_owner;Password=NEW"   --project src/SMS.Infrastructure
```

Verify with `dotnet ef migrations list --project src/SMS.Infrastructure --context MasterDbContext`. If the owner password is right it connects and shows `InitialMaster` as applied; if wrong, you get an authentication error.

### B. ✅ Put the project under version control — DONE

Repository: `https://github.com/zafar-dotcom/school-management-system` — **private**, branch `main`, first commit `e0d4ef0` pushed.

Checked before pushing: 131 files committed, `bin/` and `obj/` excluded by `.gitignore`, no real password in any committed file, and only `secrets.example.json` (the placeholder template) included. Real credentials stay in user secrets outside the project folder.

**Routine from here:**

```powershell
git add .
git commit -m "what changed"
git push
```

**Run section 8 of `HOW_TO_VERIFY.md` before every commit** — the scan for passwords in the project folder. Removing a secret after it has been pushed is painful.

### C. Make the application start

1. Implement `IClock` and `ITenantContext` in `SMS.Infrastructure`.
2. Wire dependency injection in `SMS.Web/Program.cs`: both DbContexts on a shared connection, the audit interceptor, Serilog.
3. Add the startup schema-version check that refuses to run against a mismatched schema.
4. Configure Identity with cookie authentication and persisted data-protection keys.
5. Build the setup seed: one school, one `Main` branch, the first School Admin account.
6. A login page and a bare `class` list proving the branch filter works end to end.

At the end of C, the application runs, you can log in, and the walking skeleton is complete.

### D. Then start the first feature module

**Academic setup** — academic year, class, section, subject. Order and prerequisites are in architecture document section 9.1.

---

## 8. Useful commands

> For checking that something is *correct* — connection works, migrations applied, permissions right, no password committed — see `HOW_TO_VERIFY.md`. It is organised as "I want to check X" with the command and how to read the result.

Run from `D:\myprojectt`. `dotnet-ef` is global; if not found, add `%USERPROFILE%\.dotnet\tools` to PATH.

```powershell
# Build
dotnet build

# Add a migration
dotnet ef migrations add <Name> --project src/SMS.Infrastructure --context MasterDbContext --output-dir Persistence/Migrations/Master
dotnet ef migrations add <Name> --project src/SMS.Infrastructure --context SchoolDbContext --output-dir Persistence/Migrations/SchoolSchema

# Apply migrations
dotnet ef database update --project src/SMS.Infrastructure --context MasterDbContext
dotnet ef database update --project src/SMS.Infrastructure --context SchoolDbContext

# Regenerate the SQL scripts — run BOTH after every new migration, or the
# installer will ship a schema older than the code. Do not pass --no-build.
dotnet ef migrations script --project src/SMS.Infrastructure --context MasterDbContext --idempotent -o db/02_master_schema.sql
dotnet ef migrations script --project src/SMS.Infrastructure --context SchoolDbContext --idempotent -o db/03_school_schema.sql

# Inspect the database
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U sms_owner -d sms
```

**Always pass `--context`.** There are two, and the wrong one silently targets the wrong schema.

---

## 9. Decisions still open

These do not block the next work. Full list in architecture document section 12.

| Decision | Needed before |
|---|---|
| Windows / domain login in scope? | Identity configuration (step C4) |
| Licence file format and contents | The installer |
| Which modules ship in the first release | Starting feature modules |
| Attendance: daily or period-wise? | The attendance module |
| Currency, decimal precision, invoice cycle, late fees, payment gateway | The fees module |
| Grading scheme and report-card layout | The exams module |
| Target market and country | Nothing yet — affects data retention and consent rules later |

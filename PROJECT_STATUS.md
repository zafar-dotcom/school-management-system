# Project Status

**Last updated:** 27 October 2026 — walking skeleton, then logic extracted to `SMS.Application` and `SMS.Api` added
**Purpose:** Where the project actually stands — what is built, what is verified, what is not started, and what to do next.

> Starting a new session? Read `SESSION_HANDOVER.md` first — it covers the whole picture including why decisions were made and what reversed.

This is the living progress document. The others change rarely:

| Document | What it is |
|---|---|
| `SESSION_HANDOVER.md` | The whole picture, decisions and what reversed. Read first in a new session. |
| `School_Management_System_Architecture_and_Decisions.md` | Decisions and why. Revision 8. |
| `Master_Schema_Design.md` | The `master` schema design and its reasoning. |
| `HOW_TO_VERIFY.md` | How to check anything is correct — connections, migrations, permissions, tables. |
| **`PROJECT_STATUS.md`** (this file) | Current state and next actions. |

---

## 1. Where we are in one paragraph

**The walking skeleton is complete, and it now runs behind two hosts.** The application starts, checks its schema, runs a first-time setup wizard, signs a user in, and serves a branch-filtered screen that proves the isolation mechanism end to end. The use cases live in `SMS.Application`; `SMS.Web` serves them as server-rendered MVC with cookies and `SMS.Api` serves them as JSON with JWT, both in-process. A .NET 10 solution with seven projects compiles clean with zero warnings. The `master` and `school_main` schemas exist in a local PostgreSQL 18 database, created by EF Core migrations, with a two-role permission model that has been tested and holds.

Still missing above that line: any automated test, licence enforcement, the installer, and every feature module. The next step is the tests in section 7D, then the first feature module.

**Scope reminder:** version 1 is **one school, one branch, one computer** (Model A). Multi-branch, LAN, multi-school and cloud are deferred — architecture document sections 1.1 to 1.3.

---

## 2. Environment

| Thing | Value |
|---|---|
| Application URLs | Web `https://localhost:7106` · API `https://localhost:7286` |
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

> **Quote a password that contains a semicolon.** `Password=<"value in double quotes">`. Unquoted, Npgsql reads
> everything after the `;` as a separate keyword and the connection string will not parse at
> all — you get a format error, not an authentication error. This bit us on
> `ConnectionStrings:App`; see gotcha 12. Check it after every rotation.

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
│   ├── SMS.Application\     use cases: Access, Setup, Classes — plus the store
│   │                        contracts. No reference to SMS.Infrastructure
│   ├── SMS.Infrastructure\  DbContexts, configurations, migrations, interceptor,
│   │   │                    tenancy, clock, startup schema check, stores, DI
│   │   └── secrets.example.json   template only — no real values
│   ├── SMS.Web\             MVC host: setup wizard, login, class list, cookies
│   └── SMS.Api\             JSON host for mobile and WPF: JWT, same handlers
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

Project references follow the Clean Architecture rule: `Application` → `Domain`;
`Infrastructure` → `Application`; `Web` and `Api` → both.
**`Application` does not reference `Infrastructure`.**

**Each rule is now written as a comment at the top of the `.csproj` it governs**, with the
reasoning. A markdown file nobody has open while adding a project reference does not stop
the reference being added; a comment where the temptation lives has a chance. Borrowed
from the `psp-admin-api` company project, which does the same with numbered rule ids.

### Build configuration — two files at the repository root

| File | What it does |
|---|---|
| `Directory.Packages.props` | **Central package management.** Every NuGet version for the solution, declared once. Project files list packages with no `Version` attribute |
| `Directory.Build.props` | `net10.0`, `Nullable`, `ImplicitUsings` and `TreatWarningsAsErrors` for every project, inherited automatically |

Before this, `Serilog.AspNetCore` 10.0.0 was declared in two project files and the
`10.0.14` framework version in nine places — one upgrade meant editing five files and
hoping none was missed.

**Zero warnings is now enforced, not just a habit.** `TreatWarningsAsErrors` is on and was
verified by compiling a deliberate CS0169 and confirming the build failed. The single
exception is NuGet audit (`NU19xx`), which stays a warning: a new advisory can be published
against a package we already ship, overnight, and that must be loud without blocking an
unrelated hotfix build.

```
Browser  ──>  SMS.Web (MVC, cookies)  ──┐
Mobile   ──>                            ├──>  SMS.Application  ──>  SMS.Infrastructure  ──>  DB
WPF      ──>  SMS.Api (JSON, JWT)    ──┘
```

**`SMS.Web` never calls `SMS.Api` over HTTP.** Same process, same handlers, called directly.

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

- `MasterDbContext` — derives from `IdentityUserContext<AppUser, Guid>`, default schema `master`, and implements `IDataProtectionKeyContext`
- `SchoolDbContext` — default schema `school_main`, global `BranchId` query filter
- Eleven `IEntityTypeConfiguration` classes, no EF attributes on domain types
- `AuditingInterceptor` — stamps audit columns, assigns `BranchId` server-side on insert
- `ModelBuilderExtensions.UseXminConcurrencyToken()` — shared concurrency helper
- `DesignTimeFactories` — reads the connection string from user secrets
- Three migration histories entries across `Migrations/Master` and `Migrations/SchoolSchema`
- `Time/SystemClock` — `IClock`, singleton, always UTC
- `Tenancy/TenantContext` — scoped, mutable, **throws when read before it is resolved** rather than returning `Guid.Empty`
- `Tenancy/MetadataOnlyTenantContext` — for the startup check only; never serves a request
- `Persistence/CrossSchemaTransaction` — one transaction spanning both schemas
- `Setup/InstallationState` and `Setup/SchemaVersionCheck` — the startup gate
- `DependencyInjection.AddInfrastructure` — the single registration point

### Application (`SMS.Application`)

The use cases, shared by both hosts. No reference to `SMS.Infrastructure`.

- `Abstractions/` — `IClock`, `ITenantContext`, plus `IMasterStore`, `ISchoolStore`, `IUnitOfWork`, `ISchemaVersionSource`
- `Common/Result` — success or a list of errors, so ordinary failures are not exceptions
- `Access/AuthenticateHandler` — credentials, lockout, membership. Does **not** sign anyone in
- `Access/ResolveTenantHandler` — the per-request validation rules, shared by cookie and token
- `Access/BuildClaimSetHandler`, `SchoolClaimSet`, `SchoolClaimTypes` — the claim set both hosts emit
- `Access/IdentityPolicy` — password and lockout rules, so the two hosts cannot drift
- `Setup/CompleteSetupHandler` — the whole first-run seed, in one transaction
- `Classes/ListClassesHandler`, `Classes/CreateClassHandler`

### Web (`SMS.Web`)

- `Program.cs` — Serilog, Identity, cookies, DPAPI-protected keys, pipeline order
- `Tenancy/SchoolClaimsPrincipalFactory` — writes the shared claim set into the cookie
- `Tenancy/InstallationGateMiddleware` — 503 maintenance page, or redirect to setup
- `Tenancy/TenantResolutionMiddleware` — reads cookie claims, calls `ResolveTenantHandler`, signs out on refusal
- `Controllers/` — `Setup`, `Account`, `Classes`. Thin: validate, call one handler, return a view

### Api (`SMS.Api`)

- `Program.cs` — Serilog, JWT bearer, the same schema check, minimal endpoints
- `Auth/JwtOptions`, `SigningKeyProvider`, `JwtTokenIssuer` — writes the shared claim set into a token
- `Tenancy/ApiTenantResolutionMiddleware` — same handler as the web host; refuses with 401 JSON instead of a redirect
- `Contracts/` — request and response records. **No `BranchId` on any of them, by design**
- Endpoints: `GET /health`, `POST /auth/token`, `GET /classes`, `POST /classes`

### Database — applied and verified

| Schema | Tables |
|---|---|
| `master` | `school`, `branch`, `app_user`, `app_user_claim`, `app_user_login`, `app_user_token`, `school_membership`, `school_membership_branch`, `installation`, `license`, `audit_log`, `data_protection_key`, `__ef_migrations_history` |
| `school_main` | `class`, `__ef_migrations_history` |

Migrations applied: `20261026180448_InitialMaster`, `20261027081215_AddDataProtectionKeys`, `20261026180204_InitialSchool`.

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

### Verified by running the application, 27 October 2026

Driven over HTTP against the live database, not reasoned about:

- **The startup gate works.** With no school row the application reported `NotInstalled` and redirected every request to `/Setup`. The check reads both migration histories and never migrates.
- **Setup wrote four rows in one transaction** — school, Main branch, administrator, membership — and the school recorded `schema_version = 20261026180204_InitialSchool`. No licence row: see section 5.
- **Data-protection keys persist to the database.** The first key was written to `master.data_protection_key` at startup, so cookies now survive a restart.
- **Login works** and the cookie carries school, branch, role, branch scope and membership version.
- **`BranchId` is set server-side.** Two classes were created through a form that has no branch field; both landed on the Main branch with `created_by` set to the signed-in administrator.
- **The branch filter genuinely excludes.** A row was inserted directly in SQL carrying a *different* `branch_id`; it is present in the table and the application does not show it. The generated SQL is `WHERE c.branch_id = @ef_filter___branchId` — parameterised per query, which confirms the captured-field design is not baking one branch into the cached model.
- **Revocation does not wait for the cookie.** Changing `membership_version` in the database caused the very next request to sign the user out and redirect to login. Two independent mechanisms cover this: this check, and Identity's security-stamp refresh, which regenerates the claims from the database every five minutes.
- **Anonymous access is refused** — `/Classes` redirects to the login page with a return URL.

> **On that filter test.** `Master_Schema_Design.md` section 7 records that version 1 cannot exercise the branch filter because there is only one branch. That is true of the *seed*, but the filter itself can be tested by inserting a foreign `branch_id` directly, which is what was done above. It does not replace the two-branch test required at the start of phase D2 — that one also covers branch switching, scope and the widened-scope service — but the filter predicate itself is no longer unproven.
>
> That particular insert **no longer works**, because the cross-schema foreign key from
> section 7C2 now rejects it. Repeating the test means inserting a real second branch row
> first — which is the phase D2 test anyway.

### Verified after the §7C2 and §7E work, 27 October 2026

- **The cross-schema foreign key holds.** The exact insert that slipped through during the filter test above is now rejected: *insert or update on table "class" violates foreign key constraint "fk_class_branch"*. The constraint reads `FOREIGN KEY (branch_id) REFERENCES master.branch(id) ON DELETE RESTRICT`.
- **The web host still works after the refactor** — login, the class list, creating a class, and the duplicate-code rejection, all through the extracted handlers.
- **The API works against the same handlers.** `GET /health` anonymous; `GET /classes` is 401 without a token and returns the branch-filtered list with one; `POST /auth/token` returns a bearer token and 401s on a wrong password with the same wording the website uses.
- **`BranchId` cannot be supplied by an API client.** A `POST /classes` body carrying an extra `"branchId"` for a non-existent branch was accepted and the row still landed on Main — the field is simply not on the contract, so it is discarded. Architecture §4.6 rule 1 holds on the API surface, not only on the web form.
- **Revocation applies identically to a token.** A token minted at one `membership_version` was rejected with `401 {"error":"context_invalid","detail":"membership has changed since sign-in"}` after the membership changed. Same `ResolveTenantHandler` as the cookie path — which is the point of having extracted it.
- **The schema-version cache self-heals.** See the note in section 6, gotcha 16.

---

## 5. What is NOT built

Nothing below has been started.

### Foundation gaps — now closed

Dependency injection, `IClock`, `ITenantContext`, interceptor registration, the shared
connection and transaction, the schema-version check, Serilog, Identity with cookies, the
setup seed, the login screen and the class list are all built and exercised. See section 4.

### Still not built

| Item | Notes |
|---|---|
| Any test, of any kind | Both test projects are still empty. This is now the largest single gap — everything in section 4 was verified by hand once, and nothing stops it regressing |
| Licence enforcement | No `license` row is written at setup and nothing validates one. The file format and the signing tool are both still open, so there is nothing to validate against |
| Licence generator tool | Internal console tool, for signing licence files |
| Installer and packaging | Including the Windows Service host and local backup |
| Audit log capture | `AuditingInterceptor` stamps who and when on the row itself; it does not yet write field-level changes into `master.audit_log`. Planned alongside the first feature module |
| `installation` row | The table exists; no row is written and the clock high-water mark is not maintained. Needed with licensing |
| Cross-schema FK from `class.branch_id` to `master.branch` | Architecture §4.3 and §4.7 call for a real foreign key. There is none, which is how the filter test in section 4 was able to insert a non-existent branch |
| All feature modules | Academic setup, teachers, students, attendance, fees, exams, reports |

### Housekeeping

Version control is in place — see section 7B. No gap outstanding here.

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
| 12 | **A password containing `;` silently breaks the connection string.** `ConnectionStrings:App` held a 28-character password with a `;` in the middle and no quoting, so Npgsql split the tail into a bogus keyword and threw a format error before reaching the server. Nothing had ever connected as the app role, so it had never surfaced | Wrap the value in double quotes — `Password=<"…">`. Worth checking after every rotation; a generated password can contain a semicolon at any time |
| 13 | Naming a namespace `Installation` shadows the `Installation` entity and breaks the build — **gotcha 2 all over again**, in a new place | The namespace is `SMS.Infrastructure.Setup`. Before naming any namespace, check it is not also an entity name: `School`, `Installation`, `License`, `Branch` are all taken |
| 14 | **The password scan in `HOW_TO_VERIFY.md` §8 never ran.** `Select-String` has no `-Include` or `-Recurse`; it threw, the result was empty, and the check reported "clean" without looking. A pre-commit safety check that fails open is worse than none | Filter with `Get-ChildItem` first, then pipe into `Select-String`. Corrected in §8 |
| 15 | Testing cookie expiry or revocation with `curl -b` alone is misleading: re-sending the *original* cookie every time means Identity's security-stamp interval has always elapsed, so it refreshes the principal from the database on every request and any staleness you were trying to test disappears | Use `-b` and `-c` on the same cookie jar so the refreshed cookie is kept, then change the data and request again |
| 16 | **Nothing updated `school.schema_version` after the initial setup.** Only the setup wizard wrote it, so adding the second school-schema migration made the startup check refuse to serve — and on a customer's machine the *first* update shipped would have bricked the install until someone edited the row by hand, with no DBA on site | The migrations history is authoritative and is checked first. If it is fully applied but the column is stale, the check now refreshes the column and logs a warning instead of refusing. It still refuses outright when migrations are genuinely pending |
| 17 | `dotnet new webapi` brings `Microsoft.AspNetCore.OpenApi`, which pulls `Microsoft.OpenApi` 2.0.0 — a package with a known high-severity advisory (NU1903) | Removed. API documentation is deferred until a real client needs it (architecture §11), so there was nothing to weigh against shipping a known vulnerability |
| 18 | A stray `SMS.Web` process keeps `bin\` locked and the next build fails with MSB3027. `Stop-Process -Name dotnet` does not catch it — the run spawns a child named after the project | `Get-Process SMS.Web,SMS.Api \| Stop-Process -Force` before rebuilding |

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

### C. ✅ Make the application start — DONE

All six steps are built and were verified by running the application against the live
database. Section 4 lists what was proved and how.

**To run it:**

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/SMS.Web
```

Then open `https://localhost:7106`. On a database with no school it redirects to `/Setup`.

> User secrets load **only in Development**. Starting without `ASPNETCORE_ENVIRONMENT` set
> gives "ConnectionStrings:App is not configured" rather than a connection error.

### C2. ✅ Two items the walking-skeleton session raised — DECIDED AND DONE

**Data-protection keys are stored unencrypted.** Anyone able to read `master.data_protection_key`, or a database backup, can forge an authentication cookie. The framework logs a warning about this.

**Decision: encrypt them with DPAPI in `SingleTenant` mode.**

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<MasterDbContext>()
    .ProtectKeysWithDpapi(protectToLocalMachine: true);   // SingleTenant only
```

This ties the keys to the machine, which is exactly right for Models A and B — one server, never moved. It does **not** suit Model C, where instances come and go, so that mode will need a cloud key store instead.

This is not an `if (onPremise)` in business logic, which §7.2 of the architecture forbids. It is host configuration at the edge of the system, which the same section explicitly permits. Gate it on the deployment-mode setting alongside the other edge concerns.

*Consequence to record for Model A and B:* restoring a database backup onto a **different** machine makes existing auth cookies unreadable. Everyone is signed out and signs in again. That is acceptable and much better than portable forgeable keys, but it must be in the support notes so it is not mistaken for a fault.

**The missing cross-schema foreign key** from `school_main.class.branch_id` to `master.branch` — architecture §4.3 and §4.7 call for it and it is absent. That is why a row with a non-existent `branch_id` could be inserted during testing.

**Decision: add it, by hand, in a migration.** EF cannot generate it because the two tables belong to different `DbContext` types; it has no way to see the relationship. Add it with `migrationBuilder.Sql(...)`, the same way the three partial unique indexes were added:

```sql
ALTER TABLE school_main.class
    ADD CONSTRAINT fk_class_branch
    FOREIGN KEY (branch_id) REFERENCES master.branch (id) ON DELETE RESTRICT;
```

**This is the point of choosing schema-per-school.** Under database-per-school the constraint would have been impossible and the integrity would have rested on application code forever. Here the database can enforce it, so it should. Every branch-scoped table added from now on needs the same constraint — add it to the checklist when the academic-setup module lands.

### D. Write the tests that are now missing

This jumped ahead of the first feature module. Everything in section 4 was verified once, by
hand, and nothing currently stops it regressing — and these are precisely the behaviours
architecture §10 requires tests for.

1. Integration tests against `sms_test`: tenant resolution rejects a stale membership version, a suspended membership, a branch from another school, and a school whose `schema_name` is not this install's.
2. A test that the branch filter excludes a foreign `branch_id`, as done by hand in section 4.
3. A test that a write takes `BranchId` from the context and ignores anything posted.
4. A test that the schema-version check refuses a mismatch instead of serving.

### E. ✅ Move the business logic into `SMS.Application`, then add `SMS.Api` — DONE

Both halves are built and were verified by running the two hosts against the live database.
What follows is the original plan; section 4 records what the finished work actually proves.

**What the handlers turned out to be:**

```
SMS.Application/Access/AuthenticateHandler.cs     credentials, lockout, membership
SMS.Application/Access/ResolveTenantHandler.cs    the per-request validation rules
SMS.Application/Access/BuildClaimSetHandler.cs    school + branch + role claims
SMS.Application/Access/SchoolClaimTypes.cs        the five claim names, shared
SMS.Application/Access/IdentityPolicy.cs          password and lockout rules, shared
SMS.Application/Setup/CompleteSetupHandler.cs     the whole first-run seed
SMS.Application/Classes/ListClassesHandler.cs
SMS.Application/Classes/CreateClassHandler.cs
```

`ResolveTenantHandler` and `IdentityPolicy` were not in the original list and are the two
that mattered most: without them the API would have carried its own copy of the tenant
validation rules and its own password policy, free to drift from the website's.

**The layering rule held.** `SMS.Application` still does not reference `SMS.Infrastructure`.
It declares `IMasterStore`, `ISchoolStore`, `IUnitOfWork` and `ISchemaVersionSource`;
`SMS.Infrastructure/Persistence/Stores/` implements them; each host wires them up. These are
named operations, not generic repositories — architecture §10.

> **`SMS.Api` ports:** `https://localhost:7286`. Run it the same way as the website, with
> `ASPNETCORE_ENVIRONMENT=Development`.
>
> **`Jwt:SigningKey` must be configured outside Development** — the API refuses to start
> without it. In Development it generates an ephemeral key and logs a warning; tokens then
> stop working whenever the process restarts.
>
> **No OpenAPI package.** The template's `Microsoft.AspNetCore.OpenApi` pulls in
> `Microsoft.OpenApi` 2.0.0, which carries a known high-severity advisory (NU1903). It was
> removed rather than shipped — API documentation is deferred until a real client needs it
> anyway (architecture §11).

#### The original plan, for reference

Right now `SMS.Application` contains only `IClock` and `ITenantContext`. Every use case lives inside an MVC controller — `SetupController` alone holds the whole seed: school, branch, administrator, membership, inside the cross-schema transaction.

That works perfectly well for a web-only product. It does not survive a second consumer: `SMS.Api` cannot reach any of it without copying it.

**1. Extract the handlers.** Roughly:

```
SMS.Application/Setup/CompleteSetupHandler.cs       ← from SetupController
SMS.Application/Classes/ListClassesHandler.cs       ← from ClassesController
SMS.Application/Classes/CreateClassHandler.cs
SMS.Application/Access/AuthenticateHandler.cs       ← credentials + membership + claim set
```

Controllers become: validate input → call one handler → return a view.

**The split to get right.** *How* an identity is persisted is legitimately host-specific — cookie sign-in for Web, JWT issuance for the API. That stays in each host. What is shared is validating the credentials, resolving the `SchoolMembership`, and building the claim set. Divide `AccountController` along that seam rather than down the middle.

**2. Add `SMS.Api`** — minimal to start: a health endpoint and one or two endpoints mirroring what MVC already does. JWT, refresh tokens, versioning and documentation arrive when a real client needs them.

```
Browser  ──>  SMS.Web (MVC, cookies)  ──┐
Mobile   ──>                            ├──>  SMS.Application  ──>  Infrastructure  ──>  DB
WPF      ──>  SMS.Api (JSON, JWT)    ──┘
```

**Never have `SMS.Web` call `SMS.Api` over HTTP.** Same process — that would add a network hop and JSON serialisation for no benefit. Both reference `SMS.Application` directly.

**Why now:** this is a ~400-line refactor across three controllers. At twenty controllers it stops being a refactor and becomes a rewrite. A second consumer also makes logic-in-controller visible immediately, which is the discipline that was missing while only MVC existed.

### F. Then the first feature module

**Academic setup** — academic year, class, section, subject. Order and prerequisites are in architecture document section 9.1. It extends the `class` table the skeleton already uses.

When adding each branch-scoped table, remember the cross-schema foreign key from section C2.

### G. The user interface — deliberately deferred to the academic-setup module

**Decided 27 October 2026.** The screens built so far are stock Bootstrap 5 with no design
applied. That is a scaffold, not an oversight, and it stays that way until academic setup.

**Why wait:** the class list exists to prove the branch filter, not to be a product screen —
it prints a raw branch GUID in a column for exactly that reason. Designing against one
proof-of-concept screen means redoing it. Academic setup produces four or five screens that
resemble the real product, and both open decisions that bear on this — **target market and
country**, and **visual design and accessibility requirements** (architecture §12) — are
still unresolved. Choosing a palette and typography before knowing who buys this is
throwaway work.

**Remove when the class screen becomes real:** the `Branch` column showing a GUID, and the
explanatory note under the form. Both are scaffolding that proved a point and should not
ship.

#### Constraints to design within — the product runs in a browser on the school's own machine

These follow from Model A and are not negotiable later without rework.

| # | Constraint | Why |
|---|---|---|
| 1 | **No CDN. Every asset local.** No CDN Bootstrap, no Google Fonts, no remote icon packs, no analytics or telemetry scripts | A school's server may have no internet at all, and an asset that 404s offline takes the page's layout with it. Bootstrap and jQuery are already served from `wwwroot/lib` — keep it that way |
| 2 | **System font stack, not web fonts** | Nothing to download, nothing to licence, no flash of unstyled text on a slow machine |
| 3 | **Design for a desktop office PC first** | Model A is one computer; Model B adds other office PCs. Phones get a native client through `SMS.Api` later, so the website does not need to be mobile-first — but it should not break if someone opens it on a tablet |
| 4 | **Printing is a first-class requirement, not an afterthought** | Report cards, fee receipts and class lists are printed. The licence `read_only` state explicitly preserves "view, search, print, export" (Master_Schema_Design §3.8) — so a print stylesheet must exist and must work when everything else is locked |
| 5 | **Licence state needs visible UI** | Four states need treatment: `expiring` (warning banner), `grace` (prominent full-width warning), `read_only` (every create/edit control hidden or disabled, with the reason stated), `invalid` (same, plus contact support). §3.8 rule 5 requires the state and its reason always be visible to a School Admin |
| 6 | **Design the header so a branch switcher is additive** | It currently shows a branch GUID — that should become the branch *name*. Phase D2 drops a branch selector into the same spot. Leaving room now costs nothing |
| 7 | **The maintenance page must stay dependency-free** | `InstallationGateMiddleware` returns plain text today. It renders precisely when the schema is wrong or the database is unreachable, so it must not depend on a layout, a view engine, or anything read from the database. Style it with inline CSS only, or leave it as text |
| 8 | **Keep `asp-append-version`** | The installer replaces files in place during an upgrade. Without a content hash in the URL, a school's browser serves last version's cached CSS against this version's markup |
| 9 | **Decide the minimum browser** | Architecture §12 lists supported Windows versions as open; the browser question rides along with it. An old Edge on a school PC is a real possibility |
| 10 | **Accessibility level is still unresolved** | Architecture §12. Decide before the design is built, not after — retrofitting contrast ratios and keyboard navigation is far more expensive than designing with them |
| 11 | **Never put student data in a URL** | Already a rule (architecture §8, opaque identifiers), but it constrains how screens are routed and linked |

**Effort when it happens:** roughly half a day for a working layout — brand bar, navigation,
consistent page headers, a reusable table and form style, validation presentation, empty
states. Two to three days if a real visual identity is wanted on top.

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

# Session Handover — 26/27 October 2026

**Read this first in a new session.** It covers everything that happened, every decision and why, what reversed along the way, what was built, and exactly where to resume.

---

## 0. Starting the new session — paste this

Copy the block below as the first message of the new session:

```
Project: School Management System, at D:\myprojectt

Read these five files before doing anything, in this order:
  1. SESSION_HANDOVER.md       - the whole picture, decisions and reversals
  2. PROJECT_STATUS.md         - what is built, verified, and outstanding
  3. School_Management_System_Architecture_and_Decisions.md  - decisions and why
  4. Master_Schema_Design.md   - the master schema
  5. HOW_TO_VERIFY.md          - how to check anything is correct

Then confirm back to me, briefly:
  - what is already built
  - that nothing is outstanding, and what the next six steps are

Do not re-open decisions already recorded as closed.
Two standing rules: this project only ever touches the LOCAL PostgreSQL
(verify with SELECT inet_server_addr(), must be ::1 or 127.0.0.1), and the
structures listed in section 6 of the handover must not be removed.
```

Everything needed to continue is in those five files. Nothing from the previous conversation is required.

---

## 1. How to use these five documents

| Document | Read it when |
|---|---|
| **`SESSION_HANDOVER.md`** (this file) | Starting a new session — gives you the whole picture |
| `PROJECT_STATUS.md` | You need the current build state and next actions |
| `School_Management_System_Architecture_and_Decisions.md` | You need a decision and its reasoning. Revision 7 |
| `Master_Schema_Design.md` | You are working on the database |
| `HOW_TO_VERIFY.md` | You want to confirm something is actually correct — connections, migrations, permissions, tables |

Order for a cold start: this file → `PROJECT_STATUS.md` → the others as needed.

`HOW_TO_VERIFY.md` is the one to keep open while working. It is a lookup table of *"I want to check X"* → the command → how to read the result. Everything in it is read-only and safe to run at any time.

---

## 2. What this session achieved

The session began with a single architecture document and an empty folder. It ended with:

- A reviewed, revised architecture — seven revisions, every version-1 decision closed
- A complete `master` schema design
- A built .NET 10 solution: six projects, compiling clean, zero warnings
- A live PostgreSQL database with both schemas created by EF Core migrations
- A verified two-role security model
- Five documents

**The project is at the end of its foundation phase.** The data layer exists and works. Nothing above it does.

---

## 3. The project in brief

A commercial **School Management System**, sold to schools.

**Version 1 is deliberately narrow:** one school, one branch, installed on one computer at the school, with PostgreSQL on the same machine. The customer runs it; we do not host it.

Multi-branch, LAN sharing, multi-school and cloud hosting are **designed for but not built**. They become additive later because specific structures are carried in version 1 — see section 6 below, which is the single most important thing not to undo.

**Stack:** .NET 10 (LTS), ASP.NET Core MVC with Razor, PostgreSQL 18, EF Core 10 with Npgsql, ASP.NET Core Identity, Serilog, xUnit. Clean Architecture layering inside a modular monolith. Code First with EF migrations.

---

## 4. Decision timeline — including what reversed

A new session must know what changed, or it will act on a superseded decision found in an older part of a document.

| # | Decision | Status |
|---|---|---|
| 1 | ASP.NET Core MVC + Razor, .NET 10, Clean Architecture, modular monolith | Unchanged throughout |
| 2 | Authorization from `SchoolMembership`, **not** Identity role tables | Added in Revision 2, never changed |
| 3 | School = isolation boundary; branch = a scope inside it | Added in Revision 2, never changed |
| 4 | Case 2 (two offices, one database): install the app **once** on a shared server | **Corrected.** Originally described as installing the app on both PCs |
| 5 | Database engine | **Changed:** Microsoft SQL Server → **PostgreSQL** (Revision 4) |
| 6 | Code First with EF migrations | Decided Revision 4 |
| 7 | Tenant isolation | **Changed:** database-per-school → **schema-per-school** (Revision 5) |
| 8 | Scope | **Narrowed:** full multi-tenant → **Model A only**, one school one branch (Revision 6) |
| 9 | Subscription | Yearly; warning → grace → read-only (Revision 6/7) |
| 10 | Academic year | School-wide, no `BranchId` (Revision 7) |
| 11 | Student records | School-wide, branch held on the enrolment (Revision 7) |
| 12 | Module build order | Fixed (Revision 7) |
| 13 | Test seed | **Changed:** two branches in dev → **one branch, matching production**, at the user's direction |

### The two reversals worth understanding properly

**SQL Server → PostgreSQL.** The architecture never depended on SQL Server features, so little changed technically. Three real effects: `rowversion` became `xmin`, naming became snake_case, and — before the tenancy change — cross-database joins became impossible. The commercial effect was larger: PostgreSQL removed the database licence and the 10 GB Express ceiling from the on-premise conversation entirely.

**Database-per-school → schema-per-school.** This one is worth reading properly because the reasoning inverted.

Originally I recommended database-per-school and recorded schema-per-school as *considered and rejected*, because it demotes school isolation from a connection boundary to a software one. The user chose schema-per-school anyway. I implemented it and added the guardrail that makes it safe: **one PostgreSQL role per school, granted `USAGE` on its own schema only**, so a context bug produces a permission error rather than another school's data.

When later asked directly which I would recommend, I said schema-per-school — genuinely, and for reasons that had changed:

1. The role guardrail restores *database* enforcement; the boundary is grants rather than separate databases, but it is still the database refusing.
2. Cross-schema foreign keys became real, which removed an entire category of permanent risk — the unenforceable `master`-to-tenant link and the branch-registry mirror with its synchronisation job both disappeared.
3. For an on-premise product supported remotely with no DBA on site, one database is materially easier to install, back up and diagnose than two.

**Triggers to revisit it:** a customer contractually requiring physically separate stores; passing roughly a thousand schools; or a customer needing independent point-in-time recovery. Recorded in architecture §4.9.

---

## 5. The three deployment models

| | Model A | Model B | Model C |
|---|---|---|---|
| Shape | One computer at the school | A dedicated always-on server on the school LAN | Cloud hosted |
| Database | Local | On that server | Cloud |
| Application | Same computer | **Same server** — not on each office PC | Cloud |
| Users reach it | Browser locally | Browser over the LAN | Browser over HTTPS |
| Mode | `SingleTenant` | `SingleTenant` | `MultiTenant` |
| **Built?** | **Yes — version 1** | Deferred (phase D1) | Deferred (phase D4) |

**Why the application is not installed on every office PC** (this was corrected mid-session and the reasoning matters):

The decisive argument is security, not convenience. With the app on the server, PostgreSQL binds to that machine and only the application reaches it; office PCs need one HTTPS port. Installing the app on each PC would mean **opening port 5432 across the LAN and distributing database credentials to every workstation** — at which point a compromised office PC has direct database access, and every school and branch isolation rule, which lives in application code, is no longer in the path.

Secondary costs: version drift between installs against one schema, background jobs running twice, and certificates and configuration to maintain in several places.

---

## 6. The structures carried in version 1 — do not remove these

This is the most important section in the handover. Version 1 has one school and one branch, so several things below appear to be pointless overhead. They are not. Each is cheap now and disproportionately expensive to retrofit.

| Carried | Looks pointless because | Cost of adding it later instead |
|---|---|---|
| Two schemas, `master` + `school_main` | One school | Splitting a flat schema: data migration plus requalifying every query |
| Non-nullable `BranchId` + global query filter | One branch, filter always matches | **The worst one.** A column on ~15 tables, backfilling, revisiting every query, index and write path |
| `SchoolMembership` driving authorization | One school, one admin | Rewriting every authorization check |
| School and branch resolved from context, never hardcoded | Always the same values | Finding every place that assumed one implicit school |
| Per-school role naming (`role_school_main`) | One role | Inventing the permission model later, under deadline pressure |
| Audit, `xmin`, UTC `timestamptz`, snake_case, opaque ids | — | Backfilling audit history never recorded; type and identifier migrations |

Total overhead: two to three days. It is the difference between *adding a feature* later and *rewriting the foundation* later. Architecture document §1.3.

**Expect pressure to drop `BranchId`** around month two — "we only have one branch, why write this everywhere?" The answer is the table above.

**Known consequence of the single-branch test seed:** the branch filter is never exercised by the *seed* in version 1. Testing it with two branches is recorded as the first task of phase D2, before any branch UI is built.

The filter predicate itself is no longer unproven, though: inserting a row with a foreign `branch_id` directly in SQL and confirming the application does not show it exercises the filter without a second branch. That was done on 27 October 2026 — `PROJECT_STATUS.md` §4. It does not replace the D2 test, which also covers switching, scope and the widened-scope service.

---

## 7. Subscription and licensing

Decided this session. Full detail in `Master_Schema_Design.md` §3.6–3.8.

**Yearly subscription, enforced offline** because the customer owns the machine.

| State | When | What the school can do |
|---|---|---|
| `valid` | Normal | Everything |
| `expiring` | 30 days before expiry | Everything, with a warning banner |
| `grace` | Expiry → 14 days after | Everything, with a prominent warning |
| `read_only` | After grace | **Log in, view, search, print, export. Cannot create or change anything.** |
| `invalid` | Bad signature, wrong install, or clock moved backwards | Read-only, with a message to contact support |

**Read-only rather than a lockout**, deliberately: a school locked out mid-term cannot print a report card or find a parent's phone number — a crisis for them and an emergency for us. Read-only still forces renewal within a day or two, because attendance, fees and results cannot be recorded. It never holds a school's data hostage.

**Three mechanisms that make offline licensing work:**

- **Installation key** generated at setup. The licence is issued against it, so copying a licence file to another school fails.
- **Clock high-water mark** — the latest time ever observed. If the system clock reads earlier, it was set back, and the licence becomes invalid.
- **Signature is the only truth.** Every other licence column is a decoded cache. A customer editing `expires_at` in the database changes nothing.

**Still to build:** an internal licence generator — a small console tool that signs a licence file. Not shipped with the product. Only the public key ships; the private signing key never leaves our control.

---

## 8. What was built

```
D:\myprojectt\
├── SchoolManagementSystem.slnx
├── src\
│   ├── SMS.Domain\          12 entity files, enums, base types — no EF reference
│   ├── SMS.Application\     ITenantContext, IClock
│   ├── SMS.Infrastructure\  2 DbContexts, 10 configurations, interceptor, 2 migrations
│   └── SMS.Web\             MVC template, not yet wired
├── tests\                   both empty
├── db\                      3 SQL scripts
└── 4 markdown documents
```

**Database, live and verified:** `master` holds 11 tables, `school_main` holds `class`. Both migrations applied.

**Verified against the running database, not assumed:**
- `role_school_main` can `SELECT` from `master.school` but `CREATE TABLE` returns *permission denied*
- No Identity role tables exist
- Composite foreign keys present on `school_membership_branch`
- Three partial unique indexes and three check constraints present
- No version column created — `xmin` is the concurrency token

Full inventory in `PROJECT_STATUS.md` §3 and §4.

### Design choices in the code worth knowing

- **`IdentityUserContext<,>`, not `IdentityDbContext<,>`** — Identity's role tables are never created, so "authorization does not come from Identity roles" is structural rather than a rule someone must remember.
- **Composite foreign keys on `school_membership_branch`** carry `school_id`, making it impossible to scope a School A membership to a School B branch. The obvious single-column design would allow exactly that.
- **`SchoolDbContext` captures the branch id into a private field.** EF caches the compiled model; a filter closing over a local would freeze the first request's branch into every later request. With one branch you would never notice — you would find out on the day multi-branch activated.
- **Check constraints on `slug` and `schema_name` formats** live in the database, because those values are eventually interpolated into `SET search_path`.
- **`app_user`, not `user`** — `user` is reserved in PostgreSQL. All Identity tables take an `app_` prefix.

---

## 9. The environment setup — and one incident worth knowing

Setting up the database took several attempts. The important part:

**pgAdmin was connected to the company's remote PostgreSQL server**, not the local machine. The hostname field appeared blank, which was misleading. `SELECT inet_server_addr()` returned a public IP, and the database list showed company production and UAT databases.

Running the setup script there would have created the project database and two roles on a shared company server, and one line in it — `REVOKE ALL ON SCHEMA public FROM PUBLIC` — could have broken a live company application. It was caught before anything ran.

**Standing rule for every future session:** before running any SQL for this project, confirm with

```sql
SELECT inet_server_addr();
```

It must return `::1` or `127.0.0.1`. Never trust the pgAdmin connection dialog.

The local PostgreSQL 18 is separate, accepts connections only from the machine itself (`pg_hba.conf` allows only `local`, `127.0.0.1/32`, `::1/128`), and holds no company data.

**Also during setup:** the local `postgres` password was unknown and was reset by temporarily adding `trust` lines to `pg_hba.conf`, then removing them. The file was verified clean afterwards — all six rules are back to `scram-sha-256`.

---

## 10. Gotchas that cost time — ten of them

Recorded in full in `PROJECT_STATUS.md` §6. The five most likely to recur:

1. **`UseXminAsConcurrencyToken()` no longer exists** in the EF Core 10 Npgsql provider, though tutorials still show it. Replaced by a local `UseXminConcurrencyToken()` extension.
2. **A namespace called `School` shadows the `School` entity** and breaks the build. Namespaces are `SchoolData` and `SchoolSchema`.
3. **`CREATE DATABASE` cannot run inside a pgAdmin transaction.** Select that single line and press F5 — selecting text runs only the selection.
4. **`LC_COLLATE 'en_US.UTF-8'` fails on Windows PostgreSQL.** Omit locale clauses entirely.
5. **`dotnet ef migrations script --no-build` silently uses a stale assembly** and produces an empty script.

---

## 11. How this user prefers to work

Observed through the session; worth honouring.

- **Plain language.** Explicitly asked for simple sentences, no dense jargon. Five years of experience, but not a specialist in architecture or PostgreSQL administration.
- **Concrete numbered steps** for anything operational — which menu, which button, what to type. Conceptual explanation alone was not enough for the pgAdmin work.
- **Asked twice for WhatsApp-pasteable summaries** to send to a team lead. Plain text, no tables, no markdown.
- **Nothing from the session should be persisted** outside the conversation — no memory files, no logs, no artifacts. Project files are fine and expected.
- **Keep everything off company infrastructure.** This project is separate from company work; the local PostgreSQL only.
- **Decisions are the user's.** Where my recommendation was overridden — schema-per-tenant, the single-branch test seed — the right response was to implement the choice properly and record the consequence, not to re-argue.

---

## 12. Exactly where to resume

### Nothing is outstanding — start straight on the work below

**A. ~~Rotate the database passwords~~ — done.** Both roles were rotated, the secrets re-set under their new names, and the originals scrubbed from `db/01_create_database_and_roles.sql`. `PROJECT_STATUS.md` §7A has the commands for any future rotation.

**B. ~~Put the project under version control~~ — done.** The repository is live at `https://github.com/zafar-dotcom/school-management-system` (private), branch `main`, first commit pushed.

Verified before pushing: 131 files committed, `bin/` and `obj/` excluded, no real password anywhere, and only `secrets.example.json` — the placeholder template — included. Real credentials live in user secrets outside the project folder.

**Before every future commit**, run section 8 of `HOW_TO_VERIFY.md` — the scan for passwords in the project folder. Removing a secret after it has been pushed is painful.

**C. ~~Make the application start~~ — done, 27 October 2026.** All six steps are built and
were verified by running the application against the live database: the startup schema gate,
the setup wizard, login, the branch-filtered class list, server-set `BranchId`, and
membership revocation. `PROJECT_STATUS.md` §4 records exactly what was proved and how.

Run it with:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/SMS.Web
```

User secrets load only in Development — without that variable you get
"ConnectionStrings:App is not configured".

### Then: the tests that do not exist yet

This now comes before feature work. Everything above was verified once, by hand, and nothing
stops it regressing — and these are the behaviours architecture §10 requires tests for.
`PROJECT_STATUS.md` §7D lists the four.

### Then: the first feature module

**Academic setup** — academic year, class, section, subject. It is first because everything else references it, it is small, and it extends the `class` table that already exists.

Module order, fixed in architecture §9.1:

```
academic setup → teachers → students → attendance → fees → exams → reports
```

Teachers before students because it is smaller and is the first module to exercise the login-to-person link across schemas. Attendance before fees because fees involves money and carries the most unanswered questions.

**Do not design all seven modules up front.** Design each one just before building it.

---

## 13. Decisions still open

None of these block the next work.

| Decision | Needed before |
|---|---|
| Windows / domain login in scope? | Identity configuration |
| Licence file format and contents | The installer |
| Which modules ship in the first release | Starting feature modules |
| Attendance: daily or period-wise? | The attendance module |
| Currency, decimal precision, invoice cycle, late fees, payment gateway | The fees module |
| Grading scheme, report-card layout | The exams module |
| Target market and country | Later — affects retention and consent rules |
| Branch address detail, multi-school membership, `display_name` semantics | Cosmetic; decide any time |

One gap recorded for the future, **not a problem with one school**: every tenant role can read all of `master`, including `app_user` and `school_membership`. Under multi-school that becomes cross-tenant readable data. Two answers are documented — row-level security keyed to a session variable, or a separate master-only connection. Decide it when phase D3 activates; do not build it now.

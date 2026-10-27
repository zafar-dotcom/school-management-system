# Session Handover — 26/27 October 2026

**Read this first in a new session.** It covers everything that happened, every decision and why, what reversed along the way, what was built, and exactly where to resume.

---

## 0. Starting the new session — paste this

Copy the block below as the first message of the new session:

```
Project: School Management System.

Read these six files before doing anything, in this order:
  1. SESSION_HANDOVER.md       - the whole picture, decisions and reversals
  2. PROJECT_STATUS.md         - what is built, verified, and pending
  3. School_Management_System_Architecture_and_Decisions.md  - decisions and why
  4. Master_Schema_Design.md   - the master schema
  5. HOW_TO_VERIFY.md          - how to check anything is correct
  6. SETUP_NEW_MACHINE.md      - only if this machine is not set up yet

Then confirm back to me, briefly:
  - what is already built
  - what is pending, and what you would do next

Do not re-open decisions already recorded as closed - see section 4 of the
handover for the full list, including what reversed.

Two standing rules: this project only ever touches the LOCAL PostgreSQL
(verify with SELECT inet_server_addr(), must be ::1 or 127.0.0.1), and the
structures listed in section 6 of the handover must not be removed.
```

Everything needed to continue is in those six files. Nothing from any previous conversation is required.

**On a machine that has never run this project**, start with `SETUP_NEW_MACHINE.md` — it
takes you from a bare Windows laptop to a running application in eleven steps, with a check
after each one. The other five documents assume the machine is already working.

---

## 1. How to use these six documents

| Document | Read it when |
|---|---|
| **`SESSION_HANDOVER.md`** (this file) | Starting a new session — gives you the whole picture |
| `PROJECT_STATUS.md` | You need the current build state and next actions |
| `School_Management_System_Architecture_and_Decisions.md` | You need a decision and its reasoning. Revision 9 |
| `Master_Schema_Design.md` | You are working on the database |
| `HOW_TO_VERIFY.md` | You want to confirm something is actually correct — connections, migrations, permissions, tables |
| `SETUP_NEW_MACHINE.md` | This machine has never run the project. Bare laptop to running app, with a check after each step |

Order for a cold start: this file → `PROJECT_STATUS.md` → the others as needed.

`HOW_TO_VERIFY.md` is the one to keep open while working. It is a lookup table of *"I want to check X"* → the command → how to read the result. Everything in it is read-only and safe to run at any time.

---

## 2. What has been achieved, across two working sessions

**Session 1 — architecture and the data layer.** Began with a single architecture document
and an empty folder. Ended with a reviewed architecture (seven revisions, every version-1
decision closed), a complete `master` schema design, a six-project .NET 10 solution
compiling clean, a live PostgreSQL database with both schemas created by EF Core
migrations, and a verified two-role security model. The data layer worked; nothing above it
existed.

**Session 2 — the walking skeleton, then two hosts.** The application now starts, checks
its own schema, sets itself up on first run, signs a user in, and serves a branch-filtered
screen. The use cases were then lifted out of the controllers into `SMS.Application`, and
`SMS.Api` was added beside `SMS.Web` so a future mobile and desktop client have somewhere
to call. Seven projects, still zero warnings — and warnings are now build errors.

Also in session 2: two decided items implemented (DPAPI-protected keys, the cross-schema
foreign key), central package management adopted, five further decisions closed (market,
language, academic calendar, branding, accessibility), and three real faults found and
fixed — see section 10.

**Where that leaves it:** the foundation is complete and exercised end to end. **No
automated tests exist**, and that is now the single largest gap. No licence enforcement, no
installer, and no feature module.

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
| 14 | `SMS.Api` timing | **Changed:** deferred to phase D5 → **built now, alongside `SMS.Web`** (Revision 8). A mobile client is planned as well as WPF. The web UI stays server-rendered MVC. |
| 15 | Per-module class libraries | **Confirmed not happening.** Modules are folders inside the layers, as §3.3 always said (Revision 8) |
| 16 | Target market | **Pakistan.** `Asia/Karachi`, PKR, `dd/MM/yyyy` are real defaults now, not placeholders |
| 17 | Interface language | **English only**, expected permanently. Strings hardcoded, no resource files |
| 18 | Academic year | **Admin-created from a screen.** The product assumes no calendar — presets only prefill the form |
| 19 | Branding and accessibility | **WCAG 2.1 AA**, with a fixed set of pre-validated themes plus a free-form per-school logo. Free colour choice and an AA commitment cannot coexist |

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
<repo root>\
├── Directory.Packages.props    every NuGet version, declared once
├── Directory.Build.props       net10.0, Nullable, warnings-as-errors
├── SchoolManagementSystem.slnx
├── src\
│   ├── SMS.Domain\          12 entity files, enums, base types — no EF reference
│   ├── SMS.Application\     use cases: Access, Setup, Classes, plus the store
│   │                        contracts. Does NOT reference Infrastructure
│   ├── SMS.Infrastructure\  2 DbContexts, 11 configurations, interceptor,
│   │                        tenancy, clock, schema check, stores, 4 migrations
│   ├── SMS.Web\             MVC host — setup wizard, login, classes, cookies
│   └── SMS.Api\             JSON host — JWT, same handlers, in-process
├── tests\                   both still EMPTY — the largest gap
├── db\                      3 SQL scripts
└── 6 markdown documents
```

**Both hosts call `SMS.Application` directly. `SMS.Web` never calls `SMS.Api` over HTTP** —
same process, so an HTTP hop would buy nothing. Architecture §11.

**Database, live and verified:** `master` holds 13 tables, `school_main` holds `class`.
Four migrations applied across the two contexts.

**Ports:** Web `https://localhost:7106`, API `https://localhost:7286`. Both need
`ASPNETCORE_ENVIRONMENT=Development` or user secrets do not load.

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

## 10. Gotchas that cost time — eighteen of them

Recorded in full in `PROJECT_STATUS.md` §6. The ones most likely to recur:

1. **`UseXminAsConcurrencyToken()` no longer exists** in the EF Core 10 Npgsql provider, though tutorials still show it. Replaced by a local `UseXminConcurrencyToken()` extension.
2. **A namespace that matches an entity name breaks the build.** It happened twice — first `School`, then `Installation`. `School`, `Installation`, `License` and `Branch` are all taken. Namespaces are `SchoolData`, `SchoolSchema` and `Setup`.
3. **`CREATE DATABASE` cannot run inside a pgAdmin transaction.** Select that single line and press F5 — selecting text runs only the selection.
4. **`LC_COLLATE 'en_US.UTF-8'` fails on Windows PostgreSQL.** Omit locale clauses entirely.
5. **`dotnet ef migrations script --no-build` silently uses a stale assembly** and produces an empty script.

### Three faults found in session 2, all of which had been failing silently

6. **A password containing `;` broke the app connection string entirely.** `ConnectionStrings:App` held an unquoted 28-character password with a semicolon in it, so Npgsql split the tail into a bogus keyword and threw a *format* error before ever reaching the server. Nothing had connected as the application role until then, so it had never surfaced. Quote it — `Password=<"the value in double quotes">`. **Re-check after every rotation** — a generated password can contain one at any time.
7. **The pre-commit password scan never ran.** `HOW_TO_VERIFY.md` §8 passed `-Include` and `-Recurse` to `Select-String`, which has neither. It threw, returned nothing, and the check reported "clean" without having looked. A safety check that fails open is worse than no check. Corrected.
8. **Nothing updated `school.schema_version` after setup.** Only the setup wizard wrote it, so the second school-schema migration made the startup check refuse to serve — and on a customer's machine the *first* update shipped would have bricked the install until someone edited the row by hand, with no DBA on site. The migrations history is authoritative; a stale cache is now refreshed with a warning rather than refused.

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

### Done, so nobody redoes it

| Item | Status |
|---|---|
| Architecture and `master` schema design | Closed, Revisions 1–9 |
| Database, both schemas, two-role model | Built, verified against the running server |
| Database passwords rotated, originals scrubbed | Done |
| Version control | Done. Private repo, branch `main` |
| **Walking skeleton** — `IClock`, `ITenantContext`, DI, shared connection and transaction, startup schema check, Identity with cookies, setup wizard, login, branch-filtered class list | Done, verified by running it |
| **DPAPI-protected data-protection keys** | Done, gated on `SingleTenant` |
| **Cross-schema FK `class.branch_id` → `master.branch`** | Done, and verified to reject an invalid branch |
| **Use cases extracted into `SMS.Application`** | Done. Controllers are thin |
| **`SMS.Api`** — JWT, same handlers in-process | Done, exercised end to end |
| **Central package management, warnings-as-errors** | Done, the gate verified by compiling a deliberate warning |
| **Five decisions closed** — market, language, academic calendar, branding, accessibility | Done, §4 rows 16–19 |

Run it with:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/SMS.Web     # https://localhost:7106
dotnet run --project src/SMS.Api     # https://localhost:7286
```

User secrets load **only** in Development — without that variable you get
"ConnectionStrings:App is not configured", which reads like a database fault and is not.

### Pending, in order. `PROJECT_STATUS.md` §5 has sizes and detail

1. **Automated tests.** Both test projects are empty. Everything above was verified by hand exactly once and nothing stops it regressing. §7D lists the four that matter.
2. **`master.school_branding` migration.** Designed in `Master_Schema_Design.md` §3.11. Cheapest now, while the table has one dev row and no production rows.
3. **Academic setup module.** Nothing blocks it. The `academic_year` table shape is in `PROJECT_STATUS.md` §7F.
4. **UI design and the theme set**, alongside module 3.
5. **Audit log capture** — field-level changes into `master.audit_log`.
6. **Licence enforcement**, then the `installation` row, then the generator tool.
7. **Installer and packaging.**
8. **The remaining six feature modules.**

### The first feature module

**Academic setup** — academic year, class, section, subject. First because everything else
references it, it is small, and it extends the `class` table the skeleton already uses.

Order fixed in architecture §9.1:

```
academic setup → teachers → students → attendance → fees → exams → reports
```

Teachers before students because it is smaller and is the first module to exercise the
login-to-person link across schemas. Attendance before fees because fees involves money and
carries the most unanswered questions.

**Do not design all seven modules up front.** Design each one just before building it.

### The interface is deliberately unstyled until module 3

Stock Bootstrap, no design applied. That is a decision: the class list exists to prove the
branch filter and prints a raw branch GUID to do it. **Remove that column and the note under
the form when the screen becomes real.**

The decisions it was waiting on are now closed — English only, per-school logo with a fixed
theme set, WCAG 2.1 AA. What remains is the work itself, plus **thirteen constraints in
`PROJECT_STATUS.md` §7G** that must be read before any UI work starts. The three most
easily forgotten:

- **No CDN. Every asset local** — a school server may have no internet, and a stylesheet that 404s offline takes the layout with it.
- **Printing is a first-class requirement** — the licence read-only state explicitly preserves view, search, print and export.
- **The maintenance page must stay dependency-free** — it renders precisely when the schema is wrong or the database is unreachable.

---

## 13. Decisions still open

None of these block the next work.

| Decision | Needed before |
|---|---|
| Windows / domain login in scope? | Identity configuration |
| Licence file format and contents | The installer |
| Which modules ship in the first release | Starting feature modules |
| Attendance: daily or period-wise? | The attendance module |
| ~~Academic year start month~~ | **Closed.** Admin-created from a screen; no calendar assumed — `PROJECT_STATUS.md` §7F |
| ~~Per-school colour versus WCAG 2.1 AA~~ | **Closed.** Fixed pre-validated themes, free-form logo — `PROJECT_STATUS.md` §7G |
| Invoice cycle, late fees, tax fields, payment gateway | The fees module |
| Grading scheme, report-card layout | The exams module |
| Branch address detail, multi-school membership, `display_name` semantics | Cosmetic; decide any time |

**Closed 27 October 2026:** target market is **Pakistan** (so PKR, `Asia/Karachi`, `dd/MM/yyyy`); interface is **English only**; **each school sets its own logo and colours**; accessibility target is **WCAG 2.1 AA**. Architecture §12 carries the consequences, including the branding-versus-contrast conflict that has to be resolved before the branding columns are designed.

One gap recorded for the future, **not a problem with one school**: every tenant role can read all of `master`, including `app_user` and `school_membership`. Under multi-school that becomes cross-tenant readable data. Two answers are documented — row-level security keyed to a session variable, or a separate master-only connection. Decide it when phase D3 activates; do not build it now.

---

## 14. Decided for the future — designed, not built

These are **closed decisions with no code behind them yet**. A new session should implement
them as written rather than re-deciding, and should not treat their absence as an oversight.

| Area | What was decided | Where the detail is |
|---|---|---|
| **Academic year** | The School Admin creates them from a screen. The product assumes **no calendar**. April–March, August–May, September–June and January–December are offered as form presets only — not stored, not enforced | `PROJECT_STATUS.md` §7F, with the table shape |
| **Date columns** | Session dates, term dates and date of birth are **`date`**, not `timestamptz` — a deliberate exception to architecture §8. Stored as `timestamptz`, 1 April in `Asia/Karachi` renders as 31 March anywhere formatting in UTC. Attendance timestamps stay `timestamptz` | `PROJECT_STATUS.md` §7F |
| **Overlapping sessions** | Prevented by a PostgreSQL exclusion constraint on `daterange`, not a form validator. No extension needed | `PROJECT_STATUS.md` §7F |
| **Branding** | A fixed set of pre-validated themes plus a free-form per-school logo. A new `master.school_branding` table — separate from `school`, because `school` is read on every request and a `bytea` column there would ride along | `Master_Schema_Design.md` §3.11 |
| **Logo storage** | **In the database**, not on disk. Backup and restore stay one operation, which matters when the customer owns the machine; and it avoids the predictable-file-path tenancy surface | `Master_Schema_Design.md` §3.11 |
| **Logo formats** | PNG and JPEG only. **SVG rejected** — it is a document that can carry script, inert in an `<img>` tag only until somebody inlines it to recolour it | `Master_Schema_Design.md` §3.11 |
| **Accessibility** | WCAG 2.1 AA, designed in from the first screen. Bootstrap does not reach it unaided — the current scaffold uses `.text-muted` on white and the default `btn-primary`, both of which need replacing | `PROJECT_STATUS.md` §7G |
| **Branch-scoped tables** | **Every one added from now on needs its own cross-schema foreign key to `master.branch`**, written by hand with `migrationBuilder.Sql`. EF cannot generate it across two contexts | `PROJECT_STATUS.md` §7C2 |
| **Licence** | Yearly. Warning → grace → read-only, never a lockout. Signature is the only truth; every other column is a decoded cache. Installation key and clock high-water mark defeat casual evasion | `Master_Schema_Design.md` §3.6–3.8, handover §7 |
| **Module boundaries** | Folders inside the layers — `SMS.Application/Students/` — never per-module class libraries. Confirmed against a company project whose Business project holds 550 files across 34 feature folders and still has not split | Architecture §11 |
| **Two hosts** | `SMS.Web` and `SMS.Api` are siblings over `SMS.Application`, both in-process. **`SMS.Web` must never call `SMS.Api` over HTTP** | Architecture §11; the rule is also in both `.csproj` files |
| **Deployment mode** | Read only at the host edge, never in domain or application logic. Grepping `SMS.Domain` and `SMS.Application` for it must return nothing | Architecture §7.2 |

### Deferred phases, with their triggers

Each is **additive** provided §6 of this document was honoured. None needs version 1's data
model revisited. Architecture §9.2 has the activation steps.

| Phase | Trigger |
|---|---|
| **D1 — LAN access (Model B)** | A school with more than one office. Mostly configuration and installer work |
| **D2 — Multiple branches** | A school opens a second campus. **First task: test the branch filter with two branches**, before any branch UI |
| **D3 — Multiple schools** | A second school on one install, or the first hosted customer. Brings `SET ROLE` switching, tenant provisioning, and the `master` readability gap above |
| **D4 — Cloud hosting (Model C)** | A customer who will not host it themselves. Needs the provider decision, and subscriptions replace the offline licence |
| **D5 — WPF desktop client** | A concrete desktop requirement. **The API it needs already exists** — this is now a client, not a server, project |

### Still genuinely undecided

Listed in §13 above. The ones that will bite soonest: **the licence file format** (blocks the
installer), **attendance daily or period-wise** (shapes that whole module), and
**Pakistan's data-protection position for records about minors** — marked VERIFY, deliberately
not assumed to mirror GDPR.

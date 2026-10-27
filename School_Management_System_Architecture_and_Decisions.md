# School Management System — Architecture and Project Decisions

**Document purpose:** Record the questions, answers, agreed decisions, architecture, and initial development plan discussed for the School Management System (SMS).

**Companion documents:** `SESSION_HANDOVER.md` — read first in a new session. `PROJECT_STATUS.md` — what is built, verified and outstanding right now. `Master_Schema_Design.md` — the `master` schema design.

**Status:** Architecture agreed in full. **Version 1 is scoped to Model A only — a single school, a single branch, installed on one computer.** Multi-branch, LAN, multi-school, and cloud operation are designed for and deferred until there is demand; Section 1 defines the boundary and Section 9.2 the activation path.

**Revision 7 — plan finalised**

- **Confirmed: version 1 is one school with one branch.** Production ships a single `Main` branch; the structure in Section 1.3 is still built so multi-branch stays additive.
- **Decided: academic years and term calendars are school-wide** — no `BranchId`. Safe to defer here only because the table holds a handful of rows (Section 4.4).
- **Decided: student records are school-wide**, with the current branch held on the enrolment (Section 4.4).
- **Decided: yearly subscription**, with warning → grace → read-only on expiry, and offline renewal (`Master_Schema_Design.md` §3.8).
- **Feature module order fixed**: academic setup → teachers → students → attendance → fees → exams → reports, with each module's prerequisites listed (Section 9.1).
- Plan is closed for version 1. Remaining open items affect later modules, not the start.

**Revision 6 — changes from Revision 5**

- **Scope narrowed to Model A for version 1**: one school, one branch, one computer. Multi-branch, LAN sharing, multi-school, cloud, and the API/WPF client are deferred (Section 1.1 and 1.2).
- Added Section 1.3, **the structural decisions kept in version 1 so deferred features stay additive** — the two-schema split, non-nullable `BranchId`, `SchoolMembership`, context-resolved school and branch, and the data conventions. Each is cheap now and expensive to retrofit.
- Added Section 1.4 stating the commitment plainly: honour 1.3 and later features are additive; skip it and "add multi-tenancy later" becomes a rewrite.
- Kept the **per-school role and grant pattern in version 1** (`role_school_main` with `USAGE` on its own schema only), so phase D3 adds only role switching rather than inventing the permission model later (Section 9.1, step 3).
- Split the roadmap into **9.1 (version 1)** and **9.2 (deferred phases D1 to D5)**, each with its trigger and its activation steps.
- Marked which parts of Sections 4 to 7 describe deferred behaviour rather than version 1 behaviour.

**Revision 5 — changes from Revision 4**

- **Tenancy model changed from database-per-school to schema-per-school**, in one shared PostgreSQL database with a `master` schema (Sections 4.1 and 11). This reverses the Revision 4 decision.
- Added the guardrail that makes it safe: **one PostgreSQL role per school, granted `USAGE` on its own schema only**, so a context bug produces a permission error rather than another school's data (Section 4.5).
- Specified the `SET ROLE` + `SET search_path` mechanism, the ban on `No Reset On Close`, and the requirement to test that the *database* rejects a mis-set context (Sections 4.5 and 8).
- Required schema names to be generated from a validated slug and never derived from request data, since they are interpolated into SQL (Section 4.1).
- **Section 4.7 substantially simplified:** cross-schema foreign keys are real, so the identity-to-person link is now an enforced constraint and a single SQL join, and the branch-registry mirror with its synchronisation obligation is removed entirely.
- Added Section 4.9 recording what a single shared database costs: no per-tenant point-in-time recovery, shared blast radius, and total table count as the scaling limit to watch.
- Provisioning is now `CREATE SCHEMA` + role + grants in one transaction, so a failed provision rolls back cleanly (Sections 7.5 and 9).
- One Npgsql connection pool now serves every tenant instead of one per school (Section 8).

**Revision 4 — changes from Revision 3**

- Recorded **Code First with EF Core migrations** as the schema approach, with the rules for applying it (Sections 2 and 11).
- **Database changed from Microsoft SQL Server to PostgreSQL**, with the Npgsql EF Core provider (Sections 2 and 11).
- Concurrency token changed from `rowversion` to PostgreSQL's `xmin` system column (Section 8).
- Recorded that PostgreSQL **cannot query across databases at all**, which makes the application-level master-to-tenant join a hard constraint rather than a preference, and ruled out `postgres_fdw` (Section 4.7).
- Recorded **schema-per-school as considered and rejected**, with the reasoning, since PostgreSQL makes it a genuine option (Section 4.1).
- Adopted `snake_case` database naming via `EFCore.NamingConventions` (Section 2).
- Removed the SQL Server Express size limit and licence obligations from on-premise; added PostgreSQL installation and patching as our responsibility instead (Section 7.6).
- Updated cloud targets to managed PostgreSQL offerings, and noted PgBouncer as the scaling answer if tenant connection counts strain (Sections 7.5 and 8).

**Revision 3 — changes from Revision 2**

- Added the three customer deployment situations and established that they are **one application in two deployment modes**, not three products (Section 7.1).
- Decided that deployment mode is configuration, never a fork in business logic, and that **both schemas ship even on a single-school on-premise install** (Section 7.2).
- Defined the on-premise topology: database and application both on a **dedicated always-on server** on the school network, office PCs reaching it by browser (Section 7.3).
- Established that keeping the application on that server means **PostgreSQL is never exposed to the LAN** — the security argument that settles where the application is installed (Section 7.3).
- Recorded why the application is **not** installed on every office PC, and what is required if a customer insists (Section 7.3).
- Set **on-premise as the first build target**, which unblocks school onboarding without a cloud provider decision (Sections 7.5 and 9).
- Added the operational obligations on-premise selling creates — updates, backups, remote support, LAN certificates, database sizing, offline licensing (Section 7.6).
- Added the on-premise to cloud upgrade path (Section 7.7).

**Revision 2 — changes from Revision 1**

- Added the multi-branch model: a school may operate several branches (Section 4.1).
- Established that the **school** is the isolation boundary and the **branch** is a scope inside it.
- Added explicit branch-isolation rules to close the branch-level access loopholes that a shared database introduces (Section 4.6).
- Replaced reliance on ASP.NET Core Identity roles with an explicit membership model, because stock Identity roles cannot express per-school or per-branch access (Section 5.2).
- Named the cross-database links that cannot be enforced by a foreign key and defined how they are handled (Section 4.7).
- Added tenant schema versioning (Section 4.8) and a new section of cross-cutting concerns that must be decided before feature work (Section 8).
- Reordered the roadmap so identity and tenancy are designed together (Section 9).

---

## 1. Project Goal and Current Scope

Build a commercial School Management System for schools, as a web application. Both the browser and any future client must use the server-side application and must never connect directly to the PostgreSQL database.

### 1.1 What we are building now

**Version 1 targets one situation only: Model A — a single school, installed on one computer at that school, with PostgreSQL on the same machine.**

That means, for now:

- One school per installation
- One branch (`Main`), created automatically at setup
- One local PostgreSQL database
- Browser access on that computer
- No cloud hosting, no multi-school platform, no self-service onboarding

Everything in Sections 4 to 7 describes the **full** architecture, including multi-school and multi-branch operation. That is intentional: the structure is built now, and only a single school and single branch are *activated*. Section 1.3 explains which parts are built and which are deferred.

### 1.2 Deferred until there is demand

These are designed for and reachable, but **not built in version 1**:

| Deferred feature | What it is | Roughly what it takes to activate |
|---|---|---|
| **Shared access over the school LAN** (Model B) | The same install reached from other office PCs by browser | Configuration: bind beyond localhost, add a certificate, open one port. Days, not weeks. |
| **Multiple branches** (Case 3, part) | More than one campus under one school, head-office consolidated view | Allow more than one branch row, build branch-selection UI and the cross-branch reporting service. The data model already carries it. |
| **Multiple schools / multi-tenancy** | Several schools served by one installation | Add schema-per-school provisioning, school selection, and per-school roles. The `master` / school schema split already exists. |
| **Cloud hosting** (Model C) | Hosted by us, branches connect over the internet | Choose a provider, implement cloud provisioning, switch deployment mode to `MultiTenant`. |
| **Web API and WPF desktop client** | A desktop client for schools that want one | Add the API host and JWT authentication, reusing the same application layer. |
| **Subscriptions and platform administration** | Billing, Super Admin portal, self-service signup | Only meaningful once there is more than one school. |

### 1.3 What we keep now so that later stays cheap

**This is the part that must not be traded away for short-term speed.** Each item below costs very little to include in version 1 and is disproportionately expensive to retrofit afterwards, because retrofitting means touching every table, every query, or every authorization check that already exists.

| Keep now | Cost if we keep it now | Cost if we add it later instead |
|---|---|---|
| **Two schemas: `master` + `school_main`** | Naming in EF configuration. Effectively zero. | Splitting a flat schema in two: a data migration plus rewriting every query's qualification. |
| **Non-nullable `BranchId` on branch-scoped tables**, with one `Main` branch | One column, one global query filter configured once | Adding a column to ~15 tables, backfilling, revisiting every query, every unique index, and every write path. This is the single most expensive retrofit on the list. |
| **`SchoolMembership` driving authorization**, not Identity role tables | One table and a claims step. Branch scope is needed regardless. | Rewriting every authorization check in the system. |
| **School and branch resolved from session context**, never hardcoded | A context object that currently always resolves to the same values | Finding and changing every place that assumed a single implicit school. |
| **Conventions: audit interceptor, `xmin` concurrency, UTC `timestamptz`, snake_case, opaque ids** | Configured once, before the first entity exists | Backfilling audit history that was never recorded; a data migration for types and identifiers. |

Total overhead for version 1: on the order of two to three days. It is the difference between *adding a feature* later and *rewriting the foundation* later.

**What we deliberately do not build now**, because these are genuinely cheap to add when needed: dynamic schema switching and `SET ROLE` machinery, tenant provisioning, the school-selection screen, the Super Admin portal, branch-selection UI, the cross-branch reporting service, and cloud deployment.

### 1.4 The commitment this makes

With Section 1.3 honoured, each deferred feature is an **additive** change: new rows, new screens, new configuration. Without it, "add multi-tenancy later" means a rewrite, and the honest answer to a customer asking for it would be a new project.

Treat Section 1.3 as a constraint on version 1, not as optional polish.

## 2. Final Decisions at a Glance

| Area | Final decision |
|---|---|
| Web application | ASP.NET Core MVC |
| Web UI | Razor Views, HTML, CSS, JavaScript, and Bootstrap |
| .NET | .NET 10 (LTS) |
| Overall application style | Client-server |
| Server deployment style | Modular monolith; not microservices |
| Internal code organization | Clean Architecture principles |
| Data store | PostgreSQL |
| ORM | Entity Framework Core with the Npgsql provider |
| Isolation boundary | The school. One PostgreSQL **schema** per school, in one shared database |
| Branch model | Branches are scoped **inside** the school's schema by a non-nullable `BranchId`; a branch is not a separate schema |
| Tenant enforcement | `search_path` plus a **dedicated PostgreSQL role per school** granted USAGE on its own schema only |
| Branch isolation | Enforced in software: resolved server-side context, mandatory query filters, server-set `BranchId` on write |
| Central data | A `master` schema for the school/tenant registry, branch registry, identity, and subscriptions |
| User management | ASP.NET Core Identity, for credentials and password security only |
| Access model | Explicit `SchoolMembership` records; roles are **not** read from Identity role tables |
| MVC browser authentication | Secure, HTTP-only authentication cookies |
| Future API authentication | JWT Bearer tokens |
| Future desktop client | WPF, using the Web API |
| Authorization | Role-based and policy-based, always combined with school and branch scope checks |
| Logging | Serilog |
| Automated testing | xUnit, with integration tests against a real PostgreSQL |
| Audit trail | Mandatory from the start, via an EF Core `SaveChanges` interceptor |
| Concurrency | Optimistic concurrency using PostgreSQL's `xmin` system column as the token |
| Date and time storage | `timestamptz`, UTC in the database; school or branch time zone applied for display |
| Database naming convention | `snake_case` via `EFCore.NamingConventions`; entities stay PascalCase in C# |
| Schema approach | **Code First** with EF Core migrations; schema still designed deliberately before coding |
| Persistence mapping | `IEntityTypeConfiguration<T>` classes in Infrastructure; no EF attributes on domain entities |
| Migration execution | A deliberate installer/deployment step; never auto-migrate on application startup |
| Deployment models | Three customer situations, two modes: `SingleTenant` (on-premise) and `MultiTenant` (cloud). **Only Model A is built in version 1** |
| Deployment mode | A configuration setting, never a fork in business logic |
| Schemas shipped | `master` + one school schema in **every** model, including a single-school on-premise install |
| On-premise application placement | Installed once, on the always-on server that also hosts the database; office PCs use a browser. PostgreSQL is never exposed to the LAN |
| Version 1 scope | **Model A only** — one school, one branch, one computer. Everything else deferred (Sections 1.1–1.3) |
| Structure kept in v1 | Two schemas, non-nullable `BranchId`, `SchoolMembership`, context-resolved school/branch — so deferred features stay additive |
| Subscription model | **Yearly**, sold per installation. Signed offline licence file; cloud uses subscription records later |
| Expiry behaviour | 30-day warning → 14-day grace → **read-only**. Never a lockout, never data loss (`Master_Schema_Design.md` §3.8) |
| Hosting | Provider-independent application logic; cloud provider not yet selected |
| Future hosting options | Azure, AWS, or a VPS, after cost and operational comparison |

### Important authentication clarification

The decision is **ASP.NET Core Identity for user management**, with secure cookies for the MVC browser application and JWT Bearer authentication for API clients such as the future WPF app.

We should not force the MVC website to store a JWT in browser storage just because the future API will use JWT. Avoid storing access tokens in `localStorage`; an HTTP-only, secure cookie is generally a better fit for a server-rendered MVC website.

**Identity is used for credentials, not for authorization.** See Section 5.2 for why its role tables are not the source of truth here.

## 3. Architecture We Will Follow

These architecture terms describe different aspects of the design and can be used together.

### 3.1 Client-server architecture

Clients communicate with the application server over HTTP/HTTPS. The server authenticates users, authorizes actions, applies business rules, resolves the school and branch context, and accesses the database.

- **Initial client:** Web browser.
- **Future client:** WPF desktop application.
- **Server:** ASP.NET Core MVC initially; a Web API will be added for API clients when needed.
- **Data store:** PostgreSQL databases accessed only by trusted server-side code.

The browser and WPF client must never connect directly to PostgreSQL.

### 3.2 Three-tier separation

Conceptually, the application separates:

1. **Presentation:** Razor Views and MVC controllers; later, the WPF UI and API endpoints.
2. **Application/business processing:** Use cases, application services, validation, and orchestration.
3. **Data/infrastructure:** EF Core, PostgreSQL access, database provisioning, and other external integrations.

This is a conceptual separation of responsibilities. It does not require each tier to be a separate physical server or deployment.

### 3.3 Modular monolith

The backend will be organized into cohesive functional modules, but initially deployed as one application rather than as independently deployed microservices.

Likely functional modules include:

- Identity and access
- Schools, branches, and tenant administration
- Students and parents
- Teachers and staff
- Academics
- Attendance
- Fees and payments
- Examinations and results
- Reports and dashboards

We will introduce module boundaries as they are justified by the requirements. We will not create unnecessary projects, abstractions, or generic repositories merely to make the solution look more elaborate.

### 3.4 Clean Architecture principles

Keep business/domain rules independent from UI frameworks and database implementation details.

A practical starting solution structure is:

```text
SchoolManagementSystem/
├── SchoolManagementSystem.sln
├── src/
│   ├── SMS.Web/                 # ASP.NET Core MVC + Razor Views
│   ├── SMS.Domain/              # Domain entities and business rules
│   ├── SMS.Application/         # Use cases and application contracts
│   ├── SMS.Infrastructure/      # EF Core, PostgreSQL, external infrastructure
│   └── SMS.Api/                 # Add when API clients are introduced
└── tests/
    ├── SMS.UnitTests/
    └── SMS.IntegrationTests/
```

This is a **proposed starting structure**, not a requirement to create every project immediately. Final project boundaries and references should be checked before generating the solution.

**Dependency direction.** The runtime request flows downward through presentation, application, and infrastructure, but the *project references* do not. `SMS.Application` must never reference `SMS.Infrastructure`. The application layer declares interfaces, infrastructure implements them, and the web host wires them together at startup. The diagrams in Section 6 show the runtime flow, not the reference direction — do not add a project reference based on them.

### 3.5 Multi-tenant architecture

The platform serves multiple schools. The tenancy model has two levels, and they are deliberately different in kind:

- **School (tenant):** the isolation boundary. Each school has its own PostgreSQL schema, protected by a dedicated database role.
- **Branch (scope):** a campus or location within a school. Branches share the school's database and are separated by scope rules in software.

This is separate from the modular-monolith decision: one describes how the backend is deployed and organized; the other describes how customer data is partitioned. Section 4.1 explains why the boundary sits at the school rather than at the branch.

## 4. Multi-Tenant and Multi-Branch Database Design

> **Scope note.** This section describes the full tenancy model. **Version 1 builds the structure and activates one school and one branch** — the two-schema split, the non-nullable `BranchId`, and the global query filter are all built now (Section 1.3). The parts that wait for demand are per-school roles and `SET ROLE` switching, dynamic schema resolution, and tenant provisioning (Sections 4.5 and 9.2). Read this section in full before activating any of them.

### 4.1 Tenancy model: one database, a schema per school, a branch scope inside it

**Decision: a single PostgreSQL database containing one `master` schema plus one schema per school. Branches live inside the school's schema and are identified by a `BranchId` column on branch-scoped tables.**

Three levels, each enforced differently:

| Level | Mechanism | Enforced by |
|---|---|---|
| Platform | The `master` schema | Database grants |
| School (tenant) | One schema per school, e.g. `school_greenfield` | `search_path` + **per-tenant database role and grants** (Section 4.5) |
| Branch | A non-nullable `BranchId` column | Application code: query filters and server-set values (Section 4.6) |

**What this choice buys.** Cross-schema queries work inside one database, which removes the hardest constraint of the previous database-per-school design: the master-to-tenant link can now be a **real, enforced foreign key** rather than an unenforceable column joined in application code. Section 4.7 is substantially simpler as a result, and the branch-registry mirror it previously required is gone. Provisioning also becomes atomic, because `CREATE SCHEMA` runs inside a transaction while `CREATE DATABASE` cannot. Migrations run against many schemas over one connection instead of opening one connection per database.

**What this choice costs, stated plainly.** School isolation is no longer a connection-string boundary. A missed `search_path`, a hardcoded schema name, or a pooled connection reused without its session state reset would leak data between **different paying customers**. This is the most serious failure mode the product has, so it is not left to discipline: Section 4.5 requires a **separate PostgreSQL role per school with `USAGE` granted only on that school's schema**, so that a context bug produces a permission error instead of another school's data. The database still enforces the boundary — through grants rather than through separate databases.

Two secondary costs to accept knowingly: a single database means one WAL, one autovacuum workload, one connection limit, and a shared blast radius for corruption or a runaway query; and per-tenant point-in-time recovery is no longer possible, since PITR operates on the whole database (Section 4.9).

**Branches do not get their own schema.** Reasons:

1. **Student transfers between branches are a normal operation.** Within one schema this is an update plus an audit record. Across schemas it would be a data move with identifier remapping, re-linking of parents, and history that either moves or is lost. The most common chain-school operation must not be the most expensive one.
2. **Head office needs consolidated reporting.** Attendance, fee collection, and enrolment across all branches of a school is a routine requirement. Within one schema it is a single query with no special handling.
3. **Operational cost scales with object count.** Every tenant schema must be provisioned, migrated on each release, and monitored. Multiplying that by the branch count is a large and permanent tax, and it multiplies the table count in a single database — the one resource this design is most sensitive to (Section 4.9).
4. **Branches of a school are one customer.** They share a subscription, a billing relationship, staff who work across sites, a shared subject catalogue, and a shared grading scheme. A hard boundary would work against these, not for them.

**Every school has at least one branch.** Provisioning creates a default branch (`Main`) at the moment the school is created. There is no "school without a branch" state, and `BranchId` is therefore **never nullable** on a branch-scoped table. This removes an entire class of special-case bug.

Adding a branch later is a metadata operation only. It creates no schema and no database, so onboarding a new branch is cheap and fast.

**Schema naming.** Schemas are named from a validated, immutable slug on the school record — for example `school_greenfield` — never from free text. The name is a PostgreSQL identifier that will be interpolated into `SET search_path` and `SET ROLE` statements, so it must be generated by us, validated against a strict pattern (`^school_[a-z0-9_]{1,40}$`), quoted when emitted, and never derived from user input at request time. Treat an unexpected schema name as an error, not as something to sanitise and continue with.

### 4.2 The `master` schema

The `master` schema holds platform-level information:

- School/tenant records, unique tenant identifiers, and each school's **schema name and slug**
- **Branch registry:** every branch of every school, with its status. This is now the *only* branch table — see Section 4.7
- School registration and provisioning status
- **Tenant schema version** for each school schema (see Section 4.8)
- Identity records: user accounts and credentials, via ASP.NET Core Identity
- `SchoolMembership` records and their branch scope (see Section 5.2)
- Subscription plans, subscription status, and plan limits such as maximum branches and maximum students
- Platform-wide configuration and audit of platform-administrator actions

Exact table design remains to be defined during implementation.

The `master` schema must be readable to make any authorization decision. Login, school selection, and branch selection must all be answerable **without entering a tenant schema**. Every application role therefore holds read access to `master` regardless of which school it is serving — which is precisely why `master` must contain no school-operational data.

**The `master` schema exists in every deployment model**, including a single-school on-premise install, where it sits alongside that school's schema in the same local database. It holds the one school record, its branches, the identity and membership records, and the licence state. See Section 7.2 for why it is not merged into the school's schema.

### 4.3 The school schemas (one per school)

Each school's schema contains that school's operational data:

- Students and parent/student relationships
- Teachers and staff, and their branch assignments
- Academic years
- Classes and sections
- Subjects and timetables
- Attendance
- Fee structures, invoices, and payment records
- Examinations and results

Every school schema has an identical table structure. They differ only in the data they hold and in the migration state recorded for each (Section 4.8).

Note that branch records are **not** repeated here. They live once, in `master`, and branch-scoped tables reference them by a real cross-schema foreign key.

### 4.4 Branch-scoped data versus school-wide data

Deciding this per table is required before any schema work. Putting a `BranchId` where it does not belong fragments shared configuration; omitting one where it does belong is a data leak between branches.

**Branch-scoped (carry a non-nullable `BranchId`):**

- Student enrolments and class placements
- Classes and sections
- Timetables
- Attendance records
- Invoices and payment records
- Examination sittings and results
- Staff assignments and teaching allocations

**School-wide (no `BranchId`, shared by all branches):**

- School profile, branding, and settings
- Subject catalogue
- Grading scheme and assessment definitions
- Fee heads and fee templates (the *definitions*; the applied amounts and invoices are branch-scoped)
- Role and permission definitions

**Confirmed:**

- **Academic years and term calendars are school-wide.** No `BranchId`. If a future customer runs different calendars at different branches, adding the column then is a low-cost retrofit — `academic_year` holds a handful of rows, not millions, so backfilling it is trivial. This is the one place where deferring the branch dimension is genuinely safe, and the reason is the row count, not the principle.
- **Student records are school-wide**, with the *current branch* held on the enrolment rather than on the student. This keeps one continuous record per child and makes a future branch transfer an update rather than a data move. Confirm the detailed shape when student management is built, but the placement is settled.

**Still branch-scoped, and not negotiable for the same reason:** classes, sections, timetables, attendance, invoices, payments, and exam results. These are the high-volume tables where a later retrofit is expensive, which is why they carry `BranchId` from day one even though version 1 has a single branch.

**Unique constraints must be reviewed against the branch dimension.** Getting these wrong is a silent correctness bug:

- Admission number: unique **per school**, so a student keeps it across a branch transfer.
- Class and section codes: unique **per branch, per academic year**.
- Staff employee number: unique **per school**.

Any unique index added later must state explicitly whether `BranchId` is part of the key and why.

### 4.5 School isolation requirements

> **Deferred in version 1.** With one school there is one schema and one application role, so the role-switching mechanism below is not built yet. It is specified here because it is the precondition for serving a second school (phase D3), and because building it later against this specification is safe — inventing it later under time pressure is not.

School isolation is the single most important security property of this system. Under schema-per-school it is carried by two independent layers, and **both are required**. The application layer decides which school's data to reach; the database layer guarantees it cannot reach any other.

#### Layer 1 — the database refuses (primary guarantee)

**Decision: one PostgreSQL role per school, with `USAGE` on that school's schema only.**

- Each school gets a role, for example `role_school_greenfield`, granted `USAGE` on its own schema and `SELECT`/`INSERT`/`UPDATE`/`DELETE` on its tables, plus read access to `master` as required by Section 4.2.
- That role is explicitly **not** granted `USAGE` on any other school's schema. Default privileges are revoked from `PUBLIC` on every tenant schema so that nothing is readable by accident.
- The application connects as a low-privilege base role and then issues, per connection, `SET ROLE` to the resolved school's role followed by `SET search_path`.
- **Consequence:** a missed `search_path`, a hardcoded schema name, or a context bug produces a *permission error*, not another school's data. This is what replaces the connection-string boundary of the previous design, and it is why schema-per-school is acceptable here.
- The migration and provisioning roles are separate and more privileged. They are never used to serve a request.

#### Layer 2 — the application resolves correctly

- Never accept an arbitrary connection string, schema name, or role name from a client.
- Never trust a client-supplied `SchoolId` or `X-School-Id` without validating it against the authenticated user's authorized school membership.
- Resolve the tenant using authenticated identity and a validated school selection held in the session, not from the URL or request body.
- Derive the schema name and role name from the validated `School` record in `master`, never from request data (Section 4.1, schema naming).
- Create the school-scoped EF Core `DbContext` only after the tenant context has been resolved and validated.
- Ensure a user from School A cannot read or modify School B's records by changing a URL, request body, or record ID.
- Store role credentials using secret-management facilities appropriate to the deployment environment; do not hardcode them in source code.
- Test cross-tenant access attempts explicitly, including a test that asserts the *database* rejects a deliberately mis-set schema context — not only that the application does.

#### Session state and connection pooling

This is the mechanism most likely to be got wrong, so it is specified rather than left to implementation:

- `SET ROLE` and `SET search_path` are issued on **every connection open**, from a connection interceptor driven by the resolved tenant context. Never set once and assumed to persist.
- Npgsql must reset session state when a connection returns to the pool. **`No Reset On Close` must never be enabled**, and this must be asserted by a test rather than assumed.
- If PgBouncer is introduced later (Section 8), transaction-pooling mode changes session semantics and this mechanism must be re-validated before it is adopted. Treat it as a security change, not a performance tuning change.
- Plan for provisioning, schema migrations, and monitoring across many tenant schemas.

### 4.6 Branch isolation requirements

Because branches share a database, these rules carry the isolation guarantee. They are mandatory, not guidance.

1. **`BranchId` is never bound from a request.** It must not appear in any view model, form post, API request body, or query string that writes data. Model binding a branch identifier is the primary loophole this section exists to close.
2. **`BranchId` is set server-side on write**, from the resolved context, inside the application layer — not by the controller, and not by the client.
3. **A global query filter on `BranchId` is always applied** to branch-scoped entities. The filter is configured once on the `DbContext`, not repeated per query, so a developer writing a new query cannot forget it.
4. **Cross-branch reads are an explicit, named, audited exception.** Users with school-wide scope do not get the filter silently removed. Consolidated reporting calls a specific application service that widens the scope deliberately and records that it did. A disabled filter must never be the default state for any role.
5. **Reads by identifier are re-validated.** Fetching a record by its primary key must confirm the record's branch is within the caller's authorized scope, because a filter can be bypassed by a direct `Find` on a guessed identifier.
6. **The active branch comes from the authenticated session**, never from a URL segment or query parameter that a user could edit.
7. **Branch transfer is a dedicated operation.** Moving a student between branches is an explicit use case with its own authorization check and audit record. It is never an editable field on a general-purpose edit screen.
8. **Tests are required for the branch dimension**, not only the school dimension: a Branch A user attempting to read, update, and delete Branch B records, and a school-wide user confirming that the widened scope is reached only through the intended service.

### 4.7 Links between `master` and a school schema

Because every schema lives in one database, **cross-schema foreign keys are real and enforced by PostgreSQL**. The two links that previously had to be maintained by hand are now ordinary constraints.

**Link 1 — user account to person record.** A teacher is an Identity user in `master` and a `Teacher` row in the school's schema. The same applies to staff, and to students and parents once they are given logins.

- **Ownership is unchanged:** `master` owns the login identity (email, credentials, account status); the school schema owns the person record (the name the school uses, employment details, assignments). Do not duplicate the same source of truth in both.
- The school schema stores the user identifier as a **real foreign key** to the Identity user table in `master`, nullable because a student may exist without a login. Orphaned person records are now prevented by the database rather than by convention.
- Deleting or disabling a user in `master` must still have a defined behaviour for the person record — `ON DELETE SET NULL` with a disable-rather-than-delete policy is the expected answer. Confirm before building staff management.
- Screens needing both sides — "list staff with their login status" — are now a **single SQL join**, not two queries merged in memory. The N+1 risk that the previous design carried is gone.

*Caveat to hold in mind:* these joins couple `master` to tenant schemas at the database level. That is acceptable and is the point of the decision, but it means a future move back to separate databases would be a genuine migration, not a configuration change. Treat that door as closed.

**Link 2 — branch registry.** This link no longer exists. Branches live **once**, in `master`, and branch-scoped tables in each school schema reference them with a real foreign key. The mirrored branch table and the master-to-tenant synchronisation obligation that the previous design required are both removed.

### 4.8 Schema versioning across tenant schemas

With many school schemas, a release will sooner or later leave some migrated and some not. Partial failure is the normal case, not the exception.

- Each school schema carries **its own EF Core migrations history table**, created inside that schema. `master` additionally records the schema version of every school for fast lookup and for the check below.
- The application **refuses to serve a tenant whose schema version does not match the running code**, returning a maintenance response rather than querying a mismatched schema. Serving a stale schema silently is how data gets corrupted.
- Migrations are applied by a deliberate deployment or installer step, never automatically on first request.
- The migration runner iterates tenant schemas over a single connection, is resumable, and reports which schemas succeeded, failed, and were skipped.
- Because all schemas share one database, a migration that takes a heavy lock affects every tenant at once. Long-running DDL must be reviewed for lock behaviour before release — this was less critical when tenants were separate databases.

### 4.9 Consequences of a single shared database

These follow directly from the schema-per-school decision and must be planned for rather than discovered.

- **Per-tenant point-in-time recovery is not available.** PITR restores the whole database. Recovering one school to an earlier moment means restoring the full database to a staging instance and extracting that schema. Document this before promising any recovery guarantee to a customer.
- **Per-tenant export and backup remain easy.** `pg_dump --schema=school_x` produces a single school's data for an offboarding customer or a per-tenant backup.
- **Shared blast radius.** One WAL, one autovacuum workload, one connection limit, one instance to crash. A runaway query or index corruption affects every school, not one.
- **Table count is the scaling limit to watch.** Roughly forty tables per school means a thousand schools is forty thousand tables in one database. PostgreSQL copes into the low thousands of schemas, but catalogue size, autovacuum workload, and `pg_dump` duration all grow with it. This is the metric that decides when the model must change, and it is why the target tenant count in Section 12 matters more under this design than under the previous one.
- **Noisy-neighbour behaviour is not isolated.** Resource limits per school are not available at the database level; any throttling must be applied in the application.

## 5. Authentication, Users, Roles, and Permissions

### 5.1 Authentication

Use ASP.NET Core Identity for user management and password security rather than implementing password hashing, lockout, and related safeguards from scratch.

- **MVC website:** Secure, HTTP-only authentication cookies.
- **Future Web API:** JWT Bearer tokens.
- **Future WPF application:** Authenticate through the API and use JWT Bearer tokens for API requests.
- Use HTTPS everywhere, including development, with HSTS in deployed environments. On an on-premise LAN this means a self-signed certificate installed by the setup wizard — see Section 7.6, because secure cookies depend on it.
- Configure token expiry, signing-key storage/rotation, and refresh-token behavior deliberately if refresh tokens are implemented. Do not put signing secrets in source control.
- **Multi-factor authentication is required for Super Admin and School Admin accounts.** Identity supports this directly, and the system holds data about minors.

### 5.2 Membership model

**Decision: authorization is driven by explicit `SchoolMembership` records in the `master` schema. ASP.NET Core Identity's role tables are not used as the source of truth, and `UserManager.GetRolesAsync` must not be called for authorization decisions.**

The reason is structural. Identity's `AspNetUserRoles` table is a pair of user and role with no tenant dimension. It cannot express "Teacher at School A, Accountant at School B", which Section 5.5 requires. Attempting to encode the school into the role name produces an unmanageable role list and no real scoping.

The model is:

- `SchoolMembership` — the user, the school, the role, the membership status, and the branch scope.
- Branch scope is either **all branches of that school**, or **a specific set of branches** held in a child table.
- A user may hold more than one membership, in different schools, with different roles and different branch scope.

Membership is read at login and at every school or branch switch. It is the only input to authorization that matters.

### 5.3 Role concepts

| Role | Scope | Intended responsibilities |
|---|---|---|
| Super Admin | Platform | Manage schools, subscriptions, and platform configuration. **Does not exist in `SingleTenant` mode** — on-premise there is no platform to administer, and the School Admin is the highest role |
| School Admin | All branches of one school | Manage school settings, users, branches, and shared academic configuration |
| Branch Admin | One or more branches | Manage classes, sections, staff assignments, and day-to-day operations for assigned branches |
| Teacher | Assigned branches | Manage assigned classes, attendance, and authorized academic records |
| Accountant | School-wide or assigned branches | Manage fee structures, invoices, payments, and balances |
| Student | Own records | View their own attendance, fees, and results |
| Parent | Linked children | View information for explicitly linked children, across branches if siblings attend different branches |

These are role concepts, not a complete permission specification. The permission matrix must be defined per use case before the role names are relied on.

Two cases need particular attention during requirements:

- **A parent with children at different branches** must see both without being granted branch-wide access to either.
- **A teacher working across branches** must have an explicit multi-branch scope rather than being promoted to school-wide access as a shortcut.

### 5.4 Session context and switching

After authentication, the session carries the resolved context as claims:

- `school_id` — the validated active school
- `branch_scope` — all branches, or an explicit list
- `active_branch_id` — the branch currently being worked in, which must be within `branch_scope`

Rules:

- Switching school or branch **re-issues the authentication cookie** after re-validating membership. It is never done through a query parameter or a client-held value.
- If the active branch becomes suspended or falls outside the user's scope, the user is returned to branch selection rather than silently widened or narrowed.
- The context resolution order is fixed: authenticate, resolve school from the session claim, validate membership against master, resolve active branch, validate it against scope, and only then open the school `DbContext`.

### 5.5 Authorization principles

- Support multiple users per school and per branch.
- Model school membership and branch scope explicitly.
- A user may have different access in different school contexts, and different branch scope within one school.
- Apply role-based and policy-based authorization.
- Check record ownership/assignment where appropriate, such as a teacher's assigned classes and a parent's linked children.
- Enforce school and branch scope independently of role checks. Being an administrator of one school must not grant access to every school, and holding a role within a school must not imply access to every branch of it.

**Super Admin access to student data.** Platform administrators can reach every tenant by definition. The default is that they **cannot read student personal or academic records**. Access for support purposes is a deliberate, time-limited, logged action that notifies the school. Routine platform administration — provisioning, subscriptions, diagnostics — must not require it.

### 5.6 Revocation and session invalidation

Claims held in a cookie or token do not change when the underlying membership does. A teacher who is dismissed, or a branch that is suspended, must not retain access until the cookie expires.

- Enable Identity's security stamp validation with a short interval so account-level changes invalidate sessions promptly.
- Carry a membership version in the session and re-check it against master, so that revoking a membership or changing branch scope takes effect without waiting for expiry.
- When a subscription lapses, the school moves to a defined read-only or suspended state rather than failing unpredictably mid-request.

## 6. Web Application and Future WPF Client

### Initial stage

Build the web application using ASP.NET Core MVC and Razor Views. Keep controllers focused on HTTP and presentation concerns; place use cases and business rules in the application/domain layers.

Initial runtime flow:

```text
Web Browser
    |
    | HTTPS
    v
ASP.NET Core MVC
    |
    v
Authenticate -> Resolve School -> Validate Membership -> Resolve Branch
    |
    v
Application / Domain Logic
    |
    v
Infrastructure (EF Core)
    |
    v
Master DB  +  School DB (branch-scoped)
```

### Future desktop stage

When the WPF client is introduced, add an ASP.NET Core Web API that exposes the required use cases to authenticated API clients.

Conceptually:

```text
Web Browser ---> ASP.NET Core MVC -------+
                                         |
                                         v
                                  Shared Application
                                  and Domain Logic
                                         |
                                         v
WPF Desktop ---> ASP.NET Core Web API --> Infrastructure
                                         |
                                         v
                                      PostgreSQL
```

The MVC website can continue rendering Razor Views. The WPF application will use the Web API and will not access the database directly. MVC and API endpoints should reuse the same application/domain logic rather than duplicate business rules.

Adding the API later is a deliberate simplification for the first phase. Keep application logic independent of MVC so that adding the API does not require rewriting the core business rules. The school and branch context must be resolved identically on both hosts; it is an application-layer concern, not an MVC middleware concern that the API would have to reimplement.

## 7. Hosting and Deployment Models

The product must be sellable in three different customer situations. They are not three products: they are one application running in two deployment modes.

### 7.1 The three supported customer situations

> **Version 1 implements Model A only.** Models B and C are deferred (Sections 1.2 and 9.2). Model B is largely configuration once version 1 exists; Model C needs the multi-school work in phase D3 first.

| | Model A — single machine | Model B — school LAN | Model C — cloud multi-branch |
|---|---|---|---|
| Example customer | School A | School B | School C |
| Description | Everything installed on one computer at the school | Several offices in one building sharing one database over the internal network | A school with several branches; head office sees every branch |
| Where the database lives | On the school's computer | On a **dedicated shared server** on the school network, kept powered on | In the cloud |
| Where the application lives | On the same computer | On the **same shared server**, alongside the database | In the cloud |
| How users reach it | Browser on that computer | Browser on each office PC, over the internal network | Browser over HTTPS |
| Deployment mode | `SingleTenant` | `SingleTenant` | `MultiTenant` |
| Schemas present | `master` + one school schema | `master` + one school schema | `master` + one schema per school |
| Branches | One (`Main`) | One (`Main`) | Several, with head-office scope |
| Who operates it | The school | The school | We do |

**Models A and B are the same deployment.** The only difference is that in Model B the other offices reach the application across the internal network instead of sitting at the machine itself. There is no separate code path, no separate installer, and no separate configuration mode between them.

**Model C is the multi-tenant SaaS deployment** already described in Sections 4 and 5. The branch model in Section 4.1 is what serves it.

### 7.2 One codebase, one schema; mode is configuration

**Decision: a single codebase and a single database schema serve all three models. Deployment mode is a configuration setting, never a fork in the business logic.**

This has one concrete consequence that must not be optimised away:

**Both schemas are always created, including on a single-school on-premise install.** A small school's local PostgreSQL database will hold a `master` schema and one school schema, with the per-school role and grants applied exactly as they would be in the cloud. Two schemas in one database costs nothing operationally, and in exchange every code path — tenant resolution, role switching, `search_path` handling, membership, branch scope, query filters, audit — behaves identically in every deployment. Merging them into a single schema for on-premise would create a second table layout, a second set of queries, and a second place for an isolation bug to hide. It would also mean the isolation mechanism we depend on in the cloud is never exercised by our on-premise customers, who are the first ones to run it.

There must be no `if (onPremise)` condition inside domain or application logic. Deployment mode affects only the following, all of which live at the edges of the system:

| Behaviour | `SingleTenant` | `MultiTenant` |
|---|---|---|
| School-selection screen | Skipped; there is exactly one school | Shown when the user has more than one membership |
| School registration | Once, by the setup wizard at install time | Self-service or platform-administered |
| Super Admin portal | Disabled; there is no platform to administer | Enabled |
| Tenant provisioning | The installer creates the database and both schemas | Provider API creates the database; the app creates each school schema |
| Entitlement checks | Signed offline licence file | Subscription record in the `master` schema |
| Backups | Scheduled locally by the installer | Operated by us |
| Updates | Installer package run at the school | Deployed by us |

### 7.3 On-premise topology (Models A and B)

**Model A — one computer.** The application and PostgreSQL are installed on the single computer the school uses. Nothing is exposed to a network.

**Model B — a dedicated shared server on the school network.** This is the preferred on-premise shape wherever the school can provide a machine for it.

```text
  [Admin office PC]                [Shared server — always on]
       Browser  ──────────────────────────┐
                                          │  HTTPS 443
  [Principal office PC]                   v
       Browser  ──────────>   ASP.NET Core SMS  (Windows Service)
                              https://sms.school.local
  [Other office PC]                       │
       Browser  ──────────────────────────┤  localhost only
                                          v
                              PostgreSQL (local instance)
                              ├── schema: master
                              └── schema: school_<name>
```

Rules for this topology:

- **The database lives on the dedicated shared server**, which stays powered on. It is not an office workstation, so no one switches it off at the end of the day and no one installs unrelated software on it.
- **The application is installed on that same server**, next to the database. Office PCs do not get their own copy of the application.
- **Office PCs use a browser.** The installer places a desktop shortcut on each office PC pointing at the server address, so staff experience it as an installed program.
- **The application runs as a Windows Service** so it starts on boot without anyone logging in.
- **PostgreSQL is bound to the server itself and is not reachable from the network.** Only the application talks to it, over the local loopback. See below for why this matters.
- **The server needs a stable address** — a fixed IP or a DNS name on the school network — because every office shortcut and the TLS certificate depend on it.
- **The server is where backups, logs, the certificate, and the licence file live**, which means there is exactly one machine to look after, and exactly one machine to protect with a UPS.

**Keeping the application on the server removes PostgreSQL from the network entirely.** This is the strongest argument for this shape, and it is a security result, not a convenience one:

| | Application on the server | Application on each office PC |
|---|---|---|
| Ports open on the LAN | 443 to the application only | 5432 to PostgreSQL, from every office PC |
| What a compromised office PC can reach | The application, as whatever user is logged in | The database engine directly, with whatever the connection string grants |
| Database credentials | One copy, on the server | Distributed to every office PC |
| Authorization enforced by | Application code, on every request | Nothing — a direct SQL connection bypasses every rule in Sections 4.5 and 4.6 |

Installing the application on each office PC would require opening PostgreSQL to the LAN and distributing database credentials to every workstation. At that point a stolen or compromised office PC holds direct database access, and the school and branch isolation rules — which are enforced in application code — are simply not in the path. The rule in Section 3.1 that clients never connect directly to PostgreSQL applies on a school LAN exactly as it does on the internet.

**The other costs of a second application instance**, beyond the security point above: the two installs can be upgraded separately and drift out of sync with the database schema, which is the failure Section 4.8 exists to prevent; background work such as report rollups and scheduled tasks would run twice, duplicating or conflicting writes; and configuration, certificates, and logs must be maintained in several places in a building with no IT staff. There is no availability gain either, since every copy depends on the same server for the database.

*If a customer requires a second application instance regardless, it is permitted only with both installs version-locked to the same release, exactly one instance configured as the background-job runner, and PostgreSQL still reachable only by the application hosts — never by a workstation.*

### 7.4 Cloud topology (Model C)

```text
  [Head office branch]  Browser ──┐
  [Branch 2]            Browser ──┼──> HTTPS ──> ASP.NET Core SMS (cloud)
  [Branch 3]            Browser ──┘                      │
                                                         v
                                           PostgreSQL (cloud) — database: sms
                                           ├── schema: master
                                           └── schema: school_<one per school>
```

- **The application is hosted in the cloud alongside the database.** Branches use a browser.
- **A branch must never run a local copy of the application against the cloud database.** That would require exposing PostgreSQL to the public internet, which is an unacceptable exposure, and would put internet latency on every individual query.
- **Head office is a branch whose users hold all-branches scope** (Section 5.2). Consolidated views across branches go through the audited widened-scope service defined in Section 4.6, rule 4. Staff at a single branch hold scope limited to that branch.

### 7.5 Cloud provider decision — still open

For Model C we chose **Option D: do not select Azure, AWS, or a VPS yet**. The design should avoid unnecessary provider-specific dependencies until we compare cost, operational needs, backups, monitoring, data residency, scaling, and maintenance requirements.

Possible future deployment targets include:

- Azure App Service with Azure Database for PostgreSQL
- AWS hosting with RDS or Aurora for PostgreSQL
- A VPS running the application and PostgreSQL directly

These are options to evaluate, not decisions already made. Provider-specific deployment details and costs must be verified when the hosting choice is made.

**PostgreSQL strengthens the provider-independence position.** It is available as a first-class managed service on every major cloud and runs identically on a plain VPS, so the same database engine serves every candidate target including on-premise. The VPS option in particular becomes genuinely viable, because there is no per-core database licence to buy.

**This no longer blocks the roadmap.** Database provisioning is the one genuinely provider-specific part of the system, and the first build target is on-premise, where provisioning means *the installer creates one local database with a `master` schema and one school schema*. The provider decision is needed only before Model C ships. Provisioning sits behind an `ITenantProvisioner` interface, with a local-PostgreSQL implementation first and a cloud implementation added later.

*Implementation note: provisioning a school is now `CREATE SCHEMA`, `CREATE ROLE`, and the grants in Section 4.5 — all of which run inside a single transaction, so a failed provision rolls back cleanly rather than leaving a half-created tenant. This is a real improvement over `CREATE DATABASE`, which cannot run in a transaction. The provisioner still needs its own privileged connection, separate from the roles used to serve requests.*

### 7.6 Obligations that on-premise deployment creates

Selling an on-premise product is a different business from running a SaaS. These are commitments, not optional extras:

- **Updates.** We cannot deploy for the customer. This requires an installer with an upgrade path, and it guarantees that some schools will run old versions. The tenant schema-version check in Section 4.8 becomes the safety mechanism that refuses to run new code against an un-migrated database.
- **Backups.** The school is responsible and will generally not do it. The installer must configure an automated local backup with a retention policy, and a restore must be testable from inside the application. When a disk fails we will be blamed regardless of whose responsibility it formally was.
- **Support without access.** We cannot read their logs. A diagnostics export — logs, application version, schema version, and configuration with secrets redacted — must exist from the first release.
- **HTTPS on a local network.** Section 5.1 requires HTTPS and secure cookies depend on it, but the server has no public domain name. The setup wizard installs a self-signed certificate and registers it as trusted on each office PC. Falling back to plain HTTP is acceptable only on an isolated LAN, and must be a deliberate, recorded configuration choice rather than a silent default.
- **Database licensing and size — no longer an obligation.** PostgreSQL is free for commercial use with no edition tiers and no database size limit. There is no licence to buy for the school, no size ceiling to monitor, and no upgrade conversation when a school grows. This was a genuine constraint under the previous SQL Server decision and it is simply gone. Routine capacity monitoring is still sensible; a hard limit to engineer around is not.
- **PostgreSQL must be installed and kept patched on the school's server.** The installer bundles or installs it, configures it to listen on the loopback interface only, runs it as a Windows service, and sets the superuser and application role passwords. Security patching becomes part of the update package we ship, because the school will not do it.
- **Licensing.** A subscription cannot be enforced on hardware the customer owns. Entitlement is a signed licence file carrying the school name, installation key, limits, and expiry, validated offline. The subscription is **yearly**, and on expiry the system degrades to read-only rather than locking the school out — the full lifecycle, the clock-tampering defence, and the renewal flow are specified in `Master_Schema_Design.md` §3.8.

### 7.7 Upgrade path from on-premise to cloud

Because the schema is identical in both modes, a school that starts on Model A or B and later moves to Model C is a **database restore plus a registry entry**, not a migration project:

1. Export the school on site with `pg_dump --schema=school_<name>`.
2. Restore that schema into the cloud database, and create its role and grants.
3. Register the school in the cloud `master` schema and record its schema version.
4. Move or re-create the identity and membership records in the cloud `master` schema.
5. Re-point users to the cloud address and retire the local install.

Step 4 is the part that needs real design, because identity lives in `master` and the on-premise `master` is a separate installation from the cloud one — user ids from two installs can collide. Specify it before the first on-premise customer goes live, while it is still cheap.

## 8. Cross-Cutting Concerns Decided Up Front

These are cheap to establish before feature work and expensive to retrofit afterwards. They are decided now so that every module inherits them.

**Audit trail.** The system records grades, attendance, and money. Every change to a tracked entity records who changed it, when, in which school and branch, and what changed. Implemented once as an EF Core `SaveChanges` interceptor so no module has to remember. Platform-administrator actions are audited in the `master` schema separately.

**Optimistic concurrency.** All editable entities use PostgreSQL's `xmin` system column as the concurrency token, configured once with Npgsql's `UseXminAsConcurrencyToken`. No explicit version column is needed. Two teachers editing one grade sheet, or a payment posted twice, must produce a clear conflict rather than a silent overwrite.

**Dates and times.** Stored as `timestamptz` in UTC. Npgsql requires `DateTime` values mapped to `timestamptz` to be UTC-kind and throws otherwise, so the database layer enforces this decision rather than relying on discipline — a useful property, not an inconvenience to work around. Displayed in the time zone of the branch, which is a required field on every branch. Attendance, timetables, and examination schedules are date-critical, and this is not changeable later without a data migration.

**Data Protection keys.** ASP.NET Core stores data-protection keys on the local filesystem by default. On more than one instance, or across a container redeploy, every authentication cookie breaks. Keys must be persisted to shared, protected storage. This is a prerequisite of the cookie-authentication decision, not an optimisation.

**Background work.** Report generation, scheduled rollups, notification sending, and retried provisioning have no HTTP request and therefore no ambient context. School and branch must be **explicit parameters** passed into application services, never read from an ambient request-scoped value. This is the most likely source of a cross-tenant defect.

**Connection management.** The application connects through a single low-privilege base role, then issues `SET ROLE` and `SET search_path` per connection, so **one Npgsql pool serves every tenant** rather than one pool per school. This is a genuine advantage of schema-per-school over database-per-school. The cost is that correctness now depends on session state being reset when a connection returns to the pool — see Section 4.5, which makes that a tested requirement rather than an assumption. `AddDbContextPool` remains usable only if the tenant context is applied at connection open and never baked into the cached model. If PgBouncer is introduced later, transaction-pooling mode changes session semantics and must be re-validated as a security change.

**File storage.** Student photographs, uploaded documents, and generated reports need a storage decision, and the path or container layout is a tenant-isolation surface in its own right. Files must be addressed through a server-side lookup that applies the same school and branch checks as database access, never by a predictable path handed to the browser.

**Public identifiers.** Externally visible record identifiers should be opaque rather than sequential integers, so that enumeration is not a viable probe.

**Soft delete and retention.** Student and staff records cannot simply be deleted. Define soft deletion and the retention period alongside the regulatory requirements in Section 12.

**Test infrastructure.** The EF Core in-memory provider cannot validate the relational behaviour, query filters, and constraints that isolation depends on. Integration tests run against a real PostgreSQL, via Testcontainers or a local Docker instance. Without this, the isolation tests required in Section 10 will quietly never be written.

**Local development data.** Development and test environments mirror production: one school, one branch, one role. Seeding is part of solution setup, not an afterthought. Version 1 runs only in `SingleTenant` mode, so there is no second mode to regression-test until phase D3.

**Deployment mode is read once, at the edges.** The mode setting is consumed by startup configuration, the setup wizard, the entitlement check, and a small number of UI decisions. It must never be read inside a domain or application service. A quick way to audit this: searching the domain and application projects for the mode setting should return nothing.

## 9. Development Roadmap

### 9.1 Version 1 — Model A only

**Scope: one school, one branch, one computer.** The structural decisions in Section 1.3 are built; multi-school, multi-branch, LAN, and cloud behaviour is not. Build and test in this order.

1. **Requirements and solution design**
   - Confirm the initial school workflows and user journeys for a single school.
   - Confirm the branch-scoped versus school-wide table list in Section 4.4, including the two items marked for confirmation. **This still matters in version 1**, because it decides which tables carry `BranchId` — and that is the expensive retrofit named in Section 1.3.
   - Finalize project boundaries and dependency direction.
   - Define what belongs in the `master` schema versus the school schema.
   - Review and sign off `Master_Schema_Design.md`, including its open items and the multi-school gap recorded at the end.
   - Agree which feature modules are in the first release; steps 6 to 11 are not all required.
   - Decide the open items that affect authentication and setup: whether Windows/domain login is supported, and the licence file format (Section 12).

2. **Solution setup**
   - Create the .NET 10 solution and projects.
   - Verify the Npgsql EF Core provider version aligns with .NET 10 and EF Core 10, and add `EFCore.NamingConventions` for snake_case.
   - Add only necessary NuGet packages.
   - Configure project references, settings, development environment, and dependency injection.
   - Establish the audit interceptor, the `xmin` concurrency convention, snake_case naming, opaque identifiers, and the UTC/`timestamptz` convention now, before any entity is written.

3. **Database foundation**
   - Configure PostgreSQL and EF Core with **two schemas: `master` and `school_main`**. The schema name is read from configuration, not hardcoded, so dynamic resolution can replace it later without touching queries.
   - Create the `master` schema model and migrations, including the school record, the branch registry, and the schema version.
   - Create the school schema model and migrations.
   - Create the school's database role following the eventual per-school pattern: **`role_school_main`, granted `USAGE` on `school_main` only**, plus the read access to `master` that Section 4.2 requires. The application connects as this role. Revoke default privileges from `PUBLIC` on both schemas.
   - *This is the same number of database objects as a single generic application role, but it establishes and exercises the grant model from day one. Phase D3 then only adds `SET ROLE` switching, rather than inventing the permission model under deadline pressure. Near-zero cost now, meaningful risk removed later.*
   - `SET ROLE` switching and dynamic schema resolution remain **deferred** — see Section 9.2.
   - Implement the schema version check: refuse to run against a schema the code does not match. This is needed in version 1 regardless, because on-premise updates depend on it.

4. **Identity, membership, and context resolution**
   - Integrate ASP.NET Core Identity for credentials.
   - Implement the `SchoolMembership` model **with branch scope**, even though there is one school and one branch. Section 1.3 explains why this is not optional.
   - Configure MVC cookie authentication, data-protection key storage, and security stamp validation.
   - Implement school and branch context resolution. In version 1 it resolves to the single school and `Main` branch, but **it is resolved from context, never hardcoded**.
   - Implement the global `BranchId` query filter and the rule that `BranchId` is set server-side on write.
   - Add tests proving the filter and the server-set write path work, so the branch dimension is correct before data exists under it.
   - *Selection and switching UI, the widened-scope reporting service, and cross-school tests are deferred — see Section 9.2.*

5. **Setup and installation (Model A)**
   - Build the installer: install PostgreSQL, create the database, create the `master` and `school_main` schemas, grant the application role, run migrations.
   - Setup wizard: register the one school, create its `Main` branch, create the first School Admin account.
   - Install and run the application as a Windows Service so it starts on boot.
   - Bind PostgreSQL to localhost only.
   - Configure automated local backup with retention, and expose a restore test inside the application.
   - Implement the yearly licence: signature check on every startup, the warning/grace/read-only states, clock-tamper detection, and offline renewal by importing a signed file (`Master_Schema_Design.md` §3.8).
   - **Build the internal licence generator** — a small console tool that signs a licence from the school name, installation key, dates, and limits. Not shipped with the product. Protect the private signing key.
   - Implement the diagnostics export: logs, application version, schema version, redacted configuration.
   - Build the upgrade path, including the refusal to run against an un-migrated schema (Section 4.8).
   - *This step is what makes version 1 sellable. It is not deferrable to production readiness.*

### Feature modules — order and dependencies

**The order is largely forced by dependencies, not preference.** Nothing can be enrolled before classes exist; nothing can be charged or examined before students exist. Build one module at a time, to working and tested, before starting the next. Do not design all of them up front — each one teaches you something that changes the next.

| # | Module | Needs | Decide before starting |
|---|---|---|---|
| 6 | Academic setup | Foundation only | ✅ Nothing — decided: academic year is school-wide (§4.4) |
| 7 | Teachers and staff | 6 | Staff types; what happens when staff leave; the user-account link (§4.7) |
| 8 | Students and parents | 6 | Student record structure (§4.4); admission number format and rules |
| 9 | Attendance | 6, 7, 8 | **Daily or period-wise?** This shapes the whole table design |
| 10 | Fees and payments | 8 | Currency and decimal precision; invoice cycle; late-fee and discount rules; is a payment gateway in scope? |
| 11 | Examinations and results | 6, 8 | Grading scheme; report-card layout |
| 12 | Reports and dashboards | All of the above | Which reports actually matter to the first customer |

6. **Academic setup** — *build this first*
   - Academic year, class/grade, section, subject, and the class-subject mapping.
   - The branch dimension applied (`BranchId` on classes and sections).
   - *Why first: every other module references these. It is also small, and it extends directly from the `class` table used in the walking skeleton, so there is no wasted work.*

7. **Teacher and staff management**
   - Staff records, employee number, designation, branch assignment.
   - The link between a login account in `master` and a staff record in the school schema.
   - Class-teacher and subject-teacher assignment.
   - *Why second: it is the smallest person module, so the team learns the patterns quickly — and it is the first thing to exercise the cross-schema account-to-person link in §4.7, which is the one architectural piece the foundation does not prove. Retire that risk early.*

8. **Student and parent management**
   - Student records, admission number, admission date, status.
   - Enrolment: student + academic year + class + section + branch.
   - Parent and guardian records, and linking to students.
   - Branch transfer as an explicit, audited operation (deferred in practice until multi-branch, but model it now).
   - *This is the largest and most central module. Order with step 7 is interchangeable if the first customer demo needs students sooner — no dependency prevents it.*

9. **Attendance**
   - Daily attendance per student, per class and section, marked by a teacher.
   - States: present, absent, late, leave, half-day.
   - Monthly and term summaries.
   - *Why before fees: it is simpler, involves no money, and has fewer open decisions. It is a good module to settle the team's patterns on before taking on billing.*

10. **Fees and payments**
   - Fee heads and fee structures per class.
   - Invoice generation, payments including partial payments, receipts.
   - Discounts, scholarships, late fees, outstanding balances.
   - *The highest-risk module: it handles money, has the most edge cases, and carries the most unresolved decisions. Do not start it until those are answered.*

11. **Examinations and results**
   - Exam definition and schedule, marks entry, grading scheme, report cards.

12. **Reports and dashboards**
   - Single-branch reporting. Consolidated cross-branch reporting arrives with multi-branch (phase D2).

13. **Production readiness**
   - Update and release process with migrations gated as a deliberate step.
   - HTTPS, secret management, structured logging, monitoring, backups, restore tests, and security testing.

Implement and test each foundation before proceeding. Do not build feature modules before step 4 is validated by tests.

### 9.2 Deferred phases — build on demand

Each of these is **additive** provided Section 1.3 was honoured. None requires revisiting version 1's data model or business logic. They are listed roughly in order of how likely they are to be asked for first.

**D1. LAN access (Model B)** — *trigger: a school with more than one office*
- Bind the application beyond localhost; keep PostgreSQL on localhost.
- Install a certificate and trust it on each office PC; add desktop shortcuts.
- Document the always-on machine requirement.
- Mostly configuration and installer work. No application code changes.

**D2. Multiple branches** — *trigger: a school opens a second campus*
- Allow more than one `Branch` row and build branch management.
- Add branch-selection and switching UI, re-issuing the cookie on switch (Section 5.4).
- Build the audited widened-scope service for head-office consolidated reporting (Section 4.6, rule 4).
- Implement branch transfer as an explicit audited operation (Section 4.6, rule 7).
- Add cross-branch isolation tests.
- *The data model already carries `BranchId` and the query filter already runs, so this is new screens and services, not a migration.*

**D3. Multiple schools** — *trigger: a second school on one installation, or the first hosted customer*
- Implement `ITenantProvisioner`: `CREATE SCHEMA`, `CREATE ROLE`, and grants in one transaction.
- Replace the configured schema name with dynamic resolution from the validated tenant context.
- Add `SET ROLE` and `SET search_path` per connection, and the session-reset guarantees in Section 4.5.
- Add the school-selection screen and the Super Admin portal; switch deployment mode to `MultiTenant`.
- Extend the migration runner to iterate all tenant schemas, resumably.
- Add cross-school isolation tests, including the test that the *database* rejects a mis-set context.

**D4. Cloud hosting (Model C)** — *trigger: a customer who will not host it themselves*
- Choose the hosting provider against the criteria in Section 7.5.
- Add the cloud provisioning implementation.
- Implement the on-premise to cloud migration path, including the identity and membership move in Section 7.7.
- Subscriptions and billing replace the offline licence file.

**D5. Web API and WPF client** — *trigger: a concrete desktop-client requirement*
- Add the API host with JWT Bearer authentication, reusing the same application layer and context resolution.

**Rule for all deferred phases:** when one is activated, the corresponding section of this document already specifies it. Do not redesign it under time pressure — read Sections 4.5, 4.6, and 5.4 first, because the isolation rules there are the reason the retrofit is safe.

## 10. Engineering Practices

- Use EF Core as the default data-access approach.
- Use raw SQL only when there is a clear, measured reason — and note that raw SQL bypasses global query filters, so any raw query against a branch-scoped table must apply the branch predicate itself and be reviewed for it.
- Avoid overengineering and unnecessary generic repositories.
- Keep controllers thin and business rules outside the UI layer.
- Never model-bind `SchoolId` or `BranchId` from a request.
- Use dependency injection and configuration rather than hardcoded settings.
- Use Serilog for structured application logging. Include school and branch identifiers in the log context for traceability. Never log passwords, tokens, or sensitive student data.
- Use xUnit for unit and integration tests, with integration tests against a real PostgreSQL.
- Include tests for authentication, authorization, school selection, branch scope, and cross-school and cross-branch data isolation.
- Plan migrations and backup/restore procedures for every school schema, and test restores — including the Section 4.9 caveat that per-tenant point-in-time recovery is not available.
- Keep core business logic independent of the hosting provider.
- Set up continuous integration early: build, test, and migration validation on every change.

## 11. Questions Answered and Decisions Made

### Q: Should we use ASP.NET Core MVC/Razor or a separate frontend with Web API?

**Decision:** Use ASP.NET Core MVC with Razor Views for the initial web application. The earlier separate-frontend approach (such as React) was considered, but the final choice is MVC/Razor. Add a Web API when needed for the WPF client, keeping business logic reusable.

### Q: Which hosting provider should we use?

**Decision:** Option D — remain provider-independent for now. Azure, AWS, and VPS hosting can be compared later. Because the first build target is on-premise, this decision no longer blocks onboarding (Section 7.5).

### Q: A school wants everything installed on one computer with a local database. Does the architecture support that?

**Decision:** Yes — Model A. The installer creates the database, the `master` schema and the school schema with its role and grants, on the school's local PostgreSQL instance, registers the one school and its `Main` branch, and runs the application on the same machine. The multi-tenant and branch code paths are unchanged; they simply operate with one school and one branch.

### Q: A school wants the admin office and the principal's office to share one database over the internal network. Do we install the application on both computers?

**Decision: No.** The school provides a **dedicated server that stays powered on**. Both the database and the application are installed on that server. The admin office, the principal's office, and any other office reach it through a browser over the school's internal network, via a desktop shortcut that makes it feel like an installed program.

The decisive reason is security, not convenience. With the application on the server, PostgreSQL is bound to that machine and nothing but the application can reach it; the office PCs only need the application's HTTPS port. Installing the application on each office PC instead would require **opening PostgreSQL to the LAN and distributing database credentials to every workstation** — at which point a compromised office PC has direct database access, and the school and branch isolation rules in Sections 4.5 and 4.6, which are enforced in application code, are no longer in the path at all.

The secondary costs are version drift between installs against one schema, background jobs running twice, and configuration and certificates to maintain in several places. There is no availability benefit either, since every copy depends on the same server for its database. Section 7.3 has the full comparison and the conditions under which a second instance would be permitted.

### Q: A school has several branches and head office must see all of them. How is that deployed?

**Decision:** Model C — the application and the database are both hosted in the cloud, and branches use a browser over HTTPS. Head office is a branch whose users hold all-branches scope; branch staff are scoped to their own branch. A branch must never run a local copy of the application against the cloud database, because that would mean exposing PostgreSQL to the public internet.

### Q: Are these three separate products?

**Decision: No.** One codebase, one database schema, two deployment modes. Mode is a configuration setting affecting only the edges of the system — school selection, registration, the Super Admin portal, provisioning, entitlement, backups, and updates. There is no `if (onPremise)` condition in domain or application logic, because that is where isolation bugs would hide. The payoff is that a school starting on-premise can move to the cloud as a database restore plus a registry entry (Section 7.7).

### Q: Does this follow client-server architecture?

**Answer:** Yes. The browser (and future WPF app) is a client, the ASP.NET Core application/API is the server, and PostgreSQL is accessed only by trusted server-side code. The solution also uses modular-monolith organization, Clean Architecture principles, and multi-tenancy; these concepts are complementary.

### Q: Which authentication should we use?

**Decision:** ASP.NET Core Identity for credentials and password security; secure cookies for the MVC website; JWT Bearer authentication for the future Web API and WPF client. Authorization is driven by `SchoolMembership`, not by Identity role tables.

### Q: Should multiple users and roles be supported from the start?

**Decision:** Yes. Role concepts include Super Admin, School Admin, Branch Admin, Teacher, Accountant, Student, and Parent, with explicit school membership, branch scope, and authorization rules.

### Q: Which database should we use?

**Decision:** PostgreSQL, replacing the earlier Microsoft SQL Server decision.

What this changes in the architecture is small, because the design never depended on SQL Server features. What it changes commercially is not small: **PostgreSQL removes the database licence from the on-premise conversation entirely** — no edition tiers, no per-database size ceiling, nothing for a small school to buy. The previous design carried an obligation to monitor a 10 GB Express limit and to manage the upgrade conversation when a school outgrew it. That obligation is gone.

It also improves provider independence: PostgreSQL is a first-class managed service on every major cloud and runs identically on a plain VPS, so the same engine serves on-premise and every candidate cloud target.

Three things genuinely change in the design, all recorded in place:

1. **Optimistic concurrency** uses the `xmin` system column rather than a `rowversion` column (Section 8).
2. **The master-to-tenant join is now a hard constraint, not a preference.** PostgreSQL cannot query across databases at all, so application-level joins are mandatory (Section 4.7).
3. **Naming is `snake_case` in the database**, PascalCase in C#, via `EFCore.NamingConventions` (Section 2).

One thing improves: `timestamptz` with Npgsql rejects non-UTC values outright, so the UTC decision in Section 8 is enforced by the database rather than by discipline.

### Q: Separate database per school, or a schema per school?

**Decision: a schema per school**, inside one shared PostgreSQL database, with a `master` schema for platform data. This supersedes the earlier database-per-school decision.

What it gains:

- **Cross-schema foreign keys are real.** The link between an Identity user in `master` and a teacher or student record in a school schema is now an enforced constraint rather than an unenforceable column, and screens needing both sides are a single SQL join instead of two queries merged in memory.
- **The branch-registry mirror is gone.** Branches live once, in `master`, and tenant tables reference them directly. The one-way synchronisation obligation the previous design carried is removed entirely.
- **Provisioning is atomic.** `CREATE SCHEMA`, `CREATE ROLE`, and the grants all run in one transaction, so a failed provision rolls back cleanly. `CREATE DATABASE` cannot run in a transaction.
- **One connection pool serves every tenant**, instead of one pool per school.

What it costs, and how that cost is contained:

- School isolation is no longer a connection-string boundary. **This is the serious one**, because leaking between schools means leaking between different paying customers.
- It is contained by giving each school its **own PostgreSQL role with `USAGE` on its own schema only** (Section 4.5). A missed `search_path` or a context bug then produces a permission error rather than another school's data. The database still enforces the boundary — through grants instead of through separate databases. Two layers, application and database, and both are required.
- Per-tenant point-in-time recovery is no longer possible, and the blast radius for corruption or a runaway query is now shared (Section 4.9).
- Table count in a single database becomes the metric that decides when this model must change (Section 4.9).

### Q: Why not keep branches in separate schemas too?

**Decision: No.** Branches remain a `BranchId` column inside the school's schema. Student transfers between branches stay a simple update, consolidated head-office reporting stays a single query, and the table count in the shared database does not multiply by the branch count. Section 4.1 has the full reasoning, and Section 4.6 carries the rules that keep branch isolation safe.

### Q: Code First or Database First?

**Decision: Code First**, using EF Core migrations.

The deciding factor is on-premise deployment. The installer must create the database and both schemas and bring them to the correct version on a school's server, offline, with no DBA present, and every update must migrate an existing school's schema in place. Code First makes that a callable, versioned, resumable operation. Database First would mean shipping ordered SQL scripts and writing our own runner, which is rebuilding migrations badly. The same argument applies to applying one table layout across many school schemas and tracking a version per tenant (Section 4.8).

It is also the only option consistent with Section 3.4. Database First scaffolds entities *from* the schema, which would make the domain model a generated artifact of the database and invert the dependency direction we committed to. And every convention in Section 8 — the audit interceptor, `xmin` concurrency, the global `BranchId` filter, snake_case naming — is configured in `OnModelCreating` and applied centrally; re-scaffolding would overwrite that work each time.

**Code First does not mean skipping schema design.** The master and school schemas are designed deliberately first, then expressed as entities and configuration. The term describes where the source of truth lives, not whether the schema is thought through.

Rules for applying it here:

- Map with `IEntityTypeConfiguration<T>` classes in `SMS.Infrastructure`, never with EF attributes on entities, so `SMS.Domain` carries no EF Core reference.
- Maintain two separate migration histories, one per `DbContext`, and always pass `--context` explicitly.
- **Never migrate on application startup.** Migrations run from the installer or a deployment step, as Section 4.8 requires. On-premise there is no easy rollback of a school's database.
- Generate idempotent scripts (`dotnet ef migrations script --idempotent`) for the installer, so a school on any older version can be brought forward in one run.
- Express anything EF cannot — partial indexes, check constraints, PostgreSQL extensions — with `migrationBuilder.Sql(...)` inside the migration. Control of the DDL is not given up by choosing Code First.

### Q: Which ORM should we use?

**Decision:** Entity Framework Core with the Npgsql provider, with raw SQL selectively where justified.

### Q: How do we keep data isolated by school?

**Decision:** Each school gets its own PostgreSQL schema, and its own database role that can reach only that schema. A `master` schema holds central tenant, branch, identity, and subscription records. The server validates the authenticated user's school membership, then sets the role and `search_path` for that school; clients cannot choose a schema, a role, or a connection string, and cannot bypass tenant authorization. Both layers are required — see Section 4.5.

### Q: A school has multiple branches. Does each branch get its own schema?

**Decision: No.** The school is the isolation boundary and gets the schema. Branches are scoped within it by a non-nullable `BranchId`. The reasoning — student transfers, consolidated reporting, operational cost, and the fact that branches are one customer — is in Section 4.1.

The trade-off is explicit: branch isolation is enforced only by application code, with no database grant behind it. The rules in Section 4.6 exist to close that gap, and they are mandatory.

### Q: Can a user work across several branches?

**Decision:** Yes. Branch scope is part of the membership record and is either all branches of the school or an explicit list. A user working across branches is given a multi-branch scope, never school-wide access as a shortcut.

## 12. Decisions Still Open

The architecture direction is agreed, but these details are **not yet finalized**:

**Requires input before design can continue**

- **Target market and country.** Not yet stated anywhere, and it determines data residency, retention periods, parental consent, and data-subject-request obligations for a system holding records about minors. This also constrains the hosting choice.
- **Target scale.** Expected number of schools, branches per school, and students per branch, for year one and year three. Under schema-per-school this drives the **total table count in one database**, which is the metric that decides when the model must change (Section 4.9). It matters more under this design than it did under database-per-school.
- **Which modules ship in the first release.** The build *order* is now fixed (Section 9.1, steps 6 to 12). What is still open is where the first release stops — a sellable v1 may be steps 6 to 9 only, with fees and exams following. Decide this with the first customer, not in advance.

**On-premise decisions needed before authentication and setup are built (roadmap step 1)**

- **Windows / domain login.** On-premise schools often expect to sign in with their existing Windows account rather than a separate password. Supporting it later means retrofitting a second authentication scheme, which is expensive. Decide now whether it is in scope.
- **LAN certificate strategy.** Self-signed certificate installed and trusted by the setup wizard, or plain HTTP on an isolated network as a recorded exception. This determines whether the `Secure` cookie flag can be enforced.
- ~~Licence file format and failure behaviour.~~ **Decided:** yearly subscription, warning then grace then read-only, offline renewal by importing a signed file. Specified in `Master_Schema_Design.md` §3.6 to §3.8.
- **Minimum supported Windows and PostgreSQL major versions** for a school server, and what the installer does when they are not met. PostgreSQL major-version upgrades require `pg_upgrade` or a dump/restore, so the supported range must be stated rather than left open.
- **Who installs it.** Whether we install on site, install remotely, or ship an installer the school runs themselves. This changes the setup wizard's assumptions about the operator's technical skill.

**Design decisions to resolve during the corresponding step**

- ~~Whether academic years and term calendars are school-wide or branch-scoped.~~ **Decided: school-wide** (Section 4.4).
- How identity and membership records move from an on-premise `master` schema into the cloud `master` schema during an upgrade, including user-id collisions across two installs (Section 7.7).
- Whether on-premise installs report anything back to us at all — version, licence status, error counts — and whether that is acceptable to the customer.
- ~~Whether a student record is school-wide with branch held on the enrolment.~~ **Decided: yes** (Section 4.4). The detailed shape is settled when student management is built.
- Detailed school schema table design. *(The `master` schema is drafted in `Master_Schema_Design.md` — review and confirm its Section 8 items.)*
- Exact role and permission matrix, including the parent-with-children-at-multiple-branches and teacher-across-branches cases.
- The precise break-glass mechanism for platform-administrator access to tenant data.
- JWT claims, token lifetime, signing-key rotation, and refresh-token design for the API, including how school and branch context is carried.
- Cloud database provisioning and migration automation, once the hosting provider is known.
- Hosting provider, deployment topology, and budget.
- Payment gateway integration — whether in scope, and which provider. The fees module implies it but it was not previously listed.
- Currency and decimal precision for all monetary values.
- File storage provider and layout.
- Email/SMS provider and other external services.
- Tenant offboarding: what happens when a school leaves or a subscription lapses — read-only period, data export, retention window, and eventual deletion.
- Frontend visual design and accessibility requirements.

Do not treat these open questions as already implemented decisions. Resolve them when the corresponding design work begins.

---

## Current Starting Point

**Start with the requirements and solution structure, then establish the `master` schema, identity and membership, and school and branch isolation before building student, attendance, fee, or examination features. Build for on-premise first, on the same codebase that will later serve the cloud.**

The central design principle is to keep the system secure and understandable: a modular monolith, clean separation of business logic, server-side database access, a grant-enforced schema boundary per school, a disciplined software boundary per branch, and a path to a future WPF client through a Web API.

The one rule that carries the most risk if forgotten: **neither the school boundary nor the branch boundary is free.** The school boundary holds because each school has its own PostgreSQL role that cannot reach another school's schema; the branch boundary holds only because of the query filters and server-set values we write. Remove either and the failure is silent.

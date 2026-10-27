# Master Schema Design

**Companion to:** `School_Management_System_Architecture_and_Decisions.md`
**Covers:** roadmap step 1 — "Define what belongs in the `master` schema" — and step 3's model.
**Companion:** `PROJECT_STATUS.md` tracks what is actually built against this design.
**Scope:** the `master` schema only. The school schema (`school_main`) is designed separately, alongside the first feature modules.
**Status:** draft for review. Verified against the single-school (Model A) scope — see Section 9. Items needing confirmation are in Section 8.

---

## 1. Conventions

These follow Section 2 and Section 8 of the architecture document and apply to every table here.

| Convention | Decision |
|---|---|
| Schema | `master` |
| Naming | `snake_case` everywhere; C# stays PascalCase via `EFCore.NamingConventions` |
| Primary keys | `uuid`, application-generated (v7-style, time-ordered, to avoid index fragmentation) |
| Timestamps | `timestamptz`, always UTC |
| Concurrency | PostgreSQL `xmin`; **no version column on any table** |
| Enumerations | `varchar` + `CHECK` constraint, mapped to C# enums by a value converter |
| Audit columns | `created_at`, `created_by`, `updated_at`, `updated_by` on every mutable table |
| Lifecycle | `status` columns, not soft-delete flags — soft delete belongs to person records in the school schema |

**Why `varchar` + `CHECK` rather than native PostgreSQL enums.** Native enums are tidy but awkward to evolve: values cannot be removed, and renaming requires a type rebuild. A check constraint is altered in one migration line and maps cleanly to a C# enum through a value converter. The cost is a few bytes per row, which is irrelevant at this table size.

**Why `app_user` and not `user`.** `user` is a reserved word in PostgreSQL — `SELECT * FROM user` does not do what you expect. Every Identity table is renamed with an `app_` prefix for the same reason and for consistency.

---

## 2. Tables at a glance

```
master
├── school                      the tenant record; one row in version 1
├── branch                      campuses; one row (Main) in version 1
├── app_user                    Identity: credentials and account state
├── app_user_claim              Identity
├── app_user_login              Identity (external providers; unused in v1)
├── app_user_token              Identity
├── school_membership           WHO has WHICH role at WHICH school
├── school_membership_branch    …and at which branches, when scope is 'specific'
├── installation                this install: key, version, clock watermark
├── license                     yearly subscription, with renewal history
├── audit_log                   platform-level audit trail
└── data_protection_key         ASP.NET Core key ring
```

**No Identity role tables.** `asp_net_roles` and `asp_net_user_roles` are deliberately absent. Derive the context from `IdentityUserContext<,>` rather than `IdentityDbContext<,>` so they are never created — this makes "authorization does not come from Identity roles" (architecture §5.2) a structural fact rather than a rule someone has to remember.

Relationships:

```
school ──< branch
  │          │
  └──< school_membership ──< school_membership_branch >── branch
             │
         app_user
```

---

## 3. DDL

Shown as the SQL the migrations should produce. Write it as EF entity configurations (architecture §11, Code First); this is the target, not a script to run by hand.

### 3.1 Schema and roles

```sql
CREATE SCHEMA IF NOT EXISTS master;
CREATE SCHEMA IF NOT EXISTS school_main;

-- Nothing is readable by default.
REVOKE ALL ON SCHEMA public FROM PUBLIC;

-- The application role, named to the eventual per-school pattern (architecture §9.1 step 3).
CREATE ROLE role_school_main LOGIN PASSWORD :'app_password';

GRANT USAGE ON SCHEMA master, school_main TO role_school_main;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA master, school_main TO role_school_main;

ALTER DEFAULT PRIVILEGES IN SCHEMA master, school_main
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO role_school_main;
```

The migration and provisioning role is separate and more privileged. It is never used to serve a request.

### 3.2 `school`

```sql
CREATE TABLE master.school (
    id                uuid         NOT NULL,
    slug              varchar(40)  NOT NULL,
    name              varchar(200) NOT NULL,
    schema_name       varchar(47)  NOT NULL,
    status            varchar(20)  NOT NULL,
    country_code      char(2)      NULL,
    schema_version    varchar(100) NULL,
    last_migrated_at  timestamptz  NULL,
    created_at        timestamptz  NOT NULL,
    created_by        uuid         NULL,
    updated_at        timestamptz  NULL,
    updated_by        uuid         NULL,

    CONSTRAINT pk_school               PRIMARY KEY (id),
    CONSTRAINT uq_school_slug          UNIQUE (slug),
    CONSTRAINT uq_school_schema_name   UNIQUE (schema_name),
    CONSTRAINT uq_school_id_slug       UNIQUE (id, slug),
    CONSTRAINT ck_school_slug_format   CHECK (slug ~ '^[a-z0-9_]{1,40}$'),
    CONSTRAINT ck_school_schema_format CHECK (schema_name ~ '^school_[a-z0-9_]{1,40}$'),
    CONSTRAINT ck_school_status        CHECK (status IN ('provisioning','active','suspended','closed'))
);
```

Notes:

- `slug` and `schema_name` carry the format checks required by architecture §4.1. The schema name is interpolated into `SET search_path` when multi-school is activated, so **the database refuses a malformed value** rather than relying on the application to validate it. `varchar(47)` is `school_` plus 40, inside PostgreSQL's 63-character identifier limit.
- `schema_version` and `last_migrated_at` implement the per-tenant version check in architecture §4.8. The application refuses to serve a school whose version does not match the running code.
- `country_code` is nullable and unused in version 1; it exists because the regulatory question in architecture §12 is still open and adding it later would mean touching a populated table.
- `status` is `provisioning` only during onboarding, which is a deferred feature — version 1 writes `active` directly at setup.

### 3.3 `branch`

```sql
CREATE TABLE master.branch (
    id            uuid         NOT NULL,
    school_id     uuid         NOT NULL,
    code          varchar(20)  NOT NULL,
    name          varchar(200) NOT NULL,
    status        varchar(20)  NOT NULL,
    is_default    boolean      NOT NULL DEFAULT false,
    time_zone     varchar(64)  NOT NULL,
    address_line  varchar(300) NULL,
    city          varchar(100) NULL,
    phone         varchar(40)  NULL,
    email         varchar(256) NULL,
    created_at    timestamptz  NOT NULL,
    created_by    uuid         NULL,
    updated_at    timestamptz  NULL,
    updated_by    uuid         NULL,

    CONSTRAINT pk_branch              PRIMARY KEY (id),
    CONSTRAINT fk_branch_school       FOREIGN KEY (school_id)
                                      REFERENCES master.school (id) ON DELETE RESTRICT,
    CONSTRAINT uq_branch_school_code  UNIQUE (school_id, code),
    CONSTRAINT uq_branch_id_school    UNIQUE (id, school_id),
    CONSTRAINT ck_branch_status       CHECK (status IN ('active','suspended','closed'))
);

-- Exactly one default branch per school.
CREATE UNIQUE INDEX uq_branch_one_default_per_school
    ON master.branch (school_id)
    WHERE is_default;
```

Notes:

- **`time_zone` is NOT NULL** and holds an IANA identifier (`Asia/Karachi`). Architecture §8 stores everything in UTC and displays in the branch's zone, so a branch without one cannot render a timetable correctly.
- The **partial unique index** guarantees one default branch per school. EF cannot express this; write it with `migrationBuilder.Sql(...)`.
- `uq_branch_id_school` looks redundant next to the primary key. It is not — it is the target of the composite foreign key in §3.5 that prevents cross-school branch assignment.
- `ON DELETE RESTRICT`: schools are closed by status, never deleted out from under their branches.

### 3.4 Identity tables

Created by ASP.NET Core Identity, renamed and extended. Only `app_user` is shown in full; the claim, login, and token tables keep Identity's standard shape with `app_` names and a `uuid` key.

```sql
CREATE TABLE master.app_user (
    id                      uuid         NOT NULL,
    user_name               varchar(256) NULL,
    normalized_user_name    varchar(256) NULL,
    email                   varchar(256) NULL,
    normalized_email        varchar(256) NULL,
    email_confirmed         boolean      NOT NULL DEFAULT false,
    password_hash           text         NULL,
    security_stamp          text         NULL,
    concurrency_stamp       text         NULL,
    phone_number            text         NULL,
    phone_number_confirmed  boolean      NOT NULL DEFAULT false,
    two_factor_enabled      boolean      NOT NULL DEFAULT false,
    lockout_end             timestamptz  NULL,
    lockout_enabled         boolean      NOT NULL DEFAULT true,
    access_failed_count     integer      NOT NULL DEFAULT 0,

    -- our additions
    display_name            varchar(200) NOT NULL,
    status                  varchar(20)  NOT NULL,
    is_platform_admin       boolean      NOT NULL DEFAULT false,
    created_at              timestamptz  NOT NULL,
    created_by              uuid         NULL,
    updated_at              timestamptz  NULL,
    updated_by              uuid         NULL,

    CONSTRAINT pk_app_user        PRIMARY KEY (id),
    CONSTRAINT ck_app_user_status CHECK (status IN ('active','suspended','disabled'))
);

CREATE UNIQUE INDEX uq_app_user_normalized_email     ON master.app_user (normalized_email);
CREATE UNIQUE INDEX uq_app_user_normalized_user_name ON master.app_user (normalized_user_name);
```

Notes:

- `is_platform_admin` is the Super Admin flag from architecture §5.3. It exists but is **unused in version 1**, where the Super Admin portal is disabled. Including the column now avoids altering a populated table later.
- `status` is separate from Identity's lockout fields: lockout is a temporary security response, status is an administrative decision.
- `two_factor_enabled` supports the MFA requirement for admin roles in architecture §5.1.
- Identity's `concurrency_stamp` stays because `UserManager` uses it; it does not replace `xmin` for our own entities.

### 3.5 `school_membership` and `school_membership_branch`

The authorization core. Architecture §5.2 explains why this exists instead of Identity roles.

```sql
CREATE TABLE master.school_membership (
    id                  uuid        NOT NULL,
    user_id             uuid        NOT NULL,
    school_id           uuid        NOT NULL,
    role                varchar(30) NOT NULL,
    branch_scope        varchar(20) NOT NULL,
    status              varchar(20) NOT NULL,
    membership_version  integer     NOT NULL DEFAULT 1,
    created_at          timestamptz NOT NULL,
    created_by          uuid        NULL,
    updated_at          timestamptz NULL,
    updated_by          uuid        NULL,

    CONSTRAINT pk_school_membership        PRIMARY KEY (id),
    CONSTRAINT fk_membership_user          FOREIGN KEY (user_id)
                                           REFERENCES master.app_user (id) ON DELETE CASCADE,
    CONSTRAINT fk_membership_school        FOREIGN KEY (school_id)
                                           REFERENCES master.school (id) ON DELETE RESTRICT,
    CONSTRAINT uq_membership_user_school_role UNIQUE (user_id, school_id, role),
    CONSTRAINT uq_membership_id_school     UNIQUE (id, school_id),
    CONSTRAINT ck_membership_role          CHECK (role IN
        ('school_admin','branch_admin','teacher','accountant','student','parent')),
    CONSTRAINT ck_membership_branch_scope  CHECK (branch_scope IN ('all_branches','specific')),
    CONSTRAINT ck_membership_status        CHECK (status IN ('active','suspended'))
);

CREATE TABLE master.school_membership_branch (
    membership_id  uuid NOT NULL,
    school_id      uuid NOT NULL,
    branch_id      uuid NOT NULL,

    CONSTRAINT pk_membership_branch     PRIMARY KEY (membership_id, branch_id),
    CONSTRAINT fk_mb_membership         FOREIGN KEY (membership_id, school_id)
                                        REFERENCES master.school_membership (id, school_id)
                                        ON DELETE CASCADE,
    CONSTRAINT fk_mb_branch             FOREIGN KEY (branch_id, school_id)
                                        REFERENCES master.branch (id, school_id)
                                        ON DELETE CASCADE
);
```

Notes — the composite foreign keys matter:

- A naive design would give `school_membership_branch` simple keys to `membership_id` and `branch_id`. **Nothing would then stop a membership at School A being scoped to a branch of School B.** Carrying `school_id` in the child row and pointing both foreign keys at the composite unique constraints makes that combination impossible to insert. The database enforces it; no application check is needed, and none can be forgotten.
- `membership_version` supports the revocation requirement in architecture §5.6. It is incremented whenever role, scope, or status changes; the session carries it as a claim and re-checks it, so revoking access does not wait for the cookie to expire.
- A user may hold more than one role at a school — teacher *and* accountant — hence the unique key includes `role`. Branch scope is per role, which is correct: someone may teach at one branch while handling accounts for all of them.
- Rows exist here **only when** `branch_scope = 'specific'`. `all_branches` means no rows, not one row per branch — so adding a branch automatically widens an all-branches membership, which is the intended behaviour.
- `ON DELETE CASCADE` from `app_user`: deleting a user removes their memberships. In practice users are disabled rather than deleted (§3.4's `status`).

### 3.6 `installation`

One row, written at install. It identifies this specific installation and detects clock tampering.

```sql
CREATE TABLE master.installation (
    id                uuid         NOT NULL,
    installation_key  uuid         NOT NULL,
    installed_at      timestamptz  NOT NULL,
    app_version       varchar(50)  NOT NULL,
    clock_high_water  timestamptz  NOT NULL,
    machine_name      varchar(200) NULL,

    CONSTRAINT pk_installation          PRIMARY KEY (id),
    CONSTRAINT uq_installation_key      UNIQUE (installation_key)
);

-- Exactly one installation row, ever.
CREATE UNIQUE INDEX uq_installation_single_row ON master.installation ((true));
```

Notes:

- `installation_key` is generated at setup and shown to the customer. The licence is issued **against this key**, so a licence file copied to another school's installation will not validate. Without it, one paid licence covers unlimited installs.
- `clock_high_water` is the latest time this installation has ever seen. It is updated forward on every startup and periodically while running. If the current system time is **earlier** than this value, the clock has been moved backwards and the licence is treated as invalid. This is the standard defence against the simplest way to cheat an offline expiry date: changing the computer's date.
- The `((true))` expression index is a PostgreSQL trick that permits exactly one row in the table. There can only ever be one installation record.

### 3.7 `license`

On-premise entitlement, per architecture §7.6. Replaced by subscription records when cloud hosting arrives.

```sql
CREATE TABLE master.license (
    id                 uuid         NOT NULL,
    school_id          uuid         NOT NULL,
    installation_key   uuid         NOT NULL,
    license_number     varchar(50)  NOT NULL,
    licensed_to        varchar(200) NOT NULL,
    signed_payload     text         NOT NULL,
    issued_at          timestamptz  NOT NULL,
    starts_at          timestamptz  NOT NULL,
    expires_at         timestamptz  NOT NULL,
    grace_until        timestamptz  NOT NULL,
    max_branches       integer      NULL,
    max_students       integer      NULL,
    status             varchar(20)  NOT NULL,
    last_validated_at  timestamptz  NULL,
    activated_at       timestamptz  NOT NULL,
    superseded_at      timestamptz  NULL,
    created_at         timestamptz  NOT NULL,
    created_by         uuid         NULL,
    updated_at         timestamptz  NULL,
    updated_by         uuid         NULL,

    CONSTRAINT pk_license            PRIMARY KEY (id),
    CONSTRAINT fk_license_school     FOREIGN KEY (school_id)
                                     REFERENCES master.school (id) ON DELETE CASCADE,
    CONSTRAINT uq_license_number     UNIQUE (license_number),
    CONSTRAINT ck_license_status     CHECK (status IN
        ('valid','expiring','grace','read_only','invalid','superseded')),
    CONSTRAINT ck_license_dates      CHECK (expires_at > starts_at AND grace_until >= expires_at)
);

-- Only one licence may be the active one at a time.
CREATE UNIQUE INDEX uq_license_one_active
    ON master.license (school_id)
    WHERE superseded_at IS NULL;
```

Notes:

- **Licences are kept, not overwritten.** Renewing inserts a new row and stamps `superseded_at` on the old one. You keep a renewal history, and a support call about "when did we renew?" is answerable. The partial unique index guarantees only one is active.
- `signed_payload` holds the signed licence file verbatim. **Every other column is a decoded cache.** Validation always verifies the signature on the payload and reads the dates and limits from inside it. A customer with database access can edit `expires_at` in the table; it changes nothing, because nothing trusts it.
- `installation_key` must match `master.installation.installation_key`, or the licence is rejected as belonging to a different install.
- `grace_until` is carried explicitly rather than computed, so the grace period can differ per customer without a code change.

### 3.8 Subscription lifecycle

**Model: a yearly subscription. When it ends, the system stops allowing changes until it is renewed.**

The system moves through these states. Each is recorded in `license.status` and re-evaluated at startup and once a day while running.

| State | When | What the school can do |
|---|---|---|
| `valid` | Normal | Everything |
| `expiring` | 30 days before `expires_at` | Everything, with a warning banner that grows more prominent |
| `grace` | After `expires_at`, until `grace_until` (14 days) | Everything, with a full-width warning on every page |
| `read_only` | After `grace_until` | **Log in, view, search, print, and export everything. Cannot create or change anything.** |
| `invalid` | Signature fails, wrong installation, or clock moved backwards | Read-only, with a message telling them to contact support |
| `superseded` | Replaced by a renewal | — historical row |

**Why `read_only` rather than a full lockout.** This is a deliberate recommendation, and it is worth understanding before changing it:

- A school that cannot log in at all cannot take attendance, print a report card, or look up a parent's phone number. If the licence lapses during exam week, that is a crisis for them and a support emergency for you.
- Read-only still creates real pressure to renew — they cannot record attendance, take a fee payment, or enter a result. The system is useless for daily work within a day or two.
- It never holds a school's data hostage. That matters practically, because records about children carry retention and access obligations, and commercially, because "they locked us out of our own student records" is a story that ends a reseller relationship.
- Recovery is instant: import the new licence file and everything resumes. No data was touched.

**Renewal, with no internet assumed.** The customer sends their `installation_key` and school name. You generate and sign a new licence file. They import it through the UI — upload a file or paste a key. The application verifies the signature, inserts the new row, supersedes the old one, and returns to `valid`. No internet connection is required at any point.

**Rules the implementation must follow:**

1. The signature is verified on **every** startup and every daily re-check, never only at import.
2. Only the **public** key ships with the application. The private signing key stays with us and never leaves our control — if it leaks, anyone can mint licences.
3. Expiry never deletes, hides, or modifies data.
4. Export and print stay available in every state, including `invalid`.
5. The state and its reason are always visible to a School Admin, so nobody is confused about why the system went read-only.
6. Every state change is written to `audit_log`.

**A licence generator is a required deliverable.** A small internal console tool that takes school name, installation key, start and end dates, and limits, and produces the signed file. It is not part of the shipped application. Nobody has scheduled it yet — add it to roadmap step 5.

**Honest limit of offline licensing.** A determined customer with full control of the machine can eventually defeat any offline scheme. The goal here is to make casual evasion — moving the clock, editing the database, copying the licence to a second school — fail reliably, while keeping honest customers unaffected. That is achievable, and it is enough. Do not spend effort beyond it.

### 3.9 `audit_log`

Platform-level audit (architecture §8). The school schema carries its own audit table for school data.

```sql
CREATE TABLE master.audit_log (
    id           bigint       GENERATED ALWAYS AS IDENTITY,
    occurred_at  timestamptz  NOT NULL,
    user_id      uuid         NULL,
    school_id    uuid         NULL,
    branch_id    uuid         NULL,
    entity_type  varchar(100) NOT NULL,
    entity_id    varchar(64)  NOT NULL,
    action       varchar(10)  NOT NULL,
    changes      jsonb        NULL,
    ip_address   inet         NULL,
    user_agent   varchar(400) NULL,

    CONSTRAINT pk_audit_log   PRIMARY KEY (id),
    CONSTRAINT ck_audit_action CHECK (action IN ('insert','update','delete'))
);

CREATE INDEX ix_audit_log_occurred_at ON master.audit_log (occurred_at DESC);
CREATE INDEX ix_audit_log_entity      ON master.audit_log (entity_type, entity_id);
CREATE INDEX ix_audit_log_user        ON master.audit_log (user_id) WHERE user_id IS NOT NULL;
```

Notes:

- **No foreign keys, deliberately.** An audit record must survive deletion of whatever it describes. `user_id` and `entity_id` are recorded values, not references.
- `bigint` identity rather than `uuid`: this table is append-heavy and never exposed externally, so a narrow sequential key keeps the indexes small. The opaque-identifier rule in architecture §8 applies to records a user can see in a URL.
- `changes` is `jsonb` — PostgreSQL indexes and queries it natively, so "what changed on this record" needs no extra tables.
- `inet` is a real PostgreSQL type and validates the address on write.

### 3.10 `data_protection_key`

```sql
CREATE TABLE master.data_protection_key (
    id             integer GENERATED ALWAYS AS IDENTITY,
    friendly_name  text    NULL,
    xml            text    NULL,

    CONSTRAINT pk_data_protection_key PRIMARY KEY (id)
);
```

Shape fixed by `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`. On a single machine the filesystem key ring would work, but persisting keys here from day one means authentication cookies survive a reinstall, and the multi-instance requirement in architecture §8 is already satisfied when it matters. Four columns, no downside.

---

## 4. EF entities

POCOs in `SMS.Domain`. No EF attributes, no EF package reference (architecture §11).

```csharp
public abstract class AuditableEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public enum SchoolStatus { Provisioning, Active, Suspended, Closed }
public enum BranchStatus { Active, Suspended, Closed }
public enum UserStatus { Active, Suspended, Disabled }
public enum MembershipStatus { Active, Suspended }
public enum BranchScope { AllBranches, Specific }
public enum SchoolRole { SchoolAdmin, BranchAdmin, Teacher, Accountant, Student, Parent }

public class School : AuditableEntity
{
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string SchemaName { get; set; } = null!;
    public SchoolStatus Status { get; set; }
    public string? CountryCode { get; set; }
    public string? SchemaVersion { get; set; }
    public DateTimeOffset? LastMigratedAt { get; set; }

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}

public class Branch : AuditableEntity
{
    public Guid SchoolId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public BranchStatus Status { get; set; }
    public bool IsDefault { get; set; }
    public string TimeZone { get; set; } = null!;
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public School School { get; set; } = null!;
}

public class AppUser : IdentityUser<Guid>          // SMS.Domain may reference Identity.Stores only
{
    public string DisplayName { get; set; } = null!;
    public UserStatus Status { get; set; }
    public bool IsPlatformAdmin { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public ICollection<SchoolMembership> Memberships { get; set; } = new List<SchoolMembership>();
}

public class SchoolMembership : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid SchoolId { get; set; }
    public SchoolRole Role { get; set; }
    public BranchScope BranchScope { get; set; }
    public MembershipStatus Status { get; set; }
    public int MembershipVersion { get; set; } = 1;

    public AppUser User { get; set; } = null!;
    public School School { get; set; } = null!;
    public ICollection<SchoolMembershipBranch> Branches { get; set; } = new List<SchoolMembershipBranch>();
}

public class SchoolMembershipBranch
{
    public Guid MembershipId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid BranchId { get; set; }

    public SchoolMembership Membership { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
}
```

`AppUser` is the one domain type that touches a framework base class. The alternative — a separate Identity user plus a mirrored domain user — adds a synchronisation problem to avoid a package reference. Referencing `Microsoft.Extensions.Identity.Stores` (not the EF Core package) from `SMS.Domain` is the lesser evil, and it keeps `SMS.Domain` free of any EF Core dependency.

---

## 5. Example configurations

Two of the nine, showing the pattern. Both live in `SMS.Infrastructure/Persistence/Configurations/Master/`.

```csharp
public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branch", "master");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TimeZone).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.UseXminAsConcurrencyToken();

        builder.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
        builder.HasAlternateKey(x => new { x.Id, x.SchoolId });   // target of the composite FK

        builder.HasOne(x => x.School)
               .WithMany(x => x.Branches)
               .HasForeignKey(x => x.SchoolId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SchoolMembershipBranchConfiguration : IEntityTypeConfiguration<SchoolMembershipBranch>
{
    public void Configure(EntityTypeBuilder<SchoolMembershipBranch> builder)
    {
        builder.ToTable("school_membership_branch", "master");
        builder.HasKey(x => new { x.MembershipId, x.BranchId });

        builder.HasOne(x => x.Membership)
               .WithMany(x => x.Branches)
               .HasForeignKey(x => new { x.MembershipId, x.SchoolId })
               .HasPrincipalKey(x => new { x.Id, x.SchoolId })
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Branch)
               .WithMany()
               .HasForeignKey(x => new { x.BranchId, x.SchoolId })
               .HasPrincipalKey(x => new { x.Id, x.SchoolId })
               .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### Indexing `branch_id` in version 1

School-schema tables carry `branch_id`, but with one branch it has **zero selectivity** — every row has the same value. A standalone index on `branch_id` would therefore never help a query while still costing on every insert, update, and delete.

The rule for version 1:

- **Do not create standalone `branch_id` indexes.**
- Where an index is needed for a real query, make `branch_id` the **leading column** of a composite index — `(branch_id, academic_year_id, …)`. It is inert today and becomes the selective prefix the moment a second branch exists.
- Revisit indexing as part of activating multi-branch (phase D2), when the data distribution is finally real.

This keeps version 1 honest about performance without discarding the structure that makes D2 additive.

Things EF cannot express, which go into the migration with `migrationBuilder.Sql(...)`:

- the partial unique index for one default branch per school
- the `CHECK` constraints on `slug`, `schema_name`, and every enum column
- the `inet` and `jsonb` column types where the default mapping does not pick them
- the role creation and grants

---

## 6. `MasterDbContext`

```csharp
public class MasterDbContext : IdentityUserContext<AppUser, Guid>
{
    public DbSet<School> Schools => Set<School>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<SchoolMembership> Memberships => Set<SchoolMembership>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("master");
        base.OnModelCreating(b);                       // Identity tables
        b.ApplyConfigurationsFromAssembly(typeof(MasterDbContext).Assembly);
    }
}
```

`IdentityUserContext`, not `IdentityDbContext` — see §2. Migrations for this context go to `Migrations/Master`, separate from the school context, and every `dotnet ef` command passes `--context MasterDbContext` explicitly.

### Both contexts must be able to share a connection and a transaction

There are two `DbContext` types over **one** database. By default each opens its own connection, which means each gets its own transaction — and some ordinary operations span both schemas:

- Creating a staff member who also gets a login: `master.app_user` + `master.school_membership` + `school_main.teacher`.
- The setup wizard, which writes `master` rows and may touch the school schema.
- Deactivating a user and their person record together.

Without a shared transaction, a failure halfway leaves a login with no staff record, or the reverse.

Register a single `NpgsqlConnection` per scope, hand it to both contexts, and share the transaction explicitly:

```csharp
await using var tx = await connection.BeginTransactionAsync(ct);
await masterContext.Database.UseTransactionAsync(tx, ct);
await schoolContext.Database.UseTransactionAsync(tx, ct);
// … work on both …
await tx.CommitAsync(ct);
```

Set this up in roadmap step 3, before the first cross-schema operation is written — retrofitting it means revisiting every service that already assumed independent saves.

*This is a direct benefit of schema-per-school: both schemas live in one database, so one ordinary transaction covers them. Under database-per-school this would have required a distributed transaction or a compensation mechanism.*

---

## 7. Seed data for version 1

The setup wizard writes, in one transaction:

1. `school` — one row, `status = 'active'`, slug and schema name from the wizard, `schema_version` set to the current migration id.
2. `branch` — one row, `code = 'MAIN'`, `is_default = true`, time zone chosen in the wizard.
3. `app_user` — the first administrator.
4. `school_membership` — that user, that school, `role = 'school_admin'`, `branch_scope = 'all_branches'`, `status = 'active'`.
5. `license` — the signed file supplied at install.

### Development and test seed — one school, **two** branches

**Decision: development and test environments seed one school with one branch**, matching production exactly. No extra school and no extra branch is created anywhere.

Consequence to carry forward: the global `BranchId` query filter runs in version 1 but, with a single branch, it always matches — so version 1 does not prove it works. **The filter must be tested with two branches as the first task of phase D2 (multi-branch), before any branch-management UI is built and before any customer has data in a second branch.** Record it there; do not let D2 start with feature work.

A second *school* is likewise not seeded. There is no school-switching mechanism in version 1, so a cross-school test would assert nothing. That seed arrives with phase D3, together with the code it tests.

---

## 8. Items to confirm before this is built

1. **Branch address fields.** Currently minimal. Confirm whether invoices or reports need a full structured address, since fees are a later module and widening this table then is cheap but reprinting stationery is not.
2. **Can one user belong to several schools?** The schema allows it. Version 1 never creates it. Confirm it is wanted before the multi-school phase, because it changes the login flow.
3. **Role list.** Six roles are encoded in a `CHECK` constraint. Adding one later is a one-line migration, but confirm none is missing now.
4. **`display_name` versus the school's own name field.** Architecture §4.7 says `master` owns the login identity and the school schema owns the name the school uses. Confirm `display_name` here is the account label only, and that staff and student records carry their own names.
5. **Licence limits.** `max_branches` and `max_students` are nullable. Confirm whether version 1 enforces them or only records them.

### A known gap, for the multi-school phase

In version 1 there is one school, so this is not yet a problem — but it must be solved before a second school shares this `master` schema:

**Every tenant role can read all of `master`.** That includes `app_user`, which holds every user's email, and `school_membership`, which maps every user to every school. Under multi-tenancy that is cross-tenant readable data, which contradicts the assumption in architecture §4.2 that `master` holds nothing school-sensitive.

Two workable answers, to choose between at that point:

- **Row-level security** on `app_user` and `school_membership`, with a policy keyed to a session variable holding the current school. PostgreSQL enforces it; no application code can forget it.
- **A separate master connection** using a master-only role, with the school filter applied in the application, and no tenant role granted direct access to `master`.

Row-level security is the stronger of the two and fits the "database enforces it" principle in architecture §4.5. Decide it when phase D3 is activated; do not design around it now.

---

## 9. Verification against the version 1 scope

The schema was re-checked against a single school with a single branch. It is correct for that scope and needs no change to run it.

### What happens at runtime, end to end

1. **Install.** Create database `sms`; create schemas `master` and `school_main`; create `role_school_main` with grants on both; run both migration sets; the wizard writes the five seed rows in Section 7.
2. **Login.** Identity validates the password against `master.app_user`. The user's `school_membership` rows are read — in version 1 there is exactly one. School and branch scope are therefore known without any selection screen.
3. **Claims.** The cookie carries `school_id`, `branch_scope`, `active_branch_id` (the default branch), `membership_version`, and the role. No school picker, no branch picker.
4. **Request.** Claims populate the tenant context. `SchoolDbContext` uses the schema name from configuration. The global filter appends `WHERE branch_id = @activeBranch`, which always matches.
5. **Write.** The application layer sets `BranchId` from the context — always the `Main` branch.

Nothing in this path depends on a feature that version 1 does not build.

### What is carried but inert, and why that is correct

| Carried | Does nothing in v1 because | Kept because |
|---|---|---|
| `school` table | There is one row | Anchors the branch foreign key and the schema-version check |
| `BranchId` + global filter | One branch, filter always true | Adding it later touches every table, query, index, and write path |
| `branch_scope` + `school_membership_branch` | One branch makes every scope equivalent | Adding a scope dimension later means rewriting authorization |
| `membership_version` | Memberships rarely change with one admin | Revocation must not wait for cookie expiry (architecture §5.6) |
| `is_platform_admin`, `country_code` | No platform, market undecided | Adding columns to populated tables later is the expensive version |
| `school.status = 'provisioning'` | Setup writes `active` directly | Costs nothing; required when onboarding is built |
| Per-school role naming (`role_school_main`) | One role, no switching | Establishes and exercises the grant model from day one |

Total inert overhead: a handful of columns and one always-true predicate. No table exists solely for the future.

### Three corrections made during this check

1. **Development and test seed matches production: one school, one branch** (Section 7). The branch filter is therefore not exercised in version 1; testing it with two branches is the first task of phase D2, recorded in Section 7.
2. **Both `DbContext` types must share a connection and transaction** (Section 6). Creating a staff member writes to `master` and to the school schema; without a shared transaction a failure leaves a login with no staff record. This must be set up in roadmap step 3.
3. **No standalone `branch_id` indexes in version 1** (Section 5). With one branch the column has zero selectivity, so such an index would never be used while still costing on every write. Use composite indexes with `branch_id` leading instead.

### What is explicitly not a version 1 concern

The cross-tenant readability of `master` described at the end of Section 8 **is not a problem with one school**: there is no second tenant to read anything. It is recorded so that phase D3 begins from a known position rather than discovering it. Do not build row-level security now.

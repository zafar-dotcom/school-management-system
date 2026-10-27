# How to Verify Things

A lookup table of "I want to check X" → the command → how to read what comes back.

All commands run from `D:\myprojectt` in PowerShell. Everything here is **read-only** — nothing in this file changes anything, so all of it is safe to run at any time.

If `dotnet ef` is not found, add `%USERPROFILE%\.dotnet\tools` to your PATH.

---

## 1. Am I on the right database server?

**Run this before any SQL work.** The company PostgreSQL and your local one are both reachable from pgAdmin, and the connection dialog can be misleading.

In pgAdmin's Query Tool:

```sql
SELECT inet_server_addr();
```

| Result | Meaning |
|---|---|
| `::1` or `127.0.0.1` or blank | ✅ Your own machine — safe |
| Anything else, e.g. `138.199.194.175` | ❌ **STOP.** That is the company server. Close it. |

Second check, if you want certainty:

```sql
SELECT datname FROM pg_database WHERE datistemplate = false ORDER BY datname;
```

Your machine shows a short list: `postgres`, `sms`, `sms_test`. If you see `atmoneyappdb`, `gbuatdb`, `fintech-finhub-...` or similar, you are on the company server.

---

## 2. Does my database connection work?

The fastest credential check there is:

```powershell
dotnet ef migrations list --project src/SMS.Infrastructure --context MasterDbContext
```

| Result | Meaning |
|---|---|
| A migration name, no warning | ✅ Connected, password correct |
| `password authentication failed` | ❌ Wrong password in user secrets |
| `Pending status not shown. Unable to determine...` | ❌ Could not reach the database at all |

To know *why* it failed, check the stored connection:

```powershell
dotnet user-secrets list --project src/SMS.Infrastructure
```

You should see exactly three: `ConnectionStrings:Owner`, `ConnectionStrings:App`, `ConnectionStrings:TestOwner`.

---

## 3. Are my migrations applied, or is something pending?

```powershell
dotnet ef migrations list --project src/SMS.Infrastructure --context MasterDbContext
dotnet ef migrations list --project src/SMS.Infrastructure --context SchoolDbContext
```

**Always run both.** There are two contexts and they are migrated separately — one can be up to date while the other is not.

### Reading the output

| What you see | What it means |
|---|---|
| `20261026180448_InitialMaster` | Applied ✅ |
| `20261026180448_InitialMaster (Pending)` | In the code, **not yet in the database** |
| `Pending status not shown. Unable to determine which migrations have been applied...` | EF did not check — see below |

**About that warning.** It is worded badly and lists only two causes, but there is a third it does not mention: **you passed `--no-connect`**, which tells EF not to touch the database at all. In that case the warning is normal and nothing is wrong.

So the warning means one of:
- You used `--no-connect` → expected, ignore it
- The credentials are wrong → fix the user secret
- PostgreSQL is not running → check the service

### When you will actually use this

- **After pulling someone else's changes** — did they add a migration you have not applied?
- **Before a release** — confirming the database matches the code
- **After changing a connection string** — fastest credential check available
- **When something behaves oddly** — a `(Pending)` migration explains a missing column instantly

### If something is pending

```powershell
dotnet ef database update --project src/SMS.Infrastructure --context MasterDbContext
dotnet ef database update --project src/SMS.Infrastructure --context SchoolDbContext
```

---

## 4. What is actually in the database right now?

Open psql (it will prompt for the password):

```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U sms_owner -d sms
```

Then, at the `sms=>` prompt:

```
\dt master.*          -- list tables in the master schema
\dt school_main.*     -- list tables in the school schema
\d master.school      -- full description of one table: columns, indexes, constraints
\di master.*          -- all indexes
\q                    -- quit
```

**Expected:** 12 tables in `master`, 2 in `school_main` (including each schema's `__ef_migrations_history`).

Or in pgAdmin: expand **sms → Schemas → master → Tables**.

---

## 5. Are the two database roles behaving correctly?

This is the security property the whole design rests on — the application must not be able to change the schema.

Connect **as the application role**:

```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U role_school_main -d sms
```

Then run both of these:

```sql
SELECT count(*) FROM master.school;     -- expect: a number (0 right now)
CREATE TABLE master.hack (x int);       -- expect: ERROR permission denied
```

| Result | Meaning |
|---|---|
| First works, second says `permission denied for schema master` | ✅ Correct — this is the intended behaviour |
| The `CREATE TABLE` succeeds | ❌ Permissions are wrong. The app role has too much power. |

A failure here is not a bug to shrug at — it means a bug in application code could drop a table.

---

## 6. Does the solution build?

```powershell
dotnet build
```

Expect `Build succeeded` with **0 Warning(s)** and **0 Error(s)**. Warnings are treated as something to fix, not background noise.

---

## 7. Are the generated SQL scripts up to date?

The files in `db/` are generated from migrations and shipped to a school's server by the installer. If they fall behind the code, the installer builds an outdated schema.

Check whether they match what is applied:

```powershell
Select-String -Path db/02_master_schema.sql,db/03_school_schema.sql -Pattern "'\d{14}_\w+'" | ForEach-Object { $_.Matches.Value } | Sort-Object -Unique
```

Compare with what the database says — in psql on `sms`:

```sql
SELECT migration_id FROM master.__ef_migrations_history
UNION ALL
SELECT migration_id FROM school_main.__ef_migrations_history
ORDER BY 1;
```

The two lists must be identical. **Regenerate after every new migration:**

```powershell
dotnet ef migrations script --project src/SMS.Infrastructure --context MasterDbContext --idempotent -o db/02_master_schema.sql
dotnet ef migrations script --project src/SMS.Infrastructure --context SchoolDbContext --idempotent -o db/03_school_schema.sql
```

**Never pass `--no-build` here** — it silently uses a stale assembly and produces a script missing your newest migration.

---

## 8. Is any password sitting in the project folder?

Run before every commit:

```powershell
Get-ChildItem -Recurse -Include *.cs,*.json,*.sql,*.md -File |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern "Password=(?!REPLACE|NEW|\.\.\.|not-configured|<)" |
  Where-Object { $_.Line -notmatch 'Select-String' }
```

**Expect no results.** Anything found must be replaced with a placeholder before committing.

> An earlier version of this command passed `-Include` and `-Recurse` to `Select-String`,
> which has neither parameter. It threw, the result was empty, and the check therefore
> reported "clean" without ever having looked — the worst way for a safety check to fail.
> Filtering with `Get-ChildItem` first is what makes it actually run.
>
> `not-configured` is excluded because `DesignTimeFactories.cs` carries it as a deliberate
> placeholder, so that `dotnet ef` gives a clear error on a machine with no secrets set
> instead of a confusing one. The last two filters drop this command's own line and any
> documentation that writes the keyword followed by `<`, so a genuine hit is the only thing
> this ever prints. **Keep it that way** — a check that always prints something is a check
> nobody reads.

Real credentials belong only here, outside the repository:

```
%APPDATA%\Microsoft\UserSecrets\4a4b251b-307d-4933-b84e-29e4e4f64e49\secrets.json
```

---

## 9. Is PostgreSQL running?

```powershell
Get-Service postgresql*
```

Expect `postgresql-x64-18` with status **Running**. If stopped:

```powershell
Start-Service postgresql-x64-18
```

Check it is accepting connections:

```powershell
Test-NetConnection localhost -Port 5432
```

`TcpTestSucceeded : True` means the port is open.

---

## 10. Is password authentication still switched on?

During setup, `pg_hba.conf` was temporarily set to `trust` to reset a forgotten password. Confirm it was put back:

```powershell
Select-String -Path "C:\Program Files\PostgreSQL\18\data\pg_hba.conf" -Pattern "^\s*(local|host)\s+(all|replication)"
```

**Every line must end in `scram-sha-256`.** If any says `trust`, anyone with access to your machine can reach the database with no password — fix it and restart the service.

---

## Quick reference

| I want to know | Command |
|---|---|
| Am I on my own server? | `SELECT inet_server_addr();` → must be `::1` |
| Does my connection work? | `dotnet ef migrations list ... --context MasterDbContext` |
| Anything unapplied? | Same command — look for `(Pending)` |
| What tables exist? | `\dt master.*` in psql |
| Are role permissions right? | `CREATE TABLE master.hack(x int);` as `role_school_main` → must fail |
| Does it build? | `dotnet build` |
| Which secrets are set? | `dotnet user-secrets list --project src/SMS.Infrastructure` |
| Is PostgreSQL up? | `Get-Service postgresql*` |

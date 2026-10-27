# Setting Up on a New Machine

From a bare Windows laptop to a running School Management System. Follow it in order; each
step says how to tell it worked.

Allow about an hour, most of it waiting for installers.

> **The one rule that matters throughout:** this project only ever touches the **local**
> PostgreSQL. Before running any SQL, confirm with `SELECT inet_server_addr();` — it must
> return `::1` or `127.0.0.1`. A previous session nearly ran the setup script against a
> company production server because pgAdmin's hostname field looked blank. Never trust the
> connection dialog.

---

## 1. Install the tools

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | **10.0.300** or later | `winget install Microsoft.DotNet.SDK.10` |
| PostgreSQL | **18.x** | Installer from postgresql.org. Keep the default port 5432. **Write the superuser password down** — recovering it means editing `pg_hba.conf` |
| Git | any recent | |
| pgAdmin | optional | Ships with the PostgreSQL installer. psql on the command line is enough |

Then the EF Core tool:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.14
```

**Check:**

```powershell
dotnet --version          # 10.0.300 or later
dotnet ef --version       # 10.0.14
Get-Service postgresql*   # postgresql-x64-18, Running
```

If `dotnet ef` is not found, add `%USERPROFILE%\.dotnet\tools` to your PATH and open a new
terminal.

`psql.exe` is **not** on the PATH by default. It lives at
`C:\Program Files\PostgreSQL\18\bin\psql.exe` — the commands below use the full path.

---

## 2. Clone the repository

```powershell
git clone https://github.com/zafar-dotcom/school-management-system.git
cd school-management-system
```

The folder name does not matter. Earlier documents say `D:\myprojectt` because that is
where it was first built; nothing depends on the path.

---

## 3. Create the database and the two roles

**First, confirm you are on your own machine.** Open psql as the superuser:

```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U postgres -d postgres
```

```sql
SELECT inet_server_addr();
```

Must be `::1`, `127.0.0.1`, or blank. **Anything else — stop.**

Now follow **`db/01_create_database_and_roles.sql`**. Read it rather than pasting it blind;
it is written in numbered steps and explains which connection each belongs to. In short:

1. As superuser on the `postgres` database — create roles `sms_owner` and `role_school_main`, each with a password you choose.
2. `CREATE DATABASE sms OWNER sms_owner;` and the same for `sms_test`.
3. Connect **to `sms`** and create the `master` and `school_main` schemas, grant `USAGE` to `role_school_main`, and set default privileges.
4. Repeat step 3 connected to `sms_test`.

Two things that will bite otherwise, both learned the hard way:

- **`CREATE DATABASE` cannot run inside a transaction.** In pgAdmin, select just that one line and press F5 — selecting text runs only the selection.
- **Do not add `LC_COLLATE` or `LC_CTYPE`.** Linux-style locale names such as `en_US.UTF-8` are invalid on a Windows PostgreSQL install and the statement fails. Inherit the server default.

**Check** — connected to `sms`:

```sql
SELECT schema_name FROM information_schema.schemata
WHERE schema_name IN ('master','school_main');     -- expect both
```

---

## 4. Store the three connection strings

Credentials live in .NET user secrets, **outside the repository**, so they cannot be
committed. The template is `src/SMS.Infrastructure/secrets.example.json`.

Run from the repository root, substituting your passwords:

```powershell
dotnet user-secrets set "ConnectionStrings:Owner"     "Host=localhost;Port=5432;Database=sms;Username=sms_owner;Password=<your-password>"        --project src/SMS.Infrastructure
dotnet user-secrets set "ConnectionStrings:App"       "Host=localhost;Port=5432;Database=sms;Username=role_school_main;Password=<your-password>" --project src/SMS.Infrastructure
dotnet user-secrets set "ConnectionStrings:TestOwner" "Host=localhost;Port=5432;Database=sms_test;Username=sms_owner;Password=<your-password>"   --project src/SMS.Infrastructure
```

> **If a password contains a semicolon, wrap the value in double quotes** — `Password=<"value">`.
> Unquoted, Npgsql reads everything after the `;` as a separate keyword and the connection
> string **will not parse at all** — you get a format error, not an authentication error.
> This cost real time once; see `PROJECT_STATUS.md` gotcha 12.

One `UserSecretsId` is shared by `SMS.Infrastructure`, `SMS.Web` and `SMS.Api`, so setting
them once on `SMS.Infrastructure` covers all three.

**Check:**

```powershell
dotnet user-secrets list --project src/SMS.Infrastructure    # expect exactly three
```

---

## 5. Build

```powershell
dotnet build
```

Expect **0 Warning(s), 0 Error(s)**. Warnings are errors on this project
(`Directory.Build.props`), so a warning will stop you here.

---

## 6. Create the schema

Migrations are a deliberate step and **never run automatically at startup**. There are two
`DbContext` types with separate migration histories, so **always pass `--context`** — the
wrong one silently targets the wrong schema.

```powershell
dotnet ef database update --project src/SMS.Infrastructure --context MasterDbContext
dotnet ef database update --project src/SMS.Infrastructure --context SchoolDbContext
```

**Check:**

```powershell
dotnet ef migrations list --project src/SMS.Infrastructure --context MasterDbContext
dotnet ef migrations list --project src/SMS.Infrastructure --context SchoolDbContext
```

Every migration listed, none marked `(Pending)`. Then confirm the table counts — `13` in
`master`, `2` in `school_main` — using §4 of `HOW_TO_VERIFY.md`.

---

## 7. Trust the HTTPS development certificate

Not optional. The authentication cookie is marked `Secure`, so **sign-in silently fails
over plain HTTP**.

```powershell
dotnet dev-certs https --trust
```

Accept the Windows prompt. **Check:** `dotnet dev-certs https --check --trust` should report
a *trusted* certificate and exit 0.

---

## 8. Run it

User secrets load **only in the Development environment**. Without this variable you get
"ConnectionStrings:App is not configured", which looks like a database fault but is not.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/SMS.Web
```

Open **https://localhost:7106**. On an empty database every request redirects to `/Setup`.

Complete the wizard — school name, branch name, time zone, and the first administrator.
The password needs at least 12 characters with upper, lower, a digit and a symbol. That
writes the school, the `Main` branch, the administrator and the membership in one
transaction, and the application becomes usable.

**Check:** sign in, open **Classes**, add one. It should appear with a branch id filled in
by the server — there is no branch field on the form.

### The API, if you need it

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/SMS.Api        # https://localhost:7286
```

`GET /health` should return `{"status":"Ready","ready":true,"detail":null}`.

No `Jwt:SigningKey` is needed in Development — the API generates an ephemeral one and warns
that tokens stop working when the process restarts. **Outside Development it refuses to
start without one.** To set it:

```powershell
dotnet user-secrets set "Jwt:SigningKey" "at-least-32-bytes-of-random-text" --project src/SMS.Infrastructure
```

---

## 9. Read these before changing anything

| Document | Why |
|---|---|
| `SESSION_HANDOVER.md` | Start here. The whole picture, every decision, and what reversed |
| `PROJECT_STATUS.md` | What is built, what is verified, what is pending, what to do next |
| `School_Management_System_Architecture_and_Decisions.md` | A decision and its reasoning. Revision 9 |
| `Master_Schema_Design.md` | The `master` schema |
| `HOW_TO_VERIFY.md` | Keep this one open. "I want to check X" → the command → how to read it |
| **`SETUP_NEW_MACHINE.md`** (this file) | Only needed once per machine |

---

## 10. When things go wrong

| Symptom | Cause |
|---|---|
| `ConnectionStrings:App is not configured` | `ASPNETCORE_ENVIRONMENT` is not `Development`, so user secrets did not load |
| Format error on the connection string | A password containing `;` is not quoted — §4 above |
| `password authentication failed` | Wrong password in user secrets, or the role was not created |
| A 503 maintenance page on every request | The startup schema check refused. **Read the page** — it names the reason. Usually a migration is unapplied |
| Every request redirects to `/Setup` | No school row yet. Expected on a fresh database |
| Sign-in appears to do nothing | You are on `http://`. The auth cookie is `Secure` and will not be set — use `https://localhost:7106` |
| `MSB3027` / file in use on build | A host is still running. `Get-Process SMS.Web,SMS.Api \| Stop-Process -Force` |
| `dotnet ef` not found | `%USERPROFILE%\.dotnet\tools` is not on PATH |
| `CREATE DATABASE cannot run inside a transaction block` | pgAdmin wraps multi-statement runs. Select that one line and press F5 |
| `migrations list` warns "Pending status not shown" | You passed `--no-connect`, or the database is genuinely unreachable |

Gotchas 1 to 18 in `PROJECT_STATUS.md` §6 cover the rest, each with what it cost and how it
was resolved.

---

## 11. Before your first commit on this machine

Run the password scan in §8 of `HOW_TO_VERIFY.md`. It must return nothing. Removing a
secret after it has been pushed is painful, and the scan itself was broken once and
reported "clean" without ever having looked — so make sure it actually runs.

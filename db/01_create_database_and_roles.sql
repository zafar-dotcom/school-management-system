-- =====================================================================
--  School Management System — one-time database and role setup
--  Target: PostgreSQL 18, local instance on port 5432
--
--  RUN THIS IN TWO SEPARATE STEPS. The connection you are on matters.
--  Replace the two passwords before running.
--
--  This script creates ONLY:
--      - two roles:   sms_owner, role_school_main
--      - one database: sms
--      - two schemas inside sms: master, school_main
--
--  It makes NO change to any existing database.
-- =====================================================================


-- =====================================================================
--  STEP 1 — connect to the "postgres" database as a superuser
-- =====================================================================
--  In pgAdmin: expand your server → Databases → postgres →
--              right-click → Query Tool. Run only this block.
--
--  Everything here is a CREATE. Nothing existing is altered.
-- ---------------------------------------------------------------------

-- The migration/owner role. Owns the schema. Runs EF migrations.
CREATE ROLE sms_owner
    LOGIN
    PASSWORD 'REPLACE_WITH_OWNER_PASSWORD'
    NOSUPERUSER
    NOCREATEDB
    NOCREATEROLE
    NOREPLICATION;

-- The application runtime role. Reads and writes data. Cannot change schema.
CREATE ROLE role_school_main
    LOGIN
    PASSWORD 'REPLACE_WITH_APP_PASSWORD'
    NOSUPERUSER
    NOCREATEDB
    NOCREATEROLE
    NOREPLICATION;

-- ---------------------------------------------------------------------
--  STEP 1b — run this ONE LINE ON ITS OWN
-- ---------------------------------------------------------------------
--  CREATE DATABASE cannot run inside a transaction, and pgAdmin wraps a
--  multi-statement run in one. So select just the next line and press F5.
--  (Selecting text makes pgAdmin run only the selection.)
--
--  Locale and encoding are inherited from the server default. Do not pass
--  LC_COLLATE/LC_CTYPE: Linux-style names such as 'en_US.UTF-8' are not
--  valid on a Windows PostgreSQL install and the statement would fail.

CREATE DATABASE sms OWNER sms_owner;


-- ---------------------------------------------------------------------
--  STEP 1c — again, this ONE LINE ON ITS OWN
-- ---------------------------------------------------------------------

CREATE DATABASE sms_test OWNER sms_owner;


-- ---------------------------------------------------------------------
--  STEP 1d — these four can be run together
-- ---------------------------------------------------------------------

REVOKE CONNECT ON DATABASE sms FROM PUBLIC;
GRANT  CONNECT ON DATABASE sms TO sms_owner, role_school_main;

REVOKE CONNECT ON DATABASE sms_test FROM PUBLIC;
GRANT  CONNECT ON DATABASE sms_test TO sms_owner, role_school_main;

-- STOP HERE. Step 2 runs on a different connection.


-- =====================================================================
--  STEP 2 — DISCONNECT, then connect to the "sms" database
-- =====================================================================
--
--  !! IMPORTANT !!
--  The REVOKE below is scoped to whichever database you are connected
--  to. Running it while connected to a company database would remove
--  permissions there. Check the Query Tool title bar says "sms" before
--  you run anything in this block.
--
--  In pgAdmin: Databases → sms → right-click → Query Tool.
-- ---------------------------------------------------------------------

-- Confirm where you are. This must print: sms
SELECT current_database();

-- We do not use the public schema. Lock it down inside THIS database only.
REVOKE ALL ON SCHEMA public FROM PUBLIC;

-- The two schemas the application uses.
CREATE SCHEMA IF NOT EXISTS master      AUTHORIZATION sms_owner;
CREATE SCHEMA IF NOT EXISTS school_main AUTHORIZATION sms_owner;

-- The app role may use the schemas but not create objects in them.
GRANT USAGE ON SCHEMA master      TO role_school_main;
GRANT USAGE ON SCHEMA school_main TO role_school_main;

-- Any table that sms_owner creates later (i.e. by migration) is
-- automatically readable and writable by the app role.
ALTER DEFAULT PRIVILEGES FOR ROLE sms_owner IN SCHEMA master, school_main
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO role_school_main;

ALTER DEFAULT PRIVILEGES FOR ROLE sms_owner IN SCHEMA master, school_main
    GRANT USAGE, SELECT ON SEQUENCES TO role_school_main;


-- =====================================================================
--  STEP 3 — repeat STEP 2, connected to "sms_test"
-- =====================================================================
--  Same block, different database. Check the title bar says sms_test.
-- =====================================================================


-- =====================================================================
--  VERIFY — run while connected to sms
-- =====================================================================

-- Should list: master, school_main  (and public)
SELECT schema_name FROM information_schema.schemata
WHERE  schema_name IN ('master','school_main','public');

-- Should show both roles with all the "can" columns false
SELECT rolname, rolsuper, rolcreatedb, rolcreaterole
FROM   pg_roles
WHERE  rolname IN ('sms_owner','role_school_main');


-- =====================================================================
--  UNDO — removes everything this script created, and nothing else
-- =====================================================================
--  Run from the "postgres" database.
--
--  DROP DATABASE sms;
--  DROP DATABASE sms_test;
--  DROP ROLE role_school_main;
--  DROP ROLE sms_owner;
-- =====================================================================

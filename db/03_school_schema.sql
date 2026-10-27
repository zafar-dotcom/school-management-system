DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'school_main') THEN
        CREATE SCHEMA school_main;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS school_main.__ef_migrations_history (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM school_main.__ef_migrations_history WHERE "migration_id" = '20261026180204_InitialSchool') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'school_main') THEN
            CREATE SCHEMA school_main;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM school_main.__ef_migrations_history WHERE "migration_id" = '20261026180204_InitialSchool') THEN
    CREATE TABLE school_main.class (
        id uuid NOT NULL,
        code character varying(20) NOT NULL,
        name character varying(200) NOT NULL,
        sort_order integer NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        updated_at timestamp with time zone,
        updated_by uuid,
        branch_id uuid NOT NULL,
        CONSTRAINT pk_class PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM school_main.__ef_migrations_history WHERE "migration_id" = '20261026180204_InitialSchool') THEN
    CREATE UNIQUE INDEX ix_class_branch_id_code ON school_main.class (branch_id, code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM school_main.__ef_migrations_history WHERE "migration_id" = '20261026180204_InitialSchool') THEN
    INSERT INTO school_main.__ef_migrations_history (migration_id, product_version)
    VALUES ('20261026180204_InitialSchool', '10.0.14');
    END IF;
END $EF$;
COMMIT;


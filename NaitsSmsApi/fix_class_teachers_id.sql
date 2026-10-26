-- Repair ClassTeachers when the Id primary-key column is missing.
-- Safe to run multiple times.

DO $repair$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'ClassTeachers'
          AND column_name = 'Id'
    ) THEN
        ALTER TABLE "ClassTeachers" ADD COLUMN "Id" character varying(40);

        UPDATE "ClassTeachers"
        SET "Id" = "UserId"
        WHERE "Id" IS NULL OR btrim("Id") = '';

        UPDATE "ClassTeachers"
        SET "Id" = gen_random_uuid()::text
        WHERE "Id" IS NULL OR btrim("Id") = '';

        ALTER TABLE "ClassTeachers" ALTER COLUMN "Id" SET NOT NULL;

        IF NOT EXISTS (
            SELECT 1
            FROM pg_constraint
            WHERE conname = 'PK_ClassTeachers'
        ) THEN
            ALTER TABLE "ClassTeachers"
                ADD CONSTRAINT "PK_ClassTeachers" PRIMARY KEY ("Id");
        END IF;
    END IF;
END
$repair$;

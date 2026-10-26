-- Update AcademicSessions.Session by Id (from legacy reference data)
UPDATE "AcademicSessions" SET "Session" = '2019/2020' WHERE "Id" = '44690e20-e343-4e7a-8cf1-61f52aae582d';
UPDATE "AcademicSessions" SET "Session" = '2020/2021' WHERE "Id" = '465093a3-d97a-4f45-82bd-957df95e2a17';
UPDATE "AcademicSessions" SET "Session" = '2021/2022' WHERE "Id" = '59624fb6-ffa2-48fd-8615-ac519ce7b941';
UPDATE "AcademicSessions" SET "Session" = '2022/2023' WHERE "Id" = '8fb03b8c-ad31-42c5-8693-29ec48d1b9c4';
UPDATE "AcademicSessions" SET "Session" = '2023/2024' WHERE "Id" = '9ebfdb21-0bec-4b1a-8442-b8d36218cf57';
UPDATE "AcademicSessions" SET "Session" = '2024/2025' WHERE "Id" = 'ed70b55d-0da2-4e85-88db-a3bff7dbe700';
UPDATE "AcademicSessions" SET "Session" = '2025/2026' WHERE "Id" = 'f15fdf72-577d-4522-80fa-2694c213c7cc';

SELECT "Id", "Session", "Status", "SchoolInfoId"
FROM "AcademicSessions"
ORDER BY "Session";

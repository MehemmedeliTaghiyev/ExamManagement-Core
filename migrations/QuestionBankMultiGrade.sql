-- QuestionBankMultiGrade (EF Core 9, idempotent) + grade-10 overlap tags
-- SmarterASP: SQL Manager -> db_ace530_exam -> New Query -> paste this file -> Execute.
-- Required together with publishing/redeploying the API. SQL alone or publish alone is not enough.
-- QuestionBank only. Does NOT ALTER exam Questions. Does not touch exams 94/96/97/98.
-- Do not use SQLite dump/restore. Keep the DB password in the panel connection string only.
--
-- Grade-10 source: ExamPulse 10th-grade DİM syllabus (same chapter names as bank MainTopic).
-- Frontend StudentSyllabus generic labels map onto those chapters; this script matches MainTopic.
-- Do not invent topics from the grade-11 February calendar.
--
-- Pipeline inventory used for the February schedule (4700 classified rows): 3699 would gain
-- grade 10 (topics on the grade-10 syllabus). 1001 stay 9/11-only (silsilə 193, koordinat 267,
-- fırlanma 7, törəmə 200, limit 123, inteqral 211). Live SmarterASP counts are the SELECT
-- result sets below (WouldGainGrade10 / RowsTaggedGrade10). Per-topic pipeline totals that
-- match: Həqiqi 402, Kompleks 97, Nisbət 227, Rasional 480, kök 190, Ölçmə 221, Birdəyişənli 383,
-- Tənliklər sistemi 171, Bərabərsizliklər 294, Funksiyalar 108, Triqonometriya 4, Üstlü funksiya 2,
-- Üstlü tənlik 2, Sadə həndəsi 90, Çevrə 235, Üçbucaqlar 345, Dördbucaqlılar 208, Sahə 146,
-- Fəzada 84, Çoxüzlülər 10.

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.QuestionBank', N'Grade') IS NOT NULL
   AND EXISTS (
        SELECT 1
        FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.QuestionBank')
          AND c.name = N'Grade'
          AND c.max_length > 0
          AND c.max_length < 64 -- nvarchar(n) bytes; widen anything shorter than nvarchar(32)
   )
BEGIN
    ALTER TABLE [QuestionBank] ALTER COLUMN [Grade] nvarchar(32) NULL;
END;

IF OBJECT_ID(N'dbo.QuestionBank', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261115101500_QuestionBankMultiGrade'
   )
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261115101500_QuestionBankMultiGrade', N'9.0.0');
END;

COMMIT;
GO

-- Mapping: ExamPulse grade-10 syllabus topic = bank MainTopic (exact).
SELECT * FROM (VALUES
    (N'Həqiqi ədədlər', N'Həqiqi ədədlər', N'tag 9/11 → also 10'),
    (N'Kompleks ədədlər', N'Kompleks ədədlər', N'tag 9/11 → also 10'),
    (N'Nisbət. Tənasüb. Faiz', N'Nisbət. Tənasüb. Faiz', N'tag 9/11 → also 10'),
    (N'Rasional ifadələr', N'Rasional ifadələr', N'tag 9/11 → also 10'),
    (N'n-ci dərəcədən kök. İrrasional ifadələr. Üstlü qüvvət', N'n-ci dərəcədən kök. İrrasional ifadələr. Üstlü qüvvət', N'tag 9/11 → also 10'),
    (N'Ölçmə. Statistika. Çoxluqlar. Birləşmələr və ehtimal', N'Ölçmə. Statistika. Çoxluqlar. Birləşmələr və ehtimal', N'tag 9/11 → also 10'),
    (N'Birdəyişənli tənliklər və məsələ həlli', N'Birdəyişənli tənliklər və məsələ həlli', N'tag 9/11 → also 10'),
    (N'Tənliklər sistemi və məsələ həlli', N'Tənliklər sistemi və məsələ həlli', N'tag 9/11 → also 10'),
    (N'Bərabərsizliklər', N'Bərabərsizliklər', N'tag 9/11 → also 10'),
    (N'Funksiyalar və qrafiklər', N'Funksiyalar və qrafiklər', N'tag 9/11 → also 10'),
    (N'Triqonometrik funksiyalar. Triqonometrik tənliklər', N'Triqonometrik funksiyalar. Triqonometrik tənliklər', N'tag 9/11 → also 10'),
    (N'Üstlü və loqarifmik funksiyalar', N'Üstlü və loqarifmik funksiyalar', N'tag 9/11 → also 10'),
    (N'Üstlü və loqarifmik tənliklər və bərabərsizliklər', N'Üstlü və loqarifmik tənliklər və bərabərsizliklər', N'tag 9/11 → also 10'),
    (N'Sadə həndəsi fiqurlar', N'Sadə həndəsi fiqurlar', N'tag 9/11 → also 10'),
    (N'Çevrə və dairə', N'Çevrə və dairə', N'tag 9/11 → also 10'),
    (N'Üçbucaqlar', N'Üçbucaqlar', N'tag 9/11 → also 10'),
    (N'Dördbucaqlılar və çoxbucaqlılar', N'Dördbucaqlılar və çoxbucaqlılar', N'tag 9/11 → also 10'),
    (N'Fiqurların sahəsi', N'Fiqurların sahəsi', N'tag 9/11 → also 10'),
    (N'Fəzada nöqtə, düz xətt, müstəvi', N'Fəzada nöqtə, düz xətt, müstəvi', N'tag 9/11 → also 10'),
    (N'Çoxüzlülər, onların səthi və həcmi', N'Çoxüzlülər, onların səthi və həcmi', N'tag 9/11 → also 10'),
    (N'Ədədi ardıcıllıqlar. Silsilələr', N'Ədədi ardıcıllıqlar. Silsilələr', N'exclude (grade 11 only)'),
    (N'Koordinatlar üsulu. Hərəkət. Oxşarlıq. Vektorlar', N'Koordinatlar üsulu. Hərəkət. Oxşarlıq. Vektorlar', N'exclude (grade 11 only)'),
    (N'Fırlanma fiqurları, onların səthi və həcmi', N'Fırlanma fiqurları, onların səthi və həcmi', N'exclude (grade 11 only)'),
    (N'Törəmə və onun tətbiqləri', N'Törəmə və onun tətbiqləri', N'exclude (Blok / grade 11)'),
    (N'Ardıcıllığın və funksiyanın limiti', N'Ardıcıllığın və funksiyanın limiti', N'exclude (Blok / grade 11)'),
    (N'İbtidai funksiya və inteqral', N'İbtidai funksiya və inteqral', N'exclude (Blok / grade 11)')
) AS Map(SyllabusTopic, BankMainTopic, Action);
GO

DECLARE @topics TABLE (Topic nvarchar(200) NOT NULL PRIMARY KEY);
INSERT INTO @topics (Topic) VALUES
    (N'Həqiqi ədədlər'),
    (N'Kompleks ədədlər'),
    (N'Nisbət. Tənasüb. Faiz'),
    (N'Rasional ifadələr'),
    (N'n-ci dərəcədən kök. İrrasional ifadələr. Üstlü qüvvət'),
    (N'Ölçmə. Statistika. Çoxluqlar. Birləşmələr və ehtimal'),
    (N'Birdəyişənli tənliklər və məsələ həlli'),
    (N'Tənliklər sistemi və məsələ həlli'),
    (N'Bərabərsizliklər'),
    (N'Funksiyalar və qrafiklər'),
    (N'Triqonometrik funksiyalar. Triqonometrik tənliklər'),
    (N'Üstlü və loqarifmik funksiyalar'),
    (N'Üstlü və loqarifmik tənliklər və bərabərsizliklər'),
    (N'Sadə həndəsi fiqurlar'),
    (N'Çevrə və dairə'),
    (N'Üçbucaqlar'),
    (N'Dördbucaqlılar və çoxbucaqlılar'),
    (N'Fiqurların sahəsi'),
    (N'Fəzada nöqtə, düz xətt, müstəvi'),
    (N'Çoxüzlülər, onların səthi və həcmi');

;WITH src AS (
    SELECT
        qb.Id,
        qb.MainTopic,
        qb.Grade,
        REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(qb.Grade, N''))), N';', N','), N'|', N','), N' ', N'') AS Compact
    FROM dbo.QuestionBank AS qb
    INNER JOIN @topics AS t ON t.Topic = qb.MainTopic
),
flagged AS (
    SELECT
        Id,
        MainTopic,
        Grade,
        Compact,
        CASE WHEN N',' + Compact + N',' LIKE N'%,9,%' THEN 1 ELSE 0 END AS Has9,
        CASE WHEN N',' + Compact + N',' LIKE N'%,10,%' THEN 1 ELSE 0 END AS Has10,
        CASE WHEN N',' + Compact + N',' LIKE N'%,11,%' THEN 1 ELSE 0 END AS Has11
    FROM src
)
SELECT
    COUNT(*) AS WouldGainGrade10
FROM flagged
WHERE (Has9 = 1 OR Has11 = 1) AND Has10 = 0;

;WITH src AS (
    SELECT
        qb.Id,
        qb.MainTopic,
        qb.Grade,
        REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(qb.Grade, N''))), N';', N','), N'|', N','), N' ', N'') AS Compact
    FROM dbo.QuestionBank AS qb
    INNER JOIN @topics AS t ON t.Topic = qb.MainTopic
),
flagged AS (
    SELECT
        Id,
        MainTopic,
        Grade,
        Compact,
        CASE WHEN N',' + Compact + N',' LIKE N'%,9,%' THEN 1 ELSE 0 END AS Has9,
        CASE WHEN N',' + Compact + N',' LIKE N'%,10,%' THEN 1 ELSE 0 END AS Has10,
        CASE WHEN N',' + Compact + N',' LIKE N'%,11,%' THEN 1 ELSE 0 END AS Has11
    FROM src
)
SELECT
    MainTopic,
    Grade AS GradeBefore,
    COUNT(*) AS Rows
FROM flagged
WHERE (Has9 = 1 OR Has11 = 1) AND Has10 = 0
GROUP BY MainTopic, Grade
ORDER BY MainTopic, Grade;
GO

DECLARE @topics TABLE (Topic nvarchar(200) NOT NULL PRIMARY KEY);
INSERT INTO @topics (Topic) VALUES
    (N'Həqiqi ədədlər'),
    (N'Kompleks ədədlər'),
    (N'Nisbət. Tənasüb. Faiz'),
    (N'Rasional ifadələr'),
    (N'n-ci dərəcədən kök. İrrasional ifadələr. Üstlü qüvvət'),
    (N'Ölçmə. Statistika. Çoxluqlar. Birləşmələr və ehtimal'),
    (N'Birdəyişənli tənliklər və məsələ həlli'),
    (N'Tənliklər sistemi və məsələ həlli'),
    (N'Bərabərsizliklər'),
    (N'Funksiyalar və qrafiklər'),
    (N'Triqonometrik funksiyalar. Triqonometrik tənliklər'),
    (N'Üstlü və loqarifmik funksiyalar'),
    (N'Üstlü və loqarifmik tənliklər və bərabərsizliklər'),
    (N'Sadə həndəsi fiqurlar'),
    (N'Çevrə və dairə'),
    (N'Üçbucaqlar'),
    (N'Dördbucaqlılar və çoxbucaqlılar'),
    (N'Fiqurların sahəsi'),
    (N'Fəzada nöqtə, düz xətt, müstəvi'),
    (N'Çoxüzlülər, onların səthi və həcmi');

BEGIN TRANSACTION;

;WITH src AS (
    SELECT
        qb.Id,
        REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(qb.Grade, N''))), N';', N','), N'|', N','), N' ', N'') AS Compact
    FROM dbo.QuestionBank AS qb
    INNER JOIN @topics AS t ON t.Topic = qb.MainTopic
),
flagged AS (
    SELECT
        Id,
        Compact,
        CASE WHEN N',' + Compact + N',' LIKE N'%,9,%' THEN 1 ELSE 0 END AS Has9,
        CASE WHEN N',' + Compact + N',' LIKE N'%,10,%' THEN 1 ELSE 0 END AS Has10,
        CASE WHEN N',' + Compact + N',' LIKE N'%,11,%' THEN 1 ELSE 0 END AS Has11
    FROM src
)
UPDATE qb
SET Grade = CASE
    WHEN f.Has9 = 1 AND f.Has11 = 1 THEN N'9,10,11'
    WHEN f.Has9 = 1 THEN N'9,10'
    WHEN f.Has11 = 1 THEN N'10,11'
    ELSE N'10'
END
FROM dbo.QuestionBank AS qb
INNER JOIN flagged AS f ON f.Id = qb.Id
WHERE (f.Has9 = 1 OR f.Has11 = 1)
  AND f.Has10 = 0;

SELECT @@ROWCOUNT AS RowsTaggedGrade10;

COMMIT;
GO

-- Read-only sanity: copied exam questions are unchanged (this script never writes Questions).
SELECT e.Id AS ExamId, e.Title, COUNT(q.Id) AS QuestionCount
FROM dbo.Exams AS e
LEFT JOIN dbo.Questions AS q ON q.ExamId = e.Id
WHERE e.Id IN (94, 96, 97, 98)
GROUP BY e.Id, e.Title
ORDER BY e.Id;
GO

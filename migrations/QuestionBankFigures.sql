-- QuestionBank figure stem (EF Core 9, idempotent) + drop text-only duplicates of figured items
-- SmarterASP: SQL Manager -> db_ace530_exam -> New Query -> paste this file -> Execute.
-- QuestionBank only. Does NOT ALTER exam Questions. Does not touch exams 94/96/97/98.
-- Keep the DB password in the panel connection string only. Do not dump SQLite onto SQL Server.
--
-- Figured bank rows are stem (şərt) + FigureUrl. uniqueness_text/cosine dumps stay local
-- (questions_dim.db) and must not be a second student-facing row.
-- Selection metadata remains topic (MainTopic), question difficulty, and multi-grade.

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.QuestionBank', N'FigureUrl') IS NOT NULL
   AND EXISTS (
        SELECT 1
        FROM sys.columns c
        WHERE c.object_id = OBJECT_ID(N'dbo.QuestionBank')
          AND c.name = N'FigureUrl'
          AND c.max_length > 0 -- skip nvarchar(max) = -1
   )
BEGIN
    ALTER TABLE [QuestionBank] ALTER COLUMN [FigureUrl] nvarchar(max) NULL;
END;

IF OBJECT_ID(N'dbo.QuestionBank', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261115110000_QuestionBankFigureStem'
   )
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261115110000_QuestionBankFigureStem', N'9.0.0');
END;

COMMIT;
GO

-- Preview: text-only QuestionBank rows that duplicate a figured stem (same topic).
SELECT
    t.Id AS TextOnlyId,
    t.Qnum AS TextOnlyQnum,
    f.Id AS FigureId,
    f.Qnum AS FigureQnum,
    t.MainTopic
FROM dbo.QuestionBank AS t
INNER JOIN dbo.QuestionBank AS f
    ON f.Id <> t.Id
   AND (t.MainTopic IS NULL OR f.MainTopic IS NULL OR t.MainTopic = f.MainTopic)
WHERE (t.FigureUrl IS NULL OR LTRIM(RTRIM(t.FigureUrl)) = N'')
  AND f.FigureUrl IS NOT NULL AND LTRIM(RTRIM(f.FigureUrl)) <> N''
  AND LEN(LTRIM(RTRIM(t.Text))) >= 24
  AND LEN(LTRIM(RTRIM(f.Text))) >= 24
  AND (
        LTRIM(RTRIM(t.Text)) = LTRIM(RTRIM(f.Text))
     OR LEFT(t.Text, LEN(LTRIM(RTRIM(f.Text)))) = LTRIM(RTRIM(f.Text))
     OR LEFT(f.Text, LEN(LTRIM(RTRIM(t.Text)))) = LTRIM(RTRIM(t.Text))
  );
GO

BEGIN TRANSACTION;

DECLARE @dupes TABLE (Id int NOT NULL PRIMARY KEY);
INSERT INTO @dupes (Id)
SELECT DISTINCT t.Id
FROM dbo.QuestionBank AS t
INNER JOIN dbo.QuestionBank AS f
    ON f.Id <> t.Id
   AND (t.MainTopic IS NULL OR f.MainTopic IS NULL OR t.MainTopic = f.MainTopic)
WHERE (t.FigureUrl IS NULL OR LTRIM(RTRIM(t.FigureUrl)) = N'')
  AND f.FigureUrl IS NOT NULL AND LTRIM(RTRIM(f.FigureUrl)) <> N''
  AND LEN(LTRIM(RTRIM(t.Text))) >= 24
  AND LEN(LTRIM(RTRIM(f.Text))) >= 24
  AND (
        LTRIM(RTRIM(t.Text)) = LTRIM(RTRIM(f.Text))
     OR LEFT(t.Text, LEN(LTRIM(RTRIM(f.Text)))) = LTRIM(RTRIM(f.Text))
     OR LEFT(f.Text, LEN(LTRIM(RTRIM(t.Text)))) = LTRIM(RTRIM(t.Text))
  );

DELETE o
FROM dbo.QuestionBankOptions AS o
INNER JOIN @dupes AS d ON d.Id = o.QuestionBankId;

DELETE qb
FROM dbo.QuestionBank AS qb
INNER JOIN @dupes AS d ON d.Id = qb.Id;

SELECT @@ROWCOUNT AS TextOnlyDuplicatesRemoved;

COMMIT;
GO

-- Read-only sanity: copied exam questions are unchanged.
SELECT e.Id AS ExamId, e.Title, COUNT(q.Id) AS QuestionCount
FROM dbo.Exams AS e
LEFT JOIN dbo.Questions AS q ON q.ExamId = e.Id
WHERE e.Id IN (94, 96, 97, 98)
GROUP BY e.Id, e.Title
ORDER BY e.Id;
GO

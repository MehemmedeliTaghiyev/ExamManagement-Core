-- QuestionBank UseCount / LastUsedAt (EF Core 9, idempotent)
-- SmarterASP: SQL Manager -> db_ace530_exam -> New Query -> paste this file -> Execute.
-- QuestionBank only. Does NOT ALTER exam Questions.
-- Backfill UseCount from existing Questions.SourceQnum (distinct exams per Qnum).
-- Keep the DB password in the panel connection string only.

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.QuestionBank', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.QuestionBank', N'UseCount') IS NULL
BEGIN
    ALTER TABLE [QuestionBank] ADD [UseCount] int NOT NULL CONSTRAINT [DF_QuestionBank_UseCount] DEFAULT (0);
END;

IF OBJECT_ID(N'dbo.QuestionBank', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.QuestionBank', N'LastUsedAt') IS NULL
BEGIN
    ALTER TABLE [QuestionBank] ADD [LastUsedAt] datetime2 NULL;
END;

IF OBJECT_ID(N'dbo.QuestionBank', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Questions', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.QuestionBank', N'UseCount') IS NOT NULL
BEGIN
    UPDATE qb
    SET UseCount = c.N
    FROM dbo.QuestionBank AS qb
    INNER JOIN (
        SELECT SourceQnum, COUNT(DISTINCT ExamId) AS N
        FROM dbo.Questions
        WHERE SourceQnum IS NOT NULL AND LTRIM(RTRIM(SourceQnum)) <> N''
        GROUP BY SourceQnum
    ) AS c ON c.SourceQnum = qb.Qnum
    WHERE c.N > ISNULL(qb.UseCount, 0);
END;

IF OBJECT_ID(N'dbo.QuestionBank', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT * FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20261115120000_QuestionBankUseCount'
   )
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261115120000_QuestionBankUseCount', N'9.0.0');
END;

COMMIT;
GO

SELECT
    SUM(CASE WHEN UseCount = 0 THEN 1 ELSE 0 END) AS UnusedRows,
    SUM(CASE WHEN UseCount > 0 THEN 1 ELSE 0 END) AS UsedRows,
    COUNT(*) AS BankRows
FROM dbo.QuestionBank;
GO

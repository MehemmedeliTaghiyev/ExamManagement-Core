-- AddQuestionBank (EF Core 9, idempotent)
-- SmarterASP: SQL Manager -> db_ace530_exam -> New Query -> paste this file -> Execute.
-- Required together with publishing/redeploying the API. SQL alone or publish alone is not enough.
-- Run once before relying on QuestionBank APIs. Does not modify existing exam question rows.
-- Do not use SQLite dump/restore.

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107183041_AddQuestionBank'
)
BEGIN
    ALTER TABLE [Questions] ADD [FigureUrl] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107183041_AddQuestionBank'
)
BEGIN
    ALTER TABLE [Questions] ADD [SourceQnum] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107183041_AddQuestionBank'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[QuestionOptions]') AND [c].[name] = N'OptionText');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [QuestionOptions] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [QuestionOptions] ALTER COLUMN [OptionText] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107183041_AddQuestionBank'
)
BEGIN
    CREATE TABLE [QuestionBank] (
        [Id] int NOT NULL IDENTITY,
        [Qnum] nvarchar(64) NOT NULL,
        [Text] nvarchar(max) NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        [InputKind] nvarchar(20) NOT NULL,
        [CorrectText] nvarchar(500) NULL,
        [DifficultyLevel] nvarchar(20) NULL,
        [Points] int NOT NULL,
        [Grade] nvarchar(8) NULL,
        [Subject] nvarchar(32) NULL,
        [Audience] nvarchar(20) NULL,
        [MainTopic] nvarchar(200) NULL,
        [Language] nvarchar(8) NULL,
        [SituationId] nvarchar(64) NULL,
        [SituationTitle] nvarchar(300) NULL,
        [FigureUrl] nvarchar(1000) NULL,
        CONSTRAINT [PK_QuestionBank] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107183041_AddQuestionBank'
)
BEGIN
    CREATE TABLE [QuestionBankOptions] (
        [Id] int NOT NULL IDENTITY,
        [QuestionBankId] int NOT NULL,
        [OptionText] nvarchar(max) NOT NULL,
        [IsCorrect] bit NOT NULL,
        CONSTRAINT [PK_QuestionBankOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_QuestionBankOptions_QuestionBank_QuestionBankId] FOREIGN KEY ([QuestionBankId]) REFERENCES [QuestionBank] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107183041_AddQuestionBank'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QuestionBank_Qnum] ON [QuestionBank] ([Qnum]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107183041_AddQuestionBank'
)
BEGIN
    CREATE INDEX [IX_QuestionBankOptions_QuestionBankId] ON [QuestionBankOptions] ([QuestionBankId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107183041_AddQuestionBank'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107183041_AddQuestionBank', N'9.0.0');
END;

COMMIT;
GO


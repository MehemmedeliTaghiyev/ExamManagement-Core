-- Teacher sınaq sharing: public / private / group / students.
-- Run in SmarterASP SQL Manager on db_ace530_exam (New Query → paste → Execute).
-- Safe to re-run. Does not print or change passwords. Does not publish the API.
-- Live is ahead of GitHub — port these columns; do not replace the live site from this repo.

IF COL_LENGTH('Exams', 'IsPublic') IS NULL
    ALTER TABLE Exams ADD IsPublic bit NOT NULL CONSTRAINT DF_Exams_IsPublic DEFAULT(0);

IF COL_LENGTH('Exams', 'Visibility') IS NULL
    ALTER TABLE Exams ADD Visibility nvarchar(20) NULL;

IF COL_LENGTH('Exams', 'ShareGroupName') IS NULL
    ALTER TABLE Exams ADD ShareGroupName nvarchar(80) NULL;

IF COL_LENGTH('Exams', 'Topic') IS NULL
    ALTER TABLE Exams ADD Topic nvarchar(200) NULL;

IF OBJECT_ID('ExamAssignees', 'U') IS NULL
BEGIN
    CREATE TABLE ExamAssignees (
        ExamId int NOT NULL,
        StudentId int NOT NULL,
        CONSTRAINT PK_ExamAssignees PRIMARY KEY (ExamId, StudentId)
    );
    CREATE INDEX IX_ExamAssignees_StudentId ON ExamAssignees(StudentId);
END;

IF COL_LENGTH('Exams', 'IsPublic') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Exams_IsPublic' AND object_id = OBJECT_ID('Exams'))
    CREATE INDEX IX_Exams_IsPublic ON Exams(IsPublic);

-- One-topic detection uses existing Questions.SourceQnum → QuestionBank.Qnum → MainTopic.
-- No ALTER on Questions.

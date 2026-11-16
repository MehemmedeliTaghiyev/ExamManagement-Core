-- Student «Müəllim tap» directory columns on Users (SmarterASP SQL Manager).
-- Safe to re-run. Does not print or change passwords.
IF COL_LENGTH('Users', 'Position') IS NULL
    ALTER TABLE Users ADD Position nvarchar(80) NULL;
IF COL_LENGTH('Users', 'TeachingSubject') IS NULL
    ALTER TABLE Users ADD TeachingSubject nvarchar(80) NULL;

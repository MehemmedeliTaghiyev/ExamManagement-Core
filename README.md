# 🎓 ExamStation Web API

**ExamStation** is a modern, scalable RESTful API built with **.NET 9** and **Entity Framework Core** designed for educational platforms and study centers. It handles exam workflows, subject assignments, document/PDF management, and student assessments following Clean Architecture principles.

### 🛠️ Tech Stack
* **Backend:** .NET 9 Web API (C#)
* **ORM:** Entity Framework Core
* **Database:** SQL Server
* **Architecture:** Service Pattern & DTO-driven Data Flow

### QuestionBank (SmarterASP)

Both of these are required (neither alone is enough):

1. Run `migrations/AddQuestionBank.sql` in SmarterASP SQL Manager on `db_ace530_exam` (New Query → paste → Execute). Do not dump SQLite onto SQL Server. Keep the DB password in the panel connection string only.
2. Publish / redeploy this API so `POST /api/question-bank/import` and `POST /api/exams/{id}/from-bank` exist.
3. Grade 10 overlap (after AddQuestionBank): run `migrations/QuestionBankMultiGrade.sql` in the same SQL Manager. It widens `QuestionBank.Grade` for multi-grade tags (`9,10` / `10,11`) and tags matching grade 9/11 rows as **also** grade 10. It never touches exam `Questions` (including copied exams 94/96/97/98).

Empty `Audience` on a bank row is never selected for weekly or teacher papers. Empty `Audience` may be used only when the request is specialty/blok.

`POST /api/exams/{id}/from-bank` replaces previous copies that have `SourceQnum`. Manual questions (no `SourceQnum`) are kept. If the exam already has questions and none of them have `SourceQnum`, the API returns **409** instead of appending duplicates.

Weekly from-bank ignores exam `DifficultyLevel` (asan/orta). Quizzes have no paper difficulty; question difficulties mix inside **one** weekly paper per window. Teacher quizzes may still send `DifficultyLevel` to filter questions.

Situation/isbat items are copied only when audience is specialty (Blok). Grade-9 buraxılış situation is not injected here.

Grade 10 has no native bank papers. Bank chapters that appear on the ExamPulse 10th-grade DİM syllabus (not the grade-11 February calendar) are tagged `10` in addition to `9`/`11`. Mapping is 1:1 on `MainTopic`. Not tagged: Ədədi ardıcıllıqlar. Silsilələr; Koordinatlar üsulu. Hərəkət. Oxşarlıq. Vektorlar; Fırlanma fiqurları, onların səthi və həcmi; Törəmə və onun tətbiqləri; Ardıcıllığın və funksiyanın limiti; İbtidai funksiya və inteqral.

Exam question payloads expose the TikZ PNG as `imageUrl` / `figure` / `figure_path` (same value as `figureUrl`) and embed `<img src>` plus markdown `![figure](url)` in `text`. Situation stem is prefixed from `SituationTitle`.

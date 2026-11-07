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

Empty `Audience` on a bank row is never selected for weekly or teacher papers. Empty `Audience` may be used only when the request is specialty/blok.

`POST /api/exams/{id}/from-bank` replaces previous copies that have `SourceQnum`. Manual questions (no `SourceQnum`) are kept. If the exam already has questions and none of them have `SourceQnum`, the API returns **409** instead of appending duplicates.

Situation/isbat items are copied only when audience is specialty (Blok). Grade-9 buraxılış situation is not injected here.

Exam question payloads expose the TikZ PNG as `imageUrl` / `figure` / `figure_path` (same value as `figureUrl`) and embed `<img src>` plus markdown `![figure](url)` in `text`. Situation stem is prefixed from `SituationTitle`.

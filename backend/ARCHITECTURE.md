# Backend module boundaries

The backend is one ASP.NET Core process and one deployable API. The existing
Domain, Application, Infrastructure and API projects still express the layer
dependencies. Within those layers, code is grouped by business module. This
is a modular monolith; no HTTP call is needed between modules.

| Module | Owns | Public API |
| --- | --- | --- |
| Identity | Users, roles, login, admin account management | Auth and admin user endpoints |
| QuestionPreparation | Question banks, questions, expected key points, rubrics | Question bank, key point and rubric endpoints |
| Viva | Exam sessions, selected question snapshots, student attempts | Exam session endpoints |
| LegacyExams | The original sample exam list | `GET /api/v1/exams` |

## Dependency rules

1. API maps the endpoints of each module. Application code contains use cases
   and business policies. Infrastructure owns PostgreSQL queries and adapters.
2. A module must not query another module's tables from its service or
   repository. It calls a narrow interface in `AiViva.ModuleContracts`.
   `IQuestionPreparationReader` supplies bank, question and readiness facts to
   Viva. `IExamUsageReader` supplies session usage facts to
   QuestionPreparation. The owning module implements each reader.
3. The readers and callers use the same request scope and EF Core context, so
   existing transactions, routes and database data remain valid. These are
   in-process contracts, not separate services or a second database.
4. `QuestionBankDbContext`, configurations and migrations stay in the shared
   Infrastructure persistence folder for this refactor. A later schema
   migration can split persistence ownership if the business model requires
   it. Do not move or regenerate existing migrations merely to add a module.
5. Register new services in their module registration class. Keep
   `Program.cs` limited to application setup and module mapping.

The planned WF1 expansion belongs to QuestionPreparation: subject documents,
AI-generated question drafts, lecturer review, Bloom category and difficulty,
and subject rubrics with rubric-level percentage weights. These features and
their schema changes are separate work after this architecture refactor.

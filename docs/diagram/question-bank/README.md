# MVP Database: Question Banks

## Physical ERD

- Editable source: [Physical ERD.drawio](<Physical ERD.drawio>).
- Current presentation image: [physical-erd.svg](physical-erd.svg), maintained as
  a code-native illustration of the same schema (not an automatic draw.io export).
- [Physical ERD.drawio.png](<Physical ERD.drawio.png>) is the original Issue #12
  export, retained for traceability. It predates the cardinality correction below;
  use the current SVG or export the updated draw.io source for the presentation.
- The original archive remains at [Physical ERD.drawio.zip](<../../Physical ERD.drawio.zip>).
  Despite its extension, it is RAR5. It is preserved unchanged; these extracted
  files avoid requiring teammates to guess its format.

### Review against implementation (Issue #8)

Reviewed against `QuestionBankConfiguration.cs`, `QuestionConfiguration.cs` and
`20261004200441_InitialQuestionBank.cs` in
`backend/src/AiViva.Infrastructure/Persistence/`:

| Item | Result |
| --- | --- |
| Table/column names, UUIDs, string lengths, timestamp types | Match the migration; diagram table labels normalized to lowercase names. |
| Nullability and primary/foreign keys | Match; only `description` permits NULL. |
| Cardinality | Corrected the source's one-or-more endpoint to **zero-or-more**: an empty bank is valid. Each question still requires exactly one bank. |
| Delete behavior | `ON DELETE RESTRICT`; API maps a nonempty-bank delete to HTTP 409. Added to source notes and current image. |
| FK index | `ix_questions_question_bank_id` exists in the migration. Added to source notes and current image. |
| Unique bank name | Not required and not indexed uniquely. |
| Generated values | Application creates UUIDs/UTC timestamps; there are no database-generated defaults for these columns. |

No database migration change was needed for this review. This diagram deliberately
covers only `question_banks` and `questions`, not the whole conceptual system.
Authentication adds `roles` and `users` in `20261004212256_AddAuthUsers`; if that
scope is included in a full-system Physical ERD, it must be added separately.
There is currently **no** `owner_id`/`created_by` FK from banks to users: role-based
access does not imply per-lecturer bank ownership.

### Data dictionary

| Table | Column | PostgreSQL type | Nullability / constraint | Meaning |
|---|---|---|---|---|
| `question_banks` | `id` | `uuid` | PK | Unique identifier for a question bank. |
| `question_banks` | `name` | `varchar(120)` | NOT NULL | Display name of the question bank. Not unique in MVP. |
| `question_banks` | `description` | `varchar(500)` | NULL allowed | Optional description. |
| `question_banks` | `created_at` | `timestamptz` | NOT NULL | Creation timestamp with time zone. |
| `questions` | `id` | `uuid` | PK | Unique identifier for a question. |
| `questions` | `question_bank_id` | `uuid` | NOT NULL, FK → `question_banks.id`, ON DELETE RESTRICT | Parent question bank. |
| `questions` | `content` | `varchar(2000)` | NOT NULL | Question text/content. |
| `questions` | `created_at` | `timestamptz` | NOT NULL | Creation timestamp with time zone. |

### Relationship and implementation notes

- One `question_banks` row can have zero or many `questions` rows.
- Every question belongs to exactly one question bank.
- Create an index on `questions.question_bank_id` to support FK lookups and listing questions by bank.
- The FK uses `ON DELETE RESTRICT`: a bank with dependent questions cannot be deleted; API should return HTTP `409 Conflict`.
- `question_banks.name` is deliberately not unique in the MVP.
- The migration must match this contract, including nullability, types, FK behavior, and index.

## Demo test checklist

Mark each item after running it. Use a fresh test database or clearly identified test records.

### `question_banks` CRUD

- [ ] Create a bank with valid `name` and no description; verify generated/assigned UUID and non-null `created_at`.
- [ ] Create a bank with valid `name` and `description`; verify values persist.
- [ ] Read a bank by ID; verify all fields and timestamp.
- [ ] List banks; verify created records appear.
- [ ] Update `name`; verify persisted value.
- [ ] Update `description` from text to `NULL`; verify NULL is accepted.
- [ ] Delete a bank with no questions; verify deletion succeeds.
- [ ] Create two banks with the same `name`; verify both are accepted in MVP.

### `questions` CRUD

- [ ] Create a question referencing an existing bank; verify UUID, FK value, content, and timestamp.
- [ ] Read a question by ID; verify all fields.
- [ ] List questions for a bank; verify only that bank's questions are returned.
- [ ] Update question `content`; verify persisted value.
- [ ] Delete a question; verify it is removed.
- [ ] Create multiple questions in one bank; verify one-to-many relationship.

### Invalid input and constraint cases

- [ ] Create a bank with missing/NULL `name`; expect validation/database rejection.
- [ ] Create a bank with `name` longer than 120 characters; expect rejection.
- [ ] Create a bank with `description` longer than 500 characters; expect rejection.
- [ ] At the database level, inserting a question with NULL `question_bank_id` is rejected. The HTTP API obtains this FK from `{bankId}` in the nested route, not from the request body.
- [ ] At the database level, a nonexistent `question_bank_id` violates the FK. Through HTTP, creating under a nonexistent `{bankId}` returns the documented 404 without leaking a database exception.
- [ ] Create a question with missing/NULL `content`; expect rejection.
- [ ] Create a question with `content` longer than 2000 characters; expect rejection.
- [ ] Attempt to delete a bank that still has questions; verify deletion is blocked by `RESTRICT` and API returns `409 Conflict`.
- [ ] Read, update, or delete using a malformed UUID; expect validation error, not an unhandled server error.
- [ ] Read, update, or delete a well-formed but nonexistent ID; verify documented not-found behavior.
- [ ] Verify timestamps include timezone semantics and are non-null.
- [ ] Verify database/API does not enforce uniqueness on `question_banks.name` in this MVP.

### Demo evidence

- [ ] Record request/payload, response status, and a brief result for each failed case.
- [ ] Confirm the index exists on `questions.question_bank_id` using the database's index inspection command.
- [ ] Confirm schema and migration match this document before merging.

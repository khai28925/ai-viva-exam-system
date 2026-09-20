# AI Viva Exam System

AI-powered Viva Exam System for the Entrepreneurship Experience Project.

## Overview

The system supports AI-assisted oral examinations (Viva), including question preparation, adaptive follow-up questions, exam monitoring, and feedback/reporting.

## Main Functional Scope

- **Question Bank & Rubric Management** — lecturers prepare question banks, expected key points, and rubrics.
- **AI-powered Viva Exam** — students answer by voice; the system can ask adaptive follow-up or probing questions.
- **Monitoring & Transparency** — lecturers review attempts, transcripts, AI decisions, and activity logs.
- **Feedback & Reporting** — students and lecturers can view exam reports and feedback.

## Planned Technology Stack

- **Frontend:** React
- **Backend:** Spring Boot
- **Database:** PostgreSQL
- **External services:** Speech-to-Text (STT), Large Language Model (LLM), Text-to-Speech (TTS)
- **Backend architecture:** Modular Monolith

## Repository Structure

```text
.
├── backend/
├── frontend/
└── docs/
    ├── requirements/
    ├── workflows/
    ├── use-case/
    ├── erd/
    └── architecture/
```

## Branching Strategy

- `main` — stable versions only.
- `develop` — integration branch for ongoing development.
- `feature/*` — application features.
- `fix/*` — bug fixes.
- `docs/*` — documentation and diagrams.

Create new work branches from `develop` and open Pull Requests back into `develop`. Merge `develop` into `main` only when the integrated version is stable.

### Example

```bash
git checkout develop
git pull origin develop
git checkout -b docs/wf1-swimlane

# make changes

git add .
git commit -m "docs: add WF1 swimlane diagram"
git push -u origin docs/wf1-swimlane
```

Then open a Pull Request from `docs/wf1-swimlane` into `develop`.

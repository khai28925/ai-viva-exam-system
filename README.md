# AI Viva Exam System

AI-powered Viva Exam System for the Entrepreneurship Experience Project.

## Overview

The system supports AI-assisted oral examinations (Viva), including question preparation, adaptive follow-up questions, exam monitoring, and feedback/reporting.

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
    └── diagram/
```

The repository only provides the shared base structure. Team members create their own task branches and add documents, diagrams, or source code as needed.

## Branching Strategy

- `main` — stable versions only.
- `develop` — integration branch for ongoing development.
- `feature/*` — application features.
- `fix/*` — bug fixes.
- `docs/*` — documentation and diagrams.

Each member should create a branch from `develop`, push their work to that branch, then open a Pull Request into `develop`.

```bash
git checkout develop
git pull origin develop
git checkout -b <branch-name>

# make changes

git add .
git commit -m "<commit-message>"
git push -u origin <branch-name>
```

Do not push task changes directly to `main`.

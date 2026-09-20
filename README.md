# AI Viva Exam System

AI-powered Viva Exam System for the Entrepreneurship Experience Project.

## Overview

The system supports AI-assisted oral examinations (Viva), including question preparation, adaptive follow-up/probing questions, exam monitoring, and feedback/reporting.

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

## Branching Strategy

```text
feature/* ──┐
docs/*    ──┼── Pull Request ──> develop ── Pull Request ──> main
fix/*     ──┘
```

- `main` — stable/submission-ready versions only. Do not push directly.
- `develop` — integration branch. Do not push task changes directly.
- `feature/*` — implementation of application features.
- `docs/*` — documentation and diagrams.
- `fix/*` — bug fixes.

Create every task branch from the latest `develop`. Open a Pull Request back into `develop` when the task is complete. Merge `develop` into `main` only through a Pull Request when a stable milestone/release is ready.

### Start a task

```bash
git checkout develop
git pull origin develop
git checkout -b docs/<task-name>
# or: feature/<task-name>
# or: fix/<task-name>
```

### Commit and push

Stage only files belonging to the current task when possible:

```bash
git status
git add <task-files>
git commit -m "docs: add <description>"
git push -u origin <branch-name>
```

Then open a Pull Request:

```text
<task-branch> -> develop
```

Before opening the PR, verify that the branch contains only intended changes:

```bash
git fetch origin
git diff --name-only origin/develop..HEAD
```

### Recommended commit prefixes

- `feat:` new functionality
- `fix:` bug fix
- `docs:` documentation/diagram changes
- `refactor:` code restructuring without behavior change
- `test:` tests
- `chore:` repository/tooling maintenance

## Pull Request Rules

- Use one branch/PR per task.
- Target `develop` for normal work.
- Require review before merging when branch rules are enabled.
- Resolve review conversations before merging.
- Prefer **Squash merge** to keep the integration history clean.
- Delete the task branch after it is merged.
- Never force-push `main` or `develop`.

## Milestone 1 Documents

Store diagrams in `docs/diagram/`. For Milestone 1, use `docs/*` branches such as:

```text
docs/swimlane-diagram
docs/use-case-diagram
docs/conceptual-erd
docs/system-overview
```

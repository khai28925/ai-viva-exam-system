<#
.SYNOPSIS
Verifies authenticated Question Bank CRUD against a disposable PostgreSQL database.
.DESCRIPTION
Requires Docker, the pinned .NET SDK, Node with frontend npm ci completed,
and PowerShell on Windows. Restores/builds
the backend, creates a unique postgres:16-alpine container on a random loopback
port, applies migrations, and tests cookies/CSRF/roles plus CRUD and validation.
Restarts the API to verify real persistence and runs the demo seed twice to
verify idempotency. Runs the real React/HTTP/PostgreSQL integration test.
Stops only its own API and disposable container in finally.
Does not use, reset, migrate or seed the developer's configured database.
.EXAMPLE
powershell -ExecutionPolicy Bypass -File backend/scripts/Test-QuestionBankSmoke.ps1
#>
param()

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Test-AuthSmoke.ps1') -IncludeQuestionBank

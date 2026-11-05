<#
.SYNOPSIS
    Starts the local AIVES stack: PostgreSQL, EF Core migrations, backend API and Vite frontend.

.DESCRIPTION
    Reads the repository-root .env with the same rule as the backend: variables that already
    exist in the environment win. ConnectionStrings__QuestionBank must be set (in .env or the
    environment).

    If the database port is not reachable and the host is local, Docker Desktop is started when
    needed, then either the container named by AIVES_DB_CONTAINER is started, or (for port 5432)
    backend/compose.postgres.yml is brought up with the password taken from the connection
    string, so .env stays the single place to configure it.

    The backend and the frontend open in their own windows. Close a window (or press Ctrl+C in
    it) to stop that part. The database keeps running; stop it with
    docker compose -f backend/compose.postgres.yml stop

    First run on an empty database: set AIVES_BOOTSTRAP_ADMIN_EMAIL and
    AIVES_BOOTSTRAP_ADMIN_PASSWORD in the terminal first (see backend/README.md).

.EXAMPLE
    .\start-dev.cmd

.EXAMPLE
    .\start-dev.cmd -SkipMigrations -NoBrowser
#>
[CmdletBinding()]
param(
    # Skip "dotnet ef database update" (faster when the schema is known to be current).
    [switch]$SkipMigrations,
    # Do not open the browser when the app is ready.
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $root 'backend'
$frontend = Join-Path $root 'frontend'
$composeFile = Join-Path $backend 'compose.postgres.yml'
$backendUrl = 'http://localhost:5065'
$frontendUrl = 'http://localhost:5173'

function Write-Step([string]$Message) { Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Note([string]$Message) { Write-Host "    $Message" -ForegroundColor Gray }
function Write-Warn([string]$Message) { Write-Host "    ! $Message" -ForegroundColor Yellow }
function Stop-Script([string]$Message) {
    Write-Host "    x $Message" -ForegroundColor Red
    exit 1
}

# Runs a command line through cmd so native stderr never becomes a PowerShell error record.
function Test-CommandSucceeds([string]$CommandLine) {
    cmd /c "$CommandLine >nul 2>&1"
    return $LASTEXITCODE -eq 0
}

# Same rules as the backend's LocalEnvironment: a line it rejects would stop the API at startup,
# so such lines are reported here (by number only: a line may hold a secret).
function Import-DotEnv([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $false }
    $lineNumber = 0
    $invalid = New-Object System.Collections.Generic.List[int]
    foreach ($raw in Get-Content -LiteralPath $Path -Encoding UTF8) {
        $lineNumber++
        $line = $raw.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith('#')) { continue }
        if ($line.StartsWith('export ')) { $line = $line.Substring(7).TrimStart() }
        $separator = $line.IndexOf('=')
        $key = if ($separator -ge 1) { $line.Substring(0, $separator).Trim() } else { '' }
        if ($key -cnotmatch '^[A-Za-z_][A-Za-z0-9_]*$') { $invalid.Add($lineNumber); continue }
        $value = $line.Substring($separator + 1).Trim()
        if ($value.Length -ge 1 -and ($value[0] -eq '"' -or $value[0] -eq "'")) {
            $closing = $value.LastIndexOf($value[0])
            $trailing = if ($closing -gt 0) { $value.Substring($closing + 1).Trim() } else { '' }
            if ($closing -le 0 -or ($trailing.Length -gt 0 -and -not $trailing.StartsWith('#'))) { $invalid.Add($lineNumber); continue }
            $value = $value.Substring(1, $closing - 1)
        }
        else {
            $comment = $value.IndexOf(' #')
            if ($comment -ge 0) { $value = $value.Substring(0, $comment).TrimEnd() }
        }
        if ($null -eq [Environment]::GetEnvironmentVariable($key)) {
            [Environment]::SetEnvironmentVariable($key, $value)
        }
    }
    if ($invalid.Count -gt 0) {
        Stop-Script ("Invalid .env line(s): $($invalid -join ', '). The backend refuses to start with them. " +
            'Each line must be KEY=value, empty, or start with #.')
    }
    return $true
}

function Start-DockerIfNeeded {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Stop-Script 'PostgreSQL is not running and Docker is not installed. Start PostgreSQL manually (docs/database-setup.md).'
    }
    if (Test-CommandSucceeds 'docker info') { return }
    $desktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
    if (-not (Test-Path -LiteralPath $desktop)) { Stop-Script 'Docker is not running. Start Docker, then run this script again.' }
    Write-Note 'Starting Docker Desktop (this can take a minute)...'
    Start-Process -FilePath $desktop
    if (-not (Wait-Until { Test-CommandSucceeds 'docker info' } 180 3)) {
        Stop-Script 'Docker did not become ready within 3 minutes. Open Docker Desktop, check for errors, and retry.'
    }
}

function Get-ConnectionValue([string]$ConnectionString, [string[]]$Names) {
    foreach ($part in $ConnectionString.Split(';')) {
        $pair = $part.Split([char[]]'=', 2)
        if ($pair.Count -eq 2 -and $Names -contains $pair[0].Trim()) {
            return $pair[1].Trim().Trim('"', "'")
        }
    }
    return $null
}

function Test-TcpPort([string]$HostName, [int]$Port) {
    $client = New-Object System.Net.Sockets.TcpClient
    try { return $client.ConnectAsync($HostName, $Port).Wait(1500) -and $client.Connected }
    catch { return $false }
    finally { $client.Dispose() }
}

function Get-PortOwner([int]$Port) {
    $listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $listener) { return $null }
    $process = Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue
    if ($null -eq $process) { return "PID $($listener.OwningProcess)" }
    return "$($process.ProcessName) (PID $($process.Id))"
}

function Wait-Until([scriptblock]$Condition, [int]$TimeoutSeconds, [int]$IntervalSeconds = 2) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (& $Condition) { return $true }
        Start-Sleep -Seconds $IntervalSeconds
    }
    return $false
}

function Test-HttpOk([string]$Url) {
    try { return (Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3).StatusCode -eq 200 }
    catch { return $false }
}

# Opens a new PowerShell window; -EncodedCommand avoids every quoting problem in paths and commands.
# The window inherits this process's environment, including the values loaded from .env.
function Start-DevWindow([string]$Title, [string]$WorkingDirectory, [string]$Command) {
    $directory = $WorkingDirectory.Replace("'", "''")
    $script = "`$Host.UI.RawUI.WindowTitle = '$Title'; Set-Location -LiteralPath '$directory'; $Command"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
    Start-Process powershell.exe -ArgumentList '-NoExit', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encoded
}

# ---------------------------------------------------------------- prerequisites
Write-Step 'Checking tools'
foreach ($tool in 'dotnet', 'npm.cmd') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { Stop-Script "$tool was not found in PATH. See README.md (Yeu cau moi truong)." }
}

Write-Step 'Loading .env'
if (-not (Import-DotEnv (Join-Path $root '.env'))) {
    Stop-Script '.env was not found. Copy .env.example to .env at the repository root and fill it in.'
}
if ([string]::IsNullOrWhiteSpace($env:GEMINI_API_KEY)) {
    Write-Warn 'GEMINI_API_KEY is empty: AI question generation, grading and transcription will return 503.'
}

$connection = $env:ConnectionStrings__QuestionBank
if ([string]::IsNullOrWhiteSpace($connection)) {
    Stop-Script ('ConnectionStrings__QuestionBank is not set. In .env, uncomment the ConnectionStrings__QuestionBank ' +
        'line and put your PostgreSQL password in it (see docs/database-setup.md).')
}
$dbHost = Get-ConnectionValue $connection @('Host', 'Server')
$dbPortText = Get-ConnectionValue $connection @('Port')
if ([string]::IsNullOrWhiteSpace($dbHost)) { $dbHost = 'localhost' }
$dbPort = 5432
if (-not [string]::IsNullOrWhiteSpace($dbPortText)) { $dbPort = [int]$dbPortText }

# ---------------------------------------------------------------- database
Write-Step "Checking PostgreSQL at ${dbHost}:$dbPort"
if (Test-TcpPort $dbHost $dbPort) {
    Write-Note 'PostgreSQL is reachable.'
}
elseif ($dbHost -notin @('localhost', '127.0.0.1')) {
    Stop-Script "PostgreSQL is not reachable at ${dbHost}:$dbPort. Start that server and run this script again."
}
elseif (-not [string]::IsNullOrWhiteSpace($env:AIVES_DB_CONTAINER)) {
    # A database container created outside compose (for example a separate demo database).
    $container = $env:AIVES_DB_CONTAINER.Trim()
    if ($container -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') { Stop-Script 'AIVES_DB_CONTAINER must be a Docker container name.' }
    Start-DockerIfNeeded
    Write-Note "Starting Docker container $container..."
    if (-not (Test-CommandSucceeds "docker start $container")) {
        Stop-Script "Docker container '$container' (AIVES_DB_CONTAINER in .env) was not found or could not start."
    }
    if (-not (Wait-Until { Test-CommandSucceeds "docker exec $container pg_isready -q" } 60)) {
        Stop-Script "PostgreSQL in container '$container' did not become ready within 1 minute."
    }
}
else {
    if ($dbPort -ne 5432) {
        Stop-Script ("PostgreSQL is not reachable at ${dbHost}:$dbPort. Set AIVES_DB_CONTAINER in .env to the Docker " +
            'container that serves this port, or use Port=5432 to let this script run backend/compose.postgres.yml.')
    }
    Start-DockerIfNeeded
    if ([string]::IsNullOrWhiteSpace($env:AIVES_POSTGRES_PASSWORD)) {
        $env:AIVES_POSTGRES_PASSWORD = Get-ConnectionValue $connection @('Password', 'Pwd')
    }
    if ([string]::IsNullOrWhiteSpace($env:AIVES_POSTGRES_PASSWORD)) {
        Stop-Script 'The connection string has no Password; compose.postgres.yml needs it.'
    }
    Write-Note 'Starting backend/compose.postgres.yml...'
    docker compose -f $composeFile up -d --wait
    if ($LASTEXITCODE -ne 0) { Stop-Script 'docker compose could not start PostgreSQL (see the output above).' }
}

# ---------------------------------------------------------------- migrations
if ($SkipMigrations) {
    Write-Step 'Skipping migrations (-SkipMigrations)'
}
else {
    Write-Step 'Applying EF Core migrations'
    Push-Location $backend
    try {
        dotnet tool restore | Out-Null
        if ($LASTEXITCODE -ne 0) { Stop-Script 'dotnet tool restore failed.' }
        dotnet ef database update --project src/AiViva.Infrastructure --startup-project src/AiViva.Infrastructure
        if ($LASTEXITCODE -ne 0) {
            Stop-Script ('Migrations failed. On "password authentication failed": a Docker volume keeps the password ' +
                'it was created with, so the password in .env must match it (docs/database-setup.md).')
        }
    }
    finally { Pop-Location }
}

# ---------------------------------------------------------------- backend and frontend
if (-not (Test-Path -LiteralPath (Join-Path $frontend 'node_modules'))) {
    Write-Step 'Installing frontend dependencies (npm ci)'
    Push-Location $frontend
    try {
        npm.cmd ci
        if ($LASTEXITCODE -ne 0) { Stop-Script 'npm ci failed.' }
    }
    finally { Pop-Location }
}

$startedSomething = $false
foreach ($app in @(
        @{ Name = 'backend'; Port = 5065; Directory = $backend; Command = 'dotnet run --project src/AiViva.Api' },
        @{ Name = 'frontend'; Port = 5173; Directory = $frontend; Command = 'npm.cmd run dev' })) {
    $owner = Get-PortOwner $app.Port
    if ($null -ne $owner) {
        Write-Warn "Port $($app.Port) is already used by $owner; not starting the $($app.Name). Close it to restart with this configuration."
        continue
    }
    Write-Step "Starting the $($app.Name) in a new window"
    Start-DevWindow "AIVES $($app.Name)" $app.Directory $app.Command
    $startedSomething = $true
}

Write-Step 'Waiting for the app to respond'
if (-not (Wait-Until { Test-HttpOk "$backendUrl/api/health" } 180)) {
    Stop-Script 'The backend did not respond within 3 minutes. Check the "AIVES backend" window for errors.'
}
if (-not (Wait-Until { Test-HttpOk $frontendUrl } 60)) {
    Stop-Script 'The frontend did not respond within 1 minute. Check the "AIVES frontend" window for errors.'
}

Write-Host ''
Write-Host 'AIVES is running.' -ForegroundColor Green
Write-Note "App:      $frontendUrl"
Write-Note "API:      $backendUrl  (Swagger: $backendUrl/swagger)"
if ($startedSomething) { Write-Note 'Stop:     close the "AIVES backend" / "AIVES frontend" windows (or Ctrl+C in them).' }
if (-not [string]::IsNullOrWhiteSpace($env:AIVES_DB_CONTAINER)) {
    Write-Note "Database: keeps running; stop it with  docker stop $($env:AIVES_DB_CONTAINER.Trim())"
}
elseif ($dbPort -eq 5432 -and $dbHost -in @('localhost', '127.0.0.1')) {
    Write-Note 'Database: keeps running; stop it with  docker compose -f backend/compose.postgres.yml stop'
}

if (-not $NoBrowser) {
    # Edge/Chrome are needed for the voice interview (speech recognition and recording).
    # msedge is registered under App Paths rather than PATH, so try it and fall back to the default browser.
    try { Start-Process msedge $frontendUrl } catch { Start-Process $frontendUrl }
}

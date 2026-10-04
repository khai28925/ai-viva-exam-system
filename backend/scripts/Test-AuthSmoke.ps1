param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

function New-SmokeSecret {
    $bytes = New-Object byte[] 32
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
        return [Convert]::ToBase64String($bytes)
    }
    finally {
        $generator.Dispose()
    }
}

function Assert-Status {
    param(
        [Parameter(Mandatory)]$Response,
        [Parameter(Mandatory)][int]$Expected,
        [Parameter(Mandatory)][string]$Label
    )

    if ($Response.Status -ne $Expected) {
        throw "$Label expected HTTP $Expected but received HTTP $($Response.Status)."
    }

    Write-Host "[PASS] $Label (HTTP $Expected)"
}

function Invoke-SmokeApi {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        $Body,
        [switch]$IncludeCsrf
    )

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::new($Method),
        "$script:smokeBaseUrl$Path")
    try {
        if ($null -ne $Body) {
            $json = ConvertTo-Json -InputObject $Body -Compress -Depth 5
            $request.Content = [System.Net.Http.StringContent]::new(
                $json,
                [System.Text.Encoding]::UTF8,
                'application/json')
        }

        if ($IncludeCsrf) {
            if ([string]::IsNullOrWhiteSpace($script:smokeCsrfToken)) {
                throw 'CSRF token has not been fetched.'
            }

            $request.Headers.Add('X-CSRF-TOKEN', $script:smokeCsrfToken)
        }

        $response = $script:smokeHttpClient.SendAsync($request).GetAwaiter().GetResult()
        try {
            $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $parsed = if ([string]::IsNullOrWhiteSpace($content)) {
                $null
            }
            else {
                $content | ConvertFrom-Json
            }

            $authCookieHttpOnly = $false
            if ($response.Headers.Contains('Set-Cookie')) {
                $authCookieHttpOnly = [bool](@($response.Headers.GetValues('Set-Cookie') |
                    Where-Object {
                        $_ -match '^aives\.auth=' -and
                        $_ -match '(?i)(^|;)\s*httponly\s*(;|$)'
                    }).Count)
            }

            return [pscustomobject]@{
                Status = [int]$response.StatusCode
                Json = $parsed
                AuthCookieHttpOnly = $authCookieHttpOnly
            }
        }
        finally {
            $response.Dispose()
        }
    }
    finally {
        $request.Dispose()
    }
}

function Get-SmokeCsrfToken {
    $response = Invoke-SmokeApi -Method GET -Path '/api/v1/auth/csrf'
    Assert-Status $response 200 'Get CSRF token'
    if ([string]::IsNullOrWhiteSpace($response.Json.token)) {
        throw 'CSRF endpoint returned no token.'
    }

    $script:smokeCsrfToken = $response.Json.token
}

$backendDir = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$containerName = 'aives-auth-smoke-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$dbPassword = New-SmokeSecret
$adminPassword = New-SmokeSecret
$studentPassword = New-SmokeSecret
$lecturerPassword = New-SmokeSecret
$adminEmail = "admin-$([Guid]::NewGuid().ToString('N'))@example.test"
$studentEmail = "student-$([Guid]::NewGuid().ToString('N'))@example.test"
$lecturerEmail = "lecturer-$([Guid]::NewGuid().ToString('N'))@example.test"

$environmentNames = @(
    'ASPNETCORE_ENVIRONMENT',
    'ASPNETCORE_URLS',
    'ConnectionStrings__QuestionBank',
    'AIVES_BOOTSTRAP_ADMIN_EMAIL',
    'AIVES_BOOTSTRAP_ADMIN_PASSWORD'
)
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $item = Get-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
    $previousEnvironment[$name] = if ($null -eq $item) { $null } else { $item.Value }
}

$containerStarted = $false
$apiProcess = $null
$smokeHttpClient = $null
$smokeHttpHandler = $null
$locationPushed = $false

try {
    Push-Location -LiteralPath $backendDir
    $locationPushed = $true

    Write-Host '[INFO] Starting isolated PostgreSQL container.'
    & docker run --rm -d --name $containerName `
        -e 'POSTGRES_DB=aives' `
        -e 'POSTGRES_USER=aives' `
        -e "POSTGRES_PASSWORD=$dbPassword" `
        -p '127.0.0.1::5432' `
        'postgres:16-alpine' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not start the isolated PostgreSQL container.'
    }
    $containerStarted = $true

    $databaseReady = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & docker exec $containerName pg_isready -U aives -d aives 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $databaseReady = $true
            break
        }

        Start-Sleep -Milliseconds 500
    }
    if (-not $databaseReady) {
        throw 'PostgreSQL did not become ready within 30 seconds.'
    }

    $portOutput = & docker port $containerName '5432/tcp'
    if ($LASTEXITCODE -ne 0 -or $portOutput -notmatch '127\.0\.0\.1:(\d+)$') {
        throw 'Could not determine the temporary PostgreSQL host port.'
    }
    $dbPort = [int]$Matches[1]

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__QuestionBank = "Host=127.0.0.1;Port=$dbPort;Database=aives;Username=aives;Password=$dbPassword"
    $env:AIVES_BOOTSTRAP_ADMIN_EMAIL = $adminEmail
    $env:AIVES_BOOTSTRAP_ADMIN_PASSWORD = $adminPassword

    Write-Host '[INFO] Restoring tools and building backend.'
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }
    & dotnet restore 'AiVivaExamSystem.sln'
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
    & dotnet build 'AiVivaExamSystem.sln' --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

    Write-Host '[INFO] Applying EF Core migrations to the isolated database.'
    & dotnet ef database update --project 'src/AiViva.Infrastructure' `
        --startup-project 'src/AiViva.Infrastructure' --no-build
    if ($LASTEXITCODE -ne 0) { throw 'EF Core migration failed.' }

    $portListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $portListener.Start()
    try {
        $apiPort = $portListener.LocalEndpoint.Port
    }
    finally {
        $portListener.Stop()
    }

    $env:ASPNETCORE_URLS = "http://127.0.0.1:$apiPort"
    $smokeBaseUrl = $env:ASPNETCORE_URLS
    Write-Host '[INFO] Starting API with ASPNETCORE_ENVIRONMENT=Development.'
    $apiProcess = Start-Process -FilePath 'dotnet' `
        -ArgumentList 'src/AiViva.Api/bin/Debug/net10.0/AiViva.Api.dll' `
        -WorkingDirectory $backendDir -WindowStyle Hidden -PassThru

    $smokeHttpHandler = [System.Net.Http.HttpClientHandler]::new()
    $smokeHttpHandler.UseCookies = $true
    $smokeHttpHandler.CookieContainer = [System.Net.CookieContainer]::new()
    $smokeHttpClient = [System.Net.Http.HttpClient]::new($smokeHttpHandler)
    $smokeHttpClient.Timeout = [TimeSpan]::FromSeconds(5)

    $apiReady = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($apiProcess.HasExited) {
            throw 'API process exited before becoming ready.'
        }

        try {
            $health = Invoke-SmokeApi -Method GET -Path '/api/health'
            if ($health.Status -eq 200) {
                $apiReady = $true
                break
            }
        }
        catch [System.Net.Http.HttpRequestException] {
            # The listener is not ready yet.
        }

        Start-Sleep -Milliseconds 500
    }
    if (-not $apiReady) {
        throw 'API did not become ready within 30 seconds.'
    }

    Assert-Status (Invoke-SmokeApi -Method GET -Path '/api/v1/auth/me') 401 'Anonymous /auth/me'
    Assert-Status (Invoke-SmokeApi -Method GET -Path '/api/v1/question-banks') 401 'Anonymous Question Bank'

    Get-SmokeCsrfToken
    $loginBody = @{ email = $adminEmail; password = $adminPassword }
    Assert-Status (Invoke-SmokeApi -Method POST -Path '/api/v1/auth/login' -Body $loginBody) `
        400 'Login without CSRF token'
    Assert-Status (Invoke-SmokeApi -Method POST -Path '/api/v1/auth/login' `
            -Body @{ email = $adminEmail; password = 'wrong-password' } -IncludeCsrf) `
        401 'Login with wrong password'

    $adminLogin = Invoke-SmokeApi -Method POST -Path '/api/v1/auth/login' `
        -Body $loginBody -IncludeCsrf
    Assert-Status $adminLogin 200 'Admin login'
    if (-not $adminLogin.AuthCookieHttpOnly) {
        throw 'Admin login did not set an HttpOnly authentication cookie.'
    }
    Write-Host '[PASS] Authentication cookie is HttpOnly'
    Get-SmokeCsrfToken
    $adminMe = Invoke-SmokeApi -Method GET -Path '/api/v1/auth/me'
    Assert-Status $adminMe 200 'Admin /auth/me'
    if ($adminMe.Json.role -ne 'ADMIN') { throw 'Admin session has the wrong role.' }

    Assert-Status (Invoke-SmokeApi -Method POST -Path '/api/v1/admin/users' `
            -Body @{ email = $studentEmail; password = $studentPassword; role = 'STUDENT' } -IncludeCsrf) `
        201 'Admin creates student'
    Assert-Status (Invoke-SmokeApi -Method POST -Path '/api/v1/admin/users' `
            -Body @{ email = $lecturerEmail; password = $lecturerPassword; role = 'LECTURER' } -IncludeCsrf) `
        201 'Admin creates lecturer'
    Assert-Status (Invoke-SmokeApi -Method POST -Path '/api/v1/admin/users' `
            -Body @{ email = $studentEmail.ToUpperInvariant(); password = $studentPassword; role = 'STUDENT' } -IncludeCsrf) `
        409 'Duplicate email with different case'
    Assert-Status (Invoke-SmokeApi -Method POST -Path '/api/v1/admin/users' `
            -Body @{ email = 'short-password@example.test'; password = 'short'; role = 'STUDENT' } -IncludeCsrf) `
        400 'Reject password shorter than 12 characters'
    Assert-Status (Invoke-SmokeApi -Method POST -Path '/api/v1/admin/users' `
            -Body @{ email = 'second-admin@example.test'; password = $adminPassword; role = 'ADMIN' } -IncludeCsrf) `
        400 'Reject admin account creation via API'

    $studentLogin = Invoke-SmokeApi -Method POST -Path '/api/v1/auth/login' `
        -Body @{ email = $studentEmail; password = $studentPassword } -IncludeCsrf
    Assert-Status $studentLogin 200 'Student login'
    Get-SmokeCsrfToken
    Assert-Status (Invoke-SmokeApi -Method GET -Path '/api/v1/admin/users') 403 'Student denied admin API'
    Assert-Status (Invoke-SmokeApi -Method GET -Path '/api/v1/question-banks') 403 'Student denied Question Bank'

    $lecturerLogin = Invoke-SmokeApi -Method POST -Path '/api/v1/auth/login' `
        -Body @{ email = $lecturerEmail; password = $lecturerPassword } -IncludeCsrf
    Assert-Status $lecturerLogin 200 'Lecturer login'
    Get-SmokeCsrfToken
    Assert-Status (Invoke-SmokeApi -Method GET -Path '/api/v1/admin/users') 403 'Lecturer denied admin API'
    Assert-Status (Invoke-SmokeApi -Method GET -Path '/api/v1/question-banks') 200 'Lecturer reads Question Bank'
    Assert-Status (Invoke-SmokeApi -Method POST -Path '/api/v1/question-banks' `
            -Body @{ name = 'Auth smoke bank'; description = 'Temporary test data' } -IncludeCsrf) `
        201 'Lecturer creates Question Bank'

    Assert-Status (Invoke-SmokeApi -Method POST -Path '/api/v1/auth/logout' -IncludeCsrf) `
        204 'Logout'
    Assert-Status (Invoke-SmokeApi -Method GET -Path '/api/v1/auth/me') 401 'After logout /auth/me'
    Get-SmokeCsrfToken
    Assert-Status (Invoke-SmokeApi -Method POST -Path '/api/v1/auth/login' `
            -Body $loginBody -IncludeCsrf) 429 'Sixth login blocked by rate limiter'

    Write-Host '[PASS] Auth HTTP smoke completed.'
}
finally {
    if ($null -ne $smokeHttpClient) { $smokeHttpClient.Dispose() }
    if ($null -ne $smokeHttpHandler) { $smokeHttpHandler.Dispose() }

    if ($null -ne $apiProcess) {
        if (-not $apiProcess.HasExited) {
            Stop-Process -Id $apiProcess.Id -ErrorAction SilentlyContinue
            Wait-Process -Id $apiProcess.Id -Timeout 10 -ErrorAction SilentlyContinue
        }

        $apiProcess.Dispose()
    }

    if ($containerStarted) {
        & docker stop $containerName 2>$null | Out-Null
    }

    foreach ($name in $environmentNames) {
        if ($null -eq $previousEnvironment[$name]) {
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -LiteralPath "Env:$name" -Value $previousEnvironment[$name]
        }
    }

    if ($locationPushed) { Pop-Location }
    Write-Host '[INFO] Smoke process and temporary PostgreSQL container cleaned up.'
}

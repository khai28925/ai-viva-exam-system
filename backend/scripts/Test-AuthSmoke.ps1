<#
.SYNOPSIS
Runs authenticated API checks against a disposable local PostgreSQL container.
.PARAMETER IncludeQuestionBank
Also runs the Question Bank CRUD/persistence checks and verifies demo seeding.
#>
param([switch]$IncludeQuestionBank)

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
                Location = $response.Headers.Location
                ContentType = $response.Content.Headers.ContentType.MediaType
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

function Start-SmokeApi {
    if ($null -ne $script:apiProcess) {
        if (-not $script:apiProcess.HasExited) {
            Stop-Process -Id $script:apiProcess.Id
            Wait-Process -Id $script:apiProcess.Id -Timeout 10 -ErrorAction SilentlyContinue
        }
        $script:apiProcess.Dispose()
    }

    $script:apiProcess = Start-Process -FilePath 'dotnet' `
        -ArgumentList 'src/AiViva.Api/bin/Debug/net10.0/AiViva.Api.dll' `
        -WorkingDirectory $backendDir -WindowStyle Hidden -PassThru

    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($script:apiProcess.HasExited) {
            throw 'API process exited before becoming ready.'
        }
        try {
            $health = Invoke-SmokeApi -Method GET -Path '/api/health'
            if ($health.Status -eq 200) { return }
        }
        catch [System.Net.Http.HttpRequestException] {
            # The listener is not ready yet.
        }
        Start-Sleep -Milliseconds 500
    }
    throw 'API did not become ready within 30 seconds.'
}

function Invoke-QuestionBankSmoke {
    $swagger = Invoke-SmokeApi -Method GET -Path '/swagger/v1/swagger.json'
    Assert-Status $swagger 200 'Read OpenAPI document'
    $requestSchemas = @{
        CreateQuestionBankRequest = 'name'
        UpdateQuestionBankRequest = 'name'
        CreateQuestionRequest = 'content'
        UpdateQuestionRequest = 'content'
    }
    foreach ($schemaName in $requestSchemas.Keys) {
        $schema = $swagger.Json.components.schemas.$schemaName
        $fieldName = $requestSchemas[$schemaName]
        if ($null -eq $schema.properties.$fieldName -or
            $schema.properties.$fieldName.readOnly -eq $true -or
            $schema.required -notcontains $fieldName) {
            throw "OpenAPI $schemaName must document '$fieldName' as required and writable."
        }
        Write-Host "[PASS] OpenAPI $schemaName required/writable $fieldName"
    }
    # The auth checks intentionally exhausted the limiter. Restart the same API,
    # retaining the isolated database, before the separate CRUD scenario.
    Start-SmokeApi
    Get-SmokeCsrfToken
    $lecturerCredentials = @{ email = $lecturerEmail; password = $lecturerPassword }
    Assert-Status (Invoke-SmokeApi -Method POST -Path '/api/v1/auth/login' `
            -Body $lecturerCredentials -IncludeCsrf) 200 'CRUD lecturer login'
    Get-SmokeCsrfToken

    $banksPath = '/api/v1/question-banks'
    $create = Invoke-SmokeApi -Method POST -Path $banksPath `
        -Body @{ name = '  CRUD smoke bank  '; description = 'Temporary CRUD test' } -IncludeCsrf
    Assert-Status $create 201 'Create bank'
    $bankPath = "$banksPath/$($create.Json.id)"
    if ($create.Json.name -ne 'CRUD smoke bank' -or [string]$create.Location -ne $bankPath) {
        throw 'Create bank did not trim the name or return the expected Location.'
    }
    $readBank = Invoke-SmokeApi -Method GET -Path $bankPath
    Assert-Status $readBank 200 'Read bank'
    # PostgreSQL stores microseconds; compare two persisted representations,
    # not the initial .NET create response which can contain 100ns precision.
    $createdAt = $readBank.Json.createdAt

    $update = Invoke-SmokeApi -Method PUT -Path $bankPath `
        -Body @{ name = 'CRUD smoke bank updated'; description = '   ' } -IncludeCsrf
    Assert-Status $update 200 'Update bank'
    if ($update.Json.description -ne $null) { throw 'Bank update must normalize a blank description to null.' }
    if ($update.Json.createdAt -ne $createdAt) { throw 'Bank update must preserve the persisted createdAt.' }

    $invalidBank = Invoke-SmokeApi -Method POST -Path $banksPath `
        -Body @{ name = '   ' } -IncludeCsrf
    Assert-Status $invalidBank 400 'Reject blank bank name'
    if ($invalidBank.ContentType -ne 'application/problem+json' -or $null -eq $invalidBank.Json.errors) {
        throw 'Bank validation did not return ValidationProblemDetails.'
    }
    Assert-Status (Invoke-SmokeApi -Method PUT -Path $bankPath `
            -Body @{ name = ('x' * 121) } -IncludeCsrf) 400 'Reject bank name over 120 characters'
    Assert-Status (Invoke-SmokeApi -Method PUT -Path $bankPath `
            -Body @{ name = 'Valid name'; description = ('x' * 501) } -IncludeCsrf) `
        400 'Reject bank description over 500 characters'
    $boundaryBank = Invoke-SmokeApi -Method POST -Path $banksPath `
        -Body @{ name = ('  ' + ('x' * 120) + '  '); description = ('  ' + ('x' * 500) + '  ') } -IncludeCsrf
    Assert-Status $boundaryBank 201 'Validate bank lengths after trimming'
    if ($boundaryBank.Json.name.Length -ne 120 -or $boundaryBank.Json.description.Length -ne 500) {
        throw 'Boundary bank fields were not trimmed.'
    }
    Assert-Status (Invoke-SmokeApi -Method DELETE -Path "$banksPath/$($boundaryBank.Json.id)" -IncludeCsrf) `
        204 'Delete boundary bank'
    Assert-Status (Invoke-SmokeApi -Method POST -Path $banksPath `
            -Body @{ name = 'No CSRF bank' }) 400 'Reject bank mutation without CSRF'

    $questionsPath = "$bankPath/questions"
    $empty = Invoke-SmokeApi -Method GET -Path $questionsPath
    Assert-Status $empty 200 'Read empty question list'
    if (@($empty.Json).Count -ne 0) { throw 'New bank should have no questions.' }
    $question = Invoke-SmokeApi -Method POST -Path $questionsPath `
        -Body @{ content = '  Explain dependency injection.  ' } -IncludeCsrf
    Assert-Status $question 201 'Create question'
    $questionPath = "$questionsPath/$($question.Json.id)"
    if ($question.Json.content -ne 'Explain dependency injection.' -or
        $question.Json.questionBankId -ne $create.Json.id -or
        [string]$question.Location -ne $questionPath) {
        throw 'Create question returned the wrong content, parent or Location.'
    }
    $readQuestion = Invoke-SmokeApi -Method GET -Path $questionPath
    Assert-Status $readQuestion 200 'Read question'
    $updatedQuestion = Invoke-SmokeApi -Method PUT -Path $questionPath `
        -Body @{ content = 'Explain dependency inversion with an example.' } -IncludeCsrf
    Assert-Status $updatedQuestion 200 'Update question'
    if ($updatedQuestion.Json.createdAt -ne $readQuestion.Json.createdAt) {
        throw 'Question update must preserve createdAt.'
    }
    Assert-Status (Invoke-SmokeApi -Method POST -Path $questionsPath `
            -Body @{ content = '   ' } -IncludeCsrf) 400 'Reject blank question'
    Assert-Status (Invoke-SmokeApi -Method PUT -Path $questionPath `
            -Body @{ content = ('x' * 2001) } -IncludeCsrf) 400 'Reject question over 2000 characters'
    $boundaryQuestion = Invoke-SmokeApi -Method POST -Path $questionsPath `
        -Body @{ content = ('  ' + ('x' * 2000) + '  ') } -IncludeCsrf
    Assert-Status $boundaryQuestion 201 'Validate question length after trimming'
    if ($boundaryQuestion.Json.content.Length -ne 2000) { throw 'Boundary question was not trimmed.' }
    Assert-Status (Invoke-SmokeApi -Method DELETE -Path "$questionsPath/$($boundaryQuestion.Json.id)" -IncludeCsrf) `
        204 'Delete boundary question'
    $nonempty = Invoke-SmokeApi -Method DELETE -Path $bankPath -IncludeCsrf
    Assert-Status $nonempty 409 'Reject deleting nonempty bank'
    if ($nonempty.ContentType -ne 'application/problem+json') {
        throw 'Conflict did not return ProblemDetails.'
    }

    $otherBank = Invoke-SmokeApi -Method POST -Path $banksPath `
        -Body @{ name = 'CRUD other bank' } -IncludeCsrf
    Assert-Status $otherBank 201 'Create other bank'
    $otherPath = "$banksPath/$($otherBank.Json.id)"
    $wrongParent = "$otherPath/questions/$($question.Json.id)"
    Assert-Status (Invoke-SmokeApi -Method GET -Path $wrongParent) 404 'Cannot read question through another bank'
    Assert-Status (Invoke-SmokeApi -Method PUT -Path $wrongParent `
            -Body @{ content = 'Must not change original' } -IncludeCsrf) 404 'Cannot update question through another bank'
    Assert-Status (Invoke-SmokeApi -Method DELETE -Path $wrongParent -IncludeCsrf) `
        404 'Cannot delete question through another bank'
    $missingBank = "$banksPath/$([Guid]::NewGuid())"
    Assert-Status (Invoke-SmokeApi -Method GET -Path "$banksPath/not-a-uuid") 400 'Malformed bank ID'
    Assert-Status (Invoke-SmokeApi -Method GET -Path "$questionsPath/not-a-uuid") 400 'Malformed question ID'
    Assert-Status (Invoke-SmokeApi -Method GET -Path $missingBank) 404 'Missing bank'
    Assert-Status (Invoke-SmokeApi -Method PUT -Path $missingBank `
            -Body @{ name = 'Missing bank update' } -IncludeCsrf) 404 'Cannot update missing bank'
    Assert-Status (Invoke-SmokeApi -Method DELETE -Path $missingBank -IncludeCsrf) 404 'Cannot delete missing bank'
    Assert-Status (Invoke-SmokeApi -Method GET -Path "$missingBank/questions") 404 'Missing parent bank'
    Assert-Status (Invoke-SmokeApi -Method POST -Path "$missingBank/questions" `
            -Body @{ content = 'Must not create orphan' } -IncludeCsrf) 404 'Reject orphan question'
    $missingQuestion = "$questionsPath/$([Guid]::NewGuid())"
    Assert-Status (Invoke-SmokeApi -Method GET -Path $missingQuestion) 404 'Missing question'
    Assert-Status (Invoke-SmokeApi -Method PUT -Path $missingQuestion `
            -Body @{ content = 'Missing question update' } -IncludeCsrf) 404 'Cannot update missing question'
    Assert-Status (Invoke-SmokeApi -Method DELETE -Path $missingQuestion -IncludeCsrf) 404 'Cannot delete missing question'

    Write-Host '[INFO] Restarting API while keeping the same PostgreSQL container.'
    Start-SmokeApi
    Get-SmokeCsrfToken
    Assert-Status (Invoke-SmokeApi -Method POST -Path '/api/v1/auth/login' `
            -Body $lecturerCredentials -IncludeCsrf) 200 'Lecturer login after API restart'
    Get-SmokeCsrfToken
    $persistedBank = Invoke-SmokeApi -Method GET -Path $bankPath
    $persistedQuestion = Invoke-SmokeApi -Method GET -Path $questionPath
    Assert-Status $persistedBank 200 'Read persisted bank after API restart'
    Assert-Status $persistedQuestion 200 'Read persisted question after API restart'
    if ($persistedBank.Json.name -ne 'CRUD smoke bank updated' -or
        $persistedQuestion.Json.content -ne 'Explain dependency inversion with an example.') {
        throw 'Updates were not persisted to PostgreSQL across the API restart.'
    }

    Assert-Status (Invoke-SmokeApi -Method DELETE -Path $questionPath -IncludeCsrf) 204 'Delete question'
    Assert-Status (Invoke-SmokeApi -Method GET -Path $questionPath) 404 'Deleted question is absent'
    Assert-Status (Invoke-SmokeApi -Method DELETE -Path $bankPath -IncludeCsrf) 204 'Delete now-empty bank'
    Assert-Status (Invoke-SmokeApi -Method GET -Path $bankPath) 404 'Deleted bank is absent'
    Assert-Status (Invoke-SmokeApi -Method DELETE -Path $otherPath -IncludeCsrf) 204 'Delete other empty bank'

    $seedPassword = ConvertTo-SecureString -String $lecturerPassword -AsPlainText -Force
    try {
        $seedScript = Join-Path $PSScriptRoot 'Seed-QuestionBankDemo.ps1'
        & $seedScript -BaseUrl $smokeBaseUrl -Email $lecturerEmail -Password $seedPassword -Confirm:$false
        $firstSeed = Invoke-SmokeApi -Method GET -Path $banksPath
        Assert-Status $firstSeed 200 'Read banks after first seed'
        & $seedScript -BaseUrl $smokeBaseUrl -Email $lecturerEmail -Password $seedPassword -Confirm:$false
        $secondSeed = Invoke-SmokeApi -Method GET -Path $banksPath
        Assert-Status $secondSeed 200 'Read banks after second seed'
        if (@($firstSeed.Json).Count -ne @($secondSeed.Json).Count) {
            throw 'Running demo seed twice created duplicate banks.'
        }
        $demoBanks = @($secondSeed.Json | Where-Object {
                $_.description -and $_.description.Contains('[seed:issue-8:v1]')
            })
        if ($demoBanks.Count -ne 2) { throw 'Demo seed must contain exactly two banks.' }
        foreach ($demoBank in $demoBanks) {
            $demoQuestions = Invoke-SmokeApi -Method GET -Path "$banksPath/$($demoBank.id)/questions"
            Assert-Status $demoQuestions 200 'Read seeded questions'
            if (@($demoQuestions.Json).Count -ne 3) {
                throw 'Running demo seed twice must retain three questions per demo bank.'
            }
        }
        Write-Host '[PASS] Demo seed is idempotent (two banks, six questions).'
    }
    finally {
        $seedPassword.Dispose()
    }
    $frontendEnvironment = @(
        'VITE_API_BASE_URL', 'AIVES_LIVE_CRUD', 'AIVES_TEST_EMAIL', 'AIVES_TEST_PASSWORD'
    )
    $savedFrontendEnvironment = @{}
    foreach ($name in $frontendEnvironment) {
        $item = Get-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        $savedFrontendEnvironment[$name] = if ($null -eq $item) { $null } else { $item.Value }
    }
    $frontendLocationPushed = $false
    try {
        $env:VITE_API_BASE_URL = "$smokeBaseUrl/api"
        $env:AIVES_LIVE_CRUD = '1'
        $env:AIVES_TEST_EMAIL = $lecturerEmail
        $env:AIVES_TEST_PASSWORD = $lecturerPassword
        $frontendDir = Join-Path $backendDir '../frontend'
        Push-Location -LiteralPath $frontendDir
        $frontendLocationPushed = $true
        Write-Host '[INFO] Running real React -> HTTP API -> PostgreSQL CRUD integration.'
        & npm.cmd test -- --run 'src/features/question-bank/QuestionBankLive.test.jsx'
        if ($LASTEXITCODE -ne 0) { throw 'Live React CRUD integration test failed.' }
    }
    finally {
        if ($frontendLocationPushed) { Pop-Location }
        foreach ($name in $frontendEnvironment) {
            if ($null -eq $savedFrontendEnvironment[$name]) {
                Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
            }
            else {
                Set-Item -LiteralPath "Env:$name" -Value $savedFrontendEnvironment[$name]
            }
        }
    }
    Write-Host '[PASS] Question Bank HTTP/PostgreSQL smoke completed.'
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
    'DOTNET_ENVIRONMENT',
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
    $env:DOTNET_ENVIRONMENT = 'Development'
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

    $smokeHttpHandler = [System.Net.Http.HttpClientHandler]::new()
    $smokeHttpHandler.UseCookies = $true
    $smokeHttpHandler.CookieContainer = [System.Net.CookieContainer]::new()
    $smokeHttpClient = [System.Net.Http.HttpClient]::new($smokeHttpHandler)
    $smokeHttpClient.Timeout = [TimeSpan]::FromSeconds(5)

    Start-SmokeApi

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
    if ($IncludeQuestionBank) { Invoke-QuestionBankSmoke }
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
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Could not stop temporary container '$containerName'; inspect Docker and remove only this smoke container."
        }
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
    Write-Host '[INFO] Smoke cleanup finished; any cleanup failure is reported above.'
}

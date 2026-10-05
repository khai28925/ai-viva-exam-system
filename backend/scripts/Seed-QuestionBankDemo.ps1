<#
.SYNOPSIS
Adds two demo Question Banks and six questions to an explicitly chosen local API.
.DESCRIPTION
Opt-in local demo setup; never invoked at application startup. Requires an
existing ADMIN or LECTURER account. Prompts for email and a hidden password when
not supplied; does not create users or persist credentials. Requests a cookie
session and CSRF token, then creates only absent seed banks/questions through
the normal API. Running again with unchanged seed data does not duplicate it.
Never updates or deletes existing data. Fails rather than repurposing an
existing bank with the same name but without the expected seed description.
Stops if the API URL is not loopback. Redirects are disabled so credentials
cannot be forwarded to another host. Confirm the target database yourself from
the API configuration before proceeding; the script only knows its API URL.
.PARAMETER BaseUrl
Explicit API origin, e.g. http://localhost:5065 (without /api).
.PARAMETER Email
Email of an existing ADMIN or LECTURER. Omit to be prompted.
.PARAMETER Password
Optional SecureString password; omit to be prompted securely. Do not put a
plaintext password into a shell command or committed file.
.EXAMPLE
powershell -ExecutionPolicy Bypass -File backend/scripts/Seed-QuestionBankDemo.ps1 -BaseUrl http://localhost:5065
.EXAMPLE
./backend/scripts/Seed-QuestionBankDemo.ps1 -BaseUrl http://localhost:5065 -Email lecturer@example.test -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)][Uri]$BaseUrl,
    [string]$Email,
    [System.Security.SecureString]$Password
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

if (-not $BaseUrl.IsAbsoluteUri -or $BaseUrl.Scheme -notin @('http', 'https') -or
    -not $BaseUrl.IsLoopback -or $BaseUrl.AbsolutePath -ne '/' -or
    $BaseUrl.UserInfo -or $BaseUrl.Query -or $BaseUrl.Fragment) {
    throw 'BaseUrl must be an explicit loopback HTTP(S) origin without credentials, path, query or fragment.'
}

$seedOrigin = $BaseUrl.GetLeftPart([UriPartial]::Authority)
if (-not $PSCmdlet.ShouldProcess($seedOrigin, 'Add demo banks/questions to this API database (no updates/deletions)')) {
    return
}
if ([string]::IsNullOrWhiteSpace($Email)) {
    $Email = Read-Host 'Existing ADMIN or LECTURER email'
}
if ([string]::IsNullOrWhiteSpace($Email)) { throw 'Email is required.' }
$ownsPassword = $null -eq $Password
if ($ownsPassword) { $Password = Read-Host 'Account password' -AsSecureString }

$seedHandler = [System.Net.Http.HttpClientHandler]::new()
$seedHandler.AllowAutoRedirect = $false
$seedHandler.UseCookies = $true
$seedHandler.CookieContainer = [System.Net.CookieContainer]::new()
$seedClient = [System.Net.Http.HttpClient]::new($seedHandler)
$seedClient.Timeout = [TimeSpan]::FromSeconds(15)
$seedCsrf = $null
$seedLoggedIn = $false

function Invoke-SeedApi {
    param([string]$Method, [string]$Path, $Body, [int]$Expected = 200)

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::new($Method), "$seedOrigin$Path")
    try {
        if ($null -ne $Body) {
            $request.Content = [System.Net.Http.StringContent]::new(
                (ConvertTo-Json -InputObject $Body -Compress), [System.Text.Encoding]::UTF8, 'application/json')
        }
        if ($Method -ne 'GET') { $request.Headers.Add('X-CSRF-TOKEN', $seedCsrf) }
        $response = $seedClient.SendAsync($request).GetAwaiter().GetResult()
        try {
            if ([int]$response.StatusCode -ne $Expected) {
                throw "$Method $Path expected HTTP $Expected but received HTTP $([int]$response.StatusCode)."
            }
            $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if (-not [string]::IsNullOrWhiteSpace($content)) { return ($content | ConvertFrom-Json) }
        }
        finally { $response.Dispose() }
    }
    finally { $request.Dispose() }
}

try {
    $seedCsrf = (Invoke-SeedApi -Method GET -Path '/api/v1/auth/csrf').token
    $plainPassword = [System.Net.NetworkCredential]::new('', $Password).Password
    try {
        $session = Invoke-SeedApi -Method POST -Path '/api/v1/auth/login' `
            -Body @{ email = $Email.Trim(); password = $plainPassword }
    }
    finally { $plainPassword = $null }
    $seedLoggedIn = $true
    $seedCsrf = (Invoke-SeedApi -Method GET -Path '/api/v1/auth/csrf').token
    if ($session.role -notin @('ADMIN', 'LECTURER')) {
        throw 'Demo seeding requires an ADMIN or LECTURER account; no demo data was changed.'
    }

    $samples = @(
        @{
            Name = '[AIVES DEMO] Software Architecture'
            Description = '[seed:issue-8:v1] Architecture discussion questions for the CRUD demo.'
            Questions = @(
                'Compare monolithic and microservices architectures. When would you choose each?'
                'Explain dependency inversion using a repository interface and a database adapter.'
                'How does separating application logic from infrastructure improve testability?'
            )
        },
        @{
            Name = '[AIVES DEMO] Object-Oriented Programming'
            Description = '[seed:issue-8:v1] Object-oriented design questions for the CRUD demo.'
            Questions = @(
                'Explain encapsulation with an example from a Question Bank application.'
                'How do abstraction and polymorphism support interchangeable repository implementations?'
                'Describe the Single Responsibility Principle and show one example.'
            )
        }
    )
    $banksPath = '/api/v1/question-banks'
    $existingBanks = @(Invoke-SeedApi -Method GET -Path $banksPath)
    $createdBanks = 0
    $createdQuestions = 0
    foreach ($sample in $samples) {
        $matches = @($existingBanks | Where-Object { $_.name -ceq $sample.Name })
        if ($matches.Count -gt 1 -or
            ($matches.Count -eq 1 -and $matches[0].description -cne $sample.Description)) {
            throw "Ambiguous or non-seed bank named '$($sample.Name)'. No existing bank will be changed."
        }
        if ($matches.Count -eq 0) {
            $bank = Invoke-SeedApi -Method POST -Path $banksPath -Expected 201 `
                -Body @{ name = $sample.Name; description = $sample.Description }
            $createdBanks++
        }
        else { $bank = $matches[0] }
        $questionsPath = "$banksPath/$($bank.id)/questions"
        $existingQuestions = @(Invoke-SeedApi -Method GET -Path $questionsPath)
        foreach ($content in $sample.Questions) {
            if (@($existingQuestions | Where-Object { $_.content -ceq $content }).Count -eq 0) {
                Invoke-SeedApi -Method POST -Path $questionsPath -Expected 201 `
                    -Body @{ content = $content } | Out-Null
                $createdQuestions++
            }
        }
        Write-Host "[OK] $($sample.Name)"
    }
    Write-Host "[DONE] Created $createdBanks bank(s), $createdQuestions question(s). Existing data was retained."
}
finally {
    if ($seedLoggedIn -and $seedCsrf) {
        try { Invoke-SeedApi -Method POST -Path '/api/v1/auth/logout' -Expected 204 | Out-Null }
        catch { Write-Warning 'Could not log out the temporary seed session; it will expire automatically.' }
    }
    $seedClient.Dispose()
    $seedHandler.Dispose()
    if ($ownsPassword -and $null -ne $Password) { $Password.Dispose() }
}

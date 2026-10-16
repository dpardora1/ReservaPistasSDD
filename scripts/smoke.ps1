#requires -Version 7.4
[CmdletBinding()]
param(
    [string] $OutputDirectory,
    [switch] $InjectFailure
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root "artifacts/smoke-$([guid]::NewGuid())" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path $OutputDirectory) { throw 'Smoke output directory must be new to avoid stale evidence.' }
$null = New-Item -ItemType Directory -Path $OutputDirectory
$dll = Join-Path $root 'src/SalaReservas.Api/bin/Release/net10.0/SalaReservas.Api.dll'
$stdout = Join-Path $OutputDirectory 'server.stdout.log'
$stderr = Join-Path $OutputDirectory 'server.stderr.log'
$process = $null
$client = [Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(10)
$report = [ordered]@{ passed = $false; injected = [bool]$InjectFailure; pid = $null; url = $null; stopped = $false; steps = @(); error = $null }

function Request([string] $Method, [string] $Path, [int] $Expected, [object] $Body = $null, [string] $Code = '') {
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method), "$($report.url)$Path")
    $response = $null
    try {
        if ($null -ne $Body) {
            $request.Content = [Net.Http.StringContent]::new(($Body | ConvertTo-Json -Compress), [Text.Encoding]::UTF8, 'application/json')
        }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $status = [int]$response.StatusCode
        $report.steps += "$Method $Path -> $status"
        if ($status -ne $Expected) { throw "Expected $Expected, got ${status}: $text" }
        if ($Expected -eq 204 -and $text.Length -ne 0) { throw '204 must have no body.' }
        if ($Code) {
            if ($response.Content.Headers.ContentType.MediaType -ne 'application/problem+json') { throw 'Expected ProblemDetails.' }
            $problem = $text | ConvertFrom-Json
            if ($problem.code -ne $Code -or $problem.status -ne $Expected -or !$problem.title -or !$problem.detail) { throw "Invalid ProblemDetails: $text" }
        }
        if ($Expected -eq 201) {
            $document = [Text.Json.JsonDocument]::Parse($text)
            try {
                $names = @($document.RootElement.EnumerateObject().Name | Sort-Object)
                if (($names -join ',') -ne 'end,id,roomId,start') { throw 'Creation response shape changed.' }
                $reservation = @{}
                foreach ($name in $names) { $reservation[$name] = $document.RootElement.GetProperty($name).GetString() }
            }
            finally { $document.Dispose() }
            $id = [guid]::Parse($reservation.id)
            if ($id -eq [guid]::Empty -or $reservation.roomId -ne $Body.roomId) { throw 'Invalid created reservation.' }
            foreach ($field in @('start', 'end')) {
                $instant = [DateTimeOffset]::Parse($reservation.$field, [Globalization.CultureInfo]::InvariantCulture)
                $expectedInstant = [DateTimeOffset]::Parse($Body.$field, [Globalization.CultureInfo]::InvariantCulture)
                if ($instant -ne $expectedInstant -or $instant.Offset -ne [TimeSpan]::Zero) { throw 'Reservation dates changed.' }
            }
            return $reservation
        }
    }
    finally {
        if ($response) { $response.Dispose() }
        $request.Dispose()
    }
}

try {
    if (!(Test-Path $dll)) { throw "Build Release before smoke: $dll" }
    $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @("`"$dll`"", '--urls', 'http://127.0.0.1:0') -WorkingDirectory $root -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr -Environment @{
        ASPNETCORE_ENVIRONMENT = 'Production'
        'Logging__LogLevel__Microsoft.Hosting.Lifetime' = 'Information'
        Logging__Console__FormatterOptions__ColorBehavior = 'Disabled'
    }
    $report.pid = $process.Id
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) { throw "Server exited during startup: $($process.ExitCode)" }
        if (Test-Path $stdout) {
            $log = Get-Content $stdout -Raw
            if ($log -match 'Now listening on: (http://127\.0\.0\.1:\d+)') { $report.url = $Matches[1]; break }
        }
        Start-Sleep -Milliseconds 100
    }
    if (!$report.url) { throw 'Server readiness timeout.' }
    Request GET '/' 404
    if ($InjectFailure) { throw 'Intentional assertion failure to verify owned-process cleanup.' }
    $start = [DateTimeOffset]::UtcNow.AddDays(1)
    $body = @{ roomId = 'A'; start = $start.ToString('O'); end = $start.AddHours(1).ToString('O') }
    $first = Request POST '/reservations' 201 $body
    Request POST '/reservations' 409 $body 'reservation_overlap'
    Request DELETE "/reservations/$($first.id)" 204
    Request DELETE "/reservations/$($first.id)" 204
    $replacement = Request POST '/reservations' 201 $body
    if ($replacement.id -eq $first.id) { throw 'Replacement must have a new ID.' }
    Request DELETE "/reservations/$($first.id)" 204
    Request POST '/reservations' 409 $body 'reservation_overlap'
    Request DELETE '/reservations/not-a-guid' 400 $null 'invalid_reservation_id'
    Request DELETE "/reservations/$([guid]::NewGuid())" 404 $null 'reservation_not_found'
    $report.passed = $true
}
catch {
    $report.error = $_.Exception.Message
    throw
}
finally {
    try {
        if ($process) {
            if (!$process.HasExited) { $process.Kill($true) }
            if (!$process.WaitForExit(10000)) { throw 'Owned server did not stop.' }
            $report.stopped = $true
            $process.Dispose()
        }
    }
    catch {
        $report.passed = $false
        $report.error = "Cleanup failed: $($_.Exception.Message)"
        throw
    }
    finally {
        $client.Dispose()
        $report | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDirectory 'smoke.json') -Encoding utf8
        Write-Host "Smoke evidence: $OutputDirectory"
    }
}

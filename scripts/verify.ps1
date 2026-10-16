#requires -Version 7.4
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root "artifacts/verify-$([guid]::NewGuid())"
$null = New-Item -ItemType Directory -Path $output -Force
Push-Location $root
Start-Transcript -Path (Join-Path $output 'verify.log') | Out-Null
function Invoke-Dotnet([string[]] $Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed with $LASTEXITCODE" }
}
try {
    $sdk = (Get-Content (Join-Path $root 'global.json') -Raw | ConvertFrom-Json).sdk.version
    $actual = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $actual.Trim() -ne $sdk) { throw "Expected SDK $sdk; got $actual" }
    Invoke-Dotnet @('--info')
    Invoke-Dotnet @('--list-runtimes')
    Write-Host "GitHub head=$env:GITHUB_SHA ref=$env:GITHUB_REF run=$env:GITHUB_RUN_ID attempt=$env:GITHUB_RUN_ATTEMPT"
    & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify tested commit.' }
    Invoke-Dotnet @('restore', 'SalaReservas.slnx', '--locked-mode')
    Invoke-Dotnet @('build', 'SalaReservas.slnx', '-c', 'Release', '--no-restore', '--warnaserror')
    Invoke-Dotnet @('test', 'SalaReservas.slnx', '-c', 'Release', '--no-build', '--no-restore', '--logger', 'trx', '--collect', 'XPlat Code Coverage', '--results-directory', (Join-Path $output 'tests'))
    $trxFiles = @(Get-ChildItem (Join-Path $output 'tests') -Recurse -Filter '*.trx')
    $coverage = @(Get-ChildItem (Join-Path $output 'tests') -Recurse -Filter 'coverage.cobertura.xml')
    if ($trxFiles.Count -eq 0 -or $coverage.Count -eq 0) { throw 'Missing TRX or coverage evidence.' }
    foreach ($trx in $trxFiles) {
        [xml] $xml = Get-Content $trx.FullName -Raw
        $counters = $xml.TestRun.ResultSummary.Counters
        if ([int]$counters.total -le 0 -or $counters.total -ne $counters.passed) { throw 'Tests missing, skipped or not passing.' }
    }
    & (Join-Path $PSScriptRoot 'smoke.ps1') -OutputDirectory (Join-Path $output 'smoke')
    # A separate process must fail, but must still stop its owned server.
    $failureOutput = Join-Path $output 'smoke-cleanup-check'
    & (Get-Process -Id $PID).Path -NoLogo -NoProfile -File (Join-Path $PSScriptRoot 'smoke.ps1') -OutputDirectory $failureOutput -InjectFailure *> (Join-Path $output 'cleanup-check.log')
    if ($LASTEXITCODE -eq 0) { throw 'Injected smoke failure unexpectedly succeeded.' }
    $failure = Get-Content (Join-Path $failureOutput 'smoke.json') -Raw | ConvertFrom-Json
    if (!$failure.stopped -or $failure.passed -or $failure.error -notlike 'Intentional assertion failure*') { throw 'Smoke failure cleanup was not verified.' }
    # GitHub's pwsh wrapper propagates LASTEXITCODE; the expected failure was validated above.
    $global:LASTEXITCODE = 0
    Write-Host "Verification PASSED. Reports: $output"
}
finally {
    Stop-Transcript | Out-Null
    Pop-Location
}

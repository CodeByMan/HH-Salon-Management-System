$ErrorActionPreference = 'Stop'

$FrontendRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$ProjectRoot = (Resolve-Path (Join-Path $FrontendRoot '..')).Path

$Candidates = @(
    (Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'),
    (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe')
)

$PathDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($PathDotnet) {
    $Candidates += $PathDotnet.Source
}

$Candidates = $Candidates |
    Where-Object { $_ -and (Test-Path $_) } |
    Select-Object -Unique

$DotnetExe = $null
$DotnetVersion = $null

Push-Location $ProjectRoot
try {
    foreach ($Candidate in $Candidates) {
        try {
            $VersionOutput = & $Candidate --version 2>$null
            if ($LASTEXITCODE -eq 0 -and $VersionOutput) {
                $DotnetExe = $Candidate
                $DotnetVersion = ($VersionOutput | Select-Object -First 1).Trim()
                break
            }
        }
        catch {
            continue
        }
    }
}
finally {
    Pop-Location
}

if (-not $DotnetExe) {
    throw "No installed .NET SDK can satisfy $ProjectRoot\global.json. Expected the user SDK at $env:USERPROFILE\.dotnet\dotnet.exe or a compatible system SDK."
}

$CertDir = Join-Path $env:LOCALAPPDATA 'hhSalon\https'
$CertPem = Join-Path $CertDir 'localhost.pem'
$CertKey = Join-Path $CertDir 'localhost.key'

New-Item -ItemType Directory -Path $CertDir -Force | Out-Null
Remove-Item $CertPem, $CertKey -Force -ErrorAction SilentlyContinue

Push-Location $ProjectRoot
try {
    & $DotnetExe dev-certs https `
        --export-path $CertPem `
        --format PEM `
        --no-password

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet dev-certs failed using $DotnetExe."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path $CertPem) -or -not (Test-Path $CertKey)) {
    throw 'The localhost certificate or private key was not exported successfully.'
}

$Certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($CertPem)
$ExistingRoot = Get-ChildItem Cert:\CurrentUser\Root |
    Where-Object Thumbprint -eq $Certificate.Thumbprint

if (-not $ExistingRoot) {
    Import-Certificate `
        -FilePath $CertPem `
        -CertStoreLocation 'Cert:\CurrentUser\Root' `
        -Confirm:$false | Out-Null
}

Write-Host ''
Write-Host 'Local hhSalon HTTPS is configured for the current Windows user.' -ForegroundColor Green
Write-Host "Using .NET SDK: $DotnetVersion"
Write-Host "dotnet: $DotnetExe"
Write-Host "Certificate: $CertPem"
Write-Host "Private key: $CertKey"
Write-Host 'Close all Chrome/Edge windows before testing so the browser reloads certificate trust.'

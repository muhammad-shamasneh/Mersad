$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Mersad V1 local demo launcher
$Root = "C:\Mersad"
$ApiProject = "$Root\backend\Mersad.Api\Mersad.Api.csproj"
$DashboardDir = "$Root\dashboard\mersad-console"
$ComposeFile = "$Root\infrastructure\docker-compose.yml"
$LogDir = "$env:LOCALAPPDATA\Mersad\logs"
$ApiUrl = "http://localhost:5121"
$DashboardUrl = "http://localhost:5173"

function Assert-CommandSuccess([string]$Step) {
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

function Test-PortBusy([int]$Port) {
    return (@(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue).Count -gt 0)
}

function Wait-ForApi {
    $key = $env:MersadSecurity__DashboardApiKey
    $deadline = (Get-Date).AddSeconds(60)
    do {
        try {
            $null = Invoke-RestMethod -Uri "$ApiUrl/api/alerts" `
                -Headers @{ Authorization = "Bearer $key" } -TimeoutSec 3
            return $true
        } catch {
            Start-Sleep -Seconds 2
        }
    } while ((Get-Date) -lt $deadline)
    return $false
}

function Wait-ForDashboard {
    $deadline = (Get-Date).AddSeconds(60)
    do {
        try {
            $response = Invoke-WebRequest -Uri $DashboardUrl -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -eq 200) { return $true }
        } catch {
            Start-Sleep -Seconds 2
        }
    } while ((Get-Date) -lt $deadline)
    return $false
}

Set-Location -LiteralPath $Root
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

# 1. Load configured keys without displaying them.
foreach ($name in @(
    "MersadSecurity__DashboardApiKey",
    "MersadSecurity__AgentApiKey",
    "MERSAD_AGENT_API_KEY"
)) {
    $value = [Environment]::GetEnvironmentVariable($name, "User")
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Required user environment variable '$name' is missing. No key value is displayed."
    }
    [Environment]::SetEnvironmentVariable($name, $value, "Process")
}
$env:ASPNETCORE_ENVIRONMENT = "Development"

# 2. Identify and validate any existing listeners.
$ApiAlreadyRunning = $false
$DashboardAlreadyRunning = $false
$NodeDir = "C:\Program Files\nodejs"

$apiListeners = @(Get-NetTCPConnection -LocalPort 5121 -State Listen -ErrorAction SilentlyContinue)
foreach ($listener in $apiListeners) {
    $proc = Get-CimInstance Win32_Process -Filter "ProcessId = $($listener.OwningProcess)" -ErrorAction SilentlyContinue
    $expectedExe = "$Root\backend\Mersad.Api\bin\Debug\net10.0\Mersad.Api.exe"
    $valid = $false
    if ($null -ne $proc) {
        if ($proc.ExecutablePath -ieq $expectedExe) { $valid = $true }
        if ($proc.Name -ieq "dotnet.exe" -and $proc.CommandLine -like "*$ApiProject*") { $valid = $true }
    }
    if (-not $valid) { throw "Port 5121 belongs to an unverified process. Nothing was stopped." }
    $ApiAlreadyRunning = $true
}

$dashboardListeners = @(Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue)
foreach ($listener in $dashboardListeners) {
    $proc = Get-CimInstance Win32_Process -Filter "ProcessId = $($listener.OwningProcess)" -ErrorAction SilentlyContinue
    $valid = $false
    if ($null -ne $proc -and $proc.Name -ieq "node.exe" -and $proc.ExecutablePath -ieq "$NodeDir\node.exe" -and $proc.CommandLine -match "vite") {
        $valid = $true
    }
    if (-not $valid) { throw "Port 5173 belongs to an unverified process. Nothing was stopped." }
    $DashboardAlreadyRunning = $true
}

if ($ApiAlreadyRunning -and -not (Wait-ForApi)) {
    throw "An API process occupies port 5121 but authenticated readiness failed. Nothing was stopped."
}
if ($DashboardAlreadyRunning -and -not (Wait-ForDashboard)) {
    throw "A dashboard process occupies port 5173 but HTTP readiness failed. Nothing was stopped."
}

if ($ApiAlreadyRunning -and $DashboardAlreadyRunning) {
    Write-Host "Mersad is already running and both readiness checks passed." -ForegroundColor Green
    Write-Host "Dashboard: $DashboardUrl"
    Write-Host "API:       $ApiUrl"
    Write-Host "Logs:      $LogDir"
    return
}

# Start PostgreSQL and apply migrations only when the API needs starting.
if (-not $ApiAlreadyRunning) {
    Write-Host "`n[1/4] PostgreSQL" -ForegroundColor Cyan
    docker compose -f $ComposeFile up -d
    Assert-CommandSuccess "Docker Compose"

    $dbReady = $false
    for ($i = 0; $i -lt 30; $i++) {
        docker exec mersad-postgres pg_isready -U mersad -d mersad *> $null
        if ($LASTEXITCODE -eq 0) {
            $dbReady = $true
            break
        }
        Start-Sleep -Seconds 2
    }
    if (-not $dbReady) { throw "PostgreSQL did not become ready within 60 seconds." }

    Write-Host "`n[2/4] Database migrations" -ForegroundColor Cyan
    dotnet ef database update --project $ApiProject --startup-project $ApiProject
    Assert-CommandSuccess "Entity Framework database update"

    Write-Host "`n[3/4] API" -ForegroundColor Cyan
    Start-Process -FilePath "dotnet" -ArgumentList @("run", "--no-launch-profile", "--project", $ApiProject, "--urls", $ApiUrl) -WorkingDirectory $Root -RedirectStandardOutput "$LogDir\api.stdout.log" -RedirectStandardError "$LogDir\api.stderr.log" -WindowStyle Hidden | Out-Null
    if (-not (Wait-ForApi)) { throw "API readiness failed. Inspect API logs in '$LogDir'." }
} else {
    Write-Host "Reusing the verified running API." -ForegroundColor Green
}

# Start dashboard only when it is not already running.
if (-not $DashboardAlreadyRunning) {
    Write-Host "`n[4/4] Dashboard" -ForegroundColor Cyan
    $env:PATH = "$NodeDir;$env:PATH"
    Start-Process -FilePath "$env:WINDIR\System32\cmd.exe" -ArgumentList @("/d", "/c", "npm.cmd run dev -- --host localhost") -WorkingDirectory $DashboardDir -RedirectStandardOutput "$LogDir\dashboard.stdout.log" -RedirectStandardError "$LogDir\dashboard.stderr.log" -WindowStyle Hidden | Out-Null
    if (-not (Wait-ForDashboard)) { throw "Dashboard readiness failed. Inspect dashboard logs in '$LogDir'." }
} else {
    Write-Host "Reusing the verified running dashboard." -ForegroundColor Green
}

Write-Host "`nMersad V1 demo started successfully." -ForegroundColor Green
Write-Host "Dashboard: $DashboardUrl"
Write-Host "API:       $ApiUrl"
Write-Host "Logs:      $LogDir"
Write-Host "Sign in with the dashboard API key already configured in your user environment."
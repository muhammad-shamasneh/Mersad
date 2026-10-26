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

# 2. Refuse to interfere with existing listeners.
if ((Test-PortBusy 5121) -or (Test-PortBusy 5173)) {
    if ((Test-PortBusy 5121) -and (Test-PortBusy 5173) -and
        (Wait-ForApi) -and (Wait-ForDashboard)) {
        Write-Host "Mersad is already running and both readiness checks passed." -ForegroundColor Green
        Write-Host "Dashboard: $DashboardUrl"
        Write-Host "API:       $ApiUrl"
        Write-Host "Logs:      $LogDir"
        return
    }
    throw "Port 5121 or 5173 is occupied by an unverified process. Nothing was stopped."
}

# 3. Start PostgreSQL and wait for readiness.
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

# 4. Apply migrations before starting the API.
Write-Host "`n[2/4] Database migrations" -ForegroundColor Cyan
dotnet ef database update --project $ApiProject --startup-project $ApiProject
Assert-CommandSuccess "Entity Framework database update"

# 5. Start API and verify authenticated access.
Write-Host "`n[3/4] API" -ForegroundColor Cyan
Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "--no-launch-profile", "--project", $ApiProject, "--urls", $ApiUrl) `
    -WorkingDirectory $Root `
    -RedirectStandardOutput "$LogDir\api.stdout.log" `
    -RedirectStandardError "$LogDir\api.stderr.log" `
    -WindowStyle Hidden | Out-Null

if (-not (Wait-ForApi)) {
    throw "API readiness failed. Inspect API logs in '$LogDir'."
}

# 6. Start dashboard and verify HTTP 200.
Write-Host "`n[4/4] Dashboard" -ForegroundColor Cyan
$dashboardCommand = "Set-Location -LiteralPath '$DashboardDir'; npm.cmd run dev -- --host localhost"
Start-Process -FilePath "powershell.exe" `
    -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $dashboardCommand) `
    -WorkingDirectory $DashboardDir `
    -RedirectStandardOutput "$LogDir\dashboard.stdout.log" `
    -RedirectStandardError "$LogDir\dashboard.stderr.log" `
    -WindowStyle Hidden | Out-Null

if (-not (Wait-ForDashboard)) {
    throw "Dashboard readiness failed. Inspect dashboard logs in '$LogDir'."
}

Write-Host "`nMersad V1 demo started successfully." -ForegroundColor Green
Write-Host "Dashboard: $DashboardUrl"
Write-Host "API:       $ApiUrl"
Write-Host "Logs:      $LogDir"
Write-Host "Sign in with the dashboard API key already configured in your user environment."

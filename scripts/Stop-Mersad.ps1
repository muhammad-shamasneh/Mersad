$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Stop only processes identified as Mersad API/dashboard listeners.
foreach ($port in @(5121, 5173)) {
    $processIds = @(
        Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    )

    foreach ($processId in $processIds) {
        $proc = Get-CimInstance Win32_Process -Filter "ProcessId = $processId"
        if ($null -eq $proc) { continue }

        $cmd = [string]$proc.CommandLine
        $isApi = $cmd -match '(?i)C:\\Mersad\\backend\\Mersad\.Api|Mersad\.Api\.dll'
        $isDashboard = $cmd -match '(?i)C:\\Mersad\\dashboard\\mersad-console|node_modules\\vite'

        if (-not ($isApi -or $isDashboard)) {
            throw "Port $port belongs to an unverified process (PID $processId). It was not stopped."
        }

        Write-Host "Stopping Mersad listener on port $port (PID $processId)." -ForegroundColor Yellow
        Stop-Process -Id $processId -Force
    }
}

Start-Sleep -Seconds 2
foreach ($port in @(5121, 5173)) {
    if (@(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue).Count -gt 0) {
        throw "Port $port is still listening. Check it manually."
    }
    Write-Host "Port $port is stopped." -ForegroundColor Green
}

Write-Host "PostgreSQL was left running; database data was not deleted." -ForegroundColor Green

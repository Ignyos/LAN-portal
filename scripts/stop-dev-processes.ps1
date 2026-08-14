param(
    [string]$WorkspaceRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($WorkspaceRoot)) {
    $WorkspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}
else {
    $WorkspaceRoot = (Resolve-Path $WorkspaceRoot).Path
}

$targetNames = @('Ignyos.LanPortal.Api', 'Ignyos.LanPortal.Web', 'Ignyos.LanPortal.Host')
$targetPorts = @(5212, 5014)

$pidsFromPath = @(
    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ExecutablePath -and
            $_.ExecutablePath.StartsWith($WorkspaceRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
            ($targetNames -contains [System.IO.Path]::GetFileNameWithoutExtension($_.Name))
        } |
        Select-Object -ExpandProperty ProcessId
)

$pidsFromPorts = @(
    Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalPort -in $targetPorts } |
        Select-Object -ExpandProperty OwningProcess -Unique
)

$pids = @($pidsFromPath + $pidsFromPorts) | Where-Object { $_ } | Sort-Object -Unique

if ($pids.Count -eq 0) {
    Write-Host 'No stale dev processes found.'
    exit 0
}

$stopped = 0
foreach ($processId in $pids) {
    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        continue
    }

    if (-not ($targetNames -contains $process.ProcessName)) {
        continue
    }

    Write-Host ('Stopping PID {0} ({1})' -f $process.Id, $process.ProcessName)
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    $stopped++
}

Write-Host ('Stopped {0} process(es).' -f $stopped)
exit 0

param(
    [string]$CodexHome = 'C:\Users\user\.codex',
    [string]$AppId = 'OpenAI.Codex_2p2nqsd0c76g0!App'
)

$ErrorActionPreference = 'Stop'

function Write-RecoveryLog {
    param([string]$Message)
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Add-Content -LiteralPath $script:LogPath -Value "[$timestamp] $Message" -Encoding UTF8
}

while (Get-Process -Name 'ChatGPT' -ErrorAction SilentlyContinue) {
    Start-Sleep -Seconds 2
}

Start-Sleep -Seconds 3

$backupStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupRoot = "C:\Users\user\Codex-Recovery-Backup-$backupStamp"
New-Item -ItemType Directory -Path $backupRoot -ErrorAction Stop | Out-Null
$script:LogPath = Join-Path $backupRoot 'recovery.log'
Write-RecoveryLog "Codex history recovery started."

try {
    $statePath = Join-Path $CodexHome '.codex-global-state.json'
    $stateBackupPath = Join-Path $CodexHome '.codex-global-state.json.bak'
    $indexPath = Join-Path $CodexHome 'session_index.jsonl'
    $sessionsPath = Join-Path $CodexHome 'sessions'
    $archivedSessionsPath = Join-Path $CodexHome 'archived_sessions'
    $sqlitePath = Join-Path $CodexHome 'sqlite'

    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
        throw "Global state file was not found: $statePath"
    }
    if (-not (Test-Path -LiteralPath $sessionsPath -PathType Container)) {
        throw "Sessions directory was not found: $sessionsPath"
    }

    Copy-Item -LiteralPath $statePath -Destination (Join-Path $backupRoot '.codex-global-state.json')
    if (Test-Path -LiteralPath $stateBackupPath -PathType Leaf) {
        Copy-Item -LiteralPath $stateBackupPath -Destination (Join-Path $backupRoot '.codex-global-state.json.bak')
    }
    if (Test-Path -LiteralPath $indexPath -PathType Leaf) {
        Copy-Item -LiteralPath $indexPath -Destination (Join-Path $backupRoot 'session_index.jsonl')
    }
    Copy-Item -LiteralPath $sessionsPath -Destination (Join-Path $backupRoot 'sessions') -Recurse
    if (Test-Path -LiteralPath $archivedSessionsPath -PathType Container) {
        Copy-Item -LiteralPath $archivedSessionsPath -Destination (Join-Path $backupRoot 'archived_sessions') -Recurse
    }
    if (Test-Path -LiteralPath $sqlitePath -PathType Container) {
        Copy-Item -LiteralPath $sqlitePath -Destination (Join-Path $backupRoot 'sqlite') -Recurse
    }
    Write-RecoveryLog "Backup completed: $backupRoot"

    $state = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $atomState = $state.'electron-persisted-atom-state'
    if ($null -eq $atomState) {
        throw 'electron-persisted-atom-state is missing from the global state.'
    }

    $preferences = $atomState.'flat-project-sidebar-preferences-v1'
    if ($null -eq $preferences) {
        $preferences = [pscustomobject]@{
            chatSortMode = 'updated_at'
            initialized = $true
            mode = 'list'
            projectSortMode = 'priority'
        }
        $atomState | Add-Member -NotePropertyName 'flat-project-sidebar-preferences-v1' -NotePropertyValue $preferences
    }
    else {
        $preferences.chatSortMode = 'updated_at'
        $preferences.initialized = $true
        $preferences.mode = 'list'
    }

    $json = $state | ConvertTo-Json -Depth 100 -Compress
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($statePath, $json, $utf8NoBom)
    [System.IO.File]::WriteAllText($stateBackupPath, $json, $utf8NoBom)

    $sessionCount = @(Get-ChildItem -LiteralPath $sessionsPath -Recurse -File -Filter '*.jsonl').Count
    $summary = [ordered]@{
        completedAt = (Get-Date).ToString('o')
        backupPath = $backupRoot
        sessionFileCount = $sessionCount
        sidebarMode = 'list'
        chatSortMode = 'updated_at'
        databaseModified = $false
    } | ConvertTo-Json
    [System.IO.File]::WriteAllText((Join-Path $backupRoot 'recovery-summary.json'), $summary, $utf8NoBom)
    Write-RecoveryLog "Sidebar restored to the complete chronological list. Session files found: $sessionCount"
}
catch {
    Write-RecoveryLog "RECOVERY FAILED: $($_.Exception.Message)"
}
finally {
    Start-Process -FilePath 'explorer.exe' -ArgumentList "shell:AppsFolder\$AppId"
}

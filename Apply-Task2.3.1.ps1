param(
    [string]$RepositoryRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$sourceFolder = Join-Path $RepositoryRoot "QuantumRelay"
$guiPath = Join-Path $sourceFolder "QuantumRelayGui.cs"
$missionPath = Join-Path $sourceFolder "QuantumRelayMissionControl.cs"
$bootstrapPath = Join-Path $sourceFolder "QuantumRelayBootstrap.cs"

foreach ($path in @($guiPath, $missionPath, $bootstrapPath)) {
    if (-not (Test-Path $path)) {
        throw "Required source file not found: $path"
    }
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupFolder = Join-Path $RepositoryRoot "Task2.3.1-Backup-$timestamp"
New-Item -ItemType Directory -Path $backupFolder | Out-Null

Copy-Item $guiPath $backupFolder
Copy-Item $missionPath $backupFolder
Copy-Item $bootstrapPath $backupFolder

function Save-Utf8 {
    param([string]$Path, [string]$Content)
    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        [System.Text.UTF8Encoding]::new($false)
    )
}

function Replace-Once {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Replacement,
        [string]$Description
    )

    $regex = [regex]::new(
        $Pattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

    if (-not $regex.IsMatch($Text)) {
        throw "Could not locate $Description. Backups are in $backupFolder"
    }

    return $regex.Replace($Text, $Replacement, 1)
}

# ---------------------------------------------------------------------------
# QuantumRelayGui.cs
# ---------------------------------------------------------------------------

$gui = Get-Content $guiPath -Raw

if (-not $gui.Contains("private bool _quantumRelaySceneActive;")) {
    $gui = Replace-Once `
        $gui `
        '(private string _localTicker\s*=\s*"Ready\.";\s*)' `
        '$1
        private bool _quantumRelaySceneActive;
        private bool _quantumRelayEventsRegistered;
' `
        "Quantum Relay GUI lifecycle fields"
}

if (-not $gui.Contains("GUI disabled for unsupported scene")) {
    $gui = Replace-Once `
        $gui `
        'public void Start\(\)\s*\{' `
        'public void Start()
        {
            _quantumRelaySceneActive = IsSupportedScene();

            if (!_quantumRelaySceneActive)
            {
                enabled = false;
                Debug.Log(
                    "[QuantumRelay] GUI disabled for unsupported scene: " +
                    HighLogic.LoadedScene);
                return;
            }

            Debug.Log(
                "[QuantumRelay] GUI starting in supported scene: " +
                HighLogic.LoadedScene);
' `
        "Quantum Relay GUI Start method"
}

if (-not $gui.Contains("_quantumRelayEventsRegistered = true;")) {
    $gui = Replace-Once `
        $gui `
        '(GameEvents\.onGUIApplicationLauncherDestroyed\.Add\(OnAppLauncherDestroyed\);\s*)' `
        '$1            _quantumRelayEventsRegistered = true;
' `
        "GUI event registration"
}

if (-not $gui.Contains("GUI destroyed in scene")) {
    $gui = Replace-Once `
        $gui `
        'public void OnDestroy\(\)\s*\{.*?\n\s*\}' `
        'public void OnDestroy()
        {
            if (_quantumRelayEventsRegistered)
            {
                GameEvents.onGUIApplicationLauncherReady.Remove(
                    OnAppLauncherReady);
                GameEvents.onGUIApplicationLauncherDestroyed.Remove(
                    OnAppLauncherDestroyed);
                _quantumRelayEventsRegistered = false;
            }

            if (_quantumRelaySceneActive)
            {
                SaveWindowPositionNow();
                RemoveButton();
            }

            _visible = false;
            _quantumRelaySceneActive = false;

            Debug.Log(
                "[QuantumRelay] GUI destroyed in scene: " +
                HighLogic.LoadedScene);
        }' `
        "Quantum Relay GUI OnDestroy method"
}

if (-not $gui.Contains("if (!_quantumRelaySceneActive || !IsSupportedScene()) return;")) {
    $gui = Replace-Once `
        $gui `
        'public void OnGUI\(\)\s*\{\s*if \(!_visible \|\| !IsSupportedScene\(\)\) return;' `
        'public void OnGUI()
        {
            if (!_quantumRelaySceneActive || !IsSupportedScene()) return;
            if (!_visible) return;' `
        "Quantum Relay GUI OnGUI guard"
}

Save-Utf8 $guiPath $gui

# ---------------------------------------------------------------------------
# QuantumRelayMissionControl.cs
# ---------------------------------------------------------------------------

$mission = Get-Content $missionPath -Raw

if (-not $mission.Contains("Mission Control disabled for unsupported scene")) {
    $mission = Replace-Once `
        $mission `
        'public void Start\(\)\s*\{\s*if \(!IsMissionControlScene\(\)\)\s*return;' `
        'public void Start()
        {
            if (!IsMissionControlScene())
            {
                enabled = false;
                Debug.Log(
                    "[QuantumRelay] Mission Control disabled for unsupported scene: " +
                    HighLogic.LoadedScene);
                return;
            }

            Debug.Log(
                "[QuantumRelay] Mission Control starting in scene: " +
                HighLogic.LoadedScene);' `
        "Mission Control Start guard"
}

if (-not $mission.Contains("public void OnDestroy()")) {
    $insertBefore = '        private static void ScanPersistentVessels()'
    if (-not $mission.Contains($insertBefore)) {
        throw "Could not locate Mission Control scan method. Backups are in $backupFolder"
    }

    $destroyMethod = @'
        public void OnDestroy()
        {
            Debug.Log(
                "[QuantumRelay] Mission Control destroyed in scene: " +
                HighLogic.LoadedScene);
        }

'@

    $mission = $mission.Replace(
        $insertBefore,
        $destroyMethod + $insertBefore
    )
}

Save-Utf8 $missionPath $mission

# ---------------------------------------------------------------------------
# QuantumRelayBootstrap.cs
# ---------------------------------------------------------------------------

$bootstrap = Get-Content $bootstrapPath -Raw

if (-not $bootstrap.Contains("private bool _quantumRelayEventsRegistered;")) {
    $bootstrap = Replace-Once `
        $bootstrap `
        '(private bool _lastOnline;\s*)' `
        '$1
        private bool _quantumRelayEventsRegistered;
' `
        "flight bootstrap lifecycle field"
}

if (-not $bootstrap.Contains("Flight bootstrap starting")) {
    $bootstrap = Replace-Once `
        $bootstrap `
        'public void Start\(\)\s*\{' `
        'public void Start()
        {
            Debug.Log(
                "[QuantumRelay] Flight bootstrap starting.");' `
        "flight bootstrap Start method"
}

if (-not $bootstrap.Contains("_quantumRelayEventsRegistered = true;")) {
    $bootstrap = Replace-Once `
        $bootstrap `
        '(private void RegisterEvents\(\)\s*\{\s*)' `
        '$1
            if (_quantumRelayEventsRegistered)
                return;

            _quantumRelayEventsRegistered = true;
' `
        "bootstrap RegisterEvents guard"
}

if (-not $bootstrap.Contains("if (!_quantumRelayEventsRegistered)")) {
    $bootstrap = Replace-Once `
        $bootstrap `
        '(private void UnregisterEvents\(\)\s*\{\s*)' `
        '$1
            if (!_quantumRelayEventsRegistered)
                return;

            _quantumRelayEventsRegistered = false;
' `
        "bootstrap UnregisterEvents guard"
}

if (-not $bootstrap.Contains("Flight bootstrap destroyed")) {
    $bootstrap = Replace-Once `
        $bootstrap `
        '(QuantumGatewayManager\.Clear\(\);\s*)' `
        '$1
            Debug.Log(
                "[QuantumRelay] Flight bootstrap destroyed.");
' `
        "flight bootstrap destruction log"
}

Save-Utf8 $bootstrapPath $bootstrap

Write-Host ""
Write-Host "Quantum Relay Task 2.3.1 installed successfully." -ForegroundColor Green
Write-Host "Backups: $backupFolder"
Write-Host ""
Write-Host "Build with:"
Write-Host "  dotnet build"

param(
    [string]$RepositoryRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$sourceFolder = Join-Path $RepositoryRoot "QuantumRelay"
$guiPath = Join-Path $sourceFolder "QuantumRelayGui.cs"

if (-not (Test-Path $guiPath)) {
    throw "QuantumRelayGui.cs was not found at: $guiPath"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupFolder = Join-Path $RepositoryRoot "Task2.5-Backup-$timestamp"
New-Item -ItemType Directory -Path $backupFolder | Out-Null
Copy-Item $guiPath $backupFolder

function Save-Utf8 {
    param([string]$Path, [string]$Content)

    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        [System.Text.UTF8Encoding]::new($false)
    )
}

function Replace-Required {
    param(
        [string]$Text,
        [string]$Old,
        [string]$New,
        [string]$Description
    )

    if (-not $Text.Contains($Old)) {
        throw "Could not locate $Description. No source changes were saved."
    }

    return $Text.Replace($Old, $New)
}

$gui = Get-Content $guiPath -Raw

$oldHeader = @'
            GUILayout.Label("Quantum link quality: " + QuantumRelaySettings.SignalQualityPercent + "%");
            GUILayout.Label("Power requirement: " + FormatNumber(QuantumRelaySettings.ElectricChargePerSecondPerGateway * 2.0) + " EC/s total");
            GUILayout.EndVertical();
'@

$newHeader = @'
            GUILayout.Label("Quantum link quality: " + QuantumRelaySettings.SignalQualityPercent + "%");

            double gatewayAPower = GetDisplayedGatewayPowerRate(
                flight,
                true);
            double gatewayBPower = GetDisplayedGatewayPowerRate(
                flight,
                false);
            double totalPower = gatewayAPower + gatewayBPower;

            GUILayout.Label(
                "Live relay draw: " +
                FormatNumber(totalPower) +
                " EC/s total");
            GUILayout.Label(
                "Gateway A: " +
                FormatNumber(gatewayAPower) +
                " EC/s | Gateway B: " +
                FormatNumber(gatewayBPower) +
                " EC/s");
            GUILayout.EndVertical();
'@

$gui = Replace-Required `
    $gui `
    $oldHeader `
    $newHeader `
    "bridge power requirement display"

$oldDiagnostics = @'
            GUILayout.Label("Power per gateway: " + FormatNumber(QuantumRelaySettings.ElectricChargePerSecondPerGateway) + " EC/s");
            GUILayout.Label("Last state update UT: " + FormatNumber(QuantumRelayRuntimeState.UpdatedUt));
'@

$newDiagnostics = @'
            double gatewayAPower =
                QuantumRelayRuntimeState.GatewayA != null
                    ? Math.Max(
                        0.0,
                        QuantumRelayRuntimeState.GatewayA.RelayPowerRate)
                    : 0.0;
            double gatewayBPower =
                QuantumRelayRuntimeState.GatewayB != null
                    ? Math.Max(
                        0.0,
                        QuantumRelayRuntimeState.GatewayB.RelayPowerRate)
                    : 0.0;

            GUILayout.Label(
                "Configured fallback power: " +
                FormatNumber(
                    QuantumRelaySettings
                        .ElectricChargePerSecondPerGateway) +
                " EC/s");
            GUILayout.Label(
                "Gateway A live draw: " +
                FormatNumber(gatewayAPower) +
                " EC/s");
            GUILayout.Label(
                "Gateway B live draw: " +
                FormatNumber(gatewayBPower) +
                " EC/s");
            GUILayout.Label(
                "Combined live draw: " +
                FormatNumber(gatewayAPower + gatewayBPower) +
                " EC/s");
            GUILayout.Label("Last state update UT: " + FormatNumber(QuantumRelayRuntimeState.UpdatedUt));
'@

$gui = Replace-Required `
    $gui `
    $oldDiagnostics `
    $newDiagnostics `
    "diagnostics power display"

$helperAnchor = @'
        private static void DrawRelayIdentity(
'@

$helperMethod = @'
        private static double GetDisplayedGatewayPowerRate(
            bool flight,
            bool gatewayA)
        {
            if (flight)
            {
                GatewayCandidate gateway =
                    gatewayA
                        ? QuantumRelayRuntimeState.GatewayA
                        : QuantumRelayRuntimeState.GatewayB;

                return gateway != null
                    ? Math.Max(0.0, gateway.RelayPowerRate)
                    : 0.0;
            }

            GatewayTelemetry telemetry =
                gatewayA
                    ? QuantumRelayRegistry.GatewayA
                    : QuantumRelayRegistry.GatewayB;

            return telemetry != null && telemetry.IsKnown
                ? Math.Max(0.0, telemetry.RelayPowerRate)
                : 0.0;
        }

'@

if (-not $gui.Contains("GetDisplayedGatewayPowerRate(")) {
    throw "Unexpected state: helper references were not inserted."
}

if (-not $gui.Contains("private static double GetDisplayedGatewayPowerRate")) {
    if (-not $gui.Contains($helperAnchor)) {
        throw "Could not locate helper insertion point. No source changes were saved."
    }

    $gui = $gui.Replace(
        $helperAnchor,
        $helperMethod + $helperAnchor
    )
}

$gui = $gui.Replace(
    '"Quantum Relay v1.2 alpha 2"',
    '"Quantum Relay v1.2 alpha 3"'
)
$gui = $gui.Replace(
    'GUILayout.Label("Version: 1.2 alpha 2");',
    'GUILayout.Label("Version: 1.2 alpha 3");'
)
$gui = $gui.Replace(
    'GUILayout.Label("Version 1.2 alpha 2");',
    'GUILayout.Label("Version 1.2 alpha 3");'
)

Save-Utf8 $guiPath $gui

Write-Host ""
Write-Host "Quantum Relay Task 2.5 installed successfully." -ForegroundColor Green
Write-Host "Backup created at: $backupFolder"
Write-Host ""
Write-Host "Build with:"
Write-Host "  dotnet build"

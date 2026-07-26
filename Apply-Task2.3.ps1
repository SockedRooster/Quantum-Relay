param(
    [string]$RepositoryRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$projectFolder = Join-Path $RepositoryRoot "QuantumRelay"
$registryPath = Join-Path $projectFolder "QuantumRelayRegistry.cs"
$missionPath = Join-Path $projectFolder "QuantumRelayMissionControl.cs"
$guiPath = Join-Path $projectFolder "QuantumRelayGui.cs"

foreach ($path in @($registryPath, $missionPath, $guiPath)) {
    if (-not (Test-Path $path)) {
        throw "Required file not found: $path"
    }
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupFolder = Join-Path $RepositoryRoot "Task2.3-Backup-$timestamp"
New-Item -ItemType Directory -Path $backupFolder | Out-Null

Copy-Item $registryPath $backupFolder
Copy-Item $missionPath $backupFolder
Copy-Item $guiPath $backupFolder

$packageFolder = Split-Path -Parent $MyInvocation.MyCommand.Path
$replacementFolder = Join-Path $packageFolder "ReplacementFiles"
Copy-Item (Join-Path $replacementFolder "QuantumRelayRegistry.cs") $registryPath -Force
Copy-Item (Join-Path $replacementFolder "QuantumRelayMissionControl.cs") $missionPath -Force

$gui = Get-Content $guiPath -Raw

function Replace-Exact {
    param(
        [string]$Text,
        [string]$Old,
        [string]$New,
        [string]$Description
    )

    if (-not $Text.Contains($Old)) {
        throw "Could not find expected GUI block: $Description. Original files are preserved in $backupFolder"
    }

    return $Text.Replace($Old, $New)
}

$oldLive = @'
            GUILayout.Label("Distance: " + FormatNumber(gateway.DistanceMetres / 1000.0) + " km");
            Color old = GUI.contentColor;
            GUI.contentColor = gateway.IsValid ? Color.green : Color.yellow;
            GUILayout.Label(gateway.IsValid ? "● READY" : "● WAITING");
            GUI.contentColor = old;
            GUILayout.Label("Electric charge: " + FormatCharge(gateway));
'@

$newLive = @'
            GUILayout.Label("Distance: " + FormatNumber(gateway.DistanceMetres / 1000.0) + " km");
            DrawRelayIdentity(
                gateway.HasQuantumRelayModule,
                gateway.RelayModel,
                gateway.RelayTier);
            Color old = GUI.contentColor;
            GUI.contentColor = gateway.IsValid ? Color.green : Color.yellow;
            GUILayout.Label(gateway.IsValid ? "● READY" : "● WAITING");
            GUI.contentColor = old;
            DrawRelayState(
                gateway.HasQuantumRelayModule,
                gateway.RelayOperationalState,
                gateway.RelayDeploymentState,
                gateway.RelaySynchronized,
                gateway.RelaySynchronizationFraction,
                gateway.RelayPowerRate);
            GUILayout.Label("Electric charge: " + FormatCharge(gateway));
'@

$gui = Replace-Exact $gui $oldLive $newLive "live gateway summary"

$oldSaved = @'
            GUILayout.Label("Distance: " + FormatNumber(gateway.DistanceMetres / 1000.0) + " km");
            Color old = GUI.contentColor;
            GUI.contentColor = gateway.Ready ? Color.green : Color.yellow;
            GUILayout.Label(gateway.Ready ? "● LAST KNOWN READY" : "● LAST KNOWN WAITING");
            GUI.contentColor = old;
            GUILayout.Label("Electric charge: " + FormatCharge(gateway.ElectricChargeAmount, gateway.ElectricChargeCapacity));
'@

$newSaved = @'
            GUILayout.Label("Distance: " + FormatNumber(gateway.DistanceMetres / 1000.0) + " km");
            DrawRelayIdentity(
                gateway.HasQuantumRelayModule,
                gateway.RelayModel,
                gateway.RelayTier);
            Color old = GUI.contentColor;
            GUI.contentColor = gateway.Ready ? Color.green : Color.yellow;
            GUILayout.Label(gateway.Ready ? "● LAST KNOWN READY" : "● LAST KNOWN WAITING");
            GUI.contentColor = old;
            DrawRelayState(
                gateway.HasQuantumRelayModule,
                gateway.RelayOperationalState,
                gateway.RelayDeploymentState,
                gateway.RelaySynchronized,
                gateway.RelaySynchronizationFraction,
                gateway.RelayPowerRate);
            GUILayout.Label("Electric charge: " + FormatCharge(gateway.ElectricChargeAmount, gateway.ElectricChargeCapacity));
'@

$gui = Replace-Exact $gui $oldSaved $newSaved "saved gateway telemetry"

$oldDiagnostics = @'
            GUILayout.Label("Vessel: " + SafeName(gateway.Vessel.vesselName));
            GUILayout.Label(StatusMark(gateway.HasReflector) + " RFL-2000 reflector");
            GUILayout.Label(StatusMark(gateway.ReflectorDeployed) + " Reflector deployed");
            GUILayout.Label(StatusMark(gateway.HasElectricCharge) + " Electric charge");
            GUILayout.Label("Valid: " + (gateway.IsValid ? "YES" : "NO"));
'@

$newDiagnostics = @'
            GUILayout.Label("Vessel: " + SafeName(gateway.Vessel.vesselName));
            if (gateway.HasQuantumRelayModule)
            {
                GUILayout.Label(
                    StatusMark(gateway.QuantumRelayOperational) +
                    " ModuleQuantumRelay");
                GUILayout.Label(
                    "Model: " + SafeName(gateway.RelayModel) +
                    " | Tier " + gateway.RelayTier);
                GUILayout.Label(
                    "Operational state: " +
                    SafeName(gateway.RelayOperationalState));
                GUILayout.Label(
                    "Deployment state: " +
                    SafeName(gateway.RelayDeploymentState));
                GUILayout.Label(
                    StatusMark(gateway.RelaySynchronized) +
                    " Synchronization " +
                    FormatPercent(
                        gateway.RelaySynchronizationFraction));
                GUILayout.Label(
                    "Relay power draw: " +
                    FormatNumber(gateway.RelayPowerRate) +
                    " EC/s");
            }
            else
            {
                GUILayout.Label(
                    StatusMark(gateway.HasReflector) +
                    " Legacy reflector");
                GUILayout.Label(
                    StatusMark(gateway.ReflectorDeployed) +
                    " Reflector deployed");
            }

            GUILayout.Label(
                StatusMark(gateway.HasCommNet) +
                " CommNet hardware");
            GUILayout.Label(
                StatusMark(gateway.HasProbeControl) +
                " Probe control");
            GUILayout.Label(
                StatusMark(gateway.HasElectricCharge) +
                " Electric charge");
            GUILayout.Label(
                "Valid: " +
                (gateway.IsValid ? "YES" : "NO"));
'@

$gui = Replace-Exact $gui $oldDiagnostics $newDiagnostics "diagnostics panel"

$marker = @'
        private static string FormatCharge(GatewayCandidate gateway)
'@

$helpers = @'
        private static void DrawRelayIdentity(
            bool hasQuantumRelayModule,
            string relayModel,
            int relayTier)
        {
            if (!hasQuantumRelayModule)
            {
                GUILayout.Label("Hardware: Legacy reflector");
                return;
            }

            GUILayout.Label(
                "Hardware: " + SafeName(relayModel) +
                " | Tier " + relayTier);
        }

        private static void DrawRelayState(
            bool hasQuantumRelayModule,
            string operationalState,
            string deploymentState,
            bool synchronized,
            double synchronizationFraction,
            double powerRate)
        {
            if (!hasQuantumRelayModule)
                return;

            GUILayout.Label(
                "Relay state: " + SafeName(operationalState));
            GUILayout.Label(
                "Deployment: " + SafeName(deploymentState));
            GUILayout.Label(
                "Synchronization: " +
                (synchronized
                    ? "Synchronized"
                    : FormatPercent(synchronizationFraction)));
            GUILayout.Label(
                "Relay draw: " +
                FormatNumber(powerRate) + " EC/s");
        }

        private static string FormatPercent(double fraction)
        {
            double clamped =
                Math.Max(0.0, Math.Min(1.0, fraction));

            return (clamped * 100.0).ToString("0") + "%";
        }

        private static string FormatCharge(GatewayCandidate gateway)
'@

$gui = Replace-Exact $gui $marker $helpers "relay display helpers"

Set-Content -Path $guiPath -Value $gui -Encoding UTF8

Write-Host ""
Write-Host "Task 2.3 installed successfully." -ForegroundColor Green
Write-Host "Backups: $backupFolder"
Write-Host ""
Write-Host "Now run:"
Write-Host "  dotnet build"

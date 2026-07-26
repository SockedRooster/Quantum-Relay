using System;
using KSP.UI.Screens;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>v1.1 alpha 4 multi-scene toolbar, navigation, settings, diagnostics and about console.</summary>
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    internal sealed class QuantumRelayGui : MonoBehaviour
    {
        private const int WindowId = 0x51524C59;
        private const float WindowWidth = 420f;

        private enum Page { Status, Settings, Diagnostics, About }

        private ApplicationLauncherButton _button;
        private Texture2D _toolbarTexture;
        private Rect _windowRect = new Rect(260f, 100f, WindowWidth, 430f);
        private Vector2 _lastSavedPosition;
        private float _savePositionAfter;
        private Vector2 _scroll;
        private bool _visible;
        private Page _page;

        private int _draftSignal;
        private double _draftRadius;
        private double _draftPower;
        private bool _draftAutoRebuild;
        private bool _draftMessages;
        private bool _draftDebug;
        private string _localTicker = "Ready.";

        
        private bool _quantumRelaySceneActive;
        private bool _quantumRelayEventsRegistered;
public void Start()
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

            CopySettingsToDraft();
            _windowRect.x = QuantumRelaySettings.WindowX;
            _windowRect.y = QuantumRelaySettings.WindowY;
            _lastSavedPosition = new Vector2(_windowRect.x, _windowRect.y);
            GameEvents.onGUIApplicationLauncherReady.Add(OnAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnAppLauncherDestroyed);
                        _quantumRelayEventsRegistered = true;
if (ApplicationLauncher.Ready) OnAppLauncherReady();
        }

        public void OnDestroy()
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
        }

        private void OnAppLauncherReady()
        {
            if (_button != null || ApplicationLauncher.Instance == null) return;

            _toolbarTexture = GameDatabase.Instance.GetTexture("QuantumRelay/Icons/QuantumRelay_38", false);
            if (_toolbarTexture == null)
                _toolbarTexture = GameDatabase.Instance.GetTexture("QuantumRelay/Icons/QuantumRelay", false);

            _button = ApplicationLauncher.Instance.AddModApplication(
                ShowWindow, HideWindow, null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW |
                ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.TRACKSTATION,
                _toolbarTexture);

            Debug.Log("[QuantumRelay] v1.1 alpha 4 toolbar button created for Flight, Space Center and Tracking Station.");
        }

        private void OnAppLauncherDestroyed() { _button = null; }

        private void RemoveButton()
        {
            if (_button == null || ApplicationLauncher.Instance == null) return;
            try { ApplicationLauncher.Instance.RemoveModApplication(_button); }
            catch (Exception ex) { Debug.LogWarning("[QuantumRelay] Could not remove toolbar button: " + ex.Message); }
            _button = null;
        }

        private void ShowWindow() { _visible = true; }
        private void HideWindow() { _visible = false; }

        public void OnGUI()
        {
            if (!_quantumRelaySceneActive || !IsSupportedScene()) return;
            if (!_visible) return;
            GUI.skin = HighLogic.Skin;
            _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow,
                "Quantum Relay v1.2 alpha 3", GUILayout.Width(WindowWidth));
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - 36f));
            QueueWindowPositionSave();
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();
            DrawTabs();
            GUILayout.Space(5f);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(275f), GUILayout.MaxHeight(510f));
            switch (_page)
            {
                case Page.Status: DrawStatusPage(); break;
                case Page.Settings: DrawSettingsPage(); break;
                case Page.Diagnostics: DrawDiagnosticsPage(); break;
                case Page.About: DrawAboutPage(); break;
            }
            GUILayout.EndScrollView();

            GUILayout.Space(5f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("STATUS: " + (string.IsNullOrEmpty(_localTicker) ? QuantumRelayRuntimeState.Ticker : _localTicker));
            GUILayout.EndVertical();

            if (GUILayout.Button("Close"))
            {
                _visible = false;
                if (_button != null) _button.SetFalse(false);
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
        }

        private void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_page == Page.Status, "Status", GUI.skin.button)) _page = Page.Status;
            if (GUILayout.Toggle(_page == Page.Settings, "Settings", GUI.skin.button)) _page = Page.Settings;
            if (GUILayout.Toggle(_page == Page.Diagnostics, "Diagnostics", GUI.skin.button)) _page = Page.Diagnostics;
            if (GUILayout.Toggle(_page == Page.About, "About", GUI.skin.button)) _page = Page.About;
            GUILayout.EndHorizontal();
        }

        private void DrawStatusPage()
        {
            bool flight = HighLogic.LoadedSceneIsFlight;
            DrawBridgeHeader(flight);
            GUILayout.Space(6f);

            if (flight)
            {
                DrawGatewaySummary("Gateway A", QuantumRelayRuntimeState.GatewayA);
                GUILayout.Space(5f);
                DrawGatewaySummary("Gateway B", QuantumRelayRuntimeState.GatewayB);
            }
            else
            {
                DrawGatewayTelemetry("Gateway A", QuantumRelayRegistry.GatewayA);
                GUILayout.Space(5f);
                DrawGatewayTelemetry("Gateway B", QuantumRelayRegistry.GatewayB);
            }
            GUILayout.Space(7f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(flight ? "Refresh Network" : "Refresh Telemetry"))
            {
                if (flight) QuantumRelayCommands.RequestRefresh();
                else QuantumRelayRegistry.Reload();
                _localTicker = flight ? "Gateway scan requested." : "Mission Control telemetry reloaded.";
            }

            bool oldEnabled = GUI.enabled;
            GUI.enabled = flight;
            if (GUILayout.Button("Rebuild CommNet"))
            {
                QuantumRelayCommands.RequestRebuild();
                _localTicker = "CommNet rebuild requested.";
            }
            GUI.enabled = oldEnabled;
            GUILayout.EndHorizontal();

            DrawGatewayActions("A", QuantumRelayRuntimeState.GatewayA);
            DrawGatewayActions("B", QuantumRelayRuntimeState.GatewayB);
        }

        private static void DrawBridgeHeader(bool flight)
        {
            bool online = flight ? QuantumRelayRuntimeState.Online : QuantumRelayRegistry.Online;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(flight ? "RoosterWorks Quantum Link" : "Quantum Relay Mission Control");

            Color old = GUI.contentColor;
            GUI.contentColor = online ? Color.green : new Color(1f, 0.75f, 0.2f);
            GUILayout.Label(online ? "[ONLINE] BRIDGE ONLINE" : (QuantumRelayRegistry.HasTelemetry ? "[OFFLINE] LAST KNOWN: OFFLINE" : "[WAITING] AWAITING TELEMETRY"));
            GUI.contentColor = old;

            if (!flight)
                GUILayout.Label("Last telemetry: " + QuantumRelayRegistry.AgeText());
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
        }

        private static void DrawGatewaySummary(string heading, GatewayCandidate gateway)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(heading);
            if (gateway == null || gateway.Vessel == null)
            {
                GUILayout.Label("No gateway selected");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label("Vessel: " + SafeName(gateway.Vessel.vesselName));
            GUILayout.Label("Endpoint: " + (gateway.Wormhole != null ? SafeName(gateway.Wormhole.Name) : "Unknown"));
            GUILayout.Label("Distance: " + FormatNumber(gateway.DistanceMetres / 1000.0) + " km");
            DrawRelayIdentity(
                gateway.HasQuantumRelayModule,
                gateway.RelayModel,
                gateway.RelayTier);
            Color old = GUI.contentColor;
            GUI.contentColor = gateway.IsValid ? Color.green : Color.yellow;
            GUILayout.Label(gateway.IsValid ? "[READY] READY" : "[WAITING] WAITING");
            GUI.contentColor = old;
            DrawRelayState(
                gateway.HasQuantumRelayModule,
                gateway.RelayOperationalState,
                gateway.RelayDeploymentState,
                gateway.RelaySynchronized,
                gateway.RelaySynchronizationFraction,
                gateway.RelayPowerRate);
            GUILayout.Label("Electric charge: " + FormatCharge(gateway));
            GUILayout.EndVertical();
        }

        private static void DrawGatewayTelemetry(string heading, GatewayTelemetry gateway)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(heading);
            if (gateway == null || !gateway.IsKnown)
            {
                GUILayout.Label("No registered gateway telemetry");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label("Vessel: " + SafeName(gateway.VesselName));
            GUILayout.Label("Endpoint: " + SafeName(gateway.EndpointName));
            GUILayout.Label("Location: " + SafeName(gateway.BodyName));
            GUILayout.Label("Distance: " + FormatNumber(gateway.DistanceMetres / 1000.0) + " km");
            DrawRelayIdentity(
                gateway.HasQuantumRelayModule,
                gateway.RelayModel,
                gateway.RelayTier);
            Color old = GUI.contentColor;
            GUI.contentColor = gateway.Ready ? Color.green : Color.yellow;
            GUILayout.Label(gateway.Ready ? "[READY] LAST KNOWN READY" : "[WAITING] LAST KNOWN WAITING");
            GUI.contentColor = old;
            DrawRelayState(
                gateway.HasQuantumRelayModule,
                gateway.RelayOperationalState,
                gateway.RelayDeploymentState,
                gateway.RelaySynchronized,
                gateway.RelaySynchronizationFraction,
                gateway.RelayPowerRate);
            GUILayout.Label("Electric charge: " + FormatCharge(gateway.ElectricChargeAmount, gateway.ElectricChargeCapacity));
            GUILayout.EndVertical();
        }

        private void DrawSettingsPage()
        {
            GUILayout.Label("Quantum Link Configuration");
            GUILayout.Space(5f);

            DrawStepSetting("Quantum link quality", _draftSignal + "%",
                delegate { _draftSignal = Math.Max(10, _draftSignal - 10); },
                delegate { _draftSignal = Math.Min(100, _draftSignal + 10); });

            DrawStepSetting("Gateway activation radius", FormatNumber(_draftRadius / 1000.0) + " km",
                delegate { _draftRadius = Math.Max(100000.0, _draftRadius - 25000.0); },
                delegate { _draftRadius = Math.Min(500000.0, _draftRadius + 25000.0); });

            DrawStepSetting("Gateway power requirement", FormatNumber(_draftPower) + " EC/s",
                delegate { _draftPower = Math.Max(0.0, _draftPower - 1.0); },
                delegate { _draftPower = Math.Min(50.0, _draftPower + 1.0); });

            GUILayout.Space(6f);
            _draftAutoRebuild = GUILayout.Toggle(_draftAutoRebuild, "Automatically rebuild CommNet after changes");
            _draftMessages = GUILayout.Toggle(_draftMessages, "Show screen messages");
            _draftDebug = GUILayout.Toggle(_draftDebug, "Enable debug logging");
            GUILayout.Space(8f);

            if (GUILayout.Button("Apply and Save"))
            {
                bool networkChanged = QuantumRelaySettings.Apply(_draftSignal, _draftRadius, _draftPower,
                    _draftAutoRebuild, _draftMessages, _draftDebug, true);
                if (networkChanged)
                {
                    QuantumRelayCommands.RequestRefresh();
                    if (QuantumRelaySettings.AutoRebuildCommNet) QuantumRelayCommands.RequestRebuild();
                }
                _localTicker = "Settings saved.";
                QuantumRelayNotifications.Post("settings-saved", "Settings saved.", false);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload"))
            {
                QuantumRelaySettings.Load();
                CopySettingsToDraft();
                QuantumRelayCommands.RequestRefresh();
                if (QuantumRelaySettings.AutoRebuildCommNet) QuantumRelayCommands.RequestRebuild();
                _localTicker = "Settings reloaded.";
            }
            if (GUILayout.Button("Reset Defaults"))
            {
                QuantumRelaySettings.ResetDefaults(true);
                CopySettingsToDraft();
                QuantumRelayCommands.RequestRefresh();
                QuantumRelayCommands.RequestRebuild();
                _localTicker = "Default settings restored.";
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawStepSetting(string label, string value, Action decrease, Action increase)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(42f))) decrease();
            GUILayout.Label(value, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(">", GUILayout.Width(42f))) increase();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static void DrawDiagnosticsPage()
        {
            GUILayout.Label("Runtime Diagnostics");
            DrawGatewayDiagnostics("Gateway A", QuantumRelayRuntimeState.GatewayA);
            GUILayout.Space(5f);
            DrawGatewayDiagnostics("Gateway B", QuantumRelayRuntimeState.GatewayB);
            GUILayout.Space(5f);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Bridge");
            GUILayout.Label("CommNet hook: " + (CommNetNetworkInstaller.IsInstalled ? "INSTALLED" : "NOT INSTALLED"));
            GUILayout.Label("Gateway pair active: " + (QuantumGatewayManager.Active ? "YES" : "NO"));
            GUILayout.Label("Configured signal quality: " + QuantumRelaySettings.SignalQualityPercent + "%");
            GUILayout.Label("Gateway radius: " + FormatNumber(QuantumRelaySettings.GatewayRadiusMetres / 1000.0) + " km");
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
            GUILayout.Label("Version: 1.2 alpha 3");
            GUILayout.Label("Registry telemetry: " + (QuantumRelayRegistry.HasTelemetry ? "AVAILABLE" : "NONE"));
            GUILayout.Label("Registry age: " + QuantumRelayRegistry.AgeText());
            GUILayout.Space(5f);
            GUILayout.Label("Recent events");
            foreach (string item in QuantumRelayNotifications.History)
                GUILayout.Label(item);
            GUILayout.EndVertical();
        }

        private static void DrawGatewayDiagnostics(string heading, GatewayCandidate gateway)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(heading);
            if (gateway == null || gateway.Vessel == null)
            {
                GUILayout.Label("Candidate unavailable");
                GUILayout.EndVertical();
                return;
            }

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
            GUILayout.EndVertical();
        }

        private static void DrawAboutPage()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Quantum Relay");
            GUILayout.Label("Version 1.2 alpha 3");
            GUILayout.Space(6f);
            GUILayout.Label("Developed by SockedRooster");
            GUILayout.Label("RoosterWorks");
            GUILayout.Label("MIT License");
            GUILayout.Space(8f);
            GUILayout.Label("True stock CommNet routing through the Promised Worlds wormhole pair.");
            GUILayout.Space(8f);
            GUILayout.Label("Built for Kerbal Space Program 1.12.5");
            GUILayout.EndVertical();
        }

        private void CopySettingsToDraft()
        {
            _draftSignal = QuantumRelaySettings.SignalQualityPercent;
            _draftRadius = QuantumRelaySettings.GatewayRadiusMetres;
            _draftPower = QuantumRelaySettings.ElectricChargePerSecondPerGateway;
            _draftAutoRebuild = QuantumRelaySettings.AutoRebuildCommNet;
            _draftMessages = QuantumRelaySettings.ShowScreenMessages;
            _draftDebug = QuantumRelaySettings.DebugLogging;
        }

        private void DrawGatewayActions(string label, GatewayCandidate gateway)
        {
            bool available = gateway != null && gateway.Vessel != null;
            bool flightScene = HighLogic.LoadedSceneIsFlight;
            bool loaded = flightScene && available && gateway.Vessel.loaded;

            GUILayout.BeginHorizontal();

            bool oldEnabled = GUI.enabled;
            GUI.enabled = loaded;
            if (GUILayout.Button("Focus Gateway " + label))
                FocusGateway(gateway);
            GUI.enabled = oldEnabled;

            GUI.enabled = flightScene && available;
            if (GUILayout.Button("Track Gateway " + label))
                TrackGateway(gateway);
            GUI.enabled = oldEnabled;

            GUILayout.EndHorizontal();

            if (!flightScene)
                GUILayout.Label("Gateway navigation controls are available during flight.");
            else if (available && !loaded)
                GUILayout.Label("Gateway " + label + " is on rails. Use Track to locate it in Map View.");
        }

        private void FocusGateway(GatewayCandidate gateway)
        {
            if (gateway == null || gateway.Vessel == null)
            {
                SetLocalStatus("No gateway is available to focus.");
                return;
            }

            if (!gateway.Vessel.loaded)
            {
                SetLocalStatus(SafeName(gateway.Vessel.vesselName) +
                    " is currently on rails. Use Track to locate it in Map View.");
                return;
            }

            try
            {
                FlightGlobals.SetActiveVessel(gateway.Vessel);
                SetLocalStatus("Focused " + SafeName(gateway.Vessel.vesselName) + ".");
            }
            catch (Exception ex)
            {
                SetLocalStatus("Unable to focus gateway: " + ex.Message);
            }
        }

        private void TrackGateway(GatewayCandidate gateway)
        {
            if (gateway == null || gateway.Vessel == null)
            {
                SetLocalStatus("No gateway is available to track.");
                return;
            }

            try
            {
                FlightGlobals.fetch.SetVesselTarget(gateway.Vessel);
                if (!MapView.MapIsEnabled)
                    MapView.EnterMapView();

                SetLocalStatus("Tracking " + SafeName(gateway.Vessel.vesselName) + " in Map View.");
            }
            catch (Exception ex)
            {
                SetLocalStatus("Unable to track gateway: " + ex.Message);
            }
        }

        private void SetLocalStatus(string message)
        {
            _localTicker = message;
            QuantumRelayNotifications.Post("gui-status", message, false);
        }

        private void QueueWindowPositionSave()
        {
            Vector2 current = new Vector2(_windowRect.x, _windowRect.y);
            if ((current - _lastSavedPosition).sqrMagnitude > 1f)
                _savePositionAfter = Time.realtimeSinceStartup + 0.75f;

            if (_savePositionAfter > 0f && Time.realtimeSinceStartup >= _savePositionAfter)
                SaveWindowPositionNow();
        }

        private void SaveWindowPositionNow()
        {
            Vector2 current = new Vector2(_windowRect.x, _windowRect.y);
            if ((current - _lastSavedPosition).sqrMagnitude <= 1f) return;
            QuantumRelaySettings.SaveWindowPosition(current.x, current.y);
            _lastSavedPosition = current;
            _savePositionAfter = 0f;
        }

        private static bool IsSupportedScene()
        {
            return HighLogic.LoadedSceneIsFlight ||
                   HighLogic.LoadedScene == GameScenes.SPACECENTER ||
                   HighLogic.LoadedScene == GameScenes.TRACKSTATION;
        }

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
        {
            if (gateway.ElectricChargeCapacity <= 0.0) return FormatNumber(gateway.ElectricChargeAmount) + " EC";
            double percentage = Math.Max(0.0, Math.Min(100.0,
                gateway.ElectricChargeAmount / gateway.ElectricChargeCapacity * 100.0));
            return FormatNumber(gateway.ElectricChargeAmount) + "/" + FormatNumber(gateway.ElectricChargeCapacity) +
                   " EC (" + percentage.ToString("0") + "%)";
        }

        private static string FormatCharge(double amount, double capacity)
        {
            if (capacity <= 0.0) return FormatNumber(amount) + " EC";
            double percentage = Math.Max(0.0, Math.Min(100.0, amount / capacity * 100.0));
            return FormatNumber(amount) + "/" + FormatNumber(capacity) + " EC (" + percentage.ToString("0") + "%)";
        }

        private static string StatusMark(bool value) { return value ? "[OK]" : "[--]"; }
        private static string SafeName(string value) { return string.IsNullOrEmpty(value) ? "Unnamed" : value; }
        private static string FormatNumber(double value) { return value.ToString("0.##"); }
    }
}


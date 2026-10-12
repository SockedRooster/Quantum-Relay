using System;
using System.Collections.Generic;
using KSP.UI.Screens;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>Multi-scene toolbar, network overview, settings, diagnostics and about console.</summary>
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    internal sealed partial class QuantumRelayGui : MonoBehaviour
    {
        private const int WindowId = 0x51524C59;
        private const float DefaultWindowWidth = 560f;
        private const float DefaultWindowHeight = 700f;
        private const float MinimumWindowWidth = 460f;
        private const float MinimumWindowHeight = 420f;
        private const float ResizeGripSize = 24f;
        private const int ResizeControlHint = 0x51525253;

        private enum Page { Status, Map, Settings, Diagnostics, About }
        private enum NetworkSort { Name, Gateway }

        private ApplicationLauncherButton _button;
        private Texture2D _toolbarTexture;
        private GUISkin _windowSkin;
        private GUIStyle _tooltipStyle;
        private readonly Dictionary<string, bool> _expandedDiagnostics = new Dictionary<string, bool>();
        private bool _showEventHistory;
        private Rect _windowRect = new Rect(260f, 100f, DefaultWindowWidth, DefaultWindowHeight);
        private Vector2 _lastSavedPosition;
        private float _savePositionAfter;
        private Vector2 _scroll;
        private bool _visible;
        private bool _resizing;
        private Vector2 _resizeStartMouse;
        private Vector2 _resizeStartSize;
        private Page _page;
        private NetworkSort _networkSort = NetworkSort.Name;
        private string _networkFilter = string.Empty;
        private bool _onlineOnly;
        private readonly Dictionary<string, bool> _expandedFlightNetworks =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _expandedRegistryNetworks =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private Vector2 _lastSavedSize;

        private bool _draftAutoRebuild;
        private bool _draftMessages;
        private bool _draftDebug;
        private bool _draftShowHud, _draftFollowStock;
        private float _draftHudX, _draftHudY, _draftUiScale;
        private float _guiScale = 1f;
        private bool _restoreLayout;
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

            QuantumRelaySettings.Load();
            CopySettingsToDraft();
            _windowRect.x = QuantumRelaySettings.WindowX;
            _windowRect.y = QuantumRelaySettings.WindowY;
            _windowRect.width = QuantumRelaySettings.WindowWidth;
            _windowRect.height = QuantumRelaySettings.WindowHeight;
            _lastSavedPosition = new Vector2(_windowRect.x, _windowRect.y);
            _lastSavedSize = new Vector2(_windowRect.width, _windowRect.height);
            ClampWindowSize();
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
            if (_windowSkin != null) Destroy(_windowSkin);

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

            Debug.Log("[QuantumRelay] " + QuantumRelayConstants.DisplayVersion + " toolbar button created for Flight, Space Center and Tracking Station.");
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
        private void HideWindow() { SaveWindowPositionNow(); _visible = false; }
        public void OnApplicationQuit() { if (_quantumRelaySceneActive) SaveWindowPositionNow(); }

        public void OnGUI()
        {
            if (!_quantumRelaySceneActive || !IsSupportedScene()) return;
            if (!_visible) return;
            if (Event.current.type == EventType.Layout)
            {
                _guiScale = QuantumRelaySettings.UiScale;
                if (_restoreLayout)
                {
                    _windowRect = new Rect(QuantumRelaySettings.WindowX, QuantumRelaySettings.WindowY,
                        QuantumRelaySettings.WindowWidth, QuantumRelaySettings.WindowHeight);
                    _lastSavedPosition = new Vector2(_windowRect.x, _windowRect.y);
                    _lastSavedSize = new Vector2(_windowRect.width, _windowRect.height);
                    _savePositionAfter = 0f;
                    _restoreLayout = false;
                }
            }
            Matrix4x4 oldMatrix = GUI.matrix;
            GUISkin oldSkin = GUI.skin;
            try
            {
            GUI.matrix = oldMatrix * Matrix4x4.Scale(new Vector3(_guiScale, _guiScale, 1f));
            if (_windowSkin == null)
            {
                _windowSkin = Instantiate(HighLogic.Skin);
                _windowSkin.label.wordWrap = true;
                _windowSkin.button.wordWrap = true;
            }
            GUI.skin = _windowSkin;
            ClampWindowSize();
            _windowRect = GUILayout.Window(
                WindowId,
                _windowRect,
                DrawWindow,
                "Quantum Relay " + QuantumRelayConstants.DisplayVersion,
                GUILayout.Width(_windowRect.width),
                GUILayout.Height(_windowRect.height),
                GUILayout.MaxWidth(_windowRect.width),
                GUILayout.MaxHeight(_windowRect.height));

            ClampWindowSize();
            _windowRect.x = Mathf.Clamp(
                _windowRect.x,
                0f,
                Mathf.Max(0f, Screen.width / _guiScale - _windowRect.width));
            _windowRect.y = Mathf.Clamp(
                _windowRect.y,
                0f,
                Mathf.Max(0f, Screen.height / _guiScale - _windowRect.height));
            QueueWindowPositionSave();
            if (_page == Page.Map && !string.IsNullOrEmpty(GUI.tooltip))
            {
                if (_tooltipStyle == null) _tooltipStyle = new GUIStyle(GUI.skin.box) { wordWrap = true };
                float width = Mathf.Min(360f, Screen.width / _guiScale - 20f);
                float height = _tooltipStyle.CalcHeight(new GUIContent(GUI.tooltip), width);
                Vector2 pointer = Event.current.mousePosition;
                GUI.Box(new Rect(Mathf.Clamp(pointer.x + 12f, 0f, Screen.width / _guiScale - width),
                    Mathf.Clamp(pointer.y + 24f, 0f, Screen.height / _guiScale - height), width, height),
                    GUI.tooltip, _tooltipStyle);
            }
            }
            finally { GUI.matrix = oldMatrix; GUI.skin = oldSkin; }
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();
            DrawTabs();
            GUILayout.Space(5f);

            float scrollHeight = Mathf.Max(
                220f,
                _windowRect.height - 150f);
            _scroll = GUILayout.BeginScrollView(
                _scroll,
                GUILayout.Height(scrollHeight));
            switch (_page)
            {
                case Page.Status: DrawStatusPage(); break;
                case Page.Map: DrawNetworkMapPage(); break;
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
                SaveWindowPositionNow();
                _visible = false;
                if (_button != null) _button.SetFalse(false);
            }

            GUILayout.EndVertical();

            DrawResizeGrip();
            HandleResize();
            if (!_resizing)
                GUI.DragWindow(new Rect(0f, 0f, _windowRect.width - ResizeGripSize, 28f));
        }

        private void DrawResizeGrip()
        {
            Rect grip = new Rect(
                _windowRect.width - ResizeGripSize,
                _windowRect.height - ResizeGripSize,
                ResizeGripSize,
                ResizeGripSize);

            GUI.Box(grip, "↘");
        }

        private void HandleResize()
        {
            Event current = Event.current;
            int controlId = GUIUtility.GetControlID(
                ResizeControlHint,
                FocusType.Passive);

            Rect grip = new Rect(
                _windowRect.width - ResizeGripSize,
                _windowRect.height - ResizeGripSize,
                ResizeGripSize,
                ResizeGripSize);

            EventType eventType = current.GetTypeForControl(controlId);

            if (eventType == EventType.MouseDown &&
                current.button == 0 &&
                grip.Contains(current.mousePosition))
            {
                GUIUtility.hotControl = controlId;
                GUIUtility.keyboardControl = 0;
                _resizing = true;
                _resizeStartMouse = GUIUtility.GUIToScreenPoint(
                    current.mousePosition);
                _resizeStartSize = new Vector2(
                    _windowRect.width,
                    _windowRect.height);
                current.Use();
                return;
            }

            if (GUIUtility.hotControl != controlId || !_resizing)
                return;

            if (eventType == EventType.MouseDrag)
            {
                Vector2 screenMouse = GUIUtility.GUIToScreenPoint(
                    current.mousePosition);
                Vector2 delta = (screenMouse - _resizeStartMouse) / _guiScale;

                _windowRect.width = _resizeStartSize.x + delta.x;
                _windowRect.height = _resizeStartSize.y + delta.y;
                ClampWindowSize();
                current.Use();
                return;
            }

            // rawType remains MouseUp even when IMGUI changes the routed event
            // type because the pointer has moved outside the resize control.
            if (eventType == EventType.MouseUp ||
                current.rawType == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
                _resizing = false;
                SaveWindowPositionNow();
                current.Use();
            }
        }

        private void ClampWindowSize()
        {
            float maximumWidth = Mathf.Max(
                MinimumWindowWidth,
                Screen.width / _guiScale - 20f);
            float maximumHeight = Mathf.Max(
                MinimumWindowHeight,
                Screen.height / _guiScale - 20f);

            _windowRect.width = Mathf.Clamp(
                _windowRect.width,
                MinimumWindowWidth,
                maximumWidth);
            _windowRect.height = Mathf.Clamp(
                _windowRect.height,
                MinimumWindowHeight,
                maximumHeight);
        }

        private void DrawTabs()
        {
            Page previousPage = _page;
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_page == Page.Status, "Status", GUI.skin.button)) _page = Page.Status;
            if (GUILayout.Toggle(_page == Page.Map, "Map", GUI.skin.button)) _page = Page.Map;
            if (GUILayout.Toggle(_page == Page.Settings, "Settings", GUI.skin.button)) _page = Page.Settings;
            if (GUILayout.Toggle(_page == Page.Diagnostics, "Diagnostics", GUI.skin.button)) _page = Page.Diagnostics;
            if (GUILayout.Toggle(_page == Page.About, "About", GUI.skin.button)) _page = Page.About;
            GUILayout.EndHorizontal();
            if (_page != previousPage) _scroll = Vector2.zero;
        }

        private void DrawStatusPage()
        {
            bool flight = HighLogic.LoadedSceneIsFlight;
            DrawBridgeHeader(flight);
            GUILayout.Space(6f);
            DrawNetworkControls();
            GUILayout.Space(6f);

            if (flight)
                DrawNetworkLinks();
            else
                DrawRegistryNetworkLinks();

            GUILayout.Space(7f);
            DrawNetworkStatistics(flight);
            GUILayout.Space(7f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(flight ? "Refresh Network" : "Refresh Telemetry"))
            {
                if (flight) QuantumRelayCommands.RequestRefresh();
                else QuantumRelayRegistry.Reload();
                _localTicker = flight
                    ? "Gateway scan requested."
                    : "Mission Control telemetry reloaded.";
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

        }

        private static void DrawBridgeHeader(bool flight)
        {
            bool online = flight ? QuantumRelayRuntimeState.Online : QuantumRelayRegistry.Online;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(flight ? "RoosterWorks Quantum Link" : "Quantum Relay Mission Control");

            Color old = GUI.contentColor;
            GUI.contentColor = online ? Color.green : new Color(1f, 0.75f, 0.2f);
            GUILayout.Label(online ? "[ONLINE] NETWORK ONLINE" : (QuantumRelayRegistry.HasTelemetry ? "[OFFLINE] LAST KNOWN" : "[WAITING] AWAITING TELEMETRY"));
            GUI.contentColor = old;

            if (!flight)
                GUILayout.Label("Last telemetry: " + QuantumRelayRegistry.AgeText());
            if (flight)
                GUILayout.Label("Active wormholes: " + QuantumRelayRuntimeState.ActiveLinkCount);

            if (flight)
            {
                DrawFlightPowerSummary();
            }
            else
            {
                DrawRegistryPowerSummary();
            }
            GUILayout.EndVertical();
        }

        private void DrawNetworkControls()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            DrawSectionTitle("MISSION CONTROL FILTERS");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(55f));
            _networkFilter = GUILayout.TextField(
                _networkFilter ?? string.Empty,
                GUILayout.MinWidth(140f));
            if (GUILayout.Button("Clear", GUILayout.Width(55f)))
                _networkFilter = string.Empty;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Sort", GUILayout.Width(55f));
            if (GUILayout.Toggle(_networkSort == NetworkSort.Name, "Name", GUI.skin.button, GUILayout.Width(75f)))
                _networkSort = NetworkSort.Name;
            if (GUILayout.Toggle(_networkSort == NetworkSort.Gateway, "Gateway", GUI.skin.button, GUILayout.Width(85f)))
                _networkSort = NetworkSort.Gateway;
            _onlineOnly = GUILayout.Toggle(_onlineOnly, "Online only");
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private List<QuantumRelay.Core.ActiveQuantumLink> GetVisibleFlightNetworks()
        {
            List<QuantumRelay.Core.ActiveQuantumLink> result =
                new List<QuantumRelay.Core.ActiveQuantumLink>();
            IList<QuantumRelay.Core.ActiveQuantumLink> links =
                QuantumRelayRuntimeState.Links;

            if (links != null)
            {
                for (int i = 0; i < links.Count; i++)
                {
                    QuantumRelay.Core.ActiveQuantumLink link = links[i];
                    if (link == null || (_onlineOnly && !link.Online))
                        continue;
                    if (!MatchesFilter(
                        link.SafeDisplayName,
                        GetLinkVesselName(link.GatewayA),
                        GetLinkVesselName(link.GatewayB),
                        GetLinkEndpointName(link.GatewayA),
                        GetLinkEndpointName(link.GatewayB)))
                        continue;
                    result.Add(link);
                }
            }

            result.Sort(CompareFlightNetworks);
            return result;
        }

        private List<NetworkTelemetry> GetVisibleRegistryNetworks()
        {
            List<NetworkTelemetry> result = new List<NetworkTelemetry>();
            IList<NetworkTelemetry> networks = QuantumRelayRegistry.Networks;

            if (networks != null)
            {
                for (int i = 0; i < networks.Count; i++)
                {
                    NetworkTelemetry network = networks[i];
                    if (network == null || (_onlineOnly && !network.Online))
                        continue;
                    if (!MatchesFilter(
                        network.DisplayName,
                        GetTelemetryVesselName(network.GatewayA),
                        GetTelemetryVesselName(network.GatewayB),
                        GetTelemetryEndpointName(network.GatewayA),
                        GetTelemetryEndpointName(network.GatewayB)))
                        continue;
                    result.Add(network);
                }
            }

            result.Sort(CompareRegistryNetworks);
            return result;
        }

        private bool MatchesFilter(params string[] values)
        {
            string filter = (_networkFilter ?? string.Empty).Trim();
            if (filter.Length == 0)
                return true;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]) &&
                    values[i].IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private int CompareFlightNetworks(
            QuantumRelay.Core.ActiveQuantumLink left,
            QuantumRelay.Core.ActiveQuantumLink right)
        {
            if (_networkSort == NetworkSort.Gateway)
            {
                int gateway = string.Compare(
                    GetLinkVesselName(left.GatewayA),
                    GetLinkVesselName(right.GatewayA),
                    StringComparison.OrdinalIgnoreCase);
                if (gateway != 0) return gateway;
            }

            return string.Compare(
                left.SafeDisplayName,
                right.SafeDisplayName,
                StringComparison.OrdinalIgnoreCase);
        }

        private int CompareRegistryNetworks(NetworkTelemetry left, NetworkTelemetry right)
        {
            if (_networkSort == NetworkSort.Gateway)
            {
                int gateway = string.Compare(
                    GetTelemetryVesselName(left.GatewayA),
                    GetTelemetryVesselName(right.GatewayA),
                    StringComparison.OrdinalIgnoreCase);
                if (gateway != 0) return gateway;
            }

            return string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.OrdinalIgnoreCase);
        }

        private void DrawRegistryNetworkLinks()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            DrawSectionTitle("SAVED QUANTUM NETWORKS");

            List<NetworkTelemetry> networks = GetVisibleRegistryNetworks();
            if (networks.Count == 0)
            {
                GUILayout.Label("No saved network telemetry.");
                GUILayout.EndVertical();
                return;
            }

            for (int i = 0; i < networks.Count; i++)
            {
                NetworkTelemetry item = networks[i];
                if (item == null)
                    continue;

                string itemKey = GetNetworkKey(item.Id, item.NetworkId, i);
                GetExpanded(_expandedRegistryNetworks, itemKey);
            }

            DrawExpandCollapseControls(_expandedRegistryNetworks, networks.Count);

            for (int i = 0; i < networks.Count; i++)
            {
                NetworkTelemetry network = networks[i];
                if (network == null)
                    continue;

                string key = GetNetworkKey(network.Id, network.NetworkId, i);
                bool expanded = GetExpanded(_expandedRegistryNetworks, key);
                string status = network.Online ? "● Online" : "● Offline";
                string arrow = expanded ? "v" : ">";
                string heading = arrow + "  " + status + "  " + SafeName(network.DisplayName);

                Color old = GUI.contentColor;
                GUI.contentColor = network.Online
                    ? Color.green
                    : new Color(1f, 0.75f, 0.2f);

                if (GUILayout.Button(heading, GUI.skin.button))
                    _expandedRegistryNetworks[key] = !expanded;

                GUI.contentColor = old;

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(
                    GetTelemetryEndpointName(network.GatewayA) +
                    "  <->  " +
                    GetTelemetryEndpointName(network.GatewayB));

                GUILayout.Label(network.Online && network.GatewayA != null && network.GatewayB != null
                    ? GetTelemetryVesselName(network.GatewayA) + "  <->  " +
                      GetTelemetryVesselName(network.GatewayB) + "    Signal: " +
                      FormatPercent(Math.Min(
                          network.GatewayA.HasQuantumRelayModule ? network.GatewayA.RelaySignalStrength : 0.25,
                          network.GatewayB.HasQuantumRelayModule ? network.GatewayB.RelaySignalStrength : 0.25))
                    : "No Partner Connected    Signal: unavailable");

                if (expanded)
                {
                    GUILayout.Space(3f);
                    GUILayout.Label(
                        "State: " +
                        (string.IsNullOrEmpty(network.Reason)
                            ? (network.Online ? "ready" : "offline")
                            : network.Reason));

                    if (network.GatewayA != null && network.GatewayB != null)
                    {
                        double aStrength = network.GatewayA.HasQuantumRelayModule
                            ? network.GatewayA.RelaySignalStrength
                            : 0.25;
                        double bStrength = network.GatewayB.HasQuantumRelayModule
                            ? network.GatewayB.RelaySignalStrength
                            : 0.25;
                        double effective = Math.Min(aStrength, bStrength);
                        GUILayout.Label("Bridge strength: " + FormatPercent(effective));

                        if (Math.Abs(aStrength - bStrength) > 0.001)
                        {
                            GUILayout.Label(
                                "Limited by: " +
                                (aStrength < bStrength
                                    ? GetTelemetryVesselName(network.GatewayA)
                                    : GetTelemetryVesselName(network.GatewayB)));
                        }
                    }

                    GUILayout.Space(4f);
                    DrawGatewayTelemetry("GATEWAY A", network.GatewayA);
                    GUILayout.Space(3f);
                    DrawGatewayTelemetry("GATEWAY B", network.GatewayB);
                }

                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }

            GUILayout.EndVertical();
        }

        private static void DrawRegistryPowerSummary()
        {
            IList<NetworkTelemetry> networks = QuantumRelayRegistry.Networks;
            Dictionary<Guid, double> uniqueGatewayPower =
                new Dictionary<Guid, double>();

            if (networks != null)
            {
                for (int i = 0; i < networks.Count; i++)
                {
                    NetworkTelemetry network = networks[i];
                    if (network == null)
                        continue;

                    AddTelemetryPower(uniqueGatewayPower, network.GatewayA);
                    AddTelemetryPower(uniqueGatewayPower, network.GatewayB);
                }
            }

            double totalPower = 0.0;
            foreach (double power in uniqueGatewayPower.Values)
                totalPower += power;

            GUILayout.Label(
                "Last known relay draw: " +
                FormatNumber(totalPower) +
                " EC/s across " +
                uniqueGatewayPower.Count +
                (uniqueGatewayPower.Count == 1 ? " gateway" : " gateways"));

            if (networks == null)
                return;

            for (int i = 0; i < networks.Count; i++)
            {
                NetworkTelemetry network = networks[i];
                if (network == null)
                    continue;

                double networkPower = GetTelemetryPower(network.GatewayA);
                if (!SameTelemetryVessel(network.GatewayA, network.GatewayB))
                    networkPower += GetTelemetryPower(network.GatewayB);

                GUILayout.Label(
                    SafeName(network.DisplayName) +
                    ": " +
                    FormatNumber(networkPower) +
                    " EC/s");
            }
        }

        private static void AddTelemetryPower(
            IDictionary<Guid, double> powers,
            GatewayTelemetry gateway)
        {
            if (powers == null || gateway == null || !gateway.IsKnown)
                return;

            Guid vesselId = gateway.VesselId;
            if (vesselId == Guid.Empty)
                return;

            if (!powers.ContainsKey(vesselId))
                powers.Add(vesselId, GetTelemetryPower(gateway));
        }

        private static double GetTelemetryPower(GatewayTelemetry gateway)
        {
            return gateway != null
                ? Math.Max(0.0, gateway.RelayPowerRate)
                : 0.0;
        }

        private static bool SameTelemetryVessel(
            GatewayTelemetry a,
            GatewayTelemetry b)
        {
            return a != null &&
                   b != null &&
                   a.VesselId != Guid.Empty &&
                   b.VesselId != Guid.Empty &&
                   a.VesselId == b.VesselId;
        }

        private static string GetTelemetryEndpointName(GatewayTelemetry gateway)
        {
            return gateway != null && gateway.IsKnown
                ? SafeName(gateway.EndpointName)
                : "Unassigned";
        }

        private static string GetTelemetryVesselName(GatewayTelemetry gateway)
        {
            return gateway != null && gateway.IsKnown
                ? SafeName(gateway.VesselName)
                : "No gateway";
        }

        private void DrawNetworkLinks()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            DrawSectionTitle("LIVE QUANTUM NETWORKS");

            List<QuantumRelay.Core.ActiveQuantumLink> links =
                GetVisibleFlightNetworks();

            if (links.Count == 0)
            {
                GUILayout.Label("No wormhole links configured.");
                GUILayout.EndVertical();
                return;
            }

            for (int i = 0; i < links.Count; i++)
            {
                QuantumRelay.Core.ActiveQuantumLink item = links[i];
                if (item == null)
                    continue;

                string itemKey = GetNetworkKey(item.Id, item.NetworkId, i);
                GetExpanded(_expandedFlightNetworks, itemKey);
            }

            DrawExpandCollapseControls(_expandedFlightNetworks, links.Count);

            for (int i = 0; i < links.Count; i++)
            {
                QuantumRelay.Core.ActiveQuantumLink link = links[i];
                if (link == null)
                    continue;

                string key = GetNetworkKey(link.Id, link.NetworkId, i);
                bool expanded = GetExpanded(_expandedFlightNetworks, key);
                string status = link.Online ? "● Online" : "● Offline";
                string arrow = expanded ? "v" : ">";
                string heading = arrow + "  " + status + "  " + SafeName(link.SafeDisplayName);

                Color old = GUI.contentColor;
                GUI.contentColor = link.Online
                    ? Color.green
                    : new Color(1f, 0.75f, 0.2f);

                if (GUILayout.Button(heading, GUI.skin.button))
                    _expandedFlightNetworks[key] = !expanded;

                GUI.contentColor = old;

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(
                    GetLinkEndpointName(link.GatewayA) +
                    "  <->  " +
                    GetLinkEndpointName(link.GatewayB));

                GUILayout.Label(link.Online && link.HasValidGateways
                    ? GetLinkVesselName(link.GatewayA) + "  <->  " +
                      GetLinkVesselName(link.GatewayB) + "    Signal: " +
                      FormatPercent(Math.Min(
                          link.GatewayA.HasQuantumRelayModule ? link.GatewayA.RelaySignalStrength : 0.25,
                          link.GatewayB.HasQuantumRelayModule ? link.GatewayB.RelaySignalStrength : 0.25))
                    : "No Partner Connected    Signal: unavailable");

                if (expanded)
                {
                    GUILayout.Space(3f);
                    GUILayout.Label(
                        "State: " +
                        (string.IsNullOrEmpty(link.Reason)
                            ? (link.Online ? "ready" : "offline")
                            : link.Reason));

                    if (link.GatewayA != null && link.GatewayB != null)
                    {
                        double aStrength = link.GatewayA.HasQuantumRelayModule
                            ? link.GatewayA.RelaySignalStrength
                            : 0.25;
                        double bStrength = link.GatewayB.HasQuantumRelayModule
                            ? link.GatewayB.RelaySignalStrength
                            : 0.25;
                        double effective = Math.Min(aStrength, bStrength);
                        GUILayout.Label("Bridge strength: " + FormatPercent(effective));

                        if (Math.Abs(aStrength - bStrength) > 0.001)
                        {
                            GUILayout.Label(
                                "Limited by: " +
                                (aStrength < bStrength
                                    ? GetLinkVesselName(link.GatewayA)
                                    : GetLinkVesselName(link.GatewayB)));
                        }
                    }

                    GUILayout.Space(4f);
                    DrawGatewaySummary("GATEWAY A", link.GatewayA);
                    GUILayout.Space(3f);
                    DrawGatewaySummary("GATEWAY B", link.GatewayB);
                }

                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }

            GUILayout.EndVertical();
        }

        private static void DrawSectionTitle(string title)
        {
            Color old = GUI.contentColor;
            GUI.contentColor = new Color(0.55f, 0.9f, 1f);
            GUILayout.Label(title);
            GUI.contentColor = old;
        }

        private static string GetNetworkKey(
            string id,
            string networkId,
            int index)
        {
            if (!string.IsNullOrEmpty(id))
                return id;
            if (!string.IsNullOrEmpty(networkId))
                return networkId;
            return "network-" + index;
        }

        private static bool GetExpanded(
            IDictionary<string, bool> states,
            string key)
        {
            bool expanded;
            if (states.TryGetValue(key, out expanded))
                return expanded;

            states[key] = false;
            return false;
        }

        private static void DrawExpandCollapseControls(
            IDictionary<string, bool> states,
            int networkCount)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                networkCount + (networkCount == 1 ? " network" : " networks"));

            if (GUILayout.Button("Expand All", GUILayout.Width(90f)))
                SetAllExpanded(states, true);

            if (GUILayout.Button("Collapse All", GUILayout.Width(90f)))
                SetAllExpanded(states, false);

            GUILayout.EndHorizontal();
            GUILayout.Space(3f);
        }

        private static void SetAllExpanded(
            IDictionary<string, bool> states,
            bool expanded)
        {
            List<string> keys = new List<string>(states.Keys);
            for (int i = 0; i < keys.Count; i++)
                states[keys[i]] = expanded;
        }

        private static void DrawNetworkStatistics(bool flight)
        {
            int networks = 0;
            int online = 0;
            int gateways = 0;
            double power = 0.0;
            HashSet<Guid> gatewayIds = new HashSet<Guid>();

            if (flight)
            {
                IList<QuantumRelay.Core.ActiveQuantumLink> links =
                    QuantumRelayRuntimeState.Links;

                if (links != null)
                {
                    for (int i = 0; i < links.Count; i++)
                    {
                        QuantumRelay.Core.ActiveQuantumLink link = links[i];
                        if (link == null)
                            continue;

                        networks++;
                        if (link.Online)
                            online++;

                        AddFlightGatewayStatistic(link.GatewayA, gatewayIds, ref power);
                        AddFlightGatewayStatistic(link.GatewayB, gatewayIds, ref power);
                    }
                }
            }
            else
            {
                IList<NetworkTelemetry> saved = QuantumRelayRegistry.Networks;
                if (saved != null)
                {
                    for (int i = 0; i < saved.Count; i++)
                    {
                        NetworkTelemetry network = saved[i];
                        if (network == null)
                            continue;

                        networks++;
                        if (network.Online)
                            online++;

                        AddRegistryGatewayStatistic(network.GatewayA, gatewayIds, ref power);
                        AddRegistryGatewayStatistic(network.GatewayB, gatewayIds, ref power);
                    }
                }
            }

            gateways = gatewayIds.Count;

            GUILayout.BeginVertical(GUI.skin.box);
            DrawSectionTitle("NETWORK SUMMARY");
            GUILayout.BeginHorizontal();
            GUILayout.Label(networks + " networks | " + online + " online | " +
                Math.Max(0, networks - online) + " offline | " + gateways + " gateways");
            GUILayout.EndHorizontal();
            GUILayout.Label(
                (flight ? "Live" : "Last known") +
                " relay draw: " +
                FormatNumber(power) +
                " EC/s");
            GUILayout.EndVertical();
        }

        private static void DrawStatistic(string label, string value)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(value);
            GUILayout.Label(label);
            GUILayout.EndVertical();
        }

        private static void AddFlightGatewayStatistic(
            GatewayCandidate gateway,
            ISet<Guid> ids,
            ref double power)
        {
            if (gateway == null || gateway.Vessel == null)
                return;

            Guid id = gateway.Vessel.id;
            if (id == Guid.Empty || !ids.Add(id))
                return;

            power += Math.Max(0.0, gateway.RelayPowerRate);
        }

        private static void AddRegistryGatewayStatistic(
            GatewayTelemetry gateway,
            ISet<Guid> ids,
            ref double power)
        {
            if (gateway == null || !gateway.IsKnown)
                return;

            Guid id = gateway.VesselId;
            if (id == Guid.Empty || !ids.Add(id))
                return;

            power += Math.Max(0.0, gateway.RelayPowerRate);
        }

        private static void DrawFlightPowerSummary()
        {
            IList<QuantumRelay.Core.ActiveQuantumLink> links =
                QuantumRelayRuntimeState.Links;

            Dictionary<Guid, double> uniqueGatewayPower =
                new Dictionary<Guid, double>();

            if (links != null)
            {
                for (int i = 0; i < links.Count; i++)
                {
                    QuantumRelay.Core.ActiveQuantumLink link = links[i];
                    if (link == null)
                        continue;

                    AddGatewayPower(uniqueGatewayPower, link.GatewayA);
                    AddGatewayPower(uniqueGatewayPower, link.GatewayB);
                }
            }

            double totalPower = 0.0;
            foreach (double power in uniqueGatewayPower.Values)
                totalPower += power;

            GUILayout.Label(
                "Live relay draw: " +
                FormatNumber(totalPower) +
                " EC/s across " +
                uniqueGatewayPower.Count +
                (uniqueGatewayPower.Count == 1 ? " gateway" : " gateways"));

            if (links == null)
                return;

            for (int i = 0; i < links.Count; i++)
            {
                QuantumRelay.Core.ActiveQuantumLink link = links[i];
                if (link == null)
                    continue;

                double networkPower = GetGatewayPower(link.GatewayA);
                if (!SameGatewayVessel(link.GatewayA, link.GatewayB))
                    networkPower += GetGatewayPower(link.GatewayB);

                GUILayout.Label(
                    SafeName(link.SafeDisplayName) +
                    ": " +
                    FormatNumber(networkPower) +
                    " EC/s");
            }
        }

        private static void AddGatewayPower(
            IDictionary<Guid, double> powers,
            GatewayCandidate gateway)
        {
            if (powers == null || gateway == null || gateway.Vessel == null)
                return;

            Guid vesselId = gateway.Vessel.id;
            if (!powers.ContainsKey(vesselId))
                powers.Add(vesselId, GetGatewayPower(gateway));
        }

        private static double GetGatewayPower(GatewayCandidate gateway)
        {
            return gateway != null
                ? Math.Max(0.0, gateway.RelayPowerRate)
                : 0.0;
        }

        private static bool SameGatewayVessel(
            GatewayCandidate a,
            GatewayCandidate b)
        {
            return a != null &&
                   b != null &&
                   a.Vessel != null &&
                   b.Vessel != null &&
                   a.Vessel.id == b.Vessel.id;
        }

        private static string GetLinkEndpointName(GatewayCandidate gateway)
        {
            return gateway != null && gateway.Wormhole != null
                ? SafeName(gateway.Wormhole.Name)
                : "Unassigned";
        }

        private static string GetLinkVesselName(GatewayCandidate gateway)
        {
            return gateway != null && gateway.Vessel != null
                ? SafeName(gateway.Vessel.vesselName)
                : "No gateway";
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
            GUILayout.Label("Hardware signal strength: " +
                FormatPercent(gateway.HasQuantumRelayModule ? gateway.RelaySignalStrength : 0.25));
            Color old = GUI.contentColor;
            GUI.contentColor = gateway.IsValid ? Color.green : Color.yellow;
            GUILayout.Label(gateway.IsValid ? "[READY]" : "[WAITING]");
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
            GUILayout.Label(gateway.Ready ? "[READY] LAST KNOWN" : "[WAITING] LAST KNOWN");
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

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Quantum Relay manages gateway distance automatically.");
            GUILayout.Label("Version: " + QuantumRelayConstants.DisplayVersion);
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            _draftAutoRebuild = GUILayout.Toggle(_draftAutoRebuild, "Automatically rebuild CommNet after changes");
            _draftMessages = GUILayout.Toggle(_draftMessages, "Show screen messages");
            _draftDebug = GUILayout.Toggle(_draftDebug, "Enable detailed gateway logging");
            GUILayout.Space(8f);
            GUILayout.Label("Flight badge");
            _draftShowHud = GUILayout.Toggle(_draftShowHud, "Show flight route badge");
            _draftFollowStock = GUILayout.Toggle(_draftFollowStock, "Keep badge below stock communications bar");
            if (!_draftFollowStock)
            {
                DrawStepSetting("Badge horizontal position (pixels)", _draftHudX.ToString("0"),
                    () => _draftHudX = Mathf.Max(0f, _draftHudX - 10f), () => _draftHudX += 10f);
                DrawStepSetting("Badge vertical position (pixels)", _draftHudY.ToString("0"),
                    () => _draftHudY = Mathf.Max(0f, _draftHudY - 10f), () => _draftHudY += 10f);
            }
            DrawStepSetting("Menu scale", (_draftUiScale * 100f).ToString("0") + "%",
                () => _draftUiScale = Mathf.Max(0.75f, _draftUiScale - 0.05f),
                () => _draftUiScale = Mathf.Min(1.5f, _draftUiScale + 0.05f));
            GUILayout.Label("Display changes take effect after Apply and Save. Window layout saves automatically.");
            GUILayout.Space(8f);

            if (GUILayout.Button("Apply and Save"))
            {
                SaveWindowPositionNow();
                QuantumRelaySettings.ApplyDisplay(_draftShowHud, _draftFollowStock,
                    _draftHudX, _draftHudY, _draftUiScale);
                bool networkChanged = QuantumRelaySettings.Apply(
                    _draftAutoRebuild, _draftMessages, _draftDebug, true);
                if (networkChanged)
                {
                    QuantumRelayCommands.RequestRefresh();
                    if (QuantumRelaySettings.AutoRebuildCommNet) QuantumRelayCommands.RequestRebuild();
                }
                _localTicker = QuantumRelaySettings.LastSaveSucceeded ? "Settings saved." : "Settings applied; file save failed. Check KSP.log.";
                QuantumRelayNotifications.Post("settings-save", _localTicker, false);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload"))
            {
                QuantumRelaySettings.Load();
                CopySettingsToDraft();
                _restoreLayout = true;
                QuantumRelayCommands.RequestRefresh();
                if (QuantumRelaySettings.AutoRebuildCommNet) QuantumRelayCommands.RequestRebuild();
                _localTicker = "Settings reloaded.";
            }
            if (GUILayout.Button("Reset Defaults"))
            {
                QuantumRelaySettings.ResetDefaults(true);
                CopySettingsToDraft();
                _restoreLayout = true;
                QuantumRelayCommands.RequestRefresh();
                QuantumRelayCommands.RequestRebuild();
                _localTicker = QuantumRelaySettings.LastSaveSucceeded ? "Default settings restored and saved." : "Defaults restored; file save failed. Check KSP.log.";
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

        private static bool _showDetailedDiagnostics;

        private void DrawDiagnosticsPage()
        {
            GUILayout.Label("Quantum Network Diagnostics");
            IList<NetworkTelemetry> networks = QuantumRelayRegistry.Networks;
            int online = 0;
            foreach (NetworkTelemetry network in networks)
                if (network != null && network.Online) online++;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Network health");
            GUILayout.Label("Active quantum links: " + online + " / " + networks.Count);
            GUILayout.Label("CommNet hook (current scene): " +
                (!HighLogic.LoadedSceneIsFlight ? "FLIGHT-ONLY CHECK" :
                 CommNetNetworkInstaller.IsInstalled ? "INSTALLED" : "NOT INSTALLED"));
            GUILayout.Label("Telemetry: " +
                (QuantumRelayRegistry.HasTelemetry ? "AVAILABLE" : "NOT AVAILABLE") +
                " | Age: " + QuantumRelayRegistry.AgeText());
            GUILayout.Label(HighLogic.LoadedSceneIsFlight
                ? "Hook installation does not prove an active routed quantum edge."
                : "Saved telemetry shown. Live CommNet hook checks require flight.");
            GUILayout.EndVertical();

            foreach (NetworkTelemetry network in networks)
            {
                if (network == null) continue;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(SafeName(network.DisplayName) + " — " +
                    (network.Online ? "ONLINE" : "OFFLINE"));
                GUILayout.Label("Gateway A: " + GetTelemetryVesselName(network.GatewayA));
                GUILayout.Label("Gateway B: " + GetTelemetryVesselName(network.GatewayB));
                if (network.Online && network.GatewayA != null && network.GatewayB != null)
                    GUILayout.Label("Combined link signal: " + FormatPercent(Math.Min(
                        network.GatewayA.HasQuantumRelayModule ? network.GatewayA.RelaySignalStrength : 0.25,
                        network.GatewayB.HasQuantumRelayModule ? network.GatewayB.RelaySignalStrength : 0.25)));
                else
                    GUILayout.Label("Link signal: unavailable | Reason: " + SafeName(network.Reason));
                GUILayout.EndVertical();
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Gateway readiness");
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (QuantumRelayRuntimeState.Links.Count == 0)
                    GUILayout.Label("No live gateway telemetry yet.");
                foreach (var link in QuantumRelayRuntimeState.Links)
                {
                    if (link == null) continue;
                    DrawFlightGatewayFoldout(link.Id + ":A", link.SafeDisplayName + " / Gateway A", link.GatewayA);
                    DrawFlightGatewayFoldout(link.Id + ":B", link.SafeDisplayName + " / Gateway B", link.GatewayB);
                }
            }
            else
            {
                GUILayout.Label("Last-known readiness; live checks require flight.");
                foreach (NetworkTelemetry network in networks)
                {
                    if (network == null) continue;
                    DrawSavedGatewayFoldout(network.Id + ":A", SafeName(network.DisplayName) + " / Gateway A", network.GatewayA);
                    DrawSavedGatewayFoldout(network.Id + ":B", SafeName(network.DisplayName) + " / Gateway B", network.GatewayB);
                }
            }
            GUILayout.EndVertical();

            _showDetailedDiagnostics = GUILayout.Toggle(_showDetailedDiagnostics,
                "Show detailed runtime diagnostics");
            if (_showDetailedDiagnostics)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Bridge runtime");
                GUILayout.Label("Registered quantum pairs: " + QuantumGatewayManager.ActivePairCount);
                GUILayout.Label("Gateway radius: " +
                    FormatNumber(QuantumRelaySettings.GatewayRadiusMetres / 1000.0) + " km");
                if (HighLogic.LoadedSceneIsFlight) DrawFlightPowerSummary();
                else GUILayout.Label("Live power totals require flight.");
                GUILayout.Label("Last runtime update UT: " +
                    FormatNumber(QuantumRelayRuntimeState.UpdatedUt));
                GUILayout.EndVertical();
            }

            GUILayout.BeginVertical(GUI.skin.box);
            _showEventHistory = GUILayout.Toggle(_showEventHistory, "Recent events", GUI.skin.button);
            if (_showEventHistory)
                foreach (string item in QuantumRelayNotifications.History)
                    GUILayout.Label(item);
            GUILayout.EndVertical();
        }

        private void DrawFlightGatewayFoldout(string key, string heading, GatewayCandidate gateway)
        {
            bool ready = gateway != null && gateway.IsValid;
            string summary = gateway == null || gateway.Vessel == null ? "Unavailable" :
                SafeName(gateway.Vessel.vesselName) + " | " + SafeName(gateway.RelayOperationalState) +
                " | " + FormatNumber(gateway.RelayPowerRate) + " EC/s";
            if (DrawGatewayFoldout("live:" + key, heading, summary, ready))
                DrawGatewayDiagnostics(heading, gateway);
        }

        private void DrawSavedGatewayFoldout(string key, string heading, GatewayTelemetry gateway)
        {
            string summary = gateway == null ? "No telemetry" :
                SafeName(gateway.VesselName) + " | " + SafeName(gateway.RelayOperationalState) +
                " | " + FormatNumber(gateway.RelayPowerRate) + " EC/s (saved)";
            if (DrawGatewayFoldout("saved:" + key, heading, summary, gateway != null && gateway.Ready))
                DrawGatewayTelemetry(heading, gateway);
        }

        private bool DrawGatewayFoldout(string key, string heading, string summary, bool ready)
        {
            bool expanded;
            _expandedDiagnostics.TryGetValue(key, out expanded);
            Color old = GUI.contentColor;
            GUI.contentColor = ready ? Color.green : new Color(1f, 0.75f, 0.2f);
            if (GUILayout.Button((expanded ? "v " : "> ") + heading + "\n" + summary))
                expanded = !expanded;
            GUI.contentColor = old;
            _expandedDiagnostics[key] = expanded;
            return expanded;
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
            GUILayout.Label("Version " + QuantumRelayConstants.DisplayVersion);
            GUILayout.Space(6f);
            GUILayout.Label("Developed by SockedRooster");
            GUILayout.Label("RoosterWorks");
            GUILayout.Label("MIT License");
            GUILayout.Space(8f);
            GUILayout.Label("True stock CommNet routing through multiple Promised Worlds wormhole pairs.");
            GUILayout.Space(8f);
            GUILayout.Label("Built for Kerbal Space Program 1.12.5");
            GUILayout.EndVertical();
        }

        private void CopySettingsToDraft()
        {
            _draftAutoRebuild = QuantumRelaySettings.AutoRebuildCommNet;
            _draftMessages = QuantumRelaySettings.ShowScreenMessages;
            _draftDebug = QuantumRelaySettings.DebugLogging;
            _draftShowHud = QuantumRelaySettings.ShowFlightHud;
            _draftFollowStock = QuantumRelaySettings.HudFollowStock;
            _draftHudX = QuantumRelaySettings.HudX;
            _draftHudY = QuantumRelaySettings.HudY;
            _draftUiScale = QuantumRelaySettings.UiScale;
        }

        private void SetLocalStatus(string message)
        {
            _localTicker = message;
            QuantumRelayNotifications.Post("gui-status", message, false);
        }

        private void QueueWindowPositionSave()
        {
            Vector2 currentPosition = new Vector2(_windowRect.x, _windowRect.y);
            Vector2 currentSize = new Vector2(_windowRect.width, _windowRect.height);

            bool positionChanged =
                (currentPosition - _lastSavedPosition).sqrMagnitude > 1f;
            bool sizeChanged =
                (currentSize - _lastSavedSize).sqrMagnitude > 1f;

            // Start the debounce once per change. Resetting the deadline on
            // every OnGUI event prevented the window from ever being saved.
            if ((positionChanged || sizeChanged) && _savePositionAfter <= 0f)
                _savePositionAfter = Time.realtimeSinceStartup + 0.75f;

            if (_savePositionAfter > 0f &&
                Time.realtimeSinceStartup >= _savePositionAfter)
            {
                SaveWindowPositionNow();
            }
        }

        private void SaveWindowPositionNow()
        {
            if (_restoreLayout) return;
            Vector2 currentPosition = new Vector2(_windowRect.x, _windowRect.y);
            Vector2 currentSize = new Vector2(_windowRect.width, _windowRect.height);

            bool positionChanged =
                (currentPosition - _lastSavedPosition).sqrMagnitude > 1f;
            bool sizeChanged =
                (currentSize - _lastSavedSize).sqrMagnitude > 1f;

            if (!positionChanged && !sizeChanged)
            {
                _savePositionAfter = 0f;
                return;
            }

            QuantumRelaySettings.SaveWindowLayout(
                currentPosition.x,
                currentPosition.y,
                currentSize.x,
                currentSize.y);

            if (!QuantumRelaySettings.LastSaveSucceeded)
            {
                _savePositionAfter = Time.realtimeSinceStartup + 5f;
                return;
            }

            _lastSavedPosition = currentPosition;
            _lastSavedSize = currentSize;
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
            double clamped = Math.Max(0.0, Math.Min(1.25, fraction));
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


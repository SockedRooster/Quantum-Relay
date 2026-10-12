using System;
using CommNet;
using UnityEngine;

namespace QuantumRelay
{
    internal sealed partial class QuantumRelayGui
    {
        private string _mapSelection;
        private bool _mapSelectB;
        private GUIStyle _mapNodeStyle;
        private GUIStyle _mapLinkStyle;

        private void DrawNetworkMapPage()
        {
            bool flight = HighLogic.LoadedSceneIsFlight;
            GUILayout.Label("QUANTUM NETWORK MAP — schematic, not to scale");
            GUILayout.Label((flight ? "Latest flight telemetry" : "Saved Mission Control telemetry") +
                " | Age: " + QuantumRelayRegistry.AgeText());
            QuantumCommNetwork graph = flight && CommNetNetwork.Instance != null
                ? CommNetNetwork.Instance.CommNet as QuantumCommNetwork : null;
            Vessel craft = flight ? FlightGlobals.ActiveVessel : null;
            bool quantumRoute = flight && QuantumRouteDetector.UsesQuantumRoute(craft, graph);
            if (flight)
                GUILayout.Label("Active craft: " + (craft == null ? "None" : craft.vesselName) +
                    " | " + (quantumRoute ? "Quantum control route verified" :
                    craft == null || craft.connection == null ? "Connection unavailable" :
                    !craft.connection.IsConnected ? "No signal" : "No quantum control route verified"));
            else GUILayout.Label("Active-craft route highlighting is available in flight.");
            GUILayout.Label("Green: online | Amber: offline | Cyan: active route. Select a gateway for details.");
            DrawNetworkControls();
            var networks = GetVisibleRegistryNetworks();
            if (networks.Count == 0)
            {
                GUILayout.Label(QuantumRelayRegistry.Networks.Count == 0
                    ? "No network telemetry yet. Enter flight with configured gateways to populate the map."
                    : "No networks match these filters.");
                return;
            }
            if (_mapNodeStyle == null)
            {
                _mapNodeStyle = new GUIStyle(GUI.skin.button) { wordWrap = true, richText = false };
                _mapLinkStyle = new GUIStyle(GUI.skin.label) {
                    alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = false };
            }
            for (int i = 0; i < networks.Count; i++)
            {
                NetworkTelemetry network = networks[i];
                string key = GetNetworkKey(network.Id, network.NetworkId, i);
                bool highlighted = quantumRoute && network.Online && MapPairOnRoute(network, craft, graph);
                Color stateColor = highlighted ? Color.cyan : network.Online ? Color.green : new Color(1f, 0.75f, 0.2f);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(SafeName(network.DisplayName));
                bool stacked = _windowRect.width < 640f;
                Rect area = GUILayoutUtility.GetRect(0f, stacked ? 136f : 82f, GUILayout.ExpandWidth(true));
                float nodeWidth = stacked ? area.width : area.width * 0.35f;
                Rect left = new Rect(area.x, area.y + 4f, nodeWidth, stacked ? 44f : 68f);
                Rect right = stacked ? new Rect(area.x, area.y + 88f, nodeWidth, 44f) :
                    new Rect(area.xMax - nodeWidth, area.y + 4f, nodeWidth, 68f);
                Color oldColor = GUI.color;
                Color oldContent = GUI.contentColor;
                GUI.color = stateColor;
                GUI.DrawTexture(stacked ? new Rect(area.x, area.y + 82f, area.width, 3f) :
                    new Rect(left.xMax, area.y + 67f, right.x - left.xMax, 3f), Texture2D.whiteTexture);
                GUI.color = oldColor;
                GUI.contentColor = stateColor;
                string strength = network.GatewayA == null || network.GatewayB == null || !network.Online
                    ? "Unavailable" : FormatPercent(Math.Min(
                        network.GatewayA.HasQuantumRelayModule ? network.GatewayA.RelaySignalStrength : 0.25,
                        network.GatewayB.HasQuantumRelayModule ? network.GatewayB.RelaySignalStrength : 0.25));
                GUI.Label(stacked ? new Rect(area.x, area.y + 49f, area.width, 30f) :
                    new Rect(left.xMax + 3f, area.y + 4f, right.x - left.xMax - 6f, 60f),
                    (network.Online ? "ONLINE" : "OFFLINE") + (stacked ? " | " : "\n") + strength +
                    (highlighted ? (stacked ? " | ACTIVE ROUTE" : "\nACTIVE ROUTE") : ""), _mapLinkStyle);
                GUI.contentColor = _mapSelection == key && !_mapSelectB ? Color.cyan : oldContent;
                if (GUI.Button(left, MapNodeLabel("A", network.GatewayA, left.width), _mapNodeStyle))
                { _mapSelection = key; _mapSelectB = false; }
                GUI.contentColor = _mapSelection == key && _mapSelectB ? Color.cyan : oldContent;
                if (GUI.Button(right, MapNodeLabel("B", network.GatewayB, right.width), _mapNodeStyle))
                { _mapSelection = key; _mapSelectB = true; }
                GUI.contentColor = oldContent;
                if (!network.Online) GUILayout.Label("State: " + SafeName(network.Reason));
                if (_mapSelection == key)
                {
                    DrawGatewayTelemetry("Selected gateway " + (_mapSelectB ? "B" : "A") + " — telemetry snapshot",
                        _mapSelectB ? network.GatewayB : network.GatewayA);
                    if (GUILayout.Button("Close gateway details")) _mapSelection = null;
                }
                GUILayout.EndVertical();
            }
        }

        private GUIContent MapNodeLabel(string side, GatewayTelemetry gateway, float width)
        {
            if (gateway == null) return new GUIContent("Gateway " + side + "\nNo telemetry");
            string vessel = "Gateway " + side + " | " + SafeName(gateway.VesselName);
            string endpoint = SafeName(gateway.EndpointName);
            return new GUIContent(FitMapLabel(vessel, width) + "\n" + FitMapLabel(endpoint, width),
                vessel + "\nBody: " + SafeName(gateway.BodyName) + "\nEndpoint: " + endpoint);
        }

        private string FitMapLabel(string text, float width)
        {
            float available = Mathf.Max(1f, width - _mapNodeStyle.padding.horizontal - 8f);
            if (_mapNodeStyle.CalcSize(new GUIContent(text)).x <= available) return text;
            int length = text.Length;
            while (length > 0 && _mapNodeStyle.CalcSize(new GUIContent(text.Substring(0, length) + "…")).x > available)
                length--;
            return text.Substring(0, length) + "…";
        }

        private static bool MapPairOnRoute(NetworkTelemetry network, Vessel craft, QuantumCommNetwork graph)
        {
            if (network.GatewayA == null || network.GatewayB == null) return false;
            foreach (var link in QuantumRelayRuntimeState.Links)
            {
                if (link == null || !link.Online || link.GatewayA == null || link.GatewayB == null) continue;
                Vessel a = link.GatewayA.Vessel;
                Vessel b = link.GatewayB.Vessel;
                if (a == null || b == null || a.connection == null || b.connection == null) continue;
                bool same = (a.id == network.GatewayA.VesselId && b.id == network.GatewayB.VesselId) ||
                    (b.id == network.GatewayA.VesselId && a.id == network.GatewayB.VesselId);
                if (same && QuantumRouteDetector.UsesPair(craft, graph, a.connection.Comm, b.connection.Comm))
                    return true;
            }
            return false;
        }
    }
}

using CommNet;
using UnityEngine;
using KSP.UI.Screens.Flight;

namespace QuantumRelay
{
    /// <summary>Route status badge; cyan only for a verified quantum route.</summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal sealed class QuantumRelayFlightHud : MonoBehaviour
    {
        private bool uiVisible = true;
        private GUIStyle badgeStyle;
        private readonly GUIContent badge = new GUIContent();
        private string lastLoggedState;
        private float nextLogCheck;
        private Texture2D qrLogo;
        private readonly Vector3[] corners = new Vector3[4];

        public void Start()
        {
            GameEvents.onHideUI.Add(HideUi);
            GameEvents.onShowUI.Add(ShowUi);
            qrLogo = GameDatabase.Instance.GetTexture("QuantumRelay/Icons/QuantumRelay_64", false);
            Debug.Log("[QuantumRelay][HUD] " + QuantumRelayConstants.DisplayVersion +
                " started; badge anchored to stock communications strip.");
        }

        public void OnDestroy()
        {
            GameEvents.onHideUI.Remove(HideUi);
            GameEvents.onShowUI.Remove(ShowUi);


        }

        private void HideUi() { uiVisible = false; }
        private void ShowUi() { uiVisible = true; }

        private bool ScreenRect(RectTransform transform, out Rect rect)
        {
            rect = new Rect();
            if (transform == null || !transform.gameObject.activeInHierarchy) return false;
            Canvas canvas = transform.GetComponentInParent<Canvas>();
            Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            transform.GetWorldCorners(corners);
            Vector2 lower = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 upper = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            rect = new Rect(lower.x, Screen.height - upper.y, upper.x - lower.x, upper.y - lower.y);
            return rect.width > 0f && rect.height > 0f;
        }

        private Rect BadgeRect(out float scale)
        {
            scale = 1f;
            if (!QuantumRelaySettings.HudFollowStock)
                return new Rect(Mathf.Clamp(QuantumRelaySettings.HudX, 0f, Mathf.Max(0f, Screen.width - 210f)),
                    Mathf.Clamp(QuantumRelaySettings.HudY, 0f, Mathf.Max(0f, Screen.height - 28f)), 210f, 28f);
            TelemetryUpdate telemetry = TelemetryUpdate.Instance;
            Rect left, right, arrow;
            if (telemetry != null && telemetry.control_icon != null && telemetry.modeButton != null &&
                ScreenRect(telemetry.control_icon.rectTransform, out left) &&
                ScreenRect(telemetry.modeButton.transform as RectTransform, out right))
            {
                if (telemetry.arrow_icon != null && ScreenRect(telemetry.arrow_icon.rectTransform, out arrow))
                    scale = Mathf.Clamp(arrow.height / 24f, 0.75f, 2f);
                float width = Mathf.Max(146f * scale, right.xMax - left.xMin + 12f * scale);
                float x = Mathf.Clamp((left.xMin + right.xMax - width) * 0.5f,
                    0f, Mathf.Max(0f, Screen.width - width));
                float y = Mathf.Min(Mathf.Max(left.yMax, right.yMax) + 6f * scale,
                    Mathf.Max(0f, Screen.height - 28f * scale));
                return new Rect(x, y, width, 28f * scale);
            }
            // During HUD startup, use the requested area until the anchor exists.
            return new Rect(270f, 84f, 210f, 28f);
        }

        private bool LogoRect(float scale, out Rect rect)
        {
            rect = new Rect();
            TelemetryUpdate telemetry = TelemetryUpdate.Instance;
            Rect mode;
            if (telemetry == null || telemetry.modeButton == null ||
                !ScreenRect(telemetry.modeButton.transform as RectTransform, out mode))
                return false;
            float size = 24f * scale;
            // A separate slot after the rightmost stock communication button.
            // Never clamp it back across a stock icon if screen space is limited.
            float x = mode.xMax + 6f * scale;
            if (x + size > Screen.width) return false;
            rect = new Rect(x, mode.center.y - size * 0.5f, size, size);
            return true;
        }

        private static QuantumRouteStatus InspectActiveRoute()
        {
            CommNetNetwork owner = CommNetNetwork.Instance;
            return QuantumRouteDetector.Inspect(FlightGlobals.ActiveVessel,
                owner == null ? null : owner.CommNet as QuantumCommNetwork);
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready ||
                Time.realtimeSinceStartup < nextLogCheck) return;
            nextLogCheck = Time.realtimeSinceStartup + 0.5f;
            Vessel vessel = FlightGlobals.ActiveVessel;
            QuantumRouteStatus status = InspectActiveRoute();
            string key = (vessel == null ? "none" : vessel.id.ToString()) + ":" + status;
            if (key == lastLoggedState) return;
            lastLoggedState = key;
            CommPath path = vessel == null || vessel.connection == null
                ? null : vessel.connection.ControlPath;
            Debug.Log("[QuantumRelay][HUD] vessel=" + (vessel == null ? "none" : vessel.vesselName) +
                " | id=" + (vessel == null ? "none" : vessel.id.ToString()) +
                " | state=" + status + " | routeHops=" + (path == null ? 0 : path.Count) +
                " | activePairs=" + QuantumGatewayManager.ActivePairCount);
        }

        private static string Title(QuantumRouteStatus state)
        {
            switch (state)
            {
                case QuantumRouteStatus.QuantumRoute: return "QUANTUM LINK";
                case QuantumRouteStatus.OrdinaryRoute: return "QR: STANDARD";
                case QuantumRouteStatus.NoSignal: return "QR: NO SIGNAL";
                case QuantumRouteStatus.NoConnection: return "QR: NO COMMS";
                case QuantumRouteStatus.NoPath: return "QR: NO ROUTE";
                case QuantumRouteStatus.InvalidPath:
                case QuantumRouteStatus.StalePath: return "QR: UPDATING";
                default: return "QR: WAITING";
            }
        }

        private static string Explain(QuantumRouteStatus state)
        {
            switch (state)
            {
                case QuantumRouteStatus.QuantumRoute:
                    return "Selected CommNet control route crosses an active quantum relay link.";
                case QuantumRouteStatus.OrdinaryRoute:
                    return "Selected CommNet control route does not cross a quantum link.";
                case QuantumRouteStatus.NoSignal:
                    return "Stock CommNet reports this craft disconnected. Online quantum networks alone do not provide a route.";
                case QuantumRouteStatus.NoConnection:
                    return "This craft has no stock CommNet connection object.";
                case QuantumRouteStatus.NoPath:
                    return "Stock CommNet has not supplied a selected control route.";
                case QuantumRouteStatus.StalePath:
                    return "Selected route contains an edge absent from the current graph. Waiting for CommNet to update.";
                case QuantumRouteStatus.InvalidPath:
                    return "Selected route contains an incomplete edge. Waiting for CommNet to update.";
                case QuantumRouteStatus.GraphUnavailable:
                    return "The Quantum Relay CommNet graph is not available yet.";
                default: return "Waiting for the active craft.";
            }
        }

        public void OnGUI()
        {
            if (!uiVisible || !QuantumRelaySettings.ShowFlightHud || !HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready)
                return;

            QuantumRouteStatus state = InspectActiveRoute();
            badge.text = Title(state);
            badge.tooltip = Explain(state);

            if (badgeStyle == null)
            {
                badgeStyle = new GUIStyle(HighLogic.Skin.box);
                badgeStyle.fontStyle = FontStyle.Bold;
                badgeStyle.alignment = TextAnchor.MiddleCenter;
            }
            badgeStyle.normal.textColor = state == QuantumRouteStatus.QuantumRoute
                ? new Color(0.0f, 1.0f, 1.0f, 1.0f) : new Color(0.75f, 0.75f, 0.75f, 1.0f);

            float scale;
            Rect rect = BadgeRect(out scale);
            badgeStyle.fontSize = Mathf.RoundToInt(12f * scale);
            Color oldColor = GUI.color;
            Color oldContent = GUI.contentColor;
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.Box(rect, badge, badgeStyle);
            Rect iconRect = new Rect();
            bool hasIcon = qrLogo != null && LogoRect(scale, out iconRect);
            if (hasIcon)
            {
                GUI.color = state == QuantumRouteStatus.QuantumRoute ? Color.white :
                    new Color(0.55f, 0.55f, 0.55f, 1f);
                GUI.DrawTexture(iconRect, qrLogo, ScaleMode.ScaleToFit, true);
                GUI.color = Color.white;
            }
            if (rect.Contains(Event.current.mousePosition) ||
                (hasIcon && iconRect.Contains(Event.current.mousePosition)))
                GUI.Label(new Rect(Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - 520f)),
                    rect.yMax + 6f, Mathf.Min(520f, Screen.width - 24f), 68f),
                    badge.tooltip, HighLogic.Skin.box);
            GUI.color = oldColor;
            GUI.contentColor = oldContent;
        }
    }
}

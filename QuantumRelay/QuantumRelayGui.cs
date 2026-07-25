using System;
using KSP.UI.Screens;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// v1.0.1 UI foundation: stock AppLauncher button and a small draggable window.
    /// Future releases can add live gateway status and settings without changing
    /// the toolbar lifecycle code.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal sealed class QuantumRelayGui : MonoBehaviour
    {
        private const int WindowId = 0x51524C59;
        private const float WindowWidth = 320f;

        private ApplicationLauncherButton _button;
        private Texture2D _toolbarTexture;
        private Rect _windowRect = new Rect(280f, 120f, WindowWidth, 150f);
        private bool _visible;

        public void Start()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(OnAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnAppLauncherDestroyed);

            if (ApplicationLauncher.Ready)
                OnAppLauncherReady();
        }

        public void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnAppLauncherDestroyed);
            RemoveButton();
        }

        private void OnAppLauncherReady()
        {
            if (_button != null || ApplicationLauncher.Instance == null)
                return;

            _toolbarTexture = GameDatabase.Instance.GetTexture("QuantumRelay/Icons/QuantumRelay_38", false);
            if (_toolbarTexture == null)
                _toolbarTexture = GameDatabase.Instance.GetTexture("QuantumRelay/Icons/QuantumRelay", false);

            _button = ApplicationLauncher.Instance.AddModApplication(
                ShowWindow,
                HideWindow,
                null,
                null,
                null,
                null,
                ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                _toolbarTexture);

            Debug.Log("[QuantumRelay] v1.0.1 toolbar button created.");
        }

        private void OnAppLauncherDestroyed()
        {
            _button = null;
        }

        private void RemoveButton()
        {
            if (_button == null || ApplicationLauncher.Instance == null)
                return;

            try
            {
                ApplicationLauncher.Instance.RemoveModApplication(_button);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QuantumRelay] Could not remove toolbar button: " + ex.Message);
            }

            _button = null;
        }

        private void ShowWindow()
        {
            _visible = true;
        }

        private void HideWindow()
        {
            _visible = false;
        }

        public void OnGUI()
        {
            if (!_visible || !HighLogic.LoadedSceneIsFlight)
                return;

            GUI.skin = HighLogic.Skin;
            _windowRect = GUILayout.Window(
                WindowId,
                _windowRect,
                DrawWindow,
                "Quantum Relay",
                GUILayout.Width(WindowWidth));

            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - 36f));
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("RoosterWorks Quantum Relay");
            GUILayout.Label("UI foundation build v1.0.1");
            GUILayout.Space(6f);
            GUILayout.Label("Gateway status and configuration options will be added in the next UI build.");
            GUILayout.Space(8f);

            if (GUILayout.Button("Close"))
            {
                _visible = false;
                if (_button != null)
                    _button.SetFalse(false);
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
        }
    }
}

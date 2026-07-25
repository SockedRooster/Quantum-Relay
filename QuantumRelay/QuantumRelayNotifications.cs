using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>Centralised player notifications, logging and short status history.</summary>
    internal static class QuantumRelayNotifications
    {
        private const int MaxHistory = 5;
        private static readonly List<string> HistoryItems = new List<string>();
        private static string _lastKey;

        public static IList<string> History => HistoryItems.AsReadOnly();

        public static void Post(string key, string text, bool screenMessage, float duration = 5f)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!string.IsNullOrEmpty(key) && string.Equals(_lastKey, key, StringComparison.Ordinal)) return;
            _lastKey = key;

            string stamped = DateTime.Now.ToString("HH:mm:ss") + "  " + text;
            HistoryItems.Insert(0, stamped);
            while (HistoryItems.Count > MaxHistory) HistoryItems.RemoveAt(HistoryItems.Count - 1);

            QuantumRelayRuntimeState.SetTicker(text);
            Debug.Log("[QuantumRelay] " + text);

            if (screenMessage && QuantumRelaySettings.ShowScreenMessages)
                ScreenMessages.PostScreenMessage(text, duration, ScreenMessageStyle.UPPER_CENTER);
        }

        public static void Clear()
        {
            HistoryItems.Clear();
            _lastKey = null;
        }
    }
}

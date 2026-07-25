using System;
using System.Collections.Generic;
using CommNet;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Installs QuantumCommNetwork while preserving every CommNode already held
    /// by the stock graph. It also repairs the installation after a stock reset,
    /// such as when game settings are applied.
    /// </summary>
    internal static class CommNetNetworkInstaller
    {
        private static bool _loggedWaiting;

        public static bool IsInstalled
        {
            get
            {
                return CommNetNetwork.Instance != null && CommNetNetwork.Instance.CommNet is QuantumCommNetwork;
            }
        }

        public static bool EnsureInstalled()
        {
            CommNetNetwork owner = CommNetNetwork.Instance;
            if (owner == null || owner.CommNet == null)
            {
                if (!_loggedWaiting)
                {
                    _loggedWaiting = true;
                    Debug.Log("[QuantumRelay] Waiting for CommNetNetwork initialization.");
                }
                return false;
            }

            _loggedWaiting = false;
            if (owner.CommNet is QuantumCommNetwork) return true;

            try
            {
                CommNetwork previous = owner.CommNet;
                var existingNodes = new List<CommNode>(previous.Count);
                for (int i = 0; i < previous.Count; i++)
                {
                    CommNode node = previous[i];
                    if (node != null) existingNodes.Add(node);
                }

                var replacement = new QuantumCommNetwork();
                foreach (CommNode node in existingNodes)
                    replacement.Add(node);

                owner.CommNet = replacement;
                owner.QueueRebuild();
                Debug.Log("[QuantumRelay] QuantumCommNetwork installed | preservedNodes=" + existingNodes.Count);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[QuantumRelay] QuantumCommNetwork installation failed: " + ex);
                return false;
            }
        }
    }
}

using System.Collections.Generic;
using QuantumRelay.Core;

namespace QuantumRelay.Managers
{
    /// <summary>
    /// Central registry for all known quantum bridges.
    /// </summary>
    public class QuantumBridgeManager
    {
        private static readonly QuantumBridgeManager instance =
            new QuantumBridgeManager();

        public static QuantumBridgeManager Instance
        {
            get { return instance; }
        }

        private readonly List<QuantumBridge> bridges;

        private QuantumBridgeManager()
        {
            bridges = new List<QuantumBridge>();
        }

        public IEnumerable<QuantumBridge> Bridges
        {
            get { return bridges; }
        }

        public void RegisterBridge(QuantumBridge bridge)
        {
            if (bridge == null)
                return;

            if (!bridges.Contains(bridge))
                bridges.Add(bridge);
        }

        public void Clear()
        {
            bridges.Clear();
        }
    }
}
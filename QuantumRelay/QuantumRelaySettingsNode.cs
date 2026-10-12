namespace QuantumRelay
{
    internal static class QuantumRelaySettingsNode
    {
        public static ConfigNode Find(ConfigNode root)
        {
            if (root == null) return null;
            if (root.name == "QUANTUM_RELAY_SETTINGS") return root;
            ConfigNode direct = root.GetNode("QUANTUM_RELAY_SETTINGS");
            if (direct != null) return direct;
            ConfigNode wrapper = root.name == "QUANTUM_RELAY_CONFIG"
                ? root : root.GetNode("QUANTUM_RELAY_CONFIG");
            return wrapper == null ? null : wrapper.GetNode("QUANTUM_RELAY_SETTINGS");
        }
    }
}

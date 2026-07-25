namespace QuantumRelay
{
    /// <summary>Small command mailbox shared by the IMGUI and flight controller.</summary>
    internal static class QuantumRelayCommands
    {
        private static bool _refreshRequested;
        private static bool _rebuildRequested;

        public static void RequestRefresh() { _refreshRequested = true; }
        public static void RequestRebuild() { _rebuildRequested = true; }

        public static bool ConsumeRefresh()
        {
            bool value = _refreshRequested;
            _refreshRequested = false;
            return value;
        }

        public static bool ConsumeRebuild()
        {
            bool value = _rebuildRequested;
            _rebuildRequested = false;
            return value;
        }
    }
}

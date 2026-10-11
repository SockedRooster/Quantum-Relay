namespace QuantumRelay
{
    /// <summary>
    /// Presentation-only state. It intentionally contains no gameplay logic.
    /// </summary>
    internal enum RelayVisualState
    {
        Folded,
        Standby,
        Searching,
        Initializing,
        Entangled,
        Transmitting,
        Fault
    }
}

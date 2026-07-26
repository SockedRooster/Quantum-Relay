using System;

namespace QuantumRelay.Core
{
    /// <summary>
    /// Describes one independent communication corridor created by one
    /// natural wormhole pair. Networks do not create direct edges to one
    /// another; ordinary CommNet on a shared side (for example Kerbol) is
    /// responsible for transferring traffic between corridors.
    /// </summary>
    internal sealed class QuantumNetwork
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string EndpointA { get; set; }
        public string EndpointB { get; set; }

        public QuantumNetwork()
        {
            Id = Guid.NewGuid().ToString("N");
            DisplayName = "Quantum Network";
            EndpointA = string.Empty;
            EndpointB = string.Empty;
        }

        public static string CreateStableId(string endpointA, string endpointB)
        {
            string a = Normalize(endpointA);
            string b = Normalize(endpointB);
            return string.Compare(a, b, StringComparison.Ordinal) <= 0
                ? a + "--" + b
                : b + "--" + a;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unknown";
            char[] buffer = value.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (!char.IsLetterOrDigit(buffer[i])) buffer[i] = '-';
            }
            return new string(buffer).Trim('-');
        }
    }
}

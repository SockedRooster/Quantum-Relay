using System;
using System.Collections.Generic;

namespace QuantumRelay
{
    public sealed class RelayDefinition
    {
        public RelayClass RelayClass { get; private set; }
        public int Tier { get; private set; }
        public string DisplayName { get; private set; }
        public double SynchronizationStrength { get; private set; }
        public int DesignCapacity { get; private set; }

        internal RelayDefinition(
            RelayClass relayClass,
            int tier,
            string displayName,
            double synchronizationStrength,
            int designCapacity)
        {
            RelayClass = relayClass;
            Tier = tier;
            DisplayName = displayName;
            SynchronizationStrength = synchronizationStrength;
            DesignCapacity = designCapacity;
        }
    }

    public static class RelayCatalog
    {
        private static readonly Dictionary<RelayClass, RelayDefinition> Definitions =
            new Dictionary<RelayClass, RelayDefinition>
            {
                {
                    RelayClass.Pioneer,
                    new RelayDefinition(RelayClass.Pioneer, 1, "QR-100 Pioneer", 0.25, 1)
                },
                {
                    RelayClass.Voyager,
                    new RelayDefinition(RelayClass.Voyager, 2, "QR-250 Voyager", 0.50, 2)
                },
                {
                    RelayClass.EventHorizon,
                    new RelayDefinition(RelayClass.EventHorizon, 3, "QR-500 Event Horizon", 1.00, 4)
                },
                {
                    RelayClass.HorizonPrime,
                    new RelayDefinition(RelayClass.HorizonPrime, 4, "QR-750 Horizon Prime", 1.25, 6)
                }
            };

        public static RelayDefinition Get(RelayClass relayClass)
        {
            RelayDefinition definition;
            if (Definitions.TryGetValue(relayClass, out definition))
                return definition;

            return Definitions[RelayClass.Pioneer];
        }

        public static RelayDefinition FromTier(int tier)
        {
            switch (tier)
            {
                case 2:
                    return Get(RelayClass.Voyager);
                case 3:
                    return Get(RelayClass.EventHorizon);
                case 4:
                    return Get(RelayClass.HorizonPrime);
                default:
                    return Get(RelayClass.Pioneer);
            }
        }

        public static RelayClass Parse(string value, int fallbackTier)
        {
            RelayClass parsed;
            if (!string.IsNullOrEmpty(value) &&
                Enum.TryParse(value, true, out parsed) &&
                Definitions.ContainsKey(parsed))
            {
                return parsed;
            }

            return FromTier(fallbackTier).RelayClass;
        }
    }
}

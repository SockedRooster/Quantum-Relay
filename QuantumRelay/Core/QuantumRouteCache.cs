using System.Collections.Generic;
using QuantumRelay.Models;

namespace QuantumRelay.Core
{
    /// <summary>
    /// Small route cache. Entries are discarded when topology changes or when
    /// their lifetime expires.
    /// </summary>
    internal sealed class QuantumRouteCache
    {
        private readonly Dictionary<RouteKey, QuantumRoute> _routes =
            new Dictionary<RouteKey, QuantumRoute>();

        private readonly object _syncRoot = new object();

        public double LifetimeSeconds { get; set; }

        public int Count
        {
            get
            {
                lock (_syncRoot) return _routes.Count;
            }
        }

        public QuantumRouteCache()
        {
            LifetimeSeconds = 2.0;
        }

        public bool TryGet(RouteKey key, double universalTime, out QuantumRoute route)
        {
            lock (_syncRoot)
            {
                if (!_routes.TryGetValue(key, out route)) return false;

                double age = universalTime - route.CreatedUniversalTime;
                if (age < 0.0 || age > LifetimeSeconds)
                {
                    _routes.Remove(key);
                    route = null;
                    return false;
                }

                return true;
            }
        }

        public void Store(QuantumRoute route)
        {
            if (route == null) return;
            lock (_syncRoot) _routes[route.Key] = route;
        }

        public void Invalidate(RouteKey key)
        {
            lock (_syncRoot) _routes.Remove(key);
        }

        public void Clear()
        {
            lock (_syncRoot) _routes.Clear();
        }
    }
}

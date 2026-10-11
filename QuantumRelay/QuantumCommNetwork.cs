using System;
using System.Collections.Generic;
using CommNet;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Stock CommNetwork with one narrowly scoped exception: the two active
    /// gateway nodes are evaluated as an unobstructed relay pair. The resulting
    /// link strengths are capped to the configured quantum-link quality.
    /// </summary>
    internal sealed class QuantumCommNetwork : CommNetwork
    {
        // Separate state per pair: a single shared flag produces misleading
        // CONNECTED/FAILED alternation when two networks have different results.
        private readonly Dictionary<string, bool> _lastPairState = new Dictionary<string, bool>();
        private readonly Dictionary<string, float> _lastFailureLog = new Dictionary<string, float>();

        protected override bool SetNodeConnection(CommNode a, CommNode b)
        {
            if (!QuantumGatewayManager.IsGatewayPair(a, b))
                return base.SetNodeConnection(a, b);

            bool connected = TryConnect(a, b, 1E-07, true, true, true);
            if (connected)
            {
                // Connect returns the same graph edge created by TryConnect.
                // Setting all three stock route strengths makes the configured
                // percentage participate naturally in CommNet path quality and
                // science-transmission calculations.
                CommLink link = Connect(a, b, 1E-07);
                double quality = QuantumGatewayManager.GetPairSignalQuality(a, b);
                if (quality <= 0.0) quality = 0.25;
                link.strengthRR = quality;
                link.strengthAR = quality;
                link.strengthBR = quality;
                link.aCanRelay = true;
                link.bCanRelay = true;
                link.bothRelay = true;
            }

            string pair = QuantumGatewayManager.DescribeGatewayPair(a, b);
            bool previous;
            bool first = !_lastPairState.TryGetValue(pair, out previous);
            bool changed = first || previous != connected;
            float now = Time.realtimeSinceStartup;
            float lastFailure;
            bool repeatFailure = !connected &&
                (!_lastFailureLog.TryGetValue(pair, out lastFailure) || now - lastFailure >= 30f);
            if (changed || repeatFailure)
            {
                double quality = QuantumGatewayManager.GetPairSignalQuality(a, b);
                Debug.Log("[QuantumRelay][EdgeDiag] " + (connected ? "CONNECTED" : "FAILED") +
                          " | " + pair +
                          " | quality=" + (quality * 100.0).ToString("F1") + "%" +
                          " | TryConnect=" + connected +
                          " | " + DescribeNodeDiagnostics(a, b) +
                          (connected ? " | stock edge accepted" :
                           " | TryConnect returned false; rejection cause not exposed by this API"));
                if (!connected) _lastFailureLog[pair] = now;
            }
            _lastPairState[pair] = connected;
            return connected;
        }

        // Diagnostics only. Never add, remove, reconnect or otherwise mutate
        // CommNet nodes while inspecting them.
        private string DescribeNodeDiagnostics(CommNode a, CommNode b)
        {
            CommNetNetwork owner = CommNetNetwork.Instance;
            CommNetwork active = owner == null ? null : owner.CommNet;
            if (active == null)
                return "graph=null | nodeARegistered=false | nodeBRegistered=false";

            int indexA = -1;
            int indexB = -1;
            for (int i = 0; i < active.Count; i++)
            {
                CommNode current = active[i];
                if (ReferenceEquals(current, a)) indexA = i;
                if (ReferenceEquals(current, b)) indexB = i;
            }

            return "graph=" + active.GetType().Name +
                   " | graphNodes=" + active.Count +
                   " | nodeARegistered=" + (indexA >= 0) +
                   " | nodeBRegistered=" + (indexB >= 0) +
                   " | nodeAIndex=" + indexA +
                   " | nodeBIndex=" + indexB +
                   " | nodeAType=" + (a == null ? "null" : a.GetType().Name) +
                   " | nodeBType=" + (b == null ? "null" : b.GetType().Name) +
                   " | sameNode=" + ReferenceEquals(a, b) +
                   " | installed=" + ReferenceEquals(active, this);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace QuantumRelay
{
    internal sealed class ReflectorDetection
    {
        public bool Found { get; set; }
        public bool Deployed { get; set; }
        public string Evidence { get; set; } = "not found";
    }

    internal static class ReflectorDetector
    {
        public static ReflectorDetection InspectLoaded(Part part, PartModule reflector)
        {
            var result = new ReflectorDetection { Found = reflector != null };
            if (part == null || reflector == null) return result;

            var evidence = new List<string> { "module=" + reflector.moduleName };
            ModuleDeployablePart deployable = reflector as ModuleDeployablePart;

            if (deployable != null)
            {
                string state = deployable.deployState.ToString();
                evidence.Add("enum=" + state);
                result.Deployed = deployable.deployState == ModuleDeployablePart.DeployState.EXTENDED;

                BaseEvent extend = deployable.Events != null ? deployable.Events["Extend"] : null;
                BaseEvent retract = deployable.Events != null ? deployable.Events["Retract"] : null;
                evidence.Add("extendActive=" + (extend != null && extend.active));
                evidence.Add("retractActive=" + (retract != null && retract.active));

                // For stock deployables, Retract being available and Extend unavailable means deployed.
                if (!result.Deployed && retract != null && retract.active && (extend == null || !extend.active))
                    result.Deployed = true;
            }
            else
            {
                evidence.Add("enum=not-a-ModuleDeployablePart");
            }

            // Inspect actual module fields/properties before relying on model animation.
            if (!result.Deployed && reflector.Fields != null)
            {
                foreach (string key in new[] { "deployState", "isDeployed", "deployed", "state", "status", "stateString" })
                {
                    BaseField field = reflector.Fields[key];
                    if (field == null) continue;
                    object value = field.GetValue(reflector);
                    string text = value == null ? string.Empty : value.ToString();
                    evidence.Add(key + "=" + text);
                    if (IsExtended(text) || IsTrue(text)) result.Deployed = true;
                }
            }

            try
            {
                Animation[] animators = part.FindModelAnimators(QuantumRelaySettings.ReflectorAnimationName);
                if (animators != null && animators.Length > 0)
                {
                    float highest = 0f;
                    bool foundState = false;
                    foreach (Animation animation in animators)
                    {
                        if (animation == null) continue;
                        AnimationState animationState = animation[QuantumRelaySettings.ReflectorAnimationName];
                        if (animationState == null) continue;
                        foundState = true;
                        highest = Mathf.Max(highest, animationState.normalizedTime);
                    }
                    evidence.Add("anim=" + (foundState ? highest.ToString("0.000", CultureInfo.InvariantCulture) : "state-missing"));
                    if (foundState && highest >= 0.95f) result.Deployed = true;
                }
                else evidence.Add("anim=not-found");
            }
            catch (Exception ex)
            {
                evidence.Add("anim-error=" + ex.GetType().Name);
            }

            result.Evidence = string.Join(",", evidence.ToArray());
            return result;
        }

        public static ReflectorDetection InspectUnloaded(ProtoPartSnapshot part, ProtoPartModuleSnapshot module)
        {
            var result = new ReflectorDetection { Found = module != null };
            if (part == null || module == null || module.moduleValues == null)
            {
                result.Evidence = "proto-module-missing";
                return result;
            }

            var evidence = new List<string> { "module=" + module.moduleName };
            foreach (string key in new[] { "deployState", "isDeployed", "deployed", "state", "status", "stateString" })
            {
                string value = module.moduleValues.GetValue(key);
                if (string.IsNullOrEmpty(value)) continue;
                evidence.Add(key + "=" + value);
                if (IsExtended(value) || IsTrue(value)) result.Deployed = true;
            }
            result.Evidence = evidence.Count == 1 ? "proto-state-unknown" : string.Join(",", evidence.ToArray());
            return result;
        }

        private static bool IsExtended(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string s = value.Trim().ToUpperInvariant();
            return s.Contains("EXTENDED") || s.Contains("DEPLOYED");
        }

        private static bool IsTrue(string value)
        {
            bool parsed;
            return bool.TryParse(value, out parsed) && parsed;
        }
    }
}

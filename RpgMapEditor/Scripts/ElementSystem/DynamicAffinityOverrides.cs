using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGElementSystem
{
    /// <summary>
    /// 動的親和性オーバーライドシステム
    /// </summary>
    public class DynamicAffinityOverrides
    {
        private Dictionary<string, AffinityOverride> activeOverrides = new Dictionary<string, AffinityOverride>();

        [Serializable]
        public struct AffinityOverride
        {
            public ElementType attackElement;
            public ElementType defenseElement;
            public float newAffinity;
            public float duration;
            public bool isPermanent;
            public string sourceId;
        }

        public void AddOverride(string overrideId, ElementType attackElement, ElementType defenseElement, float newAffinity, float duration = -1f, string sourceId = "")
        {
            activeOverrides[overrideId] = new AffinityOverride
            {
                attackElement = attackElement,
                defenseElement = defenseElement,
                newAffinity = newAffinity,
                duration = duration,
                isPermanent = duration < 0f,
                sourceId = sourceId
            };
        }

        public void RemoveOverride(string overrideId)
        {
            activeOverrides.Remove(overrideId);
        }

        public void RemoveOverridesBySource(string sourceId)
        {
            var toRemove = new List<string>();
            foreach (var kvp in activeOverrides)
            {
                if (kvp.Value.sourceId == sourceId)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (string id in toRemove)
            {
                activeOverrides.Remove(id);
            }
        }

        public float GetModifiedAffinity(ElementType attackElement, ElementType defenseElement, float originalAffinity)
        {
            foreach (var kvp in activeOverrides)
            {
                var overrideData = kvp.Value;
                if (overrideData.attackElement == attackElement && overrideData.defenseElement == defenseElement)
                {
                    return overrideData.newAffinity;
                }
            }

            return originalAffinity;
        }

        public void UpdateOverrides(float deltaTime)
        {
            var expiredOverrides = new List<string>();

            foreach (var kvp in activeOverrides)
            {
                if (!kvp.Value.isPermanent)
                {
                    var overrideData = kvp.Value;
                    overrideData.duration -= deltaTime;

                    if (overrideData.duration <= 0f)
                    {
                        expiredOverrides.Add(kvp.Key);
                    }
                    else
                    {
                        activeOverrides[kvp.Key] = overrideData;
                    }
                }
            }

            foreach (string id in expiredOverrides)
            {
                activeOverrides.Remove(id);
            }
        }

        public List<AffinityOverride> GetActiveOverrides()
        {
            return new List<AffinityOverride>(activeOverrides.Values);
        }

        public void ClearAllOverrides()
        {
            activeOverrides.Clear();
        }
    }
}
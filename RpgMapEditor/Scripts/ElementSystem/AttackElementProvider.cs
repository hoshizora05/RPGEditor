using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGElementSystem
{
    /// <summary>
    /// 属性攻撃プロバイダーシステム
    /// </summary>
    public class AttackElementProvider
    {
        private Dictionary<string, List<ElementalBonus>> weaponBonuses = new Dictionary<string, List<ElementalBonus>>();
        private Dictionary<string, List<ElementalBonus>> skillBonuses = new Dictionary<string, List<ElementalBonus>>();

        [Serializable]
        public struct ElementalBonus
        {
            public ElementType elementType;
            public float flatBonus;
            public float percentageBonus;
            public bool isTemporary;
            public float duration;
            public string sourceId;
        }

        public void RegisterWeaponBonus(string weaponId, ElementType element, float flatBonus, float percentageBonus = 0f)
        {
            if (!weaponBonuses.ContainsKey(weaponId))
                weaponBonuses[weaponId] = new List<ElementalBonus>();

            weaponBonuses[weaponId].Add(new ElementalBonus
            {
                elementType = element,
                flatBonus = flatBonus,
                percentageBonus = percentageBonus,
                isTemporary = false,
                sourceId = weaponId
            });
        }

        public void RegisterSkillBonus(string skillId, ElementType element, float flatBonus, float percentageBonus = 0f, float duration = -1f)
        {
            if (!skillBonuses.ContainsKey(skillId))
                skillBonuses[skillId] = new List<ElementalBonus>();

            skillBonuses[skillId].Add(new ElementalBonus
            {
                elementType = element,
                flatBonus = flatBonus,
                percentageBonus = percentageBonus,
                isTemporary = duration > 0f,
                duration = duration,
                sourceId = skillId
            });
        }

        public ElementalAttack CreateAttack(CharacterStats attacker, string weaponId = null, string skillId = null)
        {
            var attack = new ElementalAttack(ElementType.None, 0f, attacker);

            // Apply weapon bonuses
            if (!string.IsNullOrEmpty(weaponId) && weaponBonuses.ContainsKey(weaponId))
            {
                foreach (var bonus in weaponBonuses[weaponId])
                {
                    float power = bonus.flatBonus;
                    if (bonus.percentageBonus > 0f)
                    {
                        power += attacker.GetStatValue(StatType.Attack) * bonus.percentageBonus;
                    }

                    if (power > 0f)
                    {
                        attack.AddElement(bonus.elementType, power);
                    }
                }
            }

            // Apply skill bonuses
            if (!string.IsNullOrEmpty(skillId) && skillBonuses.ContainsKey(skillId))
            {
                foreach (var bonus in skillBonuses[skillId])
                {
                    float power = bonus.flatBonus;
                    if (bonus.percentageBonus > 0f)
                    {
                        power += attacker.GetStatValue(StatType.MagicPower) * bonus.percentageBonus;
                    }

                    if (power > 0f)
                    {
                        attack.AddElement(bonus.elementType, power);
                    }
                }
            }

            // If no elements were added, use physical/neutral damage
            if (attack.elements.Count == 0)
            {
                attack.AddElement(ElementType.None, attacker.GetStatValue(StatType.Attack));
            }

            return attack;
        }

        public void UpdateTemporaryBonuses(float deltaTime)
        {
            // Update weapon bonuses
            foreach (var weaponBonuses in weaponBonuses.Values)
            {
                for (int i = weaponBonuses.Count - 1; i >= 0; i--)
                {
                    var bonus = weaponBonuses[i];
                    if (bonus.isTemporary)
                    {
                        bonus.duration -= deltaTime;
                        if (bonus.duration <= 0f)
                        {
                            weaponBonuses.RemoveAt(i);
                        }
                        else
                        {
                            weaponBonuses[i] = bonus;
                        }
                    }
                }
            }

            // Update skill bonuses
            foreach (var skillBonuses in skillBonuses.Values)
            {
                for (int i = skillBonuses.Count - 1; i >= 0; i--)
                {
                    var bonus = skillBonuses[i];
                    if (bonus.isTemporary)
                    {
                        bonus.duration -= deltaTime;
                        if (bonus.duration <= 0f)
                        {
                            skillBonuses.RemoveAt(i);
                        }
                        else
                        {
                            skillBonuses[i] = bonus;
                        }
                    }
                }
            }
        }

        public void ClearTemporaryBonuses()
        {
            foreach (var weaponBonuses in weaponBonuses.Values)
            {
                weaponBonuses.RemoveAll(b => b.isTemporary);
            }

            foreach (var skillBonuses in skillBonuses.Values)
            {
                skillBonuses.RemoveAll(b => b.isTemporary);
            }
        }

        public List<ElementalBonus> GetWeaponBonuses(string weaponId)
        {
            return weaponBonuses.TryGetValue(weaponId, out List<ElementalBonus> bonuses)
                ? new List<ElementalBonus>(bonuses)
                : new List<ElementalBonus>();
        }

        public List<ElementalBonus> GetSkillBonuses(string skillId)
        {
            return skillBonuses.TryGetValue(skillId, out List<ElementalBonus> bonuses)
                ? new List<ElementalBonus>(bonuses)
                : new List<ElementalBonus>();
        }
    }
}
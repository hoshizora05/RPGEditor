using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGStatsSystem;

namespace RPGStatusEffectSystem
{
    /// <summary>
    /// プリセット状態異常の作成例
    /// </summary>
    public class StatusEffectPresets : MonoBehaviour
    {
        [Header("Status Effect Database")]
        public StatusEffectDatabase statusEffectDatabase;

        [ContextMenu("Create Basic Status Effects")]
        public void CreateBasicStatusEffects()
        {
            if (statusEffectDatabase == null)
            {
                Debug.LogError("Status Effect Database not assigned!");
                return;
            }

            CreatePoisonEffect();
            CreateStunEffect();
            CreateRegenerationEffect();
            CreateAttackUpEffect();
            CreateShieldEffect();

            Debug.Log("Created basic status effects");
        }

        private void CreatePoisonEffect()
        {
            var poison = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            poison.effectId = "poison_basic";
            poison.effectName = "Poison";
            poison.description = "Deals damage over time";
            poison.effectType = StatusEffectType.DoT;
            poison.category = StatusEffectCategory.Poison;

            poison.baseDuration = 10f;
            poison.basePower = 5f;
            poison.tickInterval = 1f;
            poison.maxStacks = 5;
            poison.stackBehavior = StackBehavior.Intensity;

            poison.resistance = new StatusEffectResistance(ResistanceType.PoisonResistance, 0.1f);
            poison.characterTintColor = new Color(0.5f, 1f, 0.5f, 0.8f);

            statusEffectDatabase.AddEffect(poison);
        }

        private void CreateStunEffect()
        {
            var stun = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            stun.effectId = "stun_basic";
            stun.effectName = "Stun";
            stun.description = "Unable to move or act";
            stun.effectType = StatusEffectType.Control;
            stun.category = StatusEffectCategory.Stun;

            stun.baseDuration = 3f;
            stun.basePower = 0f;
            stun.maxStacks = 1;
            stun.stackBehavior = StackBehavior.Replace;

            stun.preventMovement = true;
            stun.preventActions = true;
            stun.preventSkills = true;

            stun.resistance = new StatusEffectResistance(ResistanceType.StunResistance, 0.2f);
            stun.immuneAfterRemoval.Add(StatusEffectCategory.Stun);
            stun.immunityDuration = 2f;

            stun.characterTintColor = new Color(1f, 1f, 0.5f, 0.8f);

            statusEffectDatabase.AddEffect(stun);
        }

        private void CreateRegenerationEffect()
        {
            var regen = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            regen.effectId = "regeneration_basic";
            regen.effectName = "Regeneration";
            regen.description = "Restores health over time";
            regen.effectType = StatusEffectType.HoT;
            regen.category = StatusEffectCategory.Regeneration;

            regen.baseDuration = 30f;
            regen.basePower = 3f;
            regen.tickInterval = 2f;
            regen.maxStacks = 3;
            regen.stackBehavior = StackBehavior.Duration;

            regen.characterTintColor = new Color(0.5f, 1f, 0.5f, 0.8f);

            statusEffectDatabase.AddEffect(regen);
        }

        private void CreateAttackUpEffect()
        {
            var attackUp = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            attackUp.effectId = "attack_up_basic";
            attackUp.effectName = "Attack Up";
            attackUp.description = "Increases attack power";
            attackUp.effectType = StatusEffectType.Buff;
            attackUp.category = StatusEffectCategory.AttackUp;

            attackUp.baseDuration = 60f;
            attackUp.basePower = 10f;
            attackUp.maxStacks = 3;
            attackUp.stackBehavior = StackBehavior.Intensity;

            attackUp.affectedStats.Add(StatType.Attack);
            attackUp.statModifierValues.Add(10f);

            attackUp.characterTintColor = new Color(1f, 0.8f, 0.8f, 0.8f);

            statusEffectDatabase.AddEffect(attackUp);
        }

        private void CreateShieldEffect()
        {
            var shield = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            shield.effectId = "magic_shield_basic";
            shield.effectName = "Magic Shield";
            shield.description = "Increases magical defense";
            shield.effectType = StatusEffectType.Buff;
            shield.category = StatusEffectCategory.MagicShield;

            shield.baseDuration = 120f;
            shield.basePower = 15f;
            shield.maxStacks = 1;
            shield.stackBehavior = StackBehavior.Replace;

            shield.affectedStats.Add(StatType.MagicDefense);
            shield.statModifierValues.Add(15f);

            statusEffectDatabase.AddEffect(shield);
        }
    }
}
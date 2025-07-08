using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
namespace RPGStatusEffectSystem.Editor
{
    using UnityEditor;
    /// <summary>
    /// 状態異常エフェクト作成ウィザード
    /// </summary>
    public class StatusEffectCreationWizard : ScriptableWizard
    {
        [Header("Basic Information")]
        public string effectId = "";
        public string effectName = "";
        public string description = "";
        public StatusEffectType effectType = StatusEffectType.Debuff;
        public StatusEffectCategory category = StatusEffectCategory.Poison;

        [Header("Parameters")]
        public float duration = 10f;
        public float power = 5f;
        public float tickInterval = 1f;
        public int maxStacks = 1;
        public StackBehavior stackBehavior = StackBehavior.Replace;

        [Header("Database")]
        public StatusEffectDatabase targetDatabase;

        [MenuItem("Tools/RPG Status Effect System/Create Status Effect")]
        static void CreateWizard()
        {
            ScriptableWizard.DisplayWizard<StatusEffectCreationWizard>("Create Status Effect", "Create");
        }

        void OnWizardCreate()
        {
            if (targetDatabase == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a target database", "OK");
                return;
            }

            if (string.IsNullOrEmpty(effectId))
            {
                EditorUtility.DisplayDialog("Error", "Effect ID cannot be empty", "OK");
                return;
            }

            var newEffect = CreateInstance<StatusEffectDefinition>();
            newEffect.effectId = effectId;
            newEffect.effectName = effectName;
            newEffect.description = description;
            newEffect.effectType = effectType;
            newEffect.category = category;
            newEffect.baseDuration = duration;
            newEffect.basePower = power;
            newEffect.tickInterval = tickInterval;
            newEffect.maxStacks = maxStacks;
            newEffect.stackBehavior = stackBehavior;

            // Save as asset
            string path = AssetDatabase.GetAssetPath(targetDatabase);
            string directory = System.IO.Path.GetDirectoryName(path);
            string assetPath = $"{directory}/{effectId}_Effect.asset";

            AssetDatabase.CreateAsset(newEffect, assetPath);

            // Add to database
            targetDatabase.AddEffect(newEffect);

            EditorUtility.SetDirty(targetDatabase);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created status effect: {effectName} ({effectId})");
        }

        void OnWizardUpdate()
        {
            helpString = "Create a new status effect definition";

            if (string.IsNullOrEmpty(effectId))
            {
                errorString = "Effect ID is required";
                isValid = false;
            }
            else if (targetDatabase == null)
            {
                errorString = "Target database is required";
                isValid = false;
            }
            else
            {
                errorString = "";
                isValid = true;
            }
        }
    }
}
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RPGSkillSystem.UI
{
    /// <summary>
    /// スキルツリーUI
    /// </summary>
    public class SkillTreeUI : MonoBehaviour
    {
        [Header("UI References")]
        public Transform skillNodeContainer;
        public GameObject skillNodePrefab;
        public ScrollRect scrollRect;
        public TextMeshProUGUI skillPointsText;
        public Button resetButton;

        [Header("Settings")]
        public SkillManager targetSkillManager;
        public Vector2 nodeSpacing = new Vector2(150f, 100f);

        private Dictionary<string, SkillTreeNodeUI> skillNodes = new Dictionary<string, SkillTreeNodeUI>();
        private List<GameObject> connectionLines = new List<GameObject>();

        #region Unity Lifecycle

        private void Start()
        {
            InitializeSkillTree();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void InitializeSkillTree()
        {
            if (targetSkillManager?.skillDatabase == null) return;

            CreateSkillNodes();
            CreateConnections();
            UpdateSkillTree();
        }

        private void CreateSkillNodes()
        {
            var allSkills = targetSkillManager.skillDatabase.GetAllSkills();

            foreach (var skill in allSkills)
            {
                var nodeObj = Instantiate(skillNodePrefab, skillNodeContainer);
                var nodeUI = nodeObj.GetComponent<SkillTreeNodeUI>();

                if (nodeUI != null)
                {
                    nodeUI.Initialize(skill, targetSkillManager);
                    skillNodes[skill.skillId] = nodeUI;

                    // Position node (simple grid layout - can be improved)
                    int tier = skill.minLevel / 5; // Group by level tiers
                    int index = allSkills.IndexOf(skill) % 5;

                    Vector2 position = new Vector2(
                        index * nodeSpacing.x,
                        -tier * nodeSpacing.y
                    );

                    nodeObj.GetComponent<RectTransform>().anchoredPosition = position;
                }
            }
        }

        private void CreateConnections()
        {
            // Create connection lines between prerequisite skills
            foreach (var kvp in skillNodes)
            {
                var skill = targetSkillManager.skillDatabase.GetSkill(kvp.Key);
                if (skill == null) continue;

                foreach (int prerequisiteId in skill.prerequisiteSkillIds)
                {
                    string prereqIdString = prerequisiteId.ToString();
                    if (skillNodes.TryGetValue(prereqIdString, out SkillTreeNodeUI prereqNode))
                    {
                        CreateConnectionLine(prereqNode.transform, kvp.Value.transform);
                    }
                }
            }
        }

        private void CreateConnectionLine(Transform from, Transform to)
        {
            var lineObj = new GameObject("Connection Line");
            lineObj.transform.SetParent(skillNodeContainer);

            var lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.endColor = Color.gray;
            lineRenderer.startWidth = 0.02f;
            lineRenderer.endWidth = 0.02f;
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = false;

            lineRenderer.SetPosition(0, from.localPosition);
            lineRenderer.SetPosition(1, to.localPosition);

            connectionLines.Add(lineObj);
        }

        private void SubscribeToEvents()
        {
            if (targetSkillManager != null)
            {
                targetSkillManager.OnSkillLearned += OnSkillLearned;
                targetSkillManager.OnSkillLevelUp += OnSkillLevelUp;
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(OnResetButtonClicked);
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (targetSkillManager != null)
            {
                targetSkillManager.OnSkillLearned -= OnSkillLearned;
                targetSkillManager.OnSkillLevelUp -= OnSkillLevelUp;
            }
        }

        #endregion

        #region Update Methods

        private void UpdateSkillTree()
        {
            // Update skill points display
            if (skillPointsText != null)
            {
                skillPointsText.text = $"Skill Points: {targetSkillManager.currentSkillPoints}";
            }

            // Update all skill nodes
            foreach (var nodeUI in skillNodes.Values)
            {
                nodeUI.UpdateDisplay();
            }
        }

        #endregion

        #region Event Handlers

        private void OnSkillLearned(string skillId, int level)
        {
            UpdateSkillTree();
        }

        private void OnSkillLevelUp(string skillId, int level)
        {
            UpdateSkillTree();
        }

        private void OnResetButtonClicked()
        {
            // Implement skill reset functionality
            Debug.Log("Skill reset requested");
        }

        #endregion
    }

}
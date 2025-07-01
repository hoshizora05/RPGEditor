using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem
{
    /// <summary>
    /// イベントの実行内容を定義するページ
    /// 出現条件、コマンドリスト、トリガー設定などを管理
    /// </summary>
    [System.Serializable]
    public class EventPage
    {
        [Header("基本設定")]
        [SerializeField] private string pageName = "New Page";
        [SerializeField] private int priority = 0;
        [SerializeField] private bool enabled = true;

        [Header("出現条件")]
        [SerializeField] private EventConditions conditions;

        [Header("トリガー設定")]
        [SerializeField] private EventTrigger trigger = EventTrigger.ActionButton;
        [SerializeField] private EventPriority displayPriority = EventPriority.SameAsCharacters;

        [Header("グラフィック")]
        [SerializeField] private EventGraphic graphic;

        [Header("移動設定")]
        [SerializeField] private EventMoveType moveType = EventMoveType.Fixed;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private bool moveAnimation = true;

        [Header("コマンド")]
        [SerializeField] private List<EventCommandData> commands = new List<EventCommandData>();

        [Header("オプション")]
        [SerializeField] private bool autoSetSelfSwitch = false;
        [SerializeField] private string selfSwitchName = "A";
        [SerializeField] private bool walkThrough = false;
        [SerializeField] private bool directionFix = false;

        // プロパティ
        public string PageName => pageName;
        public int Priority => priority;
        public bool Enabled => enabled;
        public EventConditions Conditions => conditions;
        public EventTrigger Trigger => trigger;
        public EventPriority DisplayPriority => displayPriority;
        public EventGraphic Graphic => graphic;
        public EventMoveType MoveType => moveType;
        public float MoveSpeed => moveSpeed;
        public List<EventCommandData> Commands => commands;
        public bool AutoSetSelfSwitch => autoSetSelfSwitch;
        public string SelfSwitchName => selfSwitchName;

        public void SetEnable(bool value)
        {
            enabled = value;
        }

        /// <summary>
        /// 条件をチェック
        /// </summary>
        public bool CheckConditions()
        {
            if (!enabled) return false;
            return conditions?.CheckAllConditions() ?? true;
        }

        /// <summary>
        /// ページを複製
        /// </summary>
        public EventPage Clone()
        {
            var clone = new EventPage
            {
                pageName = pageName + " (Copy)",
                priority = priority,
                enabled = enabled,
                conditions = conditions?.Clone(),
                trigger = trigger,
                displayPriority = displayPriority,
                graphic = graphic?.Clone(),
                moveType = moveType,
                moveSpeed = moveSpeed,
                moveAnimation = moveAnimation,
                autoSetSelfSwitch = autoSetSelfSwitch,
                selfSwitchName = selfSwitchName,
                walkThrough = walkThrough,
                directionFix = directionFix
            };

            // コマンドを複製
            clone.commands = new List<EventCommandData>();
            foreach (var cmd in commands)
            {
                clone.commands.Add(cmd.Clone());
            }

            return clone;
        }
    }

    /// <summary>
    /// イベントの出現条件
    /// </summary>
    [System.Serializable]
    public class EventConditions
    {
        [Header("スイッチ条件")]
        [SerializeField] private List<SwitchCondition> switchConditions = new List<SwitchCondition>();

        [Header("変数条件")]
        [SerializeField] private List<VariableCondition> variableConditions = new List<VariableCondition>();

        [Header("セルフスイッチ条件")]
        [SerializeField] private List<SelfSwitchCondition> selfSwitchConditions = new List<SelfSwitchCondition>();

        [Header("アイテム条件")]
        [SerializeField] private List<ItemCondition> itemConditions = new List<ItemCondition>();

        [Header("カスタム条件")]
        [SerializeField] private string customConditionScript = "";

        /// <summary>
        /// すべての条件をチェック
        /// </summary>
        public bool CheckAllConditions()
        {
            // スイッチ条件
            foreach (var condition in switchConditions)
            {
                if (condition.enabled && !condition.Check())
                    return false;
            }

            // 変数条件
            foreach (var condition in variableConditions)
            {
                if (condition.enabled && !condition.Check())
                    return false;
            }

            // セルフスイッチ条件
            foreach (var condition in selfSwitchConditions)
            {
                if (condition.enabled && !condition.Check())
                    return false;
            }

            // アイテム条件
            foreach (var condition in itemConditions)
            {
                if (condition.enabled && !condition.Check())
                    return false;
            }

            // カスタム条件（将来の拡張用）
            if (!string.IsNullOrEmpty(customConditionScript))
            {
                // カスタムスクリプトの評価
                // 実装は省略
            }

            return true;
        }

        /// <summary>
        /// 条件を複製
        /// </summary>
        public EventConditions Clone()
        {
            var clone = new EventConditions
            {
                customConditionScript = customConditionScript
            };

            clone.switchConditions = switchConditions.Select(c => c.Clone()).ToList();
            clone.variableConditions = variableConditions.Select(c => c.Clone()).ToList();
            clone.selfSwitchConditions = selfSwitchConditions.Select(c => c.Clone()).ToList();
            clone.itemConditions = itemConditions.Select(c => c.Clone()).ToList();

            return clone;
        }
    }

    /// <summary>
    /// スイッチ条件
    /// </summary>
    [System.Serializable]
    public class SwitchCondition
    {
        public bool enabled = true;
        public string switchName = "";
        public bool requiredValue = true;

        public bool Check()
        {
            if (string.IsNullOrEmpty(switchName)) return true;
            return EventSystem.Instance.GetSwitch(switchName) == requiredValue;
        }

        public SwitchCondition Clone()
        {
            return new SwitchCondition
            {
                enabled = enabled,
                switchName = switchName,
                requiredValue = requiredValue
            };
        }
    }

    /// <summary>
    /// 変数条件
    /// </summary>
    [System.Serializable]
    public class VariableCondition
    {
        public bool enabled = true;
        public string variableName = "";
        public ComparisonOperator comparisonOperator = ComparisonOperator.Equal;
        public int value = 0;

        public bool Check()
        {
            if (string.IsNullOrEmpty(variableName)) return true;

            int currentValue = EventSystem.Instance.GetVariable(variableName);

            switch (comparisonOperator)
            {
                case ComparisonOperator.Equal:
                    return currentValue == value;
                case ComparisonOperator.NotEqual:
                    return currentValue != value;
                case ComparisonOperator.Greater:
                    return currentValue > value;
                case ComparisonOperator.GreaterOrEqual:
                    return currentValue >= value;
                case ComparisonOperator.Less:
                    return currentValue < value;
                case ComparisonOperator.LessOrEqual:
                    return currentValue <= value;
                default:
                    return true;
            }
        }

        public VariableCondition Clone()
        {
            return new VariableCondition
            {
                enabled = enabled,
                variableName = variableName,
                comparisonOperator = comparisonOperator,
                value = value
            };
        }
    }

    /// <summary>
    /// セルフスイッチ条件
    /// </summary>
    [System.Serializable]
    public class SelfSwitchCondition
    {
        public bool enabled = true;
        public string switchName = "A";
        public bool requiredValue = true;
        private int eventID; // 実行時に設定

        public void SetEventID(int id)
        {
            eventID = id;
        }

        public bool Check()
        {
            return EventSystem.Instance.GetSelfSwitch(eventID, switchName) == requiredValue;
        }

        public SelfSwitchCondition Clone()
        {
            return new SelfSwitchCondition
            {
                enabled = enabled,
                switchName = switchName,
                requiredValue = requiredValue
            };
        }
    }

    /// <summary>
    /// アイテム条件
    /// </summary>
    [System.Serializable]
    public class ItemCondition
    {
        public bool enabled = true;
        public int itemID = 0;
        public int requiredAmount = 1;

        public bool Check()
        {
            // アイテムシステムが実装されたら、ここでチェック
            // 仮実装
            return true;
        }

        public ItemCondition Clone()
        {
            return new ItemCondition
            {
                enabled = enabled,
                itemID = itemID,
                requiredAmount = requiredAmount
            };
        }
    }

    /// <summary>
    /// イベントグラフィック
    /// </summary>
    [System.Serializable]
    public class EventGraphic
    {
        public Sprite sprite;
        public string animationName = "";
        public Direction direction = Direction.South;
        public float opacity = 1f;
        public bool blendMode = false;

        public EventGraphic Clone()
        {
            return new EventGraphic
            {
                sprite = sprite,
                animationName = animationName,
                direction = direction,
                opacity = opacity,
                blendMode = blendMode
            };
        }
    }

    /// <summary>
    /// イベントトリガー
    /// </summary>
    public enum EventTrigger
    {
        ActionButton,   // 決定ボタン
        PlayerTouch,    // プレイヤー接触
        EventTouch,     // イベント接触
        Autorun,        // 自動実行
        Parallel        // 並列処理
    }

    /// <summary>
    /// 比較演算子
    /// </summary>
    public enum ComparisonOperator
    {
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual
    }
}
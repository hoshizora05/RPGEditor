using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestSystem
{
    public class GlobalVariableManager : MonoBehaviour
    {
        public static GlobalVariableManager Instance { get; private set; }

        private Dictionary<string, object> globalVariables = new Dictionary<string, object>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public T GetVariable<T>(string key)
        {
            if (globalVariables.ContainsKey(key) && globalVariables[key] is T)
            {
                return (T)globalVariables[key];
            }
            return default(T);
        }

        public void SetVariable<T>(string key, T value)
        {
            globalVariables[key] = value;
        }

        public bool HasVariable(string key)
        {
            return globalVariables.ContainsKey(key);
        }

        public void RemoveVariable(string key)
        {
            globalVariables.Remove(key);
        }

        public void ClearAllVariables()
        {
            globalVariables.Clear();
        }

        // Save/Load methods for persistence
        public Dictionary<string, object> GetAllVariables()
        {
            return new Dictionary<string, object>(globalVariables);
        }

        public void LoadVariables(Dictionary<string, object> variables)
        {
            globalVariables = new Dictionary<string, object>(variables);
        }
    }
}
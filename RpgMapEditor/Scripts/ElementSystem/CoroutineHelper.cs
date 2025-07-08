using System;
using System.Collections.Generic;
using UnityEngine;
using RPGStatsSystem;

namespace RPGElementSystem
{
    /// <summary>
    /// コルーチンヘルパークラス
    /// </summary>
    public static class CoroutineHelper
    {
        private static CoroutineRunner runner;

        static CoroutineHelper()
        {
            var go = new GameObject("CoroutineHelper");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<CoroutineRunner>();
        }

        public static Coroutine DelayedCall(float delay, System.Action callback)
        {
            return runner.StartCoroutine(DelayedCallCoroutine(delay, callback));
        }

        private static System.Collections.IEnumerator DelayedCallCoroutine(float delay, System.Action callback)
        {
            yield return new WaitForSeconds(delay);
            callback?.Invoke();
        }

        private class CoroutineRunner : MonoBehaviour { }
    }
}
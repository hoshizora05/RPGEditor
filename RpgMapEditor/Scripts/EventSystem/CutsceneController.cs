using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using System.Collections.Generic;
using System.Linq;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem
{
    /// <summary>
    /// カットシーンコントローラー（基本版）
    /// </summary>
    public class CutsceneController : MonoBehaviour
    {
        private CutsceneData cutsceneData;
        private ExecutionMode executionMode;
        private PlayableDirector playableDirector;
        private EventInterpreter eventInterpreter;
        private bool isPlaying = false;

        public CutsceneData CutsceneData => cutsceneData;
        public bool IsPlaying => isPlaying;

        public void Initialize(CutsceneData data, ExecutionMode mode)
        {
            cutsceneData = data;
            executionMode = mode;

            SetupComponents();
        }

        private void SetupComponents()
        {
            // Timeline Director
            if (executionMode == ExecutionMode.Timeline || executionMode == ExecutionMode.Hybrid)
            {
                if (cutsceneData.TimelineAsset != null)
                {
                    playableDirector = gameObject.AddComponent<PlayableDirector>();
                    playableDirector.playableAsset = cutsceneData.TimelineAsset;
                }
            }

            // Event Interpreter
            if (executionMode == ExecutionMode.Command || executionMode == ExecutionMode.Hybrid)
            {
                if (cutsceneData.Commands != null && cutsceneData.Commands.Count > 0)
                {
                    eventInterpreter = gameObject.AddComponent<EventInterpreter>();
                }
            }
        }

        public System.Collections.IEnumerator Execute()
        {
            isPlaying = true;

            // セットアップ
            yield return SetupActors();

            // 実行
            switch (executionMode)
            {
                case ExecutionMode.Timeline:
                    yield return ExecuteTimeline();
                    break;

                case ExecutionMode.Command:
                    yield return ExecuteCommands();
                    break;

                case ExecutionMode.Hybrid:
                    yield return ExecuteHybrid();
                    break;
            }

            // クリーンアップ
            yield return Cleanup();

            isPlaying = false;
        }

        private System.Collections.IEnumerator SetupActors()
        {
            // アクターをスポーン
            foreach (var actorRef in cutsceneData.ActorReferences)
            {
                if (actorRef.actorPrefab != null)
                {
                    GameObject actorObj = Instantiate(actorRef.actorPrefab, actorRef.spawnPosition, actorRef.spawnRotation);
                    ActorController actor = actorObj.GetComponent<ActorController>();
                    if (actor == null)
                    {
                        actor = actorObj.AddComponent<ActorController>();
                    }
                    actor.Initialize(actorRef.actorID);
                    EventSystem.Instance.RegisterActor(actor);
                }
            }
            yield return null;
        }

        private System.Collections.IEnumerator ExecuteTimeline()
        {
            if (playableDirector != null)
            {
                playableDirector.Play();
                yield return new WaitUntil(() => playableDirector.state != PlayState.Playing);
            }
        }

        private System.Collections.IEnumerator ExecuteCommands()
        {
            if (eventInterpreter != null)
            {
                eventInterpreter.StartInterpretationCutscene(cutsceneData.Commands);
                while (eventInterpreter.IsRunning)
                {
                    yield return null; // コマンドの実行中は待機
                }
                //yield return StartCoroutine(eventInterpreter.StartInterpretation(cutsceneData.Commands));
            }
        }

        private System.Collections.IEnumerator ExecuteHybrid()
        {
            // Timeline と Commands を並行実行
            var timelineCoroutine = StartCoroutine(ExecuteTimeline());
            var commandCoroutine = StartCoroutine(ExecuteCommands());

            yield return timelineCoroutine;
            yield return commandCoroutine;
        }

        private System.Collections.IEnumerator Cleanup()
        {
            // アクターをクリーンアップ
            foreach (var actor in EventSystem.Instance.actorControllers.ToList())
            {
                EventSystem.Instance.UnregisterActor(actor);
            }
            yield return null;
        }

        public void Stop()
        {
            if (playableDirector != null && playableDirector.state == PlayState.Playing)
            {
                playableDirector.Stop();
            }

            if (eventInterpreter != null)
            {
                eventInterpreter.StopInterpretation();
            }

            isPlaying = false;
        }
    }
}
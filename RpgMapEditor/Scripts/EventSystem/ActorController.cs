using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RPGSystem.EventSystem.Commands;

namespace RPGSystem.EventSystem
{
    /// <summary>
    /// アクターコントローラー（基本版）
    /// </summary>
    public class ActorController : MonoBehaviour
    {
        [SerializeField] private string actorID;

        public string ActorID => actorID;

        public void Initialize(string id)
        {
            actorID = id;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void PlayAnimation(string animationName)
        {
            Animator animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play(animationName);
            }
        }
    }
}

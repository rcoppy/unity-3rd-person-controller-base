using System;
using GGJ2022;
using UnityEngine;

namespace DefaultNamespace
{
    public class PlayerAnimationStateManager : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        public void HandleWalkStart()
        {
            _animator.SetBool("IsWalking",true);
        }

        public void HandleWalkEnd()
        {
            _animator.SetBool("IsWalking", false);
        }
    }
}
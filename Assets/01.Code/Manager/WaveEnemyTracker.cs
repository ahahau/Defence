using System;
using UnityEngine;

namespace _01.Code.Manager
{
    public class WaveEnemyTracker : MonoBehaviour
    {
        public Action OnEnemyDied;

        private void OnDestroy()
        {
            OnEnemyDied?.Invoke();
        }
    }
}

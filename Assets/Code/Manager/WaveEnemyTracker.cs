using System;
using UnityEngine;

namespace Code.Manager
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

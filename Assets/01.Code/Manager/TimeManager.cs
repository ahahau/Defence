using UnityEngine;

namespace _01.Code.Manager
{
    public class TimeManager : MonoBehaviour
    {
        public bool IsNightTime { get; private set; }
        public int CurrentHour { get; private set; } = 0;
        
        [field: SerializeField] public int DayTimeHour { get; private set; } = 120;
        [field:SerializeField] public int NightTimeHour { get; private set; } = 80;

        private bool _runTime = false;
        public void Initialize()
        {
            _runTime = true;
        }

        private void Update()
        {
            
        }
    }
}
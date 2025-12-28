using UnityEngine;

namespace _01.Code.Manager
{
    public class TimeManager : MonoBehaviour
    {
        public bool AttackTimr { get; private set; }
        
        public void Initialize()
        {
            AttackTimr = false;
        }
        public void SetAttackTimer(bool value)
        {
            AttackTimr = value;
        }
    }
}
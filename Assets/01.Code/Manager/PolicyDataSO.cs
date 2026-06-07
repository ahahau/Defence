using UnityEngine;

namespace _01.Code.Manager
{
    [CreateAssetMenu(menuName = "SO/Management/Policy", fileName = "PolicyData")]
    public class PolicyDataSO : ScriptableObject
    {
        [SerializeField] private string displayName = "정책";
        [SerializeField, TextArea] private string description;
        [SerializeField, Range(-100, 100)] private int moraleDeltaOnSelect;
        [SerializeField] private int goldDeltaOnSelect;
        [SerializeField, Range(-20, 20)] private int dailyMoraleDelta;
        [SerializeField, Min(0)] private int durationDays = 1;
        [SerializeField] private bool canRepeat = true;

        public string DisplayName => displayName;
        public string Description => description;
        public int MoraleDeltaOnSelect => moraleDeltaOnSelect;
        public int GoldDeltaOnSelect => goldDeltaOnSelect;
        public int DailyMoraleDelta => dailyMoraleDelta;
        public int DurationDays => durationDays;
        public bool CanRepeat => canRepeat;
    }
}

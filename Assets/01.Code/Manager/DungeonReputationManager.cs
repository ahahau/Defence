using UnityEngine;

namespace _01.Code.Manager
{
    /// <summary>귀환한 방문객의 후기를 모아 다음 영업의 규모와 소비력을 결정한다.</summary>
    public sealed class DungeonReputationManager : MonoBehaviour
    {
        public const int DefaultReputation = 50;
        public static DungeonReputationManager Current { get; private set; }

        [SerializeField, Range(0, 100)] private int reputation = DefaultReputation;

        private int _dailyReviewCount;
        private int _dailySatisfactionTotal;
        private int _dailyReputationDelta;

        public int Reputation => Mathf.Clamp(reputation, 0, 100);
        public int DailyReviewCount => _dailyReviewCount;
        public int DailyAverageSatisfaction => _dailyReviewCount > 0
            ? Mathf.RoundToInt(_dailySatisfactionTotal / (float)_dailyReviewCount)
            : 0;
        public int LastReputationDelta { get; private set; }
        public int LastReviewCount { get; private set; }
        public int LastAverageSatisfaction { get; private set; }

        private void Awake()
        {
            Current = this;
            reputation = Mathf.Clamp(reputation, 0, 100);
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public void BeginBusinessDay()
        {
            _dailyReviewCount = 0;
            _dailySatisfactionTotal = 0;
            _dailyReputationDelta = 0;
        }

        public void RecordReview(int satisfaction)
        {
            var safe = Mathf.Clamp(satisfaction, 0, 100);
            _dailyReviewCount++;
            _dailySatisfactionTotal += safe;
            _dailyReputationDelta += DungeonReputationRules.ResolveReviewDelta(safe);
        }

        public void FinalizeBusinessDay()
        {
            LastReviewCount = _dailyReviewCount;
            LastAverageSatisfaction = DailyAverageSatisfaction;
            LastReputationDelta = Mathf.Clamp(_dailyReputationDelta, -10, 10);
            reputation = Mathf.Clamp(reputation + LastReputationDelta, 0, 100);
            _dailyReviewCount = 0;
            _dailySatisfactionTotal = 0;
            _dailyReputationDelta = 0;
        }

        public void RestoreReputation(int value)
        {
            reputation = Mathf.Clamp(value, 0, 100);
            BeginBusinessDay();
            LastReputationDelta = 0;
            LastReviewCount = 0;
            LastAverageSatisfaction = 0;
        }
    }

    public static class DungeonReputationRules
    {
        public static int ResolveReviewDelta(int satisfaction)
        {
            var safe = Mathf.Clamp(satisfaction, 0, 100);
            if (safe >= 80) return 3;
            if (safe >= 60) return 1;
            if (safe >= 40) return 0;
            if (safe >= 20) return -2;
            return -4;
        }

        public static int ResolveVisitorCount(int baseCount, int reputation)
        {
            if (baseCount <= 0)
                return 0;
            var multiplier = Mathf.Lerp(0.6f, 1.5f, Mathf.Clamp01(reputation / 100f));
            return Mathf.Max(1, Mathf.RoundToInt(baseCount * multiplier));
        }

        public static int ResolveVisitorLevel(int baseLevel, int reputation)
        {
            var bonus = reputation >= 80 ? 1 : reputation <= 20 ? -1 : 0;
            return Mathf.Max(1, baseLevel + bonus);
        }

        public static int ResolveBudget(int baseBudget, int reputation)
        {
            var multiplier = Mathf.Lerp(0.75f, 1.5f, Mathf.Clamp01(reputation / 100f));
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, baseBudget) * multiplier));
        }

        public static string GetGrade(int reputation)
        {
            var safe = Mathf.Clamp(reputation, 0, 100);
            if (safe >= 80) return "명소";
            if (safe >= 60) return "인기";
            if (safe >= 40) return "보통";
            if (safe >= 20) return "악평";
            return "외면";
        }
    }
}

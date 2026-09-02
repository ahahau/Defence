using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Manager
{
    /// <summary>
    /// 연속으로 완벽하게 막아낸 날 수와, 그 대가.
    ///
    /// 20일이 늘 같은 모양으로 흐르는 것이 이 게임의 가장 큰 문제였다.
    /// 잘 막으면 다음 날이 더 후하되 더 무거워지면, 같은 일차라도 판마다 다른 압력이 걸린다.
    /// 연속이 끊기면 보상도 압력도 함께 0으로 돌아가므로, 한 번 새면 숨을 돌릴 수 있다.
    ///
    /// 이 값은 다음 웨이브에 적용된다 — 오늘 쌓은 연속이 오늘의 보상을 바꾸지는 않는다.
    /// 이미 시작할 때 정해진 값을 뒤늦게 흔들면 화면에 뜬 숫자와 어긋난다.
    /// </summary>
    public sealed class DefenseStreakSystem : MonoBehaviour
    {
        public static DefenseStreakSystem Current { get; private set; }

        [SerializeField] private GameEventChannelSO waveEventChannel;

        [SerializeField, Range(0f, 0.5f), Tooltip("연속 1회마다 붙는 웨이브 보상 배율.")]
        private float rewardPerStreak = 0.15f;

        [SerializeField, Min(1), Tooltip("보상이 더 오르지 않는 지점. 없으면 후반이 무한정 불어난다.")]
        private int maxRewardStreak = 5;

        [SerializeField, Min(0), Tooltip("연속 1회마다 다음 습격에 더해지는 인원.")]
        private int enemiesPerStreak = 1;

        [SerializeField, Min(0), Tooltip("연속으로 더해질 수 있는 최대 인원.")]
        private int maxExtraEnemies = 6;

        public int CurrentStreak { get; private set; }
        public int BestStreak { get; private set; }

        public event System.Action StreakChanged;

        /// <summary>다음 웨이브 보상에 곱해질 값.</summary>
        public float RewardMultiplier =>
            1f + rewardPerStreak * Mathf.Min(CurrentStreak, Mathf.Max(1, maxRewardStreak));

        /// <summary>다음 웨이브에 더해질 인원.</summary>
        public int ExtraEnemies =>
            Mathf.Min(CurrentStreak * Mathf.Max(0, enemiesPerStreak), Mathf.Max(0, maxExtraEnemies));

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"Duplicate {nameof(DefenseStreakSystem)} detected. Keep exactly one scene instance.", this);
                enabled = false;
                return;
            }

            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        private void OnEnable() => waveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
        private void OnDisable() => waveEventChannel?.RemoveListener<WaveEndedEvent>(HandleWaveEnded);

        private void HandleWaveEnded(WaveEndedEvent evt)
        {
            // 한 명이라도 흘려보내면 끊긴다. "거의 막았다"에 보상을 주면 연속이 의미를 잃는다.
            lastKnownDay = evt.Day;
            CurrentStreak = evt.ClearRate >= 1f ? CurrentStreak + 1 : 0;
            BestStreak = Mathf.Max(BestStreak, CurrentStreak);
            StreakChanged?.Invoke();
        }

        [Header("청산")]
        [SerializeField] private GameEventChannelSO costEventChannel;

        [SerializeField, Min(0), Tooltip("청산할 때 연속 1회가 주는 금화. 일차가 오르면 함께 커진다.")]
        private int goldPerStreak = 30;

        [SerializeField, Range(0f, 0.3f), Tooltip("일차마다 청산 금액에 붙는 성장률.")]
        private float goldGrowthPerDay = 0.05f;

        private int lastKnownDay = 1;

        /// <summary>지금 청산하면 받을 금화. 0이면 청산할 것이 없다.</summary>
        public int CashOutValue =>
            CurrentStreak <= 0
                ? 0
                : Mathf.RoundToInt(CurrentStreak * goldPerStreak * (1f + goldGrowthPerDay * Mathf.Max(0, lastKnownDay - 1)));

        public bool CanCashOut => CashOutValue > 0;

        /// <summary>
        /// 쌓인 연속을 금화로 바꾸고 0으로 되돌린다.
        ///
        /// 이게 이 시스템의 결정이다. 그냥 두면 보상 배율이 계속 오르지만 습격도 함께 무거워지고,
        /// 실측에서는 끊기는 순간이 곧 게임오버였다 — 스스로 멈출 수 있어야 위험을 감수할 값이 생긴다.
        /// </summary>
        public bool TryCashOut(out int gold)
        {
            gold = CashOutValue;
            if (gold <= 0)
                return false;

            costEventChannel?.RaiseEvent(new GoldEarnedEvent(gold, GoldChangeSource.General));
            CurrentStreak = 0;
            StreakChanged?.Invoke();
            return true;
        }

        /// <summary>저장에서 되돌린다.</summary>
        public void RestoreState(int current, int best)
        {
            CurrentStreak = Mathf.Max(0, current);
            BestStreak = Mathf.Max(BestStreak, Mathf.Max(0, best));
            StreakChanged?.Invoke();
        }

        /// <summary>준비 화면에 붙일 한 줄. 연속이 없으면 아무 말도 하지 않는다.</summary>
        public string DescribeForPreparation()
        {
            if (CurrentStreak <= 0)
                return string.Empty;

            var bonus = Mathf.RoundToInt((RewardMultiplier - 1f) * 100f);
            var extra = ExtraEnemies;
            return extra > 0
                ? $"<color=#7ADB8A>연속 방어 {CurrentStreak}일</color> · 보상 +{bonus}% · 습격 +{extra}명"
                : $"<color=#7ADB8A>연속 방어 {CurrentStreak}일</color> · 보상 +{bonus}%";
        }
    }
}

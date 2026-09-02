using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;
using _01.Code.Persistence;

namespace _01.Code.Manager
{
    public class MoralePolicyManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO managementEventChannel;

        [Header("Morale")]
        [SerializeField, Range(0, 100)] private int initialMorale = 50;
        [SerializeField, Range(-20, 20),
         Tooltip("완전히 막아냈을 때의 민심 변화.")]
        private int waveClearMoraleDelta = 2;

        [SerializeField, Range(-20, 20),
         Tooltip("한 명도 못 막았을 때의 민심 변화. 격퇴율에 따라 이 값과 완전 방어 값 사이를 오간다.\n" +
                 "예전에는 이 축이 없어서, 0/14로 전멸한 날에도 '방어 성공' 보너스가 그대로 붙었다.")]
        private int waveBreachMoraleDelta = -6;
        [SerializeField, Range(-20, 20)] private int dailyRecoveryDelta = 1;

        [Header("Morale Consequences")]
        [SerializeField, Min(1f), Tooltip("민심이 바닥일 때의 유지비 배율. 불안하면 위험수당을 더 줘야 한다.")]
        private float upkeepAtZeroMorale = 1.5f;

        [SerializeField, Range(0f, 1f), Tooltip("민심이 가득할 때의 유지비 배율.")]
        private float upkeepAtFullMorale = 0.8f;

        [SerializeField, Range(0f, 1f), Tooltip("민심이 바닥일 때 찾아오는 지원자 비율. 0이면 아무도 오지 않는다.")]
        private float applicantsAtZeroMorale = 0.25f;

        [SerializeField, Min(1f),
         Tooltip("민심이 바닥일 때의 웨이브 보상 배율. 뒤처진 판이 만회할 수 있는 유일한 통로다.\n" +
                 "민심이 가득할 때는 1이라 잘 굴러가는 판의 균형은 건드리지 않는다.")]
        private float rewardAtZeroMorale = 1.4f;

        [Header("Policies")]
        [SerializeField] private PolicyDataSO[] availablePolicies;
        [SerializeField, Min(1)] private int offeredPolicyCount = 3;
        [SerializeField] private bool autoOfferPolicies = true;
        [SerializeField, Min(1)] private int offerIntervalDays = 3;
        [SerializeField] private bool offerAfterWaveEnd = true;

        [SerializeField, Tooltip("두 정책이 동시에 살아 있을 때 따로 붙는 효과. 비어 있으면 조합이 없다.")]
        private PolicyComboCatalogSO policyCombos;

        private readonly List<PolicyDataSO> currentChoices = new();
        private readonly List<PolicyDataSO> selectedPolicies = new();
        private readonly List<ActivePolicy> activePolicies = new();
        private readonly List<PolicyDataSO> activePolicyBuffer = new();
        private readonly List<PolicyCombo> activeCombos = new();

        private int currentDay;

        public static MoralePolicyManager Current { get; private set; }

        public int CurrentMorale { get; private set; }
        public IReadOnlyList<PolicyDataSO> CurrentChoices => currentChoices;
        public event System.Action CombatModifiersChanged;

        /// <summary>지금 성립한 조합. 화면에 무엇이 걸려 있는지 보여 주는 데 쓴다.</summary>
        public IReadOnlyList<PolicyCombo> ActiveCombos
        {
            get { RefreshActiveCombos(); return activeCombos; }
        }

        public float UnitDamageMultiplier =>
            Mathf.Max(0.1f, MultiplyActivePolicies(policy => policy.UnitDamageMultiplier) * MultiplyActiveCombos(combo => combo.UnitDamageMultiplier));
        public int UnitDefenseBonus =>
            SumActivePolicies(policy => policy.UnitDefenseBonus) + SumActiveCombos(combo => combo.UnitDefenseBonus);
        public float DungeonPowerGainMultiplier =>
            Mathf.Max(0.1f, MultiplyActivePolicies(policy => policy.DungeonPowerGainMultiplier) * MultiplyActiveCombos(combo => combo.DungeonPowerGainMultiplier));

        /// <summary>민심 0~100을 0~1로. 곡선을 한 곳에 모아 두어 소비처마다 다르게 해석하지 않게 한다.</summary>
        private float MoraleRatio => Mathf.Clamp01(CurrentMorale / 100f);

        /// <summary>
        /// 유지비 배율. 민심이 낮으면 같은 부하를 데리고 있는 데 더 든다.
        /// 정산은 매일 읽는 화면이라, 여기에 걸어야 민심이 숫자로 즉시 체감된다.
        /// </summary>
        public float UpkeepMultiplier =>
            Mathf.Lerp(Mathf.Max(1f, upkeepAtZeroMorale), Mathf.Clamp01(upkeepAtFullMorale), MoraleRatio);

        /// <summary>
        /// 웨이브 보상 배율. 유지비의 대칭축이다 — 민심이 낮으면 보상이 오른다.
        ///
        /// 이게 없으면 민심은 일방통행이 된다. 실패 → 민심↓ → 유지비↑ → 자금난 → 더 실패.
        /// 실측에서 3일차 광산 하나 차이가 8일차 유닛 한 명 차이로 벌어지고 되돌릴 길이 없었다.
        /// 뒤처진 판에만 걸리고 잘 굴러가는 판에는 1이라, 성공을 깎지 않고 실패만 눅인다.
        /// </summary>
        public float WaveRewardMultiplier =>
            Mathf.Lerp(Mathf.Max(1f, rewardAtZeroMorale), 1f, MoraleRatio);

        /// <summary>민심이 나쁘면 찾아오는 지원자도 줄어든다.</summary>
        public int AdjustRecruitCount(int baseCount)
        {
            if (baseCount <= 0)
                return 0;

            var ratio = Mathf.Lerp(Mathf.Clamp01(applicantsAtZeroMorale), 1f, MoraleRatio);
            return Mathf.Max(0, Mathf.RoundToInt(baseCount * ratio));
        }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"Duplicate {nameof(MoralePolicyManager)} detected. Keep exactly one scene instance.", this);
                enabled = false;
                return;
            }

            Current = this;
            CurrentMorale = Mathf.Clamp(initialMorale, 0, 100);
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        private void OnEnable()
        {
            dayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);
            waveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
            managementEventChannel?.AddListener<MoraleChangeRequestedEvent>(HandleMoraleChangeRequested);
        }

        private void Start()
        {
            RaiseMoraleChanged(0, "초기 민심");
        }

        private void OnDisable()
        {
            dayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);
            waveEventChannel?.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            managementEventChannel?.RemoveListener<MoraleChangeRequestedEvent>(HandleMoraleChangeRequested);
        }

        public void SelectPolicy(PolicyDataSO policy)
        {
            if (policy == null || !currentChoices.Contains(policy))
                return;

            selectedPolicies.Add(policy);
            currentChoices.Clear();

            ApplyImmediatePolicyEffect(policy);
            AddActivePolicy(policy);
            CombatModifiersChanged?.Invoke();
            managementEventChannel?.RaiseEvent(new PolicySelectedEvent(currentDay, policy));
        }

        public void OfferPolicies()
        {
            currentChoices.Clear();

            if (availablePolicies == null || availablePolicies.Length == 0)
                return;

            var candidates = BuildCandidatePolicies();
            var choiceLimit = Mathf.Min(offeredPolicyCount, candidates.Count);

            for (var i = 0; i < choiceLimit; i++)
            {
                var selectedIndex = Random.Range(0, candidates.Count);
                currentChoices.Add(candidates[selectedIndex]);
                candidates.RemoveAt(selectedIndex);
            }

            if (currentChoices.Count > 0)
                managementEventChannel?.RaiseEvent(new PolicyChoicesOfferedEvent(currentDay, currentChoices));
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            currentDay = evt.Day;
            ChangeMorale(dailyRecoveryDelta, "일일 안정도");
            ApplyActivePolicyEffects();

            if (!offerAfterWaveEnd && ShouldOfferPolicy())
                OfferPolicies();
        }

        private void HandleWaveEnded(WaveEndedEvent evt)
        {
            currentDay = evt.Day;

            // 격퇴율에 따라 완전 방어와 완전 돌파 사이를 오간다.
            // 완전히 막으면 예전과 같은 값이라 잘 막던 판의 감각은 그대로다.
            var clearRate = evt.ClearRate;
            var delta = Mathf.RoundToInt(Mathf.Lerp(waveBreachMoraleDelta, waveClearMoraleDelta, clearRate));
            var reason = clearRate >= 1f
                ? "방어 성공"
                : $"방어 실패 ({evt.KillCount}/{evt.EnemyCount})";
            ChangeMorale(delta, reason);

            if (offerAfterWaveEnd && ShouldOfferPolicy())
                OfferPolicies();
        }

        private void HandleMoraleChangeRequested(MoraleChangeRequestedEvent evt)
        {
            ChangeMorale(evt.Delta, evt.Reason);
        }

        private bool ShouldOfferPolicy()
        {
            if (!autoOfferPolicies)
                return false;

            return offerIntervalDays <= 1 || currentDay % offerIntervalDays == 0;
        }

        private List<PolicyDataSO> BuildCandidatePolicies()
        {
            var candidates = new List<PolicyDataSO>();
            foreach (var policy in availablePolicies)
            {
                if (policy == null)
                    continue;

                if (!policy.CanRepeat && selectedPolicies.Contains(policy))
                    continue;

                candidates.Add(policy);
            }

            return candidates;
        }

        private void ApplyImmediatePolicyEffect(PolicyDataSO policy)
        {
            ChangeMorale(policy.MoraleDeltaOnSelect, policy.DisplayName);

            if (policy.GoldDeltaOnSelect > 0)
                costEventChannel?.RaiseEvent(new GoldEarnedEvent(policy.GoldDeltaOnSelect, GoldChangeSource.Policy));
            else if (policy.GoldDeltaOnSelect < 0)
                costEventChannel?.RaiseEvent(new GoldLostEvent(Mathf.Abs(policy.GoldDeltaOnSelect), GoldChangeSource.Policy));
        }

        private void AddActivePolicy(PolicyDataSO policy)
        {
            if (policy.DurationDays <= 0 || (policy.DailyMoraleDelta == 0 && !policy.HasCombatEffect))
                return;

            activePolicies.Add(new ActivePolicy(policy, policy.DurationDays));
        }

        private void ApplyActivePolicyEffects()
        {
            // 조합은 정책이 줄어들기 전에 센다. 오늘 하루는 두 정책이 함께 살아 있던 날이므로,
            // 오늘 만료되는 정책의 조합도 오늘 몫은 받아야 한다.
            RefreshActiveCombos();
            var todaysCombos = new List<PolicyCombo>(activeCombos);

            var changed = false;
            for (var i = activePolicies.Count - 1; i >= 0; i--)
            {
                var activePolicy = activePolicies[i];
                ChangeMorale(activePolicy.Policy.DailyMoraleDelta, activePolicy.Policy.DisplayName);
                activePolicy.RemainingDays--;

                if (activePolicy.RemainingDays <= 0)
                {
                    activePolicies.RemoveAt(i);
                    changed = true;
                }
            }

            foreach (var combo in todaysCombos)
            {
                if (combo != null && combo.DailyMoraleDelta != 0)
                    ChangeMorale(combo.DailyMoraleDelta, combo.DisplayName);
            }

            if (changed)
                CombatModifiersChanged?.Invoke();
        }

        /// <summary>
        /// 지금 살아 있는 정책으로 성립하는 조합을 다시 센다.
        /// 매 프레임 읽히는 값이라 할당 없이 버퍼를 재사용한다.
        /// </summary>
        private void RefreshActiveCombos()
        {
            activeCombos.Clear();
            if (policyCombos == null || activePolicies.Count < 2)
                return;

            activePolicyBuffer.Clear();
            foreach (var active in activePolicies)
            {
                if (active?.Policy != null)
                    activePolicyBuffer.Add(active.Policy);
            }

            policyCombos.CollectActive(activePolicyBuffer, activeCombos);
        }

        /// <summary>
        /// 이 정책이 참여하는 조합 안내. 짝이 지금 돌고 있으면 그렇다고 알려 준다.
        ///
        /// 이게 없으면 조합은 우연히 걸리는 보너스일 뿐 선택이 되지 않는다.
        /// 무엇과 묶이는지 고르는 순간에 보여야 "어제 계엄령을 걸어 뒀으니 오늘은 철벽"이 성립한다.
        /// </summary>
        public string DescribeCombosFor(PolicyDataSO policy)
        {
            if (policy == null || policyCombos == null)
                return string.Empty;

            var lines = new List<string>();
            foreach (var combo in policyCombos.Combos)
            {
                var partner = combo?.GetPartnerOf(policy);
                if (partner == null)
                    continue;

                var partnerActive = false;
                foreach (var active in activePolicies)
                {
                    if (active?.Policy == partner)
                        partnerActive = true;
                }

                var effect = DescribeComboEffect(combo);
                lines.Add(partnerActive
                    ? $"<color=#7ADB8A>조합 성립 · {combo.DisplayName}</color>  {effect}"
                    : $"<color=#9A8B78>조합 · {combo.DisplayName}  ({WithParticle(partner.DisplayName)} 겹칠 때)  {effect}</color>");
            }

            return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
        }

        /// <summary>
        /// 이름 뒤에 붙는 "와/과"를 고른다. 한글 음절의 받침 유무로 갈린다 —
        /// 고정으로 "와"를 쓰면 "계엄령와"처럼 읽는 사람이 걸리는 문장이 나온다.
        /// </summary>
        private static string WithParticle(string noun)
        {
            if (string.IsNullOrEmpty(noun))
                return noun;

            var last = noun[noun.Length - 1];
            if (last < 0xAC00 || last > 0xD7A3)
                return noun + "와";

            var hasFinalConsonant = (last - 0xAC00) % 28 != 0;
            return noun + (hasFinalConsonant ? "과" : "와");
        }

        private static string DescribeComboEffect(PolicyCombo combo)
        {
            var parts = new List<string>();
            if (!Mathf.Approximately(combo.UnitDamageMultiplier, 1f))
                parts.Add($"공격 {FormatComboPercent(combo.UnitDamageMultiplier)}");
            if (combo.UnitDefenseBonus != 0)
                parts.Add($"방어 {(combo.UnitDefenseBonus > 0 ? "+" : string.Empty)}{combo.UnitDefenseBonus}");
            if (!Mathf.Approximately(combo.DungeonPowerGainMultiplier, 1f))
                parts.Add($"권능 {FormatComboPercent(combo.DungeonPowerGainMultiplier)}");
            if (combo.DailyMoraleDelta != 0)
                parts.Add($"민심 {(combo.DailyMoraleDelta > 0 ? "+" : string.Empty)}{combo.DailyMoraleDelta}/일");

            return parts.Count > 0 ? string.Join(" · ", parts) : string.Empty;
        }

        private static string FormatComboPercent(float multiplier)
        {
            var percent = Mathf.RoundToInt((multiplier - 1f) * 100f);
            return percent > 0 ? $"+{percent}%" : $"{percent}%";
        }

        private float MultiplyActiveCombos(System.Func<PolicyCombo, float> selector)
        {
            RefreshActiveCombos();
            var value = 1f;
            foreach (var combo in activeCombos)
            {
                if (combo != null)
                    value *= selector(combo);
            }

            return value;
        }

        private int SumActiveCombos(System.Func<PolicyCombo, int> selector)
        {
            RefreshActiveCombos();
            var value = 0;
            foreach (var combo in activeCombos)
            {
                if (combo != null)
                    value += selector(combo);
            }

            return value;
        }

        private float MultiplyActivePolicies(System.Func<PolicyDataSO, float> selector)
        {
            var value = 1f;
            foreach (var activePolicy in activePolicies)
            {
                if (activePolicy?.Policy != null)
                    value *= selector(activePolicy.Policy);
            }

            return Mathf.Max(0.1f, value);
        }

        private int SumActivePolicies(System.Func<PolicyDataSO, int> selector)
        {
            var value = 0;
            foreach (var activePolicy in activePolicies)
            {
                if (activePolicy?.Policy != null)
                    value += selector(activePolicy.Policy);
            }

            return value;
        }

        private void ChangeMorale(int delta, string reason)
        {
            if (delta == 0)
                return;

            var previousMorale = CurrentMorale;
            CurrentMorale = Mathf.Clamp(CurrentMorale + delta, 0, 100);
            RaiseMoraleChanged(CurrentMorale - previousMorale, reason);
        }

        private void RaiseMoraleChanged(int delta, string reason)
        {
            managementEventChannel?.RaiseEvent(new MoraleChangedEvent(CurrentMorale, delta, reason));
        }

        public void CaptureSaveState(SavedPolicyState target)
        {
            if (target == null)
                return;
            target.selected.Clear();
            target.active.Clear();
            foreach (var policy in selectedPolicies)
                if (policy != null) target.selected.Add(policy.name);
            foreach (var policy in activePolicies)
                if (policy?.Policy != null) target.active.Add(new SavedActivePolicy
                {
                    assetKey = policy.Policy.name,
                    remainingDays = policy.RemainingDays
                });
        }

        public void RestoreCheckpoint(int morale, int day, SavedPolicyState savedPolicies)
        {
            CurrentMorale = Mathf.Clamp(morale, 0, 100);
            currentDay = Mathf.Max(0, day);
            currentChoices.Clear();
            selectedPolicies.Clear();
            activePolicies.Clear();
            if (savedPolicies != null)
            {
                if (savedPolicies.selected != null)
                    foreach (var key in savedPolicies.selected)
                    {
                        var policy = ResolvePolicy(key);
                        if (policy != null && !selectedPolicies.Contains(policy)) selectedPolicies.Add(policy);
                    }
                if (savedPolicies.active != null)
                    foreach (var saved in savedPolicies.active)
                    {
                        var policy = ResolvePolicy(saved.assetKey);
                        if (policy != null && saved.remainingDays > 0)
                            activePolicies.Add(new ActivePolicy(policy, saved.remainingDays));
                    }
            }
            RaiseMoraleChanged(0, "저장 불러오기");
            CombatModifiersChanged?.Invoke();
        }

        private PolicyDataSO ResolvePolicy(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || availablePolicies == null)
                return null;
            foreach (var policy in availablePolicies)
                if (policy != null && policy.name == key) return policy;
            return null;
        }

        private class ActivePolicy
        {
            public ActivePolicy(PolicyDataSO policy, int remainingDays)
            {
                Policy = policy;
                RemainingDays = remainingDays;
            }

            public PolicyDataSO Policy { get; }
            public int RemainingDays { get; set; }
        }
    }
}

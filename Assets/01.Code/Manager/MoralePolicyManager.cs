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
        [SerializeField, Range(-20, 20)] private int waveClearMoraleDelta = 2;
        [SerializeField, Range(-20, 20)] private int dailyRecoveryDelta = 1;

        [Header("Morale Consequences")]
        [SerializeField, Min(1f), Tooltip("민심이 바닥일 때의 유지비 배율. 불안하면 위험수당을 더 줘야 한다.")]
        private float upkeepAtZeroMorale = 1.5f;

        [SerializeField, Range(0f, 1f), Tooltip("민심이 가득할 때의 유지비 배율.")]
        private float upkeepAtFullMorale = 0.8f;

        [SerializeField, Range(0f, 1f), Tooltip("민심이 바닥일 때 찾아오는 지원자 비율. 0이면 아무도 오지 않는다.")]
        private float applicantsAtZeroMorale = 0.25f;

        [Header("Policies")]
        [SerializeField] private PolicyDataSO[] availablePolicies;
        [SerializeField, Min(1)] private int offeredPolicyCount = 3;
        [SerializeField] private bool autoOfferPolicies = true;
        [SerializeField, Min(1)] private int offerIntervalDays = 3;
        [SerializeField] private bool offerAfterWaveEnd = true;

        private readonly List<PolicyDataSO> currentChoices = new();
        private readonly List<PolicyDataSO> selectedPolicies = new();
        private readonly List<ActivePolicy> activePolicies = new();

        private int currentDay;

        public static MoralePolicyManager Current { get; private set; }

        public int CurrentMorale { get; private set; }
        public IReadOnlyList<PolicyDataSO> CurrentChoices => currentChoices;

        /// <summary>민심 0~100을 0~1로. 곡선을 한 곳에 모아 두어 소비처마다 다르게 해석하지 않게 한다.</summary>
        private float MoraleRatio => Mathf.Clamp01(CurrentMorale / 100f);

        /// <summary>
        /// 유지비 배율. 민심이 낮으면 같은 부하를 데리고 있는 데 더 든다.
        /// 정산은 매일 읽는 화면이라, 여기에 걸어야 민심이 숫자로 즉시 체감된다.
        /// </summary>
        public float UpkeepMultiplier =>
            Mathf.Lerp(Mathf.Max(1f, upkeepAtZeroMorale), Mathf.Clamp01(upkeepAtFullMorale), MoraleRatio);

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
            ChangeMorale(waveClearMoraleDelta, "방어 성공");

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
            if (policy.DurationDays <= 0 || policy.DailyMoraleDelta == 0)
                return;

            activePolicies.Add(new ActivePolicy(policy, policy.DurationDays));
        }

        private void ApplyActivePolicyEffects()
        {
            for (var i = activePolicies.Count - 1; i >= 0; i--)
            {
                var activePolicy = activePolicies[i];
                ChangeMorale(activePolicy.Policy.DailyMoraleDelta, activePolicy.Policy.DisplayName);
                activePolicy.RemainingDays--;

                if (activePolicy.RemainingDays <= 0)
                    activePolicies.RemoveAt(i);
            }
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

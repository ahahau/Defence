using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

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

        [Header("Policies")]
        [SerializeField] private PolicyDataSO[] availablePolicies;
        [SerializeField, Min(1)] private int offeredPolicyCount = 3;
        [SerializeField, Min(1)] private int offerIntervalDays = 1;
        [SerializeField] private bool offerAfterWaveEnd = true;

        private readonly List<PolicyDataSO> currentChoices = new();
        private readonly List<PolicyDataSO> selectedPolicies = new();
        private readonly List<ActivePolicy> activePolicies = new();

        private int currentDay;

        public int CurrentMorale { get; private set; }
        public IReadOnlyList<PolicyDataSO> CurrentChoices => currentChoices;

        private void Awake()
        {
            CurrentMorale = Mathf.Clamp(initialMorale, 0, 100);
        }

        private void OnEnable()
        {
            dayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);
            waveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
        }

        private void Start()
        {
            RaiseMoraleChanged(0, "초기 민심");
        }

        private void OnDisable()
        {
            dayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);
            waveEventChannel?.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
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

        private bool ShouldOfferPolicy()
        {
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

using System.Collections.Generic;
using Code.Persistence;
using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 한 판의 사건 순서와 기록.
    ///
    /// 예전에는 운영 방침(3일마다)과 일차 대화(4일마다)가 따로 돌아, 둘이 겹치는 날엔 한 전환에 창이
    /// 두 개씩 떴고 같은 사건을 다시 만나도 지난번에 무엇을 골랐는지 알 수 없었다. 한 전환에는 가장
    /// 중요한 사건 하나만 띄우고, 일반 사건 뒤에는 쉬는 시간을 두며, 고른 결과를 기록해 다시 보여 준다.
    ///
    /// Unity 오브젝트를 모르는 장부다. 전환(영업 종료) 하나당 <see cref="ResolveTransition"/>을 한 번 센다.
    /// </summary>
    public sealed class RunEventLedger
    {
        /// <summary>일반 사건 뒤 다시 일반 사건이 나올 때까지 쉬는 전환 수(포함 범위).</summary>
        public const int MinNormalCooldown = 4;
        public const int MaxNormalCooldown = 6;

        private readonly HashSet<string> _chosenKeys = new();
        private readonly List<SavedDialogueFollowUp> _pendingFollowUps = new();
        private readonly HashSet<string> _completedFollowUps = new();
        private int _resolvedDay = -1;
        private TransitionEventKind _resolvedKind;

        /// <summary>다음 일반·상황 사건까지 남은 전환 수. 0 이하면 다음 전환에 나올 수 있다.</summary>
        public int NormalCooldown { get; private set; } = MinNormalCooldown;

        /// <summary>긴급 사건에 밀린 운영 방침이 다음 전환을 기다리는가.</summary>
        public bool PolicyDeferred { get; private set; }

        public IReadOnlyCollection<string> ChosenKeys => _chosenKeys;
        public int PendingFollowUpCount => _pendingFollowUps.Count;

        /// <summary>한 첫 사건은 한 판에 한 번만 후속을 만든다. 선택하지 않은 사건은 호출하지 않는다.</summary>
        public bool TryScheduleFollowUp(string sourceKey, string sequenceKey, int selectedDay, int delayDays)
        {
            if (string.IsNullOrWhiteSpace(sourceKey) || string.IsNullOrWhiteSpace(sequenceKey)
                || _completedFollowUps.Contains(sourceKey))
                return false;

            foreach (SavedDialogueFollowUp pending in _pendingFollowUps)
            {
                if (pending.sourceKey == sourceKey)
                    return false;
            }

            _pendingFollowUps.Add(new SavedDialogueFollowUp
            {
                sourceKey = sourceKey,
                sequenceKey = sequenceKey,
                dueDay = (int)System.Math.Min(int.MaxValue,
                    (long)System.Math.Max(0, selectedDay) + System.Math.Max(2, delayDays))
            });
            return true;
        }

        /// <summary>등장 허가가 난 일반 사건 자리에서만 조회한다. 예정일 동률은 예약 순서다.</summary>
        public bool TryGetReadyFollowUp(int day, out string sourceKey, out string sequenceKey)
        {
            SavedDialogueFollowUp ready = null;
            foreach (SavedDialogueFollowUp pending in _pendingFollowUps)
            {
                if (pending.dueDay <= day && (ready == null || pending.dueDay < ready.dueDay))
                    ready = pending;
            }

            sourceKey = ready?.sourceKey;
            sequenceKey = ready?.sequenceKey;
            return ready != null;
        }

        /// <summary>후속의 실제 선택 또는 건너뛰기를 확정한다. 단순 닫기/중단은 예약을 남긴다.</summary>
        public void CompleteFollowUp(string sourceKey)
        {
            for (int i = 0; i < _pendingFollowUps.Count; i++)
            {
                if (_pendingFollowUps[i].sourceKey != sourceKey)
                    continue;

                _pendingFollowUps.RemoveAt(i);
                _completedFollowUps.Add(sourceKey);
                return;
            }
        }

        /// <summary>저장 객체와 런타임 장부가 같은 가변 예약을 공유하지 않도록 복사한다.</summary>
        public void CaptureFollowUps(SavedPolicyState target)
        {
            target.pendingDialogueFollowUps = new List<SavedDialogueFollowUp>();
            target.completedDialogueFollowUps = new List<string>(_completedFollowUps);
            foreach (SavedDialogueFollowUp pending in _pendingFollowUps)
            {
                target.pendingDialogueFollowUps.Add(new SavedDialogueFollowUp
                {
                    sourceKey = pending.sourceKey,
                    sequenceKey = pending.sequenceKey,
                    dueDay = pending.dueDay
                });
            }
        }

        /// <summary>누락된 옛 저장은 빈 예약으로 시작한다. 완료된 예약과 중복 항목은 되살리지 않는다.</summary>
        public void RestoreFollowUps(SavedPolicyState state)
        {
            _pendingFollowUps.Clear();
            _completedFollowUps.Clear();
            if (state?.completedDialogueFollowUps != null)
            {
                foreach (string key in state.completedDialogueFollowUps)
                {
                    if (!string.IsNullOrWhiteSpace(key))
                        _completedFollowUps.Add(key);
                }
            }

            if (state?.pendingDialogueFollowUps == null)
                return;

            foreach (SavedDialogueFollowUp saved in state.pendingDialogueFollowUps)
            {
                if (saved != null)
                    TryScheduleFollowUp(saved.sourceKey, saved.sequenceKey, 0, saved.dueDay);
            }
        }

        /// <summary>
        /// 이 전환에 일반 사건을 띄워도 되는가. 같은 날을 여러 번 물어도 처음 판정을 그대로 돌려준다.
        /// 주요 사건이 뜨는 전환이면 일반 사건은 미루고(쉬는 시간은 계속 줄어든 채로 다음 전환을 기다림),
        /// 일반 사건이 나오면 쉬는 시간을 4~6 중 하나로 다시 건다.
        /// </summary>
        /// <param name="roll01">쉬는 시간을 고를 0~1 난수. 테스트가 값을 정할 수 있게 밖에서 받는다.</param>
        public bool ResolveTransition(int day, bool majorEventThisTransition, float roll01) =>
            ResolveTransition(day, false, majorEventThisTransition, false, roll01) == TransitionEventKind.Normal;

        /// <summary>
        /// 이 전환에 띄울 사건 하나를 고른다. 같은 날을 여러 번 물어도 처음 판정을 그대로 돌려준다.
        /// 순서: 긴급(청산 위기) → 주요(운영 방침, 긴급에 밀렸으면 다음 전환에) → 상황 → 일반.
        /// 긴급 사건은 쉬는 시간과 무관하고, 상황 사건은 일반 사건과 쉬는 시간을 함께 쓴다.
        /// </summary>
        public TransitionEventKind ResolveTransition(
            int day, bool urgentSituation, bool majorEventThisTransition, bool situationAvailable, float roll01)
        {
            if (day == _resolvedDay)
                return _resolvedKind;

            _resolvedDay = day;
            NormalCooldown--;
            var major = majorEventThisTransition || PolicyDeferred;

            if (urgentSituation)
            {
                PolicyDeferred = major;
                _resolvedKind = TransitionEventKind.Urgent;
            }
            else if (major)
            {
                PolicyDeferred = false;
                _resolvedKind = TransitionEventKind.Major;
            }
            else if (NormalCooldown <= 0)
            {
                NormalCooldown = RollCooldown(roll01);
                _resolvedKind = situationAvailable ? TransitionEventKind.Situation : TransitionEventKind.Normal;
            }
            else
            {
                _resolvedKind = TransitionEventKind.None;
            }

            return _resolvedKind;
        }

        public bool HasChosen(string key) => !string.IsNullOrEmpty(key) && _chosenKeys.Contains(key);

        public void MarkChosen(string key)
        {
            if (!string.IsNullOrEmpty(key))
                _chosenKeys.Add(key);
        }

        /// <summary>저장에서 되돌린다. 사건 기록이 없던 옛 저장(<paramref name="hasState"/> false)은 처음 상태로 시작한다.</summary>
        public void Restore(IEnumerable<string> chosenKeys, int normalCooldown, bool hasState, bool policyDeferred = false)
        {
            _chosenKeys.Clear();
            _pendingFollowUps.Clear();
            _completedFollowUps.Clear();
            if (chosenKeys != null)
            {
                foreach (var key in chosenKeys)
                    MarkChosen(key);
            }

            NormalCooldown = hasState ? normalCooldown : MinNormalCooldown;
            PolicyDeferred = hasState && policyDeferred;
            _resolvedDay = -1;
            _resolvedKind = TransitionEventKind.None;
        }

        public static int RollCooldown(float roll01) =>
            Mathf.Clamp(
                MinNormalCooldown + Mathf.FloorToInt(Mathf.Clamp01(roll01) * (MaxNormalCooldown - MinNormalCooldown + 1)),
                MinNormalCooldown,
                MaxNormalCooldown);

        public static string PolicyKey(PolicyDataSO policy) => policy != null ? "policy:" + policy.name : string.Empty;

        public static string DialogueChoiceKey(string sequenceName, int lineIndex, int choiceIndex) =>
            string.IsNullOrEmpty(sequenceName) ? string.Empty : $"dialogue:{sequenceName}:{lineIndex}:{choiceIndex}";
    }
}

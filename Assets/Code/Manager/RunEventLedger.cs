using System.Collections.Generic;
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
        private int _resolvedDay = -1;
        private bool _normalGrantedForResolvedDay;

        /// <summary>다음 일반 사건까지 남은 전환 수. 0 이하면 다음 전환에 나올 수 있다.</summary>
        public int NormalCooldown { get; private set; } = MinNormalCooldown;

        public IReadOnlyCollection<string> ChosenKeys => _chosenKeys;

        /// <summary>
        /// 이 전환에 일반 사건을 띄워도 되는가. 같은 날을 여러 번 물어도 처음 판정을 그대로 돌려준다.
        /// 주요 사건이 뜨는 전환이면 일반 사건은 미루고(쉬는 시간은 계속 줄어든 채로 다음 전환을 기다림),
        /// 일반 사건이 나오면 쉬는 시간을 4~6 중 하나로 다시 건다.
        /// </summary>
        /// <param name="roll01">쉬는 시간을 고를 0~1 난수. 테스트가 값을 정할 수 있게 밖에서 받는다.</param>
        public bool ResolveTransition(int day, bool majorEventThisTransition, float roll01)
        {
            if (day == _resolvedDay)
                return _normalGrantedForResolvedDay;

            _resolvedDay = day;
            NormalCooldown--;
            _normalGrantedForResolvedDay = !majorEventThisTransition && NormalCooldown <= 0;
            if (_normalGrantedForResolvedDay)
                NormalCooldown = RollCooldown(roll01);

            return _normalGrantedForResolvedDay;
        }

        public bool HasChosen(string key) => !string.IsNullOrEmpty(key) && _chosenKeys.Contains(key);

        public void MarkChosen(string key)
        {
            if (!string.IsNullOrEmpty(key))
                _chosenKeys.Add(key);
        }

        /// <summary>저장에서 되돌린다. 사건 기록이 없던 옛 저장(<paramref name="hasState"/> false)은 처음 상태로 시작한다.</summary>
        public void Restore(IEnumerable<string> chosenKeys, int normalCooldown, bool hasState)
        {
            _chosenKeys.Clear();
            if (chosenKeys != null)
            {
                foreach (var key in chosenKeys)
                    MarkChosen(key);
            }

            NormalCooldown = hasState ? normalCooldown : MinNormalCooldown;
            _resolvedDay = -1;
            _normalGrantedForResolvedDay = false;
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

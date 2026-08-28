using System.Collections.Generic;
using _01.Code.Manager;
using _01.Code.Persistence;
using UnityEngine;

namespace _01.Code.Progression
{
    /// <summary>
    /// 이번 판에서 각 마을을 얼마나 장악했는지.
    /// 원정 결과가 쓰고 웨이브 편성이 읽는, 두 시스템 사이의 유일한 접점이다.
    ///
    /// 카탈로그 에셋에 담으면 플레이를 끝내도 값이 남아 다음 판이 오염되고,
    /// 정적 클래스로 두면 도메인 리로드를 꺼 둔 에디터에서 값이 살아남는다.
    /// 컴포넌트로 두면 씬과 함께 사라지므로 한 판의 수명과 정확히 맞는다.
    /// </summary>
    public sealed class VillageConquestSystem : MonoBehaviour
    {
        public static VillageConquestSystem Current { get; private set; }

        private readonly Dictionary<AdventurerPartySO, int> _conquestByParty = new();

        /// <summary>마을이 하나도 없을 때 0으로 나누지 않기 위해 마을 수를 따로 센다.</summary>
        private int _villageCount;
        private int _totalConquest;

        /// <summary>장악한 정도의 평균(0~1). 웨이브가 얼마나 줄어들지는 이 값으로 정해진다.</summary>
        public float AverageConquestRatio =>
            _villageCount <= 0 ? 0f : Mathf.Clamp01(_totalConquest / (float)(_villageCount * 100));

        public int VillageCount => _villageCount;

        /// <summary>완전히 장악해 더 이상 습격대를 보내지 않는 마을 수.</summary>
        public int FullyConqueredCount
        {
            get
            {
                var held = 0;
                foreach (var conquest in _conquestByParty.Values)
                {
                    if (conquest >= 100)
                        held++;
                }

                return held;
            }
        }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"Duplicate {nameof(VillageConquestSystem)} detected. Keep exactly one scene instance.", this);
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

        public void ResetConquest()
        {
            _conquestByParty.Clear();
            _villageCount = 0;
            _totalConquest = 0;
        }

        /// <summary>판이 시작될 때 마을 목록을 등록한다. 등록된 마을 수가 곧 평균의 분모다.</summary>
        public void Register(AdventurerPartySO originParty, int startingConquest)
        {
            _villageCount++;
            var clamped = Mathf.Clamp(startingConquest, 0, 100);
            _totalConquest += clamped;

            if (originParty != null)
                _conquestByParty[originParty] = clamped;
        }

        public void SetConquest(AdventurerPartySO originParty, int conquest)
        {
            var clamped = Mathf.Clamp(conquest, 0, 100);
            var previous = originParty != null && _conquestByParty.TryGetValue(originParty, out var stored)
                ? stored
                : 0;

            _totalConquest += clamped - previous;

            if (originParty != null)
                _conquestByParty[originParty] = clamped;
        }

        /// <summary>
        /// 이 파티가 이번에 오지 않을 확률(0~1). 장악도가 곧 억제 확률이라
        /// 완전히 장악한 마을에서는 더 이상 습격대가 오지 않는다.
        /// </summary>
        public float GetSuppression(AdventurerPartySO party)
        {
            if (party == null || !_conquestByParty.TryGetValue(party, out var conquest))
                return 0f;

            return Mathf.Clamp01(conquest / 100f);
        }

        public int GetConquest(AdventurerPartySO party) =>
            party != null && _conquestByParty.TryGetValue(party, out var value) ? value : 0;

        public void CaptureSaveState(List<SavedCount> target)
        {
            if (target == null) return;
            target.Clear();
            foreach (var pair in _conquestByParty)
                if (pair.Key != null) target.Add(new SavedCount
                {
                    assetKey = pair.Key.name,
                    count = Mathf.Clamp(pair.Value, 0, 100)
                });
        }

        public void RestoreSaveState(IReadOnlyList<SavedCount> source)
        {
            if (source == null) return;
            foreach (var saved in source)
            foreach (var party in new List<AdventurerPartySO>(_conquestByParty.Keys))
            {
                if (party != null && party.name == saved.assetKey)
                {
                    SetConquest(party, saved.count);
                    break;
                }
            }
        }
    }
}

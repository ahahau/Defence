using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 스테이지 상인의 구매 상태(2026-11-02 사용자 결정: 이번 스테이지 한정 물건, 매 준비 단계, 범용 팝업).
    /// 물건은 카탈로그(<see cref="StageCatalogSO.MerchantItems"/>)가 정하고, 구매 검사·적용은
    /// <see cref="StageSession.TryBuyMerchantItem"/>가, 팝업은 UI가 맡는다. 스테이지를 바꾸거나 다시 시작하면 비운다.
    /// </summary>
    public sealed class StageMerchant
    {
        /// <summary>범용 팝업 버튼 수와 같다.</summary>
        public const int MaxItems = 3;
        private readonly HashSet<string> _bought = new(StringComparer.Ordinal);

        /// <summary>스테이지 시작·재도전·이어하기마다 바뀐다. 이전 시도의 팝업 콜백을 거르는 데 쓴다.</summary>
        public int Attempt { get; private set; }
        /// <summary>이번 스테이지에 산 배율 물건 ID.</summary>
        public IReadOnlyCollection<string> Bought => _bought;
        /// <summary>마지막으로 코어 보수를 산 준비 단계의 다음 웨이브 번호(1부터). 0이면 사지 않았다. 준비 저장의 정수와 같다.</summary>
        public int RepairWave { get; private set; }
        /// <summary>이번 스테이지에 상인 보수로 실제 회복한 코어 체력 누계. 준비 저장 검사에서 코어 손실을 맞출 때 쓴다.</summary>
        public int CoreRepaired { get; private set; }
        public float UnitAttackMultiplier { get; private set; } = 1f;
        public float BarricadeHealthMultiplier { get; private set; } = 1f;
        /// <summary>카탈로그 물건 목록이 검사를 통과했는가. 스테이지를 고를 때마다 다시 본다.</summary>
        public bool IsAvailable { get; private set; }

        public static IReadOnlyList<StageMerchantItem> ItemsOf(StageCatalogSO catalog) =>
            catalog != null && catalog.MerchantItems != null ? catalog.MerchantItems : Array.Empty<StageMerchantItem>();

        public static bool IsOpen(StageSession session) =>
            session.Phase == StagePhase.Preparing && session.Merchant.IsAvailable;

        /// <summary>물건마다 효과 하나·ID 중복 없음·팝업 버튼 수 이하인가. 빌더가 쓴 값을 인스펙터에서 바꿨을 때를 막는다.</summary>
        public static bool TryValidate(StageCatalogSO catalog, out string reason)
        {
            IReadOnlyList<StageMerchantItem> items = ItemsOf(catalog);
            reason = string.Empty;
            if (items.Count > MaxItems)
            {
                reason = $"상인 물건은 {MaxItems}개까지입니다";
                return false;
            }
            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (StageMerchantItem item in items)
            {
                if (item == null)
                {
                    reason = "상인 물건 칸이 비어 있습니다";
                    return false;
                }
                if (!item.TryValidate(out reason))
                    return false;
                if (!ids.Add(item.Id))
                {
                    reason = $"상인 물건 ID가 중복됩니다: {item.Id}";
                    return false;
                }
            }
            return true;
        }

        /// <summary>배율 물건은 이번 스테이지에, 코어 보수는 이번 준비 단계에 이미 샀는가.</summary>
        public bool IsSoldOut(StageMerchantItem item, int waveIndex) =>
            item.IsStageBuff ? _bought.Contains(item.Id) : RepairWave == waveIndex + 1;

        public static int BarricadeHealth(StageDefinitionSO definition, float multiplier) =>
            Mathf.Max(1, Mathf.RoundToInt(definition.BarricadeHealth * multiplier));

        /// <summary>배치·복원된 부하에 공격력 배율을 얹는다. 강화·유물과 따로 곱한다.</summary>
        public void Apply(StageActor actor)
        {
            if (actor == null || actor.IsEnemy || actor.Unit == null)
                return;
            actor.Unit.Combatant.SetAttackModifier(this, 0f, UnitAttackMultiplier);
        }

        /// <summary>저장된 구매가 현재 카탈로그·진행과 맞는가.</summary>
        public static bool IsValidSaved(StageCatalogSO catalog, int nextWave, List<string> bought, int repairWave, int repaired)
        {
            int maximumRepair = 0;
            foreach (StageMerchantItem item in ItemsOf(catalog))
                if (item != null)
                    maximumRepair = Mathf.Max(maximumRepair, item.CoreRepair);
            if (repairWave < 0 || repairWave > nextWave + 1 || (repairWave == 0) != (repaired == 0)
                || repaired < 0 || repaired > maximumRepair * repairWave)
                return false;
            if (bought == null)
                return true;
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (string id in bought)
            {
                StageMerchantItem item = Find(catalog, id);
                if (item == null || !item.IsStageBuff || !seen.Add(id))
                    return false;
            }
            return true;
        }

        /// <summary>저장된 구매로 바리케이드 체력 배율을 다시 계산한다. 준비 저장 검사가 쓴다.</summary>
        public static float BarricadeMultiplier(StageCatalogSO catalog, IEnumerable<string> bought)
        {
            float multiplier = 1f;
            if (bought != null)
                foreach (string id in bought)
                    multiplier *= Find(catalog, id)?.BarricadeHealthMultiplier ?? 1f;
            return multiplier;
        }

        /// <summary>새 스테이지·재도전·이어하기 전에 구매를 비우고 물건 목록을 다시 검사한다. 잘못된 목록이면 상인을 닫는다.</summary>
        internal void Reset(StageCatalogSO catalog)
        {
            IsAvailable = TryValidate(catalog, out string reason) && ItemsOf(catalog).Count > 0;
            if (reason.Length > 0)
                Debug.LogError("스테이지 상인을 닫습니다: " + reason);
            Attempt++;
            _bought.Clear();
            RepairWave = 0;
            CoreRepaired = 0;
            UnitAttackMultiplier = 1f;
            BarricadeHealthMultiplier = 1f;
        }

        internal void Buy(StageMerchantItem item, int waveIndex, int repaired)
        {
            if (item.IsStageBuff)
            {
                _bought.Add(item.Id);
                UnitAttackMultiplier *= item.UnitAttackMultiplier;
                BarricadeHealthMultiplier *= item.BarricadeHealthMultiplier;
            }
            else
            {
                RepairWave = waveIndex + 1;
                CoreRepaired += repaired;
            }
        }

        /// <summary>준비 저장에서 읽은 구매를 기록한다. 배치를 되살리기 전에 불러야 배율이 붙는다.</summary>
        internal void Set(StageCatalogSO catalog, IEnumerable<string> bought, int repairWave, int repaired)
        {
            _bought.Clear();
            UnitAttackMultiplier = 1f;
            BarricadeHealthMultiplier = 1f;
            if (bought != null)
                foreach (string id in bought)
                {
                    StageMerchantItem item = Find(catalog, id);
                    if (item != null && _bought.Add(id))
                    {
                        UnitAttackMultiplier *= item.UnitAttackMultiplier;
                        BarricadeHealthMultiplier *= item.BarricadeHealthMultiplier;
                    }
                }
            RepairWave = repairWave;
            CoreRepaired = repaired;
        }

        private static StageMerchantItem Find(StageCatalogSO catalog, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (StageMerchantItem item in ItemsOf(catalog))
                if (item != null && item.Id == id)
                    return item;
            return null;
        }
    }
}

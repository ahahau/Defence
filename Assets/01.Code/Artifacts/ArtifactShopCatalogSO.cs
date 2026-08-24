using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Artifacts
{
    /// <summary>
    /// 상인이 취급하는 유물 목록과 진열 규칙.
    /// 어떤 유물을 파는지는 데이터로 두고, 매일 무엇이 진열될지는 여기서 뽑는다.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Artifact/Shop Catalog", fileName = "ArtifactShopCatalog")]
    public sealed class ArtifactShopCatalogSO : ScriptableObject
    {
        [SerializeField, Tooltip("상인이 취급할 유물. 가격이 0인 항목은 진열되지 않는다.")]
        private List<ArtifactDataSO> stock = new();

        [SerializeField, Min(1), Tooltip("한 번에 진열할 칸 수")]
        private int slotCount = 3;

        [SerializeField, Range(0f, 1f), Tooltip("일차가 오를수록 붙는 가격 상승률. 0이면 정가 고정.")]
        private float priceInflationPerDay;

        [Header("방문 주기")]
        [SerializeField, Min(1), Tooltip("떠돌이 상인이 처음 찾아오는 일차")]
        private int firstVisitDay = 3;

        [SerializeField, Min(1), Tooltip("그 뒤로 몇 일마다 다시 찾아오는지")]
        private int visitIntervalDays = 3;

        [Header("구매 누적 인상")]
        [SerializeField, Range(0f, 1f), Tooltip("한 번 살 때마다 모든 가격에 영구히 더해지는 인상률. 0.15면 살 때마다 15%p씩 오른다.")]
        private float priceIncreasePerPurchase = 0.15f;

        [Header("무작위 상품")]
        [SerializeField, Tooltip("무엇이 나올지 모르는 유물 한 점. 재고가 남아 있는 한 계속 살 수 있다.")]
        private bool offerRandomArtifact = true;

        [SerializeField, Min(1), Tooltip("무작위 상품의 기준 가격. 보통 지정 상품보다 싸게 둔다.")]
        private int randomArtifactPrice = 90;

        [SerializeField, Tooltip("무작위 상품 칸에 표시할 이름")]
        private string randomArtifactLabel = "정체불명의 유물";

        public IReadOnlyList<ArtifactDataSO> Stock => stock;
        public int SlotCount => Mathf.Max(1, slotCount);
        public int FirstVisitDay => Mathf.Max(1, firstVisitDay);
        public int VisitIntervalDays => Mathf.Max(1, visitIntervalDays);

        /// <summary>그 일차에 상인이 던전에 와 있는가.</summary>
        public bool IsVisitDay(int day)
        {
            return day >= FirstVisitDay && (day - FirstVisitDay) % VisitIntervalDays == 0;
        }

        /// <summary>다음에 상인이 오는 일차. 오늘 와 있으면 오늘을 그대로 돌려준다.</summary>
        public int GetNextVisitDay(int day)
        {
            if (day < FirstVisitDay)
                return FirstVisitDay;

            var sinceLast = (day - FirstVisitDay) % VisitIntervalDays;
            return sinceLast == 0 ? day : day + (VisitIntervalDays - sinceLast);
        }
        public bool OfferRandomArtifact => offerRandomArtifact;
        public string RandomArtifactLabel => randomArtifactLabel;

        public void ReplaceStock(List<ArtifactDataSO> value)
        {
            stock = value ?? new List<ArtifactDataSO>();
        }

        /// <summary>
        /// 일차 보정과 누적 구매 인상을 반영한 실제 판매가.
        /// <paramref name="purchaseCount"/>는 런타임 상태다. 여기에 저장하면 에디터 에셋이
        /// 플레이할 때마다 비싸지므로 값은 항상 바깥에서 받는다.
        /// </summary>
        public int GetPrice(ArtifactDataSO artifact, int day, int purchaseCount)
        {
            return artifact == null || artifact.Price <= 0
                ? 0
                : ScalePrice(artifact.Price, day, purchaseCount);
        }

        public int GetRandomArtifactPrice(int day, int purchaseCount)
        {
            return ScalePrice(randomArtifactPrice, day, purchaseCount);
        }

        private int ScalePrice(int basePrice, int day, int purchaseCount)
        {
            var dayScale = 1f + Mathf.Max(0, day) * priceInflationPerDay;
            var purchaseScale = 1f + Mathf.Max(0, purchaseCount) * priceIncreasePerPurchase;
            return Mathf.Max(1, Mathf.RoundToInt(basePrice * dayScale * purchaseScale));
        }

        /// <summary>무작위 상품이 실제로 내줄 유물. 아직 없는 것 중에서 하나 고른다.</summary>
        public ArtifactDataSO PickRandomUnowned(ArtifactInventorySO inventory)
        {
            var candidates = CollectAvailable(inventory);
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        /// <summary>무작위 상품으로 내줄 수 있는 유물이 아직 남아 있는가.</summary>
        public bool HasAvailableArtifact(ArtifactInventorySO inventory)
        {
            return CollectAvailable(inventory).Count > 0;
        }

        /// <summary>
        /// 이번 진열 목록을 뽑는다. 이미 가진 유물과 가격이 없는 유물은 빼고 무작위로 고른다.
        /// 살 수 있는 게 칸 수보다 적으면 있는 만큼만 돌려준다.
        /// </summary>
        public List<ArtifactDataSO> RollDisplay(ArtifactInventorySO inventory)
        {
            var candidates = CollectAvailable(inventory);
            var display = new List<ArtifactDataSO>();
            var take = Mathf.Min(SlotCount, candidates.Count);
            for (var i = 0; i < take; i++)
            {
                var index = Random.Range(0, candidates.Count);
                display.Add(candidates[index]);
                candidates.RemoveAt(index);
            }

            return display;
        }

        /// <summary>아직 안 가졌고 가격이 매겨진 유물만 추린다.</summary>
        private List<ArtifactDataSO> CollectAvailable(ArtifactInventorySO inventory)
        {
            var candidates = new List<ArtifactDataSO>();
            foreach (var artifact in stock)
            {
                if (artifact == null || artifact.Price <= 0)
                    continue;

                if (inventory != null && inventory.HasObtained(artifact))
                    continue;

                if (!candidates.Contains(artifact))
                    candidates.Add(artifact);
            }

            return candidates;
        }
    }
}

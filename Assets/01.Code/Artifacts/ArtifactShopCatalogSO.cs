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

        [SerializeField, Min(0), Tooltip("한 진열에 올릴 소모품 최대 개수. 물약은 사도 목록에서 빠지지 않아, 제한이 없으면 영구 유물이 팔려 나갈수록 매대가 물약으로 뒤덮인다.")]
        private int maxConsumableSlots = 1;

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

        /// <summary>
        /// 무작위 상품이 실제로 내줄 유물. 아직 없는 것 중에서 하나 고른다.
        /// 소모품은 제외한다 — 상자 값(<see cref="randomArtifactPrice"/>)은 유물 기준이라
        /// 물약이 나오면 정가보다 비싸게 주고 산 꼴이 되고, 손에 남는 것도 없다.
        /// </summary>
        public ArtifactDataSO PickRandomUnowned(ArtifactInventorySO inventory)
        {
            var candidates = CollectAvailable(inventory, true);
            return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
        }

        /// <summary>무작위 상품으로 내줄 수 있는 유물이 아직 남아 있는가. 소모품은 세지 않는다.</summary>
        public bool HasAvailableArtifact(ArtifactInventorySO inventory)
        {
            return CollectAvailable(inventory, true).Count > 0;
        }

        /// <summary>
        /// 이번 진열 목록을 뽑는다. 이미 가진 유물과 가격이 없는 유물은 빼고 무작위로 고른다.
        /// 살 수 있는 게 칸 수보다 적으면 있는 만큼만 돌려준다.
        /// 지정 진열에는 소모품도 함께 올린다 — 물약을 살 자리가 여기뿐이다.
        /// </summary>
        /// <param name="previousDisplay">
        /// 지난번에 깔았던 목록. 여기 있던 것은 뒤로 미뤄, 상인이 다시 왔을 때 같은 매대를 보지 않게 한다.
        /// 상태를 이 에셋에 담지 않고 밖에서 받는 이유는 <see cref="GetPrice"/>와 같다 —
        /// 스크립터블 오브젝트에 적어 두면 플레이할 때마다 에디터 에셋이 바뀐다.
        /// </param>
        public List<ArtifactDataSO> RollDisplay(ArtifactInventorySO inventory,
            IReadOnlyList<ArtifactDataSO> previousDisplay = null)
        {
            // 새 물건을 먼저 깔고, 모자랄 때만 지난번 것을 다시 꺼낸다.
            // 후보가 칸 수보다 적은 날에도 매대를 비우지 않기 위해서다.
            var fresh = new List<ArtifactDataSO>();
            var repeats = new List<ArtifactDataSO>();
            foreach (var artifact in CollectAvailable(inventory, false))
            {
                if (WasDisplayed(previousDisplay, artifact))
                    repeats.Add(artifact);
                else
                    fresh.Add(artifact);
            }

            var display = new List<ArtifactDataSO>();
            var consumables = 0;
            DrawInto(display, fresh, ref consumables);
            DrawInto(display, repeats, ref consumables);
            return display;
        }

        /// <summary>IReadOnlyList 에는 Contains 가 없어 직접 훑는다. 목록이 서너 칸이라 이걸로 충분하다.</summary>
        private static bool WasDisplayed(IReadOnlyList<ArtifactDataSO> previousDisplay, ArtifactDataSO artifact)
        {
            if (previousDisplay == null)
                return false;

            for (var i = 0; i < previousDisplay.Count; i++)
                if (previousDisplay[i] == artifact)
                    return true;

            return false;
        }

        /// <summary>후보 더미에서 칸이 찰 때까지 뽑아 담는다. 소모품은 정해진 몫까지만 올린다.</summary>
        private void DrawInto(List<ArtifactDataSO> display, List<ArtifactDataSO> pool, ref int consumables)
        {
            while (display.Count < SlotCount && pool.Count > 0)
            {
                var index = Random.Range(0, pool.Count);
                var artifact = pool[index];
                pool.RemoveAt(index);

                if (artifact.IsConsumable)
                {
                    if (consumables >= maxConsumableSlots)
                        continue;

                    consumables++;
                }

                display.Add(artifact);
            }
        }

        /// <summary>
        /// 아직 안 가졌고 가격이 매겨진 유물만 추린다.
        /// 소모품은 소지품에 남지 않아 <c>HasObtained</c>로 걸러지지 않으므로,
        /// 영구 유물만 필요한 자리에서는 <paramref name="permanentOnly"/>로 따로 뺀다.
        /// </summary>
        private List<ArtifactDataSO> CollectAvailable(ArtifactInventorySO inventory, bool permanentOnly)
        {
            var candidates = new List<ArtifactDataSO>();
            foreach (var artifact in stock)
            {
                if (artifact == null || artifact.Price <= 0)
                    continue;

                if (permanentOnly && artifact.IsConsumable)
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

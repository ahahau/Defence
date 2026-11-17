using System.Collections.Generic;
using Code.Artifacts;
using Code.Core;
using Code.Events;
using Code.Manager;
using Code.UI.ViewModels;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 떠돌이 상인. 대기 중에만 열 수 있고, 유물을 금화로 판다.
    /// 진열은 하루마다 새로 뽑히며 이미 가진 유물은 나오지 않는다.
    /// 그리는 화면은 지금 없다(uGUI 전환 대기) — 화면은 <see cref="Changed"/>를 듣고 <see cref="Offers"/>·<see cref="Title"/>·<see cref="Detail"/>을 읽으며,
    /// 버튼은 <see cref="Buy"/>·<see cref="Hide"/>를 부른다.
    /// </summary>
    public class MerchantPanelView : MonoBehaviour
    {
        [SerializeField] private ArtifactShopCatalogSO shopCatalog;
        [SerializeField] private ArtifactInventorySO artifactInventory;

        [Header("Event Channels")]
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GameEventChannelSO artifactEventChannel;

        private readonly List<ArtifactCombo> comboBuffer = new();

        [SerializeField] private string titleFormat = "떠돌이 상인";

        /// <summary>진열 한 칸. 무작위 상품은 살 때가 되어서야 어떤 유물인지 정해진다.</summary>
        private readonly struct ShopOffer
        {
            public ShopOffer(ArtifactDataSO artifact, bool isRandom)
            {
                Artifact = artifact;
                IsRandom = isRandom;
            }

            public ArtifactDataSO Artifact { get; }
            public bool IsRandom { get; }
        }

        private readonly List<ShopOffer> display = new();
        /// <summary>직전 방문에 깔았던 유물. 다음 진열에서 뒤로 미루는 데만 쓴다.</summary>
        private readonly List<ArtifactDataSO> lastDisplay = new();
        private int currentDay;
        private bool hasRolled;
        private bool _pendingWasRandom;
        private bool? lastStandbyState;

        /// <summary>
        /// 이번 판에서 상인에게 산 횟수. 살 때마다 값이 올라 모든 가격이 영구히 비싸진다.
        /// 카탈로그(에셋)가 아니라 여기에 두어야 에디터 에셋이 오염되지 않는다.
        /// </summary>
        private int purchaseCount;

        /// <summary>한 번에 진열하는 최대 칸 수.</summary>
        public const int MaxOffers = 5;

        public bool IsPanelOpen => _isOpen;
        public int PurchaseCount => purchaseCount;

        public void RestoreCheckpoint(IReadOnlyList<string> artifactKeys, int savedPurchaseCount, int day)
        {
            purchaseCount = Mathf.Max(0, savedPurchaseCount);
            currentDay = Mathf.Max(0, day);
            artifactInventory?.Clear(artifactEventChannel);
            if (artifactKeys != null && shopCatalog != null && artifactInventory != null)
            {
                foreach (var key in artifactKeys)
                foreach (var artifact in shopCatalog.Stock)
                {
                    if (artifact == null || artifact.name != key)
                        continue;
                    artifactInventory.Obtain(artifact, artifactEventChannel);
                    break;
                }
            }
            hasRolled = false;
            RefreshVisitState();
        }

        private bool _isOpen;
        private readonly List<MerchantOfferViewModel> _offerModels = new();

        public IReadOnlyList<MerchantOfferViewModel> Offers => _offerModels;
        public string Title { get; private set; } = string.Empty;
        public string Subtitle { get; private set; } = string.Empty;
        public string Detail { get; private set; } = string.Empty;
        public MerchantDetailTone DetailTone { get; private set; }

        /// <summary>열림·진열·안내 문구가 바뀌었다.</summary>
        public event System.Action Changed;

        private void Awake()
        {
            RefreshVisitState();
        }

        private void OnEnable()
        {
            if (dayEventChannel != null)
                dayEventChannel.AddListener<DayChangedEvent>(HandleDayChanged);
            if (costEventChannel != null)
            {
                costEventChannel.AddListener<ArtifactPurchasePaidEvent>(HandlePurchasePaid);
                costEventChannel.AddListener<ArtifactPurchaseRejectedEvent>(HandlePurchaseRejected);
            }
        }

        private void OnDisable()
        {
            if (dayEventChannel != null)
                dayEventChannel.RemoveListener<DayChangedEvent>(HandleDayChanged);
            if (costEventChannel != null)
            {
                costEventChannel.RemoveListener<ArtifactPurchasePaidEvent>(HandlePurchasePaid);
                costEventChannel.RemoveListener<ArtifactPurchaseRejectedEvent>(HandlePurchaseRejected);
            }
        }

        private void Update()
        {
            var isStandby = DayManager.Current != null && DayManager.Current.IsStandby;
            if (lastStandbyState != isStandby)
                RefreshVisitState();
        }

        /// <summary>오늘 상인이 던전에 와 있는가.</summary>
        public bool IsMerchantHere => CoreLoopFeatureUnlocks.IsArtifactUnlocked(currentDay)
                                      && shopCatalog != null
                                      && shopCatalog.IsVisitDay(currentDay);

        private void HandleDayChanged(DayChangedEvent evt)
        {
            currentDay = evt.Day;

            // 찾아온 날마다 물건을 새로 가져온다.
            // "떠나 있다가 돌아올 때만"으로 좁히면 방문일이 연달아 올 때 재고가 그대로 남는다.
            if (IsMerchantHere)
                hasRolled = false;

            if (IsPanelOpen)
                Hide();

            RefreshVisitState();
        }

        /// <summary>
        /// 대기가 끝나면 창을 닫는다.
        /// </summary>
        private void RefreshVisitState()
        {
            var isStandby = DayManager.Current != null && DayManager.Current.IsStandby;
            lastStandbyState = isStandby;

            if (!isStandby)
                Hide();
        }

        public void Toggle()
        {
            if (IsPanelOpen)
                Hide();
            else
                Show();
        }

        public void Show()
        {
            // 웨이브 중에 상점을 여는 건 막는다. 대기 중에만 거래한다.
            if (DayManager.Current == null || !DayManager.Current.IsStandby)
                return;

            if (!CoreLoopFeatureUnlocks.IsArtifactUnlocked(currentDay) || !IsMerchantHere)
                return;

            EnsureRolled();
            _isOpen = true;
            Refresh();
        }

        public void Hide()
        {
            if (!_isOpen)
                return;

            _isOpen = false;
            Changed?.Invoke();
        }

        private void EnsureRolled()
        {
            if (hasRolled || shopCatalog == null)
                return;

            // 지난 매대를 기억해 두고 넘긴다. 같은 물건이 연달아 깔리면 상인이 온 보람이 없다.
            lastDisplay.Clear();
            foreach (var offer in display)
                if (!offer.IsRandom && offer.Artifact != null)
                    lastDisplay.Add(offer.Artifact);

            display.Clear();
            foreach (var artifact in shopCatalog.RollDisplay(artifactInventory, lastDisplay))
                display.Add(new ShopOffer(artifact, false));

            // 무작위 상품은 내줄 유물이 남아 있을 때만 진열한다.
            if (shopCatalog.OfferRandomArtifact && shopCatalog.HasAvailableArtifact(artifactInventory))
                display.Add(new ShopOffer(null, true));

            hasRolled = true;
        }

        /// <summary>진열 칸을 산다. 결제는 비용 채널로 요청하고 결과는 이벤트로 받는다.</summary>
        public void Buy(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= display.Count || costEventChannel == null || shopCatalog == null)
                return;

            var offer = display[slotIndex];

            // 무작위 상품은 결제 직전에 무엇이 나올지 정한다.
            var artifact = offer.IsRandom ? shopCatalog.PickRandomUnowned(artifactInventory) : offer.Artifact;
            if (artifact == null)
            {
                SetDetail("더 내줄 유물이 남아 있지 않습니다.", MerchantDetailTone.Failure);
                return;
            }

            _pendingWasRandom = offer.IsRandom;
            costEventChannel.RaiseEvent(new ArtifactPurchaseRequestedEvent(artifact, GetOfferPrice(offer)));
        }

        private void HandlePurchasePaid(ArtifactPurchasePaidEvent evt)
        {
            if (evt.Artifact == null)
                return;

            // 소모품은 소지품에 남기지 않는다. 산 자리에서 쓰고 끝난다.
            if (evt.Artifact.IsConsumable)
                Code.Artifacts.ConsumableUse.Consume(evt.Artifact);
            else
                artifactInventory?.Obtain(evt.Artifact, artifactEventChannel);

            // 살 때마다 이후 모든 가격이 영구히 오른다.
            purchaseCount++;

            if (_pendingWasRandom)
            {
                // 무작위 칸은 재고가 남아 있으면 계속 팔되, 방금 나온 유물은 이제 지정 진열에서 빠진다.
                display.RemoveAll(o => !o.IsRandom && o.Artifact == evt.Artifact);
                if (shopCatalog != null && !shopCatalog.HasAvailableArtifact(artifactInventory))
                    display.RemoveAll(o => o.IsRandom);

                SetDetail($"상자에서 {ResolveName(evt.Artifact)}!\n남은 금화 {evt.RemainingGold}G",
                    MerchantDetailTone.Success);
            }
            else
            {
                display.RemoveAll(o => !o.IsRandom && o.Artifact == evt.Artifact);
                SetDetail($"{ResolveName(evt.Artifact)} 구입 완료\n남은 금화 {evt.RemainingGold}G",
                    MerchantDetailTone.Success);
            }

            _pendingWasRandom = false;
            Refresh(false);
        }

        private int GetOfferPrice(ShopOffer offer)
        {
            if (shopCatalog == null)
                return 0;

            return offer.IsRandom
                ? shopCatalog.GetRandomArtifactPrice(currentDay, purchaseCount)
                : shopCatalog.GetPrice(offer.Artifact, currentDay, purchaseCount);
        }

        private void HandlePurchaseRejected(ArtifactPurchaseRejectedEvent evt)
        {
            SetDetail($"금화가 부족합니다.\n필요 {evt.GoldAmount}G · 보유 {evt.CurrentGold}G",
                MerchantDetailTone.Failure);
        }

        private void Refresh(bool resetDetail = true)
        {
            // 언제 다시 오는지가 살지 말지를 정하는 정보라 제목 옆에 같이 적는다.
            var nextVisit = shopCatalog != null ? shopCatalog.GetNextVisitDay(currentDay + 1) : 0;

            _offerModels.Clear();
            for (var i = 0; i < display.Count && i < MaxOffers; i++)
            {
                var offer = display[i];
                var price = GetOfferPrice(offer);
                var combo = offer.IsRandom ? (string.Empty, false) : BuildComboHint(offer.Artifact);
                _offerModels.Add(new MerchantOfferViewModel
                {
                    Name = offer.IsRandom ? shopCatalog.RandomArtifactLabel : ResolveName(offer.Artifact),
                    Price = price,
                    Affordable = CostManager.Current != null && CostManager.Current.CurrentGold >= price,
                    Body = offer.IsRandom ? "무엇이 나올지는 열어봐야 안다." : offer.Artifact.Description,
                    Icon = offer.IsRandom ? null : offer.Artifact.Icon,
                    IsRandom = offer.IsRandom,
                    Combo = combo.Item1,
                    ComboComplete = combo.Item2
                });
            }

            Title = titleFormat;
            Subtitle = $"{currentDay}일차  ·  다음 방문 {nextVisit}일차";

            if (!resetDetail)
            {
                Changed?.Invoke();
                return;
            }

            if (display.Count == 0)
            {
                SetDetail("오늘은 팔 물건이 없습니다.", MerchantDetailTone.Normal);
                return;
            }

            // 살수록 비싸진다는 규칙은 눌러보기 전에 알려줘야 한다.
            SetDetail(purchaseCount > 0
                ? $"거래할수록 값을 올려 부릅니다. (누적 {purchaseCount}회)"
                : "유물을 고르면 즉시 구매합니다.\n거래할 때마다 이후 가격이 오릅니다.",
                MerchantDetailTone.Normal);
        }

        /// <summary>
        /// 이 유물이 어떤 유물과 맞물리는지. 이미 가진 짝이면 그렇다고 알려 준다.
        ///
        /// 유물은 좋은 유닛 두 명 값이다. 스탯만 보면 언제나 유닛이 낫고, 살 이유는 조합에 있다.
        /// 그 조합이 사는 자리에서 보이지 않으면 없는 것과 같다.
        /// </summary>
        /// <returns>조합 안내 글과, 짝을 이미 가져 완성되는 조합이 있는지.</returns>
        private (string text, bool complete) BuildComboHint(ArtifactDataSO artifact)
        {
            var catalog = artifactInventory != null ? artifactInventory.Combos : null;
            if (catalog == null || artifact == null)
                return (string.Empty, false);

            catalog.CollectCombosWith(artifact, comboBuffer);
            if (comboBuffer.Count == 0)
                return (string.Empty, false);

            var lines = new System.Text.StringBuilder();
            var complete = false;
            foreach (var combo in comboBuffer)
            {
                var partner = combo.GetPartnerOf(artifact);
                if (partner == null)
                    continue;

                var owned = artifactInventory.HasObtained(partner);
                complete |= owned;
                if (lines.Length > 0)
                    lines.Append('\n');
                lines.Append(owned
                    ? $"조합 완성 · {combo.DisplayName}"
                    : $"조합 · {combo.DisplayName} ({partner.DisplayName} 필요)");
            }

            return (lines.ToString(), complete);
        }

        private void SetDetail(string value, MerchantDetailTone tone)
        {
            Detail = value;
            DetailTone = tone;
            Changed?.Invoke();
        }

        private static string ResolveName(ArtifactDataSO artifact)
        {
            if (artifact == null)
                return "유물";

            return string.IsNullOrWhiteSpace(artifact.DisplayName) ? artifact.name : artifact.DisplayName;
        }
    }
}

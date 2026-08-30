using System.Collections.Generic;
using _01.Code.Artifacts;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    /// <summary>
    /// 떠돌이 상인. 대기 중에만 열 수 있고, 유물을 금화로 판다.
    /// 진열은 하루마다 새로 뽑히며 이미 가진 유물은 나오지 않는다.
    /// </summary>
    public sealed class MerchantPanelView : MonoBehaviour
    {
        [SerializeField] private ArtifactShopCatalogSO shopCatalog;
        [SerializeField] private ArtifactInventorySO artifactInventory;

        [Header("Event Channels")]
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GameEventChannelSO artifactEventChannel;

        [Header("UI")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button[] slotButtons = System.Array.Empty<Button>();
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text detailText;

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
        private readonly List<UnityEngine.Events.UnityAction> slotActions = new();
        private int currentDay;
        private bool hasRolled;
        private bool isWired;
        private bool _pendingWasRandom;
        private bool? lastStandbyState;

        /// <summary>
        /// 이번 판에서 상인에게 산 횟수. 살 때마다 값이 올라 모든 가격이 영구히 비싸진다.
        /// 카탈로그(에셋)가 아니라 여기에 두어야 에디터 에셋이 오염되지 않는다.
        /// </summary>
        private int purchaseCount;

        public bool IsPanelOpen => panelRoot != null && panelRoot.activeInHierarchy;
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

        private void Awake()
        {
            SetActiveSafe(panelRoot, false);
            RefreshVisitState();
        }

        private void OnEnable()
        {
            Wire();
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
            Unwire();
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

        private void Wire()
        {
            if (isWired)
                return;

            isWired = true;
            AddClick(openButton, Toggle);
            AddClick(closeButton, Hide);

            slotActions.Clear();
            for (var i = 0; i < slotButtons.Length; i++)
            {
                var index = i;
                UnityEngine.Events.UnityAction action = () => Buy(index);
                slotActions.Add(action);
                AddClick(slotButtons[i], action);
            }
        }

        private void Unwire()
        {
            if (!isWired)
                return;

            isWired = false;
            RemoveClick(openButton, Toggle);
            RemoveClick(closeButton, Hide);
            for (var i = 0; i < slotButtons.Length && i < slotActions.Count; i++)
                RemoveClick(slotButtons[i], slotActions[i]);
            slotActions.Clear();
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

        /// <summary>상인이 없는 날에는 여는 버튼 자체를 감춘다.</summary>
        private void RefreshVisitState()
        {
            var isStandby = DayManager.Current != null && DayManager.Current.IsStandby;
            lastStandbyState = isStandby;
            if (openButton != null)
            {
                openButton.gameObject.SetActive(IsMerchantHere);
                openButton.interactable = isStandby;
            }

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
            SetActiveSafe(panelRoot, true);
            if (panelRoot != null)
                panelRoot.transform.SetAsLastSibling();
            Refresh();
        }

        public void Hide() => SetActiveSafe(panelRoot, false);

        private void EnsureRolled()
        {
            if (hasRolled || shopCatalog == null)
                return;

            display.Clear();
            foreach (var artifact in shopCatalog.RollDisplay(artifactInventory))
                display.Add(new ShopOffer(artifact, false));

            // 무작위 상품은 내줄 유물이 남아 있을 때만 진열한다.
            if (shopCatalog.OfferRandomArtifact && shopCatalog.HasAvailableArtifact(artifactInventory))
                display.Add(new ShopOffer(null, true));

            hasRolled = true;
        }

        private void Buy(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= display.Count || costEventChannel == null || shopCatalog == null)
                return;

            var offer = display[slotIndex];

            // 무작위 상품은 결제 직전에 무엇이 나올지 정한다.
            var artifact = offer.IsRandom ? shopCatalog.PickRandomUnowned(artifactInventory) : offer.Artifact;
            if (artifact == null)
            {
                SetDetail("더 내줄 유물이 남아 있지 않습니다.");
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
                _01.Code.Artifacts.ConsumableUse.Consume(evt.Artifact);
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

                SetDetail($"상자에서 {ResolveName(evt.Artifact)}!\n남은 금화 {evt.RemainingGold}G");
            }
            else
            {
                display.RemoveAll(o => !o.IsRandom && o.Artifact == evt.Artifact);
                SetDetail($"{ResolveName(evt.Artifact)} 구입 완료\n남은 금화 {evt.RemainingGold}G");
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
            SetDetail($"금화가 부족합니다.\n필요 {evt.GoldAmount}G · 보유 {evt.CurrentGold}G");
        }

        private void Refresh(bool resetDetail = true)
        {
            if (titleText != null)
            {
                // 언제 다시 오는지가 살지 말지를 정하는 정보라 제목에 같이 적는다.
                var nextVisit = shopCatalog != null ? shopCatalog.GetNextVisitDay(currentDay + 1) : 0;
                titleText.text = $"{titleFormat}  ·  {currentDay}일차  ·  다음 방문 {nextVisit}일차";
            }

            for (var i = 0; i < slotButtons.Length; i++)
            {
                var button = slotButtons[i];
                if (button == null)
                    continue;

                if (i >= display.Count)
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                var offer = display[i];
                var price = GetOfferPrice(offer);
                button.gameObject.SetActive(true);
                button.interactable = CostManager.Current != null && CostManager.Current.CurrentGold >= price;

                var label = offer.IsRandom
                    ? $"{shopCatalog.RandomArtifactLabel}\n{price}G\n무엇이 나올지는 열어봐야 안다."
                    : $"{ResolveName(offer.Artifact)}\n{price}G\n{offer.Artifact.Description}";
                InstallCardPresenter.SetButtonText(button, label);
            }

            if (!resetDetail)
                return;

            if (display.Count == 0)
            {
                SetDetail("오늘은 팔 물건이 없습니다.");
                return;
            }

            // 살수록 비싸진다는 규칙은 눌러보기 전에 알려줘야 한다.
            SetDetail(purchaseCount > 0
                ? $"거래할수록 값을 올려 부릅니다. (누적 {purchaseCount}회)"
                : "유물을 고르면 즉시 구매합니다.\n거래할 때마다 이후 가격이 오릅니다.");
        }

        private void SetDetail(string value)
        {
            if (detailText != null)
                detailText.text = value;
        }

        private static string ResolveName(ArtifactDataSO artifact)
        {
            if (artifact == null)
                return "유물";

            return string.IsNullOrWhiteSpace(artifact.DisplayName) ? artifact.name : artifact.DisplayName;
        }

        private static void SetActiveSafe(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        private static void AddClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        private static void RemoveClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.RemoveListener(action);
        }
    }
}

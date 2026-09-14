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

        /// <summary>상인을 여는 버튼. 튜토리얼이 여기를 비춘다.</summary>
        public RectTransform OpenButtonRect =>
            openButton != null && openButton.gameObject.activeInHierarchy
                ? openButton.transform as RectTransform
                : null;

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

                SetDetail($"<color=#8FE3A0>상자에서 <b>{ResolveName(evt.Artifact)}</b>!</color>"
                          + $"\n<size=90%><color=#B3A492>남은 금화 {evt.RemainingGold}G</color></size>");
            }
            else
            {
                display.RemoveAll(o => !o.IsRandom && o.Artifact == evt.Artifact);
                SetDetail($"<color=#8FE3A0><b>{ResolveName(evt.Artifact)}</b> 구입 완료</color>"
                          + $"\n<size=90%><color=#B3A492>남은 금화 {evt.RemainingGold}G</color></size>");
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
            SetDetail($"<color=#E08A6E><b>금화가 부족합니다.</b></color>"
                      + $"\n<size=90%><color=#B3A492>필요 {evt.GoldAmount}G · 보유 {evt.CurrentGold}G</color></size>");
        }

        private void Refresh(bool resetDetail = true)
        {
            if (titleText != null)
            {
                // 언제 다시 오는지가 살지 말지를 정하는 정보라 제목에 같이 적는다.
                var nextVisit = shopCatalog != null ? shopCatalog.GetNextVisitDay(currentDay + 1) : 0;
                // 상인 이름과 날짜가 같은 무게로 붙어 있으면 한 줄이 통째로 흘러간다.
                // 이름만 세우고 날짜는 한 단계 죽여, 무엇을 보는 창인지 먼저 읽히게 한다.
                titleText.text = $"<b>{titleFormat}</b>"
                                 + $"<color=#8A7F71>  ·  </color>"
                                 + $"<size=88%><color=#B3A492>{currentDay}일차  ·  다음 방문 {nextVisit}일차</color></size>";
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
                var affordable = CostManager.Current != null && CostManager.Current.CurrentGold >= price;
                button.gameObject.SetActive(true);
                button.interactable = affordable;

                InstallCardPresenter.SetWrappedButtonText(button, BuildOfferLabel(offer, price, affordable));

                // 무엇을 사는지 그림으로 먼저 알아보게 한다. 정체불명 유물은 보여 줄 그림이 없으니
                // 비워 두면 ApplyCardSprite 가 이미지를 꺼 준다.
                var sprite = offer.IsRandom ? null : offer.Artifact.Icon;
                InstallCardPresenter.ApplyCardSprite(button, sprite);
                var icon = InstallCardPresenter.ResolveCardIconImage(button);
                if (icon != null)
                {
                    var placeholder = icon.transform.Find("Placeholder")?.GetComponent<TMP_Text>();
                    if (placeholder == null && sprite == null)
                    {
                        var prefab = Resources.Load<TextMeshProUGUI>("UI/OfferPlaceholder");
                        if (prefab != null)
                        {
                            placeholder = Instantiate(prefab, icon.transform, false);
                            placeholder.gameObject.name = "Placeholder";
                        }
                        else
                            Debug.LogError("Missing UI prefab: UI/OfferPlaceholder", this);
                    }
                    if (placeholder != null)
                    {
                        placeholder.gameObject.SetActive(sprite == null);
                        placeholder.text = offer.IsRandom ? "?" : "유물";
                    }
                }
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

        /// <summary>
        /// 이 유물이 어떤 유물과 맞물리는지. 이미 가진 짝이면 그렇다고 알려 준다.
        ///
        /// 유물은 좋은 유닛 두 명 값이다. 스탯만 보면 언제나 유닛이 낫고, 살 이유는 조합에 있다.
        /// 그 조합이 사는 자리에서 보이지 않으면 없는 것과 같다.
        /// </summary>
        /// <summary>
        /// 카드 한 장의 글. 이름·값·설명이 서로 다른 무게로 읽히게 한다.
        ///
        /// 셋을 같은 크기 같은 색으로 늘어놓으면 어느 것이 이름이고 어느 것이 값인지 한눈에
        /// 갈리지 않아, 카드 넉 장이 글자 덩어리 넷으로 보인다. 이름은 키우고, 값에는 금화 색을
        /// 주고, 설명은 한 단계 죽인다.
        ///
        /// 값이 모자랄 때는 그 자리에서 이유를 말한다. 버튼이 회색으로 죽어 있기만 하면
        /// 안 팔린 것인지 못 사는 것인지 구분되지 않는다.
        /// </summary>
        private string BuildOfferLabel(ShopOffer offer, int price, bool affordable)
        {
            var name = offer.IsRandom ? shopCatalog.RandomArtifactLabel : ResolveName(offer.Artifact);
            var body = offer.IsRandom
                ? "무엇이 나올지는 열어봐야 안다."
                : $"{offer.Artifact.Description}{BuildComboHint(offer.Artifact)}";

            var priceLine = affordable
                ? $"<color=#F0C860><b>{price}</b> G</color>"
                : $"<color=#C4705E><b>{price}</b> G · 금화 부족</color>";

            return $"<size=112%><b>{name}</b></size>\n{priceLine}\n<size=90%><color=#B3A492>{body}</color></size>";
        }

        private string BuildComboHint(ArtifactDataSO artifact)
        {
            var catalog = artifactInventory != null ? artifactInventory.Combos : null;
            if (catalog == null || artifact == null)
                return string.Empty;

            catalog.CollectCombosWith(artifact, comboBuffer);
            if (comboBuffer.Count == 0)
                return string.Empty;

            var lines = new System.Text.StringBuilder();
            foreach (var combo in comboBuffer)
            {
                var partner = combo.GetPartnerOf(artifact);
                if (partner == null)
                    continue;

                var owned = artifactInventory.HasObtained(partner);
                lines.Append(owned
                    ? $"\n<color=#7ADB8A>조합 완성 · {combo.DisplayName}</color>"
                    : $"\n<color=#9A8B78>조합 · {combo.DisplayName} ({partner.DisplayName} 필요)</color>");
            }

            return lines.ToString();
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

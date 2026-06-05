using System;
using System.Collections.Generic;
using _01.Code.Artifacts;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public enum UnlockRewardKind
    {
        None,
        Unit,
        Building,
        Trap
    }

    public class WaveRewardPanelView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Graphic goldAmountText;
        [SerializeField] private Button goldRewardButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject warningPanel;
        [SerializeField] private Button warningCancelButton;
        [SerializeField] private Button warningCloseButton;
        [Header("Artifact Reward")]
        [SerializeField] private ArtifactInventorySO artifactInventory;
        [SerializeField] private GameEventChannelSO artifactEventChannel;
        [SerializeField] private ArtifactDataSO[] artifactPool;
        [SerializeField] private Button artifactRewardButton;
        [SerializeField] private Graphic artifactRewardText;
        [SerializeField] private ArtifactRewardChoicePanelView artifactChoicePanel;
        [SerializeField] private RectTransform rewardChoiceRoot;
        [SerializeField, Min(1)] private int artifactChoiceCount = 3;
        [Header("Unit Unlock Reward")]
        [SerializeField] private UnitDataSO[] unitRewardPool;
        [SerializeField] private BuildingDataSO[] buildingRewardPool;
        [SerializeField] private Button unitRewardButton;
        [SerializeField] private Graphic unitRewardText;
        [SerializeField, Min(1)] private int unitChoiceCount = 3;

        private readonly List<ArtifactDataSO> pendingArtifactChoices = new();
        private readonly List<UnitDataSO> pendingUnitChoices = new();
        private readonly List<BuildingDataSO> pendingBuildingChoices = new();
        private readonly List<UnitDataSO> unlockedUnitRewards = new();
        private readonly List<BuildingDataSO> unlockedBuildingRewards = new();
        private int offeredUnitUnlockRewards;
        private int offeredBuildingUnlockRewards;
        private int offeredTrapUnlockRewards;
        private UnlockRewardKind pendingUnlockRewardKind;
        private GameEventChannelSO _costEventChannel;
        private Graphic _unitRewardTitleText;
        private int _pendingGoldAmount;
        private bool _hasPendingGoldReward;
        private bool _hasPendingArtifactReward;
        private bool _hasPendingUnitReward;
        private bool _hasShownReward;

        public event Action Closed;
        public bool IsShowingReward => _hasShownReward && gameObject.activeSelf;

        private void Awake()
        {
            if (artifactChoicePanel == null)
                artifactChoicePanel = GetComponentInChildren<ArtifactRewardChoicePanelView>(true);

            ConfigureRewardChoiceLayout();
        }

        private void OnEnable()
        {
            goldRewardButton?.onClick.AddListener(HandleGoldRewardClicked);
            artifactRewardButton?.onClick.AddListener(HandleArtifactRewardClicked);
            unitRewardButton?.onClick.AddListener(HandleUnitRewardClicked);
            closeButton?.onClick.AddListener(HandleCloseClicked);
            warningCancelButton?.onClick.AddListener(HideWarning);
            warningCloseButton?.onClick.AddListener(ForceClose);
        }

        private void OnDisable()
        {
            goldRewardButton?.onClick.RemoveListener(HandleGoldRewardClicked);
            artifactRewardButton?.onClick.RemoveListener(HandleArtifactRewardClicked);
            unitRewardButton?.onClick.RemoveListener(HandleUnitRewardClicked);
            closeButton?.onClick.RemoveListener(HandleCloseClicked);
            warningCancelButton?.onClick.RemoveListener(HideWarning);
            warningCloseButton?.onClick.RemoveListener(ForceClose);
        }

        public void Initialize(GameEventChannelSO costEventChannel)
        {
            _costEventChannel = costEventChannel;
        }

        public void ShowGoldReward(int goldAmount, bool includeArtifactReward = true)
        {
            gameObject.SetActive(true);
            ConfigureModalLayout();
            ConfigureRewardChoiceLayout();
            PrepareArtifactChoices(includeArtifactReward);
            PrepareUnlockChoices();

            _pendingGoldAmount = Mathf.Max(0, goldAmount);
            _hasPendingGoldReward = _pendingGoldAmount > 0;
            _hasPendingArtifactReward = pendingArtifactChoices.Count > 0 && artifactRewardButton != null && artifactChoicePanel != null;
            _hasPendingUnitReward = HasPendingUnlockReward() && unitRewardButton != null && artifactChoicePanel != null;

            if (!_hasPendingGoldReward && !_hasPendingArtifactReward && !_hasPendingUnitReward)
            {
                _hasShownReward = false;
                Hide();
                return;
            }

            _hasShownReward = true;

            if (iconImage != null)
                iconImage.gameObject.SetActive(_hasPendingGoldReward);
            SetLabelText(goldAmountText, _hasPendingGoldReward ? _pendingGoldAmount.ToString() : "골드 없음");
            if (goldRewardButton != null)
            {
                goldRewardButton.gameObject.SetActive(_hasPendingGoldReward);
                goldRewardButton.interactable = _hasPendingGoldReward;
            }

            SetArtifactRewardButtonState(_hasPendingArtifactReward, _hasPendingArtifactReward ? "아티팩트 선택" : "선택 완료", _hasPendingArtifactReward);
            SetUnitRewardButtonState(_hasPendingUnitReward, _hasPendingUnitReward ? ResolveUnlockRewardLabel() : "선택 완료", _hasPendingUnitReward);
            HideWarning();
            HideArtifactChoices();
        }

        private void ConfigureModalLayout()
        {
            if (transform is RectTransform rootRect)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                rootRect.localScale = Vector3.one;
            }

            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = short.MaxValue;
            }

            var blocker = GetComponent<Image>();
            if (blocker != null)
            {
                blocker.color = Color.black;
                blocker.raycastTarget = true;
            }

            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            var shouldNotifyClosed = _hasShownReward && gameObject.activeSelf;
            HideWarning();
            HideArtifactChoices();
            gameObject.SetActive(false);

            if (!shouldNotifyClosed)
                return;

            _hasShownReward = false;
            Closed?.Invoke();
        }

        private void HandleGoldRewardClicked()
        {
            if (!_hasPendingGoldReward || _pendingGoldAmount <= 0)
                return;

            _costEventChannel?.RaiseEvent(new GoldEarnedEvent(_pendingGoldAmount));
            _hasPendingGoldReward = false;
            _pendingGoldAmount = 0;

            if (goldRewardButton != null)
                goldRewardButton.interactable = false;
            SetLabelText(goldAmountText, "수령 완료");

            HideWarning();
        }

        private void HandleArtifactRewardClicked()
        {
            if (!_hasPendingArtifactReward || pendingArtifactChoices.Count == 0)
                return;

            if (artifactChoicePanel == null)
            {
                Debug.LogError($"{nameof(WaveRewardPanelView)} requires an assigned artifact choice panel.", this);
                return;
            }

            artifactChoicePanel.Show(pendingArtifactChoices, ObtainArtifact);
        }

        private void HandleUnitRewardClicked()
        {
            if (!_hasPendingUnitReward || !HasPendingUnlockReward())
                return;

            if (artifactChoicePanel == null)
            {
                Debug.LogError($"{nameof(WaveRewardPanelView)} requires an assigned reward choice panel.", this);
                return;
            }

            artifactChoicePanel.ShowUnlocks(pendingUnitChoices, pendingBuildingChoices, UnlockUnit, UnlockBuilding);
        }

        private void ObtainArtifact(ArtifactDataSO artifact)
        {
            if (artifact == null || artifactInventory == null)
                return;

            artifactInventory.Obtain(artifact, artifactEventChannel);
            _hasPendingArtifactReward = false;
            pendingArtifactChoices.Clear();
            SetArtifactRewardButtonState(false, "선택 완료");
            HideArtifactChoices();
            HideWarning();
        }

        private void UnlockUnit(UnitDataSO unit)
        {
            if (unit == null)
                return;

            if (!unlockedUnitRewards.Contains(unit))
                unlockedUnitRewards.Add(unit);

            _costEventChannel?.RaiseEvent(new UnitUnlockRequestedEvent(unit));
            _hasPendingUnitReward = false;
            pendingUnitChoices.Clear();
            pendingBuildingChoices.Clear();
            pendingUnlockRewardKind = UnlockRewardKind.None;
            SetUnitRewardButtonState(false, "선택 완료");
            HideArtifactChoices();
            HideWarning();
        }

        private void UnlockBuilding(BuildingDataSO building)
        {
            if (building == null)
                return;

            if (!unlockedBuildingRewards.Contains(building))
                unlockedBuildingRewards.Add(building);

            _costEventChannel?.RaiseEvent(new BuildingUnlockRequestedEvent(building));
            _hasPendingUnitReward = false;
            pendingUnitChoices.Clear();
            pendingBuildingChoices.Clear();
            pendingUnlockRewardKind = UnlockRewardKind.None;
            SetUnitRewardButtonState(false, "선택 완료");
            HideArtifactChoices();
            HideWarning();
        }

        private void HandleCloseClicked()
        {
            if (_hasPendingGoldReward || _hasPendingArtifactReward || _hasPendingUnitReward)
            {
                ShowWarning();
                return;
            }

            Hide();
        }

        private void ShowWarning()
        {
            warningPanel?.SetActive(true);
        }

        private void HideWarning()
        {
            warningPanel?.SetActive(false);
        }

        private void ForceClose()
        {
            _hasPendingGoldReward = false;
            _hasPendingArtifactReward = false;
            _hasPendingUnitReward = false;
            _pendingGoldAmount = 0;
            pendingArtifactChoices.Clear();
            pendingUnitChoices.Clear();
            pendingBuildingChoices.Clear();
            pendingUnlockRewardKind = UnlockRewardKind.None;
            HideArtifactChoices();
            Hide();
        }

        private void PrepareArtifactChoices(bool includeArtifactReward)
        {
            pendingArtifactChoices.Clear();

            if (!includeArtifactReward)
                return;

            if (artifactPool == null || artifactPool.Length == 0 || artifactInventory == null)
                return;

            var candidates = new List<ArtifactDataSO>();
            foreach (var artifact in artifactPool)
            {
                if (artifact != null && !artifactInventory.HasObtained(artifact))
                    candidates.Add(artifact);
            }

            for (var i = 0; i < artifactChoiceCount && candidates.Count > 0; i++)
            {
                var index = UnityEngine.Random.Range(0, candidates.Count);
                pendingArtifactChoices.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
        }

        private void PrepareUnlockChoices()
        {
            pendingUnitChoices.Clear();
            pendingBuildingChoices.Clear();
            pendingUnlockRewardKind = UnlockRewardKind.None;

            var unitCandidates = new List<UnitDataSO>();
            if (unitRewardPool != null)
            {
                foreach (var unit in unitRewardPool)
                {
                    if (unit != null && unit.Locked && !unlockedUnitRewards.Contains(unit))
                        unitCandidates.Add(unit);
                }
            }

            var buildingCandidates = new List<BuildingDataSO>();
            var trapCandidates = new List<BuildingDataSO>();
            if (buildingRewardPool != null)
            {
                foreach (var building in buildingRewardPool)
                {
                    if (building == null || !building.Locked || unlockedBuildingRewards.Contains(building))
                        continue;

                    if (building.Category == InstallCategory.Trap)
                        trapCandidates.Add(building);
                    else if (building.Category == InstallCategory.Building)
                        buildingCandidates.Add(building);
                }
            }

            pendingUnlockRewardKind = ChooseUnlockRewardKind(unitCandidates.Count, buildingCandidates.Count, trapCandidates.Count);
            switch (pendingUnlockRewardKind)
            {
                case UnlockRewardKind.Unit:
                    PickUnitChoices(unitCandidates);
                    offeredUnitUnlockRewards++;
                    break;
                case UnlockRewardKind.Building:
                    PickBuildingChoices(buildingCandidates);
                    offeredBuildingUnlockRewards++;
                    break;
                case UnlockRewardKind.Trap:
                    PickBuildingChoices(trapCandidates);
                    offeredTrapUnlockRewards++;
                    break;
            }
        }

        private bool HasPendingUnlockReward()
        {
            return pendingUnitChoices.Count > 0 || pendingBuildingChoices.Count > 0;
        }

        private UnlockRewardKind ChooseUnlockRewardKind(int unitCandidateCount, int buildingCandidateCount, int trapCandidateCount)
        {
            var unitWeight = ResolveUnlockWeight(unitCandidateCount, offeredUnitUnlockRewards, 0.65f);
            var buildingWeight = ResolveUnlockWeight(buildingCandidateCount, offeredBuildingUnlockRewards, 0.45f);
            var trapWeight = ResolveUnlockWeight(trapCandidateCount, offeredTrapUnlockRewards, 0.45f);
            var totalWeight = unitWeight + buildingWeight + trapWeight;

            if (totalWeight <= 0f)
                return UnlockRewardKind.None;

            var roll = UnityEngine.Random.value * totalWeight;
            if (roll < unitWeight)
                return UnlockRewardKind.Unit;

            roll -= unitWeight;
            if (roll < buildingWeight)
                return UnlockRewardKind.Building;

            return UnlockRewardKind.Trap;
        }

        private static float ResolveUnlockWeight(int candidateCount, int offeredCount, float penalty)
        {
            if (candidateCount <= 0)
                return 0f;

            return candidateCount / (1f + offeredCount * penalty);
        }

        private void PickUnitChoices(List<UnitDataSO> candidates)
        {
            for (var i = 0; i < unitChoiceCount && candidates.Count > 0; i++)
            {
                var index = UnityEngine.Random.Range(0, candidates.Count);
                pendingUnitChoices.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
        }

        private void PickBuildingChoices(List<BuildingDataSO> candidates)
        {
            for (var i = 0; i < unitChoiceCount && candidates.Count > 0; i++)
            {
                var index = UnityEngine.Random.Range(0, candidates.Count);
                pendingBuildingChoices.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
        }

        private string ResolveUnlockRewardLabel()
        {
            return pendingUnlockRewardKind switch
            {
                UnlockRewardKind.Unit => "유닛 선택",
                UnlockRewardKind.Building => "건물 선택",
                UnlockRewardKind.Trap => "트랩 선택",
                _ => "해금 선택"
            };
        }

        private void ConfigureRewardChoiceLayout()
        {
            if (goldRewardButton == null)
                return;

            if (rewardChoiceRoot == null)
                rewardChoiceRoot = goldRewardButton.transform.parent as RectTransform;

            if (rewardChoiceRoot != null)
            {
                rewardChoiceRoot.localScale = Vector3.one;
            }

            var layout = rewardChoiceRoot != null
                ? rewardChoiceRoot.GetComponent<HorizontalLayoutGroup>()
                : goldRewardButton.GetComponentInParent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }
        }

        private void SetArtifactRewardButtonState(bool interactable, string label, bool visible = true)
        {
            if (artifactRewardButton == null)
                return;

            artifactRewardButton.gameObject.SetActive(visible);
            artifactRewardButton.interactable = interactable;

            if (artifactRewardText == null)
                artifactRewardText = ResolveLabelGraphic(artifactRewardButton);
            SetLabelText(artifactRewardText, label);
        }

        private void SetUnitRewardButtonState(bool interactable, string label, bool visible = true)
        {
            if (unitRewardButton == null)
                return;

            unitRewardButton.gameObject.SetActive(visible);
            unitRewardButton.interactable = interactable;

            if (_unitRewardTitleText == null)
                _unitRewardTitleText = ResolveChildLabelGraphic(unitRewardButton, "Title");
            SetLabelText(_unitRewardTitleText, visible ? ResolveUnlockRewardTitle() : "해금");

            if (unitRewardText == null)
                unitRewardText = ResolveLabelGraphic(unitRewardButton);
            SetLabelText(unitRewardText, label);
        }

        private void HideArtifactChoices()
        {
            artifactChoicePanel?.Hide();
        }

        private static Graphic ResolveLabelGraphic(Button button)
        {
            if (button == null)
                return null;

            var tmpText = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
                return tmpText;

            return button.GetComponentInChildren<Text>(true);
        }

        private static Graphic ResolveChildLabelGraphic(Button button, string childName)
        {
            if (button == null)
                return null;

            foreach (var tmpText in button.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmpText != null && tmpText.name == childName)
                    return tmpText;
            }

            foreach (var legacyText in button.GetComponentsInChildren<Text>(true))
            {
                if (legacyText != null && legacyText.name == childName)
                    return legacyText;
            }

            return null;
        }

        private string ResolveUnlockRewardTitle()
        {
            return pendingUnlockRewardKind switch
            {
                UnlockRewardKind.Unit => "유닛",
                UnlockRewardKind.Building => "건물",
                UnlockRewardKind.Trap => "트랩",
                _ => "해금"
            };
        }

        private static void SetLabelText(Graphic labelGraphic, string value)
        {
            switch (labelGraphic)
            {
                case TMP_Text tmpText:
                    tmpText.text = value;
                    break;
                case Text legacyText:
                    legacyText.text = value;
                    break;
            }
        }
    }
}

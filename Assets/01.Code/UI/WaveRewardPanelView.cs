using System;
using System.Collections.Generic;
using _01.Code.Artifacts;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.MapCreateSystem;
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
        private enum OperationRewardKind
        {
            SupplyFunds
        }

        public static WaveRewardPanelView Current { get; private set; }

        [Header("Result")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Graphic goldAmountText;
        [SerializeField] private Button goldRewardButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text resultSummaryText;
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
        [SerializeField, Min(1)] private int artifactChoiceCount = 3;

        [Header("Supply Reward")]
        [SerializeField] private Button unitRewardButton;
        [SerializeField] private Graphic unitRewardText;
        [SerializeField, Min(1)] private int supplyGoldReward = 24;

        [Header("Medical Support")]
        [SerializeField, Range(0f, 100f)] private float medicalFatigueRecovery = 35f;
        [SerializeField, Range(0f, 1f)] private float medicalHealthRecoveryRatio = 0.3f;

        private readonly List<ArtifactDataSO> pendingArtifactChoices = new();
        private GameEventChannelSO _costEventChannel;
        private Graphic _operationTitleText;
        private int _pendingGoldAmount;
        private int _currentRewardDay;
        private bool _secondaryIsArtifact;
        private bool _hasPendingGoldReward;
        private bool _hasPendingSecondaryReward;
        private bool _hasPendingOperationReward;
        private bool _hasShownReward;
        private bool _hasWaveResult;
        private OperationRewardKind _operationRewardKind;

        public event Action Closed;
        public bool IsShowingReward => _hasShownReward && gameObject.activeSelf;
        public RectTransform UnlockRewardButtonRect => unitRewardButton != null
            ? unitRewardButton.transform as RectTransform
            : null;
        public RectTransform UnlockChoiceRect => artifactChoicePanel != null
            ? artifactChoicePanel.FirstChoiceRect
            : null;
        public RectTransform CurrentUnlockTutorialRect => artifactChoicePanel != null && artifactChoicePanel.IsShowingChoices
            ? artifactChoicePanel.FirstChoiceRect
            : UnlockRewardButtonRect;

        private bool HasPendingPrimaryReward => _hasPendingGoldReward
                                                || _hasPendingSecondaryReward
                                                || _hasPendingOperationReward;

        private void Awake()
        {
            if (artifactChoicePanel == null)
                artifactChoicePanel = GetComponentInChildren<ArtifactRewardChoicePanelView>(true);
        }

        private void OnEnable()
        {
            Current = this;
            goldRewardButton?.onClick.AddListener(HandleGoldRewardClicked);
            artifactRewardButton?.onClick.AddListener(HandleSecondaryRewardClicked);
            unitRewardButton?.onClick.AddListener(HandleOperationRewardClicked);
            closeButton?.onClick.AddListener(HandleCloseClicked);
            warningCancelButton?.onClick.AddListener(HideWarning);
            warningCloseButton?.onClick.AddListener(ForceClose);
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            goldRewardButton?.onClick.RemoveListener(HandleGoldRewardClicked);
            artifactRewardButton?.onClick.RemoveListener(HandleSecondaryRewardClicked);
            unitRewardButton?.onClick.RemoveListener(HandleOperationRewardClicked);
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
            ShowGoldReward(goldAmount, 0, includeArtifactReward);
        }

        public void ShowGoldReward(int goldAmount, int day, bool includeArtifactReward = true)
        {
            ShowWaveResult(goldAmount, day, 0, 0, 0, 0, 0, includeArtifactReward);
        }

        public void ShowWaveResult(
            int goldAmount,
            int day,
            int enemyCount,
            int killCount,
            int damageDealt,
            int damageTaken,
            int criticalHitCount,
            bool includeArtifactReward = true)
        {
            gameObject.SetActive(true);
            _currentRewardDay = Mathf.Max(0, day);
            _pendingGoldAmount = Mathf.Max(0, goldAmount);
            _operationRewardKind = OperationRewardKind.SupplyFunds;

            ConfigureModalLayout();
            PrepareArtifactChoices(includeArtifactReward);
            _secondaryIsArtifact = pendingArtifactChoices.Count > 0;
            _hasWaveResult = day > 0 || enemyCount > 0;
            _hasPendingGoldReward = _pendingGoldAmount > 0 && goldRewardButton != null;
            _hasPendingSecondaryReward = artifactRewardButton != null;
            _hasPendingOperationReward = unitRewardButton != null;

            if (!HasPendingPrimaryReward && !_hasWaveResult)
            {
                _hasShownReward = false;
                Hide();
                return;
            }

            _hasShownReward = true;
            RefreshResultSummary(enemyCount, killCount, damageDealt, damageTaken, criticalHitCount);
            RefreshRewardButtons();
            HideWarning();
            HideArtifactChoices();
        }

        public void Hide()
        {
            var shouldNotifyClosed = _hasShownReward && gameObject.activeSelf;
            _hasWaveResult = false;
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

            _costEventChannel?.RaiseEvent(new GoldEarnedEvent(_pendingGoldAmount, GoldChangeSource.WaveReward));
            CompletePrimaryReward("모험가에게서 회수한 금화를 금고에 보관했습니다");
        }

        private void HandleSecondaryRewardClicked()
        {
            if (!_hasPendingSecondaryReward)
                return;

            if (_secondaryIsArtifact)
            {
                if (artifactChoicePanel == null)
                {
                    Debug.LogError($"{nameof(WaveRewardPanelView)} requires an assigned artifact choice panel.", this);
                    return;
                }

                artifactChoicePanel.Show(pendingArtifactChoices, ObtainArtifact);
                return;
            }

            ApplyMedicalSupport();
            CompletePrimaryReward("의료 지원 적용 완료");
        }

        private void HandleOperationRewardClicked()
        {
            if (!_hasPendingOperationReward)
                return;

            switch (_operationRewardKind)
            {
                case OperationRewardKind.SupplyFunds:
                    _costEventChannel?.RaiseEvent(new GoldEarnedEvent(supplyGoldReward));
                    CompletePrimaryReward($"운영 보급 +{supplyGoldReward}G");
                    break;
            }
        }

        private void ObtainArtifact(ArtifactDataSO artifact)
        {
            if (artifact == null || artifactInventory == null)
                return;

            artifactInventory.Obtain(artifact, artifactEventChannel);
            pendingArtifactChoices.Clear();
            CompletePrimaryReward("유물 선택 완료");
        }

        private void ApplyMedicalSupport()
        {
            var processed = new HashSet<Unit>();
            foreach (var node in Node.ActiveNodes)
            {
                if (node == null)
                    continue;

                foreach (var placement in node.UnitPlacements)
                {
                    var unit = placement?.Instance;
                    if (unit == null || unit is MainUnit || !processed.Add(unit))
                        continue;

                    unit.ApplySupportRecovery(
                        medicalFatigueRecovery,
                        medicalHealthRecoveryRatio,
                        true);
                }
            }

            HiredUnitRoster.Current?.ApplyMedicalSupportToAvailableUnits(
                medicalFatigueRecovery,
                medicalHealthRecoveryRatio);
        }

        private void CompletePrimaryReward(string selectedLabel)
        {
            _hasPendingGoldReward = false;
            _hasPendingSecondaryReward = false;
            _hasPendingOperationReward = false;
            _pendingGoldAmount = 0;
            pendingArtifactChoices.Clear();
            HideArtifactChoices();
            HideWarning();

            SetButtonState(goldRewardButton, false, false);
            SetButtonState(artifactRewardButton, false, false);
            SetUnitRewardButtonState(false, selectedLabel, true);
            SetLabelText(_operationTitleText, "선택 완료");
        }

        private void HandleCloseClicked()
        {
            if (HasPendingPrimaryReward)
            {
                ShowWarning();
                return;
            }

            Hide();
        }

        private void ForceClose()
        {
            _hasPendingGoldReward = false;
            _hasPendingSecondaryReward = false;
            _hasPendingOperationReward = false;
            _pendingGoldAmount = 0;
            pendingArtifactChoices.Clear();
            HideArtifactChoices();
            Hide();
        }

        private void PrepareArtifactChoices(bool includeArtifactReward)
        {
            pendingArtifactChoices.Clear();
            if (!includeArtifactReward || artifactPool == null || artifactInventory == null)
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

        private void RefreshResultSummary(
            int enemyCount,
            int killCount,
            int damageDealt,
            int damageTaken,
            int criticalHitCount)
        {
            if (resultSummaryText == null)
                return;

            resultSummaryText.text =
                $"DAY {_currentRewardDay} · 습격 결산\n" +
                $"격퇴 {Mathf.Max(0, killCount)} / {Mathf.Max(0, enemyCount)}    " +
                $"치명타 {Mathf.Max(0, criticalHitCount)}회\n" +
                $"입힌 피해 {Mathf.Max(0, damageDealt):N0}    " +
                $"받은 피해 {Mathf.Max(0, damageTaken):N0}\n" +
                "드래곤의 명령으로 전리품 하나를 선택하세요";
        }

        private void RefreshRewardButtons()
        {
            if (iconImage != null)
                iconImage.gameObject.SetActive(_hasPendingGoldReward);

            SetLabelText(goldAmountText, _hasPendingGoldReward ? $"회수 금화 {_pendingGoldAmount:N0}G" : "회수 금화 없음");
            SetButtonState(goldRewardButton, _hasPendingGoldReward, _hasPendingGoldReward);

            var secondaryLabel = _secondaryIsArtifact
                ? "희귀 유물 선택"
                : $"피로 -{Mathf.RoundToInt(medicalFatigueRecovery)} · 부상 완화";
            SetArtifactRewardButtonState(true, secondaryLabel, true);

            var operationLabel = _operationRewardKind switch
            {
                OperationRewardKind.SupplyFunds => $"운영 보급 +{supplyGoldReward}G",
                _ => "던전 지원"
            };
            SetUnitRewardButtonState(true, operationLabel, true);
            ApplyRewardCardStyle(goldRewardButton, new Color(0.38f, 0.22f, 0.055f, 0.98f));
            ApplyRewardCardStyle(artifactRewardButton, new Color(0.24f, 0.075f, 0.3f, 0.98f));
            ApplyRewardCardStyle(unitRewardButton, new Color(0.28f, 0.09f, 0.055f, 0.98f));
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
                blocker.color = new Color(0.018f, 0.012f, 0.009f, 0.96f);
                blocker.raycastTarget = true;
            }

            if (resultSummaryText != null)
            {
                resultSummaryText.color = new Color(0.96f, 0.87f, 0.66f, 1f);
                resultSummaryText.fontStyle = FontStyles.Bold;
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

        private static void ApplyRewardCardStyle(Button button, Color color)
        {
            if (button == null)
                return;

            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
                button.targetGraphic = image;
            }

            var outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(0.82f, 0.58f, 0.22f, 0.96f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.88f, 0.68f, 1f);
            colors.pressedColor = new Color(0.72f, 0.68f, 0.62f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.42f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
        }

        private void SetArtifactRewardButtonState(bool interactable, string label, bool visible)
        {
            SetButtonState(artifactRewardButton, interactable, visible);
            if (artifactRewardText == null)
                artifactRewardText = ResolveLabelGraphic(artifactRewardButton);
            SetLabelText(artifactRewardText, label);
        }

        private void SetUnitRewardButtonState(bool interactable, string label, bool visible)
        {
            SetButtonState(unitRewardButton, interactable, visible);
            if (_operationTitleText == null)
                _operationTitleText = ResolveChildLabelGraphic(unitRewardButton, "Title");
            SetLabelText(_operationTitleText, "던전 운영 지원");

            if (unitRewardText == null)
                unitRewardText = ResolveLabelGraphic(unitRewardButton);
            SetLabelText(unitRewardText, label);
        }

        private static void SetButtonState(Button button, bool interactable, bool visible)
        {
            if (button == null)
                return;

            button.gameObject.SetActive(visible);
            button.interactable = interactable;
        }

        private void ShowWarning() => warningPanel?.SetActive(true);
        private void HideWarning() => warningPanel?.SetActive(false);
        private void HideArtifactChoices() => artifactChoicePanel?.Hide();

        private static Graphic ResolveLabelGraphic(Button button)
        {
            if (button == null)
                return null;

            var tmpText = button.GetComponentInChildren<TMP_Text>(true);
            return tmpText != null ? tmpText : button.GetComponentInChildren<Text>(true);
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

using System;
using System.Collections.Generic;
using _01.Code.Artifacts;
using _01.Code.Core;
using _01.Code.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
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
        [SerializeField] private ArtifactRewardChoicePanelView artifactChoicePanelPrefab;
        [SerializeField] private Transform artifactChoicePanelParent;
        [SerializeField, Min(1)] private int artifactChoiceCount = 3;

        private readonly List<ArtifactDataSO> pendingArtifactChoices = new();
        private GameEventChannelSO _costEventChannel;
        private ArtifactRewardChoicePanelView _artifactChoicePanel;
        private int _pendingGoldAmount;
        private bool _hasPendingGoldReward;
        private bool _hasPendingArtifactReward;
        private bool _hasShownReward;

        public event Action Closed;

        private void Awake()
        {
            EnsureArtifactRewardButton();
        }

        private void OnEnable()
        {
            goldRewardButton?.onClick.AddListener(HandleGoldRewardClicked);
            artifactRewardButton?.onClick.AddListener(HandleArtifactRewardClicked);
            closeButton?.onClick.AddListener(HandleCloseClicked);
            warningCancelButton?.onClick.AddListener(HideWarning);
            warningCloseButton?.onClick.AddListener(ForceClose);
        }

        private void OnDisable()
        {
            goldRewardButton?.onClick.RemoveListener(HandleGoldRewardClicked);
            artifactRewardButton?.onClick.RemoveListener(HandleArtifactRewardClicked);
            closeButton?.onClick.RemoveListener(HandleCloseClicked);
            warningCancelButton?.onClick.RemoveListener(HideWarning);
            warningCloseButton?.onClick.RemoveListener(ForceClose);
        }

        public void Initialize(GameEventChannelSO costEventChannel)
        {
            _costEventChannel = costEventChannel;
        }

        public void ShowGoldReward(int goldAmount)
        {
            EnsureArtifactRewardButton();
            PrepareArtifactChoices();

            _pendingGoldAmount = Mathf.Max(0, goldAmount);
            _hasPendingGoldReward = _pendingGoldAmount > 0;
            _hasPendingArtifactReward = pendingArtifactChoices.Count > 0;

            if (!_hasPendingGoldReward && !_hasPendingArtifactReward)
            {
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
            HideWarning();
            HideArtifactChoices();

            gameObject.SetActive(true);
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

            EnsureArtifactChoicePanel().Show(pendingArtifactChoices, ObtainArtifact);
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

        private void HandleCloseClicked()
        {
            if (_hasPendingGoldReward || _hasPendingArtifactReward)
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
            _pendingGoldAmount = 0;
            pendingArtifactChoices.Clear();
            HideArtifactChoices();
            Hide();
        }

        private void PrepareArtifactChoices()
        {
            pendingArtifactChoices.Clear();

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

        private ArtifactRewardChoicePanelView EnsureArtifactChoicePanel()
        {
            if (_artifactChoicePanel != null)
                return _artifactChoicePanel;

            var parent = artifactChoicePanelParent != null ? artifactChoicePanelParent : transform;
            _artifactChoicePanel = artifactChoicePanelPrefab != null
                ? Instantiate(artifactChoicePanelPrefab, parent)
                : new GameObject("ArtifactRewardChoicePanel", typeof(RectTransform), typeof(ArtifactRewardChoicePanelView)).GetComponent<ArtifactRewardChoicePanelView>();

            if (_artifactChoicePanel.transform.parent == null)
                _artifactChoicePanel.transform.SetParent(parent, false);

            _artifactChoicePanel.gameObject.SetActive(false);
            _artifactChoicePanel.transform.SetAsLastSibling();
            return _artifactChoicePanel;
        }

        private void EnsureArtifactRewardButton()
        {
            if (artifactRewardButton != null || goldRewardButton == null)
                return;

            artifactRewardButton = Instantiate(goldRewardButton, goldRewardButton.transform.parent);
            artifactRewardButton.name = "ArtifactRewardButton";
            artifactRewardButton.onClick.RemoveAllListeners();
            artifactRewardText = FindLabelGraphic(artifactRewardButton);

            if (artifactRewardButton.transform is RectTransform rect)
                rect.anchoredPosition += new Vector2(0f, -96f);
        }

        private void SetArtifactRewardButtonState(bool interactable, string label, bool visible = true)
        {
            if (artifactRewardButton == null)
                return;

            artifactRewardButton.gameObject.SetActive(visible);
            artifactRewardButton.interactable = interactable;

            if (artifactRewardText == null)
                artifactRewardText = FindLabelGraphic(artifactRewardButton);
            SetLabelText(artifactRewardText, label);
        }

        private void HideArtifactChoices()
        {
            if (_artifactChoicePanel != null)
                _artifactChoicePanel.Hide();
        }

        private static Graphic FindLabelGraphic(Button button)
        {
            if (button == null)
                return null;

            var tmpText = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
                return tmpText;

            return button.GetComponentInChildren<Text>(true);
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

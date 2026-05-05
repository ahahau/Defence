using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class WaveRewardPanelView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Text goldAmountText;
        [SerializeField] private Button goldRewardButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject warningPanel;
        [SerializeField] private Button warningCancelButton;
        [SerializeField] private Button warningCloseButton;

        private GameEventChannelSO _costEventChannel;
        private int _pendingGoldAmount;
        private bool _hasPendingGoldReward;

        private void OnEnable()
        {
            if (goldRewardButton != null)
                goldRewardButton.onClick.AddListener(HandleGoldRewardClicked);
            if (closeButton != null)
                closeButton.onClick.AddListener(HandleCloseClicked);
            if (warningCancelButton != null)
                warningCancelButton.onClick.AddListener(HideWarning);
            if (warningCloseButton != null)
                warningCloseButton.onClick.AddListener(ForceClose);
        }

        private void OnDisable()
        {
            if (goldRewardButton != null)
                goldRewardButton.onClick.RemoveListener(HandleGoldRewardClicked);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            if (warningCancelButton != null)
                warningCancelButton.onClick.RemoveListener(HideWarning);
            if (warningCloseButton != null)
                warningCloseButton.onClick.RemoveListener(ForceClose);
        }

        public void Initialize(GameEventChannelSO costEventChannel)
        {
            _costEventChannel = costEventChannel;
        }

        public void ShowGoldReward(int goldAmount)
        {
            if (goldAmount <= 0)
            {
                Hide();
                return;
            }

            _pendingGoldAmount = goldAmount;
            _hasPendingGoldReward = true;

            if (iconImage != null)
                iconImage.gameObject.SetActive(true);

            if (goldAmountText != null)
                goldAmountText.text = goldAmount.ToString();

            if (goldRewardButton != null)
                goldRewardButton.interactable = true;

            HideWarning();

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            HideWarning();
            gameObject.SetActive(false);
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
            if (iconImage != null)
                iconImage.gameObject.SetActive(false);
            if (goldAmountText != null)
                goldAmountText.text = string.Empty;
        }

        private void HandleCloseClicked()
        {
            if (_hasPendingGoldReward)
            {
                ShowWarning();
                return;
            }

            Hide();
        }

        private void ShowWarning()
        {
            if (warningPanel != null)
                warningPanel.SetActive(true);
        }

        private void HideWarning()
        {
            if (warningPanel != null)
                warningPanel.SetActive(false);
        }

        private void ForceClose()
        {
            _hasPendingGoldReward = false;
            _pendingGoldAmount = 0;
            Hide();
        }
    }
}

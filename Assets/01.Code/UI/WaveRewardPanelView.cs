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
        [SerializeField] private TMP_Text goldAmountText;
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
            goldRewardButton.onClick.AddListener(HandleGoldRewardClicked);
            closeButton.onClick.AddListener(HandleCloseClicked);
            warningCancelButton.onClick.AddListener(HideWarning);
            warningCloseButton.onClick.AddListener(ForceClose);
        }

        private void OnDisable()
        {
            goldRewardButton.onClick.RemoveListener(HandleGoldRewardClicked);
            closeButton.onClick.RemoveListener(HandleCloseClicked);
            warningCancelButton.onClick.RemoveListener(HideWarning);
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

            _costEventChannel.RaiseEvent(new GoldEarnedEvent(_pendingGoldAmount));
            _hasPendingGoldReward = false;
            _pendingGoldAmount = 0;

            if (goldRewardButton != null)
                goldRewardButton.interactable = false;
            if (goldAmountText != null)
                goldAmountText.text = "수령 완료";

            HideWarning();
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
            warningPanel.SetActive(true);
        }

        private void HideWarning()
        {
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

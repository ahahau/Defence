using System;
using TMPro;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>
    /// 시연 런 상인 좌판(2026-11-11 사용자 결정 "B 전용 좌판"). 항목마다 한 줄(이름·효과·가격·사기)과
    /// 새 부하 고르기 판을 그리고 클릭만 중계한다. 문구와 살 수 있는지는 <see cref="RunMerchantPresenter"/>가 정한다.
    /// </summary>
    public class RunMerchantView : MonoBehaviour, IEscapeClosable
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text gold;
        [SerializeField] private TMP_Text summary;
        [SerializeField] private GameObject listPanel;
        [SerializeField] private TMP_Text[] rowNames = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] rowDetails = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] rowPrices = Array.Empty<TMP_Text>();
        [SerializeField] private UiFrameButton[] rowButtons = Array.Empty<UiFrameButton>();
        [SerializeField] private TMP_Text warning;
        [SerializeField] private UiFrameButton leaveButton;
        [SerializeField] private GameObject pickPanel;
        [SerializeField] private TMP_Text pickHeading;
        [SerializeField] private UiFrameButton[] pickCards = Array.Empty<UiFrameButton>();
        [SerializeField] private TMP_Text[] pickBodies = Array.Empty<TMP_Text>();
        [SerializeField, Tooltip("pickCards와 같은 순서의 부하 그림(2026-11-12 사용자 A안 승인).")]
        private UnityEngine.UI.Image[] pickPortraits = Array.Empty<UnityEngine.UI.Image>();
        [SerializeField] private UiFrameButton pickBackButton;
        [SerializeField] private UiFrameButton pickConfirmButton;

        private UnityEngine.Events.UnityAction[] _rowClicks;
        private UnityEngine.Events.UnityAction[] _pickClicks;
        private UnityEngine.Events.UnityAction _leave;
        private UnityEngine.Events.UnityAction _pickBack;
        private UnityEngine.Events.UnityAction _pickConfirm;

        public event Action<int> RowClicked;
        public event Action LeaveClicked;
        public event Action<int> PickCardClicked;
        public event Action PickBackClicked;
        public event Action PickConfirmClicked;

        public bool IsOpen => root != null && root.activeSelf;
        public bool IsPicking => IsOpen && pickPanel != null && pickPanel.activeSelf;
        public int RowCount => rowButtons.Length;
        public int PickCount => pickCards.Length;

        // ESC는 새 부하 고르기 판만 닫는다. 좌판은 "떠나기"로만 끝난다(칸을 끝내는 결정이라 실수로 닫지 않게).
        bool IEscapeClosable.IsOpen => IsPicking;
        void IEscapeClosable.Close() => PickBackClicked?.Invoke();

        private void OnEnable()
        {
            EnsureRuntimeState();
            for (int i = 0; i < rowButtons.Length; i++) rowButtons[i].Button.onClick.AddListener(_rowClicks[i]);
            for (int i = 0; i < pickCards.Length; i++) pickCards[i].Button.onClick.AddListener(_pickClicks[i]);
            leaveButton?.Button.onClick.AddListener(_leave);
            pickBackButton?.Button.onClick.AddListener(_pickBack);
            pickConfirmButton?.Button.onClick.AddListener(_pickConfirm);
        }

        private void OnDisable()
        {
            if (_rowClicks == null) return;
            for (int i = 0; i < rowButtons.Length && i < _rowClicks.Length; i++) rowButtons[i].Button.onClick.RemoveListener(_rowClicks[i]);
            for (int i = 0; i < pickCards.Length && i < _pickClicks.Length; i++) pickCards[i].Button.onClick.RemoveListener(_pickClicks[i]);
            leaveButton?.Button.onClick.RemoveListener(_leave);
            pickBackButton?.Button.onClick.RemoveListener(_pickBack);
            pickConfirmButton?.Button.onClick.RemoveListener(_pickConfirm);
        }

        private void EnsureRuntimeState()
        {
            _leave ??= () => LeaveClicked?.Invoke();
            _pickBack ??= () => PickBackClicked?.Invoke();
            _pickConfirm ??= () => PickConfirmClicked?.Invoke();
            if (_rowClicks == null || _rowClicks.Length != rowButtons.Length)
            {
                _rowClicks = new UnityEngine.Events.UnityAction[rowButtons.Length];
                for (int i = 0; i < rowButtons.Length; i++)
                {
                    int index = i;
                    _rowClicks[i] = () => RowClicked?.Invoke(index);
                }
            }
            if (_pickClicks == null || _pickClicks.Length != pickCards.Length)
            {
                _pickClicks = new UnityEngine.Events.UnityAction[pickCards.Length];
                for (int i = 0; i < pickCards.Length; i++)
                {
                    int index = i;
                    _pickClicks[i] = () => PickCardClicked?.Invoke(index);
                }
            }
        }

        public void Open()
        {
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        public void SetHeader(string goldText, string summaryText)
        {
            gold?.SetText(goldText);
            summary?.SetText(summaryText);
        }

        /// <summary>항목 한 줄. 산 항목은 글자를 흐리고, 살 수 없으면 버튼을 잠근다.</summary>
        public void SetRow(int index, string name, string detail, string price, string buttonLabel, bool interactable, bool dim)
        {
            if (index >= rowButtons.Length) return;
            Color text = dim ? HudPalette.TextMuted : HudPalette.Text;
            if (index < rowNames.Length)
            {
                rowNames[index].SetText(name);
                rowNames[index].color = text;
            }
            if (index < rowDetails.Length)
            {
                rowDetails[index].SetText(detail);
                rowDetails[index].color = dim ? HudPalette.TextMuted : HudPalette.TextDim;
            }
            if (index < rowPrices.Length)
            {
                rowPrices[index].SetText(price);
                rowPrices[index].color = dim ? HudPalette.TextMuted : HudPalette.Accent;
            }
            rowButtons[index].SetLabel(buttonLabel);
            rowButtons[index].SetInteractable(interactable);
        }

        public void SetWarning(string text) => warning?.SetText(text ?? string.Empty);

        /// <summary>항목 목록과 새 부하 고르기 판 중 하나만 보인다.</summary>
        public void ShowPick(bool picking)
        {
            if (listPanel != null) listPanel.SetActive(!picking);
            if (pickPanel != null) pickPanel.SetActive(picking);
        }

        public void SetPickHeading(string text) => pickHeading?.SetText(text);

        public void SetPickCard(int index, string title, string body, Sprite portrait, bool selected)
        {
            if (index >= pickCards.Length) return;
            pickCards[index].SetLabel(title);
            pickCards[index].SetSelected(selected);
            if (index < pickBodies.Length) pickBodies[index].SetText(body);
            if (index >= pickPortraits.Length || pickPortraits[index] == null) return;
            pickPortraits[index].sprite = portrait;
            pickPortraits[index].enabled = portrait != null;
        }

        public void SetPickConfirm(string label, bool interactable)
        {
            if (pickConfirmButton == null) return;
            pickConfirmButton.SetLabel(label);
            pickConfirmButton.SetInteractable(interactable);
        }
    }
}

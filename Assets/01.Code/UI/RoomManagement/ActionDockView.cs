using System;
using TMPro;
using UnityEngine;

namespace Code.UI.RoomManagement
{
    /// <summary>
    /// 왼쪽 아래 행동 도크(uGUI, A안): 관리 안내 줄, 던전 관리(건설·유닛 배치·함정·시설), 운영(고용·상인·정산 정보·메뉴),
    /// 보유 유닛·부활 대기 줄과 보유 목록. 그리기와 버튼 입력만 하고, 할 일은 <see cref="RoomManagementScreen"/>이 정한다.
    /// </summary>
    public sealed class ActionDockView : MonoBehaviour
    {
        /// <summary>관리 버튼 순서(왼쪽부터).</summary>
        public static readonly RoomInstallKind[] KindOrder =
            { RoomInstallKind.Building, RoomInstallKind.Unit, RoomInstallKind.Trap, RoomInstallKind.Facility };

        [SerializeField] private TMP_Text managementLabel;
        [SerializeField, Tooltip("건설·유닛 배치·함정·시설 순서")] private UiFrameButton[] kindButtons;
        [SerializeField] private UiFrameButton recruitButton;
        [SerializeField] private UiFrameButton merchantButton;
        [SerializeField] private UiFrameButton settlementButton;
        [SerializeField] private UiFrameButton menuButton;
        [SerializeField] private TMP_Text rosterSummary;
        [SerializeField] private TMP_Text revivalQueue;
        [SerializeField] private UiFrameButton rosterToggle;
        [SerializeField] private GameObject rosterPanel;
        [SerializeField] private TMP_Text rosterList;
        [SerializeField, Tooltip("잠깐 띄우는 안내(방을 먼저 고르세요 등)")] private TMP_Text message;
        [SerializeField, Tooltip("안내 줄 바탕(글과 함께 켜고 끈다)")] private GameObject messageRoot;
        [SerializeField] private float messageSeconds = 4f;

        private UnityEngine.Events.UnityAction[] _kindHandlers;
        private float _messageUntil;

        public event Action<RoomInstallKind> KindRequested;
        public event Action RecruitRequested;
        public event Action MerchantRequested;
        public event Action SettlementInfoRequested;
        public event Action MenuRequested;
        public event Action RosterToggled;

        public bool IsRosterOpen => rosterPanel != null && rosterPanel.activeSelf;

        public void Render(in ActionDockViewModel model)
        {
            managementLabel.text = model.ManagementText;
            managementLabel.color = model.ManagementTone switch
            {
                ActionDockTone.PauseLocked => HudPalette.Loss,
                ActionDockTone.Management => HudPalette.Accent,
                _ => HudPalette.TextDim
            };

            foreach (var button in kindButtons)
                button.SetInteractable(model.CanManage);

            recruitButton?.SetInteractable(model.RecruitEnabled);
            if (merchantButton != null)
            {
                merchantButton.SetVisible(model.MerchantVisible);
                merchantButton.SetInteractable(model.MerchantEnabled);
            }

            rosterSummary.text = model.RosterSummary;
            revivalQueue.text = model.RevivalQueue;
            if (IsRosterOpen)
                rosterList.text = model.RosterList;
        }

        public void SetRosterOpen(bool open)
        {
            rosterPanel.SetActive(open);
            rosterToggle.SetLabel(open ? "목록 닫기" : "목록 열기");
            rosterToggle.SetSelected(open);
        }

        /// <summary>도크 위 안내 줄에 잠깐 띄운다(실제 시간).</summary>
        public void ShowMessage(string text)
        {
            message.text = text ?? string.Empty;
            messageRoot.SetActive(true);
            _messageUntil = Time.unscaledTime + messageSeconds;
        }

        public bool TryGetKindButtonScreenRect(RoomInstallKind kind, out Rect rect)
        {
            var index = Array.IndexOf(KindOrder, kind);
            rect = default;
            return index >= 0 && UiScreenRect.TryGet((RectTransform)kindButtons[index].transform, out rect);
        }

        public bool TryGetMerchantButtonScreenRect(out Rect rect)
        {
            rect = default;
            return merchantButton != null && merchantButton.gameObject.activeInHierarchy
                && UiScreenRect.TryGet((RectTransform)merchantButton.transform, out rect);
        }

        private void Awake()
        {
            _kindHandlers = new UnityEngine.Events.UnityAction[kindButtons.Length];
            for (var i = 0; i < _kindHandlers.Length; i++)
            {
                var kind = KindOrder[i];
                _kindHandlers[i] = () => KindRequested?.Invoke(kind);
            }

            messageRoot.SetActive(false);
            SetRosterOpen(false);
        }

        private void OnEnable()
        {
            for (var i = 0; i < _kindHandlers.Length; i++)
                kindButtons[i].Button.onClick.AddListener(_kindHandlers[i]);
            if (recruitButton != null) recruitButton.Button.onClick.AddListener(HandleRecruit);
            if (merchantButton != null) merchantButton.Button.onClick.AddListener(HandleMerchant);
            settlementButton.Button.onClick.AddListener(HandleSettlement);
            menuButton.Button.onClick.AddListener(HandleMenu);
            rosterToggle.Button.onClick.AddListener(HandleRoster);
        }

        private void OnDisable()
        {
            for (var i = 0; i < _kindHandlers.Length; i++)
                kindButtons[i].Button.onClick.RemoveListener(_kindHandlers[i]);
            if (recruitButton != null) recruitButton.Button.onClick.RemoveListener(HandleRecruit);
            if (merchantButton != null) merchantButton.Button.onClick.RemoveListener(HandleMerchant);
            settlementButton.Button.onClick.RemoveListener(HandleSettlement);
            menuButton.Button.onClick.RemoveListener(HandleMenu);
            rosterToggle.Button.onClick.RemoveListener(HandleRoster);
        }

        private void Update()
        {
            if (_messageUntil > 0f && Time.unscaledTime >= _messageUntil)
            {
                _messageUntil = 0f;
                messageRoot.SetActive(false);
            }
        }

        private void HandleSettlement() => SettlementInfoRequested?.Invoke();
        private void HandleRecruit() => RecruitRequested?.Invoke();
        private void HandleMerchant() => MerchantRequested?.Invoke();
        private void HandleMenu() => MenuRequested?.Invoke();
        private void HandleRoster() => RosterToggled?.Invoke();
    }
}

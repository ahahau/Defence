using Code.Audio;
using Code.Core;
using Code.Manager;
using Code.UI.ViewModels;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 하루 정산표(uGUI, A안) 화면 조립. 숫자는 전부 <see cref="ManagementSettlementManager"/>가 만든 보고서에서 읽는다 —
    /// 여기서 장부를 다시 계산하면 같은 하루를 두 가지로 설명하게 된다.
    ///
    /// 탭 셋(오늘 정산·계약/부채·유닛 상점) 중 하나만 보이고, 칸마다 그리기는 각 Pane이 맡는다.
    /// "오늘 정산" 칸의 값은 <see cref="SettlementOverviewPresenter"/>가 보고서·운영 자금·빚·청산까지 남은 날로 만든다.
    /// 정산 중 시간 정지는 관리자가 건다. 닫기("정산 완료"·ESC)는 <see cref="ManagementSettlementManager.ForceHidePanel"/>.
    /// ESC는 <see cref="UiEscapeRouter"/>가 받아 맨 위 창인 정산표부터 닫는다.
    /// </summary>
    public sealed class SettlementScreenView : MonoBehaviour, IEscapeClosable
    {
        [SerializeField] private ManagementSettlementManager settlement;
        [SerializeField, Tooltip(GameScope.FieldTooltip)] private GameScope scope;
        [SerializeField] private DayManager dayManager;
        [SerializeField] private GameObject root;
        [SerializeField, Tooltip("등장 연출로 띄워 올리는 카드")] private RectTransform card;
        [SerializeField] private CanvasGroup cardGroup;
        [SerializeField] private TMP_Text title;
        [SerializeField, Tooltip("제목 오른쪽 빚·청산일 안내")] private TMP_Text status;
        [SerializeField, Tooltip("오늘 정산·계약/부채·유닛 상점 순서")] private UiFrameButton[] tabs;
        [SerializeField, Tooltip("탭과 같은 순서의 칸")] private GameObject[] panes;
        [SerializeField] private SettlementSummaryPane summary;
        [SerializeField] private SettlementLoanPane loan;
        [SerializeField] private SettlementShopPane shop;
        [SerializeField] private UiFrameButton closeButton;
        [SerializeField] private float entranceSeconds = 0.13f;
        [SerializeField] private float entranceRise = 14f;

        private UnityEngine.Events.UnityAction[] _tabHandlers;
        private Vector2 _cardRestPosition;
        private float _entranceStart = -1f;
        private CostManager _cost;
        private SettlementReport _report;

        private void EnsureRuntimeState()
        {
            if (_tabHandlers != null)
                return;

            _tabHandlers = new UnityEngine.Events.UnityAction[tabs != null ? tabs.Length : 0];
            for (var i = 0; i < _tabHandlers.Length; i++)
            {
                var index = i;
                _tabHandlers[i] = () => SelectTab(index, true);
            }
        }

        private void Awake()
        {
            if (card != null)
                _cardRestPosition = card.anchoredPosition;

            EnsureRuntimeState();
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
            if (settlement == null)
                settlement = ManagementSettlementManager.Current;
            _cost = GameScope.Resolve(scope, s => s.Cost, this, nameof(scope));
            if (dayManager == null)
                dayManager = SceneReferenceFallback.Find<DayManager>(this, nameof(dayManager));

            for (var i = 0; i < _tabHandlers.Length; i++)
                tabs[i]?.Button.onClick.AddListener(_tabHandlers[i]);
            closeButton?.Button.onClick.AddListener(Close);
            if (shop != null)
                shop.Hired += HandleHired;

            if (settlement != null)
            {
                settlement.ReportOpened += Present;
                settlement.ReportClosed += HandleReportClosed;
            }

            // 씬을 이어받아 켜졌을 수 있다. 이미 열려 있던 정산표는 그대로 이어 그린다.
            if (settlement != null && settlement.IsPanelOpen && settlement.LatestReport != null)
                Present(settlement.LatestReport);
            else
                SetVisible(false);
        }

        private void OnDisable()
        {
            for (var i = 0; _tabHandlers != null && i < _tabHandlers.Length; i++)
                tabs[i]?.Button.onClick.RemoveListener(_tabHandlers[i]);
            closeButton?.Button.onClick.RemoveListener(Close);
            if (shop != null)
                shop.Hired -= HandleHired;

            if (settlement != null)
            {
                settlement.ReportOpened -= Present;
                settlement.ReportClosed -= HandleReportClosed;
            }
        }

        private void Update()
        {
            PlayEntrance();
        }

        private void Present(SettlementReport report)
        {
            if (report == null)
                return;

            SetVisible(true);
            GameSfxPlayer.Play(GameSfxCue.UiOpen);
            _entranceStart = Time.unscaledTime;
            PlayEntrance();

            _report = report;
            RenderOverview();
            loan.SetFeedback(string.Empty);
            loan.Refresh();
            shop.SetFeedback(string.Empty);
            shop.Refresh();
            shop.RewindScroll();
            SelectTab(0, false);
        }

        private void RenderOverview()
        {
            if (_report == null)
                return;

            var model = SettlementOverviewPresenter.Build(_report,
                _cost != null ? _cost.CurrentGold : 0,
                _cost != null ? _cost.CurrentDebt : 0,
                dayManager != null ? dayManager.DaysUntilSettlement : DayManager.WeekLength);
            title.text = model.Title;
            if (status != null)
            {
                status.text = model.Status;
                status.color = model.StatusWarn ? HudPalette.Warn : HudPalette.TextDim;
            }
            summary.Render(model);
        }

        /// <summary>어느 날이든 정산표는 오늘 정산 칸, 오늘 번 돈부터 읽힌다.</summary>
        private void SelectTab(int index, bool playSound)
        {
            for (var i = 0; i < panes.Length; i++)
                if (panes[i] != null)
                    panes[i].SetActive(i == index);

            for (var i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null)
                    continue;

                tabs[i].SetSelected(i == index);
                tabs[i].SetLabelColor(i == index ? HudPalette.Accent : HudPalette.TextDim);
            }

            if (playSound)
                GameSfxPlayer.Play(GameSfxCue.UiClick);
        }

        /// <summary>정산표에서 고용하면 운영 자금이 바뀐다. 계약 칸과 "남은 운영 자금"을 다시 그린다.</summary>
        private void HandleHired()
        {
            loan.Refresh();
            RenderOverview();
        }

        private void Close()
        {
            GameSfxPlayer.Play(GameSfxCue.UiClose);
            if (settlement != null)
                settlement.ForceHidePanel();
            else
                SetVisible(false);
        }

        private void HandleReportClosed() => SetVisible(false);

        bool IEscapeClosable.IsOpen => settlement != null && settlement.IsPanelOpen;

        void IEscapeClosable.Close() => Close();

        /// <summary>카드가 툭 나타나지 않게 한 번 띄워 올린다(실제 시간).</summary>
        private void PlayEntrance()
        {
            if (_entranceStart < 0f || card == null)
                return;

            var t = Mathf.Clamp01((Time.unscaledTime - _entranceStart) / Mathf.Max(0.01f, entranceSeconds));
            card.anchoredPosition = _cardRestPosition + new Vector2(0f, -(1f - t) * entranceRise);
            if (cardGroup != null)
                cardGroup.alpha = t;
            if (t >= 1f)
                _entranceStart = -1f;
        }

        private void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible)
                root.SetActive(visible);
        }
    }
}

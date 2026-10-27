using Code.Audio;
using Code.Manager;
using Code.UI.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 정산표 "오늘 정산" 칸(2026-10-27 한 화면 장부): 위 띠에 수입·지출·이미 반영된 몫·남은 운영 자금,
    /// 왼쪽에 수입·지출 장부, 오른쪽에 오늘 전과 칩 6자리, 아래에 해금 한 줄과 "목록" 펼치기.
    /// 값은 <see cref="SettlementOverviewPresenter"/>가 만든 모델을 그리기만 한다.
    /// </summary>
    public sealed class SettlementSummaryPane : MonoBehaviour
    {
        [SerializeField] private TMP_Text incomeTotal;
        [SerializeField] private TMP_Text expenseTotal;
        [SerializeField] private TMP_Text deferred;
        [SerializeField, Tooltip("남은 운영 자금과 정산으로 움직인 액수")] private TMP_Text remaining;
        [SerializeField] private TMP_Text ledger;
        [SerializeField] private ScrollRect ledgerScroll;
        [SerializeField, Tooltip("전과 칩 바탕(자리 수 = SettlementOverviewPresenter.MaxChips)")] private Image[] chipBackgrounds;
        [SerializeField, Tooltip("칩 바탕과 같은 순서의 글자")] private TMP_Text[] chipTexts;
        [SerializeField] private TMP_Text unlockSummary;
        [SerializeField] private UiFrameButton unlockToggle;
        [SerializeField, Tooltip("목록을 펼치면 장부·칩 위를 덮는 칸")] private GameObject unlockPanel;
        [SerializeField] private TMP_Text unlockList;
        [SerializeField] private ScrollRect unlockScroll;

        private bool _unlockOpen;

        public void Render(SettlementOverviewViewModel model)
        {
            incomeTotal.text = model.IncomeTotal;
            expenseTotal.text = model.ExpenseTotal;
            deferred.text = model.Deferred;
            // 정산으로 움직인 액수는 남은 자금 바로 뒤에 붙인다. 따로 두면 금액 길이에 따라 멀어진다.
            remaining.text = model.Remaining + "  <size=65%>"
                + HudPalette.Paint(model.NetChange, model.NetPositive ? HudPalette.Gain : HudPalette.Loss) + "</size>";

            ledger.text = model.Ledger;
            if (ledgerScroll != null)
                ledgerScroll.verticalNormalizedPosition = 1f;

            for (var i = 0; i < chipBackgrounds.Length; i++)
            {
                var visible = i < model.Chips.Count;
                // 빈 자리는 숨기기만 한다. 칸 크기는 칩 수와 상관없이 그대로다.
                chipBackgrounds[i].enabled = visible;
                chipTexts[i].text = visible ? model.Chips[i].Text : string.Empty;
                if (!visible)
                    continue;

                var tone = model.Chips[i].Tone;
                chipBackgrounds[i].color = tone == SettlementNoteTone.Danger ? HudPalette.DangerSurface : HudPalette.Chip;
                chipTexts[i].color = tone == SettlementNoteTone.Danger ? HudPalette.Text : ToneColor(tone);
            }

            unlockSummary.text = model.UnlockSummary;
            unlockList.text = model.UnlockList;
            // 새 정산은 늘 접힌 목록부터 연다.
            SetUnlockOpen(false);
        }

        private void OnEnable()
        {
            unlockToggle?.Button.onClick.AddListener(ToggleUnlocks);
        }

        private void OnDisable()
        {
            unlockToggle?.Button.onClick.RemoveListener(ToggleUnlocks);
        }

        private void ToggleUnlocks()
        {
            GameSfxPlayer.Play(GameSfxCue.UiClick);
            SetUnlockOpen(!_unlockOpen);
        }

        private void SetUnlockOpen(bool open)
        {
            _unlockOpen = open;
            if (unlockPanel != null)
                unlockPanel.SetActive(open);
            if (unlockScroll != null)
                unlockScroll.verticalNormalizedPosition = 1f;
            unlockToggle?.SetLabel(open ? "목록 접기" : "목록");
            unlockToggle?.SetSelected(open);
        }

        private static Color ToneColor(SettlementNoteTone tone) => tone switch
        {
            SettlementNoteTone.Good => HudPalette.Gain,
            SettlementNoteTone.Caution => HudPalette.Warn,
            SettlementNoteTone.Danger => HudPalette.Danger,
            SettlementNoteTone.Accent => HudPalette.AccentSoft,
            _ => HudPalette.TextBody
        };
    }
}

using System.Collections.Generic;
using System.Text;
using Code.Audio;
using Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 정산표 "이번 주 요약" 칸: 운영 자금 증감, 금고·선지출 몫, 메모, 부하 피로, 해금 로드맵(부하·시설 중 하나를 펼침).
    /// 숫자와 문구는 <see cref="SettlementReport"/>에서 읽기만 한다.
    /// </summary>
    public sealed class SettlementSummaryPane : MonoBehaviour
    {
        [SerializeField] private TMP_Text net;
        [SerializeField] private TMP_Text deferred;
        [SerializeField, Tooltip("메모와 부하 피로")] private TMP_Text notes;
        [SerializeField] private ScrollRect notesScroll;
        [SerializeField] private UiFrameButton detailsButton;
        [SerializeField] private UiFrameButton unitUnlockHeader;
        [SerializeField] private UiFrameButton buildingUnlockHeader;
        [SerializeField] private TMP_Text unlockList;
        [SerializeField] private ScrollRect unlockScroll;

        private readonly StringBuilder _builder = new();
        private SettlementReport _report;

        private UnlockCategory _openCategory = UnlockCategory.None;
        private bool _detailsOpen;

        public void Render(SettlementReport report)
        {
            _report = report;
            _detailsOpen = false;
            _openCategory = UnlockCategory.None;

            // 운영 자금이 실제로 얼마나 늘고 줄었는지를 앞세운다. 이게 내일 쓸 수 있는 돈이다.
            net.text = $"운영 자금 {GoldText.Signed(report.Net)}";
            net.color = report.Net >= 0 ? HudPalette.Gain : HudPalette.Loss;
            RefreshNotes();
            RenderUnlocks();
        }

        private void RefreshNotes()
        {
            if (_report == null)
                return;

            deferred.text = _detailsOpen && _report.DeferredDelta != 0
                ? $"그중 {GoldText.Signed(_report.DeferredDelta)}는 금고·선지출"
                : string.Empty;
            detailsButton?.SetLabel(_detailsOpen ? "상세 접기" : "상세 보기");
            detailsButton?.SetSelected(_detailsOpen);
            RenderNotes(_report.Notes, _report.Fatigue);
            if (notesScroll != null)
                notesScroll.verticalNormalizedPosition = 1f;
        }

        private void OnEnable()
        {
            detailsButton?.Button.onClick.AddListener(ToggleDetails);
            unitUnlockHeader?.Button.onClick.AddListener(ToggleUnitUnlocks);
            buildingUnlockHeader?.Button.onClick.AddListener(ToggleBuildingUnlocks);
        }

        private void OnDisable()
        {
            detailsButton?.Button.onClick.RemoveListener(ToggleDetails);
            unitUnlockHeader?.Button.onClick.RemoveListener(ToggleUnitUnlocks);
            buildingUnlockHeader?.Button.onClick.RemoveListener(ToggleBuildingUnlocks);
        }

        private void RenderNotes(IReadOnlyList<SettlementNote> noteLines, IReadOnlyList<SettlementFatigueLine> fatigue)
        {
            _builder.Clear();
            foreach (var note in noteLines)
            {
                var text = _detailsOpen ? note.Text : note.SummaryText;
                // 파산·보스 예고 등 위험은 전문만 있는 경우에도 감추지 않는다.
                if (!_detailsOpen && string.IsNullOrEmpty(text) && note.Tone == SettlementNoteTone.Danger)
                    text = note.Text;
                if (!string.IsNullOrEmpty(text))
                    _builder.AppendLine(HudPalette.Paint(text, ToneColor(note.Tone)));
            }

            if (_detailsOpen && fatigue.Count > 0)
            {
                _builder.AppendLine();
                _builder.AppendLine(HudPalette.Paint("부하 피로", HudPalette.Accent));
                for (var i = 0; i < fatigue.Count; i++)
                {
                    var line = fatigue[i];
                    // 탈진과 지친 상태는 색으로 구분한다 — 숫자만으로는 눈에 안 들어온다.
                    var text = line.IsExhausted ? $"{line.Label} 탈진 {line.Fatigue}" : $"{line.Label} {line.Fatigue}";
                    var color = line.IsExhausted ? HudPalette.Danger : line.IsTired ? HudPalette.Warn : HudPalette.TextBody;
                    if (i > 0)
                        _builder.Append("   ");
                    _builder.Append(HudPalette.Paint(text, color));
                }
            }

            notes.text = _builder.ToString();
        }

        private void RenderUnlocks()
        {
            if (_report == null)
                return;

            SetHeader(unitUnlockHeader, _report.UnitUnlocks, UnlockCategory.Units);
            SetHeader(buildingUnlockHeader, _report.BuildingUnlocks, UnlockCategory.Buildings);

            var section = _openCategory switch
            {
                UnlockCategory.Units => _report.UnitUnlocks,
                UnlockCategory.Buildings => _report.BuildingUnlocks,
                _ => null
            };

            _builder.Clear();
            if (section != null)
            {
                if (section.Lines.Count == 0)
                {
                    _builder.Append(_openCategory == UnlockCategory.Units
                        ? "고용 목록을 불러오는 중입니다."
                        : "시설 목록을 불러오는 중입니다.");
                }

                foreach (var line in section.Lines)
                {
                    _builder.AppendLine(line.IsUnlocked
                        ? HudPalette.Paint($"● {line.Name}  {section.UnlockedStateLabel}", HudPalette.TextBody)
                        : HudPalette.Paint($"○ {line.Name}  미발견 — {line.Hint}", HudPalette.TextMuted));
                }
            }

            unlockList.text = _builder.ToString();
            if (unlockScroll != null)
            {
                unlockScroll.gameObject.SetActive(section != null);
                unlockScroll.verticalNormalizedPosition = 1f;
            }
        }

        private void SetHeader(UiFrameButton header, SettlementUnlockSection section, UnlockCategory category)
        {
            if (header == null || section == null)
                return;

            var isOpen = _openCategory == category;
            header.SetLabel($"{section.Title}  {section.UnlockedCount}/{section.TotalCount}  {(isOpen ? "접기" : "펼치기")}");
            header.SetSelected(isOpen);
        }

        private void ToggleUnitUnlocks() => Toggle(UnlockCategory.Units);

        private void ToggleDetails()
        {
            _detailsOpen = !_detailsOpen;
            GameSfxPlayer.Play(GameSfxCue.UiClick);
            RefreshNotes();
        }

        private void ToggleBuildingUnlocks() => Toggle(UnlockCategory.Buildings);

        private void Toggle(UnlockCategory category)
        {
            _openCategory = _openCategory == category ? UnlockCategory.None : category;
            GameSfxPlayer.Play(GameSfxCue.UiClick);
            RenderUnlocks();
        }

        private static Color ToneColor(SettlementNoteTone tone) => tone switch
        {
            SettlementNoteTone.Good => HudPalette.Gain,
            SettlementNoteTone.Caution => HudPalette.Warn,
            SettlementNoteTone.Danger => HudPalette.Danger,
            SettlementNoteTone.Accent => HudPalette.AccentSoft,
            _ => HudPalette.TextBody
        };

        private enum UnlockCategory
        {
            None,
            Units,
            Buildings
        }
    }
}

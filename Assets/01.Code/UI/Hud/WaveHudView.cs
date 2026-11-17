using Code.UI.ViewModels;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 영업 HUD(uGUI, A안). 오른쪽 위 손님 브리핑(접힘: 제목·한 줄 요약, 펼침: 전체 설명),
    /// 영업 중에는 같은 자리에 진행 막대, 위 가운데 짧은 시작 배너, 오른쪽 아래 목표 전환·영업 시작 버튼.
    /// 판단은 <see cref="WaveView"/>가 하고, 여기서는 받은 값을 그리고 버튼 입력을 돌려준다.
    /// 지도 가운데·아래 가운데는 비운다(game-ui-frontend).
    /// </summary>
    public sealed class WaveHudView : MonoBehaviour
    {
        [SerializeField] private WaveView waveView;
        [SerializeField] private RecordBoardView recordBoard;

        [Header("Briefing")]
        [SerializeField] private RectTransform briefCard;
        [SerializeField] private TMP_Text briefTitle;
        [SerializeField] private TMP_Text briefSummary;
        [SerializeField] private TMP_Text briefObjective;
        [SerializeField] private TMP_Text briefBody;
        [SerializeField] private UiFrameButton detailToggle;
        [SerializeField] private float collapsedHeight = 164f;
        [SerializeField] private float expandedHeight = 440f;

        [Header("Progress")]
        [SerializeField] private GameObject progressCard;
        [SerializeField] private TMP_Text progressTitle;
        [SerializeField] private UnityEngine.UI.Image progressFill;
        [SerializeField] private TMP_Text progressStats;

        [Header("Actions")]
        [SerializeField] private UiFrameButton startButton;
        [SerializeField] private UiFrameButton objectiveButton;

        private readonly Vector3[] _corners = new Vector3[4];
        private WaveHudViewModel _model;
        private bool _expanded;
        private bool _yielded;

        /// <summary>오른쪽 열을 방 상세에 내준다. 그동안 브리핑·진행 카드를 숨긴다.</summary>
        public void SetRightColumnYielded(bool yielded)
        {
            if (_yielded == yielded)
                return;

            _yielded = yielded;
            Render(_model);
        }

        /// <summary>시작 버튼의 화면 사각형(왼쪽 아래 원점 픽셀). 튜토리얼이 비춘다. 숨었으면 false.</summary>
        public bool TryGetStartButtonScreenRect(out Rect rect)
        {
            rect = default;
            if (startButton == null || !startButton.gameObject.activeInHierarchy)
                return false;

            ((RectTransform)startButton.transform).GetWorldCorners(_corners);
            // 오버레이 캔버스라 월드 모서리가 곧 화면 픽셀이다.
            rect = Rect.MinMaxRect(_corners[0].x, _corners[0].y, _corners[2].x, _corners[2].y);
            return rect.width > 0f && rect.height > 0f;
        }

        private void OnEnable()
        {
            AddClick(startButton, HandleStartClicked);
            AddClick(objectiveButton, HandleObjectiveClicked);
            AddClick(detailToggle, ToggleDetail);
            if (waveView != null)
            {
                waveView.ModelChanged += Render;
                waveView.SetStartButtonRectSource(TryGetStartButtonScreenRect);
                Render(waveView.Model);
            }

        }

        private void OnDisable()
        {
            if (recordBoard != null)
                recordBoard.SetBriefingExpanded(false);
            RemoveClick(startButton, HandleStartClicked);
            RemoveClick(objectiveButton, HandleObjectiveClicked);
            RemoveClick(detailToggle, ToggleDetail);
            if (waveView != null)
            {
                waveView.ModelChanged -= Render;
                waveView.SetStartButtonRectSource(null);
            }

        }

        private void Render(WaveHudViewModel model)
        {
            _model = model;

            if (startButton != null)
            {
                startButton.SetVisible(model.StartVisible);
                startButton.SetInteractable(model.StartEnabled);
                // 튜토리얼이 비출 때 테두리를 금색으로 물들인다.
                startButton.SetSelected(model.StartHighlighted);
                startButton.SetLabel(model.StartLabel);
            }

            if (objectiveButton != null)
            {
                objectiveButton.SetVisible(model.ObjectiveVisible);
                objectiveButton.SetLabel(model.ObjectiveLabel);
            }

            RenderBriefing();

            if (progressCard != null) progressCard.SetActive(model.ProgressVisible && !_yielded);
            SetText(progressTitle, model.ProgressTitle);
            SetText(progressStats, model.ProgressStats);
            if (progressFill != null)
            {
                progressFill.fillAmount = Mathf.Clamp01(model.Progress);
                progressFill.color = model.ProgressIsBoss ? HudPalette.Accent : HudPalette.Danger;
            }
        }

        /// <summary>
        /// 접으면 첫 줄(손님 수)과 마지막 줄(선택 목표)만 남긴다. 긴 설명(특성·추천·등급)은 펼쳐야 보인다.
        /// 문장은 <see cref="WaveView"/>가 만든 그대로이고 줄을 고르기만 한다.
        /// </summary>
        private void RenderBriefing()
        {
            if (recordBoard != null)
                recordBoard.SetBriefingExpanded(_model.PreparationVisible && _expanded && !_yielded);

            if (briefCard == null)
                return;

            var visible = _model.PreparationVisible && !_yielded;
            if (briefCard.gameObject.activeSelf != visible)
                briefCard.gameObject.SetActive(visible);
            if (!visible)
                return;

            var body = _model.PreparationBody ?? string.Empty;
            var lines = body.Split('\n');
            SetText(briefTitle, _model.PreparationTitle);
            SetText(briefSummary, lines.Length > 0 ? lines[0] : string.Empty);
            SetText(briefObjective, lines.Length > 1 ? lines[^1] : string.Empty);
            SetText(briefBody, _expanded ? body : string.Empty);
            if (briefBody != null) briefBody.gameObject.SetActive(_expanded);
            if (briefSummary != null) briefSummary.gameObject.SetActive(!_expanded);
            if (briefObjective != null) briefObjective.gameObject.SetActive(!_expanded && !string.IsNullOrEmpty(briefObjective.text));
            // 잠실 글꼴에 ▾·▴ 글자가 없어 네모로 보인다. 글자로만 쓴다.
            if (detailToggle != null) detailToggle.SetLabel(_expanded ? "접기" : "자세히");

            var size = briefCard.sizeDelta;
            size.y = _expanded ? expandedHeight : collapsedHeight;
            briefCard.sizeDelta = size;
        }

        private void ToggleDetail()
        {
            _expanded = !_expanded;
            RenderBriefing();
        }

        private void HandleStartClicked()
        {
            if (waveView != null)
                waveView.RequestStart();
        }

        private void HandleObjectiveClicked()
        {
            if (waveView != null)
                waveView.ToggleObjective();
        }

        private static void AddClick(UiFrameButton button, UnityEngine.Events.UnityAction action)
        {
            if (button != null && button.Button != null)
                button.Button.onClick.AddListener(action);
        }

        private static void RemoveClick(UiFrameButton button, UnityEngine.Events.UnityAction action)
        {
            if (button != null && button.Button != null)
                button.Button.onClick.RemoveListener(action);
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null && label.text != value)
                label.text = value ?? string.Empty;
        }
    }
}

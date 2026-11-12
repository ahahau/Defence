using System;
using TMPro;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>
    /// 던전 FTL 실시간 전투 HUD(2026-11-05 목업 6안, 로보토미식): 위 왼쪽 장부, 위 가운데 목표 막대와 시계,
    /// 위 오른쪽 멈춤·배속, 왼쪽 부하 목록, 고른 부하·방의 작은 정보 창, 마우스 설명 카드, 결과 띠, 멈춤 테두리.
    /// 값은 디렉터가 넘기고, 버튼 클릭만 밖으로 알린다.
    /// </summary>
    public class RunHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text ledger;
        [SerializeField] private TMP_Text goalLabel;
        [SerializeField] private UnityEngine.UI.Image goalFill;
        [SerializeField] private TMP_Text clock;
        [SerializeField] private UiFrameButton pauseButton;
        [SerializeField] private UiFrameButton speedOneButton;
        [SerializeField] private UiFrameButton speedTwoButton;
        [SerializeField] private GameObject pauseFrame;
        [SerializeField] private RunRosterCard[] rosterCards = Array.Empty<RunRosterCard>();
        [SerializeField] private RectTransform infoPanel;
        [SerializeField] private TMP_Text infoTitle;
        [SerializeField] private TMP_Text infoBody;
        [SerializeField] private UnityEngine.UI.Image infoHealth;
        [SerializeField] private UnityEngine.UI.Image infoSkill;
        [SerializeField] private GameObject banner;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private UiFrameButton nextEncounterButton;
        [SerializeField] private RectTransform tooltip;
        [SerializeField] private TMP_Text tooltipText;
        [SerializeField] private UiFrameButton[] floorTabs = Array.Empty<UiFrameButton>();
        [SerializeField] private TMP_Text floorNotice;
        [SerializeField] private TMP_Text entryWaiting;

        public RectTransform EntryWaitingRect => entryWaiting != null ? entryWaiting.rectTransform : null;

        public void ShowEntryWaiting(string text)
        {
            if (entryWaiting != null) entryWaiting.SetText(text);
        }

        private UnityEngine.Events.UnityAction _pauseAction;
        private UnityEngine.Events.UnityAction _nextEncounterAction;
        private UnityEngine.Events.UnityAction _speedOneAction;
        private UnityEngine.Events.UnityAction _speedTwoAction;
        private Action[] _rosterActions;
        private UnityEngine.Events.UnityAction[] _floorActions;

        public event Action PauseClicked;
        public event Action NextEncounterClicked;
        public event Action<int> SpeedClicked;
        public event Action<int> RosterClicked;
        public event Action<int> FloorClicked;

        public int RosterCapacity => rosterCards.Length;

        /// <summary>첫 판 안내가 가리킬 멈춤 버튼.</summary>
        public RectTransform PauseRect => pauseButton != null ? (RectTransform)pauseButton.transform : null;

        /// <summary>첫 판 안내가 가리킬 왼쪽 부하 카드. 없는 번호면 null.</summary>
        public RectTransform RosterCardRect(int index) =>
            index >= 0 && index < rosterCards.Length && rosterCards[index] != null ? (RectTransform)rosterCards[index].transform : null;

        private void OnEnable()
        {
            EnsureRuntimeState();
            if (nextEncounterButton != null) nextEncounterButton.Button.onClick.AddListener(_nextEncounterAction);
            if (pauseButton != null && pauseButton.Button != null) pauseButton.Button.onClick.AddListener(_pauseAction);
            if (speedOneButton != null && speedOneButton.Button != null) speedOneButton.Button.onClick.AddListener(_speedOneAction);
            if (speedTwoButton != null && speedTwoButton.Button != null) speedTwoButton.Button.onClick.AddListener(_speedTwoAction);
            for (int i = 0; i < rosterCards.Length; i++)
                if (rosterCards[i] != null) rosterCards[i].Clicked += _rosterActions[i];
            for (int i = 0; i < floorTabs.Length; i++)
                if (floorTabs[i] != null) floorTabs[i].Button.onClick.AddListener(_floorActions[i]);
        }

        private void OnDisable()
        {
            if (_pauseAction == null) return;
            if (nextEncounterButton != null) nextEncounterButton.Button.onClick.RemoveListener(_nextEncounterAction);
            if (pauseButton != null && pauseButton.Button != null) pauseButton.Button.onClick.RemoveListener(_pauseAction);
            if (speedOneButton != null && speedOneButton.Button != null) speedOneButton.Button.onClick.RemoveListener(_speedOneAction);
            if (speedTwoButton != null && speedTwoButton.Button != null) speedTwoButton.Button.onClick.RemoveListener(_speedTwoAction);
            if (_rosterActions == null) return;
            for (int i = 0; i < rosterCards.Length && i < _rosterActions.Length; i++)
                if (rosterCards[i] != null) rosterCards[i].Clicked -= _rosterActions[i];
            if (_floorActions == null) return;
            for (int i = 0; i < floorTabs.Length && i < _floorActions.Length; i++)
                if (floorTabs[i] != null) floorTabs[i].Button.onClick.RemoveListener(_floorActions[i]);
        }

        private void EnsureRuntimeState()
        {
            _pauseAction ??= () => PauseClicked?.Invoke();
            _nextEncounterAction ??= () => NextEncounterClicked?.Invoke();
            _speedOneAction ??= () => SpeedClicked?.Invoke(1);
            _speedTwoAction ??= () => SpeedClicked?.Invoke(2);
            if (_floorActions == null || _floorActions.Length != floorTabs.Length)
            {
                _floorActions = new UnityEngine.Events.UnityAction[floorTabs.Length];
                for (int i = 0; i < floorTabs.Length; i++)
                {
                    int index = i;
                    _floorActions[i] = () => FloorClicked?.Invoke(index);
                }
            }
            if (_rosterActions != null && _rosterActions.Length == rosterCards.Length) return;
            _rosterActions = new Action[rosterCards.Length];
            for (int i = 0; i < rosterCards.Length; i++)
            {
                int index = i;
                _rosterActions[i] = () => RosterClicked?.Invoke(index);
            }
        }

        public void ShowLedger(string text)
        {
            if (ledger != null) ledger.text = text;
        }

        /// <summary>위 가운데 목표 막대. ratio는 채울 몫(0~1).</summary>
        public void ShowGoal(string label, float ratio)
        {
            if (goalLabel != null) goalLabel.text = label;
            if (goalFill != null) goalFill.fillAmount = Mathf.Clamp01(ratio);
        }

        public void ShowClock(string text)
        {
            if (clock != null) clock.text = text;
        }

        public void ShowTimeControls(bool paused, int speed)
        {
            if (pauseButton != null)
            {
                pauseButton.SetLabel(paused ? "계속 (Space)" : "멈춤 (Space)");
                pauseButton.SetSelected(paused);
            }
            if (speedOneButton != null) speedOneButton.SetSelected(speed == 1);
            if (speedTwoButton != null) speedTwoButton.SetSelected(speed == 2);
            if (pauseFrame != null) pauseFrame.SetActive(paused);
        }

        public void ShowRosterCard(int index, bool visible, Sprite face, string displayName, string place, float health,
            bool selected, bool down, Color affinityColor)
        {
            if (index < 0 || index >= rosterCards.Length || rosterCards[index] == null) return;
            rosterCards[index].gameObject.SetActive(visible);
            if (visible) rosterCards[index].Show(face, displayName, place, health, selected, down, affinityColor);
        }

        /// <summary>열린 층 수만큼만 층 탭을 보인다. 안 열린 층은 띄우지 않는다.</summary>
        public void ShowFloor(string label, int selected, int openFloors)
        {
            for (int i = 0; i < floorTabs.Length; i++)
            {
                if (floorTabs[i] == null) continue;
                bool open = i < openFloors;
                floorTabs[i].gameObject.SetActive(open);
                if (!open) continue;
                floorTabs[i].SetSelected(i == selected);
                if (i == selected) floorTabs[i].SetLabel(label);
            }
        }

        public void ShowFloorNotice(string text)
        {
            if (floorNotice != null) floorNotice.SetText(text);
        }

        /// <summary>작은 정보 창. title이 비면 숨긴다. health·skill이 음수면 그 막대를 숨긴다.</summary>
        public void ShowInfo(string title, string body, float health, float skill, Vector2 screenPosition)
        {
            if (infoPanel == null) return;
            bool visible = !string.IsNullOrEmpty(title);
            infoPanel.gameObject.SetActive(visible);
            if (!visible) return;
            if (infoTitle != null) infoTitle.text = title;
            if (infoBody != null)
            {
                infoBody.text = body;
                // 떠 있는 작은 창이라 고정 칸 규칙 대신 글 줄 수에 높이를 맞춘다(2026-11-12 사용자 선택 "같이 줄이기").
                float width = infoPanel.sizeDelta.x;
                float lines = infoBody.GetPreferredValues(body ?? string.Empty, width + infoBody.rectTransform.sizeDelta.x, 0f).y;
                infoPanel.sizeDelta = new Vector2(width, Mathf.Ceil(lines) - infoBody.rectTransform.sizeDelta.y);
            }
            SetBar(infoHealth, health);
            SetBar(infoSkill, skill);
            // 정보 창은 머리 위로 띄워 그 인물과 둘레 싸움을 가리지 않는다.
            PlaceBeside(infoPanel, screenPosition, 28f, true);
        }

        public void ShowBanner(string text, bool canContinue = false, string nextLabel = "다음 침입")
        {
            if (banner != null) banner.SetActive(!string.IsNullOrEmpty(text));
            if (bannerText != null) bannerText.text = text ?? string.Empty;
            if (nextEncounterButton != null)
            {
                nextEncounterButton.SetLabel(nextLabel);
                nextEncounterButton.gameObject.SetActive(canContinue);
            }
        }

        /// <summary>마우스 설명 카드. 화면 좌표 옆에 띄우고, text가 비면 숨긴다.</summary>
        public void ShowTooltip(string text, Vector2 screenPosition)
        {
            if (tooltip == null) return;
            bool visible = !string.IsNullOrEmpty(text);
            tooltip.gameObject.SetActive(visible);
            if (!visible) return;
            if (tooltipText != null)
            {
                tooltipText.text = text;
                // 두 줄짜리 작은 카드: 폭은 씬 값, 높이는 글에 맞춘다(2026-11-12 사용자 선택 "두 줄짜리 작은 카드").
                float width = tooltip.sizeDelta.x;
                float lines = tooltipText.GetPreferredValues(text, width + tooltipText.rectTransform.sizeDelta.x, 0f).y;
                tooltip.sizeDelta = new Vector2(width, Mathf.Ceil(lines) - tooltipText.rectTransform.sizeDelta.y);
            }
            PlaceBeside(tooltip, screenPosition, 18f);
        }

        private static void SetBar(UnityEngine.UI.Image fill, float value)
        {
            if (fill == null) return;
            fill.transform.parent.gameObject.SetActive(value >= 0f);
            fill.fillAmount = Mathf.Clamp01(value);
        }

        /// <summary>화면 오른쪽 절반에서는 창을 점 왼쪽에 띄워 잘리지 않게 한다.</summary>
        private static void PlaceBeside(RectTransform panel, Vector2 screenPosition, float gap, bool above = false)
        {
            RectTransform canvas = (RectTransform)panel.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screenPosition, null, out Vector2 local);
            bool right = screenPosition.x > Screen.width * 0.5f;
            panel.pivot = new Vector2(right ? 1f : 0f, above ? 0f : 1f);
            panel.anchoredPosition = local + new Vector2(right ? -gap : gap, above ? gap : -gap);
        }
    }
}

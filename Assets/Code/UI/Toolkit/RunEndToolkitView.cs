using Code.Manager;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class RunEndToolkitView : MonoBehaviour
    {
        public static RunEndToolkitView Current { get; private set; }
        private const string PanelName = "run-end-root";
        private VisualElement _root;

        /// <summary>
        /// 실제로 켜고 끄는 패널. USS가 이 요소를 기본으로 숨겨 두므로 문서 루트만 켜면 화면에 아무것도 안 뜬다.
        /// </summary>
        private VisualElement _panel;
        private Label _title;
        private Label _headline;
        private Label _summary;
        private Label _meta;
        private VisualElement _actions;
        private Button _retry;
        private Button _toTitle;

        private const string MetaName = "run-end-meta";
        private const string ActionsName = "run-end-actions";
        private const string ToTitleName = "run-end-to-title";
        private const string StageClass = "run-end-stage";
        private const string StageShownClass = "run-end-stage--shown";
        private const int TitleSceneBuildIndex = 0;

        // 끝난 판을 한 번에 쏟아 놓지 않고 제목 → 통계 → 진행도 → 버튼 순서로 보여 준다(실제 시간 ms).
        private const long HeadlineDelayMs = 200;
        private const long SummaryDelayMs = 900;
        private const long MetaDelayMs = 1600;
        private const long ActionsDelayMs = 2300;

        private void OnEnable()
        {
            Current = this;
            _root = GetComponent<UIDocument>().rootVisualElement;
            _title = _root.Q<Label>("run-end-title"); _headline = _root.Q<Label>("run-end-headline");
            _summary = _root.Q<Label>("run-end-summary"); _retry = _root.Q<Button>("run-end-retry");
            _meta = _root.Q<Label>(MetaName); _actions = _root.Q<VisualElement>(ActionsName);
            _toTitle = _root.Q<Button>(ToTitleName);
            if (_retry != null) _retry.clicked += Restart;
            if (_toTitle != null) _toTitle.clicked += ReturnToTitle;
            _panel = _root.Q<VisualElement>(PanelName) ?? _root;
            _panel.style.display = DisplayStyle.None;
        }
        private void OnDisable()
        {
            if (_retry != null) _retry.clicked -= Restart;
            if (_toTitle != null) _toTitle.clicked -= ReturnToTitle;
            if (Current == this) Current = null;
        }
        public static bool TryShow(string title, string headline)
        {
            if (Current == null) return false;
            Current.Show(title, headline); return true;
        }
        private void Show(string title, string headline)
        {
            _title.text = title; _headline.text = headline;
            var day = DayManager.Current?.CurrentDay ?? 0;
            var cost = CostManager.Current;
            var roster = HiredUnitRoster.Current;
            var stats = $"버틴 날 {day}일\n남은 금화 {cost?.CurrentGold ?? 0}G · 부채 {cost?.CurrentDebt ?? 0}G\n부하 {roster?.TotalHiredCount ?? 0}명";
            var metaLine = BuildMetaLine().TrimStart('\n');
            if (_meta != null)
            {
                _summary.text = stats;
                _meta.text = metaLine;
            }
            else
            {
                _summary.text = metaLine.Length > 0 ? $"{stats}\n\n{metaLine}" : stats;
            }

            _panel.style.display = DisplayStyle.Flex;
            PlayReveal();
        }

        /// <summary>단계마다 하나씩 드러낸다. 시간이 멈춘 뒤라 패널 스케줄러(실제 시간)를 쓴다.</summary>
        private void PlayReveal()
        {
            foreach (var stage in _panel.Query(className: StageClass).ToList())
                stage.RemoveFromClassList(StageShownClass);
            _actions?.SetEnabled(false);

            Reveal(_title, 0);
            Reveal(_headline, HeadlineDelayMs);
            Reveal(_summary, SummaryDelayMs);
            Reveal(_meta, MetaDelayMs);
            Reveal(_actions, ActionsDelayMs);
            if (_actions != null)
                _panel.schedule.Execute(() => _actions.SetEnabled(true)).StartingIn(ActionsDelayMs);
        }

        private void Reveal(VisualElement element, long delayMs)
        {
            if (element == null)
                return;

            _panel.schedule.Execute(() => element.AddToClassList(StageShownClass)).StartingIn(delayMs);
        }
        /// <summary>
        /// 이번 판이 남긴 진행도. 패배가 무엇을 남겼는지 보여야 다음 판을 시작할 이유가 생긴다.
        /// </summary>
        private static string BuildMetaLine()
        {
            var result = Code.Progression.MetaProfileStore.LastResult;
            if (!result.HasValue)
                return string.Empty;

            var r = result.Value;
            var line = $"\n\n진행도 +{r.PointsEarned} (누적 {r.TotalProgress})";
            if (r.NewBestDay)
                line += " · 최고 기록!";
            if (r.SlotsAfter > r.SlotsBefore)
                line += $"\n시작 보정 칸이 {r.SlotsAfter}칸으로 늘었습니다";

            var unlocked = CollectNewlyUnlocked(r.TotalProgress - r.PointsEarned, r.TotalProgress);
            if (unlocked.Count > 0)
                line += $"\n다음 판부터 새로 나옴: {string.Join(", ", unlocked)}";
            return line;
        }

        /// <summary>이번 판으로 진행도 조건을 채운 유닛·함정·시설·사건 이름.</summary>
        private static System.Collections.Generic.List<string> CollectNewlyUnlocked(int before, int after)
        {
            var names = new System.Collections.Generic.List<string>();
            var catalog = HiredUnitRoster.Current != null ? HiredUnitRoster.Current.UnlockCatalog : null;
            if (catalog != null)
            {
                foreach (var entry in catalog.Entries)
                {
                    if (entry != null && Code.Progression.MetaUnlockRules.BecameUnlocked(entry.MetaProgressRequired, before, after))
                        names.Add(entry.DisplayName);
                }
            }

            var runner = Code.Core.SceneReferenceFallback.FindForRuntimeObject<Code.Dialogue.DialogueRunner>();
            if (runner != null)
            {
                foreach (var sequence in runner.ScheduledEventSequences)
                {
                    if (sequence != null && Code.Progression.MetaUnlockRules.BecameUnlocked(sequence.MetaProgressRequired, before, after))
                        names.Add($"사건 '{sequence.DisplayTitle}'");
                }
            }

            return names;
        }

        private void Restart() { GameSpeedController.Current?.ResetToNormal(); SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }

        /// <summary>타이틀로 돌아간다. 판 저장은 판이 끝날 때 이미 지워졌다.</summary>
        private void ReturnToTitle() { GameSpeedController.Current?.ResetToNormal(); SceneManager.LoadScene(TitleSceneBuildIndex); }
    }
}

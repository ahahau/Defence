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
        private Button _retry;

        private void OnEnable()
        {
            Current = this;
            _root = GetComponent<UIDocument>().rootVisualElement;
            _title = _root.Q<Label>("run-end-title"); _headline = _root.Q<Label>("run-end-headline");
            _summary = _root.Q<Label>("run-end-summary"); _retry = _root.Q<Button>("run-end-retry");
            if (_retry != null) _retry.clicked += Restart;
            _panel = _root.Q<VisualElement>(PanelName) ?? _root;
            _panel.style.display = DisplayStyle.None;
        }
        private void OnDisable() { if (_retry != null) _retry.clicked -= Restart; if (Current == this) Current = null; }
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
            _summary.text = $"버틴 날 {day}일\n남은 금화 {cost?.CurrentGold ?? 0}G · 부채 {cost?.CurrentDebt ?? 0}G\n부하 {roster?.TotalHiredCount ?? 0}명";
            _panel.style.display = DisplayStyle.Flex;
        }
        private void Restart() { GameSpeedController.Current?.ResetToNormal(); SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    }
}

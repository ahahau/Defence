using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 보스 습격 배너(UI Toolkit). 보스 웨이브가 시작될 때 잠깐 미끄러져 들어왔다 사라진다.
    /// 시간이 멈춰 있어도 흘러야 하므로 패널 스케줄러(실제 시간)로 잰다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BossBannerToolkitView : MonoBehaviour
    {
        private const string ScreenResourcePath = "UI/BossBannerScreen";
        private const string RootName = "BossBannerToolkitUi";

        private const string RootElement = "boss-banner-root";
        private const string CardElement = "boss-banner-card";
        private const string TitleLabel = "boss-banner-title";
        private const string SubtitleLabel = "boss-banner-subtitle";
        private const string ShownClass = "boss-banner-card--shown";
        private const string LeavingClass = "boss-banner-card--leaving";
        private const string FinalTitleClass = "boss-banner-title--final";
        private const long LeaveFadeMs = 400;

        private static BossBannerToolkitView _instance;

        private VisualElement _root;
        private VisualElement _card;
        private Label _title;
        private Label _subtitle;
        private IVisualElementScheduledItem _leave;
        private IVisualElementScheduledItem _hide;

        /// <summary>배너를 띄운다. 이미 떠 있으면 새 문구로 다시 시작한다.</summary>
        public static void Show(string title, string subtitle, bool isFinal, float holdSeconds)
        {
            if (_instance == null)
                _instance = ToolkitScreenSO.Spawn<BossBannerToolkitView>(ScreenResourcePath, RootName);
            _instance?.Play(title, subtitle, isFinal, holdSeconds);
        }

        /// <summary>지금 배너가 보이는가(들어오거나 머무는 중).</summary>
        public static bool IsShowing =>
            _instance != null && _instance._card != null && _instance._card.ClassListContains(ShownClass);

        private void OnEnable()
        {
            var tree = GetComponent<UIDocument>().rootVisualElement;
            _root = tree.Q<VisualElement>(RootElement) ?? tree;
            _card = tree.Q<VisualElement>(CardElement);
            _title = tree.Q<Label>(TitleLabel);
            _subtitle = tree.Q<Label>(SubtitleLabel);
            _root.style.display = DisplayStyle.None;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Play(string title, string subtitle, bool isFinal, float holdSeconds)
        {
            if (_card == null)
                return;

            if (_title != null)
            {
                _title.text = title;
                _title.EnableInClassList(FinalTitleClass, isFinal);
            }

            if (_subtitle != null)
                _subtitle.text = subtitle;

            _leave?.Pause();
            _hide?.Pause();
            _card.RemoveFromClassList(ShownClass);
            _card.RemoveFromClassList(LeavingClass);
            _root.style.display = DisplayStyle.Flex;

            // 한 프레임 뒤에 클래스를 붙여야 전환(미끄러짐·서서히 나타남)이 일어난다.
            _card.schedule.Execute(() => _card.AddToClassList(ShownClass)).StartingIn(16);

            var holdMs = (long)(Mathf.Max(0.1f, holdSeconds) * 1000f) + 350;
            _leave = _card.schedule.Execute(() =>
            {
                _card.AddToClassList(LeavingClass);
                _card.RemoveFromClassList(ShownClass);
            }).StartingIn(holdMs);
            _hide = _card.schedule.Execute(() =>
            {
                _card.RemoveFromClassList(LeavingClass);
                _root.style.display = DisplayStyle.None;
            }).StartingIn(holdMs + LeaveFadeMs);
        }
    }
}

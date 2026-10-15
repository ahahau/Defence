using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 영업 HUD(준비 카드·진행 막대·시작 배너·시작 버튼·목표 전환). 판단은 <c>WaveView</c>가 하고
    /// 이 화면은 <see cref="Render"/>로 받은 값을 그리고 버튼 입력만 올려 보낸다.
    ///
    /// HUD와 같은 공용 패널에 뜬다. uGUI 튜토리얼 덮개보다 아래여야 덮개의 구멍으로만 시작 버튼이 눌린다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class WaveHudToolkitView : MonoBehaviour
    {
        public const string ScreenResourcePath = "UI/WaveHudScreen";
        public const string RootName = "WaveHudToolkitUi";

        private const string ShownClass = "is-shown";
        private const string BossClass = "is-boss";
        private const string HighlightClass = "is-highlighted";
        private const float BannerHoldSeconds = 1.5f;

        private VisualElement _prepCard;
        private Label _prepTitle;
        private Label _prepBody;
        private VisualElement _progressCard;
        private Label _progressTitle;
        private Label _progressStats;
        private VisualElement _progressFill;
        private VisualElement _banner;
        private Label _bannerTitle;
        private Label _bannerSubtitle;
        private Button _startButton;
        private Label _startLabel;
        private Button _objectiveButton;
        private float _bannerHideAt;

        public event Action StartClicked;
        public event Action ObjectiveClicked;

        /// <summary>시작 버튼의 화면 사각형(왼쪽 아래 원점 픽셀). 튜토리얼이 비춘다. 숨었으면 false.</summary>
        public bool TryGetStartButtonScreenRect(out Rect rect) => ToolkitScreenRect.TryGet(_startButton, out rect);

        public void Render(in WaveHudViewModel model)
        {
            if (_startButton == null)
                return;

            SetShown(_startButton, model.StartVisible);
            _startButton.SetEnabled(model.StartEnabled);
            _startButton.EnableInClassList(HighlightClass, model.StartHighlighted);
            _startLabel.text = model.StartLabel ?? string.Empty;

            SetShown(_objectiveButton, model.ObjectiveVisible);
            _objectiveButton.text = model.ObjectiveLabel ?? string.Empty;

            SetShown(_prepCard, model.PreparationVisible);
            _prepTitle.text = model.PreparationTitle ?? string.Empty;
            _prepBody.text = model.PreparationBody ?? string.Empty;

            SetShown(_progressCard, model.ProgressVisible);
            _progressTitle.text = model.ProgressTitle ?? string.Empty;
            _progressStats.text = model.ProgressStats ?? string.Empty;
            _progressFill.style.width = Length.Percent(Mathf.Clamp01(model.Progress) * 100f);
            _progressFill.EnableInClassList(BossClass, model.ProgressIsBoss);
        }

        /// <summary>영업이 시작될 때 가운데 배너를 잠깐 띄운다. 실제 시간으로 재서 배속·일시정지와 무관하게 사라진다.</summary>
        public void PlayBanner(string title, string subtitle)
        {
            if (_banner == null)
                return;

            _bannerTitle.text = title;
            _bannerSubtitle.text = subtitle;
            _banner.AddToClassList(ShownClass);
            _bannerHideAt = Time.unscaledTime + BannerHoldSeconds;
        }

        private void Update()
        {
            if (_bannerHideAt <= 0f || Time.unscaledTime < _bannerHideAt)
                return;

            _bannerHideAt = 0f;
            _banner?.RemoveFromClassList(ShownClass);
        }

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _prepCard = root.Q<VisualElement>("wave-prep-card");
            _prepTitle = root.Q<Label>("wave-prep-title");
            _prepBody = root.Q<Label>("wave-prep-body");
            _progressCard = root.Q<VisualElement>("wave-progress-card");
            _progressTitle = root.Q<Label>("wave-progress-title");
            _progressStats = root.Q<Label>("wave-progress-stats");
            _progressFill = root.Q<VisualElement>("wave-progress-fill");
            _banner = root.Q<VisualElement>("wave-banner");
            _bannerTitle = root.Q<Label>("wave-banner-title");
            _bannerSubtitle = root.Q<Label>("wave-banner-subtitle");
            _startButton = root.Q<Button>("wave-start-button");
            _startLabel = root.Q<Label>("wave-start-label");
            _objectiveButton = root.Q<Button>("wave-objective-button");

            if (_startButton != null)
            {
                _startButton.clicked += HandleStartClicked;
                // 지도 위에 떠 있어서 등록하지 않으면 버튼을 누를 때 아래 방까지 눌린다.
                ToolkitPointerBlockers.Register(_startButton);
            }

            if (_objectiveButton != null)
            {
                _objectiveButton.clicked += HandleObjectiveClicked;
                ToolkitPointerBlockers.Register(_objectiveButton);
            }

            Render(default);
        }

        private void OnDisable()
        {
            if (_startButton != null)
            {
                _startButton.clicked -= HandleStartClicked;
                ToolkitPointerBlockers.Unregister(_startButton);
            }

            if (_objectiveButton != null)
            {
                _objectiveButton.clicked -= HandleObjectiveClicked;
                ToolkitPointerBlockers.Unregister(_objectiveButton);
            }

            _bannerHideAt = 0f;
        }

        private void HandleStartClicked() => StartClicked?.Invoke();
        private void HandleObjectiveClicked() => ObjectiveClicked?.Invoke();

        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

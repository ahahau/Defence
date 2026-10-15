using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 사건·튜토리얼 대화 상자(UI Toolkit). 진행 판단은 <c>DialogueRunner</c>·<c>DialogueView</c>가 하고
    /// 이 화면은 받은 값을 그리고 버튼 입력만 올려 보낸다.
    ///
    /// 팝업 패널(uGUI 위)에 뜬다. 사건이 뜨는 동안 다른 화면 위에 있어야 하고, 튜토리얼 강조의
    /// 어둠도 uGUI까지 덮어야 하기 때문이다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DialogueToolkitView : MonoBehaviour
    {
        public const string ScreenResourcePath = "UI/DialogueScreen";
        public const string RootName = "DialogueToolkitUi";

        private const string OpenClass = "is-open";
        private const string BlockingClass = "is-blocking";
        private const string ShownClass = "is-shown";
        private const string ChoiceClass = "dialogue-choice";
        private const string ChoiceCompactClass = "is-compact";
        private const string ChoiceEnteringClass = "is-entering";
        private const string ChoiceTextClass = "dialogue-choice-text";
        private const string ChoiceSummaryClass = "dialogue-choice-summary";
        private const long ChoiceStaggerMilliseconds = 45;

        /// <summary>초당 글자 수. 한국어는 한 글자가 크므로 영문 기준보다 느리게 잡는다.</summary>
        private const float RevealCharsPerSecond = 45f;

        private VisualElement _root;
        private VisualElement _scrim;
        private VisualElement _spotlight;
        private readonly VisualElement[] _dims = new VisualElement[4];
        private VisualElement _box;
        private Label _speaker;
        private Label _title;
        private Label _progress;
        private Label _body;
        private VisualElement _choices;
        private Button _next;
        private Button _close;
        private readonly List<Button> _choiceButtons = new();

        private string _fullBody = string.Empty;
        private int _visibleCharacters;
        private int _totalCharacters;
        private float _revealProgress;

        public event Action NextClicked;
        public event Action CloseClicked;
        public event Action<int> ChoiceClicked;

        public bool IsOpen => _root != null && _root.ClassListContains(OpenClass);

        /// <summary>본문이 아직 흘러나오는 중인가.</summary>
        public bool IsRevealing => _visibleCharacters < _totalCharacters;

        public void Show(string title, string speaker, string body, string progress, IReadOnlyList<DialogueChoiceViewModel> choices)
        {
            if (_root == null)
                return;

            _root.AddToClassList(OpenClass);
            _title.text = title ?? string.Empty;
            _speaker.text = speaker ?? string.Empty;
            _progress.text = progress ?? string.Empty;
            StartReveal(body ?? string.Empty);
            BuildChoices(choices);

            var hasChoices = choices != null && choices.Count > 0;
            _next.style.display = hasChoices ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (_root == null)
                return;

            CompleteReveal();
            ClearChoices();
            _root.RemoveFromClassList(OpenClass);
            HideSpotlight();
        }

        /// <summary>흘러나오던 본문을 끝까지 보여 준다.</summary>
        public void CompleteReveal()
        {
            _visibleCharacters = _totalCharacters;
            if (_body != null)
                _body.text = _fullBody;
        }

        /// <summary>뒤의 화면을 덮고 클릭을 막는다(사건). 튜토리얼 대사처럼 손을 써야 하는 때는 끈다.</summary>
        public void SetBackgroundBlocking(bool blocking) => _scrim?.EnableInClassList(BlockingClass, blocking);

        public void SetNextVisible(bool visible)
        {
            if (_next != null)
                _next.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetCloseVisible(bool visible)
        {
            if (_close != null)
                _close.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }

        /// <summary>
        /// 화면 사각형(왼쪽 아래 원점 픽셀) 하나만 남기고 어둡게 덮는다. 덮는 동안은 뒤 화면 막기를 끈다 —
        /// 강조한 자리를 눌러야 다음으로 가기 때문이다.
        /// </summary>
        public void SetSpotlightScreenRect(Rect screenRect, float padding)
        {
            if (_spotlight == null || _root.panel == null)
                return;

            var panel = _root.panel.visualTree.worldBound;
            if (panel.width <= 1f || panel.height <= 1f)
                return;

            var scaleX = panel.width / Screen.width;
            var scaleY = panel.height / Screen.height;
            var left = Mathf.Clamp((screenRect.xMin - padding) * scaleX, 0f, panel.width);
            var right = Mathf.Clamp((screenRect.xMax + padding) * scaleX, 0f, panel.width);
            var top = Mathf.Clamp((Screen.height - screenRect.yMax - padding) * scaleY, 0f, panel.height);
            var bottom = Mathf.Clamp((Screen.height - screenRect.yMin + padding) * scaleY, 0f, panel.height);

            Place(_dims[0], 0f, 0f, panel.width, top);
            Place(_dims[1], 0f, bottom, panel.width, panel.height - bottom);
            Place(_dims[2], 0f, top, left, bottom - top);
            Place(_dims[3], right, top, panel.width - right, bottom - top);
            _scrim.RemoveFromClassList(BlockingClass);
            _spotlight.AddToClassList(ShownClass);
        }

        public void HideSpotlight() => _spotlight?.RemoveFromClassList(ShownClass);

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("dialogue-root");
            if (_root == null)
                return;

            _scrim = _root.Q<VisualElement>("dialogue-scrim");
            _spotlight = _root.Q<VisualElement>("dialogue-spotlight");
            _dims[0] = _root.Q<VisualElement>("dialogue-dim-top");
            _dims[1] = _root.Q<VisualElement>("dialogue-dim-bottom");
            _dims[2] = _root.Q<VisualElement>("dialogue-dim-left");
            _dims[3] = _root.Q<VisualElement>("dialogue-dim-right");
            _box = _root.Q<VisualElement>("dialogue-box");
            _speaker = _root.Q<Label>("dialogue-speaker");
            _title = _root.Q<Label>("dialogue-title");
            _progress = _root.Q<Label>("dialogue-progress");
            _body = _root.Q<Label>("dialogue-body");
            _choices = _root.Q<VisualElement>("dialogue-choices");
            _next = _root.Q<Button>("dialogue-next");
            _close = _root.Q<Button>("dialogue-close");

            _next.clicked += HandleNext;
            _close.clicked += HandleClose;
            // 대화 상자를 누를 때 아래 방까지 눌리지 않게 한다. 덮개와 어둠은 보일 때만 막는다.
            ToolkitPointerBlockers.Register(_box);
            ToolkitPointerBlockers.Register(_scrim);
            foreach (var dim in _dims)
                ToolkitPointerBlockers.Register(dim);
        }

        private void OnDisable()
        {
            if (_root == null)
                return;

            _next.clicked -= HandleNext;
            _close.clicked -= HandleClose;
            ToolkitPointerBlockers.Unregister(_box);
            ToolkitPointerBlockers.Unregister(_scrim);
            foreach (var dim in _dims)
                ToolkitPointerBlockers.Unregister(dim);
            ClearChoices();
        }

        private void Update()
        {
            if (!IsRevealing)
                return;

            // 대화는 배속·일시정지와 무관하게 같은 속도로 읽혀야 한다.
            _revealProgress += RevealCharsPerSecond * Time.unscaledDeltaTime;
            var visible = Mathf.Min(_totalCharacters, Mathf.FloorToInt(_revealProgress));
            if (visible == _visibleCharacters)
                return;

            _visibleCharacters = visible;
            _body.text = _visibleCharacters >= _totalCharacters ? _fullBody : Truncate(_fullBody, _visibleCharacters);
        }

        private void HandleNext() => NextClicked?.Invoke();
        private void HandleClose() => CloseClicked?.Invoke();

        private void StartReveal(string body)
        {
            _fullBody = body;
            _totalCharacters = CountVisibleCharacters(body);
            _visibleCharacters = 0;
            _revealProgress = 0f;
            _body.text = string.Empty;
        }

        private void BuildChoices(IReadOnlyList<DialogueChoiceViewModel> choices)
        {
            ClearChoices();
            if (choices == null)
                return;

            for (var i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var index = i;
                var button = new Button(() => ChoiceClicked?.Invoke(index));
                button.AddToClassList("btn");
                button.AddToClassList(ChoiceClass);
                button.AddToClassList(ChoiceEnteringClass);
                // 결과 요약이 없는 선택지("아무것도 하지 않음")는 한 줄 높이로 줄인다. 셋이 들어가야 한다.
                button.EnableInClassList(ChoiceCompactClass, string.IsNullOrWhiteSpace(choice.Summary));

                var text = new Label(choice.Text);
                text.AddToClassList(ChoiceTextClass);
                text.pickingMode = PickingMode.Ignore;
                button.Add(text);

                if (!string.IsNullOrWhiteSpace(choice.Summary))
                {
                    var summary = new Label(choice.Summary);
                    summary.AddToClassList(ChoiceSummaryClass);
                    summary.pickingMode = PickingMode.Ignore;
                    button.Add(summary);
                }

                button.SetEnabled(choice.Selectable);
                _choices.Add(button);
                _choiceButtons.Add(button);
                // 오른쪽에서 하나씩 미끄러져 들어온다.
                button.schedule.Execute(() => button.RemoveFromClassList(ChoiceEnteringClass))
                    .StartingIn(16 + i * ChoiceStaggerMilliseconds);
            }
        }

        private void ClearChoices()
        {
            foreach (var button in _choiceButtons)
                button.RemoveFromHierarchy();
            _choiceButtons.Clear();
        }

        private static void Place(VisualElement element, float x, float y, float width, float height)
        {
            element.style.left = x;
            element.style.top = y;
            element.style.width = Mathf.Max(0f, width);
            element.style.height = Mathf.Max(0f, height);
        }

        /// <summary>태그(&lt;...&gt;)를 뺀 글자 수.</summary>
        private static int CountVisibleCharacters(string text)
        {
            var count = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '<')
                {
                    var close = text.IndexOf('>', i);
                    if (close > i)
                    {
                        i = close;
                        continue;
                    }
                }

                count++;
            }

            return count;
        }

        /// <summary>앞에서부터 보이는 글자 <paramref name="visible"/>개까지 자른다. 태그는 글자로 세지 않고 그대로 남긴다.</summary>
        private static string Truncate(string text, int visible)
        {
            var count = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '<')
                {
                    var close = text.IndexOf('>', i);
                    if (close > i)
                    {
                        i = close;
                        continue;
                    }
                }

                if (count == visible)
                    return text.Substring(0, i);

                count++;
            }

            return text;
        }
    }
}

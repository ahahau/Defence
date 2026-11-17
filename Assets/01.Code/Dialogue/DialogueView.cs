using System;
using System.Collections.Generic;
using Code.UI.ViewModels;
using UnityEngine;

namespace Code.Dialogue
{
    /// <summary>
    /// <see cref="DialogueRunner"/>와 대화 상자 화면 사이의 다리. 표시 값(<see cref="Current"/>)을 만들어
    /// <see cref="Changed"/>로 알리고, 버튼 입력은 <see cref="Next"/>·<see cref="Close"/>·<see cref="Choose"/>로 진행기에 돌려준다.
    /// 그리는 화면은 <c>Code.UI.DialogueUiView</c>(uGUI)다.
    ///
    /// 씬의 UI 판단 오브젝트에 붙어 <c>DialogueRunner.view</c>로 배선된다.
    /// </summary>
    public class DialogueView : MonoBehaviour
    {
        [SerializeField, Tooltip("선택지가 있을 때 본문 끝에 덧붙이는 안내. 비우면 붙이지 않는다.")]
        private string choiceLeadText = string.Empty;

        private DialogueRunner runner;
        private readonly List<DialogueChoiceViewModel> _choices = new();

        /// <summary>지금 떠 있어야 할 대화. 숨었으면 <see cref="IsVisible"/>이 false.</summary>
        public DialogueScreenState Current { get; private set; }

        public bool IsVisible { get; private set; }

        /// <summary>새 대사를 띄울 때마다 오른다. 화면은 이 값이 바뀌면 글 흘리기를 처음부터 다시 한다.</summary>
        public int Version { get; private set; }

        public IReadOnlyList<DialogueChoiceViewModel> Choices => _choices;

        /// <summary>표시 값이나 버튼·강조 상태가 바뀌었다.</summary>
        public event Action Changed;

        public void Initialize(DialogueRunner dialogueRunner)
        {
            runner = dialogueRunner;
        }

        public void Show(DialogueDisplayData data)
        {
            _choices.Clear();
            foreach (var choice in data.Choices)
            {
                var canSelect = runner == null || runner.CanSelect(choice);
                _choices.Add(new DialogueChoiceViewModel(choice.Text, choice.EffectSummary, canSelect));
            }

            var state = Current;
            state.Title = data.Title;
            state.SpeakerName = data.SpeakerName;
            state.Body = BuildBodyText(data);
            state.Progress = data.Progress;
            Current = state;
            IsVisible = true;
            Version++;
            Changed?.Invoke();
        }

        public void SetBackgroundRaycastBlocking(bool blocksRaycasts) =>
            Apply((ref DialogueScreenState s) => s.BlocksBackground = blocksRaycasts);

        public void SetNextButtonVisible(bool visible) => Apply((ref DialogueScreenState s) => s.NextVisible = visible);

        public void SetCloseButtonVisible(bool visible) => Apply((ref DialogueScreenState s) => s.CloseVisible = visible);

        public void Hide()
        {
            if (this == null || !IsVisible)
                return;

            IsVisible = false;
            Changed?.Invoke();
        }

        public void SetSpotlightScreenRect(Rect screenRect, float padding = 32f) =>
            Apply((ref DialogueScreenState s) =>
            {
                s.SpotlightVisible = true;
                s.SpotlightRect = screenRect;
                s.SpotlightPadding = padding;
            });

        public void HideSpotlight() => Apply((ref DialogueScreenState s) => s.SpotlightVisible = false);

        /// <summary>다음 버튼. 글이 흘러나오는 효과는 화면 쪽이 먼저 끝낸 뒤 부른다.</summary>
        public void Next() => runner?.Next();

        public void Close() => runner?.Stop();

        public void Choose(int choiceIndex) => runner?.SelectChoice(choiceIndex);

        private void Apply(UpdateState change)
        {
            var state = Current;
            change(ref state);
            Current = state;
            Changed?.Invoke();
        }

        private delegate void UpdateState(ref DialogueScreenState state);

        private string BuildBodyText(DialogueDisplayData data)
        {
            if (!data.HasChoices || string.IsNullOrWhiteSpace(choiceLeadText))
                return data.Text;

            return $"{data.Text}\n\n<size=92%><color=#B8F0FF>{choiceLeadText}</color></size>";
        }
    }
}

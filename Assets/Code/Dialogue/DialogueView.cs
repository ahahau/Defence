using System.Collections.Generic;
using Code.UI.Toolkit;
using UnityEngine;

namespace Code.Dialogue
{
    /// <summary>
    /// <see cref="DialogueRunner"/>와 대화 상자 화면 사이의 다리. 그리기는 UI Toolkit 화면
    /// (<see cref="DialogueToolkitView"/>)이 하고, 여기서는 표시 값을 만들어 넘기고 버튼 입력을 진행기로 돌려준다.
    ///
    /// 씬의 UI 판단 오브젝트에 붙어 <c>DialogueRunner.view</c>로 배선된다.
    /// </summary>
    public class DialogueView : MonoBehaviour
    {
        [SerializeField, Tooltip("선택지가 있을 때 본문 끝에 덧붙이는 안내. 비우면 붙이지 않는다.")]
        private string choiceLeadText = string.Empty;

        private DialogueRunner runner;
        private DialogueToolkitView _view;
        private readonly List<DialogueChoiceViewModel> _choices = new();

        public void Initialize(DialogueRunner dialogueRunner)
        {
            runner = dialogueRunner;
            EnsureView();
        }

        public void Show(DialogueDisplayData data)
        {
            if (!EnsureView())
                return;

            _choices.Clear();
            foreach (var choice in data.Choices)
            {
                var canSelect = runner == null || runner.CanSelect(choice);
                _choices.Add(new DialogueChoiceViewModel(choice.Text, choice.EffectSummary, canSelect));
            }

            _view.Show(data.Title, data.SpeakerName, BuildBodyText(data), data.Progress, _choices);
        }

        public void SetBackgroundRaycastBlocking(bool blocksRaycasts)
        {
            if (EnsureView())
                _view.SetBackgroundBlocking(blocksRaycasts);
        }

        public void SetNextButtonVisible(bool visible)
        {
            if (EnsureView())
                _view.SetNextVisible(visible);
        }

        public void SetCloseButtonVisible(bool visible)
        {
            if (EnsureView())
                _view.SetCloseVisible(visible);
        }

        public void Hide()
        {
            if (this == null)
                return;

            if (_view != null)
                _view.Hide();
        }

        public void SetSpotlightScreenRect(Rect screenRect, float padding = 32f)
        {
            if (EnsureView())
                _view.SetSpotlightScreenRect(screenRect, padding);
        }

        public void HideSpotlight()
        {
            if (_view != null)
                _view.HideSpotlight();
        }

        private void OnDestroy()
        {
            UnbindView();
            if (_view != null)
                Destroy(_view.gameObject);
            _view = null;
        }

        private void HandleNextClicked()
        {
            // 아직 흘러나오는 중이면 먼저 끝까지 보여 준다. 읽던 문장이 사라지고 다음 줄로
            // 넘어가면 놓친 사람이 되돌릴 방법이 없다. 한 번 더 눌러야 넘어간다.
            if (_view != null && _view.IsRevealing)
            {
                _view.CompleteReveal();
                return;
            }

            runner?.Next();
        }

        private void HandleCloseClicked() => runner?.Stop();

        private void HandleChoiceClicked(int choiceIndex) => runner?.SelectChoice(choiceIndex);

        private string BuildBodyText(DialogueDisplayData data)
        {
            if (!data.HasChoices || string.IsNullOrWhiteSpace(choiceLeadText))
                return data.Text;

            return $"{data.Text}\n\n<size=92%><color=#B8F0FF>{choiceLeadText}</color></size>";
        }

        private bool EnsureView()
        {
            if (_view != null)
                return true;

            _view = ToolkitScreenSO.Spawn<DialogueToolkitView>(DialogueToolkitView.ScreenResourcePath, DialogueToolkitView.RootName);
            if (_view == null)
                return false;

            _view.NextClicked += HandleNextClicked;
            _view.CloseClicked += HandleCloseClicked;
            _view.ChoiceClicked += HandleChoiceClicked;
            return true;
        }

        private void UnbindView()
        {
            if (_view == null)
                return;

            _view.NextClicked -= HandleNextClicked;
            _view.CloseClicked -= HandleCloseClicked;
            _view.ChoiceClicked -= HandleChoiceClicked;
        }
    }
}

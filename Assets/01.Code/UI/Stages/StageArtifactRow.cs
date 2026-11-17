using Code.Stages;
using TMPro;
using UnityEngine;

namespace Code.UI.Stages
{
    /// <summary>유물 창의 한 줄. 무엇을 그릴지와 버튼 문구는 창이 정하고, 이 줄은 누름만 창에 돌려준다.</summary>
    public sealed class StageArtifactRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private UiFrameButton action;
        private StageArtifactWindow _window;
        private StageArtifact _artifact;

        public StageArtifact Artifact => _artifact;

        public void Bind(StageArtifactWindow window)
        {
            Unbind();
            _window = window;
            if (isActiveAndEnabled)
                BindCallbacks();
        }

        public void Show(StageArtifact artifact, string text, string actionLabel, bool interactable, bool selected)
        {
            _artifact = artifact;
            gameObject.SetActive(true);
            label.text = text;
            action.SetLabel(actionLabel);
            action.SetInteractable(interactable);
            action.SetSelected(selected);
        }

        public void Hide()
        {
            _artifact = null;
            gameObject.SetActive(false);
        }

        /// <summary>이 줄이 연결한 콜백만 해제한다.</summary>
        public void Unbind()
        {
            if (action != null && action.Button != null)
                action.Button.onClick.RemoveListener(Press);
        }

        private void BindCallbacks()
        {
            Unbind();
            if (action != null && action.Button != null)
                action.Button.onClick.AddListener(Press);
        }

        private void OnEnable()
        {
            if (_window != null)
                BindCallbacks();
        }

        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();

        private void Press()
        {
            if (_window != null && _artifact != null)
                _window.Choose(_artifact);
        }
    }
}

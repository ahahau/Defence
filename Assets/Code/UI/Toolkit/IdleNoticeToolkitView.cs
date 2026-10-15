using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>방치 시 타이틀 복귀 카운트다운 알림. 세는 일은 <c>IdleReturnToTitle</c>가 한다.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class IdleNoticeToolkitView : MonoBehaviour
    {
        public const string ScreenResourcePath = "UI/IdleNoticeScreen";
        public const string RootName = "IdleNoticeToolkitUi";

        private const string ShownClass = "is-shown";

        private VisualElement _root;
        private Label _text;

        public void SetVisible(bool visible) => _root?.EnableInClassList(ShownClass, visible);

        public void SetText(string text)
        {
            if (_text != null)
                _text.text = text;
        }

        private void OnEnable()
        {
            var document = GetComponent<UIDocument>().rootVisualElement;
            _root = document.Q<VisualElement>("idle-notice-root");
            _text = document.Q<Label>("idle-notice-text");
        }
    }
}

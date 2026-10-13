using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 필요할 때 띄우는 UI Toolkit 화면 하나의 설정. UXML은 Resources 밖(`Assets/GameModules/UI/UxmlAndUss/`)에
    /// 있어 직접 읽을 수 없으므로, 이 SO를 Resources에 두고 UXML·패널 설정을 참조로 들고 있게 한다(팝업과 같은 방식).
    /// </summary>
    [CreateAssetMenu(menuName = "SO/UI/Toolkit Screen", fileName = "ToolkitScreen")]
    public sealed class ToolkitScreenSO : ScriptableObject
    {
        [SerializeField] private VisualTreeAsset template;

        [SerializeField, Tooltip("uGUI 위에 떠야 하면 Sort Order가 높은 패널(예: PopupPanelSettings)을 준다.")]
        private PanelSettings panelSettings;

        [SerializeField, Tooltip("같은 패널 안에서의 문서 순서. 팝업(900)보다 낮게 둔다.")]
        private int sortingOrder = 500;

        public VisualTreeAsset Template => template;
        public PanelSettings PanelSettings => panelSettings;
        public int SortingOrder => sortingOrder;

        /// <summary>
        /// Resources의 설정으로 문서를 지금 씬에 세우고 화면 컴포넌트를 붙여 돌려준다. 설정이 없으면 null.
        /// 문서는 씬과 함께 사라지므로 화면 쪽은 정적 참조를 <c>OnDestroy</c>에서 비워야 한다.
        /// </summary>
        public static TView Spawn<TView>(string resourcePath, string rootName) where TView : Component
        {
            var settings = Resources.Load<ToolkitScreenSO>(resourcePath);
            if (settings == null || settings.Template == null || settings.PanelSettings == null)
            {
                Debug.LogError($"UI Toolkit 화면 설정이 비어 있습니다: Resources/{resourcePath}");
                return null;
            }

            var host = new GameObject(rootName);
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings.PanelSettings;
            document.visualTreeAsset = settings.Template;
            document.sortingOrder = settings.SortingOrder;
            return host.AddComponent<TView>();
        }
    }
}

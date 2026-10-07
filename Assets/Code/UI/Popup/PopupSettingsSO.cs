using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Popup
{
    /// <summary>
    /// 팝업 UXML을 가리키는 설정. UXML은 Resources 밖(GameModules/UI)에 있으므로
    /// 이 에셋만 `Assets/Resources/UI/PopupSettings.asset`에 두고 직렬화 참조로 연결한다.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/UI/Popup Settings", fileName = "PopupSettings")]
    public sealed class PopupSettingsSO : ScriptableObject
    {
        [SerializeField, Tooltip("Assets/GameModules/UI/UxmlAndUss/Shared/Popup/Popup.uxml")]
        private VisualTreeAsset template;

        [SerializeField, Tooltip("팝업 전용 패널. uGUI 캔버스와의 앞뒤는 문서 순서가 아니라 패널의 Sort Order로 정해지므로, " +
                                 "공용 RuntimePanelSettings(0)를 쓰면 메인 캔버스(50) 아래에 깔린다. 비우면 공용 패널을 쓴다.")]
        private PanelSettings panelSettings;

        [SerializeField, Tooltip("같은 패널 안에서의 문서 순서.")]
        private int sortingOrder = 900;

        public VisualTreeAsset Template => template;
        public PanelSettings PanelSettings => panelSettings;
        public int SortingOrder => sortingOrder;
    }
}

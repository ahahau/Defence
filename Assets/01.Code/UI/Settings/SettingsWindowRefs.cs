using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 설정 창 프리팹이 자기 조각들을 가리키는 표.
    ///
    /// 창을 프리팹으로 옮기면 <see cref="SettingsPanelView"/>가 조각들을 다시 찾아야 하는데,
    /// 이름으로 찾으면 에디터에서 이름 한 번 고치는 것만으로 조용히 끊어진다. 그래서 직렬화
    /// 참조로 들고 있는다 — 끊어지면 인스펙터에서 눈에 보이고, 자리를 옮겨도 따라간다.
    ///
    /// 누르는 동작은 여기 담지 않는다. 버튼이 무엇을 하는지는 판의 상태를 아는
    /// <see cref="SettingsPanelView"/>의 몫이고, 이 표는 "어느 것이 어느 것인지"만 말한다.
    /// </summary>
    public class SettingsWindowRefs : MonoBehaviour
    {
        [Header("Roots")]
        public GameObject backdrop;
        public GameObject window;
        public GameObject confirmWindow;

        [Header("Settings Window")]
        public Slider sfxSlider;
        public TMP_Text sfxValueLabel;
        public Slider musicSlider;
        public TMP_Text musicValueLabel;
        public Button restartButton;
        public Button titleButton;
        public Button closeButton;
        public Button guideButton;

        [Header("Game Guide")]
        public GameObject gameGuideWindow;
        public Button guideCloseButton;

        [Header("Restart Confirm")]
        public Button confirmCancelButton;
        public Button confirmAcceptButton;

        /// <summary>표가 다 채워졌는가. 하나라도 비면 창이 반쯤 죽은 채로 뜨므로 미리 본다.</summary>
        public bool IsComplete =>
            backdrop != null && window != null && confirmWindow != null
            && sfxSlider != null && sfxValueLabel != null
            && musicSlider != null && musicValueLabel != null
            && restartButton != null && titleButton != null && closeButton != null
            && guideButton != null && gameGuideWindow != null && guideCloseButton != null
            && confirmCancelButton != null && confirmAcceptButton != null;
    }
}

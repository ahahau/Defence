using Code.Dialogue;
using Code.MapCreateSystem;
using Code.UI.Popup;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Code.UI
{
    /// <summary>
    /// 게임 화면의 ESC를 한 곳에서 받는다. 한 번 누르면 위에 있는 창 하나만 닫고, 닫을 창이 없으면 설정을 연다.
    /// 예전에는 방 관리·정산만 ESC를 받아 상인·고용·상태·금고 창은 ESC로 닫히지 않았고,
    /// 화면마다 따로 받으면 한 번에 둘이 닫힐 수 있었다.
    ///
    /// 팝업이나 건물·함정을 놓는 중이면 그쪽이 ESC를 먼저 쓴다. 사건 대화·운영 방침 창은 골라야 넘어가는 창이라
    /// 떠 있는 동안 ESC를 쓰지 않는다.
    /// </summary>
    public sealed class UiEscapeRouter : MonoBehaviour
    {
        [SerializeField, Tooltip("위에 있는 창부터. 각 항목은 IEscapeClosable이어야 한다.")]
        private MonoBehaviour[] windows;
        [SerializeField] private SettingsScreenView settings;
        [SerializeField] private DialogueView dialogue;
        [SerializeField, Tooltip("운영 방침 창. 골라야 닫히는 창이라 떠 있는 동안 ESC를 쓰지 않는다.")]
        private PolicyChoiceScreenView policyChoice;

        private IEscapeClosable[] _windows;

        private void Awake()
        {
            _windows = new IEscapeClosable[windows != null ? windows.Length : 0];
            for (var i = 0; i < _windows.Length; i++)
            {
                _windows[i] = windows[i] as IEscapeClosable;
                if (windows[i] != null && _windows[i] == null)
                    Debug.LogError($"{nameof(UiEscapeRouter)}: {windows[i].name}의 {windows[i].GetType().Name}은 {nameof(IEscapeClosable)}이 아닙니다.", this);
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || PopupController.BlocksEscape)
                return;

            if (BuildingPlacementPreview.WasActiveThisFrame || EdgePlacementPreview.WasActiveThisFrame)
                return;

            if (dialogue != null && dialogue.IsVisible)
                return;

            if (policyChoice != null && policyChoice.IsOpen)
                return;

            for (var i = 0; i < _windows.Length; i++)
            {
                var window = _windows[i];
                if (window == null || !window.IsOpen)
                    continue;

                window.Close();
                return;
            }

            settings?.Open();
        }
    }
}

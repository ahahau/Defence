using System;
using System.Collections.Generic;
using Code.Audio;
using Code.Manager;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.UI.Popup
{
    /// <summary>
    /// 범용 팝업의 진입점. <c>PopupController.Show(PopupRequest.Confirm(...))</c>처럼 쓴다.
    ///
    /// 한 번에 하나만 띄우고 나머지는 도착 순서대로 기다린다. 두 팝업이 겹치면 아래 것이
    /// 눌리지 않는 채로 남기 때문이다. 처음 부를 때 스스로 생겨 씬을 넘어 유지하고,
    /// 씬이 바뀌면 대기 중인 요청을 버린다(콜백이 사라진 씬의 객체를 가리킬 수 있어서).
    ///
    /// 그리는 화면은 지금 없다(uGUI 전환 대기). 화면은 <see cref="Changed"/>를 듣고 <see cref="Current"/>를 그리며,
    /// 버튼을 누르면 <see cref="Choose"/>를 부른다. 화면이 없는 동안에는 대기열과 시간 정지만 돈다.
    /// </summary>
    public sealed class PopupController : MonoBehaviour
    {
        private static PopupController _instance;

        private readonly Queue<PopupRequest> _pending = new();
        private PopupRequest _current;
        private bool _pausedGame;
        private static int _escapeHandledFrame = -1;

        /// <summary>팝업이 떠 있는가.</summary>
        public static bool IsOpen => _instance != null && _instance._current != null;

        /// <summary>지금 떠 있는 팝업. 없으면 null.</summary>
        public static PopupRequest Current => _instance != null ? _instance._current : null;

        /// <summary>떠 있는 팝업이 바뀌었다(열림·닫힘·다음 팝업).</summary>
        public static event Action Changed;

        /// <summary>
        /// 이번 프레임의 ESC를 팝업이 가져가는가. ESC를 읽는 다른 화면은 이 값이 참이면 양보한다.
        /// 팝업이 같은 프레임에 ESC로 먼저 닫혔어도 참이다 — 그러지 않으면 Update 순서에 따라
        /// 팝업을 닫은 같은 키가 뒤의 창까지 닫는다.
        /// </summary>
        public static bool BlocksEscape => IsOpen || _escapeHandledFrame == Time.frameCount;

        /// <summary>지금 떠 있는 팝업이 이 이름(<see cref="PopupRequest.WithTag"/>)인가.</summary>
        public static bool IsShowing(string tag) =>
            IsOpen && !string.IsNullOrEmpty(tag) && _instance._current.Tag == tag;

        private static ScreenRectSource _confirmButtonRect;

        /// <summary>팝업을 그리는 화면이 확인 버튼 위치를 알려 줄 통로를 건다. 화면이 사라지면 null로 푼다.</summary>
        public static void SetConfirmButtonRectSource(ScreenRectSource source) => _confirmButtonRect = source;

        /// <summary>떠 있는 팝업의 확인 버튼 화면 좌표(픽셀, 왼쪽 아래 원점). 튜토리얼 강조용. 그리는 화면이 없으면 false.</summary>
        public static bool TryGetConfirmButtonScreenRect(out Rect rect)
        {
            rect = default;
            return IsOpen && _instance._current.ConfirmButtonIndex >= 0 && _confirmButtonRect != null && _confirmButtonRect(out rect);
        }

        /// <summary>팝업을 띄운다. 이미 떠 있으면 대기열 뒤에 선다.</summary>
        public static bool Show(PopupRequest request)
        {
            if (request == null)
                return false;

            PopupController controller = _instance != null ? _instance : Create();
            controller._pending.Enqueue(request);
            if (controller._current == null)
                controller.ShowNext();
            return true;
        }

        /// <summary>떠 있는 팝업의 버튼을 누른다. 잠긴 버튼이면 실패음만 내고 닫지 않는다.</summary>
        public static void Choose(int index)
        {
            if (_instance != null)
                _instance.HandleButton(index);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            ReleasePause();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (_current == null || !WasCancelPressed())
                return;

            _escapeHandledFrame = Time.frameCount;
            if (_current.CanCancel)
                Close(_current.OnCancel, GameSfxCue.UiClose);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _pending.Clear();
            _current = null;
            ReleasePause();
            Changed?.Invoke();
        }

        private static PopupController Create()
        {
            var host = new GameObject(nameof(PopupController));
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<PopupController>();
            return _instance;
        }

        private void ShowNext()
        {
            if (_pending.Count == 0)
            {
                _current = null;
                ReleasePause();
                Changed?.Invoke();
                return;
            }

            _current = _pending.Dequeue();
            ApplyPause(_current.PausesGame);
            GameSfxPlayer.Play(GameSfxCue.UiOpen);
            Changed?.Invoke();
        }

        private void HandleButton(int index)
        {
            if (_current == null || index < 0 || index >= _current.Buttons.Count)
                return;

            PopupButtonSpec spec = _current.Buttons[index];
            if (!spec.Interactable)
            {
                GameSfxPlayer.Play(GameSfxCue.UiFail);
                return;
            }

            GameSfxCue cue = spec.Style == PopupButtonStyle.Secondary ? GameSfxCue.UiClose : GameSfxCue.UiConfirm;
            Close(spec.OnClick, cue);
        }

        // 콜백은 팝업을 닫은 뒤에 부른다. 콜백 안에서 다음 팝업을 띄워도 대기열 순서가 꼬이지 않는다.
        private void Close(Action callback, GameSfxCue cue)
        {
            GameSfxPlayer.Play(cue);
            _current = null;
            ShowNext();
            callback?.Invoke();
        }

        private void ApplyPause(bool pause)
        {
            if (pause && !_pausedGame)
            {
                GameSpeedController.Current?.Suspend(this);
                _pausedGame = GameSpeedController.Current != null;
            }
            else if (!pause)
            {
                ReleasePause();
            }
        }

        private void ReleasePause()
        {
            if (!_pausedGame)
                return;

            GameSpeedController.Current?.Release(this);
            _pausedGame = false;
        }

        private static bool WasCancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _escapeHandledFrame = -1;
            Changed = null;
            _confirmButtonRect = null;
        }
    }
}

using System;
using System.Collections.Generic;
using Code.Audio;
using Code.Manager;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Code.UI.Popup
{
    /// <summary>
    /// 범용 팝업의 진입점. <c>PopupController.Show(PopupRequest.Confirm(...))</c>처럼 쓴다.
    ///
    /// 한 번에 하나만 띄우고 나머지는 도착 순서대로 기다린다. 두 팝업이 겹치면 아래 것이
    /// 눌리지 않는 채로 남기 때문이다. 처음 부를 때 스스로 문서를 만들어 씬을 넘어 유지하고,
    /// 씬이 바뀌면 대기 중인 요청을 버린다(콜백이 사라진 씬의 객체를 가리킬 수 있어서).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PopupController : MonoBehaviour
    {
        private const string SettingsResourcePath = "UI/PopupSettings";
        private const string PanelSettingsResourcePath = "UI/Toolkit/RuntimePanelSettings";

        private static PopupController _instance;

        private readonly Queue<PopupRequest> _pending = new();
        private PopupView _view;
        private PopupRequest _current;
        private bool _pausedGame;
        private static int _escapeHandledFrame = -1;

        /// <summary>팝업이 떠 있는가.</summary>
        public static bool IsOpen => _instance != null && _instance._current != null;

        /// <summary>
        /// 이번 프레임의 ESC를 팝업이 가져가는가. ESC를 읽는 다른 화면은 이 값이 참이면 양보한다.
        /// 팝업이 같은 프레임에 ESC로 먼저 닫혔어도 참이다 — 그러지 않으면 Update 순서에 따라
        /// 팝업을 닫은 같은 키가 뒤의 창까지 닫는다.
        /// </summary>
        public static bool BlocksEscape => IsOpen || _escapeHandledFrame == Time.frameCount;

        /// <summary>지금 떠 있는 팝업이 이 이름(<see cref="PopupRequest.WithTag"/>)인가.</summary>
        public static bool IsShowing(string tag) =>
            IsOpen && !string.IsNullOrEmpty(tag) && _instance._current.Tag == tag;

        /// <summary>떠 있는 팝업의 확인 버튼 화면 좌표(픽셀, 왼쪽 아래 원점). 튜토리얼 강조용.</summary>
        public static bool TryGetConfirmButtonScreenRect(out Rect rect)
        {
            rect = default;
            return IsOpen && _instance._current.ConfirmButtonIndex >= 0
                          && _instance._view.TryGetButtonScreenRect(_instance._current.ConfirmButtonIndex, out rect);
        }

        /// <summary>팝업을 띄운다. 이미 떠 있으면 대기열 뒤에 선다. 만들 수 없으면 false.</summary>
        public static bool Show(PopupRequest request)
        {
            if (request == null)
                return false;

            PopupController controller = _instance != null ? _instance : Create();
            if (controller == null)
                return false;

            controller._pending.Enqueue(request);
            if (controller._current == null)
                controller.ShowNext();
            return true;
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
            _view?.Hide();
        }

        private static PopupController Create()
        {
            var settings = Resources.Load<PopupSettingsSO>(SettingsResourcePath);
            var panelSettings = settings != null && settings.PanelSettings != null
                ? settings.PanelSettings
                : Resources.Load<PanelSettings>(PanelSettingsResourcePath);
            if (settings == null || settings.Template == null || panelSettings == null)
            {
                Debug.LogError($"팝업을 만들 수 없습니다. Resources/{SettingsResourcePath}.asset과 그 Template, " +
                               $"Resources/{PanelSettingsResourcePath}를 확인하세요.");
                return null;
            }

            var host = new GameObject(nameof(PopupController));
            DontDestroyOnLoad(host);
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = settings.Template;
            document.sortingOrder = settings.SortingOrder;

            var controller = host.AddComponent<PopupController>();
            controller._view = new PopupView(document.rootVisualElement);
            if (!controller._view.IsValid)
            {
                Debug.LogError("Popup.uxml에서 필요한 요소를 찾지 못했습니다. PopupUiNames와 UXML 이름을 맞추세요.");
                Destroy(host);
                return null;
            }

            controller._view.Hide();
            _instance = controller;
            return controller;
        }

        private void ShowNext()
        {
            if (_pending.Count == 0)
            {
                _current = null;
                _view.Hide();
                ReleasePause();
                return;
            }

            _current = _pending.Dequeue();
            _view.Show(_current, HandleButton);
            ApplyPause(_current.PausesGame);
            GameSfxPlayer.Play(GameSfxCue.UiOpen);
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
    }
}

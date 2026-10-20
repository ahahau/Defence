using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Code.Core
{
    /// <summary>
    /// Input System 콜백을 게임 코드가 읽기 쉬운 상태와 이벤트로 바꾸는 ScriptableObject.
    /// 카메라는 씬마다 달라질 수 있어 실행 중에 명시적으로 주입한다.
    /// </summary>
    [CreateAssetMenu(fileName = "SO/InputSystem", menuName = "InputSO", order = 0)]
    public class InputDataSO : ScriptableObject, Controls.IPlayerActions
    {
        public event Action OnMouseInputEvent;
        
        public Vector2 WorldMousePosition { get; private set; }

        public Vector2 ScreenMousePosition { get; private set; }
        
        public Vector2 MovementKey { get; private set; }
        
        private Controls _controls;
        private Camera _worldCamera;

        public void SetWorldCamera(Camera worldCamera)
        {
            // Start 이전에 호출될 수 있으므로 카메라 참조만 저장하고 좌표 계산은 요청 시 한다.
            _worldCamera = worldCamera;
        }

        private void OnEnable()
        {
            if (_controls == null)
            {
                _controls = new Controls();
                _controls.Player.SetCallbacks(this);
            }
            _controls.Player.Enable();
        }

        private void OnDisable()
        {
            if (_controls == null)
                return;

            _controls.Player.Disable();
        }
        public void OnMove(InputAction.CallbackContext context)
        {
            Vector2 movementKey = context.ReadValue<Vector2>();
            MovementKey = movementKey;
        }

        public void OnMouseInput(InputAction.CallbackContext context)
        {
            if (context.performed)
                OnMouseInputEvent?.Invoke();
        }

        public Vector2 SceneToWorldPoint()
        {
            // 테스트나 타이틀 씬처럼 카메라가 없는 경우에는 마지막으로 계산한 값을 폴백으로 쓴다.
            if (_worldCamera == null)
                return WorldMousePosition;

            Vector3 mousePos = ReadScreenMousePosition();
            mousePos.z = 0;
            WorldMousePosition = _worldCamera.ScreenToWorldPoint(mousePos);
            return WorldMousePosition;
        }

        public Vector2 ReadScreenMousePosition()
        {
            // 입력 장치는 매 프레임 바뀔 수 있으므로 캐시 대신 호출 시점 값을 읽는다.
            ScreenMousePosition = Mouse.current.position.ReadValue();
            return ScreenMousePosition;
        }
    }
}

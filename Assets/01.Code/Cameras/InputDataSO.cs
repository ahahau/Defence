using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _01.Code.Cameras
{
    [CreateAssetMenu(fileName = "CameraInput", menuName = "SO/Camera/Input", order = 0)]
    public class InputDataSO : ScriptableObject, Controls.IPlayerActions
    {
        public Vector2 MovementKey { get; private set; }
        public Vector2 MousePosition { get; private set; }
        public bool IsLeftPointerPressed { get; private set; }

        private Controls _controls;

        public event Action LeftPointerPressed;
        public event Action LeftPointerReleased;
        public event Action RightPointerPressed;

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
            _controls.Player.Disable();
        }

        public Vector2 GetWorldPosition2D()
        {
            Camera mainCam = Camera.main;
            Debug.Assert(mainCam != null, "No main camera in this scene");

            Vector3 screenPoint = new Vector3(MousePosition.x, MousePosition.y, Mathf.Abs(mainCam.transform.position.z));
            Vector3 worldPos = mainCam.ScreenToWorldPoint(screenPoint);
            return new Vector2(worldPos.x, worldPos.y);
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            MovementKey = context.ReadValue<Vector2>();
        }

        public void OnPoint(InputAction.CallbackContext context)
        {
            MousePosition = context.ReadValue<Vector2>();
        }

        public void OnClick(InputAction.CallbackContext context)
        {
            bool isPressed = context.ReadValueAsButton();

            if (isPressed && !IsLeftPointerPressed)
            {
                IsLeftPointerPressed = true;
                LeftPointerPressed?.Invoke();
                return;
            }

            if (!isPressed && IsLeftPointerPressed)
            {
                IsLeftPointerPressed = false;
                LeftPointerReleased?.Invoke();
            }
        }

        public void OnRightClick(InputAction.CallbackContext context)
        {
            if (context.ReadValueAsButton())
                RightPointerPressed?.Invoke();
        }
    }
}

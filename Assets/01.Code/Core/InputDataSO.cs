using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _01.Code.Core
{
    [CreateAssetMenu(fileName = "SO/InputSystem", menuName = "InputSO", order = 0)]
    public class InputDataSO : ScriptableObject, Controls.IPlayerActions
    {
        public event Action OnMouseInputEvent;
        
        public Vector2 WorldMousePosition { get; private set; }

        public Vector2 ScreenMousePosition { get; private set; }
        
        public Vector2 MovementKey { get; private set; }
        
        private Controls _controls;
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
            Vector3 mousePos = ReadScreenMousePosition();
            mousePos.z = 0;
            WorldMousePosition = Camera.main.ScreenToWorldPoint(mousePos);
            return WorldMousePosition;
        }

        public Vector2 ReadScreenMousePosition()
        {
            ScreenMousePosition = Mouse.current.position.ReadValue();
            return ScreenMousePosition;
        }
    }
}

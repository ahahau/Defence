using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using NotImplementedException = System.NotImplementedException;

namespace _01.Code.Cameras
{
    [CreateAssetMenu(fileName = "CameraInput", menuName = "SO/Camera/Input", order = 0)]
    public class CameraInputSO : ScriptableObject, Controls.IPlayerActions
    {
        public Vector2 MovementKey { get; private set; }
        public Vector2 MousePosition { get; private set; }
        private Controls _controls;
        
        public event Action<Vector2> OnMouseClicked;
        
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

            Vector3 worldPos = mainCam.ScreenToWorldPoint(MousePosition);
            return worldPos;
        }
        public void OnMove(InputAction.CallbackContext context)
        {
            Vector2 movementKey = context.ReadValue<Vector2>();
            MovementKey = movementKey;
        }

        public void OnPoint(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnMouseClicked?.Invoke(GetWorldPosition2D());
        }
    }
}
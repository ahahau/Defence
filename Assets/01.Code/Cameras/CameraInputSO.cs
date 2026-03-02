using UnityEngine;
using UnityEngine.InputSystem;

namespace _01.Code.Cameras
{
    [CreateAssetMenu(fileName = "CameraInput", menuName = "SO/Camera/Input", order = 0)]
    public class CameraInputSO : ScriptableObject, Controls.IPlayerActions
    {
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
            _controls.Player.Disable();
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            Vector2 movementKey = context.ReadValue<Vector2>();
            MovementKey = movementKey;
        }
    }
}
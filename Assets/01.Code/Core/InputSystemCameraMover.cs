using UnityEngine;
using UnityEngine.InputSystem;

namespace _01.Code.Core
{
    public class InputSystemCameraMover : MonoBehaviour
    {
        [SerializeField] private InputDataSO inputData;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private bool useUnscaledTime = true;

        private Controls _controls;
        private Vector2 _directMoveInput;

        private void OnEnable()
        {
            if (inputData != null)
                return;

            _controls ??= new Controls();
            _controls.Player.Move.performed += HandleMove;
            _controls.Player.Move.canceled += HandleMove;
            _controls.Player.Enable();
        }

        private void OnDisable()
        {
            if (_controls == null)
                return;

            _controls.Player.Move.performed -= HandleMove;
            _controls.Player.Move.canceled -= HandleMove;
            _controls.Player.Disable();
        }

        private void Update()
        {
            var moveInput = inputData != null ? inputData.MovementKey : _directMoveInput;
            if (moveInput.sqrMagnitude <= Mathf.Epsilon)
                return;

            if (moveInput.sqrMagnitude > 1f)
                moveInput.Normalize();

            var deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            var delta = new Vector3(moveInput.x, moveInput.y, 0f) * (moveSpeed * deltaTime);
            transform.position += delta;
        }

        private void HandleMove(InputAction.CallbackContext context)
        {
            _directMoveInput = context.ReadValue<Vector2>();
        }
    }
}

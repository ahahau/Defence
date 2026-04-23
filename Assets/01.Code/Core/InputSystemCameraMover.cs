using UnityEngine;
using UnityEngine.InputSystem;

namespace _01.Code.Core
{
    public class InputSystemCameraMover : MonoBehaviour
    {
        [SerializeField] private InputDataSO inputData;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private bool useUnscaledTime = true;

        private Controls controls;
        private Vector2 directMoveInput;

        private void OnEnable()
        {
            if (inputData != null)
                return;

            controls ??= new Controls();
            controls.Player.Move.performed += HandleMove;
            controls.Player.Move.canceled += HandleMove;
            controls.Player.Enable();
        }

        private void OnDisable()
        {
            if (controls == null)
                return;

            controls.Player.Move.performed -= HandleMove;
            controls.Player.Move.canceled -= HandleMove;
            controls.Player.Disable();
        }

        private void Update()
        {
            var moveInput = inputData != null ? inputData.MovementKey : directMoveInput;
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
            directMoveInput = context.ReadValue<Vector2>();
        }
    }
}

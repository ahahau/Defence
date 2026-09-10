using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace _01.Code.Core
{
    public class InputSystemCameraMover : MonoBehaviour
    {
        [SerializeField] private InputDataSO inputData;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float zoomSpeed = 0.5f;
        [SerializeField] private float minOrthographicSize = 3f;
        [SerializeField] private float maxOrthographicSize = 12f;
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
            HandleZoom();

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

        private void HandleZoom()
        {
            if (Mouse.current == null)
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (targetCamera == null || !targetCamera.orthographic)
                return;

            var scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) <= Mathf.Epsilon)
                return;

            targetCamera.orthographicSize = Mathf.Clamp(
                targetCamera.orthographicSize - ToNotches(scrollY) * zoomSpeed,
                minOrthographicSize,
                maxOrthographicSize);
        }

        /// <summary>
        /// 휠이 보낸 원시 값을 "몇 칸 굴렸나"로 바꾼다.
        ///
        /// 입력 시스템은 휠 델타를 장치가 준 단위 그대로 넘긴다. 윈도우는 한 칸에 120이라
        /// 이 값을 그대로 쓰면 <see cref="zoomSpeed"/>가 0.5여도 한 칸에 60이 움직인다 —
        /// 3~30 범위를 통째로 뛰어넘으니 줌이 최소와 최대 사이를 튀기만 한다.
        ///
        /// 트랙패드처럼 잘게 보내는 장치는 1 근처의 작은 값을 흘려보내므로, 큰 값일 때만
        /// 칸 단위로 나눈다. 그래야 휠은 한 칸씩, 트랙패드는 부드럽게 굴러간다.
        /// </summary>
        private static float ToNotches(float rawScroll)
        {
            const float unitsPerNotch = 120f;
            return Mathf.Abs(rawScroll) >= unitsPerNotch * 0.5f
                ? rawScroll / unitsPerNotch
                : rawScroll;
        }
    }
}

using System.Collections;
using Code.Events;
using Code.MapCreateSystem;
using UnityEngine;

namespace Code.Core
{
    [RequireComponent(typeof(Camera))]
    public class NodeCameraFocusController : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float focusDuration = 0.25f;
        [SerializeField] private bool useUnscaledTime = true;
        [Header("Department Focus")]
        [SerializeField] private bool zoomOnFocus = true;
        [SerializeField, Min(1f)] private float focusedOrthographicSize = 4.2f;

        private Coroutine _focusRoutine;
        private Vector3 _defaultPosition;
        private float _defaultOrthographicSize;
        private Node _focusedNode;

        /// <summary>있으면 카메라 크기를 이것이 정한다. 확대는 기준 해상도 단계로 한다(<see cref="PixelPerfectZoomRules"/>).</summary>
        private TilePixelCamera _pixelPerfect;
        private int _defaultZoomLevel;

        private bool UsesPixelPerfect => _pixelPerfect != null && _pixelPerfect.isActiveAndEnabled;

        private void Awake()
        {
            if (targetCamera != null)
                _pixelPerfect = targetCamera.GetComponent<TilePixelCamera>();
            CacheDefaultCameraState();
        }

        private void OnEnable()
        {
            nodeEventChannel.AddListener<NodeCameraFocusStartedEvent>(HandleFocusStarted);
        }

        private void OnDisable()
        {
            nodeEventChannel.RemoveListener<NodeCameraFocusStartedEvent>(HandleFocusStarted);
            SetFocusedGrid(null);
        }

        private void HandleFocusStarted(NodeCameraFocusStartedEvent evt)
        {
            if (evt?.Node == null || targetCamera == null)
                return;

            if (_focusRoutine != null)
                StopCoroutine(_focusRoutine);

            if (_focusedNode == evt.Node)
            {
                SetFocusedGrid(null);
                _focusRoutine = StartCoroutine(RestoreCameraRoutine());
                return;
            }

            if (_focusedNode == null)
                CacheDefaultCameraState();

            SetFocusedGrid(evt.Node);
            _focusedNode = evt.Node;
            _focusRoutine = StartCoroutine(FocusNodeRoutine(evt.Node));
        }

        private IEnumerator FocusNodeRoutine(Node node)
        {
            var startPosition = transform.position;
            var startSize = targetCamera.orthographicSize;

            var targetPosition = node.transform.position;
            targetPosition.z = startPosition.z;

            // 픽셀 단위 카메라는 크기를 부드럽게 바꿀 수 없다. 처음에 한 번에 확대 단계로 바꾸고 위치만 옮긴다.
            if (zoomOnFocus && UsesPixelPerfect)
                PixelPerfectZoomRules.Apply(_pixelPerfect, PixelPerfectZoomRules.FocusLevel);

            var elapsed = 0f;
            while (elapsed < focusDuration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                var t = focusDuration <= Mathf.Epsilon ? 1f : Mathf.Clamp01(elapsed / focusDuration);
                var easedT = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startPosition, targetPosition, easedT);
                if (zoomOnFocus && !UsesPixelPerfect)
                    targetCamera.orthographicSize = Mathf.Lerp(startSize, focusedOrthographicSize, easedT);

                yield return null;
            }

            transform.position = targetPosition;
            if (zoomOnFocus && !UsesPixelPerfect)
                targetCamera.orthographicSize = focusedOrthographicSize;
            nodeEventChannel?.RaiseEvent(new NodeCameraFocusCompletedEvent(node));
            _focusRoutine = null;
        }

        private IEnumerator RestoreCameraRoutine()
        {
            var startPosition = transform.position;
            var startSize = targetCamera != null ? targetCamera.orthographicSize : _defaultOrthographicSize;
            if (zoomOnFocus && UsesPixelPerfect)
                PixelPerfectZoomRules.Apply(_pixelPerfect, _defaultZoomLevel);
            var elapsed = 0f;

            while (elapsed < focusDuration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                var t = focusDuration <= Mathf.Epsilon ? 1f : Mathf.Clamp01(elapsed / focusDuration);
                var easedT = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startPosition, _defaultPosition, easedT);
                if (targetCamera != null && zoomOnFocus && !UsesPixelPerfect)
                    targetCamera.orthographicSize = Mathf.Lerp(startSize, _defaultOrthographicSize, easedT);

                yield return null;
            }

            transform.position = _defaultPosition;
            if (targetCamera != null && zoomOnFocus && !UsesPixelPerfect)
                targetCamera.orthographicSize = _defaultOrthographicSize;
            _focusedNode = null;
            _focusRoutine = null;
        }

        private void CacheDefaultCameraState()
        {
            _defaultPosition = transform.position;
            if (targetCamera != null)
                _defaultOrthographicSize = targetCamera.orthographicSize;
            if (UsesPixelPerfect)
                _defaultZoomLevel = PixelPerfectZoomRules.LevelOf(_pixelPerfect);
        }

        private void SetFocusedGrid(Node node)
        {
            if (_focusedNode != null && _focusedNode != node)
                _focusedNode.TrapGrid?.SetFocusedGridVisible(false);

            node?.TrapGrid?.SetFocusedGridVisible(true);
        }
    }
}

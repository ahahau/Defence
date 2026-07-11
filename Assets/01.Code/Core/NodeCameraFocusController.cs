using System.Collections;
using _01.Code.Events;
using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Core
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

        private void Awake()
        {
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

            var elapsed = 0f;
            while (elapsed < focusDuration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                var t = focusDuration <= Mathf.Epsilon ? 1f : Mathf.Clamp01(elapsed / focusDuration);
                var easedT = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startPosition, targetPosition, easedT);
                if (zoomOnFocus)
                    targetCamera.orthographicSize = Mathf.Lerp(startSize, focusedOrthographicSize, easedT);

                yield return null;
            }

            transform.position = targetPosition;
            if (zoomOnFocus)
                targetCamera.orthographicSize = focusedOrthographicSize;
            nodeEventChannel?.RaiseEvent(new NodeCameraFocusCompletedEvent(node));
            _focusRoutine = null;
        }

        private IEnumerator RestoreCameraRoutine()
        {
            var startPosition = transform.position;
            var startSize = targetCamera != null ? targetCamera.orthographicSize : _defaultOrthographicSize;
            var elapsed = 0f;

            while (elapsed < focusDuration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                var t = focusDuration <= Mathf.Epsilon ? 1f : Mathf.Clamp01(elapsed / focusDuration);
                var easedT = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startPosition, _defaultPosition, easedT);
                if (targetCamera != null && zoomOnFocus)
                    targetCamera.orthographicSize = Mathf.Lerp(startSize, _defaultOrthographicSize, easedT);

                yield return null;
            }

            transform.position = _defaultPosition;
            if (targetCamera != null && zoomOnFocus)
                targetCamera.orthographicSize = _defaultOrthographicSize;
            _focusedNode = null;
            _focusRoutine = null;
        }

        private void CacheDefaultCameraState()
        {
            _defaultPosition = transform.position;
            if (targetCamera != null)
                _defaultOrthographicSize = targetCamera.orthographicSize;
        }

        private void SetFocusedGrid(Node node)
        {
            if (_focusedNode != null && _focusedNode != node)
                _focusedNode.TrapGrid?.SetFocusedGridVisible(false);

            node?.TrapGrid?.SetFocusedGridVisible(true);
        }
    }
}

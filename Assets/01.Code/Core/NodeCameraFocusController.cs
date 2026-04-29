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
        [SerializeField] private float focusOrthographicSize = 4.5f;
        [SerializeField] private float focusDuration = 0.25f;
        [SerializeField] private bool useUnscaledTime = true;

        private Coroutine _focusRoutine;
        private Vector3 _defaultPosition;
        private float _defaultOrthographicSize;
        private Node _focusedNode;

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();

            CacheDefaultCameraState();
        }

        private void OnEnable()
        { 
            nodeEventChannel.AddListener<NodeCameraFocusStartedEvent>(HandleFocusStarted);
        }

        private void OnDisable()
        { 
            nodeEventChannel.RemoveListener<NodeCameraFocusStartedEvent>(HandleFocusStarted);
        }

        private void HandleFocusStarted(NodeCameraFocusStartedEvent evt)
        {
            if (evt?.Node == null || targetCamera == null)
                return;

            if (_focusRoutine != null)
                StopCoroutine(_focusRoutine);

            if (_focusedNode == evt.Node)
            {
                _focusRoutine = StartCoroutine(RestoreCameraRoutine());
                return;
            }

            if (_focusedNode == null)
                CacheDefaultCameraState();

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
                targetCamera.orthographicSize = Mathf.Lerp(startSize, focusOrthographicSize, easedT);

                yield return null;
            }

            transform.position = targetPosition;
            targetCamera.orthographicSize = focusOrthographicSize;
            _focusedNode = node;
            nodeEventChannel?.RaiseEvent(new NodeCameraFocusCompletedEvent(node));
            _focusRoutine = null;
        }

        private IEnumerator RestoreCameraRoutine()
        {
            var startPosition = transform.position;
            var startSize = targetCamera.orthographicSize;
            var elapsed = 0f;

            while (elapsed < focusDuration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                var t = focusDuration <= Mathf.Epsilon ? 1f : Mathf.Clamp01(elapsed / focusDuration);
                var easedT = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startPosition, _defaultPosition, easedT);
                targetCamera.orthographicSize = Mathf.Lerp(startSize, _defaultOrthographicSize, easedT);

                yield return null;
            }

            transform.position = _defaultPosition;
            targetCamera.orthographicSize = _defaultOrthographicSize;
            _focusedNode = null;
            _focusRoutine = null;
        }

        private void CacheDefaultCameraState()
        {
            _defaultPosition = transform.position;
            _defaultOrthographicSize = targetCamera != null ? targetCamera.orthographicSize : 0f;
        }
    }
}

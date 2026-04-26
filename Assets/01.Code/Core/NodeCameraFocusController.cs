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

        private Coroutine focusRoutine;
        private Vector3 defaultPosition;
        private float defaultOrthographicSize;
        private Node focusedNode;

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();

            CacheDefaultCameraState();
        }

        private void OnEnable()
        {
            if (nodeEventChannel != null)
                nodeEventChannel.AddListener<NodeCameraFocusStartedEvent>(HandleFocusStarted);
        }

        private void OnDisable()
        {
            if (nodeEventChannel != null)
                nodeEventChannel.RemoveListener<NodeCameraFocusStartedEvent>(HandleFocusStarted);
        }

        private void HandleFocusStarted(NodeCameraFocusStartedEvent evt)
        {
            if (evt?.Node == null || targetCamera == null)
                return;

            if (focusRoutine != null)
                StopCoroutine(focusRoutine);

            if (focusedNode == evt.Node)
            {
                focusRoutine = StartCoroutine(RestoreCameraRoutine());
                return;
            }

            if (focusedNode == null)
                CacheDefaultCameraState();

            focusRoutine = StartCoroutine(FocusNodeRoutine(evt.Node));
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
            focusedNode = node;
            nodeEventChannel?.RaiseEvent(new NodeCameraFocusCompletedEvent(node));
            focusRoutine = null;
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

                transform.position = Vector3.Lerp(startPosition, defaultPosition, easedT);
                targetCamera.orthographicSize = Mathf.Lerp(startSize, defaultOrthographicSize, easedT);

                yield return null;
            }

            transform.position = defaultPosition;
            targetCamera.orthographicSize = defaultOrthographicSize;
            focusedNode = null;
            focusRoutine = null;
        }

        private void CacheDefaultCameraState()
        {
            defaultPosition = transform.position;
            defaultOrthographicSize = targetCamera != null ? targetCamera.orthographicSize : 0f;
        }
    }
}

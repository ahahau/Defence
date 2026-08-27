using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.UI
{
    /// <summary>
    /// 침입자가 지나갈 길을 던전 위에 그린다.
    ///
    /// 벽으로 길을 돌리면 함정을 더 밟게 만들 수 있고 그 기제는 이미 돌고 있었지만,
    /// 경로가 화면에 없어 벽을 세워도 실제로 돌아갔는지 알 수가 없었다.
    /// 던전을 감으로 짓는 대신 보고 짓게 하는 것이 이 표시의 목적이다.
    ///
    /// 대기 중에만 그린다 — 습격이 시작되면 적이 직접 보이므로 선이 오히려 화면을 가린다.
    /// </summary>
    public sealed class IntrusionRoutePreview : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private GameEventChannelSO dayEventChannel;

        [SerializeField, Min(0.02f)] private float lineWidth = 0.12f;
        [SerializeField] private Color routeColor = new(1f, 0.42f, 0.2f, 0.75f);
        [SerializeField, Min(0f), Tooltip("경로를 다시 계산하는 간격(초). 매 프레임 A*를 돌릴 이유는 없다.")]
        private float refreshInterval = 0.5f;

        [SerializeField, Tooltip("선이 노드 스프라이트에 묻히지 않도록 올려 둘 정렬 순서")]
        private int sortingOrder = 6;

        private readonly List<Node> _route = new();
        private readonly List<Vector3> _points = new();
        private LineRenderer _line;
        private Node _portalNode;
        private bool _isWaveRunning;
        private float _nextRefreshTime;

        private void Awake()
        {
            _line = BuildLine();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (nodeEventChannel != null)
            {
                nodeEventChannel.AddListener<PortalInstalledEvent>(HandlePortalInstalled);
                nodeEventChannel.AddListener<PortalRemovedEvent>(HandlePortalRemoved);
            }

            if (waveEventChannel != null)
            {
                waveEventChannel.AddListener<WaveStartedEvent>(HandleWaveStarted);
                waveEventChannel.AddListener<WaveEndedEvent>(HandleWaveEnded);
            }

            dayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);
        }

        private void OnDisable()
        {
            if (nodeEventChannel != null)
            {
                nodeEventChannel.RemoveListener<PortalInstalledEvent>(HandlePortalInstalled);
                nodeEventChannel.RemoveListener<PortalRemovedEvent>(HandlePortalRemoved);
            }

            if (waveEventChannel != null)
            {
                waveEventChannel.RemoveListener<WaveStartedEvent>(HandleWaveStarted);
                waveEventChannel.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
            }

            dayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);
        }

        private void HandlePortalInstalled(PortalInstalledEvent evt) => _portalNode = evt.Node;
        private void HandlePortalRemoved(PortalRemovedEvent evt) => _portalNode = null;
        private void HandleWaveStarted(WaveStartedEvent evt) => _isWaveRunning = true;
        private void HandleWaveEnded(WaveEndedEvent evt) => _isWaveRunning = false;
        private void HandleDayChanged(DayChangedEvent evt) => _nextRefreshTime = 0f;

        private void Update()
        {
            if (_isWaveRunning || _portalNode == null)
            {
                SetVisible(false);
                return;
            }

            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            RefreshRoute();
        }

        private void RefreshRoute()
        {
            _route.Clear();
            if (!IntrusionThreat.TryGetPredictedRoute(_portalNode, out var route) || route.Count < 2)
            {
                // 금고가 없으면 적은 배회한다. 없는 경로를 그리면 거짓말이 된다.
                SetVisible(false);
                return;
            }

            _route.AddRange(route);
            BuildLinePoints();
            SetVisible(true);
        }

        /// <summary>
        /// 노드 중심만 이으면 선이 방 벽을 뚫고 지나가 실제로 걷는 길과 달라 보인다.
        /// 두 방을 잇는 통로가 있으면 그 통로의 양 끝을 거쳐 가도록 점을 끼워 넣는다.
        /// </summary>
        private void BuildLinePoints()
        {
            _points.Clear();
            for (var i = 0; i < _route.Count; i++)
            {
                var node = _route[i];
                if (node == null)
                    continue;

                AddPoint(node.transform.position);

                if (i + 1 >= _route.Count)
                    continue;

                var next = _route[i + 1];
                if (next == null || node.Data == null || next.Data == null)
                    continue;

                var edge = EdgeLine.FindBetween(node.Data.Id, next.Data.Id);
                if (edge == null)
                    continue;

                // 통로가 어느 방향으로 그려졌는지 모르므로 가까운 끝부터 지난다.
                var startFirst = (edge.StartPoint - node.transform.position).sqrMagnitude
                                 <= (edge.EndPoint - node.transform.position).sqrMagnitude;
                AddPoint(startFirst ? edge.StartPoint : edge.EndPoint);
                AddPoint(startFirst ? edge.EndPoint : edge.StartPoint);
            }

            _line.positionCount = _points.Count;
            for (var i = 0; i < _points.Count; i++)
                _line.SetPosition(i, _points[i]);
        }

        private void AddPoint(Vector3 position)
        {
            _points.Add(new Vector3(position.x, position.y, 0f));
        }

        private LineRenderer BuildLine()
        {
            var line = gameObject.GetComponent<LineRenderer>();
            if (line == null)
                line = gameObject.AddComponent<LineRenderer>();

            line.useWorldSpace = true;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.startColor = routeColor;
            line.endColor = routeColor;
            line.sortingOrder = sortingOrder;
            line.textureMode = LineTextureMode.Tile;
            // 런타임에서 만들어 쓰는 단순 언릿 재질. 프리팹에 재질을 물려 둘 필요가 없다.
            line.material = new Material(Shader.Find("Sprites/Default"));
            return line;
        }

        private void SetVisible(bool visible)
        {
            if (_line == null)
                return;

            if (!visible)
                _line.positionCount = 0;

            _line.enabled = visible;
        }
    }
}

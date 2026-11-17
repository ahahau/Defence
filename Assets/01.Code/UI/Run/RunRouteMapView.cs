using System;
using TMPro;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>구역 지도 창. 칸 줄과 고른 칸 설명을 그리고 클릭만 중계한다. 문구와 상태는 프레젠터가 정한다.</summary>
    public class RunRouteMapView : MonoBehaviour, IEscapeClosable
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text resources;
        [SerializeField] private UiFrameButton[] nodes = Array.Empty<UiFrameButton>();
        [SerializeField] private TMP_Text[] nodeStates = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailKind;
        [SerializeField] private TMP_Text detailBody;
        [SerializeField] private UiFrameButton closeButton;
        [SerializeField] private UiFrameButton goButton;

        private UnityEngine.Events.UnityAction[] _nodeClicks;
        private UnityEngine.Events.UnityAction _close;
        private UnityEngine.Events.UnityAction _go;

        public event Action<int> NodeClicked;
        public event Action GoClicked;
        public event Action Closed;
        public bool IsOpen => root != null && root.activeSelf;
        public int NodeCount => nodes.Length;

        private void OnEnable()
        {
            EnsureRuntimeState();
            for (int i = 0; i < nodes.Length; i++) nodes[i].Button.onClick.AddListener(_nodeClicks[i]);
            closeButton?.Button.onClick.AddListener(_close);
            goButton?.Button.onClick.AddListener(_go);
        }

        private void OnDisable()
        {
            if (_nodeClicks == null) return;
            for (int i = 0; i < nodes.Length && i < _nodeClicks.Length; i++) nodes[i].Button.onClick.RemoveListener(_nodeClicks[i]);
            closeButton?.Button.onClick.RemoveListener(_close);
            goButton?.Button.onClick.RemoveListener(_go);
        }

        private void EnsureRuntimeState()
        {
            _close ??= Close;
            _go ??= () => GoClicked?.Invoke();
            if (_nodeClicks != null && _nodeClicks.Length == nodes.Length) return;
            _nodeClicks = new UnityEngine.Events.UnityAction[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                int index = i;
                _nodeClicks[i] = () => NodeClicked?.Invoke(index);
            }
        }

        public void Open()
        {
            if (root != null) root.SetActive(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            Closed?.Invoke();
        }

        /// <summary>지도를 닫지만 닫힘 알림은 보내지 않는다(칸에 들어갈 때).</summary>
        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        public void SetResources(string text) => resources?.SetText(text);

        public void SetNode(int index, string name, string state, RunRouteNodeState kind, bool selected)
        {
            if (index >= nodes.Length) return;
            UiFrameButton node = nodes[index];
            (Color fill, Color border, Color text) = kind switch
            {
                RunRouteNodeState.Open => (HudPalette.Primary, HudPalette.Border, HudPalette.Text),
                RunRouteNodeState.Done => (HudPalette.Surface, HudPalette.BorderQuiet, HudPalette.TextDim),
                RunRouteNodeState.Skipped => (HudPalette.Deep, HudPalette.BorderQuiet, HudPalette.TextMuted),
                _ => (HudPalette.Control, HudPalette.BorderQuiet, HudPalette.TextMuted),
            };
            node.SetStyle(fill, border, text, kind == RunRouteNodeState.Open);
            node.SetLabel(name);
            node.SetSelected(selected);
            if (index < nodeStates.Length)
            {
                nodeStates[index].SetText(state);
                nodeStates[index].color = kind == RunRouteNodeState.Open ? HudPalette.Accent : text;
            }
        }

        /// <summary>칸을 숨기거나 보인다(원정 뒤 갈래가 없는 예전 저장).</summary>
        public void SetNodeVisible(int index, bool visible)
        {
            if (index >= nodes.Length) return;
            nodes[index].SetVisible(visible);
        }

        public void SetDetail(string name, string kind, string body, string goLabel)
        {
            detailName?.SetText(name);
            detailKind?.SetText(kind);
            detailBody?.SetText(body);
            if (goButton == null) return;
            goButton.SetVisible(goLabel != null);
            if (goLabel != null) goButton.SetLabel(goLabel);
        }
    }
}

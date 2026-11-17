using System;
using System.Collections.Generic;
using Code.Stages;
using UnityEngine;
using UnityEngine.Events;

namespace Code.UI.Stages
{
    /// <summary>
    /// 전투 중 오른쪽 아래 수동 장치(밀치기·낙석) 버튼 줄. 누르면 그 장치를 고르고 발동을 요청한다.
    /// 고른 장치는 금색 테두리와 F 표시. 판단·발동은 StageHudView가 한다.
    /// </summary>
    public sealed class StageDeviceButtons : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private StageDeviceButton[] buttons;
        private readonly List<Vector2Int> _cells = new();
        private UnityAction[] _clicks;

        public bool IsValid => root != null && buttons != null && buttons.Length > 0;

        /// <summary>누른 장치 칸.</summary>
        public event Action<Vector2Int> Picked;

        public void Render(IReadOnlyList<StageDevices.State> states, Vector2Int? selected)
        {
            if (!IsValid || states == null)
                return;
            _cells.Clear();
            for (int i = 0; i < states.Count && _cells.Count < buttons.Length; i++)
            {
                StageDevices.State state = states[i];
                if (state == null || !state.Device.IsManual)
                    continue;
                Vector2Int cell = state.Device.Cell;
                string name = state.Device.Kind == StageDeviceKind.Push ? "밀치기" : "낙석";
                buttons[_cells.Count].Show(name, selected == cell, state.Ready, state.Ready ? 1f : state.CooldownRatio,
                    state.CooldownLeft);
                _cells.Add(cell);
            }
            for (int i = _cells.Count; i < buttons.Length; i++)
                buttons[i].Hide();
            if (root.activeSelf != _cells.Count > 0)
                root.SetActive(_cells.Count > 0);
        }

        public void Hide()
        {
            if (root != null && root.activeSelf)
                root.SetActive(false);
        }

        private void OnEnable()
        {
            if (!IsValid)
                return;
            EnsureClicks();
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null)
                    buttons[i].Button.onClick.AddListener(_clicks[i]);
        }

        private void OnDisable()
        {
            if (!IsValid || _clicks == null)
                return;
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null)
                    buttons[i].Button.onClick.RemoveListener(_clicks[i]);
        }

        private void EnsureClicks()
        {
            if (_clicks != null && _clicks.Length == buttons.Length)
                return;
            _clicks = new UnityAction[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                _clicks[i] = () =>
                {
                    if (index < _cells.Count)
                        Picked?.Invoke(_cells[index]);
                };
            }
        }
    }
}

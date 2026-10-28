using System.Collections.Generic;
using Code.UI;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 환경 장치를 칸 색으로 그린다. 불 구멍은 주황(켜지면 밝게), 닫힌 문은 회색 블록(열리면 옅은 테), 낙석은 파랑(준비되면 금색)과 대기 막대.
    /// 그림은 배치 커서 스프라이트를 복제해 쓴다(경로 미리보기와 같은 방식).
    /// </summary>
    public sealed class StageDeviceView
    {
        /// <summary>지형(0)·경로 미리보기(5) 위, 코어·문(10)·바리케이드(12) 아래. 닫힌 문만 캐릭터 아래 블록으로 보이게 11.</summary>
        private const int FloorOrder = 6;
        private const int GateOrder = 11;
        private const int BarOrder = 45;

        private readonly SpriteRenderer _template;
        private readonly Vector3 _unitScale;
        private readonly Transform _root;
        private readonly List<SpriteRenderer> _cells = new();
        private readonly List<SpriteRenderer> _bars = new();

        public StageDeviceView(SpriteRenderer template, Vector3 unitScale)
        {
            _template = template;
            _unitScale = unitScale;
            _root = new GameObject("Stage Devices").transform;
        }

        public void Show(StageSession session)
        {
            int bars = 0;
            float size = session.Definition.CellSize;
            IReadOnlyList<StageDevices.State> states = session.Devices.States;
            for (int i = 0; i < states.Count; i++)
            {
                StageDevices.State state = states[i];
                Vector3 position = session.Board.World(state.Device.Cell);
                SpriteRenderer cell = Pick(_cells, i, "Device Cell");
                cell.transform.position = position;
                cell.transform.localScale = _unitScale * size;
                switch (state.Device.Kind)
                {
                    case StageDeviceKind.FireVent:
                        cell.sortingOrder = FloorOrder;
                        cell.color = Tint(HudPalette.Warn, state.Active ? 0.85f : 0.3f);
                        break;
                    case StageDeviceKind.Gate:
                        cell.sortingOrder = GateOrder;
                        cell.color = Tint(HudPalette.Track, state.Active ? 0.95f : 0.25f);
                        break;
                    default:
                        cell.sortingOrder = FloorOrder;
                        cell.color = Tint(state.Ready ? HudPalette.Accent : HudPalette.Magic, state.Ready ? 0.75f : 0.35f);
                        if (session.Phase == StagePhase.Fighting && !state.Ready)
                            Bar(bars++, position + Vector3.up * (0.6f * size), size, state.CooldownRatio);
                        break;
                }
                cell.gameObject.SetActive(true);
            }
            Trim(_cells, states.Count);
            Trim(_bars, bars);
        }

        public void Hide()
        {
            Trim(_cells, 0);
            Trim(_bars, 0);
        }

        private void Bar(int index, Vector3 anchor, float size, float ratio)
        {
            SpriteRenderer bar = Pick(_bars, index, "Device Cooldown");
            bar.sortingOrder = BarOrder;
            float width = 0.8f * size;
            bar.transform.position = anchor + Vector3.right * (width * (ratio - 1f) * 0.5f);
            bar.transform.localScale = new Vector3(_unitScale.x * width * ratio, _unitScale.y * 0.12f * size, _unitScale.z);
            bar.color = Tint(HudPalette.Magic, 0.8f);
            bar.gameObject.SetActive(ratio > 0f);
        }

        private SpriteRenderer Pick(List<SpriteRenderer> pool, int index, string name)
        {
            while (pool.Count <= index)
            {
                SpriteRenderer created = Object.Instantiate(_template, _root);
                created.name = name;
                pool.Add(created);
            }
            return pool[index];
        }

        private static Color Tint(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

        private static void Trim(List<SpriteRenderer> pool, int used)
        {
            for (int i = used; i < pool.Count; i++)
                pool[i].gameObject.SetActive(false);
        }
    }
}

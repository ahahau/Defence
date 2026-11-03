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
        private readonly List<SpriteRenderer> _arrows = new();

        public bool IsValid => _root != null;

        public StageDeviceView(SpriteRenderer template, Vector3 unitScale, Transform parent = null)
        {
            _template = template;
            _unitScale = unitScale;
            _root = new GameObject("Stage Devices").transform;
            if (parent != null)
                _root.SetParent(parent, false);
        }

        public void Show(StageSession session, Vector2Int? focus = null)
        {
            if (_root == null)
                return;
            int bars = 0;
            int cells = 0;
            int arrows = 0;
            float size = session.Definition.CellSize;
            IReadOnlyList<StageDevices.State> states = session.Devices.States;
            for (int i = 0; i < states.Count; i++)
            {
                StageDevices.State state = states[i];
                if (state.Device.Kind == StageDeviceKind.Push)
                {
                    Vector3 center = session.Board.World(state.Device.Cell);
                    Vector3 direction = new(state.Direction.x, state.Direction.y, 0f);
                    Vector3 side = new(-direction.y, direction.x, 0f);
                    Vector3 tip = center + direction * (size * 0.32f);
                    Stroke(arrows++, center - direction * (size * 0.28f), tip, size);
                    Stroke(arrows++, tip - direction * (size * 0.24f) + side * (size * 0.2f), tip, size);
                    Stroke(arrows++, tip - direction * (size * 0.24f) - side * (size * 0.2f), tip, size);
                    if (focus == state.Device.Cell)
                    {
                        for (int step = 1; step <= state.Device.PushCells; step++)
                        {
                            Vector2Int at = state.Device.Cell + state.Direction * step;
                            if (!session.Definition.IsFloor(at))
                                break;
                            bool pit = session.Board.Pits.Contains(at);
                            bool blocked = pit || !session.Board.IsPassable(at) || session.Board.BlockerAt(at, null) != null;
                            SpriteRenderer range = Pick(_cells, cells++, "Push Route");
                            range.transform.position = session.Board.World(at);
                            range.transform.localScale = _unitScale * size;
                            range.sortingOrder = FloorOrder + 1;
                            // 구덩이에 닿는 경로는 낙하 지점으로 붉게 보인다
                            range.color = Tint(pit ? HudPalette.Danger : blocked ? HudPalette.Warn : HudPalette.Accent, pit ? 0.55f : 0.3f);
                            range.gameObject.SetActive(true);
                            if (blocked)
                                break;
                        }
                    }
                }
                if (state.Device.IsManual && focus == state.Device.Cell)
                {
                    Vector3 center = session.Board.World(state.Device.Cell);
                    float radius = state.Device.Radius * size;
                    for (int segment = 0; segment < 32; segment++)
                    {
                        float from = segment * Mathf.PI * 2f / 32f;
                        float to = (segment + 1) * Mathf.PI * 2f / 32f;
                        Stroke(arrows++, center + new Vector3(Mathf.Cos(from), Mathf.Sin(from)) * radius,
                            center + new Vector3(Mathf.Cos(to), Mathf.Sin(to)) * radius, size);
                    }
                }
                // 불이 번지는 풀섬 칸은 불 구멍이 탈 때만 같은 주황으로 칠한다(부하도 맞는 칸)
                if (state.Device.Kind == StageDeviceKind.FireVent && state.Active)
                    foreach (Vector2Int spread in state.SpreadCells)
                    {
                        SpriteRenderer burning = Pick(_cells, cells++, "Device Cell");
                        burning.transform.position = session.Board.World(spread);
                        burning.transform.localScale = _unitScale * size;
                        burning.sortingOrder = FloorOrder;
                        burning.color = Tint(HudPalette.Warn, 0.6f);
                        burning.gameObject.SetActive(true);
                    }
                Vector3 position = session.Board.World(state.Device.Cell);
                SpriteRenderer cell = Pick(_cells, cells++, "Device Cell");
                cell.transform.position = position;
                cell.transform.localScale = _unitScale * size;
                switch (state.Device.Kind)
                {
                    case StageDeviceKind.FireVent:
                        cell.sortingOrder = FloorOrder;
                        cell.color = Tint(HudPalette.Warn, state.Active ? 0.85f : 0.3f);
                        break;
                    case StageDeviceKind.Hazard:
                        cell.sortingOrder = FloorOrder;
                        cell.color = Tint(HudPalette.Warn, 0.65f);
                        break;
                    case StageDeviceKind.Pit:
                        cell.sortingOrder = FloorOrder;
                        cell.color = Tint(HudPalette.Scrim, 0.92f);
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
            Trim(_cells, cells);
            Trim(_bars, bars);
            Trim(_arrows, arrows);
        }

        public void Hide()
        {
            Trim(_cells, 0);
            Trim(_bars, 0);
            Trim(_arrows, 0);
        }

        private void Stroke(int index, Vector3 from, Vector3 to, float size)
        {
            SpriteRenderer line = Pick(_arrows, index, "Push Direction");
            Vector3 delta = to - from;
            line.transform.position = (from + to) * 0.5f;
            line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            line.transform.localScale = new Vector3(_unitScale.x * delta.magnitude, _unitScale.y * size * 0.09f, _unitScale.z);
            line.sortingOrder = GateOrder;
            line.color = HudPalette.TextBody;
            line.gameObject.SetActive(true);
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
                if (pool[i] != null)
                    pool[i].gameObject.SetActive(false);
        }
    }
}

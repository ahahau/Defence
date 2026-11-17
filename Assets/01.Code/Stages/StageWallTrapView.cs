using System.Collections.Generic;
using Code.UI;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 벽 함정을 칸 색·종류별 표식·단계 점으로 그린다. 팩에 맞는 그림이 없어 쓰는 임시 표시다(2026-11-03 사용자 승인).
    /// 발동하면 공격 칸을 잠깐 밝히고, 독가스는 남은 동안 칸을 칠한다. 가리킨 함정은 공격 칸을 늘 보이고,
    /// 벽 함정 도구로 붙일 수 있는 벽을 가리키면 붙일 방향과 공격 칸을 미리 보인다.
    /// </summary>
    public sealed class StageWallTrapView
    {
        /// <summary>환경 장치와 같은 층. 벽 칸 위 표시는 문(11)과 같은 높이로 벽 그림 위에 보인다.</summary>
        private const int ZoneOrder = 7;
        private const int MountOrder = 11;
        private const int BarOrder = 45;

        private readonly SpriteRenderer _template;
        private readonly Vector3 _unitScale;
        private readonly Transform _root;
        private readonly List<SpriteRenderer> _cells = new();
        private readonly List<SpriteRenderer> _lines = new();
        private readonly List<SpriteRenderer> _bars = new();
        private readonly List<Vector2Int> _previewZone = new();
        private int _cellCount;
        private int _lineCount;

        public bool IsValid => _root != null;

        public StageWallTrapView(SpriteRenderer template, Vector3 unitScale, Transform parent = null)
        {
            _template = template;
            _unitScale = unitScale;
            _root = new GameObject("Stage Wall Traps").transform;
            if (parent != null)
                _root.SetParent(parent, false);
        }

        /// <param name="preview">배치 중인 벽 함정. 가리킨 칸에 붙일 수 있을 때만 미리보기를 그린다.</param>
        public void Show(StageSession session, Vector2Int? focus = null, StageOption preview = null)
        {
            if (_root == null)
                return;
            _cellCount = 0;
            _lineCount = 0;
            int bars = 0;
            float size = session.Definition.CellSize;
            StageWallTraps traps = session.Placement.WallTraps;
            // 매 프레임 그리므로 인터페이스 foreach(반복기 할당) 대신 인덱스로 돈다
            IReadOnlyList<StageWallTraps.Placed> all = traps.All;
            for (int i = 0; i < all.Count; i++)
            {
                StageWallTraps.Placed placed = all[i];
                Color color = ColorFor(placed.Data.Kind);
                Vector3 center = session.Board.World(placed.Cell);
                bool focused = focus == placed.Cell;
                // 공격 칸: 가리킬 때 옅게, 발동·가스 중에는 진하게
                float zoneAlpha = placed.CloudLeft > 0f ? 0.4f : placed.FlashLeft > 0f ? 0.55f : focused ? 0.25f : 0f;
                if (zoneAlpha > 0f)
                    foreach (Vector2Int at in placed.Zone)
                        Cell(session.Board.World(at), size, ZoneOrder, Tint(color, zoneAlpha));
                Cell(center, size * 0.8f, MountOrder, Tint(color, 0.85f));
                Glyph(placed.Data.Kind, center, placed.Direction, size, HudPalette.Deep);
                // 판 안 강화 단계를 칸 위쪽 점으로 보인다
                for (int level = 0; level < placed.Level; level++)
                    Cell(center + new Vector3((level - (placed.Level - 1) * 0.5f) * size * 0.22f, size * 0.32f, 0f),
                        size * 0.14f, MountOrder + 1, HudPalette.Text);
                if (session.Phase == StagePhase.Fighting && placed.CooldownLeft > 0f)
                    Bar(bars++, center + Vector3.up * (0.6f * size), size, placed.CooldownRatio);
            }
            if (preview?.WallTrap != null && focus.HasValue && traps.At(focus.Value) == null
                && traps.IsMount(focus.Value, out _, out _))
            {
                Vector2Int direction = traps.FacingFrom(focus.Value, Vector2Int.down);
                traps.FillZone(focus.Value, direction, preview.WallTrap.Range, _previewZone);
                Color color = ColorFor(preview.WallTrap.Kind);
                foreach (Vector2Int at in _previewZone)
                    Cell(session.Board.World(at), size, ZoneOrder, Tint(color, 0.3f));
                Vector3 center = session.Board.World(focus.Value);
                Cell(center, size * 0.8f, MountOrder, Tint(color, 0.45f));
                Glyph(preview.WallTrap.Kind, center, direction, size, HudPalette.Deep);
            }
            Trim(_cells, _cellCount);
            Trim(_lines, _lineCount);
            Trim(_bars, bars);
        }

        public void Hide()
        {
            Trim(_cells, 0);
            Trim(_lines, 0);
            Trim(_bars, 0);
        }

        private static Color ColorFor(StageWallTrapKind kind) => kind switch
        {
            StageWallTrapKind.Push => HudPalette.Accent,
            StageWallTrapKind.Arrow => HudPalette.Info,
            StageWallTrapKind.Spear => HudPalette.TextBody,
            _ => HudPalette.Gain
        };

        /// <summary>종류별 표식: 밀치기 겹화살촉, 화살 긴 화살, 창 세 갈래, 독가스 점 세 개(모두 통로 쪽을 향함).</summary>
        private void Glyph(StageWallTrapKind kind, Vector3 center, Vector2Int facing, float size, Color color)
        {
            Vector3 forward = new(facing.x, facing.y, 0f);
            Vector3 side = new(-forward.y, forward.x, 0f);
            switch (kind)
            {
                case StageWallTrapKind.Push:
                    for (int chevron = 0; chevron < 2; chevron++)
                    {
                        Vector3 tip = center + forward * (size * (0.05f + chevron * 0.22f));
                        Stroke(tip - forward * (size * 0.16f) + side * (size * 0.2f), tip, size, color);
                        Stroke(tip - forward * (size * 0.16f) - side * (size * 0.2f), tip, size, color);
                    }
                    break;
                case StageWallTrapKind.Arrow:
                {
                    Vector3 tip = center + forward * (size * 0.4f);
                    Stroke(center - forward * (size * 0.3f), tip, size, color);
                    Stroke(tip - forward * (size * 0.16f) + side * (size * 0.12f), tip, size, color);
                    Stroke(tip - forward * (size * 0.16f) - side * (size * 0.12f), tip, size, color);
                    break;
                }
                case StageWallTrapKind.Spear:
                    for (int prong = -1; prong <= 1; prong++)
                        Stroke(center - forward * (size * 0.15f) + side * (size * 0.18f * prong),
                            center + forward * (size * 0.4f) + side * (size * 0.18f * prong), size, color);
                    break;
                default:
                    Cell(center + forward * (size * 0.2f), size * 0.16f, MountOrder + 1, color);
                    Cell(center - forward * (size * 0.05f) + side * (size * 0.2f), size * 0.16f, MountOrder + 1, color);
                    Cell(center - forward * (size * 0.05f) - side * (size * 0.2f), size * 0.16f, MountOrder + 1, color);
                    break;
            }
        }

        private void Cell(Vector3 position, float size, int order, Color color)
        {
            SpriteRenderer cell = Pick(_cells, _cellCount++, "Wall Trap Cell");
            cell.transform.position = position;
            cell.transform.rotation = Quaternion.identity;
            cell.transform.localScale = _unitScale * size;
            cell.sortingOrder = order;
            cell.color = color;
            cell.gameObject.SetActive(true);
        }

        private void Stroke(Vector3 from, Vector3 to, float size, Color color)
        {
            SpriteRenderer line = Pick(_lines, _lineCount++, "Wall Trap Glyph");
            Vector3 delta = to - from;
            line.transform.position = (from + to) * 0.5f;
            line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            line.transform.localScale = new Vector3(_unitScale.x * delta.magnitude, _unitScale.y * size * 0.09f, _unitScale.z);
            line.sortingOrder = MountOrder + 1;
            line.color = color;
            line.gameObject.SetActive(true);
        }

        private void Bar(int index, Vector3 anchor, float size, float ratio)
        {
            SpriteRenderer bar = Pick(_bars, index, "Wall Trap Cooldown");
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

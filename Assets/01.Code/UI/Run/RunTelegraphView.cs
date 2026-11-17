using System.Collections.Generic;
using System.Globalization;
using Code.Run;
using TMPro;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>규칙의 현재 예고를 그린다. 재사용되는 규칙 상태 객체를 보관하지 않고 표시용 연번·종료 이유만 기억한다.</summary>
    public class RunTelegraphView
    {
        private const float EndNoteSeconds = 0.8f;
        private RunBattle _battle;
        private readonly RunGridView _grid;
        private readonly IReadOnlyList<RunActor> _actors;
        private readonly Glyph[] _glyphs;
        private readonly string[] _tenths;

        private sealed class Glyph
        {
            public GameObject Root;
            public GameObject Area;
            public SpriteRenderer Shade;
            public SpriteRenderer[] Hatches;
            public SpriteRenderer[] Path;
            public GameObject Timer;
            public TextMeshPro Countdown;
            public TextMeshPro Name;
            public SpriteRenderer NamePlate;
            public TextMeshPro EndNote;
            public GameObject Aim;
            public int Sequence;
            public int Tenth = -1;
            public RunTelegraphEndReason EndReason;
            public float EndTime;
            public bool Seen;
        }

        public RunTelegraphView(RunBattle battle, RunGridView grid, RunWorldPrimitives drawing,
            IReadOnlyList<RunActor> actors, Transform parent)
        {
            _battle = battle;
            _grid = grid;
            _actors = actors;
            _glyphs = new Glyph[battle.Combatants.Count];
            int maxTenth = 1;
            for (int i = 0; i < battle.Combatants.Count; i++)
            {
                RunTelegraph state = battle.Combatants[i].Telegraph;
                if (state.Effect == null) continue;
                maxTenth = Mathf.Max(maxTenth, Mathf.CeilToInt(state.DurationSeconds * 10f), Mathf.CeilToInt(state.Effect.WarningSeconds * 10f));
                _glyphs[i] = BuildGlyph(drawing, parent, i, state.Caster.Definition.DisplayName + " · " + state.Skill.DisplayName,
                    battle.Rooms.Count * 5 + battle.Links.Count + 2);
            }
            _tenths = new string[maxTenth + 1];
            for (int i = 0; i < _tenths.Length; i++) _tenths[i] = (i * 0.1f).ToString("0.0", CultureInfo.InvariantCulture);
        }

        public void BindBattle(RunBattle battle)
        {
            _battle = battle;
            for (int i = 0; i < _glyphs.Length; i++)
            {
                Glyph glyph = _glyphs[i];
                if (glyph == null) continue;
                glyph.Sequence = 0;
                glyph.Tenth = -1;
                glyph.EndReason = RunTelegraphEndReason.None;
                glyph.EndTime = 0f;
                glyph.Root.SetActive(false);
            }
        }

        public void Refresh(float unscaledDeltaTime)
        {
            for (int i = 0; i < _glyphs.Length; i++)
            {
                if (_glyphs[i] != null) _glyphs[i].Seen = false;
            }
            IReadOnlyList<RunTelegraph> active = _battle.Telegraphs;
            for (int i = 0; i < active.Count; i++)
            {
                RunTelegraph state = active[i];
                Glyph glyph = _glyphs[state.Caster.Id];
                if (glyph == null) continue;
                glyph.Seen = true;
                glyph.Sequence = state.Sequence;
                glyph.EndReason = RunTelegraphEndReason.None;
                glyph.EndTime = 0f;
                DrawActive(glyph, state);
            }
            for (int i = 0; i < _glyphs.Length; i++)
            {
                Glyph glyph = _glyphs[i];
                if (glyph == null || glyph.Seen) continue;
                RunTelegraph current = _battle.Combatants[i].Telegraph;
                if (current.Sequence > 0 && current.EndReason != RunTelegraphEndReason.None
                    && (glyph.Sequence != current.Sequence || glyph.EndReason != current.EndReason))
                {
                    glyph.Sequence = current.Sequence;
                    glyph.EndReason = current.EndReason;
                    glyph.EndTime = EndNoteSeconds;
                    PositionBadge(glyph, Anchor(current), current.Scope != RunTelegraphScope.StraightLine);
                    glyph.EndNote.SetText(current.EndReason == RunTelegraphEndReason.Completed ? "발동"
                        : current.EndReason == RunTelegraphEndReason.TargetEscaped ? "빗나감" : "끊김");
                }
                glyph.EndTime -= unscaledDeltaTime;
                bool show = glyph.EndTime > 0f && _grid.IsVisible(current.Origin);
                Toggle(glyph.Root, show);
                Toggle(glyph.Area, false);
                Toggle(glyph.Timer, false);
                Toggle(glyph.Aim, false);
                Toggle(glyph.EndNote.gameObject, show);
                for (int line = 0; line < glyph.Path.Length; line++) Toggle(glyph.Path[line].gameObject, false);
            }
        }

        private void DrawActive(Glyph glyph, RunTelegraph state)
        {
            bool visible = _grid.IsVisible(state.Origin);
            Toggle(glyph.Root, visible);
            if (!visible) return;
            Toggle(glyph.EndNote.gameObject, false);
            Toggle(glyph.Timer, true);
            PositionBadge(glyph, Anchor(state), state.Scope != RunTelegraphScope.StraightLine);
            int tenth = Mathf.Clamp(Mathf.CeilToInt(state.RemainingSeconds * 10f), 0, _tenths.Length - 1);
            if (tenth != glyph.Tenth)
            {
                glyph.Tenth = tenth;
                glyph.Countdown.SetText(_tenths[tenth]);
            }
            bool roomScope = state.Scope == RunTelegraphScope.Room;
            Toggle(glyph.Area, roomScope);
            if (roomScope)
            {
                Rect rect = _grid.RoomRect(state.TargetRoom);
                rect = Rect.MinMaxRect(rect.xMin + 0.12f, rect.yMin + 0.12f, rect.xMax - 0.12f, rect.yMax - 0.12f);
                glyph.Shade.transform.position = new Vector3(rect.center.x, rect.center.y, 0f);
                glyph.Shade.transform.localScale = new Vector3(rect.width, rect.height, 1f);
                for (int i = 0; i < glyph.Hatches.Length; i++)
                {
                    float x = rect.xMin - rect.height + (i + 0.5f) * (rect.width + rect.height) / glyph.Hatches.Length;
                    Vector3 from = new(Mathf.Max(rect.xMin, x), rect.yMin + Mathf.Max(0f, rect.xMin - x), 0f);
                    Vector3 to = new(Mathf.Min(rect.xMax, x + rect.height), rect.yMax - Mathf.Max(0f, x + rect.height - rect.xMax), 0f);
                    RunWorldPrimitives.Stretch(glyph.Hatches[i].transform, from, to, 0.035f);
                }
            }
            bool aim = state.Scope == RunTelegraphScope.Combatant;
            Toggle(glyph.Aim, aim);
            if (aim && state.Target != null)
                glyph.Aim.transform.position = _actors[state.Target.Id].transform.position + Vector3.up * 0.8f;
            int count = state.Scope == RunTelegraphScope.StraightLine || state.Kind == RunTelegraphKind.Fireball ? state.PathCount : 0;
            for (int i = 0; i < glyph.Path.Length; i++)
            {
                Toggle(glyph.Path[i].gameObject, i < count);
                if (i >= count) continue;
                Vector3 a = _grid.ToWorld(state.PathStart(i));
                Vector3 b = _grid.ToWorld(state.PathEnd(i));
                RunWorldPrimitives.Stretch(glyph.Path[i].transform, a, b, state.Kind == RunTelegraphKind.Charge ? 0.22f : 0.06f);
            }
        }

        private Vector3 Anchor(RunTelegraph state)
        {
            if (state.Scope == RunTelegraphScope.Combatant && state.Target != null)
                return _actors[state.Target.Id].transform.position + new Vector3(-1.7f, 0.7f, 0f);
            if (state.Scope == RunTelegraphScope.Room)
            {
                Rect rect = _grid.RoomRect(state.TargetRoom);
                return new Vector3(rect.center.x + (state.Caster.Id % 2 == 0 ? -0.65f : 0.65f), rect.yMin + 0.9f, 0f);
            }
            bool vertical = state.Origin.IsInRoom ? state.Origin.Axis == RunAxis.Vertical
                : state.Origin.IsAtEntrance || _battle.Links[state.Origin.Link].IsVertical;
            return _grid.ToWorld(state.Origin) + (vertical ? new Vector3(2f, -0.6f, 0f) : new Vector3(0f, -1.3f, 0f));
        }

        private static void PositionBadge(Glyph glyph, Vector3 anchor, bool nameBelow)
        {
            glyph.Timer.transform.position = anchor;
            glyph.Name.transform.position = anchor + Vector3.up * (nameBelow ? -0.65f : 0.48f);
            glyph.NamePlate.transform.position = glyph.Name.transform.position;
            glyph.EndNote.transform.position = anchor;
        }

        private static Glyph BuildGlyph(RunWorldPrimitives drawing, Transform parent, int id, string title, int pathCapacity)
        {
            Glyph glyph = new();
            glyph.Root = new GameObject($"Telegraph_{id}");
            glyph.Root.transform.SetParent(parent, false);
            glyph.Area = new GameObject("RoomArea");
            glyph.Area.transform.SetParent(glyph.Root.transform, false);
            glyph.Shade = drawing.Quad("Shade", Vector3.zero, Vector2.one, HudPalette.TelegraphShade, 12, glyph.Area.transform);
            glyph.Hatches = new SpriteRenderer[12];
            for (int i = 0; i < glyph.Hatches.Length; i++)
                glyph.Hatches[i] = drawing.Quad("Hatch", Vector3.zero, Vector2.one, HudPalette.TelegraphHatch, 13, glyph.Area.transform);
            glyph.Path = new SpriteRenderer[pathCapacity];
            for (int i = 0; i < pathCapacity; i++)
                glyph.Path[i] = drawing.Quad("Path", Vector3.zero, Vector2.one, HudPalette.TelegraphBand, 14, glyph.Root.transform);
            glyph.Timer = new GameObject("Timer");
            glyph.Timer.transform.SetParent(glyph.Root.transform, false);
            SpriteRenderer back = drawing.Quad("Back", Vector3.zero, Vector2.one * 0.65f, HudPalette.Deep, 50, glyph.Timer.transform);
            back.sprite = drawing.Dot;
            SpriteRenderer ring = drawing.Quad("Ring", Vector3.zero, Vector2.one * 0.65f, HudPalette.Danger, 51, glyph.Timer.transform);
            ring.sprite = drawing.Ring;
            glyph.Countdown = drawing.Label("Seconds", glyph.Timer.transform, 2.8f, HudPalette.Text, new Vector2(0.8f, 0.5f));
            glyph.Countdown.SetText("00.0");
            glyph.NamePlate = drawing.Quad("SkillNameBack", Vector3.zero, new Vector2(2.7f, 0.4f), HudPalette.Deep, 59, glyph.Root.transform);
            glyph.Name = drawing.Label("SkillName", glyph.Root.transform, 2f, HudPalette.Loss, new Vector2(3.8f, 0.4f));
            glyph.Name.SetText(title);
            glyph.EndNote = drawing.Label("EndNote", glyph.Root.transform, 2f, HudPalette.Text, new Vector2(2f, 0.5f));
            glyph.Aim = new GameObject("Aim");
            glyph.Aim.transform.SetParent(glyph.Root.transform, false);
            SpriteRenderer aim = drawing.Quad("AimRing", Vector3.zero, Vector2.one * 0.42f, HudPalette.Danger, 52, glyph.Aim.transform);
            aim.sprite = drawing.Ring;
            drawing.Quad("AimHorizontal", Vector3.zero, new Vector2(0.6f, 0.035f), HudPalette.Danger, 53, glyph.Aim.transform);
            drawing.Quad("AimVertical", Vector3.zero, new Vector2(0.035f, 0.6f), HudPalette.Danger, 53, glyph.Aim.transform);
            glyph.Root.SetActive(false);
            return glyph;
        }

        private static void Toggle(GameObject go, bool active)
        {
            if (go.activeSelf != active) go.SetActive(active);
        }
    }
}

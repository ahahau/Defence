using System.Collections.Generic;
using Code.Run;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>층 번호별 십자 평면도와 중심선 위치 변환. 그림 크기는 전투 거리를 바꾸지 않는다.</summary>
    public class RunGridView
    {
        private RunBattle _battle;
        private bool[] _highlights;
        private readonly RunWorldPrimitives _drawing;
        private readonly Rect[] _rooms;
        private readonly GameObject[] _roomRoots;
        private readonly GameObject[] _linkRoots;
        private readonly SpriteRenderer[] _bodies;
        private readonly SpriteRenderer[] _borders;
        private readonly SpriteRenderer[] _doors;
        private readonly GameObject _entrance;
        private readonly RunFloorDressing _dressing;
        private readonly GameObject[][] _brokenFrames;

        /// <param name="art">평면도 그림. 없으면 예전처럼 단색 도형으로 그린다.</param>
        public RunGridView(RunBattle battle, RunWorldPrimitives drawing, Transform parent,
            Dictionary<Collider2D, int> roomColliders, RunFloorArt art = null)
        {
            _battle = battle;
            _drawing = drawing;
            _brokenFrames = art != null ? new GameObject[battle.Rooms.Count][] : null;
            _rooms = new Rect[battle.Rooms.Count];
            _roomRoots = new GameObject[battle.Rooms.Count];
            _bodies = new SpriteRenderer[battle.Rooms.Count];
            _borders = new SpriteRenderer[battle.Rooms.Count];
            _linkRoots = new GameObject[battle.Links.Count];
            _doors = new SpriteRenderer[battle.Links.Count];
            for (int i = 0; i < battle.Rooms.Count; i++)
            {
                RunRoom room = battle.Rooms[i];
                int min = room.Column;
                int max = room.Column;
                for (int j = 0; j < battle.Rooms.Count; j++)
                {
                    if (battle.Rooms[j].Floor != room.Floor) continue;
                    min = Mathf.Min(min, battle.Rooms[j].Column);
                    max = Mathf.Max(max, battle.Rooms[j].Column);
                }
                float x = (room.Column - (min + max) * 0.5f) * (room.Length + battle.Settings.CorridorLength) - room.Length * 0.5f;
                _rooms[i] = new Rect(x, -room.Row * (room.Length + battle.Settings.CorridorLength) - room.Length * 0.5f, room.Length, room.Length);
            }
            Vector3 outside = ToWorld(battle.EntrancePosition);
            Vector3 inside = ToWorld(RunPosition.InRoom(battle.EntryRoom, RunAxis.Vertical, 0f));
            if (art == null)
                drawing.Line("EntranceBorder", outside, inside, battle.Rooms[battle.EntryRoom].Length * 0.4f + 0.12f,
                    HudPalette.DungeonFloor, 0, parent);
            _entrance = drawing.Line("EntranceTunnel", outside, inside, battle.Rooms[battle.EntryRoom].Length * 0.4f,
                HudPalette.DungeonTunnel, 1, parent).gameObject;
            if (art != null) _entrance.GetComponent<SpriteRenderer>().enabled = false;
            for (int i = 0; i < battle.Rooms.Count; i++) BuildRoom(i, parent, roomColliders);
            for (int i = 0; i < battle.Links.Count; i++) BuildLink(i, parent, art == null);
            if (art != null)
            {
                Transform[] roomRoots = new Transform[_roomRoots.Length];
                for (int i = 0; i < roomRoots.Length; i++) roomRoots[i] = _roomRoots[i].transform;
                Transform[] linkRoots = new Transform[_linkRoots.Length];
                for (int i = 0; i < linkRoots.Length; i++) linkRoots[i] = _linkRoots[i].transform;
                _dressing = new RunFloorDressing(battle, this, art, parent, roomRoots, linkRoots);
            }
            ShowFloor(0);
            Refresh();
        }

        public void BindBattle(RunBattle battle)
        {
            _battle = battle;
            _dressing?.BindBattle(battle);
        }

        /// <summary>횃불·샘·함정 그림을 전투 시간만큼 돌린다.</summary>
        public void Tick(float deltaTime) => _dressing?.Tick(deltaTime);
        public void SetHighlights(bool[] rooms) { _highlights = rooms; Refresh(); }

        public int Floor { get; private set; }
        public Vector3 EntranceOutside => ToWorld(_battle.EntrancePosition);
        public Rect RoomRect(int room) => _rooms[room];

        public bool IsVisible(RunPosition position) => _battle.Rooms[position.IsAtEntrance ? _battle.EntryRoom
            : position.IsInRoom ? position.Room : _battle.Links[position.Link].From].Floor == Floor;

        /// <summary>방 내부 중심선, 사방 복도, 북쪽 입구 굴을 평면 좌표로 옮긴다.</summary>
        public Vector3 ToWorld(RunPosition position)
        {
            if (position.IsAtEntrance)
            {
                Rect north = _rooms[_battle.EntryRoom];
                return new Vector3(north.center.x, north.yMax + _battle.Settings.EntranceLength - position.Offset, 0f);
            }
            if (position.IsInRoom)
            {
                Rect rect = _rooms[position.Room];
                return position.Axis == RunAxis.Vertical
                    ? new Vector3(rect.center.x, rect.yMax - position.Offset, 0f)
                    : new Vector3(rect.xMin + position.Offset, rect.center.y, 0f);
            }
            RunLink link = _battle.Links[position.Link];
            Vector3 a = ToWorld(RunPosition.InRoom(link.From, link.Axis, link.FromOffset));
            Vector3 b = ToWorld(RunPosition.InRoom(link.To, link.Axis, link.ToOffset));
            return Vector3.Lerp(a, b, link.Length > 0f ? position.Offset / link.Length : 0f);
        }

        public void ShowFloor(int floor)
        {
            Floor = floor;
            _entrance.SetActive(_battle.Rooms[_battle.EntryRoom].Floor == floor);
            for (int i = 0; i < _roomRoots.Length; i++) _roomRoots[i].SetActive(_battle.Rooms[i].Floor == floor);
            for (int i = 0; i < _linkRoots.Length; i++) _linkRoots[i].SetActive(_battle.Rooms[_battle.Links[i].From].Floor == floor);
            _dressing?.ShowFloor(floor);
        }

        public void FitCamera(Camera camera, float leftShare, float topShare, float bottomShare)
        {
            if (camera == null || !camera.orthographic) return;
            Rect bounds = BoundsOf(Floor);
            float minX = bounds.xMin - 0.5f;
            float maxX = bounds.xMax + 0.5f;
            float minY = bounds.yMin - 0.5f;
            float maxY = Mathf.Max(bounds.yMax + 1.1f, EntranceOutside.y + 2.1f);
            float widthShare = 1f - leftShare - 0.20f;
            float heightShare = 1f - topShare - bottomShare;
            float aspect = Mathf.Max(0.5f, camera.aspect);
            float size = Mathf.Max((maxY - minY) * 0.5f / heightShare, (maxX - minX) * 0.5f / (aspect * widthShare));
            camera.orthographicSize = size;
            float regionX = leftShare + widthShare * 0.5f;
            float regionY = bottomShare + heightShare * 0.5f;
            camera.transform.position = new Vector3((minX + maxX) * 0.5f - (regionX - 0.5f) * 2f * size * aspect,
                (minY + maxY) * 0.5f - (regionY - 0.5f) * 2f * size, camera.transform.position.z);
        }

        public void Refresh()
        {
            if (_dressing != null)
            {
                RefreshDressed();
                return;
            }
            for (int i = 0; i < _rooms.Length; i++)
            {
                RunRoom room = _battle.Rooms[i];
                _borders[i].color = room.IsBroken ? HudPalette.BorderDanger : RunVisualPalette.RoomBorder(room.Kind);
                Color body = RunVisualPalette.RoomFill(room.Kind);
                _bodies[i].color = room.HasSystem && !_battle.IsPowered(i) ? Color.Lerp(body, HudPalette.Deep, 0.75f) : body;
                if (_highlights != null && !_highlights[i])
                {
                    _bodies[i].color = Color.Lerp(_bodies[i].color, HudPalette.Deep, 0.8f);
                    _borders[i].color = Color.Lerp(_borders[i].color, HudPalette.Deep, 0.8f);
                }
            }
            for (int i = 0; i < _doors.Length; i++)
            {
                RunDoor door = _battle.Doors[i];
                bool controlled = door.ControllerRoom >= 0;
                _doors[i].gameObject.SetActive(controlled);
                if (!controlled) continue;
                bool locked = _battle.IsDoorLocked(i);
                // 적이 치는 잠긴 문은 붉게 보인다(2026-11-11 사용자 결정 "멈춤 + 위기 카드", 문 막대 붉게).
                _doors[i].color = locked ? (IsDoorHit(i) ? HudPalette.Danger : HudPalette.DoorLocked)
                    : door.IsBrokenOpen ? HudPalette.BorderDanger : HudPalette.DoorOpen;
                float thickness = locked ? 0.2f : 0.08f;
                _doors[i].transform.localScale = _battle.Links[i].IsVertical
                    ? new Vector3(1.2f, thickness, 1f) : new Vector3(thickness, 1.2f, 1f);
            }
        }

        /// <summary>그림을 입힌 평면도: 꺼짐·강조는 어두운 막, 고장은 붉은 테두리로 덮는다(첫 구현 범위, 2026-11-11 사용자 선택).</summary>
        private void RefreshDressed()
        {
            for (int i = 0; i < _rooms.Length; i++)
            {
                RunRoom room = _battle.Rooms[i];
                float shade = _highlights != null && !_highlights[i] ? 0.7f
                    : room.HasSystem && !_battle.IsPowered(i) ? 0.5f : 0f;
                Color veil = HudPalette.Deep;
                veil.a = shade;
                _bodies[i].color = veil;
                foreach (GameObject edge in _brokenFrames[i]) edge.SetActive(room.IsBroken);
            }
            _dressing.RefreshDoors();
        }

        public bool IsDoorHit(int door)
        {
            foreach (RunCombatant actor in _battle.Combatants)
                if (actor.IsOnField && actor.Side == RunSide.Intruder && actor.Activity == RunActionKind.AttackDoor
                    && !actor.Position.IsInRoom && !actor.Position.IsAtEntrance && actor.Position.Link == door) return true;
            return false;
        }

        private Rect BoundsOf(int floor)
        {
            float left = float.MaxValue;
            float right = float.MinValue;
            float bottom = float.MaxValue;
            float top = float.MinValue;
            for (int i = 0; i < _rooms.Length; i++)
            {
                if (_battle.Rooms[i].Floor != floor) continue;
                Rect rect = _rooms[i];
                left = Mathf.Min(left, rect.xMin);
                right = Mathf.Max(right, rect.xMax);
                bottom = Mathf.Min(bottom, rect.yMin);
                top = Mathf.Max(top, rect.yMax);
            }
            return Rect.MinMaxRect(left, bottom, right, top);
        }

        private void BuildRoom(int index, Transform parent, Dictionary<Collider2D, int> colliders)
        {
            RunRoom room = _battle.Rooms[index];
            Rect rect = _rooms[index];
            GameObject group = new($"Room_{index}_{room.Kind}");
            group.transform.SetParent(parent, false);
            _roomRoots[index] = group;
            Vector3 center = new(rect.center.x, rect.center.y, 0f);
            _borders[index] = _drawing.Quad("Border", center, rect.size, RunVisualPalette.RoomBorder(room.Kind), 0, group.transform);
            BoxCollider2D area = _borders[index].gameObject.AddComponent<BoxCollider2D>();
            area.size = Vector2.one;
            colliders[area] = index;
            _bodies[index] = _drawing.Quad("Body", center, rect.size - new Vector2(0.14f, 0.14f), RunVisualPalette.RoomFill(room.Kind), 1, group.transform);
            if (_brokenFrames == null) return;
            // 그림을 입히면 테두리 사각형은 클릭 판정만 남기고, 몸체는 상태 막(평소 투명)이 된다.
            _borders[index].enabled = false;
            _bodies[index].transform.localScale = new Vector3(rect.width, rect.height, 1f);
            _bodies[index].sortingOrder = 2;
            const float edge = 0.08f;
            Vector3 topLeft = new(rect.xMin, rect.yMax, 0f), topRight = new(rect.xMax, rect.yMax, 0f);
            Vector3 bottomLeft = new(rect.xMin, rect.yMin, 0f), bottomRight = new(rect.xMax, rect.yMin, 0f);
            _brokenFrames[index] = new[]
            {
                _drawing.Line("BrokenTop", topLeft, topRight, edge, HudPalette.BorderDanger, 3, group.transform).gameObject,
                _drawing.Line("BrokenBottom", bottomLeft, bottomRight, edge, HudPalette.BorderDanger, 3, group.transform).gameObject,
                _drawing.Line("BrokenLeft", bottomLeft, topLeft, edge, HudPalette.BorderDanger, 3, group.transform).gameObject,
                _drawing.Line("BrokenRight", bottomRight, topRight, edge, HudPalette.BorderDanger, 3, group.transform).gameObject,
            };
        }

        private void BuildLink(int index, Transform parent, bool drawShapes)
        {
            RunLink link = _battle.Links[index];
            GameObject group = new($"Corridor_{index}");
            group.transform.SetParent(parent, false);
            _linkRoots[index] = group;
            if (!drawShapes) return;
            Vector3 a = ToWorld(RunPosition.InRoom(link.From, link.Axis, link.FromOffset));
            Vector3 b = ToWorld(RunPosition.InRoom(link.To, link.Axis, link.ToOffset));
            float width = _battle.Rooms[link.From].Length * 0.4f;
            _drawing.Line("Border", a, b, width + 0.12f, HudPalette.DungeonFloor, 0, group.transform);
            _drawing.Line("Tunnel", a, b, width, HudPalette.DungeonTunnel, 1, group.transform);
            _doors[index] = _drawing.Quad("Door", ToWorld(_battle.Doors[index].Position),
                new Vector2(0.08f, width), HudPalette.DoorOpen, 6, group.transform);
        }
    }
}

using System.Collections.Generic;
using Code.Run;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.UI.Run
{
    /// <summary>
    /// 평면도에 타일·방 장치·문·함정 번개를 입힌다(2026-11-11 사용자 "이 방향 채택"). 위치와 판정은 <see cref="RunGridView"/>가 맡고,
    /// 여기서는 그림만 바꾼다. 원정 진지는 장치 그림이 정해지지 않아 바닥·벽·횃불만 깐다.
    /// </summary>
    public class RunFloorDressing
    {
        private struct Animated
        {
            public SpriteRenderer Renderer;
            public Sprite[] Frames;
            public int Phase;
        }

        private struct Strike
        {
            public SpriteRenderer Renderer;
            public float Time;
        }

        private RunBattle _battle;
        private readonly RunGridView _grid;
        private readonly RunFloorArt _art;
        private readonly GameObject[] _floorMaps;
        private readonly List<Animated> _animated = new();
        private readonly List<SpriteRenderer>[] _vents;
        private readonly Strike[] _strikes;
        private readonly float[] _lastCooldown;
        private readonly SpriteRenderer[] _doors;
        private readonly Sprite[] _strikeFrames;
        private readonly Sprite _ventIdle, _ventCharged, _doorClosed, _doorOpen;
        private float _clock;

        public RunFloorDressing(RunBattle battle, RunGridView grid, RunFloorArt art, Transform parent,
            Transform[] roomRoots, Transform[] linkRoots)
        {
            _battle = battle;
            _grid = grid;
            _art = art;
            RunFloorArtSO data = art.Data;
            _ventIdle = art.SpriteOf(data.TrapVentIdle);
            _ventCharged = art.SpriteOf(data.TrapVentCharged);
            _doorClosed = art.SpriteOf(data.DoorClosed);
            _doorOpen = art.SpriteOf(data.DoorOpen);
            _strikeFrames = art.SpritesOf(data.StrikeFrames);

            GameObject plan = new("FloorPlan");
            plan.transform.SetParent(parent, false);
            plan.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Grid gridComponent = plan.AddComponent<Grid>();
            gridComponent.cellSize = new Vector3(data.TileWorldSize, data.TileWorldSize, 0f);
            _floorMaps = new GameObject[Mathf.Max(1, battle.FloorCount)];
            for (int floor = 0; floor < _floorMaps.Length; floor++) _floorMaps[floor] = BuildTiles(floor, plan.transform);

            _vents = new List<SpriteRenderer>[battle.Rooms.Count];
            _strikes = new Strike[battle.Rooms.Count];
            _lastCooldown = new float[battle.Rooms.Count];
            for (int i = 0; i < battle.Rooms.Count; i++) DressRoom(i, roomRoots[i]);
            _doors = new SpriteRenderer[battle.Links.Count];
            for (int i = 0; i < battle.Links.Count; i++) _doors[i] = BuildDoor(i, linkRoots[i]);
            BindBattle(battle);
        }

        public void BindBattle(RunBattle battle)
        {
            _battle = battle;
            for (int i = 0; i < _strikes.Length; i++)
            {
                _lastCooldown[i] = i < battle.Rooms.Count ? battle.Rooms[i].TrapCooldown : 0f;
                if (_strikes[i].Renderer != null) _strikes[i].Renderer.gameObject.SetActive(false);
            }
        }

        public void ShowFloor(int floor)
        {
            for (int i = 0; i < _floorMaps.Length; i++) _floorMaps[i].SetActive(i == floor);
        }

        /// <summary>문 그림. 잠기면 닫힌 철문·세운 기둥, 적이 치면 붉게, 부서져 열린 철문은 붉은 열림이다.</summary>
        public void RefreshDoors()
        {
            for (int i = 0; i < _doors.Length; i++)
            {
                SpriteRenderer door = _doors[i];
                if (door == null) continue;
                RunDoor state = _battle.Doors[i];
                bool locked = state.ControllerRoom >= 0 && _battle.IsDoorLocked(i);
                bool hit = locked && _grid.IsDoorHit(i);
                if (_battle.Links[i].IsVertical)
                {
                    door.gameObject.SetActive(state.ControllerRoom >= 0);
                    door.sprite = locked ? _doorClosed : _doorOpen;
                    door.color = hit ? HudPalette.Danger : state.IsBrokenOpen ? HudPalette.BorderDanger : Color.white;
                }
                else
                {
                    door.gameObject.SetActive(locked);
                    door.color = hit ? HudPalette.Danger : Color.white;
                }
            }
        }

        /// <summary>횃불·샘 물결, 함정 장치 충전, 함정 번개. 전투 시간(멈추면 0)으로 돈다.</summary>
        public void Tick(float deltaTime)
        {
            _clock += deltaTime;
            int tick = Mathf.FloorToInt(_clock / _art.Data.FrameSeconds);
            foreach (Animated item in _animated)
            {
                if (item.Frames.Length == 0) continue;
                item.Renderer.sprite = item.Frames[(tick + item.Phase) % item.Frames.Length];
            }
            for (int i = 0; i < _battle.Rooms.Count && i < _vents.Length; i++)
            {
                RunRoom room = _battle.Rooms[i];
                if (_vents[i] != null)
                {
                    Sprite vent = _battle.IsPowered(i) && room.TrapCooldown <= 0f ? _ventCharged : _ventIdle;
                    foreach (SpriteRenderer renderer in _vents[i]) renderer.sprite = vent;
                }
                // 함정은 발동하면 쿨이 다시 가득 찬다. 쿨이 뛰어오른 순간을 발동으로 본다.
                if (room.TrapCooldown > _lastCooldown[i] + 0.5f) StartStrike(i);
                _lastCooldown[i] = room.TrapCooldown;
                TickStrike(i, deltaTime);
            }
        }

        // ── 타일 ──

        private GameObject BuildTiles(int floor, Transform parent)
        {
            RunFloorArtSO data = _art.Data;
            float size = data.TileWorldSize;
            List<Rect> open = new();
            for (int i = 0; i < _battle.Rooms.Count; i++)
                if (_battle.Rooms[i].Floor == floor) open.Add(_grid.RoomRect(i));
            for (int i = 0; i < _battle.Links.Count; i++)
            {
                RunLink link = _battle.Links[i];
                if (_battle.Rooms[link.From].Floor != floor || _battle.Rooms[link.To].Floor != floor) continue;
                Vector3 a = _grid.ToWorld(RunPosition.InRoom(link.From, link.Axis, link.FromOffset));
                Vector3 b = _grid.ToWorld(RunPosition.InRoom(link.To, link.Axis, link.ToOffset));
                open.Add(Corridor(a, b, _battle.Rooms[link.From].Length * 0.4f));
            }
            if (_battle.Rooms[_battle.EntryRoom].Floor == floor)
            {
                Rect north = _grid.RoomRect(_battle.EntryRoom);
                Vector3 outside = _grid.EntranceOutside + new Vector3(0f, data.EntranceExtra, 0f);
                open.Add(Corridor(new Vector3(north.center.x, north.center.y, 0f), outside, _battle.Rooms[_battle.EntryRoom].Length * 0.4f));
            }

            GameObject go = new($"Tiles_{floor}");
            go.transform.SetParent(parent, false);
            Tilemap map = go.AddComponent<Tilemap>();
            TilemapRenderer renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = -10;
            if (open.Count == 0) return go;

            Rect bounds = open[0];
            foreach (Rect rect in open) bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, rect.xMin), Mathf.Min(bounds.yMin, rect.yMin),
                Mathf.Max(bounds.xMax, rect.xMax), Mathf.Max(bounds.yMax, rect.yMax));
            int minX = Mathf.FloorToInt(bounds.xMin / size) - data.WallMargin.x;
            int minY = Mathf.FloorToInt(bounds.yMin / size) - data.WallMargin.y;
            int width = Mathf.CeilToInt(bounds.xMax / size) + data.WallMargin.x - minX;
            int height = Mathf.CeilToInt(bounds.yMax / size) + data.WallMargin.y - minY;
            bool[,] cells = new bool[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2 center = new((minX + x + 0.5f) * size, (minY + y + 0.5f) * size);
                    foreach (Rect rect in open)
                    {
                        if (!rect.Contains(center)) continue;
                        cells[x, y] = true;
                        break;
                    }
                }
            }

            TileBase[] tiles = new TileBase[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int hash = Hash(minX + x, minY + y);
                    RunArtPiece piece = RunFloorTileRules.KindOf(cells, x, y) switch
                    {
                        RunFloorCellKind.Floor => Pick(data.Floors, hash),
                        RunFloorCellKind.LitFloor => Pick(data.LitFloors.Length > 0 ? data.LitFloors : data.Floors, hash),
                        RunFloorCellKind.Face => Pick(data.WallFaces, hash),
                        _ => data.Rim.Length == 9 ? data.Rim[RunFloorTileRules.RimIndex(cells, x, y)] : default,
                    };
                    tiles[y * width + x] = _art.TileOf(piece);
                }
            }
            map.SetTilesBlock(new BoundsInt(minX, minY, 0, width, height, 1), tiles);
            return go;
        }

        private static Rect Corridor(Vector3 a, Vector3 b, float width)
        {
            float half = width * 0.5f;
            bool vertical = Mathf.Abs(a.x - b.x) < Mathf.Abs(a.y - b.y);
            return vertical
                ? Rect.MinMaxRect(Mathf.Min(a.x, b.x) - half, Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x) + half, Mathf.Max(a.y, b.y))
                : Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y) - half, Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y) + half);
        }

        private static int Hash(int x, int y) => (x * 73856093) ^ (y * 19349663);

        private static RunArtPiece Pick(RunArtPiece[] pieces, int hash) =>
            pieces.Length == 0 ? default : pieces[(hash & int.MaxValue) % pieces.Length];

        // ── 방 장치 ──

        private void DressRoom(int index, Transform root)
        {
            RunFloorArtSO data = _art.Data;
            RunRoom room = _battle.Rooms[index];
            Sprite[] torch = _art.SpritesOf(data.TorchFrames);
            for (int i = 0; i < data.TorchCells.Length && torch.Length > 0; i++)
            {
                SpriteRenderer renderer = Place("Torch", torch[0], index, data.TorchCells[i], -4, root);
                _animated.Add(new Animated { Renderer = renderer, Frames = torch, Phase = index * 3 + i * 5 });
            }
            if (_battle.IsRaid) return;

            foreach (RunRoomDecor decor in data.Decor)
                if (decor.Kind == room.Kind) Place("Decor", _art.SpriteOf(decor.Piece), index, decor.Cell, -5, root);
            if (room.Kind == RunRoomKind.HealingSpring && data.SpringFrames.Length > 0)
            {
                Sprite[] water = _art.SpritesOf(data.SpringFrames);
                SpriteRenderer spring = Place("Spring", water[0], index, data.SpringCell, -5, root);
                spring.color = data.SpringTint;
                _animated.Add(new Animated { Renderer = spring, Frames = water });
            }
            if (room.Kind != RunRoomKind.Trap) return;
            _vents[index] = new List<SpriteRenderer>();
            foreach (Vector2 cell in data.TrapVentCells) _vents[index].Add(Place("TrapVent", _ventIdle, index, cell, -5, root));
            if (_strikeFrames.Length == 0) return;
            GameObject strike = new("TrapStrike");
            strike.transform.SetParent(root, false);
            Rect rect = _grid.RoomRect(index);
            strike.transform.position = new Vector3(rect.center.x + data.StrikeOffset.x, rect.center.y + data.StrikeOffset.y, 0f);
            strike.transform.localScale = Vector3.one * data.StrikeScale;
            SpriteRenderer strikeRenderer = strike.AddComponent<SpriteRenderer>();
            strikeRenderer.sprite = _strikeFrames[0];
            strikeRenderer.sortingOrder = 30;
            strike.SetActive(false);
            _strikes[index] = new Strike { Renderer = strikeRenderer, Time = -1f };
        }

        /// <summary>방 왼쪽 위에서 잰 타일 칸(아래로 +)에 그림 왼쪽 위를 맞춘다.</summary>
        private SpriteRenderer Place(string name, Sprite sprite, int room, Vector2 cell, int order, Transform root)
        {
            Rect rect = _grid.RoomRect(room);
            float size = _art.Data.TileWorldSize;
            Vector2 extent = sprite != null ? (Vector2)sprite.bounds.size : Vector2.zero;
            GameObject go = new(name);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(rect.xMin + cell.x * size + extent.x * 0.5f, rect.yMax - cell.y * size - extent.y * 0.5f, 0f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void StartStrike(int room)
        {
            if (_strikes[room].Renderer == null) return;
            _strikes[room].Time = 0f;
            _strikes[room].Renderer.sprite = _strikeFrames[0];
            _strikes[room].Renderer.gameObject.SetActive(true);
        }

        private void TickStrike(int room, float deltaTime)
        {
            Strike strike = _strikes[room];
            if (strike.Renderer == null || strike.Time < 0f) return;
            strike.Time += deltaTime;
            int frame = Mathf.FloorToInt(strike.Time / _art.Data.StrikeFrameSeconds);
            if (frame >= _strikeFrames.Length)
            {
                strike.Time = -1f;
                strike.Renderer.gameObject.SetActive(false);
            }
            else strike.Renderer.sprite = _strikeFrames[frame];
            _strikes[room] = strike;
        }

        // ── 문 ──

        private SpriteRenderer BuildDoor(int index, Transform root)
        {
            RunFloorArtSO data = _art.Data;
            bool vertical = _battle.Links[index].IsVertical;
            Sprite sprite = vertical ? _doorClosed : _art.SpriteOf(data.DoorPost);
            if (sprite == null) return null;
            GameObject go = new(vertical ? "IronDoor" : "DoorPost");
            go.transform.SetParent(root, false);
            go.transform.position = _grid.ToWorld(_battle.Doors[index].Position);
            if (!vertical) go.transform.localScale = Vector3.one * data.DoorPostScale;
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 6;
            go.SetActive(false);
            return renderer;
        }
    }
}

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
        /// <summary>꺼진 방에서 멈추는 그림인가(횃불·샘). 고장 불길은 묶음이 꺼져 있으면 보이지 않을 뿐 늘 돈다.</summary>
        private enum Loop
        {
            Always,
            StopsWhenOff,
        }

        private struct Animated
        {
            public SpriteRenderer Renderer;
            public Sprite[] Frames;
            public int Phase;
            public int Room;
            public Loop Loop;
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
        private readonly Sprite _ventIdle, _ventCharged, _doorClosed, _doorOpen, _torchOff;
        // 방 상태(꺼짐·고장·심장 피해) 그림. 2026-11-12 사용자 목업 승인.
        private readonly List<SpriteRenderer>[] _torches;
        private readonly List<SpriteRenderer>[] _devices;
        private readonly Color[][] _deviceColors;
        private readonly GameObject[] _brokenSets;
        private readonly List<SpriteRenderer> _heartPillars = new();
        private readonly List<GameObject> _heartBlood = new();
        // 꺼진 방의 어두운 막(격자 뷰 몸체, 순서 2) 위에 그려야 고장 불길이 보인다.
        private const int BrokenOrder = 3;
        private readonly int[] _shownState;
        private bool[] _off;
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
            _torchOff = art.SpriteOf(data.TorchOff);

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
            _torches = new List<SpriteRenderer>[battle.Rooms.Count];
            _devices = new List<SpriteRenderer>[battle.Rooms.Count];
            _deviceColors = new Color[battle.Rooms.Count][];
            _brokenSets = new GameObject[battle.Rooms.Count];
            _shownState = new int[battle.Rooms.Count];
            _off = new bool[battle.Rooms.Count];
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
            // 새 전투의 방 상태를 처음부터 다시 그린다.
            for (int i = 0; i < _shownState.Length; i++) _shownState[i] = -1;
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
            RefreshStates();
            int tick = Mathf.FloorToInt(_clock / _art.Data.FrameSeconds);
            foreach (Animated item in _animated)
            {
                if (item.Frames.Length == 0 || item.Loop == Loop.StopsWhenOff && _off[item.Room]) continue;
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
                        _ => data.Rim.Length == 9 ? RimPiece(data, RunFloorTileRules.RimIndex(cells, x, y, data.RimInner.Length == 4)) : default,
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

        private static RunArtPiece RimPiece(RunFloorArtSO data, int index) =>
            index >= RunFloorTileRules.RimInnerStart ? data.RimInner[index - RunFloorTileRules.RimInnerStart] : data.Rim[index];

        private static int Hash(int x, int y) => (x * 73856093) ^ (y * 19349663);

        private static RunArtPiece Pick(RunArtPiece[] pieces, int hash) =>
            pieces.Length == 0 ? default : pieces[(hash & int.MaxValue) % pieces.Length];

        // ── 방 장치 ──

        private void DressRoom(int index, Transform root)
        {
            RunFloorArtSO data = _art.Data;
            RunRoom room = _battle.Rooms[index];
            Sprite[] torch = _art.SpritesOf(data.TorchFrames);
            _torches[index] = new List<SpriteRenderer>();
            _devices[index] = new List<SpriteRenderer>();
            for (int i = 0; i < data.TorchCells.Length && torch.Length > 0; i++)
            {
                SpriteRenderer renderer = Place("Torch", torch[0], index, data.TorchCells[i], -4, root);
                _torches[index].Add(renderer);
                _animated.Add(new Animated { Renderer = renderer, Frames = torch, Phase = index * 3 + i * 5, Room = index, Loop = Loop.StopsWhenOff });
            }
            if (_battle.IsRaid) return;

            foreach (RunRoomDecor decor in data.Decor)
            {
                if (decor.Kind != room.Kind) continue;
                SpriteRenderer renderer = Place("Decor", _art.SpriteOf(decor.Piece), index, decor.Cell, -5, root);
                if (room.Kind == RunRoomKind.Heart) _heartPillars.Add(renderer);
                else _devices[index].Add(renderer);
            }
            if (room.Kind == RunRoomKind.HealingSpring && data.SpringFrames.Length > 0)
            {
                Sprite[] water = _art.SpritesOf(data.SpringFrames);
                SpriteRenderer spring = Place("Spring", water[0], index, data.SpringCell, -5, root);
                spring.color = data.SpringTint;
                _devices[index].Add(spring);
                _animated.Add(new Animated { Renderer = spring, Frames = water, Room = index, Loop = Loop.StopsWhenOff });
            }
            if (room.Kind == RunRoomKind.Heart)
            {
                for (int i = 0; i < data.HeartBlood.Length && i < data.HeartBloodCells.Length; i++)
                {
                    SpriteRenderer blood = Place("HeartBlood", _art.SpriteOf(data.HeartBlood[i]), index, data.HeartBloodCells[i], -7, root);
                    blood.gameObject.SetActive(false);
                    _heartBlood.Add(blood.gameObject);
                }
            }
            if (room.HasSystem) _brokenSets[index] = BuildBrokenSet(index, root);
            if (room.Kind == RunRoomKind.Trap)
            {
                _vents[index] = new List<SpriteRenderer>();
                foreach (Vector2 cell in data.TrapVentCells)
                {
                    SpriteRenderer vent = Place("TrapVent", _ventIdle, index, cell, -5, root);
                    _vents[index].Add(vent);
                    _devices[index].Add(vent);
                }
            }
            _deviceColors[index] = new Color[_devices[index].Count];
            for (int i = 0; i < _devices[index].Count; i++) _deviceColors[index][i] = _devices[index][i].color;
            if (room.Kind != RunRoomKind.Trap) return;
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

        // ── 방 상태 ──

        /// <summary>고장 난 방 바닥의 불길·파편·불씨 묶음. 고장일 때만 켠다.</summary>
        private GameObject BuildBrokenSet(int index, Transform root)
        {
            RunFloorArtSO data = _art.Data;
            GameObject set = new("BrokenSet");
            set.transform.SetParent(root, false);
            for (int i = 0; i < data.BrokenDebris.Length && i < data.BrokenDebrisCells.Length; i++)
                Place("Debris", _art.SpriteOf(data.BrokenDebris[i]), index, data.BrokenDebrisCells[i], BrokenOrder, set.transform);
            Sprite[] flames = _art.SpritesOf(data.BrokenFlameFrames);
            for (int i = 0; i < data.BrokenFlameCells.Length && flames.Length > 0; i++)
            {
                SpriteRenderer flame = Place("Flame", flames[0], index, data.BrokenFlameCells[i], BrokenOrder, set.transform);
                flame.transform.localScale = Vector3.one * data.BrokenFlameScale;
                _animated.Add(new Animated { Renderer = flame, Frames = flames, Phase = index * 2 + i * 3, Room = index, Loop = Loop.Always });
            }
            set.SetActive(false);
            return set;
        }

        /// <summary>
        /// 꺼진 방은 횃불을 불 없는 화로로 바꾸고 장치·샘을 회색으로 멈춘다. 고장 난 방은 불길·파편을 켠다(붉은 테두리는 격자 뷰).
        /// 심장 기둥은 체력에 맞춰 어두워지고, 체력 2/3·1/3 아래에서 핏자국이 늘어난다.
        /// </summary>
        private void RefreshStates()
        {
            if (_battle.IsRaid) return;
            RunFloorArtSO data = _art.Data;
            for (int i = 0; i < _battle.Rooms.Count && i < _shownState.Length; i++)
            {
                RunRoom room = _battle.Rooms[i];
                bool off = room.HasSystem && !_battle.IsPowered(i);
                bool broken = room.HasSystem && room.IsBroken;
                int state = (off ? 1 : 0) | (broken ? 2 : 0);
                _off[i] = off;
                if (state == _shownState[i]) continue;
                _shownState[i] = state;
                foreach (SpriteRenderer torch in _torches[i])
                {
                    // 켜질 때는 다음 장면 넘김이 불꽃 그림으로 되돌린다.
                    if (off && _torchOff != null) torch.sprite = _torchOff;
                }
                Color gray = new(data.OffDeviceGray, data.OffDeviceGray, data.OffDeviceGray, 1f);
                for (int d = 0; d < _devices[i].Count; d++)
                    _devices[i][d].color = off ? _deviceColors[i][d] * gray : _deviceColors[i][d];
                if (_brokenSets[i] != null) _brokenSets[i].SetActive(broken);
            }

            float ratio = Mathf.Clamp01(_battle.HeartHealth / (float)Mathf.Max(1, _battle.Settings.HeartHealth));
            float light = Mathf.Lerp(data.HeartDarkest, 1f, ratio);
            Color pillar = new(light, light, light, 1f);
            foreach (SpriteRenderer renderer in _heartPillars) renderer.color = pillar;
            int bloodShown = ratio < 1f / 3f ? _heartBlood.Count : ratio < 2f / 3f ? Mathf.Min(data.HeartBloodFirstCount, _heartBlood.Count) : 0;
            for (int i = 0; i < _heartBlood.Count; i++)
                if (_heartBlood[i].activeSelf != i < bloodShown) _heartBlood[i].SetActive(i < bloodShown);
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

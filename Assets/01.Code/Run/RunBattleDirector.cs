using System.Collections.Generic;
using System.Text;
using Code.Entities;
using Code.UI;
using Code.UI.Run;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Code.Run
{
    /// <summary>
    /// DungeonRun 씬의 목업 8안 화면 조립과 입력. 격자·예고 그림은 전용 뷰가 맡는다.
    /// 규칙(<see cref="RunBattle"/>)에 흐른 시간을 넘기고 방·사방 복도·문·입구 굴와 인물을 월드에, 장부·목표 막대·시간 조작·
    /// 부하 목록·정보 창은 HUD에 그린다. 조작: 부하(또는 목록 칸)를 누르고 방을 누르면 이동, 방 이름판 칸을 누르면 마력,
    /// Space·1·2로 멈춤·배속. 멈춘 채로도 명령할 수 있다.
    /// </summary>
    public class RunBattleDirector : MonoBehaviour
    {
        [SerializeField] private RunEncounterSO encounter;
        [SerializeField] private RunActor actorPrefab;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform worldRoot;
        [SerializeField] private RunHudView hud;
        [SerializeField] private RunSessionController session;
        [SerializeField] private RectTransform hudCanvas;
        [SerializeField] private RunRoomPanel[] roomPanels = System.Array.Empty<RunRoomPanel>();
        [SerializeField, Tooltip("평면도 타일·장치 그림. 비면 단색 도형으로 그린다.")] private RunFloorArtSO floorArt;

        [SerializeField, Min(0.5f)] private float actorScale = 0.65f;
        [SerializeField, Min(0.3f), Tooltip("머리 위 표시 높이(인물 로컬 단위).")] private float headHeight = 1.1f;
        [SerializeField, Range(0f, 0.4f), Tooltip("왼쪽 부하 목록이 가리는 화면 몫.")] private float leftHudFraction = 0.12f;
        [SerializeField, Range(0f, 0.4f), Tooltip("위 HUD가 가리는 화면 몫.")] private float topHudFraction = 0.1f;

        private const float RefreshSeconds = 0.2f;
        private const float DownVisibleSeconds = 0.8f;

        private readonly List<RunActor> _actors = new();
        private readonly List<RunActorOverlay> _overlays = new();
        private readonly List<SpriteRenderer> _targetLines = new();
        private readonly List<Sprite> _faces = new();
        private readonly List<RunCombatant> _minions = new();
        private readonly Dictionary<Collider2D, RunCombatant> _actorByCollider = new();
        private readonly Dictionary<Collider2D, int> _roomByCollider = new();
        private readonly StringBuilder _text = new();
        private RunGridView _grid;
        private RunWorldPrimitives _drawing;
        private RunFloorArt _floorArt;
        private RunTelegraphView _telegraphs;
        private readonly Collider2D[] _pickHits = new Collider2D[32];
        private ContactFilter2D _pickFilter;
        private readonly List<RunCombatant> _visibleMinions = new();
        private System.Action<int>[] _pipHandlers;
        private int _shownWaiting = -1;
        private int _shownFloorEnemies = -1;
        private int[] _lastHealth;
        private int[] _lastStrikes;
        private int[] _lastGoal;
        private float[] _downSince;
        private bool[] _targeted;
        private RunBattle _battle;
        private PointerEventData _dropPointer;
        private readonly List<RaycastResult> _dropUiHits = new(32);
        private int _dragMinion = -1;
        private Vector2 _dragStart;
        private bool _dragging;
        private RunCombatant _selected;
        private RunCombatant _hovered;
        private int _infoRoom = -1;
        private int _hoveredRoom = -1;
        private bool _paused;
        private int _speed = 1;
        private Sprite _square;
        private Vector2Int _lastScreen;
        private float _refreshTimer;
        private int _shownGold = -1, _shownHeart = -1, _shownManaUse = -1, _shownManaCap = -1, _shownRemaining = -1, _shownSecond = -1;
        private RunBattleOutcome _shownOutcome = (RunBattleOutcome)(-1);
        private Transform _battleVisuals;
        private bool _shownCanContinue;
        private string _infoTitle, _infoBody, _tooltipText;

        private void Start()
        {
            if (encounter == null || actorPrefab == null || hud == null || session == null)
            {
                Debug.LogError("RunBattleDirector: 전투 구성·인물 프리팹·HUD 배선이 비었습니다.", this);
                enabled = false;
                return;
            }
            if (worldCamera == null) worldCamera = Camera.main;
            _drawing = new RunWorldPrimitives(worldRoot != null ? worldRoot : transform);
            _square = _drawing.Square;
            if (floorArt != null) _floorArt = new RunFloorArt(floorArt);
            _pickFilter = new ContactFilter2D().NoFilter();

            session.Prepare(encounter);
            _battle = session.Battle;
            _dropPointer = EventSystem.current != null ? new PointerEventData(EventSystem.current) : null;
            session.Started += OnBattleStarted;
            session.Prepared += OnBattleStarted;
            session.PlacementChanged += OnPlacementChanged;
            session.MinionSelected += OnPreparationSelected;
            session.MinionDropped += DropMinion;
            session.CrisisStarted += OnCrisisStarted;
            _paused = true;
            BuildBattleVisuals();
            FitCamera();
            BindPanels();
            hud.PauseClicked += TogglePause;
            hud.NextEncounterClicked += session.OpenNextEncounter;
            hud.SpeedClicked += SetSpeed;
            hud.RosterClicked += OnRosterClicked;
            hud.FloorClicked += OnFloorClicked;
            LayoutPanels();
            ApplyTime();
            RefreshSlow();
        }

        private void OnDestroy()
        {
            if (hud == null) return;
            if (session != null)
            {
                session.Started -= OnBattleStarted;
                session.Prepared -= OnBattleStarted;
                session.PlacementChanged -= OnPlacementChanged;
                session.MinionSelected -= OnPreparationSelected;
                session.MinionDropped -= DropMinion;
                session.CrisisStarted -= OnCrisisStarted;
            }
            hud.PauseClicked -= TogglePause;
            if (session != null) hud.NextEncounterClicked -= session.OpenNextEncounter;
            hud.SpeedClicked -= SetSpeed;
            hud.RosterClicked -= OnRosterClicked;
            hud.FloorClicked -= OnFloorClicked;
            if (_pipHandlers != null)
            {
                for (int i = 0; i < _pipHandlers.Length; i++)
                {
                    if (roomPanels[i] != null) roomPanels[i].PipClicked -= _pipHandlers[i];
                }
            }
            _drawing?.Dispose();
            _floorArt?.Dispose();
        }

        private void Update()
        {
            if (_battle == null) return;
            Vector2Int screen = new(Screen.width, Screen.height);
            if (screen != _lastScreen) LayoutPanels();

            HandleKeys();
            // 멈춤은 timeScale 0이라 흐른 시간이 0이다. 프레임이 끊겨도 한 번에 너무 많이 가지 않게 자른다.
            if (_battle.Outcome == RunBattleOutcome.Ongoing && !Code.UI.Popup.PopupController.IsOpen)
                session.Advance(Mathf.Min(Time.deltaTime, 0.1f * _speed));
            session.RefreshDisplay(Time.unscaledDeltaTime);
            HandlePointer();

            RefreshActors();
            _grid.Refresh();
            _grid.Tick(Time.deltaTime);
            _telegraphs.Refresh(Time.unscaledDeltaTime);
            RefreshFast();
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = RefreshSeconds;
                RefreshSlow();
            }
        }

        // ── 입력 ──

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
            if (keyboard.digit1Key.wasPressedThisFrame) SetSpeed(1);
            if (keyboard.digit2Key.wasPressedThisFrame) SetSpeed(2);
        }

        private void OnBattleStarted()
        {
            RunBattle next = session.Battle;
            bool rebuild = _battle.IsRaid != next.IsRaid || _actors.Count != next.Combatants.Count;
            for (int i = 0; !rebuild && i < next.Combatants.Count; i++)
                rebuild = _battle.Combatants[i].Definition != next.Combatants[i].Definition
                    || _battle.Combatants[i].Affinity != next.Combatants[i].Affinity;
            _battle = next;
            if (rebuild) BuildBattleVisuals();
            _grid.BindBattle(_battle);
            _grid.SetHighlights(null);
            _actorByCollider.Clear();
            _minions.Clear();
            for (int i = 0; i < _actors.Count; i++)
            {
                RunCombatant actor = _battle.Combatants[i];
                if (actor.IsDown)
                {
                    _actors[i].gameObject.SetActive(false);
                    _downSince[i] = float.NegativeInfinity;
                }
                else
                {
                    _actors[i].Bind(actor);
                    _actorByCollider[_actors[i].GetComponent<BoxCollider2D>()] = actor;
                    _downSince[i] = -1f;
                }
                _lastHealth[i] = actor.Health;
                _overlays[i].SetHealth(actor.Health / (float)actor.MaxHealth);
                _lastStrikes[i] = 0;
                _lastGoal[i] = int.MinValue;
                if (actor.Side == RunSide.Minion) _minions.Add(actor);
            }
            _selected = null;
            _infoRoom = -1;
            _hovered = null;
            _hoveredRoom = -1;
            _tooltipText = null;
            hud.ShowTooltip(null, Vector2.zero);
            hud.ShowBanner(null);
            _shownOutcome = (RunBattleOutcome)(-1);
            _shownGold = _shownHeart = _shownManaUse = _shownManaCap = _shownRemaining = _shownSecond = -1;
            _shownFloorEnemies = _shownWaiting = -1;
            _telegraphs.BindBattle(_battle);
            _paused = session.IsPreparing;
            _speed = 1;
            LayoutPanels();
            ApplyTime();
            RefreshActors();
            RefreshSlow();
        }

        private void OnPlacementChanged()
        {
            RefreshActors();
            RefreshSlow();
            _grid.Refresh();
        }

        private void OnPreparationSelected(int index)
        {
            _selected = _minions[index];
            RefreshActors();
        }

        private void DropMinion(int index, Vector2 point)
        {
            session.EndDrag();
            if (!session.IsPreparing) return;
            if (_dropPointer != null)
            {
                _dropPointer.position = point;
                _dropUiHits.Clear();
                EventSystem.current.RaycastAll(_dropPointer, _dropUiHits);
                if (_dropUiHits.Count > 0) return;
            }
            Pick(point, out _, out int room);
            if (room >= 0) session.Place(index, room);
        }

        private void HandlePreparationPointer(Mouse mouse, Vector2 point, bool overUi)
        {
            if (mouse.leftButton.wasPressedThisFrame && !overUi)
            {
                Pick(point, out RunCombatant actor, out int room);
                if (actor != null && actor.Side == RunSide.Minion)
                {
                    session.Select(actor.Id);
                    _dragMinion = actor.Id;
                    _dragStart = point;
                }
                else if (room >= 0 && _selected != null) session.Place(_selected.Id, room);
            }
            if (_dragMinion < 0) return;
            if (mouse.leftButton.isPressed && (point - _dragStart).sqrMagnitude > 36f)
            {
                _dragging = true;
                session.Drag(point);
            }
            if (!mouse.leftButton.wasReleasedThisFrame) return;
            if (_dragging) DropMinion(_dragMinion, point);
            _dragMinion = -1;
            _dragging = false;
            session.EndDrag();
        }

        private void OnCrisisStarted()
        {
            _paused = true;
            ApplyTime();
            _grid.SetHighlights(session.Highlighted);
            session.LayoutCrisis(worldCamera, _grid);
            RefreshSlow();
        }

        private void HandlePointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pointer = mouse.position.ReadValue();
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (session.IsPreparing)
            {
                HandlePreparationPointer(mouse, pointer, overUi);
                return;
            }
            RunCombatant hovered = null;
            int hoveredRoom = -1;
            if (!overUi) Pick(pointer, out hovered, out hoveredRoom);
            if (overUi)
            {
                hovered = null;
                hoveredRoom = -1;
            }
            if (hovered != _hovered || hoveredRoom != _hoveredRoom)
            {
                _hovered = hovered;
                _hoveredRoom = hoveredRoom;
                _tooltipText = Describe(hovered, hoveredRoom);
            }
            hud.ShowTooltip(_tooltipText, pointer);

            if (_battle.Outcome != RunBattleOutcome.Ongoing) return;
            if (mouse.rightButton.wasPressedThisFrame && !overUi)
            {
                _selected = null;
                _infoRoom = -1;
                RefreshSlow();
                return;
            }
            if (!mouse.leftButton.wasPressedThisFrame || overUi) return;
            if (hovered != null && hovered.Side == RunSide.Minion)
            {
                _selected = hovered;
                _infoRoom = -1;
            }
            else if (hoveredRoom >= 0)
            {
                if (_selected != null) _battle.OrderMove(_selected, hoveredRoom);
                else _infoRoom = hoveredRoom;
            }
            RefreshSlow();
        }

        /// <summary>마우스 아래의 인물(먼저)과 방. 인물이 방 위에 겹쳐 있다.</summary>
        private void Pick(Vector2 pointer, out RunCombatant combatant, out int room)
        {
            combatant = null;
            room = -1;
            Vector3 world = worldCamera.ScreenToWorldPoint(pointer);
            int count = Physics2D.OverlapPoint(world, _pickFilter, _pickHits);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _pickHits[i];
                if (_actorByCollider.TryGetValue(hit, out RunCombatant c) && c.IsOnField) combatant ??= c;
                else if (_roomByCollider.TryGetValue(hit, out int r)) room = r;
            }
        }

        private void OnRosterClicked(int index)
        {
            if (index < 0 || index >= _visibleMinions.Count || !_visibleMinions[index].IsActive) return;
            _selected = _visibleMinions[index];
            _infoRoom = -1;
            RefreshSlow();
        }

        private void OnPipClicked(int room, int pip)
        {
            if (_battle.Outcome != RunBattleOutcome.Ongoing) return;
            int current = _battle.Rooms[room].Mana;
            int wanted = current == pip + 1 ? pip : pip + 1;
            session.ChangeMana(room, wanted);
            _grid.Refresh();
            RefreshSlow();
        }

        private void TogglePause()
        {
            if (session.IsPreparing || session.IsResult) return;
            if (session.IsCrisis) { session.Resume(); _grid.SetHighlights(null); RefreshSlow(); }
            _paused = !_paused;
            ApplyTime();
        }

        private void SetSpeed(int speed)
        {
            if (session.IsPreparing || session.IsResult) return;
            // 위기는 계속 조작으로만 닫는다. 배속 버튼으로 건너뛰지 않는다.
            if (session.IsCrisis) return;
            _speed = Mathf.Clamp(speed, 1, 2);
            _paused = false;
            ApplyTime();
        }

        /// <summary>멈춤·배속을 timeScale로 건다. 그림 애니메이션도 함께 멈춘다(프로젝트 기본 timeScale은 0이다).</summary>
        private void ApplyTime()
        {
            Time.timeScale = session.IsPreparing || _paused ? 0f : _speed;
            hud.ShowTimeControls(!session.IsPreparing && _paused, _speed);
        }

        // ── 평면도 ──

        private void BuildBattleVisuals()
        {
            if (_battleVisuals != null)
            {
                _battleVisuals.gameObject.SetActive(false);
                Destroy(_battleVisuals.gameObject);
            }
            _actors.Clear();
            _faces.Clear();
            _overlays.Clear();
            _targetLines.Clear();
            _minions.Clear();
            _actorByCollider.Clear();
            _roomByCollider.Clear();
            _battleVisuals = new GameObject("BattleVisuals").transform;
            _battleVisuals.SetParent(worldRoot != null ? worldRoot : transform, false);
            _grid = new RunGridView(_battle, _drawing, _battleVisuals, _roomByCollider, _floorArt);
            BuildActors();
            _telegraphs = new RunTelegraphView(_battle, _grid, _drawing, _actors, _battleVisuals);
        }

        private Vector3 PathToWorld(RunPosition position) => _grid.ToWorld(position);
        private float RoomTopY(int room) => _grid.RoomRect(room).yMax;
        // 함정 조작·상성 띠를 걷어 아래 몫을 줄였다(2026-11-10 사용자 C 목업 승인). 원정은 아래 줄 방 이름을 방 밑에 달아 조금 더 둔다.
        private void FitCamera() => _grid.FitCamera(worldCamera, session.IsPreparing ? 0.23f : leftHudFraction, topHudFraction,
            session.IsPreparing ? 0.04f : _battle.IsRaid ? 0.09f : 0.06f);

        private int ActorFloor(RunCombatant actor) => _battle.Rooms[actor.Position.IsAtEntrance ? _battle.EntryRoom : actor.Position.IsInRoom
            ? actor.Position.Room : _battle.Links[actor.Position.Link].From].Floor;

        private void OnFloorClicked(int floor)
        {
            if (floor < 0 || floor >= _battle.FloorCount) return;
            hud.ShowFloorNotice(string.Empty);
            _grid.ShowFloor(floor);
            _shownFloorEnemies = -1;
            LayoutPanels();
            RefreshSlow();
        }

        private void BuildActors()
        {
            int count = _battle.Combatants.Count;
            _lastHealth = new int[count];
            _lastStrikes = new int[count];
            _lastGoal = new int[count];
            _downSince = new float[count];
            _targeted = new bool[count];
            foreach (RunCombatant combatant in _battle.Combatants)
            {
                RunActor actor = Instantiate(actorPrefab, _battleVisuals);
                actor.name = $"Actor_{combatant.Id}_{combatant.Definition.DisplayName}";
                actor.transform.localScale = Vector3.one * actorScale;
                actor.transform.position = ActorPosition(combatant);
                actor.Bind(combatant);
                BoxCollider2D hitBox = actor.GetComponent<BoxCollider2D>();
                if (hitBox == null)
                {
                    hitBox = actor.gameObject.AddComponent<BoxCollider2D>();
                    hitBox.size = new Vector2(0.6f, 0.9f);
                    hitBox.offset = new Vector2(0f, 0.45f);
                }
                _actorByCollider[hitBox] = combatant;
                _actors.Add(actor);

                GameObject overlayRoot = new("Overlay");
                overlayRoot.transform.SetParent(actor.transform, false);
                RunActorOverlay overlay = overlayRoot.AddComponent<RunActorOverlay>();
                // 그림 사각형에는 위쪽 여백이 커서 경계로 재면 표시가 머리에서 떠 버린다. 실제 머리 높이에 맞춘 값을 쓴다.
                overlay.Build(_square, combatant.Side == RunSide.Minion, actor.HeadHeight(headHeight) + 0.08f, _drawing.Dot, RunVisualPalette.Affinity(combatant.Affinity));
                _overlays.Add(overlay);
                _faces.Add(FaceOf(combatant));

                for (int dash = 0; dash < 6; dash++)
                {
                    SpriteRenderer line = Quad($"TargetLine_{combatant.Id}_{dash}", Vector3.zero, Vector2.one, HudPalette.TargetLine, 35);
                    line.gameObject.SetActive(false);
                    _targetLines.Add(line);
                }

                _lastHealth[combatant.Id] = combatant.Health;
                _lastStrikes[combatant.Id] = combatant.StrikeCount;
                _lastGoal[combatant.Id] = int.MinValue;
                _downSince[combatant.Id] = -1f;
                if (combatant.Side == RunSide.Minion) _minions.Add(combatant);
            }
        }

        private void BindPanels()
        {
            _pipHandlers = new System.Action<int>[roomPanels.Length];
            for (int i = 0; i < roomPanels.Length && i < _battle.Rooms.Count; i++)
            {
                if (roomPanels[i] == null) continue;
                int room = i;
                _pipHandlers[i] = pip => OnPipClicked(room, pip);
                roomPanels[i].PipClicked += _pipHandlers[i];
            }
        }

        private static Sprite FaceOf(RunCombatant combatant)
        {
            CharacterAnimationSetSO set = combatant.Definition.ResolveAnimationSet();
            if (set == null) return null;
            Sprite[] frames = set.GetFrames(CharacterClip.Idle);
            return frames != null && frames.Length > 0 ? frames[0] : null;
        }

        private SpriteRenderer Quad(string name, Vector3 position, Vector2 size, Color color, int order) =>
            _drawing.Quad(name, position, size, color, order, _battleVisuals);

        // ── 이름판 자리 ──

        private void LayoutPanels()
        {
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (_battle == null || hudCanvas == null || worldCamera == null) return;
            FitCamera();
            for (int i = 0; i < roomPanels.Length; i++)
            {
                RunRoomPanel panel = roomPanels[i];
                if (panel == null) continue;
                bool used = i < _battle.Rooms.Count && _battle.Rooms[i].Floor == _grid.Floor;
                panel.gameObject.SetActive(used);
                if (!used) continue;
                if (panel.Footer != null) panel.Footer.gameObject.SetActive(false);
                Rect rect = _grid.RoomRect(i);
                // 원정 진지 아래 줄 방은 위로 난 복도에 이름이 걸려 걷는 부하를 가리므로 방 아래에 단다(2026-11-10 사용자 목업 승인).
                bool below = _battle.IsRaid && HasVerticalLink(i, true) && !HasVerticalLink(i, false);
                Place(panel.Header, new Vector3(rect.center.x - (i == _battle.EntryRoom ? 2.7f : 0f), below ? rect.yMin - 0.15f : rect.yMax + 1.2f, 0f), rect.width);
            }
            session.LayoutCrisis(worldCamera, _grid);
            // 함정 조작은 아래 띠 대신 함정 방 오른쪽 위에 붙인다(2026-11-10 사용자 C 목업 승인).
            if (session.TrapPanel != null && session.TrapRoom >= 0 && session.TrapRoom < _battle.Rooms.Count
                && _battle.Rooms[session.TrapRoom].Floor == _grid.Floor)
            {
                Rect trap = _grid.RoomRect(session.TrapRoom);
                PlaceCorner(session.TrapPanel, new Vector3(trap.xMax + 0.25f, trap.yMax, 0f));
            }
            Place(hud.EntryWaitingRect, _grid.EntranceOutside + new Vector3(2.1f, 0.5f, 0f), 3.8f);
        }

        /// <summary>월드 점에 패널의 왼쪽 위 모서리를 맞춘다. 크기는 그대로 둔다.</summary>
        private void PlaceCorner(RectTransform rect, Vector3 world)
        {
            Vector2 screen = worldCamera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hudCanvas, screen, null, out Vector2 local);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = local;
        }

        private bool HasVerticalLink(int room, bool above)
        {
            float y = _grid.RoomRect(room).center.y;
            foreach (RunLink link in _battle.Links)
            {
                int other = link.From == room ? link.To : link.To == room ? link.From : -1;
                if (other < 0 || _battle.Rooms[other].Floor != _battle.Rooms[room].Floor) continue;
                float otherY = _grid.RoomRect(other).center.y;
                if (above ? otherY > y + 0.01f : otherY < y - 0.01f) return true;
            }
            return false;
        }

        /// <summary>월드 점을 HUD 좌표로 옮겨 띠의 위 가장자리를 맞추고, 너비를 방 너비에 맞춘다.</summary>
        private void Place(RectTransform rect, Vector3 world, float width)
        {
            if (rect == null) return;
            Vector2 screen = worldCamera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hudCanvas, screen, null, out Vector2 local);
            Vector2 edge = worldCamera.WorldToScreenPoint(world + new Vector3(width * 0.5f - 0.1f, 0f, 0f));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hudCanvas, edge, null, out Vector2 localEdge);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = local;
            rect.sizeDelta = new Vector2((localEdge.x - local.x) * 2f, rect.sizeDelta.y);
        }

        // ── 매 프레임 ──

        /// <summary>
        /// 같은 자리에 선 인물이 겹치지 않게 번호로 조금씩 비켜 세운다. 입구 밖에서 기다리는 적은 굴 옆에 간격을 두고 줄을 선다.
        /// </summary>
        private RunPosition DisplayPosition(RunCombatant actor) => session.IsPreparing && actor.Side == RunSide.Minion
            ? _battle.CenterOf(session.Deployment.RoomOf(actor.Id)) : actor.Position;

        private Vector3 ActorPosition(RunCombatant combatant)
        {
            if (combatant.IsActive && !combatant.HasEntered)
            {
                int waiting = 0;
                for (int i = 0; i < _battle.Combatants.Count; i++)
                {
                    RunCombatant other = _battle.Combatants[i];
                    if (other.Id < combatant.Id && other.IsActive && !other.HasEntered) waiting++;
                }
                return _grid.EntranceOutside + new Vector3(-0.7f - waiting * 0.7f, 1.1f, 0f);
            }
            Vector3 world = PathToWorld(DisplayPosition(combatant));
            int rank = 0;
            int neighbors = 1;
            for (int i = 0; i < _battle.Combatants.Count; i++)
            {
                RunCombatant other = _battle.Combatants[i];
                if (other == combatant || !other.IsOnField || ActorFloor(other) != ActorFloor(combatant)) continue;
                if ((PathToWorld(DisplayPosition(other)) - world).sqrMagnitude > 0.12f) continue;
                neighbors++;
                if (other.Id < combatant.Id) rank++;
            }
            float spread = neighbors > 1 ? (rank / (float)(neighbors - 1) - 0.5f) * 0.8f : 0f;
            RunPosition position = DisplayPosition(combatant);
            bool vertical = position.IsAtEntrance || (position.IsInRoom ? position.Axis == RunAxis.Vertical : _battle.Links[position.Link].IsVertical);
            if (position.IsInRoom)
                return world + new Vector3(spread, neighbors > 1 ? (rank % 2 == 0 ? -0.22f : 0.22f) : 0f, 0f);
            return world + (vertical ? Vector3.right : Vector3.up) * spread;
        }

        private void RefreshActors()
        {
            IReadOnlyList<RunCombatant> all = _battle.Combatants;
            for (int i = 0; i < all.Count; i++) _targeted[i] = false;

            for (int i = 0; i < all.Count; i++)
            {
                RunCombatant c = all[i];
                RunActor actor = _actors[i];
                RunActorOverlay overlay = _overlays[i];

                if (c.IsDown && _downSince[i] == -1f) _downSince[i] = Time.time;
                bool visible = (!session.IsPreparing || c.Side == RunSide.Minion) && ActorFloor(c) == _grid.Floor && (c.IsActive || (c.IsDown && Time.time - _downSince[i] < DownVisibleSeconds));
                if (actor.gameObject.activeSelf != visible) actor.gameObject.SetActive(visible);

                int change = c.Health - _lastHealth[i];
                if (change != 0)
                {
                    overlay.PopNumber(change);
                    actor.ShowHealth(c.Health);
                    overlay.SetHealth(c.Health / (float)c.MaxHealth);
                    _lastHealth[i] = c.Health;
                }
                if (!visible)
                {
                    HideTargetLine(i);
                    continue;
                }

                Vector3 position = ActorPosition(c);
                actor.transform.position = position;
                actor.FaceTowards(c.Facing);

                if (c.StrikeCount != _lastStrikes[i])
                {
                    _lastStrikes[i] = c.StrikeCount;
                    actor.PlayAttack();
                }

                SkillReadiness(c, overlay);
                overlay.SetSelected(c == _selected);
                if (c.Side == RunSide.Intruder)
                {
                    int goal = c.IsOnField ? (c.IsEscaping ? -2 : _battle.GoalRoomOf(c)) : -1;
                    if (goal != _lastGoal[i])
                    {
                        _lastGoal[i] = goal;
                        overlay.SetIntent(goal >= 0 ? $"→ {RunBattleText.RoomName(_battle.Rooms[goal].Kind)}" : goal == -2 ? "→ 굴 출구" : string.Empty, c.Id % 2 == 1);
                    }
                }
                ShowTargetLine(c, i);
            }
            for (int i = 0; i < all.Count; i++) _overlays[i].SetTargeted(_targeted[i]);
        }

        private static void SkillReadiness(RunCombatant c, RunActorOverlay overlay)
        {
            if (c.Definition.Skill == null || !c.IsOnField)
            {
                overlay.SetSkill(-1f);
                return;
            }
            float ratio = c.SkillCooldownTotal > 0f ? 1f - c.SkillCooldownLeft / c.SkillCooldownTotal : 1f;
            overlay.SetSkill(ratio);
        }

        /// <summary>적이 노리는 부하에 붉은 선과 표시를 단다.</summary>
        private void ShowTargetLine(RunCombatant c, int index)
        {
            RunCombatant target = c.Target;
            bool show = c.Side == RunSide.Intruder && c.IsOnField && target != null && target.IsOnField
                && ActorFloor(c) == _grid.Floor && ActorFloor(target) == _grid.Floor
                && (c.Activity == RunActionKind.Attack || c.Activity == RunActionKind.Skill);
            if (!show)
            {
                HideTargetLine(index);
                return;
            }
            _targeted[target.Id] = true;
            float lift = headHeight * actorScale * 0.75f;
            Vector3 from = _actors[index].transform.position + Vector3.up * lift;
            Vector3 to = _actors[target.Id].transform.position + Vector3.up * lift;
            for (int dash = 0; dash < 6; dash++)
            {
                SpriteRenderer line = _targetLines[index * 6 + dash];
                if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);
                RunWorldPrimitives.Stretch(line.transform, Vector3.Lerp(from, to, dash / 6f), Vector3.Lerp(from, to, (dash + 0.55f) / 6f), 0.035f);
            }
        }

        private void HideTargetLine(int index)
        {
            for (int dash = 0; dash < 6; dash++)
            {
                GameObject line = _targetLines[index * 6 + dash].gameObject;
                if (line.activeSelf) line.SetActive(false);
            }
        }

        /// <summary>값이 바뀐 것만 다시 쓴다(장부·목표 막대·시계·결과·정보 창 자리).</summary>
        private void RefreshFast()
        {
            int manaUse = _battle.ManaInUse;
            if (_battle.VaultGold != _shownGold || _battle.HeartHealth != _shownHeart || manaUse != _shownManaUse
                || _battle.ManaCapacity != _shownManaCap)
            {
                if (_shownHeart >= 0 && _battle.HeartHealth < _shownHeart && worldCamera != null)
                    worldCamera.transform.DOShakePosition(0.25f, new Vector3(0.15f, 0.1f, 0f)).SetUpdate(true).SetLink(worldCamera.gameObject);
                _shownGold = _battle.VaultGold;
                _shownHeart = _battle.HeartHealth;
                _shownManaUse = manaUse;
                _shownManaCap = _battle.ManaCapacity;
                // 방 수·"다 못 켬"은 준비 화면이 이미 알려 줘서 전투 중 위 띠에서는 뺀다(2026-11-10 사용자 목업 승인).
                hud.ShowLedger($"금화 {_shownGold}   심장 {_shownHeart}/{_battle.Settings.HeartHealth}   마력 {_shownManaUse}/{_shownManaCap}");
            }

            int total = 0, remaining = 0, onFloor = 0, waiting = 0;
            for (int i = 0; i < _battle.Combatants.Count; i++)
            {
                RunCombatant c = _battle.Combatants[i];
                if (c.Side != RunSide.Intruder) continue;
                total++;
                if (c.IsActive && !c.HasEntered) waiting++;
                if (c.IsActive) remaining++;
                if (c.IsOnField && ActorFloor(c) == _grid.Floor) onFloor++;
            }
            if (waiting != _shownWaiting)
            {
                _shownWaiting = waiting;
                hud.ShowEntryWaiting($"밖에서 대기 {waiting}");
            }
            if (remaining != _shownRemaining)
            {
                _shownRemaining = remaining;
                hud.ShowGoal(_battle.IsRaid ? $"원정 · 진지 대장 처치 · 남은 적 {remaining}/{total}"
                    : $"침입 파티 · 남은 적 {remaining}/{total}", total > 0 ? remaining / (float)total : 0f);
            }

            if (onFloor != _shownFloorEnemies)
            {
                _shownFloorEnemies = onFloor;
                hud.ShowFloor($"{_grid.Floor + 1}층 · 적 {onFloor}", _grid.Floor, _battle.FloorCount);
            }
            int second = Mathf.FloorToInt(_battle.Elapsed);
            if (second != _shownSecond)
            {
                _shownSecond = second;
                float seconds = _battle.Settings.TimeLimitSeconds;
                int limit = float.IsInfinity(seconds) ? 0 : Mathf.RoundToInt(seconds);
                hud.ShowClock(limit == 0 ? $"{second / 60:00}:{second % 60:00}"
                    : $"{second / 60:00}:{second % 60:00} / {limit / 60:00}:{limit % 60:00}");
            }

            if (_battle.Outcome != _shownOutcome || _shownCanContinue != session.CanPrepareNext)
            {
                _shownOutcome = _battle.Outcome;
                _shownCanContinue = session.CanPrepareNext;
                hud.ShowBanner(BuildResultText(), _shownCanContinue, session.NextLabel);
            }

            if (_selected != null && !_selected.IsActive) _selected = null;
            Vector2 anchor = Vector2.zero;
            if (_selected != null && !session.IsPreparing)
                anchor = worldCamera.WorldToScreenPoint(_actors[_selected.Id].transform.position + new Vector3(0.4f, headHeight * actorScale, 0f));
            else if (_infoRoom >= 0)
                anchor = worldCamera.WorldToScreenPoint(new Vector3(_grid.RoomRect(_infoRoom).xMax, RoomTopY(_infoRoom), 0f));
            float health = _selected != null ? _selected.Health / (float)_selected.MaxHealth : -1f;
            float skill = -1f;
            if (_selected != null && _selected.Definition.Skill != null)
                skill = _selected.SkillCooldownTotal > 0f ? 1f - _selected.SkillCooldownLeft / _selected.SkillCooldownTotal : 1f;
            hud.ShowInfo(!session.IsPreparing && (_selected != null || _infoRoom >= 0) ? _infoTitle : null, _infoBody, health, skill, anchor);
        }

        /// <summary>글이 많이 드는 표시(방 이름판·부하 목록·정보 창 글·설명 카드)는 0.2초마다 다시 쓴다.</summary>
        private void RefreshSlow()
        {
            for (int i = 0; i < _battle.Rooms.Count && i < roomPanels.Length; i++)
            {
                if (roomPanels[i] == null) continue;
                RunRoom room = _battle.Rooms[i];
                (string status, Color color) = RoomStatus(room);
                float fill = room.HasSystem ? room.Durability / room.MaxDurability
                    : room.Kind == RunRoomKind.Heart ? _battle.HeartHealth / (float)_battle.Settings.HeartHealth : 1f;
                bool showBar = room.HasSystem || room.Kind == RunRoomKind.Heart;
                roomPanels[i].Show(session.RoomName(i), status, color, fill, showBar, room.Mana, room.MaxMana);
            }

            _visibleMinions.Clear();
            for (int i = 0; i < _minions.Count; i++)
                if (ActorFloor(_minions[i]) == _grid.Floor) _visibleMinions.Add(_minions[i]);
            for (int i = 0; i < hud.RosterCapacity; i++)
            {
                if (i >= _visibleMinions.Count)
                {
                    hud.ShowRosterCard(i, false, null, null, null, 0f, false, false, HudPalette.TextMuted);
                    continue;
                }
                RunCombatant m = _visibleMinions[i];
                string danger = session.DangerLabel(m.Id);
                // 방 이름은 세션에서 받는다. 원정 진지는 방 종류가 본진과 같아도 이름이 다르다(대장실·옆 통로).
                int room = _battle.RoomOf(m);
                string place = danger ?? (m.IsDown ? "쓰러짐" : room >= 0 ? session.RoomName(room) : "복도");
                hud.ShowRosterCard(i, true, _faces[m.Id], m.Definition.DisplayName, place, m.Health / (float)m.MaxHealth,
                    m == _selected || danger != null, m.IsDown, RunVisualPalette.Affinity(m.Affinity));
            }

            if (_selected != null)
            {
                _infoTitle = _selected.Definition.DisplayName;
                _infoBody = MinionInfo(_selected);
            }
            else if (_infoRoom >= 0)
            {
                _infoTitle = session.RoomName(_infoRoom);
                _infoBody = RoomInfo(_infoRoom);
            }
            if (_hovered != null || _hoveredRoom >= 0) _tooltipText = Describe(_hovered, _hoveredRoom);
        }

        private string BuildResultText()
        {
            if (session.IsRaidNode)
                return session.RaidResult switch
                {
                    RunDemoRoute.Result.Victory => "원정 성공 · 생존 부하 귀환",
                    RunDemoRoute.Result.Retreat => "원정 후퇴 · 귀환 완료",
                    RunDemoRoute.Result.Defeat => "원정 실패",
                    _ when !_battle.IsRaid => "원정 준비",
                    _ => _battle.IsRaidRetreating ? "원정 · 입구로 후퇴 중" : "원정 · 진지 대장 처치",
                };
            if (_battle.Outcome == RunBattleOutcome.Ongoing) return null;
            string title = _battle.Outcome == RunBattleOutcome.Defeat
                ? "심장이 무너졌습니다"
                : $"침입 {session.EncounterNumber}/{RunBattleFlow.EncounterLimit} 방어 성공";
            int unrecoveredGold = 0;
            int survivors = 0;
            foreach (RunCombatant actor in _battle.Combatants)
            {
                if (actor.Side == RunSide.Intruder) unrecoveredGold += actor.CarriedGold;
                else if (actor.IsActive) survivors++;
            }
            return $"<size=24>{title}</size>\n<size=18>미회수 금화 {unrecoveredGold} · 심장 {_battle.HeartHealth}/{_battle.Settings.HeartHealth} · 생존 부하 {survivors}</size>";
        }

        private string MinionInfo(RunCombatant m)
        {
            RunCombatantSO d = m.Definition;
            _text.Clear();
            _text.Append(RunBattleText.Activity(_battle, m)).Append('\n');
            _text.Append($"체력 {m.Health}/{m.MaxHealth} · 공격 {d.Attack} · {_battle.AttackInterval(m):0.#}초마다");
            if (d.Skill != null)
                _text.Append('\n').Append(m.IsSkillReady ? $"{d.Skill.DisplayName} 준비됨" : $"{d.Skill.DisplayName} {m.SkillCooldownLeft:0.0}초 뒤");
            _text.Append("\n방을 누르면 걸어간다 · 우클릭 해제");
            return _text.ToString();
        }

        private string RoomInfo(int index)
        {
            RunRoom room = _battle.Rooms[index];
            (string status, Color _) = RoomStatus(room);
            _text.Clear();
            _text.Append(status);
            if (room.HasSystem) _text.Append($"\n내구 {Mathf.CeilToInt(room.Durability)}/{room.MaxDurability} · 마력 {room.Mana}/{room.MaxMana}");
            int trap = _battle.TrapDamage(index);
            if (trap > 0) _text.Append($"\n들어온 적 전원에게 {trap} · 쿨 {_battle.Settings.TrapCooldownSeconds:0.#}초");
            if (room.MaxMana > 0) _text.Append("\n이름판 칸을 눌러 마력을 넣고 뺀다");
            return _text.ToString();
        }

        private string Describe(RunCombatant combatant, int room)
        {
            if (combatant != null)
            {
                RunCombatantSO d = combatant.Definition;
                _text.Clear();
                _text.Append($"<b>{d.DisplayName}</b> · {(d.IsRanged ? "원거리" : "근접")}\n");
                _text.Append($"체력 {combatant.Health}/{combatant.MaxHealth} · 공격 {d.Attack} · {_battle.AttackInterval(combatant):0.#}초마다");
                if (combatant.Side == RunSide.Minion && d.RepairPerSecond > 0f) _text.Append($" · 수리 {d.RepairPerSecond:0.#}/초");
                if (d.Skill != null)
                    _text.Append($"\n{d.Skill.DisplayName}: {d.Skill.Description}");
                _text.Append('\n').Append(RunBattleText.Activity(_battle, combatant));
                return _text.ToString();
            }
            if (room < 0) return null;
            RunRoom r = _battle.Rooms[room];
            return $"<b>{session.RoomName(room)}</b>\n{RoomInfo(room)}"
                + (_selected != null ? "\n누르면 고른 부하가 이 방으로 간다" : string.Empty);
        }

        private bool ControlledDoorBroken(int room)
        {
            foreach (RunDoor door in _battle.Doors)
            {
                if (door.ControllerRoom == room && door.IsBrokenOpen) return true;
            }
            return false;
        }

        // 방을 치는 적(공병·방 목표 적)이 있으면 이름판에 붉게 보인다(2026-11-11 목업 A안 승인).
        private bool IsRoomUnderAttack(int roomIndex)
        {
            foreach (RunCombatant actor in _battle.Combatants)
                if (actor.IsOnField && actor.Side == RunSide.Intruder && actor.Activity == RunActionKind.AttackRoom && _battle.RoomOf(actor) == roomIndex)
                    return true;
            return false;
        }

        private (string, Color) RoomStatus(RunRoom room)
        {
            switch (room.Kind)
            {
                case RunRoomKind.Entrance: return ("적이 들어오는 곳", HudPalette.TextMuted);
                case RunRoomKind.Heart: return ($"체력 {_battle.HeartHealth}", HudPalette.Danger);
                case RunRoomKind.Vault: return ($"금화 {_battle.VaultGold}", HudPalette.Accent);
            }
            if (room.IsBroken) return ("고장", HudPalette.Danger);
            if (IsRoomUnderAttack(room.Index)) return ("공격받는 중", HudPalette.Danger);
            if (room.Mana > 0 && !_battle.IsConnected(room.Index)) return ("끊김", HudPalette.Warn);
            if (room.Mana == 0) return ("꺼짐", HudPalette.TextMuted);
            if (room.Kind == RunRoomKind.Trap && room.TrapCooldown > 0f)
                return ($"쿨 {Mathf.CeilToInt(room.TrapCooldown)}초", HudPalette.Warn);
            if (room.Kind == RunRoomKind.Trap) return ("켜짐", HudPalette.Gain);
            if (room.Kind == RunRoomKind.DoorControl && ControlledDoorBroken(room.Index))
                return ("문 부서짐", HudPalette.Warn);
            return ("켜짐", HudPalette.Gain);
        }
    }
}

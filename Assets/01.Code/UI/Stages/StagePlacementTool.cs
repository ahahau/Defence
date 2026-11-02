using System;
using Code.Stages;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Code.UI.Stages
{
    /// <summary>배치 도구 상태와 지도 입력/커서 표시. 가능 여부는 StagePlacement에 묻는다.</summary>
    public sealed class StagePlacementTool
    {
        private readonly StageSession _session;
        private readonly Camera _camera;
        private readonly SpriteRenderer _cursor;
        private readonly Vector3 _unitScale;
        public StageOption Selected { get; private set; }
        public bool Placing { get; private set; } = true;
        public bool Demolishing { get; private set; }
        public string Hint { get; private set; } = string.Empty;
        /// <summary>커서가 올라간 지형 칸의 이름과 효과(예: "물웅덩이: 불 화로 설치 불가 · …"). 지형이 아니면 빈 문자열.</summary>
        public string TerrainNote { get; private set; } = string.Empty;
        private StageTerrainRule _noteRule;
        /// <summary>명령을 줄 부하. 철거 도구가 아니면 배치한 부하를 눌러 고르고, 우클릭으로 푼다.</summary>
        public StageActor SelectedUnit => _selectedUnit != null && _selectedUnit.IsAlive ? _selectedUnit : null;
        private StageActor _selectedUnit;
        public event Action Changed;

        public StagePlacementTool(StageSession session, Camera camera, SpriteRenderer cursor, Vector3 unitScale)
        {
            _session = session;
            _camera = camera;
            _cursor = cursor;
            _unitScale = unitScale;
        }

        public bool IsSelected(StageOption option) => _session.Phase == StagePhase.Preparing && Placing && Selected == option;

        public void Select(StageOption option)
        {
            // 다른 도구를 고르면 부하 선택(명령 막대)을 푼다
            _selectedUnit = null;
            Selected = option;
            Placing = true;
            Demolishing = false;
            Hint = string.Empty;
            _cursor.gameObject.SetActive(false);
            Changed?.Invoke();
        }

        public void SelectDemolition()
        {
            _selectedUnit = null;
            Placing = false;
            Demolishing = true;
            Hint = string.Empty;
            _cursor.gameObject.SetActive(false);
            Changed?.Invoke();
        }

        public void Cancel()
        {
            Selected = null;
            _selectedUnit = null;
            Placing = false;
            Demolishing = false;
            Hint = string.Empty;
            _cursor.gameObject.SetActive(false);
            Changed?.Invoke();
        }

        public void Hide()
        {
            // 씬 종료 때 커서가 HUD보다 먼저 파괴될 수 있다.
            if (_cursor != null)
                _cursor.gameObject.SetActive(false);
            SetHint(string.Empty);
            SetTerrain(null);
        }

        public void Tick(bool pointerFree)
        {
            // 웨이브가 시작되면 부하 선택을 푼다. 다음 준비 단계에 막대가 저절로 다시 뜨지 않게 한다.
            if (_session.Phase != StagePhase.Preparing)
                _selectedUnit = null;
            if (_session.Phase != StagePhase.Preparing || !pointerFree || Mouse.current == null)
            {
                Hide();
                return;
            }
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                Cancel();
                return;
            }
            Vector3 world = _camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2Int cell = _session.Board.Cell(world);
            bool click = Mouse.current.leftButton.wasPressedThisFrame;
            // 부하가 선 칸을 누르면 배치 대신 그 부하를 고른다(철거 도구는 원래대로 철거한다)
            if (click && !Demolishing && TrySelectUnit(cell))
                return;
            if (!Placing && !Demolishing)
            {
                Hide();
                return;
            }
            StagePlacementCheck check = Demolishing ? _session.Placement.CheckDemolition(cell)
                : _session.Placement.Check(cell, Selected, Selected == null);
            Show(check);
            SetHint(check.Allowed ? string.Empty : check.Reason);
            SetTerrain(_session.Board.Terrain.RuleAt(cell));
            if (!click)
                return;
            // 실행 직전에도 같은 검사를 거친다. 실패 문구는 설치/철거 쪽이 한 번만 알린다.
            if (Demolishing)
                _session.Demolish(cell);
            else
                _session.TryPlace(cell, Selected, Selected == null);
        }

        private bool TrySelectUnit(Vector2Int cell)
        {
            foreach (StageActor actor in _session.Board.Actors)
            {
                if (actor == null || actor.IsEnemy || !actor.IsAlive || _session.Board.Cell(actor.transform.position) != cell)
                    continue;
                _selectedUnit = actor;
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        private void Show(StagePlacementCheck check)
        {
            float size = _session.Definition.CellSize;
            _cursor.transform.position = _session.Board.World(check.Anchor)
                + new Vector3(check.Footprint.x - 1, check.Footprint.y - 1, 0) * (size * 0.5f);
            _cursor.transform.localScale = new Vector3(_unitScale.x * size * check.Footprint.x,
                _unitScale.y * size * check.Footprint.y, _unitScale.z);
            Color tint = !check.Allowed ? HudPalette.Danger : Demolishing ? HudPalette.Warn : HudPalette.Gain;
            tint.a = 0.3f;
            _cursor.color = tint;
            _cursor.gameObject.SetActive(true);
        }

        private void SetHint(string reason)
        {
            if (Hint == reason)
                return;
            Hint = reason;
            Changed?.Invoke();
        }

        // 같은 지형 위에서는 글을 다시 만들지 않는다(커서는 매 프레임 움직인다)
        private void SetTerrain(StageTerrainRule rule)
        {
            if (rule == _noteRule)
                return;
            _noteRule = rule;
            TerrainNote = rule != null ? $"{rule.Label}: {rule.Describe()}" : string.Empty;
            Changed?.Invoke();
        }
    }
}

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
            Selected = option;
            Placing = true;
            Demolishing = false;
            Hint = string.Empty;
            _cursor.gameObject.SetActive(false);
            Changed?.Invoke();
        }

        public void SelectDemolition()
        {
            Placing = false;
            Demolishing = true;
            Hint = string.Empty;
            _cursor.gameObject.SetActive(false);
            Changed?.Invoke();
        }

        public void Cancel()
        {
            Selected = null;
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
        }

        public void Tick(bool pointerFree)
        {
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
            if (!Placing && !Demolishing)
            {
                Hide();
                return;
            }
            Vector3 world = _camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2Int cell = _session.Board.Cell(world);
            StagePlacementCheck check = Demolishing ? _session.Placement.CheckDemolition(cell)
                : _session.Placement.Check(cell, Selected, Selected == null);
            Show(check);
            SetHint(check.Allowed ? string.Empty : check.Reason);
            if (!Mouse.current.leftButton.wasPressedThisFrame)
                return;
            // 실행 직전에도 같은 검사를 거친다. 실패 문구는 설치/철거 쪽이 한 번만 알린다.
            if (Demolishing)
                _session.Demolish(cell);
            else
                _session.TryPlace(cell, Selected, Selected == null);
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
    }
}
